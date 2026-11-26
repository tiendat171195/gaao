# PawFit — Tiến độ

> File này cập nhật cuối mỗi Phase để session sau đọc là làm tiếp được ngay.

## Đã xong

- **Phase 0** — Plan + kiến trúc + GDD: `Docs/PLAN.md`, `Docs/GDD.md`.
- **Phase 0.5** — POC hình ảnh thú: ảnh `Docs/art/01..04`, bộ dựng `Tools/Blender/`.
- **Gen 1** — 5 dạng Adult (Lửa, Nước, Ánh Sáng, Thép, Bóng Tối):
  - Ảnh: `Docs/art/05_gen1_5_dang_adult.jpg`
  - Model: `Assets/PawFit/Art/Pets/Gen1/PawFit_Gen1_PetKit.fbx`
  - Blendshape thân: `to_adult_fire / water / light / steel / shadow`, `head_big`, `body_chubby`, `legs_long`
- **Phase 1.1** — Nền móng project Unity 6.3 + Core + Pet DNA:
  - Project: `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `.gitignore`, `Assets/PawFit/link.xml`
  - Core (C# thuần): `ServiceLocator`, `EventBus`, `ILog`, `ITimeProvider` (+ `DebugTimeProvider` tua giờ), `SeededRandom` (PCG32), `ISaveService` (+ `JsonFileSaveService` ghi an toàn có .bak), `GameConfig`
  - Pet (C# thuần): `PetDna` (18 gene, có trường `Gen`), `DnaCodec` (mã `PF-XXXX-XXXX-XXXX` có ký tự kiểm tra), `DnaGenerator`, `PetNeeds` + `NeedsSimulator`, `PetService` (tạo / nạp / lưu, tính bù offline, chăm sóc, ngủ)
  - App: `GameBootstrap` (tự chạy khi Play, in DNA + nhu cầu ra Console)
  - Editor: menu **PawFit ▸ Setup Project** (URP + scene Main)
  - Test: 33 unit test, chạy cả Unity Test Runner và `dotnet test Tools/LogicTests`

- **Phase 1.2** — Vườn + camera + điều khiển thú:
  - Logic (C# thuần, có test): `GardenLayout` (5 khu: chòi ăn trái, hồ phải, sân tập sau, ổ giữa, tưởng niệm góc), `PointerGestureClassifier` (chạm / kéo / giữ lâu), `CareActionService` (cho ăn, tắm, vuốt ve, ngủ / dậy, từ chối khi no / chưa buồn ngủ), `PetBrain` (lệnh người chơi + tự lang thang, đói tới chòi ăn, buồn ngủ về ổ)
  - View (Unity): `GardenScene` (màn dọc, ánh sáng mềm, hậu kỳ URP, NavMesh nướng lúc chạy), `GardenBuilder` (dựng vườn bằng code), `GardenCamera` (theo thú + kéo), `GardenInputRouter` (chạm / kéo), `PetController` (NavMeshAgent), `PlaceholderPetAvatar` (thú tạm bằng khối, màu + tai / sừng / đuôi theo DNA) qua `IPetAvatar`
  - App: `SceneFlow` (vào vườn), `DevHotkeys` (phím 1/2/3 tua giờ, N in nhu cầu — chỉ Editor)
  - Editor Setup thêm: material gốc `Resources/PawFit/LitBase`, bật Input cũ, cho xoay dọc / ngang, gán dữ liệu hậu kỳ
  - Test: 50/50 pass. Thêm `Tools/UnityCompileCheck` compile thử code Unity bằng stub: pass

## Quyết định đã chốt

- Engine: **Unity 6.3 LTS**, URP, C#. Logic là C# thuần (asmdef `noEngineReferences`).
- Model thú: **Gen 1** (dựng bằng script, dùng để build gameplay). **Gen 2** làm sau bằng tool đẹp hơn, thay qua `IPetAvatar` mà không sửa gameplay.
- Input: dùng Input Manager cũ (touch + chuột), bọc interface ở Phase 1.2.
- Giả định GDD G1–G11: tạm dùng, bạn sửa khi cần.

## Tiếp theo

- **Phase 1.3** — Thay thú tạm bằng model Gen 1 (FBX) qua `IPetAvatar` + bản LOD nhẹ cho mobile, trạng thái bằng hình (đói nhìn chòi ăn + kêu, bẩn có ruồi, buồn ngủ mắt díp), animation bằng code, Debug overlay.

## Ghi chú cho session sau

- Container cloud mới phải cài lại công cụ:
  `apt-get install -y --no-install-recommends blender python3-numpy dotnet-sdk-8.0`
- Chạy test logic: `dotnet test Tools/LogicTests` (khoảng 30 giây, gồm cả restore NuGet lần đầu).
- Compile thử code Unity: `dotnet build Tools/UnityCompileCheck` (stub ở `UnityStubs.cs`; dùng API Unity mới thì thêm stub).
- Môi trường cloud riêng `gaao` có setup script cài sẵn Blender + .NET + restore NuGet (chưa kiểm chứng ở session mới).
- `ProjectVersion.txt` đặt `6000.3.0f1`. Mở bằng bản 6.3 khác thì Unity tự cập nhật file này.
- Bản Blender của Ubuntu không có OpenImageDenoise, nên script đã tắt denoise.
- Thân base mesh 38k đỉnh. Phase 1.3 cần xuất thêm bản LOD nhẹ cho mobile.
- Lỗi nhỏ đã biết (ảnh render): da dưới lông đậm hơn lông nên quanh mắt hơi cam. Trong Unity sẽ dùng shader riêng.
