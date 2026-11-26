# PawFit

Game nuôi thú ảo 3D cho Android / iOS (Unity 6.3 LTS, URP, C#). Plan: `Docs/PLAN.md` · Thiết kế: `Docs/GDD.md` · Tiến độ: `Docs/PROGRESS.md`.

## Mở project lần đầu

1. Cài **Unity 6.3 LTS** qua Unity Hub, chọn thêm module Android Build Support (và iOS nếu có Mac).
2. Unity Hub ▸ **Add project from disk** ▸ chọn thư mục gốc repo này. Nếu Hub hỏi bản Editor thì chọn bản 6.3 bạn đã cài.
3. Đợi Unity import xong. Nếu Package Manager báo lỗi phiên bản URP / Test Framework: **Window ▸ Package Manager**, chọn package đó rồi bấm cài bản được gợi ý.
4. Menu **PawFit ▸ Setup Project**: tự tạo + gán URP Asset, tạo scene `Assets/PawFit/Scenes/Main.unity`.
   - Nếu bước tạo URP báo lỗi, làm tay: **Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)**, rồi kéo asset vào **Project Settings ▸ Graphics ▸ Default Render Pipeline**.
5. Mở scene `Main`, bấm **Play**. Console sẽ in: tên thú, mã DNA `PF-XXXX-XXXX-XXXX`, 4 nhu cầu và nơi lưu file.
6. Commit luôn các file `.meta` Unity vừa sinh để giữ GUID ổn định.

## Chạy unit test

- Trong Unity: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.
- Không cần Unity (máy có .NET 8 SDK):

```bash
dotnet test Tools/LogicTests
```

## Thư mục chính

| Thư mục | Nội dung |
|---|---|
| `Assets/PawFit/Scripts/Core` | Service nền, C# thuần: ServiceLocator, EventBus, thời gian, random có seed, lưu JSON, config |
| `Assets/PawFit/Scripts/Pet` | DNA (gene, mã chia sẻ, sinh ngẫu nhiên), nhu cầu, PetService — C# thuần |
| `Assets/PawFit/Scripts/App` | GameBootstrap: nối các service, tự chạy khi bấm Play |
| `Assets/PawFit/Art/Pets/Gen1` | Model thú Gen 1 (FBX: thân + blendshape + xương + bộ kiểu dáng) |
| `Tools/Blender` | Bộ dựng thú Gen 1 bằng script |
| `Tools/LogicTests` | Chạy test logic bằng dotnet |
