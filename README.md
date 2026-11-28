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
| 2 ngón chụm / mở · xoay 2 ngón · 2 ngón kéo lên xuống | Phóng to thu nhỏ · xoay vườn · đổi góc nhìn (máy tính: lăn chuột, giữ chuột phải kéo). Hòm thư có nút về góc mặc định |
| Chạm vào thú | Thú quay lại nhìn, nhảy mừng, mắt híp, tim bay. Chạm 3 lần liền = cù (cười khúc khích). Đang ngủ thì thức dậy |
| Xoa qua lại trên người thú | Vuốt ve: thú đứng yên lim dim, dụi đầu theo tay, vẫy đuôi, tim bay, gừ gừ (+ vui). Đang ngủ vẫn vuốt được |
| Kéo thú ra xa | Thú chạy theo ngón tay (dắt). Thả vào **hồ** thì tắm, thả vào **ổ** thì ngủ (nếu đã buồn ngủ), thả lên **ghế gỗ** thì ngồi nghỉ |
| Vuốt **quả bóng** (đồ chơi) | Ném bóng: thú chạy nhặt, ngậm mang về chỗ bạn ném. Chạm bóng = đá nhẹ |
| Giữ + kéo **cần lông vũ** | Chùm lông bay theo ngón tay, thú đuổi theo vồ |
| Chạm **máy bong bóng** / chạm bong bóng | Thổi 8 bong bóng, thú nhảy đớp / bong bóng vỡ |
| Chạm **ghế gỗ** · **đèn lồng** · **cối xay gió** · **vòm hoa** | Thú ra ghế ngồi · đèn lấp lánh, thú ngước nhìn · thú ra ăn túi bánh (mỗi ngày 1 túi) · bướm bay tới rủ thú đuổi |
| Kéo quả táo từ **khay ở chòi ăn** (bên trái) thả lên thú | Cho ăn. No rồi thì thú lắc đầu |
| Chạm **luống đất** ở vườn rau (trước chòi ăn) | Luống trống: chọn hạt để gieo · đang lớn + đất khô: tưới · có ngôi sao lấp lánh: hái quả vào giỏ |
| Chạm **giỏ quả** (cạnh khay táo) | Chọn quả cho thú ăn: thú chạy ra giỏ ăn, mỗi loại quả 1 tác dụng |
| Chạm vào khay đồ ăn | Thú chạy tới chòi ăn |
| Chạm vào **quầy hàng** (phía trước bên phải, có lối đá dẫn tới) | Mở quầy: mua thuốc / bánh / nước, xem quảng cáo lấy thưởng |
| Chạm vào **hình nộm** ở sân tập | Vào sân tập: Huấn luyện hoặc Đấu Bot |
| Chạm vào **hòm thư** (cạnh cổng đấu trường, sau sân tập) | Cài đặt: bật / tắt âm thanh, bật / tắt thông báo nhắc chăm bé (trên điện thoại), tài khoản (biệt danh, mã chuyển máy) khi có máy chủ, xem lại hướng dẫn, copy mã DNA, phiên bản game |
| Chạm vào **cổng đấu trường** (sau sân tập) | Thẻ rank (bậc, Elo, thắng / thua, mùa còn mấy ngày) + nút **Tìm trận** PvP |

Thú tự làm khi rảnh: đi lang thang; đói thì tới chòi ăn đứng chờ; buồn ngủ thì về ổ ngủ (ban đêm buồn ngủ sớm hơn, ngủ một mạch tới sáng); hơi mệt thì ra ghế gỗ ngồi (nếu có); chán thì tự đẩy bóng chơi (nếu có bóng); bướm ở vòm hoa bay tới thì đuổi theo.

