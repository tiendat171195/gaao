# -*- coding: utf-8 -*-
"""
pawfit_kit.py — Bộ dựng thú PawFit bằng script Blender (Phase 0.5 — POC hình ảnh).

Chạy bên trong Blender (bpy), được import bởi render_demo.py và export_kit.py.

Ý tưởng chính (khớp hệ DNA trong game):
  * Thân = 1 base mesh (Baby) dựng từ metaball rồi remesh cho lưới đều.
    Blendshape (shape key) của thân: to_adult_fire, head_big, body_chubby, legs_long.
    Mỗi blendshape tính như "xương ảo": mỗi khối metaball có 1 phép biến đổi,
    đỉnh thuộc khối nào (theo trường metaball) thì đi theo khối đó.
  * Tai / đuôi / sừng / mắt là bộ phận rời, sinh theo tham số với lưới CỐ ĐỊNH
    (mọi kiểu dùng chung số đỉnh) => morph mượt giữa các kiểu được.
  * Vùng màu (bụng sáng, chân tối, đốm/sọc) lưu ở vertex color "Masks"
    => thú lớn lên thì hoa văn đi theo cơ thể. Lông (particle hair) đọc màu này.
"""

import bpy
import bmesh
import math
import random
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree


# ============================================================================
# 1. Tiện ích chung
# ============================================================================

def lin(hexstr):
    """Đổi '#RRGGBB' (sRGB) sang màu tuyến tính RGBA cho Cycles."""
    h = hexstr.lstrip('#')
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def C(v):
    """Nhận màu dạng '#hex' hoặc tuple tuyến tính -> tuple tuyến tính."""
    return lin(v) if isinstance(v, str) else tuple(v)


def mixc(a, b, t):
    a, b = C(a), C(b)
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(4))


def mulc(c, k):
    c = C(c)
    return (min(1.0, c[0] * k), min(1.0, c[1] * k), min(1.0, c[2] * k), 1.0)


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    if e0 == e1:
        return 0.0 if x < e0 else 1.0
    t = (x - e0) / (e1 - e0)
    t = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
    return t * t * (3.0 - 2.0 * t)


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def activate(ob):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)


def apply_mod(ob, mod):
    activate(ob)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def frame_matrix(origin, x, y, z):
    """Ma trận 4x4 từ gốc + 3 trục (đặt theo cột)."""
    m = Matrix.Identity(4)
    for i in range(3):
        m[i][0], m[i][1], m[i][2], m[i][3] = x[i], y[i], z[i], origin[i]
    return m


def make_frame(z_axis, y_hint):
    """Khung trực chuẩn tay phải: trục z cho trước, trục y gần với y_hint."""
    z = z_axis.normalized()
    y = y_hint - z * y_hint.dot(z)
    if y.length < 1e-6:
        y = Vector((0.0, 0.0, 1.0)) if abs(z.z) < 0.9 else Vector((0.0, 1.0, 0.0))
        y = y - z * y.dot(z)
    y.normalize()
    return y.cross(z), y, z


