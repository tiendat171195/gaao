# PawFit

Game nuôi thú ảo 3D cho Android / iOS (Unity 6.3 LTS, URP, C#). Plan: `Docs/PLAN.md` · Thiết kế: `Docs/GDD.md` · Tiến độ: `Docs/PROGRESS.md` · **Cài môi trường + build Android trên Mac: `Docs/BUILD.md`** · **Máy chủ riêng (tài khoản, sao lưu, PvP): `Server/README.md`**.

## Mở project lần đầu

1. Cài **Unity 6.3 LTS** qua Unity Hub, chọn thêm module Android Build Support (và iOS nếu có Mac).
2. Unity Hub ▸ **Add project from disk** ▸ chọn thư mục gốc repo này. Nếu Hub hỏi bản Editor thì chọn bản 6.3 bạn đã cài.
3. Đợi Unity import xong. Nếu Package Manager báo lỗi phiên bản URP / Test Framework: **Window ▸ Package Manager**, chọn package đó rồi bấm cài bản được gợi ý.
4. Menu **PawFit ▸ Setup Project**. Bước này tự làm các việc sau:
   - Tạo + gán URP Asset.
   - Tạo material gốc `Resources/PawFit/LitBase`.
   - Đặt Active Input Handling = Input Manager (Old). Project không dùng package Input System. Cảnh báo vàng "Input Manager is a legacy feature" là bình thường.
   - Cho phép xoay dọc / ngang.
   - Tạo scene `Assets/PawFit/Scenes/Main.unity`.
   - Nếu Unity hiện hộp thoại "khởi động lại" thì tắt mở lại Unity.
   - Nếu bước tạo URP báo lỗi, làm tay: **Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)**, rồi kéo asset vào **Project Settings ▸ Graphics ▸ Default Render Pipeline**.
5. Game view chọn độ phân giải **1080x1920 (dọc)**, mở scene `Main`, bấm **Play**.
6. Commit luôn các file `.meta` Unity vừa sinh để giữ GUID ổn định.

**Build Android:** menu **PawFit ▸ Build Android ▸ Dev (APK)** (có bảng DEV) / **Dev + chạy trên điện thoại (USB)** / **Release (APK)**. File ra ở `Builds/Android/`. **Build iOS:** **PawFit ▸ Build iOS ▸ Dev / Release (project Xcode)**, ra `Builds/iOS/`, mở bằng Xcode để ký + cài vào iPhone. Hướng dẫn từng bước (cài module, bật gỡ lỗi USB / Developer Mode, cài APK, ký bằng Apple ID, lỗi hay gặp): `Docs/BUILD.md`.

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
| Chạm vào **quầy hàng** (phía trước bên phải, có lối đá dẫn tới) | Mở quầy: mua thuốc / bánh / nước, xem quảng cáo lấy thưởng |
| Chạm vào **hình nộm** ở sân tập | Vào sân tập: Huấn luyện hoặc Đấu Bot |
| Chạm vào **hòm thư** (cạnh cổng đấu trường, sau sân tập) | Cài đặt: bật / tắt âm thanh, tài khoản (biệt danh, mã chuyển máy) khi có máy chủ, xem lại hướng dẫn, copy mã DNA, phiên bản game |
| Chạm vào **cổng đấu trường** (sau sân tập) | Thẻ rank (bậc, Elo, thắng / thua, mùa còn mấy ngày) + nút **Tìm trận** PvP |

Thú tự làm khi rảnh: đi lang thang; đói thì tới chòi ăn đứng chờ; buồn ngủ thì về ổ ngủ.

**Mẹo hướng dẫn:**
- Khung "Mẹo" nền vàng hiện ở dưới màn hình đúng lúc cần. Mỗi mẹo hiện 1 lần, tự làm trước thì mẹo đó bỏ qua. Các mẹo cách nhau tối thiểu 25 giây.
  - Mở đầu: chạm đất → vuốt ve → dắt thú.
  - Khi đói / bẩn / buồn ngủ / ốm.
  - Sau một lúc chơi: quầy hàng → sân tập → đấu trường.
- Vào game sau ≥ 1 giờ vắng có dòng "Bạn vắng 6 giờ. Kem đang đói, bẩn."
- Hòm thư ▸ **Xem lại hướng dẫn** để hiện lại từ đầu.

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
- **Thuốc:** mua ở **quầy hàng** (Ốm 60 Coin, Nguy kịch 150 Coin). Uống xong là bắt đầu hồi phục. Sau đó chỉ cần chăm thường (cho ăn, lau, vuốt ve...) để giữ hồi phục, không cần đủ 3 việc chăm kỹ.

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
- **Chỉ số + lực chiến** theo GDD G8, giảm khi mệt / ốm. Vườn không hiện số; chỉ xem được trong bảng DEV. Khi đấu, thú dùng đúng chỉ số này.

