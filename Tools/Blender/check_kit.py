# -*- coding: utf-8 -*-
"""
check_kit.py — Kiểm tra FBX Gen 1 vừa xuất: nạp lại file, in khung bao từng bộ phận,
kiểm tra đuôi gắn sau mông (không rơi xuống đất), rồi render ảnh xem trước kiểu "trong game"
(không có lông particle, màu lấy từ vertex color giống shader Unity).

Cách chạy (từ gốc repo):
    blender -b --factory-startup --python Tools/Blender/check_kit.py -- \
        Assets/PawFit/Resources/Pets/Gen1/PawFit_Gen1_PetKit.fbx out/check_kit.jpg
(không truyền tham số thì dùng đúng 2 đường dẫn trên). Dòng "CHECK_RESULT PASS" = đạt.
"""

import bpy
import os
import sys
import json
import math
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pawfit_kit as K  # noqa: E402

EYE_KINDS = ('tron', 'hanh_nhan', 'meo', 'lim_dim', 'cun', 'sac_hep')

# 3 con mẫu: (màu lông, bụng, chân/chóp, hoa văn, kiểu hoa văn, mắt, màu mắt, tai, sừng, kiểu sừng, đuôi, độ nhắm mắt, head_big, chubby, legs_long)
VARIANTS = [
    dict(fur='#E59A4E', belly='#FFF0DC', points='#5A3B2C', pattern='#7A3F1E', pat='solid', eye='hanh_nhan',
         iris='#2FB3C2', ear='nhon', horns=[], horn='thang', tail='cao', closed=0.0, head=0.4, chubby=0.2, legs=0.3),
    dict(fur='#8E9AA8', belly='#EEF1F4', points='#3A4250', pattern='#2E3440', pat='stripes', eye='tron',
         iris='#E9B23B', ear='tron', horns=['PR', 'PL'], horn='thang', tail='tua', closed=0.0, head=0.8, chubby=0.4,
         legs=0.0),
    dict(fur='#F3EEE6', belly='#FFFFFF', points='#B8A79A', pattern='#6B5A50', pat='spots', eye='lim_dim',
         iris='#58A6E6', ear='cup', horns=['C'], horn='cong', tail='bong', closed=1.0, head=0.2, chubby=0.9, legs=0.2),
    dict(fur='#3B3346', belly='#6F6480', points='#1C1822', pattern='#8F7BC8', pat='solid', eye='sac_hep',
         iris='#EA4450', ear='vay', horns=['C', 'WR', 'WL'], horn='dai', tail='xoan', closed=0.0, head=0.3,
         chubby=0.1, legs=0.9),
    dict(fur='#C46A3A', belly='#F6D9B8', points='#4A2A1C', pattern='#5A2A14', pat='stripes', eye='cun',
         iris='#4CC38A', ear='nhon', horns=[], horn='thang', tail='vay', closed=0.5, head=0.6, chubby=0.5, legs=0.5),
]


