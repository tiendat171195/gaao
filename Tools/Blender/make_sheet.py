# -*- coding: utf-8 -*-
"""
make_sheet.py — Gắn tiêu đề + chú thích tiếng Việt lên ảnh render (chạy bằng python3 thường, cần Pillow).

    python3 Tools/Blender/make_sheet.py <thư mục ảnh render> <thư mục xuất>
"""

import os
import sys
from PIL import Image, ImageDraw, ImageFont

FONT = '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
FONT_B = '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
BG = (246, 244, 240)
INK = (40, 38, 44)
SUB = (110, 104, 112)

SHEETS = [
    ('final_hero.png', '01_hero_baby.jpg',
     'PawFit · Baby (Ngày 0) — DNA "Kem Cam"',
     [(0.5, 'Tai nhọn · Đuôi cáo · Mắt hạnh nhân · 0 sừng',
       'Render Blender từ base mesh + blendshape — chưa phải ảnh trong game')]),
    ('final_angles.png', '02_baby_3_goc.jpg',
     'Baby "Kem Cam" — 3 góc nhìn',
     [(0.205, 'Trước', ''), (0.517, 'Ngang', ''), (0.794, 'Sau', '')]),
    ('final_variants.png', '03_bien_the_dna.jpg',
     '4 biến thể DNA — cùng 1 base mesh, khác gene',
     [(0.13, 'Kem Cam', 'tai nhọn · đuôi cáo · mắt hạnh nhân\n0 sừng · lông trơn'),
      (0.37, 'Xám Sọc', 'tai tròn · đuôi lụ túa · mắt tròn\n2 sừng thẳng · sọc · đầu to'),
      (0.60, 'Trắng Đốm', 'tai cụp · đuôi bông · mắt lim dim\n1 sừng cong · đốm · mũm mĩm'),
      (0.84, 'Đêm Tím', 'tai vây · đuôi xoăn · mắt sắc hẹp\n3 sừng dài · chân dài')]),
    ('final_morph.png', '04_tien_hoa_lua.jpg',
     'Tiến hóa biến đổi dần: Baby → Child → Teen → Adult Lửa',
     [(0.12, 'Baby · Ngày 0', 'mắt xanh ngọc · lông trơn'),
      (0.36, 'Child · Ngày 3', 'thân dài ra · màu ấm dần'),
      (0.60, 'Teen · Ngày 7', 'nhú sừng · sọc than hồng'),
      (0.845, 'Adult Lửa · Ngày 14', 'sừng cong · chân sẫm · sọc phát sáng')]),
    ('final_gen1.png', '05_gen1_5_dang_adult.jpg',
     'Bộ thú Gen 1 — 5 dạng Adult (cùng 1 base mesh + blendshape)',
     [(0.09, 'Lửa', 'Sức mạnh trội · sọc than hồng\n2 sừng cong · chân sẫm'),
      (0.29, 'Nước', 'Tốc độ trội · thân thấp dài\nđốm phát sáng · tai + đuôi vây'),
      (0.49, 'Ánh Sáng', 'Chăm sóc trội · cao thanh\n1 sừng vàng phát sáng'),
      (0.685, 'Thép', 'Tập cân bằng · chắc nịch\n2 sừng kim loại · sọc giáp'),
      (0.88, 'Bóng Tối', 'Bóng tối trội · thon dài\n3 sừng · sọc tím · mắt đỏ')]),
]


def make(src_dir, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for src, dst, title, caps in SHEETS:
        p = os.path.join(src_dir, src)
        if not os.path.exists(p):
            print('BỎ QUA (chưa có ảnh):', src)
            continue
        im = Image.open(p).convert('RGB')
        W, H = im.size
        k = W / 2000.0 if W / H > 1.6 else W / 1100.0   # ảnh ngang (dàn hàng) / ảnh vuông
        top = int(78 * k)
        bot = int(118 * k)
        sheet = Image.new('RGB', (W, H + top + bot), BG)
        sheet.paste(im, (0, top))
        d = ImageDraw.Draw(sheet)
        ft = ImageFont.truetype(FONT_B, int(34 * k))
        fc = ImageFont.truetype(FONT_B, int(27 * k))
        fs = ImageFont.truetype(FONT, int(21 * k))
        d.text((int(28 * k), int(20 * k)), title, font=ft, fill=INK)
        for fx, head, sub in caps:
            x = int(W * fx)
            y = H + top + int(16 * k)
            d.text((x, y), head, font=fc, fill=INK, anchor='ma')
            if sub:
                d.multiline_text((x, y + int(38 * k)), sub, font=fs, fill=SUB, anchor='ma',
                                 align='center', spacing=int(6 * k))
        out = os.path.join(out_dir, dst)
        sheet.save(out, quality=92)
        print('XONG', out, sheet.size)


if __name__ == '__main__':
    make(sys.argv[1], sys.argv[2])
