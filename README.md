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

## Công cụ test (chỉ Editor / bản Development)

- Nút **DEV** góc trên trái màn hình (hoặc phím `F1`) mở bảng test:
  - Xem 4 nhu cầu, DNA, việc thú đang làm, biểu cảm.
  - Tăng / giảm từng nhu cầu.
  - Đặt nhanh trạng thái: **Đói** / **Bẩn** / **Buồn ngủ** / **Khỏe hết** / **Cho ngủ**.
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
| `Assets/PawFit/Scripts/Pet` | DNA, nhu cầu, PetService (C# thuần) · `View/`: `IPetAvatar`, thú Gen 1 `Gen1PetAvatar`, thú tạm `PlaceholderPetAvatar`, hiệu ứng `PetMoodFx` |
| `Assets/PawFit/Scripts/Garden` | Bố cục vườn, bộ não thú `PetBrain`, biểu cảm `PetExpressionService`, luật chăm sóc, phân loại chạm/kéo (C# thuần) · `View/`: dựng vườn, camera, input, NavMesh |
| `Assets/PawFit/Scripts/App` | GameBootstrap (nối service, tự chạy khi Play), SceneFlow, Debug overlay |
| `Assets/PawFit/Resources/Pets/Gen1` | Model thú Gen 1 (FBX bản 2: thân LOD0 / LOD1 + blendshape + xương + mọi kiểu bộ phận) + file mốc neo JSON |
| `Assets/PawFit/Resources/Shaders` | Shader URP `PawFit/Gen1Fur` (lông theo vertex color, bẩn) + `PawFit/Gen1Eye` (mắt vẽ bằng toán) |
| `Assets/PawFit/Editor` | Menu Setup Project · tự đặt thông số import FBX Gen 1 |
| `Tools/Blender` | Bộ dựng thú Gen 1 bằng script + xuất / kiểm tra FBX |
| `Tools/LogicTests` · `Tools/UnityCompileCheck` | Test logic / compile thử code Unity bằng dotnet |