## Sân tập: Huấn luyện + đấu Bot (Phase 3.1–3.2)

- **Vào sân tập:** chạm 1 trong 3 **hình nộm** ở sân tập (xanh = Dễ, vàng = Thường, đỏ = Khó), chọn **Huấn luyện** hoặc **Đấu**.
  - Màn hình xoay ngang. Xong bấm **Về vườn** để quay lại màn dọc.
  - Thú ốm hoặc đang ngủ thì không vào được.
- **Cách vuốt:**

  | Vuốt | Chiêu |
  |---|---|
  | Ngang | Nhanh |
  | Lên | Mạnh: hất tung, cắt đòn đang vung của đối thủ |
  | V | Chiêu riêng |
  | Z | Ultimate |
  | Xuống | Đỡ. Đỡ sát lúc đòn tới thì phản đòn |

  Vuốt sai (nét không rõ hình hoặc quá chậm) thì bị hở 1 giây. Vuốt càng nhanh, càng đúng hình thì đòn càng đau.
- **Huấn luyện:**
  - Có bóng mờ chỉ cách vuốt chiêu đang học. Vuốt đúng 10 lần thì thuộc chiêu (thuộc rồi đánh mạnh hơn). Cần thuộc đủ 4 chiêu mới lên được Ultimate.
  - Vuốt sai không bị phạt, cứ vuốt lại.
  - Hình nộm sắp đánh thì trận chậm lại và hiện cách vuốt xuống để đỡ.
  - Không ai bị hạ gục. Bấm **Xong** để kết thúc.
- **Đấu:**
  - Trận thật 90 giây với Bot mức đó, chỉ số Bot theo chỉ số thú (Dễ ×0,7, Thường ×1, Khó ×1,25).
  - Màn hình chỉ có 2 vạch HP mờ + vạch thời gian mảnh. Hết giờ thì bên còn nhiều %HP hơn thắng.
  - **Bỏ cuộc** tính là thua.
- **Điểm tập:**
  - Mỗi đòn trúng: ngang → Tốc độ; lên → Sức mạnh; V, Z → cả hai. Phản đòn → Tốc độ.
  - Thắng Bot được thêm điểm + Coin (Dễ +5, Thường +10, Khó +15).
  - Mỗi buổi tốn năng lượng + no bụng.

## Coin, quầy hàng, quảng cáo (Phase 5.1)

- **Ví Coin:** khởi đầu 200 Coin. Ví là của người chơi nên thú mất vẫn còn. Không có nạp tiền.
- **Kiếm Coin:** thắng hình nộm (+5 / +10 / +15), xem quảng cáo (+20), sau này thắng PvP (+30, Phase 4.2).
- **Quầy hàng:** chạm quầy ở phía trước bên phải vườn. Mua xong thú dùng ngay, không có túi đồ. Món chưa mua được thì xám, lý do ghi trong ngoặc. Đồ ăn thường ở chòi ăn vẫn miễn phí.

  | Món | Giá | Tác dụng | Không bán khi |
  |---|---|---|---|
  | Thuốc chữa bệnh | 60 (Ốm) / 150 (Nguy kịch) | Bắt đầu hồi phục | Thú khỏe hoặc chỉ Mệt · đã uống trong đợt này |
  | Bánh thưởng | 25 | No +15, vui +25 | Đang ngủ · no ≥ 92 |
  | Nước tăng lực | 30 | Năng lượng +30 | Đang ngủ · năng lượng ≥ 92 |

- **Quảng cáo tự nguyện** (nút trong quầy hàng):
  - Chọn **+20 Coin** hoặc **+20 năng lượng** cho thú. Thú còn ≥ 90 năng lượng thì không mời xem loại năng lượng.
  - Tối đa **5 lượt / ngày**, tính chung cả 2 loại. Qua 0 giờ (giờ trên máy) là có lại 5 lượt. Chỉnh đồng hồ lùi không được thêm lượt.
  - Phải xem hết mới có thưởng. Bỏ qua giữa chừng thì không có thưởng nhưng cũng không mất lượt.
  - Bản dev là **quảng cáo giả lập 3 giây** (màn tối có đếm ngược + nút Bỏ qua). Phase 5.3 thay bằng quảng cáo thật.
- Số liệu nằm ở `EconomyConfig` trong `GameConfig.cs` (giá thuốc ở `HealthConfig`), chỉnh được không cần sửa luật.

## PvP + rank (Phase 4)

Có 2 chế độ, tự chọn theo file `Assets/PawFit/Resources/PawFitServer.txt` (bản Dev đổi được ở bảng DEV ▸ Máy chủ):

