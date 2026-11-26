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

- **Phase 2.2** — Tiến hóa + chỉ số + lực chiến:
  - Logic (C# thuần, có test):
    - `EvolutionRules` + `EvolutionService`: giai đoạn theo tuổi (3 / 7 / 14 / 30 ngày), CareScore 7 ngày.
    - Điểm tập 4 hướng, có giới hạn mỗi ngày; nhiễu theo tính cách + seed.
    - Chọn 1 trong 5 dạng Adult theo G7; DNA trượt dần từ Teen; điều kiện Ultimate.
    - `StatsRules`: chỉ số + lực chiến (G8), nhân hệ số bệnh.
    - `PetService` thêm bước tính `IPetStepper`, `Spend` (tập tốn nhu cầu), `AgeForDebug`.
    - Bia mộ lưu thêm giai đoạn + dạng.
  - Vườn:
    - Chăm sóc → điểm Chăm sóc.
    - Thả thú vào sân tập → đấm hình nộm (Sức mạnh, ốm / quá mệt thì từ chối).
    - Dắt chạy → Tốc độ; bỏ bê / ốm → Bóng tối.
  - Model: file mốc neo có thêm độ dời đầu mỗi xương theo từng blendshape (xuất lại chỉ JSON, FBX giữ nguyên). Ảnh kiểm tra dãy tiến hóa: `Docs/art/07_gen1_tien_hoa_kiem_tra.jpg`.
  - View:
    - `Gen1PetAvatar` đổi dáng mượt theo giai đoạn / dạng, xoay khớp quanh đúng tâm đã dời; động tác đấm hình nộm; Ultimate to hơn.
    - `PetMoodFx`: quầng sáng + tia lấp lánh + tiếng chuông khi tiến hóa, hào quang Ultimate.
    - DNA trượt thì dựng lại hình.
  - Bảng DEV: xem giai đoạn / dạng / CareScore / điểm tập / chỉ số / lực chiến; nút +1 ngày tuổi, Lên giai đoạn, Thuộc 1 chiêu, +10 từng hướng.
  - Test: 100/100 pass · compile check: pass

- **Phase 3.1** — Logic đấu (C# thuần, assembly `PawFit.Battle`, chưa có màn hình):
  - `SwipeRecognizer`: ngang / lên / xuống / V / Z, vuốt sai (ngắn, chậm, không rõ hình), hệ số vuốt 0,7–1,3, ngưỡng theo DPI.
  - `BattleSimulation`:
    - Damage theo G9; chí mạng ×1,5; hồi chiêu 0,8 s; vuốt sai hở 1 s.
    - Đỡ (giảm 80%) và phản đòn (đỡ ≤ 0,2 s trước đòn); đòn mạnh cắt đòn đang vung.
    - SPD cao ra đòn trước; hết 90 s so %HP; bước cố định 1/30 s có seed nên chạy lại ra đúng kết quả.
  - `BattleBot`: 3 mức Dễ / Thường / Khó, dùng đúng cổng vuốt như người chơi. Mức Khó biết canh phản đòn.
  - Test: 120/120 pass, gồm "Bot Khó thắng Bot Dễ ≥ 75% trận" · compile check: pass

- **Phase 3.2** — Màn đấu ngang + Mode Huấn luyện + đấu Bot:
  - Logic (C# thuần, có test):
    - `BattleSession`: Huấn luyện học chiêu (vuốt đúng 10 lần = thuộc chiêu, vuốt sai không phạt, slow-mo khi hình nộm vung đòn, gợi ý đỡ).
    - `BattleSession`: Đấu Bot 90 giây. Chỉ số từ `EvolutionService.Stats`, chiêu chưa thuộc yếu hơn, điểm tập theo đòn trúng + thưởng thắng, tốn năng lượng, ốm / ngủ không vào được.
    - `EvolutionService` lưu tiến độ từng chiêu (`SkillProgress`).
  - View (assembly `PawFit.Battle.View`):
    - `BattleScene`: màn ngang, đấu trường, thú Gen 1 vs hình nộm 3 màu, camera riêng, đọc nét vuốt.
    - `FighterRig`: vung đòn, lao tới, giật lùi, khiên, choáng, ngã.
    - `FxBurst`: tia văng, vòng sáng, chữ nổi.
    - `BattleHud`: 2 vạch HP mờ, vạch thời gian, vết ngón tay, bóng mờ nét vuốt + chấm chạy theo, 10 chấm tiến độ, thẻ kết quả.
  - Vườn: chạm hình nộm ở sân tập → thẻ chọn Huấn luyện / Đấu / Thôi (`GardenUi.ShowChoice`). Vườn tạm dừng khi đấu (`GardenScene.SetPaused`).
  - App: `SceneFlow.EnterBattle` chuyển vườn ⇄ màn đấu. Bảng DEV có nút vào sân tập nhanh.
  - Test: 128/128 pass · compile check: pass

- **Phase 5.1** — Kinh tế + quầy hàng trong vườn + quảng cáo giả lập (làm trước Phase 4 theo lựa chọn của bạn):
  - Core: `WalletData`, `ICoinWallet`, sự kiện `CoinsChanged`, `EconomyConfig` (G11 / G11b).
  - File lưu bản 3: `GameSave.Wallet`. Ví là của người chơi, thú mất vẫn còn. File bản 1 / 2 được cấp 200 Coin.
  - Logic (C# thuần, assembly mới `PawFit.Economy`, có test):
    - `EconomyService`: ví Coin (cộng / trừ, có trần, lưu ngay khi đổi) + quảng cáo tự nguyện.
      - Tối đa 5 lượt / ngày theo giờ trên máy; chỉnh đồng hồ lùi không được thêm lượt.
      - Xem hết mới có thưởng (+20 Coin hoặc +20 năng lượng); bỏ qua thì không mất lượt.
    - `IAdService` + `MockAdService`: quảng cáo giả 3 giây, có Bỏ qua và giả lập hết quảng cáo.
    - `ShopService`: thuốc 60 / 150 (đi qua `HealthService.GiveMedicine`), bánh thưởng 25, nước tăng lực 30. Không bán khi không cần, không đủ tiền thì không trừ gì.
    - `BattleSession` nhận `ICoinWallet`: thắng hình nộm Dễ / Thường / Khó +5 / +10 / +15 Coin, hiện trên thẻ kết quả.
  - View:
    - Khu `Shop` + `ShopStallView`: quầy gỗ mái bạt sọc, biển "QUẦY HÀNG" + đồng Coin, 3 món bày trên quầy, lối đá dẫn tới. Ảnh kiểm tra (dựng lại đúng các khối bằng Blender): `Docs/art/08_quay_hang_kiem_tra.jpg`.
    - Chạm quầy → `GardenScene.ShopTapped`.
    - `GardenUi.ShowChoice` có nút xám (không bấm được) và tự giãn theo số nút. Thêm `ShowLive` (thẻ chữ tự cập nhật, dùng cho quảng cáo giả đếm ngược) và `Toast` (dòng báo nhanh, không chặn chạm).
  - App:
    - `SceneFlow.OpenShop`: thẻ quầy hàng (Coin, lượt quảng cáo, 3 món, 2 kiểu quảng cáo). Mua xong thú ăn ngay + toast. Xem quảng cáo xong mở lại quầy.
    - `GameBootstrap` tạo `MockAdService` / `EconomyService` / `ShopService` và cho quảng cáo giả chạy theo giờ thật.
    - Bảng DEV có mục Kinh tế.
  - Test: 149/149 pass · compile check: pass

- **Phase 5.2** — Phụ kiện + trang trí vườn + polish:
  - Core: `AccessoryKind` (Nơ, Mũ len, Khăn quàng, Vương miện), `DecorKind` (Dây đèn lồng, Ghế gỗ, Cối xay gió, Vòm hoa), `CosmeticsData`, sự kiện `AccessoryChanged` / `DecorAdded`, giá ở `EconomyConfig` (G12).
  - File lưu bản 4: `GameSave.Cosmetics`. Đồ là của người chơi, thú mất vẫn còn. File bản 3 đọc ra là chưa có đồ.
  - Logic (C# thuần, có test): `CosmeticsService`.
    - Mua bằng Coin, mua 1 lần là có mãi.
    - Phụ kiện mua xong tự đeo, mỗi lúc 1 món, chạm để đeo / tháo.
    - Trang trí mua xong báo vườn dựng thêm.
    - Công cụ test "có hết đồ".
  - View:
    - `IPetAvatar` thêm `HeadAnchor` + `HeadRadius` (Gen 1 = tâm đầu con của xương head; thú tạm = khối đầu).
    - `AccessoryView` dựng 4 phụ kiện bằng khối, cỡ theo bán kính đầu, đeo cả ở vườn lẫn màn đấu.
    - `DecorBuilder` dựng 4 món trang trí ở chỗ cố định, có vật cản. Cối xay quay, đèn lồng đung đưa (`DecorMotion`), mua xong bật lên (`PopIn`).
    - `GardenScene` dựng sẵn đồ đã mua trước khi nướng NavMesh. Mua món mới thì camera lia tới rồi nướng lại NavMesh tại chỗ (`NavMeshBuilder.UpdateNavMeshData`).
    - Ảnh kiểm tra: `Docs/art/09_phu_kien_trang_tri_kiem_tra.jpg`. Phụ kiện đeo trên FBX Gen 1 thật trong Blender; đồ trang trí dựng lại đúng các khối trên bản đồ vườn.
  - Polish:
    - `SynthAudio` (Core.Unity) dùng chung việc tạo âm thanh bằng code. `UiSounds` kêu "ting-ting" khi nhận Coin, "cạch-ting" khi tiêu.
    - `TapBounce`: quầy hàng + hình nộm nảy nhẹ khi chạm.
    - Thú nhảy lên khoe khi đeo phụ kiện mới.
  - App: quầy hàng có thêm thẻ **Phụ kiện cho bé...** / **Trang trí vườn...**. Bảng DEV có **Có hết đồ (test)**, **Đổi phụ kiện**.
  - Test: 157/157 pass · compile check: pass

## Quyết định đã chốt

- Engine: **Unity 6.3 LTS**, URP, C#. Logic là C# thuần (asmdef `noEngineReferences`).
- Model thú: **Gen 1** (dựng bằng script, dùng để build gameplay). **Gen 2** làm sau bằng tool đẹp hơn, thay qua `IPetAvatar` mà không sửa gameplay.
- Input: dùng Input Manager cũ (touch + chuột), bọc interface ở Phase 1.2.
- Thú trong game Phase 1.3: thân mượt kiểu thú bông (vertex color + viền sáng), chưa có lông. Lông shell làm ở Phase 6.
- Điện thoại dùng LOD1, PC / Editor dùng LOD0 (đổi được trong bảng DEV).
- Ốm mà chăm đủ thì bệnh dừng nặng thêm nhưng không tự khỏi; phải chăm kỹ hoặc uống thuốc (G4). Thuốc mua ở quầy hàng bằng Coin (Phase 5.1).
- Phụ kiện + trang trí chỉ để đẹp, không đổi chỉ số (PvP công bằng). Đồ trang trí có chỗ cố định, không cho tự kéo thả.
- Phase 5.1 làm trước Phase 4 (bạn chọn). Quầy hàng bán đồ dùng ngay, không có túi đồ. Ví nằm chung file lưu game (Phase 4.1 đồng bộ cùng hồ sơ người chơi).
- Quảng cáo đếm "ngày" theo giờ trên máy cho tới khi có giờ server (Phase 4.1).
- Thú mất thì xóa khỏi file lưu (chỉ còn bia mộ + trứng). Công cụ test "Thú mới" vẫn giữ bia mộ cũ.
- Tiến hóa Gen 1 chỉ đổi dáng thân + màu lông / tỉ lệ (DNA trượt); không tự đổi kiểu tai / đuôi / sừng để tránh đổi model đột ngột.
- Trước Phase 3.2, điểm Sức mạnh chỉ có từ sân tập trong vườn. Phase 3.2 thêm điểm từ Mode Huấn luyện.
- Giả định GDD G1–G11: tạm dùng, bạn sửa khi cần.

## Tiếp theo

- **Phase 5.3** — Build Android / iOS. Tôi chuẩn bị được trong repo: menu build, Player Settings (tên gói, xoay màn hình, icon tạm), tắt quảng cáo giả trong bản phát hành. **Cần bạn:** build và chạy thử trên máy có Unity (iOS cần Mac + Xcode); container không có Unity.
- **Phase 4.1** — Firebase Auth + Firestore + giờ server. **Cần bạn:** tạo project Firebase, bật Auth (ẩn danh / Google), Firestore, tải `google-services.json` (Android) + `GoogleService-Info.plist` (iOS), và cài Firebase Unity SDK. Trước khi có các thứ đó, tôi chỉ làm được interface + bản giả lập (Mock).

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
- Đổi số tiến hóa / chỉ số: `EvolutionConfig`, `StatsConfig` trong `GameConfig.cs`. Kinh tế: `EconomyConfig` (giá thuốc ở `HealthConfig`).
- Thêm assembly logic mới thì nhớ thêm vào `Tools/LogicTests/LogicTests.csproj` và asmdef của Tests / App (như `PawFit.Economy`).
- `MockAdService` vẫn đang được nối trong `GameBootstrap` cho mọi bản build. Trước khi phát hành (Phase 5.3) phải thay bằng SDK quảng cáo thật.
- Lỗi nhỏ đã biết (ảnh render): da dưới lông đậm hơn lông nên quanh mắt hơi cam. Trong Unity đã dùng shader riêng nên không bị.
