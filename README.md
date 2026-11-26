# PawFit

Game nuôi thú ảo 3D cho Android / iOS (Unity 6.3 LTS, URP, C#). Plan: `Docs/PLAN.md` · Thiết kế: `Docs/GDD.md` · Tiến độ: `Docs/PROGRESS.md`.

## Mở project lần đầu

1. Cài **Unity 6.3 LTS** qua Unity Hub, chọn thêm module Android Build Support (và iOS nếu có Mac).
2. Unity Hub ▸ **Add project from disk** ▸ chọn thư mục gốc repo này. Nếu Hub hỏi bản Editor thì chọn bản 6.3 bạn đã cài.
3. Đợi Unity import xong. Nếu Package Manager báo lỗi phiên bản URP / Test Framework: **Window ▸ Package Manager**, chọn package đó rồi bấm cài bản được gợi ý.
4. Menu **PawFit ▸ Setup Project**. Bước này tự làm các việc sau:
   - Tạo + gán URP Asset.
   - Tạo material gốc `Resources/PawFit/LitBase`.
   - Bật Input cũ.
   - Cho phép xoay dọc / ngang.
   - Tạo scene `Assets/PawFit/Scenes/Main.unity`.
   - Nếu Unity hiện hộp thoại "khởi động lại" thì tắt mở lại Unity.
   - Nếu bước tạo URP báo lỗi, làm tay: **Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)**, rồi kéo asset vào **Project Settings ▸ Graphics ▸ Default Render Pipeline**.
5. Game view chọn độ phân giải **1080x1920 (dọc)**, mở scene `Main`, bấm **Play**.
6. Commit luôn các file `.meta` Unity vừa sinh để giữ GUID ổn định.

**Nếu thú trông không đúng:**
- Thú là các khối vuông tròn (thú tạm): chưa nạp được model Gen 1. Xem Console có dòng `[PawFit] Không nạp được model Gen 1`. Thử chuột phải thư mục `Assets/PawFit/Resources/Pets/Gen1` ▸ **Reimport**.
- Thú màu hồng tím: shader lỗi trên máy đó. Mở bảng **DEV** ▸ bấm **URP Lit** để chạy tạm, rồi gửi tôi lỗi đỏ trong Console.

## Điều khiển trong vườn (không có nút nào)

| Thao tác | Kết quả |
|---|---|
| Chạm xuống đất | Thú chạy tới đó |
| Kéo trên mặt đất | Kéo camera. Thả tay khoảng 2,5 giây thì camera quay lại theo thú |
| Chạm vào thú | Thú nhảy lên (vuốt ve). Đang ngủ thì thức dậy |
| Kéo thú | Thú chạy theo ngón tay. Thả vào **hồ** thì tắm, thả vào **ổ** thì ngủ (nếu đã buồn ngủ) |
| Kéo quả táo từ **khay ở chòi ăn** (bên trái) thả lên thú | Cho ăn. No rồi thì thú lắc đầu |
| Chạm vào khay đồ ăn | Thú chạy tới chòi ăn |

Thú tự làm khi rảnh: đi lang thang; đói thì tới chòi ăn đứng chờ; buồn ngủ thì về ổ ngủ.

## Thú "nói" bằng hình (không có thanh số)

| Trạng thái | Hình |
|---|---|
| Đói (no < 35) | Cứ khoảng 9 giây lại quay đầu nhìn chòi ăn, kêu "mi-meo", hiện bong bóng có quả táo |
| Bẩn (sạch < 40) | Lông xỉn + lấm lem, 2 con ruồi bay quanh (sạch < 15 thì 4 con) |
| Buồn ngủ (năng lượng < 30) | Mắt díp dần, tai + đuôi cụp, thỉnh thoảng ngáp |
| Đang ngủ | Nhắm mắt, nằm xuống, thở chậm, chữ Z bay lên |
| Bình thường | Chớp mắt, ngó nghiêng, vẫy đuôi, giật tai |

