# Làm model PawFit bằng tool AI (Gen 2 + vườn)

Cách chia việc:
- **Bạn:** dùng AI tạo hình (ảnh mẫu → model 3D có màu → gắn xương tự động), rồi gửi file cho tôi.
- **Tôi:** xử lý kỹ thuật bằng script Blender trong container, để model đúng `Docs/MODEL_SPEC.md`:
  - chỉnh cỡ, hướng, gốc; đổi tên xương;
  - thêm điểm gắn `Anchor_*`;
  - tạo mask màu theo DNA (bụng, chân, chóp tai, sọc, đốm);
  - làm bản nhẹ LOD1, kiểm số tam giác;
  - ghép vào game (`Gen2PetAvatar`).

Bạn **không cần học Blender**, cũng không cần lo tên xương hay mask.

---

## 1. Chọn tool

| Việc | Tool gợi ý | Ghi chú |
|---|---|---|
| Ảnh mẫu (concept) | ChatGPT (tạo ảnh), Gemini, Midjourney: tool nào bạn đang có | Ra ảnh mẫu trước thì các model đồng bộ phong cách hơn hẳn |
| Ảnh → model 3D | **Tripo** (tripo3d.ai) hoặc **Meshy** (meshy.ai) | Cả hai có: tạo từ ảnh, tô màu, giảm số tam giác, **gắn xương tự động cho thú 4 chân**, xuất FBX |
| Model chất lượng cao (tùy chọn) | Rodin (hyper3d.ai) | đẹp nhưng đắt hơn |
| Miễn phí hoàn toàn (tùy chọn) | Hunyuan3D (Tencent, mã nguồn mở) | không có gắn xương tự động như Tripo / Meshy |

**Nên làm:** thử bản miễn phí của **cả Tripo lẫn Meshy với cùng 1 ảnh mẫu**, chọn bên ra đẹp hơn, rồi mới trả tiền gói tháng. Gói Pro khoảng 20 USD / tháng; 1 tháng thường đủ làm cả bộ.

⚠️ **Giấy phép:** bản miễn phí thường bắt ghi công hoặc cấm dùng thương mại. Ví dụ model tạo ở gói Free của Meshy theo giấy phép CC BY 4.0. Bản phát hành lên store nên tạo (hoặc tạo lại) bằng gói trả phí, và đọc điều khoản của tool lúc bạn dùng.

---

## 2. Quy trình cho 1 con thú

### Bước 1. Ảnh mẫu (tool tạo ảnh)

Viết câu lệnh **bằng tiếng Anh** (tool AI hiểu tốt hơn). Mẫu cho Adult dạng Lửa:

```text
Cute but cool fantasy creature for a mobile pet game, quadruped, fox-cat-like, big head,
large expressive eyes, fluffy plush-toy fur, FIRE element: two curved black horns with glowing
tips, glowing ember stripes on the back, ember-orange accents, long legs, balanced body,
full body, standing neutral pose on all four legs, head looking forward, tail slightly raised,
front three-quarter view, plain white background, soft even lighting, no shadow, 3D render style
```

- **Đồng bộ cả bộ:** giữ nguyên câu đầu ("Cute but cool fantasy creature... plush-toy fur"), chỉ thay phần dạng (FIRE → WATER...) và giai đoạn. Đổi tool giữa chừng là lệch phong cách.
- Nên xin thêm ảnh **nhìn ngang (side view)** và **nhìn thẳng (front view)** cùng con đó. Tripo nhận nhiều ảnh, model sẽ đúng hơn.
- **Tư thế:** 4 chân đứng thẳng, không chạy, không ngồi. Đuôi, tai, sừng không che thân. Như vậy gắn xương tự động mới sạch.
- **Màu lông:** để AI vẽ màu bình thường cũng được, tôi sẽ chuyển lông sang xám rồi game tô màu theo DNA. **Đừng để AI vẽ sọc / đốm lên lông**: sọc / đốm do DNA quyết định. Chi tiết riêng của dạng (than hồng, sừng, giáp) thì giữ.

Thay phần dạng theo bảng (GDD mục 4):

| Dạng | Đoạn thay vào |
|---|---|
| Lửa | `FIRE element: two curved black horns with glowing tips, glowing ember stripes on the back, long legs, balanced body` |
| Nước | `WATER element: low long otter-like body, fin-shaped ears, fin tail, short sleek glossy fur, glowing blue spots` |
| Ánh Sáng | `LIGHT element: tall elegant slim legs, very fluffy cream fur, one golden glowing horn, golden fur tips` |
| Thép | `STEEL element: stocky sturdy body, thick legs, armor-like silver plates, two metal horns, round ears` |
| Bóng Tối | `SHADOW element: slender long body, dark purple fur, glowing purple stripes, three long horns, red eyes` |
| Baby | bỏ phần dạng, thay bằng `baby stage: very big round head, tiny body, short legs, no horns, extra fluffy` |
| Child | `child stage: big head, small body, short stubby legs, tiny horn buds` |
| Teen | `teen stage: growing body, medium legs, small horns starting to show, hints of its future element` |

### Bước 2. Ảnh → model 3D (Tripo hoặc Meshy)

