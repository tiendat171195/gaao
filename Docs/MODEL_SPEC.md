# PawFit — Yêu cầu kỹ thuật model (Gen 2 + vườn)

Tài liệu cho người làm model: làm bằng tool nào cũng được (Blender, Maya, tool tạo 3D bằng AI, mua asset có sẵn). Model làm đúng các mục **Bắt buộc** thì ghép vào game không phải sửa gameplay. Mục **Nên có** giúp thú sống động hơn; thiếu thì game dùng cách thay thế (ghi ở từng mục).

Mọi số ở đây lấy từ code hiện tại: thú Gen 1, vườn dựng bằng code, màn đấu. Khi giao model, tôi viết lớp hiển thị mới (`Gen2PetAvatar`, vườn dạng prefab) theo đúng tài liệu này.

**Tự làm bằng tool AI:** làm theo `Docs/AI_ART_GUIDE.md`. Phần xương đúng tên, điểm gắn `Anchor_*`, mask màu DNA, LOD1, cỡ và hướng tôi xử lý bằng script Blender. Bạn chỉ cần tạo model có màu + gắn xương tự động, rồi gửi file.

Phong cách (GDD): **ngầu + cute, không cartoon phẳng**. Lông xù kiểu thú bông, sau này thêm shader lông (Phase 6).

---

## 1. Quy ước chung (Bắt buộc)

| Mục | Quy ước |
|---|---|
| Định dạng | **FBX** (binary). Mỗi model 1 file, hoặc nhiều model trong 1 file nhưng mỗi model là 1 object gốc riêng |
| Đơn vị | **1 đơn vị = 1 mét**. Kéo vào Unity thấy đúng kích thước, không phải chỉnh Scale |
| Trục (trong Unity) | **Y hướng lên, mặt thú nhìn về +Z**, bên phải thú là +X |
| Gốc (pivot) | Thú: **trên mặt đất, giữa 4 chân** (0, 0, 0). Đồ vật: giữa đáy, chạm đất |
| Transform | Đã "apply" hết: Rotation 0, Scale 1 ở mọi object (xương cũng vậy) |
| Đặt tên | Tiếng Anh không dấu, không khoảng trắng, đúng tên trong tài liệu này (code tìm theo tên) |
| Texture | PNG, cạnh là lũy thừa của 2 (512, 1024, 2048). Đặt cạnh FBX, đừng nhúng vào FBX |

**Xuất từ Blender:**
- Thú quay mặt về phía **-Y** (phím số 1 = nhìn từ trước thấy mặt thú).
- **Export ▸ FBX**: Scale `1.0`, Apply Scalings `FBX All`, Forward `-Z Forward`, Up `Y Up`, tick **Apply Transform**.
- Armature: bỏ tick *Add Leaf Bones*.
- Tool khác: kiểm tra kết quả trong Unity đúng bảng trên là được.

---

## 2. Thú (Gen 2)

### 2.1. Kích thước

Thú to cỡ thú bông lớn. Kích thước đo từ model Gen 1 đang dùng (vườn, màn đấu, phụ kiện đều chỉnh theo cỡ này):

| Giai đoạn | Cao tới đỉnh đầu (chưa tính tai / sừng) | Tâm đầu / bán kính đầu | Ghi chú |
|---|---|---|---|
| Baby | ~0,95 m | 0,71 m / 0,24 m | đầu rất to so với thân (đường kính đầu ~50% chiều cao) |
| Child | ~1,0 m | | |
| Teen | ~1,1 m | | bắt đầu ra nét dạng Adult sắp thành |
| Adult Nước | ~1,0 m | 0,78 / 0,23 | thấp, thân dài |
| Adult Thép | ~1,15 m | 0,91 / 0,24 | chắc nịch |
| Adult Lửa | ~1,2 m | 0,96 / 0,23 | |
| Adult Bóng Tối | ~1,25 m | 1,04 / 0,21 | thon dài |
| Adult Ánh Sáng | ~1,3 m | 1,08 / 0,22 | cao thanh |
| Ultimate | Adult × 1,08 | | Adult + chi tiết "xịn" (vòng sáng, giáp, cánh nhỏ...) |

