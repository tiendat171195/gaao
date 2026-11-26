# -*- coding: utf-8 -*-
"""
export_kit.py — Xuất bộ model thú (FBX) cho Unity: base mesh + blendshape + xương + bộ phận.

Cách chạy:
    blender -b --factory-startup --python Tools/Blender/export_kit.py -- Assets/PawFit/Art/Pet/PawFit_PetKit.fbx

Trong file FBX:
  * PF_Rig             : bộ xương (hips, spine, chest, neck, head, 4 chân, tail)
  * PF_Body            : thân Baby, blendshape: to_adult_fire, head_big, body_chubby, legs_long
                         Vertex color: R = vùng sáng (bụng/mõm/má/ngực), G = vùng chân,
                                       B = sọc mặc định, A = vùng lưng (đổ bóng lưng)
                         UV2: x = độ dài lông (0..1), y = mật độ lông (0..1)  -> shader lông shell ở Unity
  * PF_Eye*/Liner*/Nose/Mouth/Ear*/Tail : bộ phận của con mẫu Kem Cam, gắn vào xương head / tail
  * Kit_Ear / Kit_Tail / Kit_Horn / Kit_Eye : bộ kiểu dáng, mỗi kiểu là 1 blendshape
                         (morph mượt giữa các kiểu => đúng yêu cầu "không đổi model đột ngột")
"""

import bpy
import os
import sys
import math
import random
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pawfit_kit as K  # noqa: E402
import render_demo as D  # noqa: E402

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


def make_rig():
    arm_d = bpy.data.armatures.new('PF_Rig')
    arm = K.link(bpy.data.objects.new('PF_Rig', arm_d))
    K.activate(arm)
    bpy.ops.object.mode_set(mode='EDIT')
    for name, h, t, parent in BONES:
        b = arm_d.edit_bones.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        if parent:
            b.parent = arm_d.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def bind(ob, arm, weights_by_bone):
    """Gắn mesh vào xương: weights_by_bone = {tên xương: [trọng số theo đỉnh]}."""
    for bone, w in weights_by_bone.items():
        K.set_vgroup(ob, bone, w)
    mod = ob.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    mw = ob.matrix_world.copy()
    ob.parent = arm
    ob.matrix_world = mw


def strip_hair(ob):
    for m in list(ob.modifiers):
        if m.type == 'PARTICLE_SYSTEM':
            ob.modifiers.remove(m)


def kit_object(name, geoms, x_off):
    """1 mesh + mỗi kiểu dáng còn lại là 1 blendshape (cùng số đỉnh)."""
    (n0, (v0, f0, uv0)), rest = geoms[0], geoms[1:]
    ob = K.new_mesh_object(name, v0, f0, uv0)
    ob.shape_key_add(name='Basis_' + n0, from_mix=False)
    for nm, (v, f, uv) in rest:
        assert len(v) == len(v0), (name, nm)
        kb = ob.shape_key_add(name=nm, from_mix=False)
        flat = []
        for p in v:
            flat.extend((p[0], p[1], p[2]))
        kb.data.foreach_set('co', flat)
    ob.location = (x_off, 1.4, 0.0)
    return ob


def main(out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    K.reset_cache()
    root = K.build_creature('PF', D.V1_KEM_CAM, 0.0, quality='draft')
    base = K.ensure_base()
    parts = [o for o in bpy.data.objects if o.parent == root]
    for o in parts:
        strip_hair(o)
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    bpy.data.objects.remove(root, do_unlink=True)
    body = bpy.data.objects['PF_Body']
    me = body.data
    # bỏ blendshape 'pose' nếu có; giữ các blendshape DNA
    if me.shape_keys and 'pose' in me.shape_keys.key_blocks:
        body.shape_key_remove(me.shape_keys.key_blocks['pose'])
    for kb in me.shape_keys.key_blocks:
        kb.value = 0.0
    # vertex color đóng gói cho Unity: R sáng, G chân, B sọc mặc định, A lưng
    co = base['co']
    R, G, _ = K.body_masks(co, dict(paws_dark=1.0, dorsal=0.0), random.Random(1))
    _, _, B = K.body_masks(co, dict(pattern='stripes'), random.Random(1))
    A = [K.smoothstep(0.44, 0.62, p.z) * K.smoothstep(-0.22, -0.06, p.y) * (1.0 - r) for p, r in zip(co, R)]
    old = me.color_attributes.get('Masks')
    if old is not None:
        me.color_attributes.remove(old)
    attr = me.color_attributes.new('Masks', 'BYTE_COLOR', 'CORNER')
    lv = [0] * len(me.loops)
    me.loops.foreach_get('vertex_index', lv)
    flat = []
    for vi in lv:
        flat.extend((R[vi], G[vi], B[vi], A[vi]))
    attr.data.foreach_set('color', flat)
    # UV2 = (độ dài lông, mật độ lông) — lấy lại giá trị gốc trước khi bù hướng chải
    c = Vector((0.0, 0.85, -0.5)).normalized()
    mags = [(n + c * 0.78).length for n in base['nrm']]
    kmin = max(0.05, min(mags))
    vg_len, vg_den = body.vertex_groups['Fur_len'], body.vertex_groups['Fur_dens']
    raw_len, raw_den = [0.0] * len(co), [1.0] * len(co)
    for v in me.vertices:
        for g in v.groups:
            if g.group == vg_len.index:
                raw_len[v.index] = min(1.0, g.weight * mags[v.index] / kmin)
            elif g.group == vg_den.index:
                raw_den[v.index] = g.weight
    uv2 = me.uv_layers.new(name='FurMap')
    fl = []
    for vi in lv:
        fl.extend((raw_len[vi], raw_den[vi]))
    uv2.data.foreach_set('uv', fl)
    for vg in list(body.vertex_groups):
        body.vertex_groups.remove(vg)
    for o in parts:          # bộ phận: bỏ vertex group lông
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
    # xương + skin
    arm = make_rig()
    bind(body, arm, {b: base['bone_w'][b] for b in base['bone_w']})
    for o in parts:
        if o is body:
            continue
        n = len(o.data.vertices)
        bind(o, arm, {'tail' if o.name.endswith('_Tail') else 'head': [1.0] * n})
    # bộ kiểu dáng (blendshape theo kiểu)
    kits = [
        kit_object('Kit_Ear', [(k, K.ear_geom(k, 1.0)) for k in ('nhon', 'tron', 'cup', 'vay')], -1.2),
        kit_object('Kit_Tail', [(k, K.tail_geom(k, 1.0)) for k in ('cao', 'tua', 'bong', 'vay', 'xoan')], -0.4),
        kit_object('Kit_Horn', [(k, K.horn_geom(k, 0.22, 0.04)) for k in ('thang', 'dai', 'cong')], 0.4),
        kit_object('Kit_Eye', [(k, K.eye_lens_geom(K.eye_params(k), 0.08, 0.29, 1))
                               for k in ('tron', 'hanh_nhan', 'meo', 'lim_dim', 'cun', 'sac_hep')], 1.2),
    ]
    for o in bpy.context.scene.objects:
        o.select_set(o.type in ('MESH', 'ARMATURE'))
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             use_armature_deform_only=True, mesh_smooth_type='FACE',
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')
    keys = [kb.name for kb in me.shape_keys.key_blocks]
    print("EXPORT_OK %s body_verts=%d shape_keys=%s kits=%s"
          % (out, len(me.vertices), keys, [k.name for k in kits]))


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    main(args[0] if args else os.path.join(HERE, 'PawFit_PetKit.fbx'))
