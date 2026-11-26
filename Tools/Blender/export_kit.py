# -*- coding: utf-8 -*-
"""
export_kit.py — Xuất bộ model thú Gen 1 (FBX bản 2) + file mốc neo (JSON) cho Unity.

Cách chạy (từ gốc repo):
    blender -b --factory-startup --python Tools/Blender/export_kit.py -- Assets/PawFit/Resources/Pets/Gen1

Tạo 2 file trong thư mục đích:

`PawFit_Gen1_PetKit.fbx` (mét, Unity: mặt thú nhìn +Z, chân chạm y = 0)
  * PF_Rig        : xương hips, spine, chest, neck, head, legFR/FL/BR/BL, tail
  * PF_Body       : thân Baby chi tiết (LOD0, ~38k đỉnh) — PC / Editor
  * PF_Body_LOD1  : thân Baby nhẹ (LOD1, ~13k đỉnh) — điện thoại
      blendshape  : to_adult_fire / water / light / steel / shadow, head_big, body_chubby, legs_long
      vertex color: R = vùng sáng (bụng / mõm / má / ngực), G = bàn chân, B = sọc, A = đốm
      UV0 = (độ dài lông, mật độ lông) để dành cho shader lông (Phase 6)
      UV1 = (vùng lưng, vết bẩn)
  * PF_Eye_R/L, PF_Liner_R/L : mắt + viền mắt. Blendshape: 5 dạng mắt (gốc = tròn) + closed_<dạng> (nhắm)
  * PF_Nose, PF_Mouth
  * PF_Ear_<kiểu>_R/L : 4 kiểu tai nhon / tron / cup / vay (Unity giữ đúng 1 cặp theo DNA)
  * PF_Horn_C/PR/PL/WR/WL : 5 khe sừng. Blendshape: dai, cong (gốc = thang)
  * PF_Tail       : đuôi, gốc nằm sau mông. Blendshape: tua, bong, vay, xoan (gốc = cao)
  Bộ phận là khối cứng, pivot ở chân bộ phận. Unity gắn vào xương head / hips lúc chạy.
  _R = phía +X của Blender = phía -X trong Unity.

`PawFit_Gen1_Anchors.json`: tâm đầu, gốc đuôi + độ dời của chúng theo từng blendshape thân
  (tọa độ Unity). Unity dùng để dời bộ phận theo khi đầu to ra / chân dài ra.

Bản 1 có 2 lỗi đã sửa ở bản này:
  * đuôi gắn sát đất (tâm hông tính sai, xem pawfit_kit.tail_origin)
  * các mẫu Kit_* nằm dưới đất phía sau thú
"""

import bpy
import os
import sys
import json
import math
import random
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pawfit_kit as K  # noqa: E402

FBX_NAME = 'PawFit_Gen1_PetKit.fbx'
ANCHORS_NAME = 'PawFit_Gen1_Anchors.json'

# Xương ở tư thế Baby: (tên, đầu xương, đuôi xương, xương cha)
BONES = [
    ('hips',  (0, 0.30, 0.40), (0, 0.12, 0.42), None),
    ('spine', (0, 0.12, 0.42), (0, -0.05, 0.43), 'hips'),
    ('chest', (0, -0.05, 0.43), (0, -0.17, 0.50), 'spine'),
    ('neck',  (0, -0.17, 0.52), (0, -0.26, 0.66), 'chest'),
    ('head',  (0, -0.26, 0.66), (0, -0.32, 0.92), 'neck'),
    ('legFR', (0.12, -0.15, 0.36), (0.12, -0.18, 0.07), 'chest'),
    ('legFL', (-0.12, -0.15, 0.36), (-0.12, -0.18, 0.07), 'chest'),
    ('legBR', (0.13, 0.25, 0.33), (0.13, 0.21, 0.07), 'hips'),
    ('legBL', (-0.13, 0.25, 0.33), (-0.13, 0.21, 0.07), 'hips'),
    ('tail',  (0, 0.40, 0.45), (0, 0.62, 0.58), 'hips'),
]

EYE_KINDS = ('tron', 'hanh_nhan', 'meo', 'lim_dim', 'cun', 'sac_hep')   # thứ tự = enum EyeShape
EAR_KINDS = ('nhon', 'tron', 'cup', 'vay')                              # = enum EarType
HORN_KINDS = ('thang', 'dai', 'cong')                                   # = enum HornShape
TAIL_KINDS = ('cao', 'tua', 'bong', 'vay', 'xoan')                      # = enum TailType
EYE_YAW, EYE_PITCH = 34.0, -3.0
HORN_LEN = 0.13          # độ dài sừng Baby (sừng 'dai' dài hơn 35%)
TAIL_FLUFF = 0.5         # đuôi bản game dày thêm nửa độ dài lông (không có lông particle)