- Dài từ mũi tới chóp đuôi khoảng 1,1–1,5 m.
- Từ gốc (giữa 4 chân) tới mũi ≤ 1 m: màn đấu đặt 2 thú cách nhau 2,6 m, dài hơn sẽ chạm nhau.

### 2.2. Chọn cách làm

Gameplay chỉ cần thú có đủ "điểm cắm" ở mục 2.4–2.8. Có 2 cách dựng, chọn 1:

**Cách 1 — mỗi giai đoạn / dạng là 1 model riêng (khuyên dùng):**
- Dễ làm bằng mọi tool, kể cả tool AI, và dễ làm đẹp nhất.
- Cần tối thiểu **8 model**: Baby, Child, Teen, 5 Adult (Lửa, Nước, Ánh Sáng, Thép, Bóng Tối).
- Ultimate (nên có): 5 model, hoặc dùng Adult + phần gắn thêm.
- Lúc thú tiến hóa, game đổi model kèm hiệu ứng lóe sáng.
- **Cả 8 model dùng chung 1 bộ xương** (cùng tên, cùng thứ bậc; vị trí xương được khác) để chuyển động bằng code chạy chung.

**Cách 2 — 1 thân + blendshape (giống Gen 1, nâng cao):**
- 1 thân gốc (dáng Baby / Child) kèm blendshape biến dần thành từng dạng Adult: `to_adult_fire`, `to_adult_water`, `to_adult_light`, `to_adult_steel`, `to_adult_shadow` (100% = Adult hoàn chỉnh).
- Thú lớn mượt từng ngày, Teen "nghiêng" dần về dạng sắp thành.
- Khó hơn: blendshape làm dời đầu / đuôi, nên phải giao kèm bảng dời của tâm đầu, gốc đuôi và đầu mỗi xương cho từng blendshape. Gen 1 có file mẫu `Assets/PawFit/Resources/Pets/Gen1/PawFit_Gen1_Anchors.json`.

### 2.3. Năm dạng Adult (GDD mục 4)

| Dạng | Dáng | Màu / điểm nhận diện |
|---|---|---|
| Lửa | cân đối, chân dài | đỏ cam, chân + lưng sẫm, sọc than hồng phát sáng, 2 sừng cong đen chóp sáng |
| Nước | thấp, thân dài (kiểu rái cá) | xanh biển, đốm phát sáng xanh, tai vây, đuôi vây, lông ngắn bóng |
| Ánh Sáng | cao thanh, chân mảnh | kem trắng xù, 1 sừng vàng phát sáng, chóp lông vàng |
| Thép | chắc nịch, chân to | xám bạc sọc như giáp, 2 sừng kim loại, tai tròn |
| Bóng Tối | thon dài | tím đen, sọc tím phát sáng, 3 sừng dài, mắt đỏ |

Màu ở bảng là màu "đặc trưng của dạng". Màu thật của từng con do DNA quyết định (mục 2.6), nên **phần lông phải tô lại được bằng code**.

### 2.4. Xương (Bắt buộc)

Chuyển động hiện tại làm bằng code, dựa trên tên xương:
- bước chân, thở, vẫy đuôi, quay đầu nhìn;
- ăn, kêu, ngáp, ngủ, tập, đánh, đỡ, trúng đòn.

```
root                 (gốc, ở mặt đất)
└── hips             (hông)
    ├── spine        (lưng)
    │   └── chest    (ngực)
    │       ├── neck (cổ)
    │       │   └── head        (đầu — mọi thứ trên đầu bám vào đây)
    │       │       ├── jaw     (hàm, nên có: há miệng)
    │       │       ├── ear_L, ear_R   (tai, nên có)
    │       ├── legFL  (chân trước trái)   ──  legFL_2 (khớp dưới, nên có)
    │       └── legFR  (chân trước phải)   ──  legFR_2
    ├── legBL  (chân sau trái)    ──  legBL_2
    ├── legBR  (chân sau phải)    ──  legBR_2
    └── tail_1 ── tail_2 ── tail_3   (đuôi, 2–4 đốt)
```