**Mẹo hướng dẫn:**
- Khung "Mẹo" nền vàng hiện ở dưới màn hình đúng lúc cần. Mỗi mẹo hiện 1 lần, tự làm trước thì mẹo đó bỏ qua. Các mẹo cách nhau tối thiểu 25 giây.
  - Mở đầu: chạm đất → vuốt ve → dắt thú.
  - Khi đói / bẩn / buồn ngủ / ốm.
  - Sau một lúc chơi: camera → kho báu → vườn rau → quầy hàng → sân tập → đấu trường. Trời tối lần đầu: mẹo ban đêm.
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
  | Z | Tuyệt chiêu (Ultimate): cần **đầy Nộ**, không bị cắt ngang |
  | Xuống | Đỡ. Đỡ sát lúc đòn tới thì phản đòn |

  - Vuốt xong là thú ra đòn ngay. Vuốt sớm lúc thú còn đang hồi chiêu (0,6 giây) cũng được: nét được giữ lại, rảnh ra là đánh.
  - Đánh trúng liên tục thì thành **combo** (mỗi bậc +10% damage, tối đa +40%). Bị trúng đòn / bị đỡ thì mất combo.
  - **Nộ** tăng khi đánh trúng, bị đánh, đỡ được, phản đòn. Đầy thì vạch Nộ nhấp nháy + vầng cam dưới chân thú: vuốt Z.
  - Vết ngón tay đổi màu lúc nhấc tay: vàng = nhận chiêu, đỏ = vuốt sai, xám = không tính.
  - Chỉ nét nguệch ngoạc mới là vuốt sai (bị hở 0,5 giây). Nét cong, nét hơi ngắn / hơi chậm không bị phạt. Vuốt càng nhanh, càng đúng hình thì đòn càng đau.
- **Huấn luyện:**
  - Có bóng mờ chỉ cách vuốt chiêu đang học. Vuốt đúng 10 lần thì thuộc chiêu (thuộc rồi đánh mạnh hơn). Cần thuộc đủ 4 chiêu mới lên được Ultimate.
  - Huấn luyện không có Nộ: vuốt Z là ra Tuyệt chiêu luôn, để tập.
  - Vuốt sai không bị phạt, cứ vuốt lại.
  - Hình nộm sắp đánh thì trận chậm lại và hiện cách vuốt xuống để đỡ.
  - Không ai bị hạ gục. Bấm **Xong** để kết thúc.
- **Đấu:**
  - Trận thật 90 giây với Bot mức đó, chỉ số Bot theo chỉ số thú (Dễ ×0,7, Thường ×1, Khó ×1,25).
  - Màn hình có 2 vạch HP mờ + vạch Nộ + vạch thời gian mảnh. Trận ngang sức thường hết trong khoảng 25–30 giây; hết 90 giây thì bên còn nhiều %HP hơn thắng.
  - **Bỏ cuộc** tính là thua.
- **Điểm tập:**
  - Mỗi đòn trúng: ngang → Tốc độ; lên → Sức mạnh; V, Z → cả hai. Phản đòn → Tốc độ.
  - Thắng Bot được thêm điểm + Coin (Dễ +5, Thường +10, Khó +15).
  - Mỗi buổi tốn năng lượng + no bụng.

## Ngày / đêm (Phase 10.1)

- Vườn sáng / tối theo **giờ trên máy**: bình minh cam hồng, trưa sáng trắng ấm, chiều tà vàng cam, chạng vạng tím, đêm ánh trăng xanh. Trời đổi mượt.
- Đêm: đom đóm lập lòe (có con bay tới chỗ bé), sao + trăng lấp lánh trên mặt hồ, đèn nấm cạnh ổ + đèn lồng ở cửa chòi ăn tự sáng, tiếng dế thay tiếng chim, thỉnh thoảng có cú mèo.
- Từ 21:30 tới 6:00 bé buồn ngủ sớm hơn và ngủ một mạch tới sáng. Chạm bé là bé dậy chơi thêm 1 lúc.
- Màn đấu / săn quái luôn sáng như buổi trưa.
- Hòm thư ▸ **Ngày / đêm theo giờ máy** để tắt (vườn luôn sáng, bé không có giờ đi ngủ). Số liệu: GDD G23.