- **Chưa có địa chỉ máy chủ:** chơi offline. Đối thủ là người chơi ảo chạy trên máy, Elo tính trên máy.
- **Có máy chủ riêng** (`Server/README.md`):
  - Tài khoản khách tự tạo, giờ máy chủ, sao lưu, mã chuyển máy ở hòm thư.
  - Máy chủ ghép **người thật** trước, chờ 6 giây chưa có ai mới thả người chơi ảo (chạy trên máy chủ).
  - Máy chủ chạy trận + tính Elo. Thoát app / mất mạng giữa trận = thua.


- **Tìm trận:**
  - Chạm cổng đấu trường ▸ **Tìm trận**. Màn tìm hiện số giây đã chờ và khoảng đang tìm.
  - Lúc đầu tìm đối thủ chênh Elo ±100, lực chiến ±10%. Chờ 30 giây thì nới ra ±200 / ±20%.
  - Chờ 60 giây thì có thêm nút **Đấu hình nộm**, vẫn tìm tiếp. Bấm **Hủy tìm** để thôi.
- **Người chơi ảo** (chế độ offline, hoặc máy chủ vắng người):
  - Mỗi người chơi ảo có biệt danh, có thú riêng (DNA, tên, cùng giai đoạn tiến hóa với thú bạn, dạng Adult, có người đeo phụ kiện).
  - Mỗi người có lối đánh riêng: Hăng / Thủ / Kỹ thuật / Cân bằng. Elo càng cao đánh càng giỏi.
  - Họ vuốt chiêu y như người chơi, không ăn gian.
  - Thường tìm thấy sau khoảng 5–20 giây, như đang có nhiều người chơi.
- **Trận:**
  - Có thẻ "Tìm thấy đối thủ!", rồi màn xoay ngang: thú bạn vs thú đối thủ, 90 giây.
  - Cảm giác đánh (mọi trận):
    - Khựng hình một nhịp khi trúng đòn.
    - Rung camera khi chí mạng / phản đòn / Ultimate.
    - Hạ gục thì chậm lại 1 giây.
  - Thẻ kết quả hiện Elo trước → sau, lên / xuống bậc, thắng +30 Coin. **Bỏ cuộc** tính là thua.
- Thú ốm / đang ngủ không đấu được. Bậc cao nhất của thú được ghi lên bia mộ khi thú mất.

## Phụ kiện + trang trí vườn (Phase 5.2)

Cả 2 đều mua ở quầy hàng, mua 1 lần là có mãi. Đồ là của người chơi nên thú mất vẫn còn, thú mới nở vẫn đeo được. Chỉ để đẹp, không đổi chỉ số hay luật chăm sóc.

- **Phụ kiện** (nút **Phụ kiện cho bé...** trong quầy):

  | Món | Giá |
  |---|---|
  | Nơ hồng (cài lệch 1 bên đầu) | 80 |
  | Mũ len (có quả bông) | 120 |
  | Khăn quàng sọc | 100 |
  | Vương miện vàng | 250 |

  - Mua xong thú đeo luôn và nhảy lên khoe. Mỗi lúc đeo 1 món.
  - Chạm món đã có để đeo, chạm món đang đeo để tháo.
  - Ra sân tập thú vẫn đeo.
- **Trang trí vườn** (nút **Trang trí vườn...**). Mỗi món có chỗ riêng:

  | Món | Giá | Chỗ |
  |---|---|---|
  | Dây đèn lồng (3 lồng đung đưa) | 150 | Giữa hồ và sân tập |
  | Ghế gỗ | 120 | Trước hồ |
  | Cối xay gió (cánh quay) | 300 | Sau chòi ăn |
  | Vòm hoa | 200 | Phía trước bên trái |

  - Mua xong camera lia tới, món đó bật lên từ mặt đất, rồi camera quay về thú.
  - Thú đi vòng qua đồ trang trí, không đi xuyên.
- **Âm thanh:** "ting-ting" khi nhận Coin, "cạch-ting" khi tiêu Coin. Quầy hàng và hình nộm nảy nhẹ khi chạm.
- Giá nằm ở `EconomyConfig.AccessoryPrices` / `DecorPrices`.

## Công cụ test (chỉ Editor / bản Development)