- Mỗi đỉnh chịu ảnh hưởng **tối đa 4 xương**; tổng **≤ 40 xương**.
- Tư thế gốc: đứng 4 chân thẳng, đầu nhìn thẳng (+Z), đuôi hơi vểnh.
- **Nên có:** clip hoạt cảnh làm sẵn (Unity dùng thay cho chuyển động bằng code, đẹp hơn nhiều):
  - Đi lại: `idle`, `walk`, `run`, `sleep` (lặp).
  - Chăm sóc: `hop`, `eat`, `bathe`, `refuse` (lắc đầu), `cry` (ngẩng đầu kêu), `yawn`, `train` (đấm hình nộm).
  - Đánh: `attack_quick`, `attack_heavy`, `attack_special`, `attack_ultimate`, `guard`, `hit`, `ko`, `win`.
  - Thiếu clip nào thì code tự làm động tác đó như Gen 1.

### 2.5. Bộ phận đổi theo DNA (Bắt buộc với Cách 2, nên có với Cách 1)

DNA (GDD mục 3) chọn kiểu bộ phận. Mỗi kiểu là 1 mesh riêng, gắn (parent / skin) vào xương `head` hoặc `hips`. Game bật đúng kiểu, ẩn các kiểu còn lại.

| Bộ phận | Các kiểu (theo thứ tự gene) | Tên object |
|---|---|---|
| Tai | nhọn, tròn, cụp, vây | `PF_Ear_nhon_L`, `PF_Ear_nhon_R`, `PF_Ear_tron_L` … `PF_Ear_vay_R` |
| Sừng | 0–3 chiếc; dáng thẳng / dài / cong | 5 chỗ cắm: `PF_Horn_C` (giữa), `PF_Horn_PL` / `PF_Horn_PR` (cặp), `PF_Horn_WL` / `PF_Horn_WR` (cặp xa). Dáng: blendshape `dai`, `cong` trên mỗi sừng (gốc = thẳng) |
| Đuôi | cáo, chùm, bông, vây, xoắn | `PF_Tail` + blendshape `tua`, `bong`, `vay`, `xoan` (gốc = cáo); hoặc 5 mesh `PF_Tail_cao` … `PF_Tail_xoan` |
| Mắt | tròn, hạnh nhân, mèo, lim dim, cún, sắc | `PF_Eye_L`, `PF_Eye_R` + blendshape `hanh_nhan`, `meo`, `lim_dim`, `cun`, `sac_hep` (gốc = tròn) |
| Mặt | mũi, miệng, viền mắt | `PF_Nose`, `PF_Mouth`, `PF_Liner_L`, `PF_Liner_R` |

**Dáng theo DNA** (blendshape trên thân `PF_Body`, 0–100%):
- `head_big`: đầu to hơn;
- `body_chubby`: mũm mĩm;
- `legs_long`: chân dài.

Với Cách 1, 3 blendshape này **nên có** trên từng model. Không có thì game co giãn xương `head`, `spine` và các chân để thay.

### 2.6. Màu lông theo DNA (Bắt buộc)

Mỗi con có màu lông riêng: hue 64 bậc × độ đậm × độ sáng, kèm vùng sáng, sọc / đốm, chân sẫm. Vì vậy **không vẽ chết màu lông**. Giao:

1. **Albedo xám / trắng** (`*_Albedo.png`): chi tiết sợi lông, khối sáng tối. Game nhân với màu lông DNA.
2. **Mask** (`*_Mask.png`, RGBA, cùng UV), giống 4 kênh màu đỉnh của Gen 1:

   | Kênh | Vùng | DNA dùng |
   |---|---|---|
   | R | bụng / mõm / ngực sáng | BellyTone (0–3) |
   | G | chân + chóp tai sẫm | PawsDark, EarTipDark |
   | B | sọc | Pattern = Stripes |
   | A | đốm | Pattern = Spots |