Số ngưỡng nằm ở `ExpressionConfig` (C# thuần), chỉnh được không cần sửa code.

## Bệnh, chết vĩnh viễn, trứng mới (Phase 2.1)

**Đồng hồ bỏ bê** chỉ về 0 khi **chăm đủ**, tức cả 4 nhu cầu ≥ 60 cùng lúc. Bỏ bê liên tục thì:

| Bỏ bê | Giai đoạn | Thú trông thế nào |
|---|---|---|
| 24 giờ | Mệt | Mắt hơi lờ đờ, đi chậm, lười đi lang thang. Chăm đủ là khỏi ngay |
| 72 giờ (3 ngày) | Ốm | Lông nhợt, run, túi chườm trên đầu. Đi chậm, không chạy, hay về ổ nằm. Không chịu xuống hồ |
| 120 giờ | Nguy kịch | Như Ốm nhưng nặng hơn: có đám mây mưa nhỏ trên đầu, nằm gần như cả ngày |
| 168 giờ (7 ngày) | Mất | Nằm xuống, hồn bay lên. Khu tưởng niệm có thêm bia mộ |

**Chữa Ốm / Nguy kịch:** chăm đủ không tự khỏi, phải chăm kỹ.
- **Bước 1 — 1 lượt chăm kỹ:** làm đủ 3 việc trong vòng 12 giờ:
  - **Cháo:** kéo đồ ăn từ khay, lúc thú ốm khay đưa ra bát cháo.
  - **Đắp chăn:** dắt thú vào ổ ngủ. Thú ốm nằm được lúc nào cũng được.
  - **Lau người:** chạm vào thú, khăn ấm sẽ lau qua. Thú đang ngủ cũng không bị đánh thức.
- **Bước 2 — giữ hồi phục:** xong lượt đầu thì thú bắt đầu hồi phục, có dấu + xanh bay lên. Mỗi lượt chỉ giữ hồi phục thêm 12 giờ, nên cứ 12 giờ làm thêm 1 lượt.
- **Khỏi:** Ốm khỏi sau 24 giờ hồi phục, Nguy kịch sau 48 giờ.
- **Tái phát:** đang hồi phục mà quá 24 giờ không chăm gì thì tái phát.
- **Thuốc:** cũng chữa được, mua bằng Coin ở Phase 5.1. Hiện chỉ thử được bằng nút trong bảng DEV.

**Khi thú mất:**
- Bia mộ ở góc tưởng niệm ghi tên, số ngày đã sống, rank cao nhất (có từ Phase 4.3) và mã DNA. Chạm vào bia để đọc chữ to.
- Trong ổ có 1 quả trứng mang đúng 1 trait của thú cũ, ví dụ dạng đuôi, màu lông hoặc dạng tai.
- 24 giờ sau trứng lắc lư và lấp lánh. Chạm vào để trứng nở, rồi đặt tên cho thú mới.

## Tiến hóa, chỉ số, lực chiến (Phase 2.2)

**Giai đoạn theo tuổi:** Baby (ngày 0) → Child (ngày 3) → Teen (ngày 7) → Adult (ngày 14) → Ultimate (từ ngày 30, nếu đủ điều kiện). Dáng thân lớn dần, đổi mượt, không đổi đột ngột:
- Child: lớn đều.
- Teen: nghiêng dần về dạng sắp thành. Màu lông và tỉ lệ thân (DNA) cũng trượt dần, mỗi ngày 1 bước.
- Adult: thành hẳn 1 trong 5 dạng, có quầng sáng + tiếng chuông lúc tiến hóa.

**Điểm tập 4 hướng.** Mỗi hướng chủ động có giới hạn 30 điểm / ngày:

| Hướng | Có được khi |
|---|---|
| Sức mạnh | Dắt thú thả vào **sân tập**: thú đấm hình nộm 4 giây, tốn năng lượng + no bụng. Ốm hoặc quá mệt thì thú lắc đầu |
| Tốc độ | Dắt thú chạy theo ngón tay |
| Chăm sóc | Cho ăn, tắm, vuốt ve, lau người, cho ngủ trong ổ |
| Bóng tối | Tự tăng khi thú bị bỏ bê hoặc đang ốm |

**Dạng Adult** (chốt lúc lên Adult; tính cách ẩn + seed cộng nhiễu 10–20%):

| Điều kiện | Dạng |
|---|---|
| CareScore < 40 | Bóng Tối |
| Tập cân bằng (không hướng nào trội) | Thép |
| Sức mạnh trội | Lửa |
| Tốc độ trội | Nước |
| Chăm sóc trội + CareScore ≥ 70 | Ánh Sáng |
| Chăm sóc trội nhưng CareScore < 70 | Dạng của hướng mạnh thứ 2 |
| Bóng tối trội | Bóng Tối |

- **CareScore** = trung bình mức chăm (trung bình 4 nhu cầu) của 7 ngày gần nhất; giờ đang ốm chỉ tính nửa.
- **Ultimate** cần: CareScore ≥ 80, thuộc đủ 4 chiêu (Phase 3.2), 7 ngày không bệnh. Ultimate to hơn chút và có 3 đốm vàng bay quanh.
- **Chỉ số + lực chiến** theo GDD G8, giảm khi mệt / ốm. Vườn không hiện số; chỉ xem được trong bảng DEV, sẽ dùng khi đấu từ Phase 3.

## Công cụ test (chỉ Editor / bản Development)

- Nút **DEV** góc trên trái màn hình (hoặc phím `F1`) mở bảng test:
  - Xem 4 nhu cầu, DNA, việc thú đang làm, biểu cảm.
  - Tăng / giảm từng nhu cầu.
  - Đặt nhanh trạng thái: **Đói** / **Bẩn** / **Buồn ngủ** / **Khỏe hết** / **Cho ngủ**.
  - Sức khỏe: **Mệt** / **Ốm** / **Nguy kịch** / **Chết** / **Uống thuốc** / **Khỏi bệnh**. Xem số giờ bỏ bê, đã chăm kỹ mấy việc / 3, % hồi phục.
  - Khi thú đã mất: xem trứng còn bao lâu nở, thừa hưởng trait gì, nút **Trứng nở được ngay**.
  - Tiến hóa: xem giai đoạn, dạng đang nghiêng, CareScore, điểm tập, chỉ số, lực chiến. Nút **+1 ngày tuổi**, **Lên giai đoạn**, **Thuộc 1 chiêu**, **+10** từng hướng tập.
  - Tua giờ +1 / +6 / +24.
  - **Thú mới** (DNA ngẫu nhiên).
  - Đổi model Gen 1 ↔ thú tạm, LOD0 ↔ LOD1, shader riêng ↔ URP Lit.
- Phím tắt: `1` tua +1 giờ · `2` +6 giờ · `3` +24 giờ · `N` in 4 nhu cầu ra Console.
- Bản phát hành (Release) không có bảng này.

## Chạy test / kiểm tra code

- Trong Unity: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.
- Không cần Unity (máy có .NET 8 SDK). Lệnh đầu chạy unit test logic C# thuần:

```bash
dotnet test Tools/LogicTests
```

- Lệnh sau compile thử code Unity bằng stub API. Lệnh này chỉ bắt lỗi gõ sai / sai kiểu, không thay được việc mở project trong Unity:

```bash
dotnet build Tools/UnityCompileCheck
```

## Thư mục chính

| Thư mục | Nội dung |
|---|---|
| `Assets/PawFit/Scripts/Core` | Service nền, C# thuần: ServiceLocator, EventBus, thời gian, random có seed, lưu JSON, config |
| `Assets/PawFit/Scripts/Core/Unity` | Tiện ích Unity dùng chung cho View: tạo vật liệu, khối cơ bản, đổi tọa độ |
| `Assets/PawFit/Scripts/Pet` | DNA, nhu cầu, PetService, sức khỏe `Health/`, trứng + bia mộ `Lifecycle/`, tiến hóa + chỉ số `Evolution/` (C# thuần) · `View/`: `IPetAvatar`, thú Gen 1 `Gen1PetAvatar`, thú tạm `PlaceholderPetAvatar`, hiệu ứng `PetMoodFx` |
| `Assets/PawFit/Scripts/Garden` | Bố cục vườn, bộ não thú `PetBrain`, biểu cảm `PetExpressionService`, luật chăm sóc, phân loại chạm/kéo (C# thuần) · `View/`: dựng vườn, camera, input, NavMesh, bia mộ, trứng, bảng đặt tên |
| `Assets/PawFit/Scripts/App` | GameBootstrap (nối service, tự chạy khi Play), SceneFlow, Debug overlay |
| `Assets/PawFit/Resources/Pets/Gen1` | Model thú Gen 1 (FBX bản 2: thân LOD0 / LOD1 + blendshape + xương + mọi kiểu bộ phận) + file mốc neo JSON |
| `Assets/PawFit/Resources/Shaders` | Shader URP `PawFit/Gen1Fur` (lông theo vertex color, bẩn) + `PawFit/Gen1Eye` (mắt vẽ bằng toán) |
| `Assets/PawFit/Editor` | Menu Setup Project · tự đặt thông số import FBX Gen 1 |
| `Tools/Blender` | Bộ dựng thú Gen 1 bằng script + xuất / kiểm tra FBX |
| `Tools/LogicTests` · `Tools/UnityCompileCheck` | Test logic / compile thử code Unity bằng dotnet |
