# Tools/Blender — Bộ dựng thú PawFit (Phase 0.5 · POC hình ảnh)

Bộ script Python chạy trong **Blender** để dựng thú PawFit hoàn toàn bằng code, không cần artist:
thân từ metaball → base mesh + blendshape, tai / đuôi / sừng / mắt sinh theo tham số DNA, lông thật
(particle hair) để render ảnh demo, và xuất **FBX cho Unity**.

## Cần cài

- Blender 4.x (đã chạy với 4.0.2 trên Ubuntu, CPU, không cần GPU).
- Ubuntu: `apt-get install -y --no-install-recommends blender python3-numpy`
- Ghép chú thích ảnh cần `python3` + Pillow.

## Lệnh chạy (đứng ở gốc repo)

Render 1 cảnh (`hero`, `angles`, `variants`, `morph`). Chất lượng `draft` mất khoảng 20 giây, `final` mất khoảng 10–20 phút trên CPU:

```bash
blender -b --factory-startup --python Tools/Blender/render_demo.py -- hero out/final_hero.png final
```

Gắn tiêu đề + chú thích tiếng Việt (đọc `final_*.png`, xuất `.jpg`):

```bash
python3 Tools/Blender/make_sheet.py out Docs/art
```

Xuất bộ model cho Unity:

```bash
blender -b --factory-startup --python Tools/Blender/export_kit.py -- Assets/PawFit/Art/Pet/PawFit_PetKit.fbx
```

## Các file

| File | Việc |
|---|---|
| `pawfit_kit.py` | Bộ dựng: thân (metaball → mesh → blendshape), vùng màu, tai, đuôi, sừng, mắt, mũi, miệng, lông, vật liệu |
| `render_demo.py` | DNA mẫu (Kem Cam, Xám Sọc, Trắng Đốm, Đêm Tím, đích Adult Lửa) + dựng cảnh, đèn, camera |
| `export_kit.py` | Xuất FBX: thân + xương + bộ phận + bộ kiểu dáng |
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

## Bên trong file FBX (`Assets/PawFit/Art/Pet/PawFit_PetKit.fbx`)

| Object | Nội dung |
|---|---|
| `PF_Rig` | 10 xương: hips, spine, chest, neck, head, legFR/FL/BR/BL, tail |
| `PF_Body` | Thân Baby, blendshape `to_adult_fire`, `head_big`, `body_chubby`, `legs_long`. Vertex color: R = vùng sáng, G = vùng chân, B = sọc mặc định, A = vùng lưng. UV2 `FurMap`: x = độ dài lông, y = mật độ lông |
| `PF_Eye*`, `PF_Liner*`, `PF_Nose`, `PF_Mouth`, `PF_Ear*`, `PF_Tail` | Bộ phận của con mẫu, đã gắn vào xương `head` / `tail` |
| `Kit_Ear` / `Kit_Tail` / `Kit_Horn` / `Kit_Eye` | Bộ kiểu dáng: mỗi kiểu là 1 blendshape, nên morph mượt giữa các kiểu được |

## Giới hạn hiện tại (xử lý ở Phase 1.3)

- Thân 38k đỉnh, quá nặng cho mobile khi phủ lông shell. Cần thêm bản LOD khoảng 8–12k đỉnh (dựng lại base ở voxel thô hơn).
- Lông trong ảnh là lông sợi của Blender. Trong game, lông sẽ làm bằng shader shell nên đơn giản hơn một chút.
- Chưa có animation. Phase 1.3 làm animation bằng code (IK chân, nhún thân, vẫy đuôi).