3. **Phát sáng** (nên có, `*_Emission.png`): vùng phát sáng của dạng Adult (than hồng Lửa, đốm Nước, chóp vàng Ánh Sáng, sọc tím Bóng Tối). Game tô theo màu dạng.
4. **Normal map** (nên có, `*_Normal.png`).

- Không dùng texture thì có thể **tô màu đỉnh (vertex color)** đúng 4 kênh trên. Gen 1 đang làm vậy.
- Mắt: lòng đen tô được theo 12 màu DNA, nên giao **albedo + mask lòng đen** riêng cho mắt. Đồng tử và đốm sáng vẽ sẵn.
- Sừng, móng, giáp (Thép) được vẽ màu cố định.

### 2.7. Biểu cảm (Bắt buộc)

| Biểu cảm | Cần có | Thiếu thì |
|---|---|---|
| Nhắm / mở mắt (chớp, buồn ngủ, ngủ) | blendshape `eye_close` trên `PF_Eye_L/R`, hoặc mí mắt riêng | game co dẹt mắt (như Gen 1) |
| Há miệng (ăn, kêu, ngáp) | xương `jaw`, hoặc blendshape `mouth_open` trên đầu | game phóng to `PF_Mouth` |
| Tai cụp (buồn, ốm) | xương `ear_L/R` | game xoay cả mesh tai |
| Ốm (lông nhợt, run) | không cần, shader + code tự làm | — |
| Bẩn (ruồi bay, vết bẩn) | nên có: texture vết bẩn `*_Dirt.png` (đen trắng) | game dùng hiệu ứng ruồi như hiện nay |

### 2.8. Điểm gắn (Bắt buộc)

Thêm **object rỗng (Empty / Null)** con của xương `head` (trừ khi ghi khác), đúng tên:

| Tên | Vị trí | Dùng cho |
|---|---|---|
| `Anchor_Head` | tâm khối đầu. Trục: +Y lên đỉnh đầu, +Z ra phía mặt | phụ kiện (nơ, mũ, vương miện) |
| `Anchor_HeadTop` | đỉnh đầu, giữa 2 tai | bong bóng cảm xúc, "Zzz" |
| `Anchor_Mouth` | trước miệng | đồ ăn bay vào, há miệng |
| `Anchor_Neck` | quanh cổ (con của `neck`) | khăn quàng |
| `Anchor_Tail` | gốc đuôi (con của `hips`) | hiệu ứng vẫy đuôi |

Giao kèm **bán kính đầu** (m), ví dụ ghi vào tên `Anchor_Head_r0.25`, hoặc 1 dòng ghi chú. Game dùng số này để phụ kiện ôm vừa đầu.

### 2.9. Ngân sách điện thoại (Bắt buộc)

Mục tiêu 60 khung hình / giây trên máy Android tầm trung (URP).

| | LOD0 (màn đấu, cận cảnh) | LOD1 (vườn, nhìn từ xa) |
|---|---|---|
| Số tam giác (cả thân + bộ phận đang bật) | ≤ 15.000 | ≤ 6.000 |
| Vật liệu | ≤ 3 (lông, mắt, sừng / phụ) | như LOD0 |
| Texture lông | 2048² | dùng chung LOD0 |
| Blendshape trên thân | ≤ 16 | — |

- Tên mesh LOD1: thêm hậu tố `_LOD1` (vd `PF_Body_LOD1`). Gen 1 đang dùng đúng quy ước này.
- Không cần LOD1 thì báo trước. Khi đó LOD0 phải ≤ 8.000 tam giác.

### 2.10. Phụ kiện (nên có, thay bản dựng bằng code)

| Món | Gắn vào | Ngân sách |
|---|---|---|
| Nơ hồng (lệch 1 bên đầu) | `Anchor_Head` | ≤ 1.500 tam giác, 512² |
| Mũ len có quả bông | `Anchor_Head` | ≤ 1.500 |
| Khăn quàng | `Anchor_Neck` | ≤ 2.000 |
| Vương miện | `Anchor_Head` | ≤ 1.500 |

