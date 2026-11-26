# -*- coding: utf-8 -*-
"""
render_demo.py — Render ảnh demo Phase 0.5 (POC hình ảnh thú PawFit).

Cách chạy (Blender 4.x, không cần GPU):
    blender -b --factory-startup --python Tools/Blender/render_demo.py -- <cảnh> <file.png> [draft|final]

Các cảnh:
    hero      Baby Kem Cam, góc 3/4, cận cảnh
    angles    Baby Kem Cam ở 3 góc: trước / ngang / sau
    variants  4 biến thể DNA khác nhau
    morph     Tiến hóa Baby -> Child -> Teen -> Adult Lửa (biến đổi dần)
"""

import bpy
import os
import sys
import math
import time
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pawfit_kit as K  # noqa: E402

# ----------------------------------------------------------------------------
# DNA mẫu (mỗi con là 1 bộ gene hình dạng)
# ----------------------------------------------------------------------------

V1_KEM_CAM = dict(  # tai nhọn, đuôi cáo, mắt hạnh nhân xanh ngọc, 0 sừng, lông trơn
    seed=11, fur='#E8853C', belly='#FFF3E4', points='#6B3923', pattern='solid',
    eye_shape='hanh_nhan', eye_color='#2FB3C2', ear='nhon', ear_tip=0.85,
    ear_tip_color='#5E3220', horns=0, tail='cao', tail_tip='#FFF7EF',
    nose='#3A2226', inner_ear='#F3B2AA', fur_len=0.085)

V2_XAM_SOC = dict(  # tai tròn, đuôi lụ túa, mắt tròn hổ phách, 2 sừng thẳng, lông sọc, đầu to
    seed=23, fur='#8C9DB0', belly='#EEF2F6', points='#3F4A5A', pattern='stripes',
    pattern_color='#323A4C', pattern_strength=1.0, eye_shape='tron', eye_color='#E9B23B',
    ear='tron', ear_tip=0.3, ear_tip_color='#3F4A5A', horns=2, horn_shape='thang',
    horn_len=0.11, horn_color='#EDE3CF', horn_tip='#FFFFFF', tail='tua', tail_tip='#3D4656',
    head_big=0.7, nose='#4A3A42', inner_ear='#E8B4B4', fur_len=0.08)

V3_TRANG_DOM = dict(  # tai cụp, đuôi bông, mắt lim dim xanh dương, 1 sừng cong, lông đốm, mũm mĩm
    seed=37, fur='#F2EDE8', belly='#FFFFFF', points='#B08465', pattern='spots',
    pattern_color='#A87656', pattern_strength=1.0, eye_shape='lim_dim', eye_color='#58A6E6',
    ear='cup', ear_tip=0.0, horns=1, horn_shape='cong', horn_len=0.14,
    horn_color='#F4ECF3', horn_tip='#D9B8E6', tail='bong', tail_tip='#FFFFFF',
    body_chubby=0.85, nose='#D98891', inner_ear='#F6C2C6', fur_len=0.09)

V4_DEM_TIM = dict(  # tai vây, đuôi xoăn, mắt sắc hẹp đỏ, 3 sừng dài, chân dài
    seed=41, fur='#3C3544', belly='#6A607A', points='#1C1920', pattern='solid',
    eye_shape='sac_hep', eye_color='#EA4450', ear='vay', ear_tip=0.6, ear_tip_color='#8F7BC8',
    horns=3, horn_shape='dai', horn_len=0.15, horn_color='#2A2331', horn_tip='#B8A4EC',
    horn_glow=1.2, tail='xoan', tail_tip='#8F7BC8', legs_long=0.85, paws_dark=0.8,
    nose='#1C181E', inner_ear='#5A4A6A', tip_light=0.45, liner='#0E0A10', fur_len=0.075)

FIRE = dict(  # đích tiến hóa: Adult hệ Lửa (đỏ cam, chân + lưng sẫm, sọc than hồng phát sáng)
    fur='#C93A18', belly='#FFD29A', points='#1E0D09', pattern_color='#FFB43A',
    eye_color='#F4B63A', ear_tip_color='#1E0C08', tail_tip='#FFC04A', nose='#221210')