def new_mesh_object(name, verts, faces, uvs=None, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.update()
    if uvs is not None:
        uvl = me.uv_layers.new(name="UVMap")
        lv = [0] * len(me.loops)
        me.loops.foreach_get('vertex_index', lv)
        flat = []
        for vi in lv:
            flat.extend(uvs[vi])
        uvl.data.foreach_set('uv', flat)
    if smooth:
        me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    return link(bpy.data.objects.new(name, me))


def set_masks(ob, R, G, B, name="Masks"):
    """Ghi vertex color (miền góc mặt) từ 3 kênh theo đỉnh R, G, B trong [0, 1]."""
    me = ob.data
    old = me.color_attributes.get(name)
    if old is not None:
        me.color_attributes.remove(old)
    attr = me.color_attributes.new(name, 'BYTE_COLOR', 'CORNER')
    lv = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', lv)
    flat = []
    for vi in lv:
        flat.extend((R[vi], G[vi], B[vi], 1.0))
    attr.data.foreach_set('color', flat)


def set_masks_loops(ob, loop_rgb, name="Masks"):
    attr = ob.data.color_attributes.new(name, 'BYTE_COLOR', 'CORNER')
    flat = []
    for r, g, b in loop_rgb:
        flat.extend((r, g, b, 1.0))
    attr.data.foreach_set('color', flat)


def set_vgroup(ob, name, weights):
    vg = ob.vertex_groups.get(name) or ob.vertex_groups.new(name=name)
    buckets = {}
    for i, w in enumerate(weights):
        q = round(max(0.0, min(1.0, w)) * 256) / 256.0
        buckets.setdefault(q, []).append(i)
    for q, idx in buckets.items():
        vg.add(idx, q, 'REPLACE')
    return vg


def evaluated_coords(ob):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    m = ev.to_mesh()
    flat = [0.0] * (3 * len(m.vertices))
    m.vertices.foreach_get('co', flat)
    ev.to_mesh_clear()
    return [Vector(flat[3 * i:3 * i + 3]) for i in range(len(flat) // 3)]


def ray_hit(bvh, origin, direction):
    loc, nrm, _, _ = bvh.ray_cast(origin, direction)
    return loc, nrm


def surface_from_center(bvh, center, d, dist=3.0):
    """Bắn tia từ ngoài vào tâm theo hướng d -> điểm trên bề mặt + pháp tuyến."""
    loc, nrm = ray_hit(bvh, center + d * dist, -d)
    if loc is None:
        return center + d * 0.25, d.copy()
    if nrm.dot(d) < 0:
        nrm = -nrm
    return loc, nrm


def tube_mesh(points, radii, ring=12, closed=False, cap_end=True, cap_start=False,
              flatten=None, up=Vector((0.0, 0.0, 1.0))):
    """Ống chạy theo đường tâm `points` (bán kính `radii`). UV: u quanh ống, v dọc ống."""
    n = len(points)
    tans = []
    for i in range(n):
        if closed:
            t = points[(i + 1) % n] - points[i - 1]
        else:
            t = points[min(i + 1, n - 1)] - points[max(i - 1, 0)]
        tans.append(t.normalized())
    nv = up - tans[0] * up.dot(tans[0])
    if nv.length < 1e-6:
        nv = Vector((1.0, 0.0, 0.0)) - tans[0] * tans[0].x
    nv.normalize()
    verts, uvs, faces = [], [], []
    for i in range(n):
        if i > 0:  # khung song song (parallel transport) để ống không bị xoắn
            nv = nv - tans[i] * nv.dot(tans[i])
            nv.normalize()
        bv = tans[i].cross(nv)
        fx, fy = (1.0, 1.0) if flatten is None else flatten[i]
        for k in range(ring):
            a = 2.0 * math.pi * k / ring
            verts.append(points[i] + nv * (math.cos(a) * radii[i] * fx)
                         + bv * (math.sin(a) * radii[i] * fy))
            uvs.append((k / ring, i / (n if closed else (n - 1))))
    rows = n if closed else n - 1
    for i in range(rows):
        i2 = (i + 1) % n
        for k in range(ring):
            k2 = (k + 1) % ring
            faces.append((i * ring + k, i * ring + k2, i2 * ring + k2, i2 * ring + k))
    if cap_end and not closed:
        tip = len(verts)
        verts.append(points[-1] + tans[-1] * radii[-1] * 0.6)
        uvs.append((0.5, 1.0))
        b0 = (n - 1) * ring
        for k in range(ring):
            faces.append((b0 + k, b0 + (k + 1) % ring, tip))
    if cap_start and not closed:
        st = len(verts)
        verts.append(points[0] - tans[0] * radii[0] * 0.3)
        uvs.append((0.5, 0.0))
        for k in range(ring):
            faces.append(((k + 1) % ring, k, st))
    return verts, faces, uvs


# ============================================================================
# 2. Thân thú: metaball -> base mesh + blendshape
# ============================================================================

K_VIS = 0.575   # bán kính nhìn thấy / bán kính metaball (stiffness 2, threshold 0.6)
STIFF = 2.0


def _ball(name, co, r, bone):
    return dict(name=name, kind='BALL', co=Vector(co), r=r, bone=bone)


def _ell(name, co, semi, bone):
    return dict(name=name, kind='ELLIPSOID', co=Vector(co), semi=Vector(semi), bone=bone)


def _cap(name, a, b, r, bone):
    return dict(name=name, kind='CAPSULE', a=Vector(a), b=Vector(b), r=r, bone=bone)


def elems_baby():
    """Khối dựng thân Baby (đơn vị mét, mặt thú nhìn về -Y, Z hướng lên)."""
    E = [
        _ell('torso', (0, 0.06, 0.40), (0.21, 0.28, 0.20), 'spine'),
        _ball('chest', (0, -0.14, 0.43), 0.17, 'chest'),
        _ball('hips', (0, 0.24, 0.39), 0.17, 'hips'),
        _cap('neck', (0, -0.17, 0.52), (0, -0.26, 0.66), 0.12, 'neck'),
        _ball('head', (0, -0.30, 0.80), 0.27, 'head'),
        _ell('forehead', (0, -0.33, 0.90), (0.21, 0.18, 0.16), 'head'),
        _ell('muzzle', (0, -0.50, 0.73), (0.10, 0.09, 0.075), 'head'),
    ]
    for s, sd in ((1, 'R'), (-1, 'L')):
        E += [
            _ball('cheek' + sd, (0.15 * s, -0.40, 0.73), 0.12, 'head'),
            _cap('legF' + sd, (0.12 * s, -0.15, 0.36), (0.12 * s, -0.17, 0.11), 0.065, 'legF' + sd),
            _ball('pawF' + sd, (0.12 * s, -0.19, 0.065), 0.075, 'legF' + sd),
            _ball('thigh' + sd, (0.13 * s, 0.25, 0.33), 0.12, 'legB' + sd),
            _cap('legB' + sd, (0.13 * s, 0.25, 0.30), (0.13 * s, 0.22, 0.11), 0.07, 'legB' + sd),
            _ball('pawB' + sd, (0.13 * s, 0.20, 0.065), 0.078, 'legB' + sd),
        ]
    return E


def elems_adult_fire():
    """Khối dựng thân Adult hệ Lửa (cùng tên, cùng thứ tự với Baby để morph)."""
    E = [
        _ell('torso', (0, 0.08, 0.62), (0.24, 0.40, 0.22), 'spine'),
        _ball('chest', (0, -0.22, 0.68), 0.22, 'chest'),
        _ball('hips', (0, 0.36, 0.62), 0.19, 'hips'),
        _cap('neck', (0, -0.26, 0.78), (0, -0.38, 0.98), 0.13, 'neck'),
        _ball('head', (0, -0.44, 1.10), 0.25, 'head'),
        _ell('forehead', (0, -0.48, 1.19), (0.19, 0.17, 0.13), 'head'),
        _ell('muzzle', (0, -0.70, 1.02), (0.10, 0.14, 0.08), 'head'),
    ]
    for s, sd in ((1, 'R'), (-1, 'L')):
        E += [
            _ball('cheek' + sd, (0.15 * s, -0.55, 1.03), 0.11, 'head'),
            _cap('legF' + sd, (0.14 * s, -0.22, 0.58), (0.14 * s, -0.25, 0.12), 0.07, 'legF' + sd),
            _ball('pawF' + sd, (0.14 * s, -0.27, 0.07), 0.08, 'legF' + sd),
            _ball('thigh' + sd, (0.15 * s, 0.38, 0.52), 0.14, 'legB' + sd),
            _cap('legB' + sd, (0.15 * s, 0.40, 0.48), (0.15 * s, 0.36, 0.12), 0.075, 'legB' + sd),
            _ball('pawB' + sd, (0.15 * s, 0.33, 0.07), 0.085, 'legB' + sd),
        ]
    return E


FORM_BODY = {   # tỉ lệ thân của 5 dạng Adult (so với Adult Lửa) — bộ thú Gen 1
    'fire':   dict(h=1.00, leg_r=1.00, torso=(1.00, 1.00, 1.00), chest=1.00, head=1.00, muzzle=(1.00, 1.00, 1.00)),
    'water':  dict(h=0.80, leg_r=0.95, torso=(1.02, 1.15, 0.92), chest=0.92, head=1.02, muzzle=(1.08, 0.85, 1.00)),
    'light':  dict(h=1.12, leg_r=0.85, torso=(0.90, 0.92, 0.90), chest=1.08, head=0.96, muzzle=(0.92, 0.90, 0.92)),
    'steel':  dict(h=0.94, leg_r=1.35, torso=(1.25, 1.00, 1.18), chest=1.22, head=1.08, muzzle=(1.22, 0.88, 1.10)),
    'shadow': dict(h=1.08, leg_r=0.85, torso=(0.88, 1.06, 0.88), chest=0.94, head=0.92, muzzle=(0.88, 1.15, 0.92)),
}
FORMS = tuple(FORM_BODY.keys())


def elems_adult(form):
    """Khối dựng thân Adult của 1 dạng: lấy Adult Lửa rồi đổi chiều cao chân, độ to thân / đầu / mõm."""
    P = FORM_BODY[form]
    E = _copy(elems_adult_fire())
    h = P['h']
    for e in E:
        n = e['name']
        if n.startswith('paw'):
            e['r'] *= P['leg_r'] ** 0.5
            continue
        if n.startswith('leg'):
            e['a'] = Vector((e['a'].x, e['a'].y, e['a'].z * h))   # đầu trên của chân theo chiều cao thân
            e['r'] *= P['leg_r']
            continue
        for key in ('co', 'a', 'b'):
            if key in e:
                e[key] = Vector((e[key].x, e[key].y, e[key].z * h))
        if n == 'torso':
            e['semi'] = Vector((e['semi'].x * P['torso'][0], e['semi'].y * P['torso'][1], e['semi'].z * P['torso'][2]))
        elif n == 'chest':
            e['r'] *= P['chest']
        elif n.startswith('thigh'):
            e['r'] *= P['leg_r'] ** 0.7
        elif n == 'muzzle':
            e['semi'] = Vector((e['semi'].x * P['muzzle'][0], e['semi'].y * P['muzzle'][1], e['semi'].z * P['muzzle'][2]))
        elif e['bone'] == 'head':
            if 'semi' in e:
                e['semi'] = e['semi'] * P['head']
            else:
                e['r'] *= P['head']
    return E


def _copy(E):
    out = []
    for e in E:
        d = dict(e)
        for k in ('co', 'semi', 'a', 'b'):
            if k in d:
                d[k] = d[k].copy()
        out.append(d)
    return out


def elems_head_big(E, k=1.17):
    """DNA tỉ lệ: đầu to hơn (phóng quanh gốc cổ)."""
    E = _copy(E)
    piv = Vector((0.0, -0.24, 0.62))
    for e in E:
        if e['bone'] == 'head':
            for key in ('co', 'a', 'b'):
                if key in e:
                    e[key] = piv + (e[key] - piv) * k
            if 'semi' in e:
                e['semi'] = e['semi'] * k
            else:
                e['r'] *= k
    return E


def elems_chubby(E):
    """DNA tỉ lệ: thân mũm mĩm."""
    E = _copy(E)
    for e in E:
        n = e['name']
        if n == 'torso':
            e['semi'] = Vector((e['semi'].x * 1.22, e['semi'].y * 1.05, e['semi'].z * 1.15))
        elif n in ('chest', 'hips'):
            e['r'] *= 1.13
        elif n.startswith('thigh'):
            e['r'] *= 1.12
    return E


def elems_legs_long(E, dz=0.10):
    """DNA tỉ lệ: chân dài (bàn chân giữ nguyên, thân nâng lên)."""
    E = _copy(E)
    up = Vector((0.0, 0.0, dz))
    for e in E:
        n = e['name']
        if n.startswith('paw'):
            continue
        if n.startswith('leg'):
            e['a'] = e['a'] + up
            continue
        for key in ('co', 'a', 'b'):
            if key in e:
                e[key] = e[key] + up
    return E


def _center(e):
    return e['co'] if 'co' in e else (e['a'] + e['b']) * 0.5


def elem_field(e, p):
    """Trường metaball giống Blender: s * (1 - d^2/R^2)^3."""
    if e['kind'] == 'BALL':
        R = e['r'] / K_VIS
        d2 = (p - e['co']).length_squared / (R * R)
    elif e['kind'] == 'ELLIPSOID':
        q = p - e['co']
        s = e['semi']
        R = 1.0 / K_VIS
        d2 = ((q.x / s.x) ** 2 + (q.y / s.y) ** 2 + (q.z / s.z) ** 2) / (R * R)
    else:
        a, b = e['a'], e['b']
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
        R = e['r'] / K_VIS
        d2 = (p - (a + ab * t)).length_squared / (R * R)
    x = 1.0 - d2
    return STIFF * x * x * x if x > 0.0 else 0.0


def elem_matrix(e):
    if e['kind'] == 'BALL':
        r = e['r']
        return Matrix.Translation(e['co']) @ Matrix.Diagonal((r, r, r, 1.0))
    if e['kind'] == 'ELLIPSOID':
        s = e['semi']
        return Matrix.Translation(e['co']) @ Matrix.Diagonal((s.x, s.y, s.z, 1.0))
    a, b, r = e['a'], e['b'], e['r']
    d = b - a
    q = Vector((1.0, 0.0, 0.0)).rotation_difference(d.normalized())
    return (Matrix.Translation((a + b) * 0.5) @ q.to_matrix().to_4x4()
            @ Matrix.Diagonal((d.length * 0.5 + r, r, r, 1.0)))


def add_metaball_elements(mb, E):
    for e in E:
        if e['kind'] == 'BALL':
            m = mb.elements.new(type='BALL')
            m.co = e['co']
            m.radius = e['r'] / K_VIS
        elif e['kind'] == 'ELLIPSOID':
            m = mb.elements.new(type='ELLIPSOID')
            m.co = e['co']
            m.radius = 1.0 / K_VIS
            m.size_x, m.size_y, m.size_z = e['semi'].x, e['semi'].y, e['semi'].z
        else:
            d = e['b'] - e['a']
            m = mb.elements.new(type='CAPSULE')
            m.co = (e['a'] + e['b']) * 0.5
            m.radius = e['r'] / K_VIS
            m.size_x = d.length * 0.5
            m.rotation = Vector((1.0, 0.0, 0.0)).rotation_difference(d.normalized())
        m.stiffness = STIFF


def _centroid(P, w, thr):
    acc = Vector((0.0, 0.0, 0.0))
    tot = 0
    for p, wi in zip(P, w):
        if wi >= thr:
            acc += p
            tot += 1
    return acc / max(tot, 1)


def _wcentroid(P, w, power=4.0):
    """Tâm có trọng số w^power: luôn xác định (khác _centroid có thể không có đỉnh nào vượt ngưỡng)."""
    acc = Vector((0.0, 0.0, 0.0))
    tot = 0.0
    for p, wi in zip(P, w):
        k = wi ** power
        acc += p * k
        tot += k
    return acc / tot if tot > 1e-9 else acc


def _mean_dist(P, w, thr, c):
    ds = [(p - c).length for p, wi in zip(P, w) if wi >= thr]
    return sum(ds) / max(len(ds), 1)


def _add_lbs_key(ob, name, E0, E1, W, co):
    """Tạo blendshape: mỗi đỉnh đi theo phép biến đổi của các khối nó thuộc về."""
    T = [elem_matrix(E1[i]) @ elem_matrix(E0[i]).inverted() for i in range(len(E0))]
    out = []
    for vi, p in enumerate(co):
        acc = Vector((0.0, 0.0, 0.0))
        for i, w in W[vi]:
            acc += (T[i] @ p) * w
        out.extend((acc.x, acc.y, acc.z))
    kb = ob.shape_key_add(name=name, from_mix=False)
    kb.data.foreach_set('co', out)
    kb.value = 0.0


_BASES = {}
LOD0_VOXEL = 0.0095   # ~38k đỉnh: render / máy mạnh
LOD1_VOXEL = 0.016    # ~13k đỉnh: điện thoại


def reset_cache():
    _BASES.clear()


def ensure_base(voxel=LOD0_VOXEL):
    """Dựng base mesh Baby + blendshape ở 1 mức chi tiết (chỉ làm 1 lần mỗi mức)."""
    ck = round(voxel, 5)          # khóa cache (không dùng tên 'key': vòng lặp blendshape bên dưới dùng rồi)
    if ck in _BASES:
        return _BASES[ck]
    E = elems_baby()
    mb = bpy.data.metaballs.new("PF_BodyMeta")
    mb.resolution = 0.014
    mb.render_resolution = 0.014
    mb.threshold = 0.6
    add_metaball_elements(mb, E)
    ob = link(bpy.data.objects.new("PF_BodyMeta", mb))
    activate(ob)
    bpy.ops.object.convert(target='MESH')
    body = bpy.context.view_layer.objects.active
    for o in list(bpy.context.scene.objects):      # dọn metaball còn sót lại
        if o.type == 'META':
            bpy.data.objects.remove(o, do_unlink=True)
    body.name = "PF_BaseBody_%d" % int(voxel * 10000)
    rm = body.modifiers.new("rm", 'REMESH')
    rm.mode = 'VOXEL'
    rm.voxel_size = voxel
    apply_mod(body, rm)
    sm = body.modifiers.new("sm", 'SMOOTH')
    sm.factor = 0.6
    sm.iterations = 6
    apply_mod(body, sm)
    me = body.data
    n = len(me.vertices)
    flat = [0.0] * (3 * n)
    me.vertices.foreach_get('co', flat)
    co = [Vector(flat[3 * i:3 * i + 3]) for i in range(n)]
    nflat = [0.0] * (3 * n)
    me.vertices.foreach_get('normal', nflat)
    nrm = [Vector(nflat[3 * i:3 * i + 3]) for i in range(n)]
    polys = [tuple(p.vertices) for p in me.polygons]
    # trọng số mỗi đỉnh theo từng khối (chuẩn hóa) -> dùng làm "xương ảo"
    W = []
    for p in co:
        fs = [(i, elem_field(e, p)) for i, e in enumerate(E)]
        fs = [(i, f) for i, f in fs if f > 1e-6]
        tot = sum(f for _, f in fs)
        if tot <= 0.0:
            j = min(range(len(E)), key=lambda i: (p - _center(E[i])).length)
            W.append([(j, 1.0)])
        else:
            W.append([(i, f / tot) for i, f in fs])
    bones = sorted(set(e['bone'] for e in E))
    bone_w = {b: [0.0] * n for b in bones}
    for vi, lst in enumerate(W):
        for i, w in lst:
            bone_w[E[i]['bone']][vi] += w
    body.shape_key_add(name='Basis', from_mix=False)
    for key, target in ([('to_adult_' + f, elems_adult(f)) for f in FORMS] + [
                        ('head_big', elems_head_big(E)),
                        ('body_chubby', elems_chubby(E)),
                        ('legs_long', elems_legs_long(E))]):
        _add_lbs_key(body, key, E, target, W, co)
    me.use_fake_user = True
    bpy.context.scene.collection.objects.unlink(body)
    hc = _centroid(co, bone_w['head'], 0.95)
    hr = _mean_dist(co, bone_w['head'], 0.95, hc)
    _BASES[ck] = dict(mesh=me, co=co, nrm=nrm, polys=polys, W=W, bone_w=bone_w,
                      elems=E, head_r=hr, nverts=n, voxel=voxel)
    print("PF_BASE voxel=%.4f verts=%d head_r=%.3f" % (voxel, n, hr))
    return _BASES[ck]


# ============================================================================
# 3. Vùng màu + bản đồ lông của thân
# ============================================================================

MUZZLE_C = Vector((0.0, -0.52, 0.72))
CHEEK_C = (Vector((0.15, -0.41, 0.71)), Vector((-0.15, -0.41, 0.71)))


def body_masks(co, dna, rng):
    """R = vùng sáng (bụng, mõm, má, ngực) | G = chân/lưng tối | B = hoa văn (đốm / sọc)."""
    n = len(co)
    R = [0.0] * n
    G = [0.0] * n
    B = [0.0] * n
    pat = dna.get('pattern', 'solid')
    spots = []
    if pat == 'spots':
        cand = list(range(n))
        rng.shuffle(cand)
        for i in cand:
            p = co[i]
            if p.z < 0.30 or p.y < -0.42:      # không đặt đốm ở bụng / mặt
                continue
            if any((p - c).length < r + 0.03 for c, r in spots):
                continue
            spots.append((p, rng.uniform(0.07, 0.11)))
            if len(spots) >= dna.get('spot_count', 22):
                break
    belly_amt = dna.get('belly_amount', 1.0)
    paws = dna.get('paws_dark', 0.0)
    for i, p in enumerate(co):
        ax = abs(p.x)
        belly = (smoothstep(0.37, 0.27, p.z) * smoothstep(0.21, 0.10, ax)
                 * smoothstep(0.40, 0.26, p.y) * smoothstep(-0.32, -0.18, p.y))
        chest = (smoothstep(-0.06, -0.21, p.y) * smoothstep(0.66, 0.50, p.z)
                 * smoothstep(0.19, 0.08, ax) * smoothstep(0.24, 0.32, p.z))
        muzzle = smoothstep(0.165, 0.085, (p - MUZZLE_C).length)
        cheek = (max(smoothstep(0.13, 0.06, (p - c).length) for c in CHEEK_C)
                 * smoothstep(0.77, 0.69, p.z))
        r = max(belly, chest, muzzle, cheek) * belly_amt
        R[i] = r
        dorsal = smoothstep(0.44, 0.62, p.z) * smoothstep(-0.22, -0.06, p.y)   # lưng tối hơn cho có khối
        G[i] = max(smoothstep(0.16, 0.09, p.z) * paws, dorsal * dna.get('dorsal', 0.3) * (1.0 - r))
        if pat == 'spots':
            m = 0.0
            for c, rad in spots:
                d = (p - c).length
                if d < rad:
                    m = max(m, smoothstep(rad, rad * 0.75, d))
            B[i] = m * (1.0 - r)
        elif pat == 'stripes':
            nz = noise.noise(p * 3.5)
            band = math.sin(2.0 * math.pi * (p.y * 6.0 + 0.35 * nz) + 0.6)
            body_up = smoothstep(0.30, 0.44, p.z) * smoothstep(-0.30, -0.16, p.y)
            head_top = smoothstep(0.80, 0.90, p.z) * smoothstep(-0.12, -0.24, p.y)
            legs = smoothstep(0.30, 0.20, p.z) * smoothstep(0.08, 0.12, p.z)
            leg_band = math.sin(2.0 * math.pi * (p.z * 10.0 + 0.3 * nz))
            m = max(smoothstep(0.50, 0.85, band) * max(body_up, head_top),
                    smoothstep(0.55, 0.90, leg_band) * legs * 0.85)
            B[i] = m * (1.0 - r)
    return R, G, B


def body_fur_maps(co, P, feats):
    """Mật độ + độ dài lông theo vùng (mõm ngắn, má xù, quanh mắt không có lông)."""
    n = len(co)
    dens = [1.0] * n
    leng = [0.7] * n
    s = feats['s']
    for i in range(n):
        p = co[i]
        q = P[i]
        L = 0.62
        L = lerp(0.38, L, smoothstep(0.16, 0.32, p.z))                       # chân lông ngắn
        head = smoothstep(-0.12, -0.20, p.y) * smoothstep(0.56, 0.64, p.z)
        L = lerp(L, 0.55, head)                                              # đầu lông ngắn hơn thân
        L = lerp(0.24, L, smoothstep(0.075, 0.16, (p - MUZZLE_C).length))    # mõm lông ngắn
        dc = min((p - c).length for c in CHEEK_C)
        L = max(L, 1.0 - smoothstep(0.05, 0.14, dc))                         # má xù
        chest = (smoothstep(-0.05, -0.20, p.y) * smoothstep(0.64, 0.48, p.z)
                 * smoothstep(0.24, 0.32, p.z))
        L = max(L, 0.9 * chest)                                              # ngực xù
        d = 1.0
        for ec, er in feats['eyes']:
            de = (q - ec).length / er
            d = min(d, smoothstep(1.0, 1.4, de))
            L = min(L, lerp(0.10, 1.0, smoothstep(1.05, 2.3, de)))
        dn = (q - feats['nose']).length / s
        d = min(d, smoothstep(0.045, 0.08, dn))
        L = min(L, lerp(0.2, 1.0, smoothstep(0.05, 0.12, dn)))
        for hp, hr in feats.get('horns', []):              # quanh gốc sừng: lông ngắn, thưa
            dh = (q - hp).length / hr
            d = min(d, lerp(0.15, 1.0, smoothstep(0.8, 1.6, dh)))
            L = min(L, lerp(0.15, 1.0, smoothstep(0.9, 2.2, dh)))
        if feats.get('mouth') is not None:
            dmo = (q - feats['mouth']).length / s
            d = min(d, smoothstep(0.028, 0.05, dmo))
        dens[i] = d
        leng[i] = L
    return dens, leng


# ============================================================================
# 4. Vật liệu
# ============================================================================

class NB:
    """Trình dựng node vật liệu gọn tay."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        for n in list(self.nt.nodes):
            self.nt.nodes.remove(n)
        self.out = self.nt.nodes.new('ShaderNodeOutputMaterial')

    def node(self, kind, **props):
        n = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def link(self, a, b):
        self.nt.links.new(a, b)

    def put(self, sock, v):
        if isinstance(v, bpy.types.NodeSocket):
            self.nt.links.new(v, sock)
        else:
            sock.default_value = v

    def math(self, op, a, b=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.put(n.inputs[0], a)
        if b is not None:
            self.put(n.inputs[1], b)
        return n.outputs[0]

    def mix(self, fac, c1, c2, blend='MIX'):
        n = self.node('ShaderNodeMixRGB', blend_type=blend)
        self.put(n.inputs['Fac'], fac)
        self.put(n.inputs['Color1'], c1)
        self.put(n.inputs['Color2'], c2)
        return n.outputs['Color']

    def smooth(self, v, a, b):
        """smoothstep(a, b, v) bằng node Map Range."""
        n = self.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP', clamp=True)
        self.put(n.inputs['Value'], v)
        n.inputs['From Min'].default_value = a
        n.inputs['From Max'].default_value = b
        n.inputs['To Min'].default_value = 0.0
        n.inputs['To Max'].default_value = 1.0
        return n.outputs['Result']

    def dist2d(self, x, y, cx, cy):
        dx = self.math('SUBTRACT', x, cx)
        dy = self.math('SUBTRACT', y, cy)
        return self.math('SQRT', self.math('ADD', self.math('MULTIPLY', dx, dx),
                                           self.math('MULTIPLY', dy, dy)))


def _fur_color(nb, cols):
    """Màu lông/da theo Masks: R -> màu sáng, G -> màu chóp, B -> hoa văn."""
    at = nb.node('ShaderNodeAttribute', attribute_name='Masks')
    sep = nb.node('ShaderNodeSeparateColor')
    nb.link(at.outputs['Color'], sep.inputs['Color'])
    r, g, b = sep.outputs['Red'], sep.outputs['Green'], sep.outputs['Blue']
    col = nb.mix(r, C(cols['fur']), C(cols['light']))
    col = nb.mix(nb.math('MULTIPLY', b, cols.get('pattern_amt', 0.0)), col,
                 C(cols.get('pattern', cols['fur'])))
    col = nb.mix(g, col, C(cols['tip']))
    return col, r, g, b


def hair_material(name, cols, ember=None, ember_from='B'):
    mat = bpy.data.materials.new(name)
    nb = NB(mat)
    col, r, g, b = _fur_color(nb, cols)
    hi = nb.node('ShaderNodeHairInfo')
    ic = hi.outputs['Intercept']
    tipf = nb.math('MULTIPLY', nb.math('POWER', ic, 1.5), cols.get('tip_light', 0.3))
    col = nb.mix(tipf, col, nb.mix(0.5, col, (1.0, 1.0, 1.0, 1.0)))   # chóp sợi sáng hơn chút
    hb = nb.node('ShaderNodeBsdfHairPrincipled')
    hb.parametrization = 'COLOR'
    nb.link(col, hb.inputs['Color'])
    hb.inputs['Roughness'].default_value = 0.36
    hb.inputs['Radial Roughness'].default_value = 0.45
    hb.inputs['Coat'].default_value = 0.22
    hb.inputs['IOR'].default_value = 1.48          # giảm bóng trắng trên sợi lông
    if 'Random Roughness' in hb.inputs:
        hb.inputs['Random Roughness'].default_value = 0.15
    shader = hb.outputs[0]
    if ember and ember.get('amount', 0.0) > 0.001:   # lông hệ Lửa: chóp + sọc phát sáng
        em = nb.node('ShaderNodeEmission')
        em.inputs['Color'].default_value = C(ember['color'])
        em.inputs['Strength'].default_value = ember.get('strength', 4.0)
        src = {'B': b, 'G': g}[ember_from]
        f1 = nb.math('MULTIPLY', src, nb.math('POWER', ic, 1.3))
        f2 = nb.math('MULTIPLY', nb.math('POWER', ic, 4.0), ember.get('tips', 0.25))
        fac = nb.math('MULTIPLY', nb.math('ADD', f1, f2), ember['amount'], clamp=True)
        mx = nb.node('ShaderNodeMixShader')
        nb.link(fac, mx.inputs[0])
        nb.link(shader, mx.inputs[1])
        nb.link(em.outputs[0], mx.inputs[2])
        shader = mx.outputs[0]
    nb.link(shader, nb.out.inputs['Surface'])
    return mat


def skin_material(name, cols, inner=None):
    mat = bpy.data.materials.new(name)
    nb = NB(mat)
    col, r, g, b = _fur_color(nb, cols)
    col = nb.mix(0.3, col, (0.0, 0.0, 0.0, 1.0))      # da tối hơn lông một chút
    if inner is not None:                              # tai: mặt trong màu hồng
        col = nb.mix(r, col, C(inner))
    bs = nb.node('ShaderNodeBsdfPrincipled')
    nb.link(col, bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.7
    nb.link(bs.outputs[0], nb.out.inputs['Surface'])
    return mat


def simple_material(name, color, rough=0.4, coat=0.0, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    nb = NB(mat)
    bs = nb.node('ShaderNodeBsdfPrincipled')
    bs.inputs['Base Color'].default_value = C(color)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Coat Weight'].default_value = coat
    if emission is not None:
        bs.inputs['Emission Color'].default_value = C(emission)
        bs.inputs['Emission Strength'].default_value = strength
    nb.link(bs.outputs[0], nb.out.inputs['Surface'])
    return mat


def horn_material(name, base, tip, glow=0.0, glow_col=None, metal=0.0):
    mat = bpy.data.materials.new(name)
    nb = NB(mat)
    tc = nb.node('ShaderNodeTexCoord')
    sep = nb.node('ShaderNodeSeparateXYZ')
    nb.link(tc.outputs['UV'], sep.inputs['Vector'])
    v = sep.outputs['Y']
    col = nb.mix(nb.smooth(v, 0.25, 1.0), C(base), C(tip))
    bs = nb.node('ShaderNodeBsdfPrincipled')
    nb.link(col, bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.32 - 0.12 * metal
    bs.inputs['Metallic'].default_value = metal
    bs.inputs['Coat Weight'].default_value = 0.4
    if glow > 0.0:
        bs.inputs['Emission Color'].default_value = C(glow_col or tip)
        nb.link(nb.math('MULTIPLY', nb.smooth(v, 0.5, 1.0), glow), bs.inputs['Emission Strength'])
    nb.link(bs.outputs[0], nb.out.inputs['Surface'])
    return mat


def eye_material(name, iris, side, pupil=(0.2, 0.27), iris_r=0.64):
    """Mắt: mống mắt to + viền tối + đồng tử + 2 đốm sáng. UV = tọa độ mắt (đơn vị nửa bề ngang)."""
    mat = bpy.data.materials.new(name)
    nb = NB(mat)
    c = C(iris)
    tc = nb.node('ShaderNodeTexCoord')
    sep = nb.node('ShaderNodeSeparateXYZ')
    nb.link(tc.outputs['UV'], sep.inputs['Vector'])
    x, y = sep.outputs['X'], sep.outputs['Y']
    dx = nb.math('SUBTRACT', x, -0.07 * side)          # mống mắt lệch vào trong (nhìn thẳng)
    dy = nb.math('SUBTRACT', y, -0.04)
    rr = nb.math('SQRT', nb.math('ADD', nb.math('MULTIPLY', dx, dx), nb.math('MULTIPLY', dy, dy)))
    rn = nb.math('DIVIDE', rr, iris_r)
    ramp = nb.node('ShaderNodeValToRGB')
    nb.link(rn, ramp.inputs['Fac'])
    cr = ramp.color_ramp
    cr.interpolation = 'EASE'
    cr.elements[0].position = 0.0
    cr.elements[0].color = mulc(c, 2.2)
    cr.elements[1].position = 1.0
    cr.elements[1].color = mulc(c, 0.035)
    for pos, k in ((0.42, 1.45), (0.78, 1.0), (0.93, 0.28)):
        e = cr.elements.new(pos)
        e.color = mulc(c, k)
    ang = nb.math('ARCTAN2', dy, dx)                    # vân mống mắt (sợi tỏa ra)
    cmb = nb.node('ShaderNodeCombineXYZ')
    nb.put(cmb.inputs['X'], nb.math('MULTIPLY', ang, 2.5))
    nb.put(cmb.inputs['Y'], nb.math('MULTIPLY', rn, 3.0))
    nz = nb.node('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 3.0
    nz.inputs['Detail'].default_value = 3.0
    nb.link(cmb.outputs['Vector'], nz.inputs['Vector'])
    streak = nb.math('ADD', 0.78, nb.math('MULTIPLY', nz.outputs['Fac'], 0.45))
    col = nb.mix(1.0, ramp.outputs['Color'], streak, blend='MULTIPLY')
    px = nb.math('DIVIDE', dx, pupil[0])
    py = nb.math('DIVIDE', dy, pupil[1])
    pr = nb.math('SQRT', nb.math('ADD', nb.math('MULTIPLY', px, px), nb.math('MULTIPLY', py, py)))
    col = nb.mix(nb.smooth(pr, 1.0, 0.86), col, (0.006, 0.005, 0.007, 1.0))
    h1 = nb.smooth(nb.dist2d(x, y, -0.30, 0.34), 0.15, 0.11)
    h2 = nb.smooth(nb.dist2d(x, y, 0.24, -0.22), 0.075, 0.05)
    hl = nb.math('ADD', h1, nb.math('MULTIPLY', h2, 0.75), clamp=True)
    bs = nb.node('ShaderNodeBsdfPrincipled')
    nb.link(col, bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.06
    bs.inputs['Coat Weight'].default_value = 1.0
    bs.inputs['Coat Roughness'].default_value = 0.02
    bs.inputs['Emission Color'].default_value = (1.0, 1.0, 1.0, 1.0)
    nb.link(nb.math('MULTIPLY', hl, 2.5), bs.inputs['Emission Strength'])
    nb.link(bs.outputs[0], nb.out.inputs['Surface'])
    return mat


# ============================================================================
# 5. Lông (particle hair)
# ============================================================================

QUALITY = {
    'draft': dict(count=0.30, children=0.45, steps=2),
    'final': dict(count=1.0, children=1.0, steps=3),
}


def add_hair(ob, name, count, length, children, mat_index, comb=None, beta=0.0,
             normals=None, dens=None, leng=None, radius=0.0032, steps=3, clump=0.3,
             rough=0.012, seed=1):
    """Thêm hệ lông. comb = hướng chải (không gian object), beta = mức độ chải.
    Blender cộng hướng chải vào 'vận tốc' => độ dài bị lệch; bù lại bằng vertex group độ dài."""
    nverts = len(ob.data.vertices)
    leng = list(leng) if leng is not None else [1.0] * nverts
    k_min = 1.0
    if comb is not None and beta > 0.0 and normals is not None:
        cdir = comb.normalized()
        mags = [(nv + cdir * beta).length for nv in normals]
        k_min = max(0.05, min(mags))
        leng = [l * k_min / m for l, m in zip(leng, mags)]
    set_vgroup(ob, name + '_len', leng)
    if dens is not None:
        set_vgroup(ob, name + '_dens', dens)
    ob.modifiers.new(name, 'PARTICLE_SYSTEM')
    psys = ob.particle_systems[-1]
    ps = bpy.data.particles.new(ob.name + '_' + name)
    psys.settings = ps
    psys.seed = seed
    ps.type = 'HAIR'
    ps.use_advanced_hair = True
    ps.count = max(10, int(count))
    ps.hair_length = length / k_min                 # Blender đổi ra normal_factor = length/4
    if comb is not None and beta > 0.0:
        cc = comb.normalized() * (beta * ps.normal_factor)
        ps.object_align_factor = (cc.x, cc.y, cc.z)
    ps.emit_from = 'FACE'
    ps.use_even_distribution = True
    ps.render_step = steps
    ps.display_step = 1
    ps.child_type = 'INTERPOLATED'
    ps.rendered_child_count = max(1, int(children))
    ps.child_percent = 1
    ps.clump_factor = clump
    ps.clump_shape = -0.2
    ps.use_clump_noise = True
    ps.clump_noise_size = 0.6
    ps.roughness_1 = rough
    ps.roughness_1_size = 1.0
    ps.roughness_2 = rough * 1.6
    ps.roughness_2_size = 0.8
    ps.roughness_endpoint = rough * 0.6
    ps.radius_scale = radius
    ps.root_radius = 1.0
    ps.tip_radius = 0.08
    ps.shape = -0.25
    ps.material = mat_index
    psys.vertex_group_length = name + '_len'
    if dens is not None:
        psys.vertex_group_density = name + '_dens'
    return psys


# ============================================================================
# 6. Bộ phận: mắt, mũi, miệng, tai, sừng, đuôi
# ============================================================================

EYE_SHAPES = {   # 6 dạng mắt (DNA)
    'tron':      dict(w=1.00, ht=0.95, hb=0.92, p=0.50, tilt=0.0),    # tròn to
    'hanh_nhan': dict(w=1.08, ht=0.78, hb=0.62, p=0.78, tilt=9.0),    # hạnh nhân sắc
    'meo':       dict(w=1.12, ht=0.70, hb=0.52, p=0.88, tilt=18.0),   # mắt mèo xếch
    'lim_dim':   dict(w=1.05, ht=0.42, hb=0.62, p=0.72, tilt=-3.0),   # lim dim
    'cun':       dict(w=1.02, ht=0.90, hb=0.78, p=0.58, tilt=-9.0),   # mắt cún (đuôi mắt cụp)
    'sac_hep':   dict(w=1.16, ht=0.52, hb=0.42, p=0.92, tilt=15.0),   # sắc hẹp
}


def eye_params(shape):
    return dict(EYE_SHAPES[shape]) if isinstance(shape, str) else dict(shape)


def lerp_eye(a, b, t):
    A, B = eye_params(a), eye_params(b)
    return {k: lerp(A[k], B[k], t) for k in A}


def eye_outline(P, hw, seg=48):
    out = []
    ta = math.radians(P['tilt'])
    for k in range(seg):
        a = 2.0 * math.pi * k / seg
        c, sn = math.cos(a), math.sin(a)
        x = P['w'] * c
        h = P['ht'] if sn >= 0 else P['hb']
        y = h * abs(sn) ** (2.0 * P['p']) * (1.0 if sn >= 0 else -1.0)
        out.append(((x * math.cos(ta) - y * math.sin(ta)) * hw,
                    (x * math.sin(ta) + y * math.cos(ta)) * hw))
    return out


def eye_close_xy(P, hw, X, Y, side, gap=0.06):
    """Mắt NHẮM: ép 1 điểm (X, Y) của mắt mở vào đường khép mí cong ◡ (giữa võng xuống).
    Dùng cho mọi đỉnh của thấu kính + viền mắt => cùng số đỉnh, làm blendshape 'closed_<dạng>'."""
    ta = math.radians(P['tilt'])
    x = X * side
    xr = (x * math.cos(ta) + Y * math.sin(ta)) / hw          # bỏ độ nghiêng của mắt
    yr = (-x * math.sin(ta) + Y * math.cos(ta)) / hw
    c = max(-1.0, min(1.0, xr / P['w']))
    yl = -0.30 * P['hb'] - 0.12 * (1.0 - c * c)              # đường khép mí
    yr = yl + (yr - yl) * gap
    x2 = (xr * math.cos(ta) - yr * math.sin(ta)) * hw        # nghiêng lại như cũ
    y2 = (xr * math.sin(ta) + yr * math.cos(ta)) * hw
    return x2 * side, y2


def eye_lens_geom(P, hw, R_head, side, rings=9, seg=48, closed=False):
    """Mắt dạng 'thấu kính' ôm theo độ cong của đầu (cùng số đỉnh cho mọi dạng mắt).
    closed=True: mắt nhắm (thấu kính ép dẹt nằm sau viền mắt). UV giữ như mắt mở."""
    out = eye_outline(P, hw, seg)
    dome = 0.04 if closed else 0.28

    def vert(X, Y, rho):
        if closed:
            X, Y = eye_close_xy(P, hw, X, Y, side)
        return Vector((X, Y, dome * hw * (1.0 - rho * rho) ** 0.7 - (X * X + Y * Y) / (2.0 * R_head)))

    verts = [vert(0.0, 0.0, 0.0)]
    uvs = [(0.0, 0.0)]
    for j in range(1, rings + 1):
        rho = j / rings
        for (x, y) in out:
            X, Y = x * rho * side, y * rho
            verts.append(vert(X, Y, rho))
            uvs.append((X / hw, Y / hw))
    faces = []
    for k in range(seg):
        faces.append((0, 1 + k, 1 + (k + 1) % seg))
    for j in range(rings - 1):
        a0, a1 = 1 + j * seg, 1 + (j + 1) * seg
        for k in range(seg):
            k2 = (k + 1) % seg
            faces.append((a0 + k, a1 + k, a1 + k2, a0 + k2))
    if side < 0:
        faces = [tuple(reversed(f)) for f in faces]
    return verts, faces, uvs


def eye_liner_geom(P, hw, R_head, side, seg=48, closed=False):
    """Viền mắt tối (mí trên dày hơn, nhất là đuôi mắt) -> mắt 'sắc'. closed=True: 2 mí khép thành 1 nét."""
    out = eye_outline(P, hw, seg)
    pts, rad = [], []
    for k, (x, y) in enumerate(out):
        a = 2.0 * math.pi * k / seg
        X, Y = x * 1.04 * side, y * 1.04
        if closed:
            X, Y = eye_close_xy(P, hw, X, Y, side)
        pts.append(Vector((X, Y, -(X * X + Y * Y) / (2.0 * R_head) + 0.006 * hw)))
        rad.append(hw * (0.045 + 0.08 * max(0.0, math.sin(a)) * (0.7 + 0.3 * math.cos(a))))
    return tube_mesh(pts, rad, ring=8, closed=True, cap_end=False, up=Vector((0.0, 0.0, 1.0)))


def eye_frame(bvh, hc, fw, u, r, s, side, yaw, pitch):
    """Khung đặt mắt (gốc = tâm mắt trên mặt da, z = hướng ra ngoài, y = lên)."""
    d = (r * (side * math.sin(yaw)) + fw * math.cos(yaw)).normalized()
    d = (d + u * math.sin(pitch)).normalized()
    hit, nrm = surface_from_center(bvh, hc, d)
    x, y, z = make_frame((nrm * 0.75 + d * 0.25), u)
    return frame_matrix(hit - z * (0.004 * s), x, y, z), hit


def make_eyes(name, dna, bvh, hc, fw, u, r, s):
    P = eye_params(dna.get('eye_shape', 'hanh_nhan'))
    hw = 0.082 * s * dna.get('eye_scale', 1.0)
    yaw = math.radians(dna.get('eye_yaw', 34.0))
    pitch = math.radians(dna.get('eye_pitch', -3.0))
    Rh = 0.29 * s
    pupil = dna.get('pupil', (0.2, 0.27))
    liner_mat = simple_material(name + '_LinerMat', dna.get('liner', '#1C1416'), rough=0.45)
    obs, feats = [], []
    for side in (1, -1):
        M, hit = eye_frame(bvh, hc, fw, u, r, s, side, yaw, pitch)
        tag = 'R' if side > 0 else 'L'
        v, fc, uv = eye_lens_geom(P, hw, Rh, side)
        lens = new_mesh_object(name + '_Eye' + tag, v, fc, uv)
        lens.matrix_world = M
        lens.data.materials.append(eye_material(name + '_EyeMat' + tag, dna['eye_color'], side, pupil))
        lv, lf, luv = eye_liner_geom(P, hw, Rh, side)
        liner = new_mesh_object(name + '_Liner' + tag, lv, lf, luv)
        liner.matrix_world = M
        liner.data.materials.append(liner_mat)
        obs += [lens, liner]
        feats.append((hit.copy(), hw))
    return obs, feats


def make_nose_mouth(name, dna, bvh, hc, fw, u, r, s):
    hit, nrm = ray_hit(bvh, hc + fw * 3.0 + u * (-0.045 * s), -fw)
    if hit is None:
        hit, nrm = hc + fw * (0.3 * s), fw.copy()
    if nrm.dot(fw) < 0:
        nrm = -nrm
    x, y, z = make_frame(nrm * 0.55 + fw * 0.45, u)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=1.0)
    for v in bm.verts:
        X, Y, Z = v.co.x, v.co.y, v.co.z
        taper = 0.60 + 0.40 * (Y + 1.0) * 0.5            # mũi tam giác tròn: dưới hẹp hơn
        v.co = Vector((X * 0.050 * s * taper, Y * 0.032 * s, Z * 0.030 * s))
    me = bpy.data.meshes.new(name + '_Nose')
    bm.to_mesh(me)
    bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    nose = link(bpy.data.objects.new(name + '_Nose', me))
    nose.matrix_world = frame_matrix(hit - z * (0.012 * s), x, y, z)
    nose.data.materials.append(simple_material(name + '_NoseMat', dna.get('nose', '#2E2026'),
                                               rough=0.28, coat=0.5))
    pts = []                                              # miệng chữ "w" nhỏ dưới mũi
    for k in range(13):
        t = -1.0 + 2.0 * k / 12
        off = x * (t * 0.036 * s) + y * (-0.040 * s - 0.013 * s * math.sin(math.pi * abs(t)))
        h2, _ = ray_hit(bvh, hit + off + fw * 0.5, -fw)
        pts.append((h2 if h2 is not None else hit + off) + fw * (0.002 * s))
    v, fc, uv = tube_mesh(pts, [0.0034 * s] * len(pts), ring=6, cap_end=True, cap_start=True, up=fw)
    mouth = new_mesh_object(name + '_Mouth', v, fc, uv)
    mouth.data.materials.append(simple_material(name + '_MouthMat', dna.get('mouth', '#3A2428'), rough=0.5))
    return [nose, mouth], hit.copy(), pts[6].copy()


EAR_TYPES = {   # 4 dạng tai (DNA)
    'nhon': dict(L=0.24, W=0.115, cup=0.35, bend=14),    # tai nhọn (cáo)
    'tron': dict(L=0.15, W=0.125, cup=0.45, bend=4),     # tai tròn (gấu)
    'cup':  dict(L=0.22, W=0.130, cup=0.35, bend=-125, fold=(0.30, 0.62)),  # tai cụp (gập ra trước)
    'vay':  dict(L=0.27, W=0.105, cup=0.10, bend=28),    # tai vây / lông vũ
}
EAR_N, EAR_M = 16, 10


def ear_geom(kind, scale, sweep_sign=1.0):
    """Lưới tai cố định (EAR_N x EAR_M) cho mọi kiểu => morph giữa các kiểu được."""
    P = EAR_TYPES[kind]
    L, W = P['L'] * scale, P['W'] * scale
    verts, uvs = [], []
    for i in range(EAR_N + 1):
        t = i / EAR_N
        if kind == 'nhon':
            wt = (1.0 - t) ** 0.9 * (1.0 + 0.45 * math.sin(math.pi * t)) * 0.78
        elif kind == 'tron':
            wt = math.sqrt(max(0.0, 1.0 - ((t - 0.40) / 0.60) ** 2))
        elif kind == 'cup':
            wt = ((0.55 + 0.45 * math.sin(math.pi * min(1.0, t * 1.15)))
                  * math.sqrt(max(0.0, 1.0 - t ** 3)))
        else:
            wt = (1.0 - t) ** 0.6 * (1.0 + 0.18 * math.sin(3.0 * math.pi * t))
        w = max(wt * W, 0.003 * scale)
        sweep = 0.35 * W * t ** 1.5 * sweep_sign if kind == 'vay' else 0.0
        for j in range(EAR_M + 1):
            sv = -1.0 + 2.0 * j / EAR_M
            verts.append(Vector((sv * w + sweep, P['cup'] * (1.0 - sv * sv) * w, t * L)))
            uvs.append(((sv + 1.0) * 0.5, t))
    # Uốn tai: tích phân đường tâm theo góc uốn theta(t) trong mặt phẳng YZ
    # (uốn đều toàn tai, hoặc gập theo bản lề nếu có 'fold').
    B = math.radians(P['bend'])
    if abs(B) > 1e-4:
        f0, f1 = P.get('fold', (None, None))
        def theta(t):
            return B * (smoothstep(f0, f1, t) if f0 is not None else t)
        cy, cz = [0.0], [0.0]
        for i in range(1, EAR_N + 1):
            a = theta((i - 0.5) / EAR_N)
            cy.append(cy[-1] + math.sin(a) * L / EAR_N)
            cz.append(cz[-1] + math.cos(a) * L / EAR_N)
        for i in range(EAR_N + 1):
            a = theta(i / EAR_N)
            for j in range(EAR_M + 1):
                v = verts[i * (EAR_M + 1) + j]
                y0 = v.y
                v.y = cy[i] + y0 * math.cos(a)
                v.z = cz[i] - y0 * math.sin(a)
    faces = []
    for i in range(EAR_N):
        for j in range(EAR_M):
            a = i * (EAR_M + 1) + j
            faces.append((a, a + 1, a + EAR_M + 2, a + EAR_M + 1))
    return verts, faces, uvs


def ear_frame(kind, side, bvh, hc, fw, u, r, sc):
    """Khung đặt tai (gốc = chân tai, z = chiều dài tai, -y = mặt trong). Trả (ma trận, chiều vuốt)."""
    if kind == 'vay':
        d = (r * (0.72 * side) + u * 0.60 + fw * 0.02).normalized()
    else:
        d = (r * (0.55 * side) + u * 0.82 - fw * 0.10).normalized()
    hit, nrm = surface_from_center(bvh, hc, d)
    if kind == 'vay':
        zax = (r * (0.72 * side) + u * 0.52 - fw * 0.30).normalized()
        inner = r * side
    else:
        zax = (nrm * 0.40 + u * 0.88 - fw * 0.12).normalized()
        inner = (fw + r * (0.38 * side)).normalized()
    x, y, z = make_frame(zax, -inner)
    sweep = 1.0 if x.dot(-fw) > 0 else -1.0
    return frame_matrix(hit - nrm * (0.022 * sc), x, y, z), sweep


def make_ear_mesh(name, kind, side, bvh, hc, fw, u, r, sc):
    """Tai (đã có độ dày) đặt đúng chỗ trên đầu, chưa có vật liệu / lông."""
    M, sweep = ear_frame(kind, side, bvh, hc, fw, u, r, sc)
    verts, faces, uvs = ear_geom(kind, sc, sweep)
    ob = new_mesh_object(name, verts, faces, uvs)
    ob.matrix_world = M
    sol = ob.modifiers.new('sol', 'SOLIDIFY')
    sol.thickness = 0.016 * sc
    sol.offset = 0.0
    sol.use_even_offset = True
    apply_mod(ob, sol)
    return ob


def ear_masks(ob, tip_amt):
    """Vùng màu tai theo góc mặt: R = mặt trong (hồng), G = chóp tai. Trả (màu theo góc, độ 'trong' theo đỉnh)."""
    me = ob.data
    uvl = me.uv_layers.active.data
    loop_rgb = [None] * len(me.loops)
    acc = [0.0] * len(me.vertices)
    cnt = [0] * len(me.vertices)
    for poly in me.polygons:
        inner_side = 1.0 if poly.normal.y < -0.25 else 0.0
        for li in poly.loop_indices:
            uu, tt = uvl[li].uv
            s01 = abs(2.0 * uu - 1.0)
            inn = (inner_side * (1.0 - smoothstep(0.55, 0.88, s01))
                   * (1.0 - smoothstep(0.70, 0.93, tt)) * smoothstep(0.0, 0.10, tt))
            loop_rgb[li] = (inn, smoothstep(0.55, 0.97, tt) * tip_amt, 0.0)
            vi = me.loops[li].vertex_index
            acc[vi] += inn
            cnt[vi] += 1
    return loop_rgb, [acc[i] / max(cnt[i], 1) for i in range(len(me.vertices))]


def make_ear(name, dna, side, bvh, hc, fw, u, r, s, Q, ember):
    kind = dna.get('ear', 'nhon')
    sc = s * dna.get('ear_scale', 1.0)
    ob = make_ear_mesh(name + ('_EarR' if side > 0 else '_EarL'), kind, side, bvh, hc, fw, u, r, sc)
    loop_rgb, inn_v = ear_masks(ob, dna.get('ear_tip', 0.0))
    set_masks_loops(ob, loop_rgb)
    return _make_ear_body(name, dna, side, bvh, hc, fw, u, r, s, Q, ember, ob, inn_v)


def _make_ear_body(name, dna, side, bvh, hc, fw, u, r, s, Q, ember, ob, inn_v):
    me = ob.data
    sc = s * dna.get('ear_scale', 1.0)
    cols = dict(fur=dna['fur'], light=dna.get('ear_fluff', dna['belly']),
                tip=dna.get('ear_tip_color', dna.get('points', dna['fur'])),
                tip_light=dna.get('tip_light', 0.15))
    me.materials.append(skin_material(ob.name + '_Skin', cols, inner=dna.get('inner_ear', '#F2B6AE')))
    me.materials.append(hair_material(ob.name + '_Fur', cols, ember, ember_from='G'))
    normals = [v.normal.copy() for v in me.vertices]
    add_hair(ob, 'Fur', 650 * Q['count'], 0.034 * sc, 16 * Q['children'], 2,
             comb=Vector((0.0, 0.0, 1.0)), beta=0.65, normals=normals,
             dens=[1.0 - 0.82 * a for a in inn_v], leng=[1.0 - 0.35 * a for a in inn_v],
             radius=0.0024, steps=Q['steps'], clump=0.25, seed=dna.get('seed', 7) + side + 3)
    return ob


HORN_CURL = {'thang': 8.0, 'dai': 45.0, 'cong': 150.0}   # 3 dạng sừng (DNA)


def horn_geom(kind, length, radius):
    N = 18
    curl = math.radians(HORN_CURL[kind])
    pts, rad = [], []
    for i in range(N + 1):
        t = i / N
        Rc = length / curl
        a = t * curl
        p = Vector((0.0, Rc * (1.0 - math.cos(a)), Rc * math.sin(a)))
        if kind == 'thang':
            p.y -= 0.18 * length * t
        pts.append(p)
        rr = radius * (1.0 - t) ** 0.85 + radius * 0.12
        if t < 0.8:                                    # vòng gân trên sừng cho ngầu
            rr *= 1.0 + 0.07 * max(0.0, math.sin(2.0 * math.pi * 6.0 * t))
        rad.append(rr * (1.0 - 0.88 * t ** 6))
    return tube_mesh(pts, rad, ring=12, cap_end=True, cap_start=True, up=Vector((1.0, 0.0, 0.0)))


HORN_SLOTS = {   # khe gắn sừng: (bên, hệ số kích thước, kiểu hướng)
    'C': (0, 1.0, 'center'), 'PR': (1, 1.0, 'pair'), 'PL': (-1, 1.0, 'pair'),
    'WR': (1, 0.75, 'wide'), 'WL': (-1, 0.75, 'wide'),
}
HORN_LAYOUT = {1: ['C'], 2: ['PR', 'PL'], 3: ['C', 'WR', 'WL']}   # số sừng -> các khe dùng


def horn_frame(slot, bvh, hc, fw, u, r, s):
    """Khung gắn 1 sừng (gốc = chân sừng, z = hướng mọc, y = phía sau để sừng cong về sau)."""
    side, k, kind = HORN_SLOTS[slot]
    if kind == 'center':
        d = (fw * 0.40 + u * 0.92).normalized()
    elif kind == 'wide':
        d = (r * (0.46 * side) + fw * 0.16 + u * 0.87).normalized()
    else:
        d = (r * (0.36 * side) + fw * 0.26 + u * 0.90).normalized()
    hit, nrm = surface_from_center(bvh, hc, d)
    x, y, z = make_frame(nrm * 0.45 + u * 0.88 + r * (0.20 * side), -fw)
    return frame_matrix(hit - z * (0.006 * s), x, y, z), hit, k


def make_horns(name, dna, bvh, hc, fw, u, r, s):
    n = int(dna.get('horns', 0))
    feats = []
    if n <= 0 or dna.get('horn_len', 0.0) <= 0.001:
        return [], feats
    layout = HORN_LAYOUT[min(n, 3)]
    mat = horn_material(name + '_HornMat', dna.get('horn_color', '#EFE6D6'),
                        dna.get('horn_tip', '#FFFFFF'), dna.get('horn_glow', 0.0),
                        dna.get('horn_tip', '#FFFFFF'), dna.get('horn_metal', 0.0))
    obs = []
    for slot in layout:
        M, hit, k = horn_frame(slot, bvh, hc, fw, u, r, s)
        L = dna['horn_len'] * s * k
        Rr = (0.026 + 0.08 * dna['horn_len']) * s * k
        verts, faces, uvs = horn_geom(dna.get('horn_shape', 'thang'), L, Rr)
        ob = new_mesh_object(name + '_Horn%d' % len(obs), verts, faces, uvs)
        ob.matrix_world = M
        ob.data.materials.append(mat)
        obs.append(ob)
        feats.append((hit.copy(), Rr * 1.6))
    return obs, feats


TAIL_N = 28


TAIL_HAIR_LEN = {'cao': 0.12, 'tua': 0.12, 'bong': 0.095, 'vay': 0.03, 'xoan': 0.055}   # độ dài lông đuôi


def tail_fluff(kind, t):
    """Độ dày lông đuôi dọc theo đuôi (t = 0 gốc .. 1 chóp) — khớp với lông particle của bản render."""
    if kind == 'tua':
        leng = 0.22 + 0.78 * smoothstep(0.74, 0.9, t)
    elif kind == 'vay':
        leng = 0.7 * (1.0 - smoothstep(0.45, 0.58, t))
    elif kind == 'cao':
        leng = 0.55 + 0.45 * smoothstep(0.0, 0.35, t)
    elif kind == 'bong':
        leng = math.sin(math.pi * min(1.0, 0.08 + t * 0.92)) ** 0.5
    else:
        leng = 1.0
    return TAIL_HAIR_LEN[kind] * leng * max(0.0, 1.0 - t ** 6) ** 0.5


def tail_geom(kind, s, fluff=0.0):
    """5 dạng đuôi (DNA), cùng số đỉnh: cáo (xù), túa (lụ túa), bông (cục bông), vây (vây cá), xoăn.
    fluff > 0: cộng thêm độ dày lông vào bán kính (bản cho game, không có lông particle)."""
    if kind == 'cao':
        L = 0.80
        th = lambda t: 22 + 60 * t ** 1.2
        rf = lambda t: ((0.034 + 0.068 * math.sin(math.pi * min(1.0, t * 1.04)) ** 0.75)
                        * (1.0 - t ** 5) ** 0.5 + 0.006)
    elif kind == 'tua':
        L = 0.64
        th = lambda t: -12 + 75 * t ** 2.2
        rf = lambda t: (0.026 * (1.0 - 0.45 * t)
                        + 0.028 * smoothstep(0.78, 0.9, t) * (1.0 - smoothstep(0.93, 1.0, t)) + 0.004)
    elif kind == 'bong':
        L = 0.11
        th = lambda t: 28
        rf = lambda t: 0.082 * math.sin(math.pi * min(1.0, 0.08 + t * 0.92)) ** 0.55 + 0.006
    elif kind == 'vay':
        L = 0.54
        th = lambda t: 6 + 34 * t
        rf = lambda t: 0.042 * (1.0 - 0.5 * t) + 0.004
    else:  # xoăn
        L = 0.52
        th = lambda t: 22 + 300 * t ** 1.1
        rf = lambda t: 0.038 * (1.0 - 0.55 * t) + 0.004
    L *= s
    pts = [Vector((0.0, 0.0, 0.0))]
    rad, flat = [], []
    step = L / TAIL_N
    for i in range(TAIL_N + 1):
        t = i / TAIL_N
        if i > 0:
            a = math.radians(th(t))
            pts.append(pts[-1] + Vector((0.0, math.cos(a), math.sin(a))) * step)
        rad.append((rf(t) + fluff * tail_fluff(kind, t)) * s)
        flat.append((0.6, 1.0 + 3.5 * smoothstep(0.5, 0.95, t)) if kind == 'vay' else (1.0, 1.0))
    return tube_mesh(pts, rad, ring=14, cap_end=True, cap_start=True, flatten=flat,
                     up=Vector((1.0, 0.0, 0.0)))


def tail_origin(bvh, P, base, st=1.0):
    """Gốc đuôi: điểm trên mông (bắn tia từ phía sau vào tâm hông), lùn vào trong 1 chút.
    Tâm hông tính theo trọng số mềm — bản cũ dùng ngưỡng 0.85 nên không có đỉnh nào đạt,
    tâm rơi về (0,0,0) và đuôi bị gắn sát đất."""
    hp = _wcentroid(P, base['bone_w']['hips'])
    d = Vector((0.0, 1.0, 0.22)).normalized()
    hit, nrm = surface_from_center(bvh, hp, d)
    return hit - d * (0.04 * st)


def make_tail(name, dna, bvh, P, base, Q, ember):
    kind = dna.get('tail', 'cao')
    st = dna.get('tail_scale', 1.0)
    verts, faces, uvs = tail_geom(kind, st)
    ob = new_mesh_object(name + '_Tail', verts, faces, uvs)
    ob.matrix_world = Matrix.Translation(tail_origin(bvh, P, base, st))
    n = len(ob.data.vertices)
    tv = [uvs[i][1] for i in range(n)]
    striped = dna.get('pattern') == 'stripes'
    B = [(smoothstep(0.5, 0.85, math.sin(2.0 * math.pi * 5.5 * t)) * (1.0 - smoothstep(0.75, 0.85, t))
          if striped else 0.0) for t in tv]
    set_masks(ob, [0.0] * n, [smoothstep(0.62, 0.92, t) for t in tv], B)
    dens, leng = [1.0] * n, [1.0] * n
    if kind == 'tua':
        leng = [0.22 + 0.78 * smoothstep(0.74, 0.9, t) for t in tv]
    elif kind == 'vay':
        dens = [1.0 - smoothstep(0.45, 0.58, t) for t in tv]
        leng = [0.7] * n
    elif kind == 'cao':
        leng = [0.55 + 0.45 * smoothstep(0.0, 0.35, t) for t in tv]
    cols = dict(fur=dna['fur'], light=dna['belly'], tip=dna.get('tail_tip', dna['fur']),
                pattern=dna.get('pattern_color', dna['fur']),
                pattern_amt=dna.get('pattern_strength', 0.0), tip_light=dna.get('tip_light', 0.15))
    me = ob.data
    me.materials.append(skin_material(ob.name + '_Skin', cols))
    me.materials.append(hair_material(ob.name + '_Fur', cols, ember, ember_from='G'))
    hair_len = TAIL_HAIR_LEN[kind] * st
    count = {'cao': 2600, 'tua': 900, 'bong': 1300, 'vay': 700, 'xoan': 1100}[kind]
    normals = [v.normal.copy() for v in me.vertices]
    add_hair(ob, 'Fur', count * Q['count'], hair_len, 30 * Q['children'], 2,
             comb=Vector((0.0, 1.0, 0.25)), beta=0.6, normals=normals, dens=dens, leng=leng,
             radius=0.003, steps=Q['steps'] + 1, clump=0.35, rough=0.016,
             seed=dna.get('seed', 7) + 11)
    return ob


# ============================================================================
# 7. Ghép thành 1 con thú hoàn chỉnh
# ============================================================================

def build_creature(name, dna, stage=0.0, pose=None, quality='final', form='fire'):
    """Dựng 1 con thú tại gốc tọa độ (mặt nhìn -Y). Trả về empty gốc để đặt vị trí / xoay.
    stage: 0 = Baby ... 1 = Adult Lửa (giá trị blendshape to_adult_fire)."""
    base = ensure_base()
    Q = QUALITY[quality]
    rng = random.Random(dna.get('seed', 7))
    me = base['mesh'].copy()
    me.name = name + '_BodyMesh'
    body = link(bpy.data.objects.new(name + '_Body', me))
    kb = me.shape_keys.key_blocks
    kb['to_adult_' + form].value = stage
    kb['head_big'].value = dna.get('head_big', 0.0)
    kb['body_chubby'].value = dna.get('body_chubby', 0.0)
    kb['legs_long'].value = dna.get('legs_long', 0.0)
    P = evaluated_coords(body)
    hwt = base['bone_w']['head']
    nwt = base['bone_w']['neck']
    hc = _centroid(P, hwt, 0.95)
    Rm = Matrix.Identity(3)
    if pose:   # tư thế đầu (nghiêng / quay) = 1 blendshape riêng cho con này
        Rm = (Matrix.Rotation(math.radians(pose.get('yaw', 0.0)), 3, 'Z')
              @ Matrix.Rotation(math.radians(pose.get('pitch', 0.0)), 3, 'X')
              @ Matrix.Rotation(math.radians(pose.get('roll', 0.0)), 3, 'Y'))
        s0 = _mean_dist(P, hwt, 0.95, hc) / base['head_r']
        piv = hc + Vector((0.0, 0.10, -0.17)) * s0
        co = base['co']
        new = []
        for i, p in enumerate(P):
            w = min(1.0, hwt[i] + 0.55 * nwt[i])
            dl = (piv + Rm @ (p - piv) - p) * w
            b = co[i]
            new.extend((b.x + dl.x, b.y + dl.y, b.z + dl.z))
        k = body.shape_key_add(name='pose', from_mix=False)
        k.data.foreach_set('co', new)
        k.value = 1.0
        P = evaluated_coords(body)
        hc = piv + Rm @ (hc - piv)
    s = _mean_dist(P, hwt, 0.95, hc) / base['head_r']
    fw = Rm @ Vector((0.0, -1.0, 0.0))
    u = Rm @ Vector((0.0, 0.0, 1.0))
    r = Rm @ Vector((1.0, 0.0, 0.0))
    bvh = BVHTree.FromPolygons(P, base['polys'])
    ember = dna.get('ember')
    parts = [body]
    eyes, eye_feats = make_eyes(name, dna, bvh, hc, fw, u, r, s)
    parts += eyes
    nm, nose_p, mouth_p = make_nose_mouth(name, dna, bvh, hc, fw, u, r, s)
    parts += nm
    for side in (1, -1):
        parts.append(make_ear(name, dna, side, bvh, hc, fw, u, r, s, Q, ember))
    horns, horn_feats = make_horns(name, dna, bvh, hc, fw, u, r, s)
    parts += horns
    parts.append(make_tail(name, dna, bvh, P, base, Q, ember))
    R, G, B = body_masks(base['co'], dna, rng)
    set_masks(body, R, G, B)
    dens, leng = body_fur_maps(base['co'], P, dict(eyes=eye_feats, nose=nose_p, mouth=mouth_p,
                                                   horns=horn_feats, s=s))
    cols = dict(fur=dna['fur'], light=dna['belly'], tip=dna.get('points', dna['fur']),
                pattern=dna.get('pattern_color', dna['fur']),
                pattern_amt=dna.get('pattern_strength', 0.0), tip_light=dna.get('tip_light', 0.15))
    me.materials.append(skin_material(name + '_Skin', cols))
    me.materials.append(hair_material(name + '_Fur', cols, ember, ember_from='B'))
    add_hair(body, 'Fur', 9000 * Q['count'], dna.get('fur_len', 0.08), 36 * Q['children'], 2,
             comb=Vector((0.0, 0.85, -0.5)), beta=0.78, normals=base['nrm'], dens=dens, leng=leng,
             radius=0.0035, steps=Q['steps'], clump=0.42, rough=0.008, seed=dna.get('seed', 7))
    root = link(bpy.data.objects.new(name, None))
    for ob in parts:
        ob.parent = root
    return root