def bbox(ob):
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def mask_material(name, fur, light, points, pattern, pat):
    """Giống shader Unity PawFit/Gen1Fur: lông -> hoa văn (B sọc / A đốm) -> chân (G) -> vùng sáng (R)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bs = nt.nodes['Principled BSDF']
    bs.inputs['Roughness'].default_value = 0.85
    attr = nt.nodes.new('ShaderNodeAttribute')
    attr.attribute_name = 'Masks'
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    nt.links.new(attr.outputs['Color'], sep.inputs['Color'])

    def mix(a, b, fac):
        m = nt.nodes.new('ShaderNodeMix')
        m.data_type = 'RGBA'
        if isinstance(a, tuple):
            m.inputs[6].default_value = a
        else:
            nt.links.new(a, m.inputs[6])
        m.inputs[7].default_value = b
        if isinstance(fac, float):
            m.inputs[0].default_value = fac
        else:
            nt.links.new(fac, m.inputs[0])
        return m.outputs[2]
    col = K.C(fur)
    if pat == 'stripes':
        col = mix(col, K.C(pattern), sep.outputs['Blue'])
    elif pat == 'spots':
        col = mix(col, K.C(pattern), attr.outputs['Alpha'])
    col = mix(col, K.C(points), sep.outputs['Green'])
    col = mix(col, K.C(light), sep.outputs['Red'])
    nt.links.new(col, bs.inputs['Base Color'])
    return mat


def set_key(ob, name, w):
    if ob.data.shape_keys and name in ob.data.shape_keys.key_blocks:
        ob.data.shape_keys.key_blocks[name].value = w


def from_unity(a):
    """Unity (x, y, z) -> Blender (-x, -z, y)."""
    return Vector((-a[0], -a[2], a[1]))


def apply_anchors(anchors, weights, head_parts, tail):
    """Dời bộ phận theo blendshape thân — y hệt cách Gen1PetAvatar làm trong Unity."""
    hc = from_unity(anchors['headCenter'])
    dh, dt, sc = Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0)), 1.0
    for k in anchors['keys']:
        w = weights.get(k['name'], 0.0)
        dh += from_unity(k['head']) * w
        dt += from_unity(k['tail']) * w
        sc += (k['headScale'] - 1.0) * w
    M = Matrix.Translation(hc + dh) @ Matrix.Scale(sc, 4) @ Matrix.Translation(-hc)
    for o in head_parts:
        o.matrix_world = M @ o.matrix_world
    tail.matrix_world = Matrix.Translation(dt) @ tail.matrix_world


def spawn(src, v, x, idx, anchors=None):
    """Nhân bản bộ thú đã nạp thành 1 con theo cấu hình v, đặt lệch theo trục X."""
    made = []

    def dup(name):
        o = src[name].copy()
        o.data = src[name].data.copy()
        o.name = '%s_%d' % (name, idx)
        bpy.context.scene.collection.objects.link(o)
        mw = src[name].matrix_world.copy()
        o.parent = None
        for m in list(o.modifiers):
            o.modifiers.remove(m)
        o.matrix_world = mw
        o.location.x += x
        o.hide_render = False
        made.append(o)
        return o
    body = dup('PF_Body')
    for k, w in (('head_big', v['head']), ('body_chubby', v['chubby']), ('legs_long', v['legs'])):
        set_key(body, k, w)
    body.data.materials.clear()
    body.data.materials.append(mask_material('Body%d' % idx, v['fur'], v['belly'], v['points'], v['pattern'], v['pat']))
    head_parts = []
    for side in ('R', 'L'):
        eye = dup('PF_Eye_' + side)
        liner = dup('PF_Liner_' + side)
        for o in (eye, liner):
            if v['eye'] != 'tron':
                set_key(o, v['eye'], 1.0 - v['closed'])
            set_key(o, 'closed_' + v['eye'], v['closed'])
        eye.data.materials.clear()
        eye.data.materials.append(K.eye_material('Eye%d%s' % (idx, side), v['iris'], 1 if side == 'R' else -1))
        liner.data.materials.clear()
        liner.data.materials.append(K.simple_material('Liner%d' % idx, '#1C1416', rough=0.45))
        ear = dup('PF_Ear_%s_%s' % (v['ear'], side))
        ear.data.materials.clear()
        ear.data.materials.append(mask_material('Ear%d' % idx, v['fur'], '#F2B6AE', v['points'], v['pattern'], 'solid'))
        head_parts += [eye, liner, ear]
    for nm in ('PF_Nose', 'PF_Mouth'):
        o = dup(nm)
        o.data.materials.clear()
        o.data.materials.append(K.simple_material(nm + str(idx), '#2E2026', rough=0.35))
        head_parts.append(o)
    for slot in v['horns']:
        h = dup('PF_Horn_' + slot)
        if v['horn'] != 'thang':
            set_key(h, v['horn'], 1.0)
        h.data.materials.clear()
        h.data.materials.append(K.horn_material('Horn%d' % idx, '#EFE6D6', '#FFFFFF'))
        head_parts.append(h)
    tail = dup('PF_Tail')
    if v['tail'] != 'cao':
        set_key(tail, v['tail'], 1.0)
    tail.data.materials.clear()
    tail.data.materials.append(mask_material('Tail%d' % idx, v['fur'], v['belly'], v['points'], v['pattern'],
                                             'stripes' if v['pat'] == 'stripes' else 'solid'))
    if anchors is not None:
        apply_anchors(anchors, dict(head_big=v['head'], body_chubby=v['chubby'], legs_long=v['legs']),
                      head_parts, tail)
    return body, head_parts, tail


def setup_render(res):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Punchy'
    sc.view_settings.exposure = -0.2
    w = bpy.data.worlds.new('W')
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = K.C('#CFE3D8')
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.9
    sc.world = w
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.2
    sun.data.angle = math.radians(8)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sc.collection.objects.link(sun)
    gm = bpy.data.meshes.new('Ground')
    s = 30.0
    gm.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    g = bpy.data.objects.new('Ground', gm)
    mat = bpy.data.materials.new('GroundMat')
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = K.C('#9CC58A')
    gm.materials.append(mat)
    sc.collection.objects.link(g)


def camera(loc, target, lens):
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.lens = lens
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = cam
    return cam


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main(fbx, out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    src = {o.name: o for o in bpy.context.scene.objects}
    print("IMPORTED:", sorted(n for n in src))
    ok = True
    for n in sorted(src):
        o = src[n]
        if o.type != 'MESH':
            continue
        lo, hi = bbox(o)
        nk = len(o.data.shape_keys.key_blocks) - 1 if o.data.shape_keys else 0
        print("  %-16s verts=%6d keys=%2d  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (
            n, len(o.data.vertices), nk, lo.x, hi.x, lo.y, hi.y, lo.z, hi.z))
        if lo.z < -0.02:                   # bàn chân lún 1 cm cho chắc chân là bình thường
            print("  !! %s nằm dưới mặt đất" % n)
            ok = False
    lo, hi = bbox(src['PF_Tail'])
    tail_ok = lo.z > 0.25 and lo.y > 0.2       # gốc đuôi phải ở sau mông, cao hơn đất
    print("CHECK tail behind hips (min z > 0.25, min y > 0.2): %s  [z %.2f, y %.2f]" % (tail_ok, lo.z, lo.y))
    ok = ok and tail_ok
    for n in src:
        if n.startswith('Kit_'):
            print("  !! còn object mẫu %s" % n)
            ok = False
    print("CHECK_RESULT", "PASS" if ok else "FAIL")

    for o in list(src.values()):          # giấu bộ gốc, chỉ render các con mẫu nhân bản
        o.hide_render = True
    setup_render((1600, 620))
    roots = []
    ap = os.path.join(os.path.dirname(fbx), 'PawFit_Gen1_Anchors.json')
    anchors = json.load(open(ap, encoding='utf-8')) if os.path.exists(ap) else None
    print("ANCHORS", ap if anchors else "KHÔNG CÓ (bộ phận sẽ không dời theo blendshape)")
    for i, v in enumerate(VARIANTS):
        body, head_parts, tail = spawn(src, v, 0.0, i, anchors)
        root = bpy.data.objects.new('Pet%d' % i, None)
        bpy.context.scene.collection.objects.link(root)
        for o in [body, tail] + head_parts:
            o.parent = root
        roots.append(root)
    tmp = os.path.splitext(out)[0]
    n = len(roots)

    def row(axis, gap):
        for i, rt in enumerate(roots):
            off = (i - (n - 1) / 2.0) * gap
            rt.location = Vector((off, 0.0, 0.0)) if axis == 'x' else Vector((0.0, -off, 0.0))

    def frame(span, lens):
        hfov = 2.0 * math.atan(18.0 / lens)
        return (span / 2.0) / math.tan(hfov / 2.0) * 1.05
    row('x', 1.3)                                              # hàng ngang, nhìn chéo 3/4 từ trước
    d = frame(n * 1.3, 50)
    camera((-0.18 * d, -d, 0.42 * d), (0.0, 0.0, 0.62), 50)
    render_to(tmp + '_front.png')
    row('y', 2.0)                                              # nhìn ngang: thấy đuôi gắn sau mông
    d = frame(n * 2.0 + 0.4, 50)
    camera((-d, 0.0, 1.2), (0.0, 0.0, 0.55), 50)
    render_to(tmp + '_side.png')
    stitch([tmp + '_front.png', tmp + '_side.png'], out)
    print("PREVIEW", out)


def stitch(paths, out):
    import numpy as np
    imgs = [bpy.data.images.load(p) for p in paths]
    w, h = imgs[0].size
    arr = [np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4) for im in imgs]
    full = np.concatenate(list(reversed(arr)), axis=0)      # pixels của Blender đếm từ dưới lên
    res = bpy.data.images.new('sheet', w, h * len(arr))
    res.pixels.foreach_set(full.ravel())
    res.filepath_raw = out
    res.file_format = 'JPEG'
    bpy.context.scene.render.image_settings.quality = 90
    res.save()
    for p in paths:
        os.remove(p)


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    root = os.path.join(HERE, '..', '..')
    main(args[0] if args else os.path.join(root, 'Assets', 'PawFit', 'Resources', 'Pets', 'Gen1', 'PawFit_Gen1_PetKit.fbx'),
         args[1] if len(args) > 1 else os.path.join(root, 'out', 'check_kit.jpg'))
