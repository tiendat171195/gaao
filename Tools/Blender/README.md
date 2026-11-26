# Tools/Blender — Bộ dựng thú PawFit · Gen 1

> **Gen 1** = bộ thú dựng hoàn toàn bằng script này (dùng để build gameplay). **Gen 2** = bộ model đẹp hơn làm sau bằng tool khác, thay vào mà không sửa gameplay.

Bộ script Python chạy trong **Blender** để dựng thú PawFit hoàn toàn bằng code, không cần artist:
thân từ metaball → base mesh + blendshape, tai / đuôi / sừng / mắt sinh theo tham số DNA, lông thật
(particle hair) để render ảnh demo, và xuất **FBX cho Unity**.

## Cần cài

- Blender 4.x (đã chạy với 4.0.2 trên Ubuntu, CPU, không cần GPU).
- Ubuntu: `apt-get install -y --no-install-recommends blender python3-numpy`
- Ghép chú thích ảnh cần `python3` + Pillow.

## Lệnh chạy (đứng ở gốc repo)

Render 1 cảnh (`hero`, `angles`, `variants`, `morph`, `gen1` = 5 dạng Adult của bộ Gen 1). Chất lượng `draft` mất khoảng 20 giây, `final` mất khoảng 10–20 phút trên CPU:

```bash
blender -b --factory-startup --python Tools/Blender/render_demo.py -- hero out/final_hero.png final
```

Gắn tiêu đề + chú thích tiếng Việt (đọc `final_*.png`, xuất `.jpg`):

```bash
python3 Tools/Blender/make_sheet.py out Docs/art
```

Xuất bộ model cho Unity (tạo `PawFit_Gen1_PetKit.fbx` + `PawFit_Gen1_Anchors.json`, khoảng 10 giây):

```bash
blender -b --factory-startup --python Tools/Blender/export_kit.py -- Assets/PawFit/Resources/Pets/Gen1
```

Chỉ xuất lại file mốc neo, giữ nguyên FBX (repo không nặng thêm):

```bash
blender -b --factory-startup --python Tools/Blender/export_kit.py -- Assets/PawFit/Resources/Pets/Gen1 --anchors-only
```

Kiểm tra file FBX vừa xuất: nạp lại, in khung bao từng bộ phận, kiểm tra đuôi gắn sau mông, render ảnh xem trước 5 con kiểu "trong game" (không lông) ra `out/check_kit.jpg`. Dòng `CHECK_RESULT PASS` là đạt:

```bash
blender -b --factory-startup --python Tools/Blender/check_kit.py
```

Render dãy tiến hóa (Baby → Child → Teen → 5 dạng Adult) ra `out/check_kit_forms.jpg`, để xem bộ phận có bám theo dáng thân không:

```bash
blender -b --factory-startup --python Tools/Blender/check_kit.py -- --forms
```

## Các file

| File | Việc |
|---|---|
| `pawfit_kit.py` | Bộ dựng: thân (metaball → mesh → blendshape), vùng màu, tai, đuôi, sừng, mắt, mũi, miệng, lông, vật liệu |
| `render_demo.py` | DNA mẫu (Kem Cam, Xám Sọc, Trắng Đốm, Đêm Tím, đích Adult Lửa) + dựng cảnh, đèn, camera |
| `export_kit.py` | Xuất FBX cho Unity: xương + thân LOD0 / LOD1 + mọi kiểu bộ phận + file mốc neo JSON |
| `check_kit.py` | Kiểm tra FBX đã xuất (khung bao, đuôi, ảnh xem trước) |
| `make_sheet.py` | Gắn chữ lên ảnh demo |

Muốn thêm 1 con mới: copy 1 dict DNA trong `render_demo.py` rồi đổi gene (màu `fur`, `eye_shape`, `ear`, `tail`, `horns`…).

## Gene đang hỗ trợ