def to_unity(v):
    """Blender (x, y, z) -> Unity (-x, z, -y) (FBX: forward -Z, up Y; Unity lật trục X)."""
    return [round(-v.x, 5), round(v.z, 5), round(-v.y, 5)]


def dirt_noise(p):
    """Vết bẩn cố định theo vị trí (0..1), nướng vào UV1.y để không 'trôi' khi xương chuyển động."""
    return max(0.0, min(1.0, 0.5 + 0.55 * noise.noise(p * 6.5) + 0.25 * noise.noise(p * 17.0)))


# ---------------------------------------------------------------------------
# Ghi dữ liệu đỉnh cho Unity
# ---------------------------------------------------------------------------

def write_rgba(ob, rgba_of_vertex):
    """Vertex color 'Masks' (RGBA) theo đỉnh. Xuất FBX dạng LINEAR => Unity đọc đúng giá trị."""
    me = ob.data
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    attr = me.color_attributes.new('Masks', 'BYTE_COLOR', 'CORNER')
    lv = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', lv)
    flat = []
    for vi in lv:
        flat.extend(rgba_of_vertex[vi])
    attr.data.foreach_set('color', flat)


def write_rgba_loops(ob, rgba_of_loop):
    me = ob.data
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    attr = me.color_attributes.new('Masks', 'BYTE_COLOR', 'CORNER')
    flat = []
    for c in rgba_of_loop:
        flat.extend(c)
    attr.data.foreach_set('color', flat)


def add_uv(ob, name, uv_of_vertex):
    me = ob.data
    lay = me.uv_layers.new(name=name)
    lv = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', lv)
    flat = []
    for vi in lv:
        flat.extend(uv_of_vertex[vi])
    lay.data.foreach_set('uv', flat)


def world_verts(ob):
    mw = ob.matrix_world
    return [mw @ v.co for v in ob.data.vertices]


def add_keys(ob, keys):
    """keys = [(tên, danh sách đỉnh cục bộ)] — cùng số đỉnh với mesh gốc."""
    if ob.data.shape_keys is None:
        ob.shape_key_add(name='Basis', from_mix=False)
    for nm, verts in keys:
        assert len(verts) == len(ob.data.vertices), (ob.name, nm, len(verts), len(ob.data.vertices))
        kb = ob.shape_key_add(name=nm, from_mix=False)
        flat = []
        for p in verts:
            flat.extend((p[0], p[1], p[2]))
        kb.data.foreach_set('co', flat)
        kb.value = 0.0


def recenter(ob, pivot):
    """Dời pivot của object về `pivot` (tọa độ thế giới) mà hình không đổi chỗ."""
    inv = ob.matrix_world.inverted()
    local = inv @ pivot
    ob.data.transform(Matrix.Translation(-local))
    ob.matrix_world = ob.matrix_world @ Matrix.Translation(local)


def smooth_all(ob):
    me = ob.data
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))


# ---------------------------------------------------------------------------
# Xương + thân
# ---------------------------------------------------------------------------

def make_rig(tail_origin):
    arm_d = bpy.data.armatures.new('PF_Rig')
    arm = K.link(bpy.data.objects.new('PF_Rig', arm_d))
    K.activate(arm)
    bpy.ops.object.mode_set(mode='EDIT')
    for name, h, t, parent in BONES:
        b = arm_d.edit_bones.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        if name == 'tail':                       # xương đuôi bắt đầu đúng ở gốc đuôi thật
            b.head = tail_origin.copy()
            b.tail = tail_origin + Vector((0.0, 0.22, 0.13))
        if parent:
            b.parent = arm_d.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def make_body(name, base, arm, feats):
    me = base['mesh'].copy()
    me.name = name + '_Mesh'
    ob = K.link(bpy.data.objects.new(name, me))
    for kb in me.shape_keys.key_blocks:
        kb.value = 0.0
    smooth_all(ob)
    co = base['co']
    R, G, B = K.body_masks(co, dict(paws_dark=1.0, dorsal=0.0, pattern='stripes'), random.Random(1))
    _, _, A = K.body_masks(co, dict(pattern='spots', spot_count=22), random.Random(7))
    write_rgba(ob, [(R[i], G[i], B[i], A[i]) for i in range(len(co))])
    # UV0 = bản đồ lông (độ dài, mật độ) như bản render — cho shader lông sau này
    dens, leng = K.body_fur_maps(co, co, feats)
    add_uv(ob, 'FurMap', [(leng[i], dens[i]) for i in range(len(co))])
    # UV1 = (vùng lưng để đổ bóng lưng, vết bẩn)
    dorsal = [K.smoothstep(0.44, 0.62, p.z) * K.smoothstep(-0.22, -0.06, p.y) * (1.0 - R[i])
              for i, p in enumerate(co)]
    add_uv(ob, 'Extra', [(dorsal[i], dirt_noise(p)) for i, p in enumerate(co)])
    # skin vào xương theo trọng số "xương ảo" của metaball
    for bone, w in base['bone_w'].items():
        K.set_vgroup(ob, bone, w)
    mod = ob.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    ob.parent = arm
    return ob