- Dựng cho **đầu bán kính 1 m** (tâm ở gốc, +Y lên, +Z ra mặt). Game co lại theo bán kính đầu thật của từng con.
- Tên file: `Acc_Bow.fbx`, `Acc_Beanie.fbx`, `Acc_Scarf.fbx`, `Acc_Crown.fbx`.

---

## 3. Vườn

### 3.1. Khung vườn

- Mặt đất phẳng (cho phép gò rất nhẹ ≤ 0,2 m). Thú đi trên **NavMesh nướng lúc chạy** từ mặt đất + collider của vật cản.
- **Vùng đi được: X từ −10 đến +10, Z từ −13 đến +14** (20 × 27 m). Gốc (0, 0) là ổ ngủ.
- Ngoài vùng này là **cảnh nền** (đồi, cây, bầu trời): không collider, càng ít tam giác càng tốt.
- **Camera:** màn dọc, nhìn xuống **50°**, cách thú **12,5 m**, đi theo thú, người chơi kéo được.
  - Mọi thứ phải **đọc được từ trên cao chéo xuống**: mái, mặt nước, ổ, đường đi.
  - Đừng để vật cao che mất thú.
- Lối đi cho thú rộng **≥ 1 m**.

### 3.2. Các khu (Bắt buộc, đúng vị trí)

Tọa độ (X, Z) tính bằng mét, Y = 0 là mặt đất. "Bán kính" = vùng người chơi thả thú vào / chạm vào.

| Khu | Tên object | Tâm (X, Z) | Bán kính | Ghi chú |
|---|---|---|---|---|
| Ổ ngủ | `Zone_Bed` | (0, 0,5) | 1,3 | ổ tròn mềm. Thú nằm ngủ giữa ổ |
| Chòi ăn | `Zone_FoodHut` | (−7, 1) | 1,8 | chòi nhỏ có mái; thú đứng ăn ở (−5,2; 0,4) |
| Khay đồ ăn | `FoodTray` | cạnh chòi | — | khay tròn ~1 m có táo; người chơi kéo táo từ đây (cần collider) |
| Hồ tắm | `Zone_Pond` | (6,8; 0,5) | 2,6 | mặt nước `Pond_Water` (thú thả vào = tắm), đá + lá súng quanh bờ |
| Sân tập | `Zone_Training` | (0, 9,5) | 3,4 | nền đất nện; thú đứng tập ở (0; 6,8) |
| Hình nộm Dễ / Thường / Khó | `Dummy_0`, `Dummy_1`, `Dummy_2` | sân tập + (−2; 1,3), (0; 2), (2; 1,3) | — | cao ~2 m, có collider, 3 màu / kiểu dễ phân biệt (đang dùng xanh lá / vàng / đỏ) |
| Cổng đấu trường | `ArenaGate` | (0; 12,9) | — | cổng 2 cột cao ~2,4 m, có collider |
| Hòm thư | `Mailbox` | (−2,4; 12,5) | — | cao ~1,2 m, có collider |
| Quầy hàng | `Zone_Shop` | (4,5; −6,5) | 1,5 | sạp nhỏ có mái che; chỗ đứng mua ở (3,85; −8,3) |
| Khu tưởng niệm | `Zone_Memorial` | (8, 12) | 1,5 | góc yên tĩnh. Bia mộ là **1 prefab `Gravestone`** (game đặt 1 bia / thú đã mất) |

- Lối đá nối các khu: nên có, đặt thẳng trong cảnh.
- **Chỗ đặt đồ trang trí** (mua ở quầy, mặc định ẩn):

  | Món | Tên object | Tâm (X, Z) | Hướng |
  |---|---|---|---|
  | Dây đèn lồng | `Decor_Lanterns` | (4,9; 5,6) | 0° |
  | Ghế gỗ | `Decor_Bench` | (5,2; −2,7) | 25° |
  | Cối xay gió | `Decor_Windmill` | (−5,6; 5,6) | −15° |
  | Vòm hoa | `Decor_FlowerArch` | (−4,6; −7,4) | −20° |

  Mỗi món ≤ 3.000 tam giác. Món có phần chuyển động (cánh cối xay, đèn đung đưa) thì tách thành object con riêng, có tên.