1. Chọn **Image to 3D** ▸ tải ảnh mẫu lên. Tripo: nếu có ảnh ngang / thẳng thì dùng chế độ nhiều ảnh.
2. Tạo vài bản, chọn bản đẹp nhất. Soi kỹ: không thủng, không dính chân vào nhau, mặt rõ.
3. Bật **tô màu / texture** (PBR nếu có).
4. **Giảm số tam giác** (Tripo: *Smart Low Poly* / Meshy: *Remesh*, *Low Poly*): đặt khoảng **12.000–15.000 tam giác**. Tôi làm bản nhẹ 6.000 sau.

### Bước 3. Gắn xương tự động

1. Chọn **Rig / Auto-Rigging** ▸ loại **Quadruped** (4 chân).
2. Có thư viện hoạt cảnh thì chọn thêm: `idle`, `walk`, `run`, `attack`, `hit`, `sleep` (thiếu cũng được, code tự làm động tác).
3. **Xuất FBX** (chọn kèm texture nếu tool hỏi). Không có FBX thì xuất GLB cũng được.

### Bước 4. Gửi tôi

1. Chép vào thư mục repo `ArtSource/Gen2/Inbox/<tên>/` (nằm **ngoài** `Assets`, Unity không tự nạp):
   - file `.fbx` (hoặc `.glb`);
   - các file texture `.png`;
   - ảnh mẫu.
2. GitHub Desktop: gõ tóm tắt (vd "Model Lửa đợt 1") ▸ **Commit** ▸ **Push origin**.
3. Nhắn tôi. Tôi xử lý ra bản đúng `MODEL_SPEC.md`, ghép vào game, rồi gửi ảnh trong game cho bạn duyệt.

**Đợt đầu chỉ làm 1 con: Adult Lửa.** Thấy ổn mới làm 7 con còn lại (Baby, Child, Teen, 4 Adult), để khỏi làm lại cả bộ nếu phải đổi quy ước.

---

## 3. Vườn

Có 2 cách, kết hợp được:

1. **Tạo từng món bằng AI** (Tripo / Meshy, Text hoặc Image to 3D). Mỗi món 1 file, đặt tên đúng bảng dưới; tôi ghép vào đúng tọa độ bằng script. Câu lệnh chung để cả vườn đồng bộ:

   ```text
   stylized cozy fantasy garden game asset, soft pastel colors, hand-painted look,
   low poly, clean shapes, mobile game, plain white background
   ```

   rồi thêm tên món, ví dụ `small wooden food hut with red roof and a round food tray`.

2. **Mua gói asset có sẵn** cho nền vườn: cỏ, cây, đá, hàng rào, đồi phía xa. Tìm trên Unity Asset Store với từ khóa "stylized nature". Rẻ, đồng bộ, nhẹ cho điện thoại; nhớ đọc giấy phép.

| Món | Tên file | Ghi chú |
|---|---|---|
| Ổ ngủ | `Zone_Bed` | ổ tròn mềm, đường kính ~2,5 m |
| Chòi ăn | `Zone_FoodHut` | ~2,2 × 2 m, cao ~2,2 m |
| Khay đồ ăn | `FoodTray` | khay tròn ~1 m |
| Hồ tắm | `Zone_Pond` | hồ tròn đường kính ~5 m, nước riêng 1 object |
| Hình nộm Dễ / Thường / Khó | `Dummy_0`, `Dummy_1`, `Dummy_2` | cao ~2 m, 3 màu khác nhau |
| Cổng đấu trường | `ArenaGate` | cao ~2,4 m |
| Hòm thư | `Mailbox` | cao ~1,2 m |
| Quầy hàng | `Zone_Shop` | sạp nhỏ có mái che |
| Bia mộ | `Gravestone` | ~0,6 m |
| Đồ trang trí | `Decor_Lanterns`, `Decor_Bench`, `Decor_Windmill`, `Decor_FlowerArch` | dây đèn lồng, ghế gỗ, cối xay gió, vòm hoa |
| Đồ nhỏ | `Prop_Apple`, `Prop_Porridge`, `Prop_Treat`, `Prop_Drink`, `Prop_Medicine`, `Prop_Egg`, `Prop_Blanket` | táo, bát cháo, bánh, nước, thuốc, trứng, chăn |

Kích thước lệch cũng không sao: tôi co giãn cho đúng lúc ghép. Mỗi món nên **≤ 3.000 tam giác** (chòi, quầy, hồ ≤ 6.000).

---

## 4. Lỗi hay gặp khi dùng AI

| Thấy gì | Cách xử lý |
|---|---|
| Model mỗi lần một phong cách | Dùng chung 1 câu lệnh gốc + cùng 1 tool, tạo từ ảnh mẫu thay vì từ chữ |
| Chân dính nhau / thiếu chân | Ảnh mẫu phải thấy rõ cả 4 chân tách nhau; thử nhiều bản rồi chọn |
| Gắn xương bị méo khi đi | Ảnh mẫu tư thế đứng thẳng, đuôi / tai không chạm thân; thử chế độ Quadruped của tool kia |
| Texture có sọc / đốm sẵn | Tạo lại với `solid single-color fur, no stripes, no spots` |
| File quá nặng (trên 50 MB) | Giảm tam giác ở bước 2; texture 2048 là đủ |