# ---------------------------------------------------------------------------
# Bộ phận (khối cứng)
# ---------------------------------------------------------------------------

def make_eyes(bvh, hc, fw, u, r, s):
    hw = 0.082 * s
    Rh = 0.29 * s
    out, feats = [], []
    for side in (1, -1):
        M, hit = K.eye_frame(bvh, hc, fw, u, r, s, side, math.radians(EYE_YAW), math.radians(EYE_PITCH))
        tag = 'R' if side > 0 else 'L'
        P0 = K.eye_params(EYE_KINDS[0])
        v, fc, uv = K.eye_lens_geom(P0, hw, Rh, side)
        lens = K.new_mesh_object('PF_Eye_' + tag, v, fc, uv)
        lens.matrix_world = M
        lv, lf, luv = K.eye_liner_geom(P0, hw, Rh, side)
        liner = K.new_mesh_object('PF_Liner_' + tag, lv, lf, luv)
        liner.matrix_world = M
        lens_keys, liner_keys = [], []
        for kind in EYE_KINDS:
            P = K.eye_params(kind)
            if kind != EYE_KINDS[0]:
                lens_keys.append((kind, K.eye_lens_geom(P, hw, Rh, side)[0]))
                liner_keys.append((kind, K.eye_liner_geom(P, hw, Rh, side)[0]))
        for kind in EYE_KINDS:
            P = K.eye_params(kind)
            lens_keys.append(('closed_' + kind, K.eye_lens_geom(P, hw, Rh, side, closed=True)[0]))
            liner_keys.append(('closed_' + kind, K.eye_liner_geom(P, hw, Rh, side, closed=True)[0]))
        add_keys(lens, lens_keys)
        add_keys(liner, liner_keys)
        out += [lens, liner]
        feats.append((hit.copy(), hw))
    return out, feats


def make_nose_mouth(bvh, hc, fw, u, r, s):
    (nose, mouth), nose_p, mouth_p = K.make_nose_mouth('PF', dict(), bvh, hc, fw, u, r, s)
    for ob in (nose, mouth):
        ob.data.materials.clear()
    recenter(mouth, mouth_p)
    return [nose, mouth], nose_p, mouth_p


def make_ears(bvh, hc, fw, u, r, s):
    out = []
    for kind in EAR_KINDS:
        for side in (1, -1):
            ob = K.make_ear_mesh('PF_Ear_%s_%s' % (kind, 'R' if side > 0 else 'L'),
                                 kind, side, bvh, hc, fw, u, r, s)
            loop_rgb, _ = K.ear_masks(ob, 1.0)           # R = mặt trong, G = chóp tai
            write_rgba_loops(ob, [(c[0], c[1], 0.0, 0.0) for c in loop_rgb])
            wv = world_verts(ob)
            add_uv(ob, 'Extra', [(0.0, dirt_noise(p)) for p in wv])
            out.append(ob)
    return out


def make_horns(bvh, hc, fw, u, r, s):
    out = []
    for slot in ('C', 'PR', 'PL', 'WR', 'WL'):
        M, hit, k = K.horn_frame(slot, bvh, hc, fw, u, r, s)

        def geom(kind):
            L = HORN_LEN * s * k * (1.35 if kind == 'dai' else 1.0)
            Rr = (0.026 + 0.08 * HORN_LEN) * s * k
            return K.horn_geom(kind, L, Rr)
        v, fc, uv = geom(HORN_KINDS[0])
        ob = K.new_mesh_object('PF_Horn_' + slot, v, fc, uv)
        ob.matrix_world = M
        add_keys(ob, [(kind, geom(kind)[0]) for kind in HORN_KINDS[1:]])
        write_rgba(ob, [(0.0, uv[i][1], 0.0, 0.0) for i in range(len(v))])   # G = gốc -> chóp sừng
        wv = world_verts(ob)
        add_uv(ob, 'Extra', [(0.0, dirt_noise(p)) for p in wv])
        out.append(ob)
    return out