- Nút **DEV** góc trên trái màn hình (hoặc phím `F1`) mở bảng test:
  - Xem 4 nhu cầu, DNA, việc thú đang làm, biểu cảm.
  - Tăng / giảm từng nhu cầu.
  - Đặt nhanh trạng thái: **Đói** / **Bẩn** / **Buồn ngủ** / **Khỏe hết** / **Cho ngủ**.
  - Sức khỏe: **Mệt** / **Ốm** / **Nguy kịch** / **Chết** / **Uống thuốc** (miễn phí, chỉ để test) / **Khỏi bệnh**. Xem số giờ bỏ bê, đã chăm kỹ mấy việc / 3, % hồi phục.
  - Khi thú đã mất: xem trứng còn bao lâu nở, thừa hưởng trait gì, nút **Trứng nở được ngay**.
  - Tiến hóa: xem giai đoạn, dạng đang nghiêng, CareScore, điểm tập, chỉ số, lực chiến. Nút **+1 ngày tuổi**, **Lên giai đoạn**, **Thuộc 1 chiêu**, **+10** từng hướng tập.
  - Nút **Huấn luyện** / **Đấu Bot Thường**: vào sân tập ngay, không cần đi tới hình nộm.
  - Kinh tế: xem Coin, lượt quảng cáo còn lại, tổng đã nhận / đã tiêu. Nút **+100 Coin** / **-100 Coin** / **Trả lượt QC** / **Mở quầy hàng** / **QC: hết hàng** (giả lập không có quảng cáo).
  - Rank: xem bậc / Elo / mùa. Nút **Thắng PvP giả** / **Thua PvP giả** (đổi Elo như thắng / thua 1 người ngang cơ; thắng +30 Coin). Nút **Đấu trường** mở thẻ đấu trường ngay.
  - Phụ kiện / trang trí: xem đang đeo gì, có mấy món. Nút **Có hết đồ (test)** (có hết, không trừ Coin) và **Đổi phụ kiện** (lần lượt từng món).
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
| `Assets/PawFit/Scripts/Core/Unity` | Tiện ích Unity dùng chung cho View: tạo vật liệu, khối cơ bản, đổi tọa độ, âm thanh tạo bằng code `SynthAudio` |
| `Assets/PawFit/Scripts/Pet` | DNA, nhu cầu, PetService, sức khỏe `Health/`, trứng + bia mộ `Lifecycle/`, tiến hóa + chỉ số `Evolution/` (C# thuần) · `View/`: `IPetAvatar`, thú Gen 1 `Gen1PetAvatar`, thú tạm `PlaceholderPetAvatar`, hiệu ứng `PetMoodFx`, phụ kiện `AccessoryView` |
| `Assets/PawFit/Scripts/Garden` | Bố cục vườn, bộ não thú `PetBrain`, biểu cảm `PetExpressionService`, luật chăm sóc, phân loại chạm/kéo (C# thuần) · `View/`: dựng vườn (có quầy hàng), đồ trang trí `DecorBuilder`, camera, input, NavMesh, bia mộ, trứng, thẻ / bảng UI `GardenUi` |
| `Assets/PawFit/Scripts/Battle` | Đấu (C# thuần): nhận dạng vuốt, mô phỏng trận theo bước cố định có seed, Bot 3 mức, phiên Huấn luyện / Đấu Bot · `View/`: màn đấu ngang, hình nộm, HUD, bóng mờ nét vuốt |
| `Assets/PawFit/Scripts/Economy` | Kinh tế (C# thuần): ví Coin + quảng cáo `EconomyService`, quầy hàng `ShopService`, phụ kiện + trang trí `CosmeticsService`, cổng quảng cáo `IAdService` + bản giả lập `MockAdService` |
| `Assets/PawFit/Scripts/Online` | Phase 4 phần không cần tài khoản (C# thuần): rank Elo + bậc + mùa giải `RankService`, luật ghép trận + hàng chờ `Matchmaker`, người chơi ảo `BotPlayers`, cổng mạng `INetworkManager` + bản giả lập `LocalNetworkManager` |
| `Assets/PawFit/Scripts/App` | GameBootstrap (nối service, tự chạy khi Play), SceneFlow (chuyển màn, quầy hàng, quảng cáo, phụ kiện, trang trí), tiếng Coin `UiSounds`, Debug overlay |
| `Assets/PawFit/Resources/Pets/Gen1` | Model thú Gen 1 (FBX bản 2: thân LOD0 / LOD1 + blendshape + xương + mọi kiểu bộ phận) + file mốc neo JSON |
| `Assets/PawFit/Resources/Shaders` | Shader URP `PawFit/Gen1Fur` (lông theo vertex color, bẩn) + `PawFit/Gen1Eye` (mắt vẽ bằng toán) |
| `Assets/PawFit/Editor` | Menu Setup Project · tự đặt thông số import FBX Gen 1 |
| `Tools/Blender` | Bộ dựng thú Gen 1 bằng script + xuất / kiểm tra FBX |
| `Tools/LogicTests` · `Tools/UnityCompileCheck` | Test logic / compile thử code Unity bằng dotnet |