### 3.3. Đồ vật nhỏ (nên có)

| Món | Tên file | Ghi chú |
|---|---|---|
| Quả táo (cho ăn) | `Prop_Apple.fbx` | ~0,15 m, người chơi kéo được |
| Bát cháo (chữa ốm) | `Prop_Porridge.fbx` | |
| Bánh, nước, thuốc (quầy hàng) | `Prop_Treat.fbx`, `Prop_Drink.fbx`, `Prop_Medicine.fbx` | bay vào miệng thú |
| Quả trứng | `Prop_Egg.fbx` | ~0,5 m, tô màu được (mask như mục 2.6) |
| Bia mộ | `Gravestone.fbx` | ~0,6 m |
| Chăn (ổ ngủ lúc ốm) | `Prop_Blanket.fbx` | |

### 3.4. Ngân sách vườn (Bắt buộc)

- Nhìn từ camera vườn: **≤ 120.000 tam giác** trên màn hình (không kể thú), **≤ 80 lệnh vẽ (draw call)**.
- Gộp texture (atlas): 2–4 texture 2048² cho cả vườn. Vật liệu dùng chung càng nhiều càng tốt.
- Đồ đứng yên đánh dấu được **Static** (tôi làm phía Unity).
- Ánh sáng: 1 mặt trời + ánh sáng môi trường. Có ảnh nướng sáng (lightmap) thì gửi kèm, không có cũng được.

### 3.5. Màn đấu (nên có, cảnh riêng)

- Màn ngang. 2 thú đứng ở **X = −1,3 và +1,3**, mặt quay vào nhau.
- Sàn đấu hình bầu dục khoảng **6,5 × 4 m**.
- Camera nhìn vào điểm (0; 0,75; 0) với góc nhìn 38°.
- Phía sau là phông nền (khán đài, cây, núi): không collider, ≤ 60.000 tam giác.

---

## 4. Giao file

```
PawFit_Gen2/
├── Pets/
│   ├── Pet_Baby.fbx, Pet_Child.fbx, Pet_Teen.fbx              (Cách 1)
│   ├── Pet_Adult_Fire.fbx … Pet_Adult_Shadow.fbx
│   ├── Pet_Ultimate_*.fbx                                       (nên có)
│   ├── Pet_*_Albedo.png, Pet_*_Mask.png, Pet_*_Emission.png, Pet_*_Normal.png
│   └── Eye_Albedo.png, Eye_Mask.png
├── Accessories/  Acc_Bow.fbx, Acc_Beanie.fbx, Acc_Scarf.fbx, Acc_Crown.fbx
├── Garden/       Garden.fbx (đủ object tên như mục 3.2) + texture atlas
├── Props/        Prop_*.fbx, Gravestone.fbx
└── Arena/        Arena.fbx
```

- Gửi theo từng đợt được. **Đợt 1 nên là 1 model Adult** (vd Lửa) đúng mục 2.4–2.9: tôi ghép thử vào game, chỉnh quy ước nếu cần, rồi mới làm tiếp các model còn lại.
- Kèm **ảnh chụp** model ở 3 góc (trước, bên, 3/4) và ghi chú số tam giác.

## 5. Tự kiểm trước khi giao

- [ ] Kéo FBX vào Unity: thú đứng trên mặt đất, mặt nhìn +Z, cao đúng mục 2.1, Scale = 1.
- [ ] Có đủ xương đúng tên (mục 2.4) và điểm gắn `Anchor_*` (mục 2.8).
- [ ] Phần lông là màu xám / trắng + mask 4 kênh (hoặc màu đỉnh), không vẽ chết màu.
- [ ] Số tam giác LOD0 ≤ 15.000, LOD1 ≤ 6.000.
- [ ] Vườn: tên object + tọa độ các khu đúng mục 3.2, vùng đi được phẳng, lối đi ≥ 1 m.
