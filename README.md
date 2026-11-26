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

**Phím tắt test (chỉ trong Editor):** `1` tua +1 giờ · `2` +6 giờ · `3` +24 giờ · `N` in 4 nhu cầu ra Console.

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
| `Assets/PawFit/Scripts/Pet` | DNA, nhu cầu, PetService (C# thuần) · `View/`: thú tạm `PlaceholderPetAvatar` qua `IPetAvatar` |
| `Assets/PawFit/Scripts/Garden` | Bố cục vườn, bộ não thú `PetBrain`, luật chăm sóc, phân loại chạm/kéo (C# thuần) · `View/`: dựng vườn, camera, input, NavMesh |
| `Assets/PawFit/Scripts/App` | GameBootstrap (nối service, tự chạy khi Play), SceneFlow, phím tắt test |
| `Assets/PawFit/Art/Pets/Gen1` | Model thú Gen 1 (FBX: thân + blendshape + xương + bộ kiểu dáng) |
| `Tools/Blender` | Bộ dựng thú Gen 1 bằng script |
| `Tools/LogicTests` · `Tools/UnityCompileCheck` | Test logic / compile thử code Unity bằng dotnet |