## Vườn rau (Phase 10.2)

- **3 luống đất** trước chòi ăn (góc trái phía trước). Chạm luống trống để gieo hạt, đất khô thì chạm để tưới, có ngôi sao lấp lánh là chín: chạm để hái.
- Cây lớn theo giờ thật, kể cả lúc tắt app. Đất ẩm (sẫm màu) thì lớn nhanh gấp đôi trong 2 giờ. Gieo xong là tưới luôn. Cây không héo: chín rồi thì chờ bạn hái.

  | Cây | Chín sau (tưới đều) | Bé ăn 1 quả |
  |---|---|---|
  | Dâu tây | 1,5 giờ (45 phút) | no + vui hẳn |
  | Cà rốt | 3 giờ (1,5 giờ) | no + năng lượng |
  | Việt quất | 6 giờ (3 giờ) | no + vui + tập Tốc độ |
  | Bí ngô | 10 giờ (5 giờ) | rất no + tập Sức mạnh |

- Quả hái được vào **giỏ quả** cạnh khay táo. Chạm giỏ, chọn quả: bé chạy ra ăn. Ăn quả tính là 1 lần cho ăn.
- Hạt giống: quầy hàng ▸ **Hạt giống**, hoặc nhặt khi săn quái. Lần đầu được tặng 3 hạt dâu tây + 2 hạt cà rốt.
- Bật thông báo thì có tin "Vườn rau có quả chín rồi!". Số liệu: GDD G24.

## Thám hiểm = Săn quái rừng (Phase 7.2, kiểu đánh mới từ Phase 9.2)

- **Vào:** chạm **biển gỗ "THÁM HIỂM"** ở góc sau bên trái vườn (cạnh lối đá dẫn ra khe hở hàng rào).
- **Bản đồ:** 5 vùng, mỗi vùng 5 màn, màn cuối là **trùm**:
  1. Đồi Cỏ Gió (hợp bé Baby) · 2. Rừng Nấm (Child) · 3. Hồ Sương (Teen) · 4. Núi Lửa (Adult) · 5. Hang Pha Lê (Ultimate).
  - Bé lớn lên mới đi xa được: quái mạnh ngang bé ở giai đoạn của vùng. Thẻ màn ghi độ khó so với bé (Dễ / Vừa / Khó / Rất khó), quái sẽ gặp, số đợt.
  - Màn sau mở khi thắng màn trước; hạ trùm thì mở vùng sau.
- **Cách đánh** (khác trận 1-1 PvP): màn ngang, bé đứng giữa đường, quái rừng kéo tới từ 2 bên theo 3 đợt.

  | Thao tác | Bé làm gì |
  |---|---|
  | Chạm | Lao tới đánh con được chạm (chạm chỗ trống = con gần nhất), đánh liên tục thành combo |
  | Vuốt lên | Đập đất: trúng cả đám quanh bé, đẩy lùi + choáng |
  | Vuốt xuống | Đỡ. Đỡ sát lúc quái đánh trúng = **phản đòn** (không mất máu, quái choáng) |
  | Vuốt ngang | Lướt né: lúc lướt không bị trúng đòn, lướt xuyên quái thì gây sát thương |
  | Nút TUYỆT CHIÊU (đầy Nộ) | Trúng mọi quái + choáng |

  - Quái: Chất Nhờn (nhảy cắn), Dơi Rừng (bay nhanh), Nấm Độc (phun bào tử từ xa), Heo Rừng (lao húc), Đá Rêu (đập đất). Quái sắp đánh thì hiện dấu **!** đỏ; đập đất có vòng đỏ trên đất; heo có vạch đỏ chỉ hướng lao.
  - Trùm: Vua Chất Nhờn, Chúa Nấm, Người Đá Sương, Heo Lửa Đầu Đàn, Người Đá Pha Lê. Trùm biết gọi thêm quái, còn nửa máu thì nổi giận.