| Gene | Giá trị |
|---|---|
| Dạng mắt (6) | `tron`, `hanh_nhan`, `meo`, `lim_dim`, `cun`, `sac_hep` |
| Màu mắt | bất kỳ mã màu (game dùng bảng 12 màu) |
| Màu lông / da | `fur`, `belly` (vùng sáng), `points` (chân/lưng sẫm) |
| Họa tiết | `solid` (trơn), `stripes` (sọc), `spots` (đốm) |
| Dạng tai (4) | `nhon`, `tron`, `cup` (gập ra trước), `vay` |
| Sừng | 0–3 cái × `thang` / `dai` / `cong` |
| Dạng đuôi (5) | `cao`, `tua`, `bong`, `vay`, `xoan` |
| Tỉ lệ | `head_big`, `body_chubby`, `legs_long` (0..1 = giá trị blendshape) |

## Bên trong file FBX (`Assets/PawFit/Resources/Pets/Gen1/PawFit_Gen1_PetKit.fbx`, bản 2)

Trong Unity, mặt thú nhìn +Z, chân chạm y = 0, đơn vị mét. Hậu tố `_R` = phía +X của Blender = phía −X trong Unity.

| Object | Nội dung |
|---|---|
| `PF_Rig` | 10 xương: hips, spine, chest, neck, head, legFR/FL/BR/BL, tail |
| `PF_Body` | Thân Baby chi tiết (LOD0, 38k đỉnh) cho PC / Editor |
| `PF_Body_LOD1` | Thân Baby nhẹ (LOD1, 13,6k đỉnh) cho điện thoại |
| (cả 2 thân) | Blendshape `to_adult_fire / water / light / steel / shadow`, `head_big`, `body_chubby`, `legs_long`. Vertex color: R = vùng sáng, G = bàn chân, B = sọc, A = đốm. UV0 `FurMap` = (độ dài lông, mật độ lông), UV1 `Extra` = (vùng lưng, vết bẩn) |
| `PF_Eye_R/L`, `PF_Liner_R/L` | Mắt + viền mắt. Blendshape 5 dạng mắt (gốc = `tron`) + `closed_<dạng>` (nhắm: ngủ, chớp, mắt díp) |
| `PF_Nose`, `PF_Mouth` | Mũi, miệng |
| `PF_Ear_<kiểu>_R/L` | 4 kiểu tai `nhon / tron / cup / vay`. Unity giữ đúng 1 cặp theo DNA. Vertex color R = mặt trong, G = chóp tai |
| `PF_Horn_C/PR/PL/WR/WL` | 5 khe sừng (1 sừng: C · 2 sừng: PR + PL · 3 sừng: C + WR + WL). Blendshape `dai`, `cong` (gốc = `thang`) |
| `PF_Tail` | Đuôi, gốc nằm sau mông. Blendshape `tua`, `bong`, `vay`, `xoan` (gốc = `cao`). Bản game dày thêm cho giống có lông |

Bộ phận là khối cứng, pivot ở chân bộ phận. Lúc chạy, Unity (`Gen1PetAvatar`) gắn chúng vào xương `head` / `hips`.

`PawFit_Gen1_Anchors.json` lưu tâm đầu, gốc đuôi, vị trí miệng, và độ dời của chúng khi mỗi blendshape thân = 100%. Unity dùng file này để dời mắt / tai / sừng / đuôi theo khi đầu to ra hoặc chân dài ra.

## Lịch sử

- **Bản 2 (Phase 1.3):**
  - Sửa đuôi bị gắn sát đất. Lỗi do tâm hông tính bằng ngưỡng trọng số 0.85 mà không đỉnh nào đạt, nên tâm rơi về (0,0,0). Giờ tính theo trọng số mềm.
  - Bỏ các mẫu `Kit_*` nằm dưới đất.
  - Thêm LOD1, mắt nhắm, file mốc neo.
  - Vertex color xuất dạng LINEAR.

## Giới hạn hiện tại

- Lông thật chỉ có trong ảnh render (lông sợi của Blender). Trong game Phase 1.3 thân mượt kiểu thú bông, có viền sáng giả lông tơ. Lông shell làm ở Phase 6.
- (Đã xử lý ở Phase 2.2) File mốc neo có thêm độ dời đầu mỗi xương theo từng blendshape, nên Unity xoay đầu / chân quanh đúng khớp kể cả khi thú đã lớn.