def fire_stage(t):
    """DNA của con Kem Cam ở mốc tiến hóa t (0 = Baby, 1 = Adult Lửa): mọi tham số trượt dần."""
    d = dict(V1_KEM_CAM)
    for key in ('fur', 'belly', 'points', 'ear_tip_color', 'tail_tip', 'nose'):
        d[key] = K.mixc(V1_KEM_CAM[key], FIRE[key], t)
    d['pattern'] = 'stripes'
    d['pattern_color'] = FIRE['pattern_color']
    d['pattern_strength'] = K.smoothstep(0.2, 0.9, t)
    d['eye_shape'] = K.lerp_eye('hanh_nhan', 'meo', t)
    d['eye_color'] = K.mixc(V1_KEM_CAM['eye_color'], FIRE['eye_color'], t)
    d['eye_scale'] = K.lerp(1.0, 0.9, t)
    d['ear_scale'] = K.lerp(1.0, 1.12, t)
    d['ear_tip'] = K.lerp(0.85, 0.95, t)
    d['tail_scale'] = K.lerp(1.0, 1.22, t)
    d['paws_dark'] = K.lerp(0.0, 0.9, t)       # chân sẫm dần
    d['dorsal'] = K.lerp(0.3, 0.75, t)         # lưng sẫm dần
    d['belly_amount'] = K.lerp(1.0, 0.55, t)  # bụng bớt sáng: thân đỏ hơn
    d['fur_len'] = K.lerp(0.085, 0.10, t)
    if t < 0.5:
        d['horns'] = 0
    elif t < 0.85:
        d['horns'], d['horn_shape'], d['horn_len'] = 2, 'dai', K.lerp(0.06, 0.14, (t - 0.5) / 0.35)
    else:
        d['horns'], d['horn_shape'], d['horn_len'] = 2, 'cong', 0.30
    d['horn_color'] = '#1E1412'
    d['horn_tip'] = '#FFB347'
    d['horn_glow'] = K.smoothstep(0.6, 1.0, t) * 4.0
    amt = K.smoothstep(0.35, 1.0, t)
    d['ember'] = dict(color='#FF8A1E', strength=2.6, tips=0.12, amount=amt) if amt > 0 else None
    return d


# ----------------------------------------------------------------------------
# Dựng cảnh: phông nền cong, 3 đèn mềm (chính / phụ / viền), camera
# ----------------------------------------------------------------------------

def make_backdrop(color, y_back=2.2, radius=2.0, width=30.0, height=12.0):
    prof = [(y, 0.0) for y in (-16.0, -8.0, -3.0, 0.0, y_back)]
    for k in range(1, 15):
        a = (math.pi / 2) * k / 14
        prof.append((y_back + radius * math.sin(a), radius * (1.0 - math.cos(a))))
    prof.append((y_back + radius, height))
    verts, faces = [], []
    for (y, z) in prof:
        verts += [(-width / 2, y, z), (width / 2, y, z)]
    for i in range(len(prof) - 1):
        a = i * 2
        faces.append((a, a + 1, a + 3, a + 2))
    ob = K.new_mesh_object('Backdrop', [Vector(v) for v in verts], faces)
    ob.data.materials.append(K.simple_material('BackdropMat', color, rough=0.95))
    return ob


