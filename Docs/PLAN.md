# PawFit — Plan tổng thể

Game nuôi thú ảo 3D (cảm hứng Tamagotchi): 1 người = 1 thú, chết vĩnh viễn, tiến hóa theo chăm sóc + huấn luyện, đánh nhau bằng vuốt tay, PvP real-time có Ranking.

## Tech stack (đã chốt)

- **Unity 6.3 LTS** (thay 2022.3 vì bản này hết hỗ trợ), URP, C#.
- Android + iOS. Màn vườn để dọc, màn đấu để ngang.
- Firebase Auth + Firestore (profile, pet, ranking). Photon Fusion 2.1 cho PvP. `INetworkManager` là interface để thay được.
- **Toàn bộ logic game là C# thuần dạng Service.** Không viết luật game trong MonoBehaviour / UI.

## Chiến lược model thú: Gen 1 → Gen 2

- **Gen 1** = bộ thú do Claude dựng bằng script Blender (`Tools/Blender/`): base mesh + blendshape + xương, 5 dạng Adult. Dùng để build toàn bộ gameplay.
- **Gen 2** = bộ model đẹp hơn, bạn làm sau khi xong gameplay, bằng tool khác.
- Gameplay chỉ biết DNA + interface hiển thị (`IPetAvatar`). Gen 1 = `Gen1PetAvatar` (Phase 1.3). Đổi model chỉ cần thay FBX + lớp mapping DNA → hình, không sửa gameplay.
- Mã DNA có trường `Gen` (1..15). Thú Gen 1 / Gen 2 phân biệt ngay trong mã.

## Các Phase

| Phase | Nội dung | Trạng thái |
|---|---|---|
| 0 | Plan + kiến trúc + GDD ngắn | ✅ |
| 0.5 | POC hình ảnh thú bằng Blender | ✅ |
| Gen 1 | 5 dạng Adult (Lửa, Nước, Ánh Sáng, Thép, Bóng Tối) + FBX | ✅ (song song 1.1) |
| 1.1 | Nền móng Unity 6.3 + Core + Pet DNA (logic + test) | ✅ |
| 1.2 | Vườn dựng bằng code + camera + điều khiển thú (chạm / kéo) | ✅ |
| 1.3 | Thú Gen 1 trong Unity qua `IPetAvatar` + trạng thái bằng hình + animation code + Debug overlay | ✅ |
| 2.1 | Bệnh / chết vĩnh viễn / tưởng niệm / trứng mới (thừa hưởng 1 trait) | ⏳ |
| 2.2 | Tiến hóa (CareScore, TrainingVector, seed + tính cách) + chỉ số + lực chiến | ⏳ |
| 3.1 | SwipeRecognizer + BattleSimulation + Bot (logic thuần) | ⏳ |
| 3.2 | Màn đấu ngang + Mode Huấn luyện (bóng mờ, slow-mo, 10 lần = thuộc chiêu) | ⏳ |
| 4.1 | Firebase Auth + Firestore + giờ server | ⏳ |
| 4.2 | PvP real-time Photon Fusion + tìm đối thủ (Elo ±100, lực chiến ±10%) | ⏳ |
| 4.3 | Ranking Elo + bậc + mùa giải 30 ngày | ⏳ |
| 5.1 | Kinh tế + shop vườn + quảng cáo mock (tối đa 5 lần/ngày) | ⏳ |
| 5.2 | Trang trí + phụ kiện + polish | ⏳ |
| 5.3 | Build Android / iOS | ⏳ |
| 6 | Model thật (Gen 2) + shader lông + LOD mobile | ⏳ (sau gameplay) |

## Kiến trúc thư mục

```
gaao/                            ← gốc repo = gốc project Unity
├── Assets/PawFit/
│   ├── Scripts/
│   │   ├── Core/   [PawFit.Core, C# thuần]   ServiceLocator, EventBus, Time, Random, Save, Config
│   │   ├── Pet/    [PawFit.Pet,  C# thuần]   Dna/, Needs/, PetService
│   │   │   └── View/ [Unity]                 IPetAvatar, Gen1PetAvatar, PlaceholderPetAvatar (thú tạm), PetMoodFx, DnaVisuals
│   │   ├── Core/Unity/ [Unity]               MaterialFactory, Prim, VecExt (dùng chung cho View)
│   │   ├── Garden/ [PawFit.Garden, C# thuần] GardenLayout, PetBrain, PetExpressionService, CareActionService, PointerGestures
│   │   │   └── View/ [Unity]                 GardenScene, GardenBuilder, GardenCamera, GardenInputRouter, PetController
│   │   ├── Evolution/ Battle/ Network/ Economy/   (các Phase sau)
│   │   └── App/    [PawFit.App, Unity]       GameBootstrap (composition root)
│   ├── Resources/Pets/Gen1/                   PawFit_Gen1_PetKit.fbx + PawFit_Gen1_Anchors.json
│   ├── Resources/Shaders/                     PawFit/Gen1Fur, PawFit/Gen1Eye (URP)
│   ├── Editor/     [PawFit.Editor]           menu PawFit ▸ Setup Project, tự đặt thông số import FBX Gen 1
│   └── Tests/EditMode/ [NUnit]               chạy được cả Unity Test Runner lẫn dotnet
├── Tools/LogicTests/                          dotnet test (ngoài Unity)
├── Tools/UnityCompileCheck/                   compile thử code Unity bằng stub (ngoài Unity)
├── Tools/Blender/                             bộ dựng thú Gen 1
├── Docs/                                      PLAN, GDD, PROGRESS, art/
├── Packages/manifest.json
└── ProjectSettings/ProjectVersion.txt
```

**Luật:** asmdef logic bật `noEngineReferences` nên compiler cấm `using UnityEngine` trong logic. View chỉ hiển thị và đổi input thành lệnh gọi Service. Service đổi state rồi bắn event qua EventBus. App là nơi duy nhất chọn implementation cụ thể (Mock / Firebase / Photon).

## Quy trình mỗi Phase

1. Bạn gõ `Làm Phase X`, tôi chỉ làm đúng Phase đó.
2. Logic C# có unit test, tôi chạy `dotnet test` trong container trước khi giao.
3. Commit + push lên nhánh `claude/hopeful-shannon-3602l1`, cập nhật `Docs/PROGRESS.md`.
4. Bạn pull, mở Unity, test theo checklist, gửi lại lỗi Console / ảnh chụp.
