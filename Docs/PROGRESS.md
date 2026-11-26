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

- **Phase 1.3** — Thú Gen 1 trong Unity + biểu cảm bằng hình + animation code + Debug overlay:
  - Model: FBX bản 2 `Assets/PawFit/Resources/Pets/Gen1/PawFit_Gen1_PetKit.fbx` + `PawFit_Gen1_Anchors.json`. Ảnh kiểm tra: `Docs/art/06_gen1_fbx_v2_kiem_tra.jpg`
    - **Sửa lỗi đuôi rơi xuống đất** (tâm hông tính sai), bỏ các mẫu `Kit_*` nằm dưới đất
    - Thân LOD0 38k đỉnh (PC) + **LOD1 13,6k đỉnh (điện thoại)**
    - Đủ 6 dạng mắt + mắt nhắm, 4 kiểu tai, 5 khe sừng × 3 dạng, 5 kiểu đuôi
  - Blender: `export_kit.py` viết lại, thêm `check_kit.py` (tự kiểm tra + ảnh xem trước), `pawfit_kit.py` tách hàm đặt mắt / tai / sừng / đuôi
  - Logic (C# thuần, có test): `PetExpressionService`
    - Đói: nhìn chòi ăn + kêu theo nhịp
    - Bẩn: lông lấm lem + 2 / 4 ruồi
    - Buồn ngủ: mắt díp + ngáp
    - Ngủ: nhắm mắt
    - Thêm `PetService.SetNeedForDebug` cho công cụ test
  - View (Unity):
    - `Gen1PetAvatar`: DNA → blendshape / màu / bộ phận, bộ phận gắn xương. Chuyển động bằng code: bước chân, thở, vẫy đuôi, quay đầu nhìn, chớp mắt, ăn, kêu, ngáp, nằm ngủ
    - `PetMoodFx`: ruồi, bong bóng quả táo, chữ Z, tiếng kêu / ngáp tạo bằng code
    - `IPetAvatar` thêm `SetLook / LookAt / PlayCry / PlayYawn / HeadTop`
    - `PetController` tự chọn Gen 1 hoặc thú tạm, dựng lại khi có thú mới
  - Shader URP: `PawFit/Gen1Fur` (màu theo vertex color, viền giả lông tơ, độ bẩn), `PawFit/Gen1Eye` (mống mắt + đồng tử + đốm sáng)
  - Editor: `Gen1ModelPostprocessor` tự đặt thông số import FBX
  - App: `DebugOverlay` (nút DEV, chỉ Editor / bản dev) thay `DevHotkeys`
  - Test: 58/58 pass · compile check: pass

- **Phase 2.1** — Bệnh / chết vĩnh viễn / tưởng niệm / trứng mới:
  - Logic (C# thuần, có test):
    - `HealthRules` + `HealthService`: đồng hồ bỏ bê (G2); Mệt 24h → Ốm 72h → Nguy kịch 120h → Mất 168h (G3).
    - Chữa (G4): chăm kỹ (cháo + chăn + lau trong 12h) hoặc thuốc → hồi phục 24h / 48h; quá 24h không chăm thì tái phát.
    - Hệ số chỉ số −30 / −40 / −60% và cờ cấm tập / Rank (dùng ở Phase 2.2 / 3 / 4).
  - Logic: `PetService` tính bù offline theo bước 15 phút, nên biết đúng giờ thú mất. Mất → bia mộ (`MemorialEntry`) + trứng (`EggFactory`, thừa hưởng 1 trong 8 nhóm trait) → 24h sau `HatchEgg(tên)`. Tên chuẩn hóa ở `PetNames`.
  - Logic: file lưu bản 2 (trứng + bia mộ + sức khỏe), đọc được file bản 1.
  - Vườn khi ốm:
    - Cho ăn = cháo, ngủ ổ = đắp chăn, chạm = lau người (không đánh thức thú).
    - Không chịu xuống hồ.
    - Đi chậm, không chạy, hay về ổ nằm.
    - Biểu cảm thêm Mệt / Ốm / Nguy kịch / Đang hồi phục.
  - View (Unity):
    - Thú ốm: lông nhợt (`_Pale` trong shader), run lẩy bẩy, túi chườm, mây mưa khi nguy kịch, chăn, khăn lau, bát cháo, dấu + khi đang hồi phục.
    - `PetDeathFx`: cảnh chia tay.
    - `MemorialView`: bia mộ có chữ, chạm để xem thẻ.
    - `EggView`: trứng trong ổ, lắc lư khi sẵn sàng, nứt + vỡ khi nở.
    - `GardenUi`: bảng đặt tên + thẻ chữ, uGUI dựng bằng code.
    - Camera đổi theo thú / bia / trứng.
  - Bảng DEV: nút Mệt / Ốm / Nguy kịch / Chết / Uống thuốc / Khỏi bệnh / Trứng nở được ngay
  - Test: 81/81 pass · compile check: pass

## Quyết định đã chốt

- Engine: **Unity 6.3 LTS**, URP, C#. Logic là C# thuần (asmdef `noEngineReferences`).
- Model thú: **Gen 1** (dựng bằng script, dùng để build gameplay). **Gen 2** làm sau bằng tool đẹp hơn, thay qua `IPetAvatar` mà không sửa gameplay.
- Input: dùng Input Manager cũ (touch + chuột), bọc interface ở Phase 1.2.
- Thú trong game Phase 1.3: thân mượt kiểu thú bông (vertex color + viền sáng), chưa có lông. Lông shell làm ở Phase 6.
- Điện thoại dùng LOD1, PC / Editor dùng LOD0 (đổi được trong bảng DEV).
- Ốm mà chăm đủ thì bệnh dừng nặng thêm nhưng không tự khỏi; phải chăm kỹ hoặc uống thuốc (G4). Thuốc mua bằng Coin ở Phase 5.1.
- Thú mất thì xóa khỏi file lưu (chỉ còn bia mộ + trứng). Công cụ test "Thú mới" vẫn giữ bia mộ cũ.
- Giả định GDD G1–G11: tạm dùng, bạn sửa khi cần.

## Tiếp theo

- **Phase 2.2** — Tiến hóa (CareScore, TrainingVector, seed + tính cách) + chỉ số + lực chiến. Dùng `HealthService.StatFactor` để giảm chỉ số khi bệnh.

## Ghi chú cho session sau

- Container cloud mới phải cài lại công cụ:
  `apt-get install -y --no-install-recommends blender python3-numpy dotnet-sdk-8.0`
- Chạy test logic: `dotnet test Tools/LogicTests` (khoảng 30 giây, gồm cả restore NuGet lần đầu).
- Compile thử code Unity: `dotnet build Tools/UnityCompileCheck` (stub ở `UnityStubs.cs`; dùng API Unity mới thì thêm stub).
- Môi trường cloud riêng `gaao` có setup script cài sẵn Blender + .NET + restore NuGet (chưa kiểm chứng ở session mới).
- `ProjectVersion.txt` đặt `6000.3.0f1`. Mở bằng bản 6.3 khác thì Unity tự cập nhật file này.
- Bản Blender của Ubuntu không có OpenImageDenoise, nên script đã tắt denoise.
- Xuất lại FBX: `blender -b --factory-startup --python Tools/Blender/export_kit.py -- Assets/PawFit/Resources/Pets/Gen1`, rồi kiểm tra bằng `blender -b --factory-startup --python Tools/Blender/check_kit.py` (phải ra `CHECK_RESULT PASS`). Mỗi lần xuất, repo nặng thêm khoảng 9 MB, nên chỉ xuất khi đổi model.
- Shader `Gen1Fur` / `Gen1Eye` viết tay cho URP 17 (Unity 6.3) và chưa chạy thử trong Unity thật (container không có Unity). Lỗi thì bảng DEV có nút chuyển sang URP Lit.
- Phase 2.2: blendshape `to_adult_*` làm đầu dời xa nhưng xương chưa dời theo. Cần dời xương theo dạng tiến hóa để xoay đầu đúng tâm (bộ phận đã dời theo nhờ file mốc neo).
- Lỗi nhỏ đã biết (ảnh render): da dưới lông đậm hơn lông nên quanh mắt hơi cam. Trong Unity đã dùng shader riêng nên không bị.