def setup(res, samples, look, cam_loc, lens, bg='#C9DDD5', light_scale=1.0,
          y_back=2.2, radius=2.0, fstop=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    K.reset_cache()
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    cy = sc.cycles
    cy.device = 'CPU'
    cy.samples = samples
    cy.use_adaptive_sampling = True
    cy.adaptive_threshold = 0.02
    cy.use_denoising = False          # bản Blender của Ubuntu không có OpenImageDenoise
    cy.max_bounces = 8
    cy.diffuse_bounces = 3
    cy.glossy_bounces = 3
    cy.transmission_bounces = 6
    cy.transparent_max_bounces = 16
    cy.sample_clamp_indirect = 3.0
    cy.blur_glossy = 0.6
    cy.caustics_reflective = False
    cy.caustics_refractive = False
    sc.cycles_curves.shape = 'RIBBONS'
    sc.cycles_curves.subdivisions = 2
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.exposure = -0.35
    try:
        sc.view_settings.look = 'AgX - Punchy'
    except Exception:
        pass
    w = bpy.data.worlds.new('World')
    w.use_nodes = True
    bgn = w.node_tree.nodes['Background']
    bgn.inputs['Color'].default_value = K.mulc(bg, 0.9)
    bgn.inputs['Strength'].default_value = 0.25
    sc.world = w
    make_backdrop(bg, y_back=y_back, radius=radius)
    look = Vector(look)
    tgt = K.link(bpy.data.objects.new('Target', None))
    tgt.location = look
    cam_d = bpy.data.cameras.new('Cam')
    cam_d.lens = lens
    cam_d.clip_end = 200.0
    if fstop:
        cam_d.dof.use_dof = True
        cam_d.dof.focus_object = tgt
        cam_d.dof.aperture_fstop = fstop
    cam = K.link(bpy.data.objects.new('Cam', cam_d))
    cam.location = Vector(cam_loc)
    con = cam.constraints.new('TRACK_TO')
    con.target = tgt
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    sc.camera = cam
    L = light_scale

    def area(name, off, size, energy, color):
        ld = bpy.data.lights.new(name, 'AREA')
        ld.size = size * L
        ld.energy = energy * L * L
        ld.color = color
        ob = K.link(bpy.data.objects.new(name, ld))
        ob.location = look + Vector(off) * L
        c = ob.constraints.new('TRACK_TO')
        c.target = tgt
        c.track_axis = 'TRACK_NEGATIVE_Z'
        c.up_axis = 'UP_Y'

    area('Key', (-2.6, -3.0, 2.8), 2.5, 420.0, (1.0, 0.95, 0.88))    # đèn chính ấm, mềm
    area('Fill', (3.2, -2.2, 1.0), 3.5, 110.0, (0.88, 0.94, 1.0))    # đèn phụ lạnh
    area('Rim', (1.2, 3.0, 2.6), 2.0, 650.0, (1.0, 0.97, 0.92))      # đèn viền sau: làm sáng viền lông
    return sc


def place(root, x=0.0, y=0.0, rot_deg=0.0):
    root.location = (x, y, 0.0)
    root.rotation_euler = (0.0, 0.0, math.radians(rot_deg))


def render(out):
    sc = bpy.context.scene
    sc.render.filepath = out
    t = time.time()
    bpy.ops.render.render(write_still=True)
    print("RENDER_DONE %s %.1fs" % (out, time.time() - t))


# ----------------------------------------------------------------------------
# Các cảnh
# ----------------------------------------------------------------------------

def scene_hero(out, q):
    res = (560, 560) if q == 'draft' else (1100, 1100)
    setup(res, 24 if q == 'draft' else 160, look=(-0.08, -0.22, 0.60),
          cam_loc=(-1.55, -2.75, 0.98), lens=70, fstop=5.6)
    root = K.build_creature('Hero', V1_KEM_CAM, 0.0, pose=dict(roll=-12, yaw=-22, pitch=-5), quality=q)
    place(root, rot_deg=24)
    render(out)


def scene_angles(out, q):
    res = (900, 360) if q == 'draft' else (1800, 720)
    setup(res, 20 if q == 'draft' else 112, look=(0.0, 0.0, 0.50), cam_loc=(0.0, -7.6, 1.45),
          lens=50, light_scale=1.6, y_back=3.5, radius=3.0)
    for i, (x, rot) in enumerate(((-1.55, 0.0), (0.0, -90.0), (1.55, 155.0))):
        place(K.build_creature('A%d' % i, V1_KEM_CAM, 0.0, quality=q), x=x, rot_deg=rot)
    render(out)


def scene_variants(out, q):
    res = (1000, 430) if q == 'draft' else (2000, 860)
    setup(res, 20 if q == 'draft' else 112, look=(0.0, 0.0, 0.52), cam_loc=(-0.6, -8.2, 1.5),
          lens=50, light_scale=1.6, y_back=3.5, radius=3.0)
    dnas = (V1_KEM_CAM, V2_XAM_SOC, V3_TRANG_DOM, V4_DEM_TIM)
    poses = (dict(roll=-8, yaw=-6), dict(roll=8, yaw=-4), dict(roll=-10, yaw=-8), None)
    for i, (dna, pose) in enumerate(zip(dnas, poses)):
        place(K.build_creature('V%d' % (i + 1), dna, 0.0, pose=pose, quality=q),
              x=-2.1 + 1.4 * i, rot_deg=-40)
    render(out)


def scene_morph(out, q):
    res = (1000, 430) if q == 'draft' else (2000, 860)
    setup(res, 20 if q == 'draft' else 112, look=(0.0, 0.0, 0.68), cam_loc=(-0.6, -8.8, 1.7),
          lens=50, light_scale=1.6, y_back=3.5, radius=3.0)
    for i, (t, x) in enumerate(((0.0, -2.35), (0.33, -0.85), (0.66, 0.68), (1.0, 2.3))):
        place(K.build_creature('M%d' % i, fire_stage(t), t, quality=q), x=x, rot_deg=-40)
    render(out)


SCENES = dict(hero=scene_hero, angles=scene_angles, variants=scene_variants, morph=scene_morph)

if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    name = args[0] if args else 'hero'
    out = args[1] if len(args) > 1 else os.path.join(HERE, name + '.png')
    quality = args[2] if len(args) > 2 else 'draft'
    t0 = time.time()
    SCENES[name](out, quality)
    print("TOTAL %.1fs" % (time.time() - t0))