- **Sao:** thắng = ★, còn ≥ 40% máu = ★★, còn ≥ 70% máu = ★★★. Coin khi thắng lần đầu + mỗi sao mới, thắng lại được ít hơn. Hạ quái = điểm Sức mạnh, phản đòn / né = điểm Tốc độ.
- Mỗi lần săn tốn năng lượng như đấu Bot. Bé mệt (năng lượng < 15), ốm hoặc đang ngủ thì không đi được.
- Hết trận: **Đánh tiếp** (màn sau), **Đánh lại**, **Về vườn**. Số liệu: GDD G21.
- **Rơi hạt giống (Phase 10.3):** quái bị hạ đôi khi rơi hạt cho vườn rau (vùng xa hạt quý hơn), trùm luôn rơi 2 hạt quý. Thua vẫn giữ hạt đã nhặt.
- **Sổ tay quái rừng:** bản đồ Thám hiểm ▸ Sổ tay. Đếm số con đã hạ từng loài; đủ mốc (1 / 10 / 30 / 100 con, trùm 1 / 3 / 10 / 25) thì được Coin. Loài chưa gặp hiện "???". Số liệu: GDD G25.

## Việc mỗi ngày (Phase 7.3)

- **Quà mở game:** mở game mỗi ngày nhận quà ở **hòm thư**. Chuỗi 7 ngày liên tiếp: 20 → 25 → 30 → 35 → 40 → 50 → **80 Coin**. Bỏ 1 ngày thì chuỗi về ngày 1.
- **3 nhiệm vụ mỗi ngày** (hòm thư ▸ Nhiệm vụ hôm nay): 1 việc chăm bé (có thể là "thu hoạch 1 luống rau"), 1 việc đấu (có thể là "hạ 8 quái rừng"), 1 việc khám phá / chăm. Xong việc nào thì bấm nhận Coin việc đó; xong cả 3 thêm 40 Coin.
- **Kho báu:** mỗi ngày có 2 **ụ đất có dấu X lấp lánh** trong vườn. Chạm vào, bé chạy tới đào: được Coin, đôi khi bánh thưởng hoặc nước tăng lực.
- **Cờ đỏ hòm thư** dựng lên (có dấu "!") là đang có quà chờ nhận.

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

## Thông báo nhắc chăm bé (Phase 4.5, thông báo cục bộ)

- Tắt / ẩn game thì máy hẹn thông báo đúng lúc bé **đói, buồn ngủ, lấm lem, mệt, bị ốm, ốm nặng**, lúc "còn 1 ngày để cứu bé", hoặc lúc **trứng nở được**. Mở game lại thì xóa hết.
- Tính bằng đúng luật nhu cầu + sức khỏe của game. Đêm 22:00–08:00 không báo, chuyện gần nhau gộp 1 thông báo.
- Chơi khoảng 90 giây thì game hỏi có bật không, đồng ý mới hiện hộp thoại xin quyền của máy. Bật / tắt lại ở **hòm thư**.
- Bảng **DEV ▸ Thông báo**: xem lịch sẽ hẹn nếu ẩn app ngay bây giờ, nút **Thử sau 10 giây**.

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

## Đồ chơi (Phase 8.3)

Quầy hàng ▸ **Đồ chơi cho bé...**, mua 1 lần là có mãi, nằm sẵn trong vườn:

| Món | Giá | Chơi thế nào |
|---|---|---|
| Bóng cao su | 60 | Vuốt để ném (vuốt nhanh = ném xa), bóng nảy + lăn thật. Thú nhặt mang về. Thú chán thì tự đẩy bóng chơi |
| Cần lông vũ | 90 | Giữ cần rồi kéo đi khắp vườn, thú đuổi theo vồ |
| Máy thổi bong bóng | 140 | Chạm = thổi bong bóng, thú nhảy đớp |

