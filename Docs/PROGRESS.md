# PawFit — Tiến độ

> File này cập nhật cuối mỗi Phase để session sau đọc là làm tiếp được ngay.

## Đã xong

- **Phase 0** — Plan tổng thể + kiến trúc + GDD ngắn (đã trả trong chat). Đầu Phase 1.1 sẽ lưu thành `Docs/PLAN.md` và `Docs/GDD.md`.
- **Phase 0.5** — POC hình ảnh thú:
  - Ảnh demo: `Docs/art/01_hero_baby.jpg`, `02_baby_3_goc.jpg`, `03_bien_the_dna.jpg`, `04_tien_hoa_lua.jpg`
  - Script dựng thú: `Tools/Blender/` (xem `Tools/Blender/README.md`)
  - Model cho Unity: `Assets/PawFit/Art/Pet/PawFit_PetKit.fbx`

## Quyết định đã chốt

- Engine: **Unity 6.3 LTS** (thay cho 2022.3 LTS vì bản này đã hết hỗ trợ), URP, C#.
- Thú: base mesh dựng bằng script Blender + blendshape + xương. Trong Unity: lông shell shader, animation bằng code.
- Giả định GDD G1–G11 trong plan: tạm dùng, bạn sửa khi cần.

## Tiếp theo

- **Phase 1.1** — Nền móng project Unity 6.3 + Core + Pet DNA (logic C# thuần + unit test).

## Ghi chú cho session sau

- Container cloud mỗi lần mới phải cài lại công cụ:
  `apt-get install -y --no-install-recommends blender python3-numpy`
- Bản Blender của Ubuntu không có OpenImageDenoise, nên script đã tắt denoise.
- Thân base mesh hiện 38k đỉnh. Phase 1.3 cần xuất thêm bản LOD nhẹ cho mobile.