def make_tail(bvh, P, base):
    origin = K.tail_origin(bvh, P, base, 1.0)
    v, fc, uv = K.tail_geom(TAIL_KINDS[0], 1.0, fluff=TAIL_FLUFF)
    ob = K.new_mesh_object('PF_Tail', v, fc, uv)
    ob.matrix_world = Matrix.Translation(origin)
    add_keys(ob, [(kind, K.tail_geom(kind, 1.0, fluff=TAIL_FLUFF)[0]) for kind in TAIL_KINDS[1:]])
    rgba = []
    for i in range(len(v)):
        t = uv[i][1]
        stripe = (K.smoothstep(0.5, 0.85, math.sin(2.0 * math.pi * 5.5 * t))
                  * (1.0 - K.smoothstep(0.75, 0.85, t)))
        rgba.append((0.0, K.smoothstep(0.62, 0.92, t), stripe, 0.0))      # G = chóp đuôi, B = sọc
    write_rgba(ob, rgba)
    wv = world_verts(ob)
    add_uv(ob, 'Extra', [(0.0, dirt_noise(p)) for p in wv])
    return ob, origin


# ---------------------------------------------------------------------------
# Mốc neo theo blendshape thân
# ---------------------------------------------------------------------------

def anchor_data(base, head_center, tail_origin, mouth_p):
    hwt = base['bone_w']['head']
    probe = K.link(bpy.data.objects.new('PF_Probe', base['mesh'].copy()))
    kb = probe.data.shape_keys.key_blocks
    keys = []
    for k in kb:
        if k.name == 'Basis':
            continue
        for k2 in kb:
            k2.value = 0.0
        k.value = 1.0
        P = K.evaluated_coords(probe)
        hc = K._centroid(P, hwt, 0.95)
        sc = K._mean_dist(P, hwt, 0.95, hc) / base['head_r']
        bvh = BVHTree.FromPolygons(P, base['polys'])
        to = K.tail_origin(bvh, P, base, 1.0)
        keys.append(dict(name=k.name, head=to_unity(hc - head_center), headScale=round(sc, 5),
                         tail=to_unity(to - tail_origin)))
    bpy.data.objects.remove(probe, do_unlink=True)
    return dict(version=2, headCenter=to_unity(head_center), headRadius=round(base['head_r'], 5),
                tailOrigin=to_unity(tail_origin), mouth=to_unity(mouth_p), keys=keys)


# ---------------------------------------------------------------------------

def main(out_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    K.reset_cache()
    base0 = K.ensure_base(K.LOD0_VOXEL)
    base1 = K.ensure_base(K.LOD1_VOXEL)
    # bộ phận đặt trên bề mặt thân Baby chi tiết
    P = base0['co']
    hc = K._centroid(P, base0['bone_w']['head'], 0.95)
    s = 1.0
    fw, u, r = Vector((0.0, -1.0, 0.0)), Vector((0.0, 0.0, 1.0)), Vector((1.0, 0.0, 0.0))
    bvh = BVHTree.FromPolygons(P, base0['polys'])
    eyes, eye_feats = make_eyes(bvh, hc, fw, u, r, s)
    nm, nose_p, mouth_p = make_nose_mouth(bvh, hc, fw, u, r, s)
    ears = make_ears(bvh, hc, fw, u, r, s)
    horns = make_horns(bvh, hc, fw, u, r, s)
    tail, tail_o = make_tail(bvh, P, base0)
    parts = eyes + nm + ears + horns + [tail]
    for ob in parts:
        smooth_all(ob)
    feats = dict(eyes=eye_feats, nose=nose_p, mouth=mouth_p, horns=[], s=s)
    arm = make_rig(tail_o)
    body0 = make_body('PF_Body', base0, arm, feats)
    body1 = make_body('PF_Body_LOD1', base1, arm, feats)
    anchors = anchor_data(base0, hc, tail_o, mouth_p)

    os.makedirs(out_dir, exist_ok=True)
    fbx = os.path.join(out_dir, FBX_NAME)
    for o in bpy.context.scene.objects:
        o.select_set(o.type in ('MESH', 'ARMATURE'))
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             use_armature_deform_only=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y')
    with open(os.path.join(out_dir, ANCHORS_NAME), 'w', encoding='utf-8') as f:
        json.dump(anchors, f, indent=1)
    print("EXPORT_OK %s" % fbx)
    print("  body LOD0=%d LOD1=%d verts | parts=%s" % (len(body0.data.vertices), len(body1.data.vertices),
                                                       [o.name for o in parts]))
    print("  tail origin (blender) = %s | head center = %s" % (tuple(round(c, 3) for c in tail_o),
                                                             tuple(round(c, 3) for c in hc)))


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    main(args[0] if args else os.path.join(HERE, '..', '..', 'Assets', 'PawFit', 'Resources', 'Pets', 'Gen1'))