- Mỗi lượt chơi: thú vui lên + thêm điểm Tốc độ, tốn chút năng lượng + no. Thú ngủ / ốm / quá mệt thì không chơi (có dòng nhắc lý do).
- Có đồ chơi thì có ngày ra nhiệm vụ "Chơi với bé 3 lần". Số liệu: GDD G19, `PlayConfig`.

## Phụ kiện + trang trí vườn (Phase 5.2, có tác dụng từ Phase 8.4)

Cả 2 đều mua ở quầy hàng, mua 1 lần là có mãi. Đồ là của người chơi nên thú mất vẫn còn, thú mới nở vẫn đeo được. Không đổi chỉ số trận (đấu công bằng), nhưng mỗi món giúp chăm thú và thú dùng được (bảng dưới, chi tiết GDD G20):

| Món | Tác dụng | Thú dùng thế nào |
|---|---|---|
| Ghế gỗ | Năng lượng hao chậm 15% | Hơi mệt thì ra ghế nhảy lên ngồi nghỉ (+ năng lượng, + vui). Chạm ghế / dắt thú thả lên ghế cũng được |
| Dây đèn lồng | Ngủ hồi năng lượng nhanh hơn 25% | Tối (18:00–6:00) đèn sáng ấm. Chạm = đèn lấp lánh, thú ngước nhìn |
| Cối xay gió | Mỗi ngày 1 túi bánh (+20 no, +10 vui) | Chạm cối xay gió = thú chạy ra ăn |
| Vòm hoa | Vui hao chậm 15% | Bướm bay quanh vòm, thỉnh thoảng rủ thú đuổi. Chạm vòm = gọi bướm |
| Nơ · Mũ len · Khăn quàng (đang đeo) | Vui · năng lượng · no hao chậm 10% | |
| Vương miện (đang đeo) | Cả 4 nhu cầu hao chậm 5% | |

Tác dụng tính cả lúc tắt app. Quầy hàng ghi tác dụng ngay cạnh giá.

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
- **Âm thanh (Phase 8.1, GDD G17):** nhạc nền vườn + trận, gió + lá + chim, nước hồ, bước chân nhẹ (Phase 9.1), tắm, ăn, ngủ, gừ gừ khi vuốt, tiếng trận, "ting-ting" khi nhận Coin, "cạch-ting" khi tiêu. Tất cả tạo bằng code, không có file âm thanh. Hòm thư bật / tắt âm thanh và nhạc nền riêng. Quầy hàng và hình nộm nảy nhẹ khi chạm.
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
  - Thám hiểm: xem tổng sao + màn kế; nút **Bản đồ**, **Mở hết (1 sao)**, **Xóa tiến độ**.
  - Việc mỗi ngày: xem ngày, chuỗi, nhiệm vụ, kho báu còn lại; nút **Sang ngày mới**, **Xong hết nhiệm vụ**.
  - Kinh tế: xem Coin, lượt quảng cáo còn lại, tổng đã nhận / đã tiêu. Nút **+100 Coin** / **-100 Coin** / **Trả lượt QC** / **Mở quầy hàng** / **QC: hết hàng** (giả lập không có quảng cáo).
  - Rank: xem bậc / Elo / mùa. Nút **Thắng PvP giả** / **Thua PvP giả** (đổi Elo như thắng / thua 1 người ngang cơ; thắng +30 Coin). Nút **Đấu trường** mở thẻ đấu trường ngay.
  - Phụ kiện / trang trí: xem đang đeo gì, có mấy món. Nút **Có hết đồ (test)** (có hết, không trừ Coin) và **Đổi phụ kiện** (lần lượt từng món).
  - Tua giờ +1 / +6 / +24.
  - Giờ vườn (ngày / đêm): xem giờ, buổi, độ tối; nút **Giờ thật** / **6h** / **12h** / **17,75h** / **19h** / **22h** để ép giờ.
  - Vườn rau: xem từng luống (% lớn, đất ướt), số hạt, số quả trong giỏ; nút **Chín hết** / **+5 hạt mỗi loại**.
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
| `Assets/PawFit/Scripts/Core/Audio` | `SoundSynth` (C# thuần): tạo nhạc nền + mọi tiếng bằng code |
| `Assets/PawFit/Scripts/Core/Unity` | Tiện ích Unity dùng chung cho View: tạo vật liệu, khối cơ bản, đổi tọa độ, phát âm thanh `Sfx` |
| `Assets/PawFit/Scripts/Pet` | DNA, nhu cầu + tác dụng của đồ đã mua `CarePerks`, PetService, sức khỏe `Health/`, trứng + bia mộ `Lifecycle/`, tiến hóa + chỉ số `Evolution/` (C# thuần) · `View/`: `IPetAvatar`, thú Gen 1 `Gen1PetAvatar`, thú tạm `PlaceholderPetAvatar`, hiệu ứng `PetMoodFx`, phụ kiện `AccessoryView` |
| `Assets/PawFit/Scripts/Garden` | Bố cục vườn, bộ não thú `PetBrain`, biểu cảm `PetExpressionService`, luật chăm sóc, phân loại chạm/kéo, vuốt ve hay dắt `StrokeTracker`, trò chơi `PlayService`, việc mỗi ngày `DailyService`, góc camera `CameraOrbit`, ngày / đêm `DayCycle`, vườn rau `FarmService` (C# thuần) · `View/`: dựng vườn (có quầy hàng), đồ trang trí `DecorBuilder` + thú dùng đồ trang trí `DecorLife`, đồ chơi `ToyViews`, ngày / đêm `DayNightView`, vườn rau `FarmView`, camera, input, NavMesh, bia mộ, trứng, thẻ / bảng UI `GardenUi` |
| `Assets/PawFit/Scripts/Battle` | Đấu (C# thuần): nhận dạng vuốt, mô phỏng trận theo bước cố định có seed, Bot 3 mức, phiên Huấn luyện / Đấu Bot, Thám hiểm `AdventureService` (có sổ tay quái rừng), Săn quái `HuntSimulation` + `HuntSession` + `HuntGestures`, rơi hạt giống `HuntLoot` · `View/`: màn đấu ngang, hình nộm, HUD, bóng mờ nét vuốt, sân theo vùng `ArenaBuilder`, màn Săn quái `HuntScene` + quái rừng `MonsterView` |
| `Assets/PawFit/Scripts/Economy` | Kinh tế (C# thuần): ví Coin + quảng cáo `EconomyService`, quầy hàng `ShopService`, phụ kiện + trang trí `CosmeticsService`, cổng quảng cáo `IAdService` + bản giả lập `MockAdService` |
| `Assets/PawFit/Scripts/Online` | Phase 4 phần không cần tài khoản (C# thuần): rank Elo + bậc + mùa giải `RankService`, luật ghép trận + hàng chờ `Matchmaker`, người chơi ảo `BotPlayers`, cổng mạng `INetworkManager` + bản giả lập `LocalNetworkManager` |
| `Assets/PawFit/Scripts/App` | GameBootstrap (nối service, tự chạy khi Play), SceneFlow (chuyển màn, quầy hàng, quảng cáo, phụ kiện, trang trí), tiếng Coin `UiSounds`, Debug overlay |
| `Assets/PawFit/Resources/Pets/Gen1` | Model thú Gen 1 (FBX bản 2: thân LOD0 / LOD1 + blendshape + xương + mọi kiểu bộ phận) + file mốc neo JSON |
| `Assets/PawFit/Resources/Shaders` | Shader URP `PawFit/Gen1Fur` (lông theo vertex color, bẩn) + `PawFit/Gen1Eye` (mắt vẽ bằng toán) |
| `Assets/PawFit/Editor` | Menu Setup Project · tự đặt thông số import FBX Gen 1 |
| `Tools/Blender` | Bộ dựng thú Gen 1 bằng script + xuất / kiểm tra FBX |
| `Tools/LogicTests` · `Tools/UnityCompileCheck` | Test logic / compile thử code Unity bằng dotnet |
