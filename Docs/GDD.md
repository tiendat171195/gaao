# PawFit — GDD ngắn

Thiết kế gốc do bạn chốt + các con số / giả định tôi điền thêm (G1–G20). Mọi số nằm trong config, chỉnh được.

## 1. Lõi

- **1 người = 1 thú, chết vĩnh viễn.** Bỏ bê 3 ngày thì bệnh, 7 ngày không chữa thì chết. Khi thú chết có Khu tưởng niệm; 24 giờ sau nhận trứng mới, trứng thừa hưởng 1 trait của thú cũ.
- **Vườn (màn dọc):** 1 vườn cố định nhưng rộng, không có nút, không có thanh chỉ số.
  - Trạng thái hiện bằng hình ảnh.
  - Bố trí: chòi ăn bên trái, hồ tắm bên phải, sân tập phía sau, ổ ngủ ở giữa, tưởng niệm ở góc.
  - Điều khiển: chạm đất thì thú chạy tới; kéo thú để dắt; xoa qua lại trên người thú = vuốt ve (G18); chạm thú thì thú phản ứng; kéo đồ ăn thả cho thú để cho ăn; kéo thú ra hồ để tắm.
  - Đồ chơi để chơi với bé (G19); đồ trang trí + phụ kiện có tác dụng, bé dùng được (G20).
- **Thú:** 1 base mesh + DNA, sinh lúc chạy bằng blendshape + material (+ shader lông ở Gen 2). Style ngầu + cute, không cartoon phẳng.
- **Tiến hóa:** Baby (ngày 0) → Child (3) → Teen (7) → Adult (14) → Ultimate (30, nếu đủ điều kiện).
  - Đầu vào: CareScore 7 ngày + TrainingVector (Sức mạnh / Tốc độ / Chăm sóc / Bóng tối) + seed và tính cách ẩn (10–20%).
  - MVP: 5 dạng Adult — Lửa, Nước, Ánh Sáng, Thép, Bóng Tối.
- **Chỉ số:** HP, ATK, DEF, SPD, Crit. Lực chiến = tổng có trọng số. SPD quyết định ai đánh trước.
- **Chiến đấu:** chỉ vuốt, real-time, không nút.
  - Vuốt ngang = đánh nhanh; vuốt lên = đánh mạnh / hất tung; vuốt xuống = đỡ / phản đòn; vuốt V = chiêu riêng; vuốt Z = Tuyệt chiêu (Ultimate, cần đầy Nộ).
  - Hồi chiêu 0,6 giây, vuốt sớm thì được giữ lại và ra đòn ngay khi rảnh; vuốt sai bị hở 0,5 giây; trúng liên tiếp thành combo (Phase 7.1, G9d).
  - Mode Huấn luyện có bóng mờ gesture, slow-mo, cho vuốt lại, vuốt đúng 10 lần thì thuộc chiêu.
  - PvP 90 giây, màn hình chỉ có 2 thú + vạch HP mờ.
- **Ranking:** ghép đối thủ theo Elo ±100 và Lực chiến ±10%. Bậc Bronze → Silver → Gold → Diamond → Master. Mùa giải 30 ngày.
- **Kinh tế:** Coin; không nạp tiền. Quảng cáo tự nguyện, tối đa 5 lần/ngày (`IAdService` + `MockAdService`).

## 2. Số liệu & giả định (G1–G20)

- **G1 · Nhu cầu ẩn:** No bụng, Sạch, Năng lượng, Vui (0–100). Đang cài đặt ở `NeedsConfig`: no −4/h, sạch −3/h, năng lượng −4/h khi thức và +15/h khi ngủ, vui −3.5/h, lúc nở = 80.
- **G1b · Biểu cảm (Phase 1.3, `ExpressionConfig`):**
  - Đói khi no < 35: cứ khoảng 9 giây (±30%) nhìn chòi ăn 2,2 giây, kêu ở giây 0,8.
  - Bẩn khi sạch < 40: 2 ruồi, sạch < 15 thì 4 ruồi. Lông lấm lem từ 30% đến 100%.
  - Buồn ngủ khi năng lượng < 30: mắt mở 72% → 42%, ngáp khoảng 11 giây một lần.
  - Đang ngủ: mắt nhắm hẳn.
- **G2 · Bỏ bê:** đồng hồ bỏ bê chỉ reset khi **chăm đủ**, tức cả 4 nhu cầu ≥ 60 cùng lúc.
- **G3 · Bệnh:**
  - Khỏe: 0–24h.
  - Mệt: 24–72h, −30% chỉ số.
  - Ốm: 72–120h, −40%, cấm Rank và tập luyện, chỉ chăm nhẹ.
  - Nguy kịch: 120–168h, −60%.
  - Chết: 168h.
- **G4 · Chữa** (đã cài ở Phase 2.1, `HealthRules` + `HealthConfig`):
  - Mệt: chăm đủ là hết.
  - Ốm / Nguy kịch: dùng thuốc (60 / 150 Coin), hoặc chăm kỹ (cháo + chăn + lau người, ít nhất 1 lượt mỗi 12h). Cả 2 cách khỏi sau 24h / 48h.
  - Đang hồi phục mà quá 24h không chăm thì tái phát. Thú ốm không chịu xuống hồ tắm.
- **G4b · Cách cài cụ thể (Phase 2.1):**
  - 1 lượt chăm kỹ = cháo (cho ăn lúc ốm) + chăn (ngủ trong ổ lúc ốm) + lau người (chạm vào thú lúc ốm), cả 3 trong vòng 12h.
  - Lượt đầu (hoặc thuốc) bắt đầu hồi phục. Mỗi lượt sau giữ hồi phục thêm 12h. Đã uống thuốc thì chăm thường (cho ăn, lau...) cũng giữ được 12h.
  - Hồi phục chỉ tiến triển trong khoảng còn được giữ. Đang hồi phục thì bệnh không nặng thêm.
  - Ốm mà chưa chữa thì chăm đủ chỉ làm bệnh dừng nặng thêm, không tự khỏi.
  - Khỏi bệnh: đồng hồ bỏ bê về 0.
  - Thú ốm đi chậm (mệt ×0.85, ốm ×0.65, nguy kịch ×0.45), không chạy, mệt chút là về ổ nằm.
- **G5 · Chết & trứng:** bia mộ ghi tên, tuổi, rank cao nhất, mã DNA. Sau 24h có trứng thừa hưởng 1 gene hình dạng; chạm trứng để nở và đặt tên.
  - Đã cài: trứng chọn ngẫu nhiên 1 trong 8 nhóm trait: dạng mắt, màu mắt, màu lông, họa tiết, tai, sừng, đuôi, tỉ lệ thân. Các gene còn lại ngẫu nhiên. Giữ nguyên `Gen`.
  - Tên tối đa 16 ký tự; để trống thì là "Bé".
- **G6 · Mã DNA:** `PF-XXXX-XXXX-XXXX` = 55 bit gene → 11 ký tự Base32 + 1 ký tự kiểm tra (đã cài: `DnaCodec`). Chỉ chứa gene hình dạng và trường `Gen`; seed + tính cách ẩn không có trong mã.
- **G7 · Tiến hóa:**
  - Lửa: Sức mạnh trội.
  - Nước: Tốc độ trội.
  - Ánh Sáng: Chăm sóc trội và CareScore ≥ 70.
  - Bóng Tối: Bóng tối trội, hoặc CareScore < 40.
  - Thép: tập cân bằng.
  - Cộng nhiễu 10–20%. Từ Teen, DNA trượt dần về dạng đang nghiêng. Ultimate cần CareScore ≥ 80 + thuộc đủ 4 chiêu + 7 ngày không bệnh.
  - Đã cài ở Phase 2.2 (`EvolutionRules`, `EvolutionConfig`):
    - CareScore = trung bình (4 nhu cầu / 4) từng giờ của 168 giờ gần nhất; giờ đang ốm ×0.5.
    - Nguồn điểm: Chăm sóc (cho ăn 2, tắm 3, vuốt ve 0.5, lau 1, ngủ ổ 1); Sức mạnh (sân tập 6 / buổi); Tốc độ (0.08 / mét dắt chạy); Bóng tối (0.4 / giờ bị bỏ bê, +1 / giờ đang ốm). Mỗi hướng chủ động tối đa 30 / ngày tuổi.
    - Nhiễu: seed ×(1 ± 0.1) mỗi hướng; tính cách hợp hướng +15% (Fiery → Sức mạnh, Playful → Tốc độ, Calm → Chăm sóc, Moody → Bóng tối, các tính cách khác chia đôi).
    - Cân bằng = hướng cao nhất ≤ 32% tổng. Chăm sóc trội mà CareScore < 70 thì theo hướng mạnh thứ 2.
    - DNA trượt (từ Teen, mỗi ngày): màu lông +3 nấc về màu của dạng, độ đậm / sáng / bụng / chân ±1, luân phiên 1 trong 3 gene tỉ lệ ±1. Không đổi kiểu tai / đuôi / sừng.
- **G8 · Chỉ số:**
  - Baby: HP 100 / ATK 10 / DEF 10 / SPD 10 / Crit 5%.
  - Hệ số theo giai đoạn: ×1 / 1.5 / 2.2 / 3.2 / 4.2.
  - Theo dạng: Lửa +25% ATK, Nước +25% SPD, Ánh Sáng +25% HP, Thép +25% DEF, Bóng Tối +10 điểm Crit.
  - Lực chiến = 0.25·HP + 2·ATK + 1.5·DEF + 1.5·SPD + 10·Crit%.
- **G9 · Chiến đấu:**
  - Ngang = chiêu hệ 1 · Lên = chiêu hệ 2 · V = chiêu riêng · Z = Tuyệt chiêu (cần đầy Nộ) · Xuống = đỡ (≤ 0.2s trước đòn = phản đòn).
  - Damage = ATK × hệ số chiêu × hệ số vuốt (0.7–1.3) × hệ số combo (1–1,4) × 100/(100+DEF); crit ×1.5.
  - Máu trong trận = HP × 2,5 (chỉ trong trận). Hết 90 giây: bên còn nhiều %HP hơn thắng.
- **G9b · Cách cài cụ thể (Phase 3.1, `BattleConfig` / `SwipeConfig` / `BotConfig`):**
  - **Nhận dạng vuốt** (nới ở Phase 7.1 cho hợp ngón tay thật):
    - Nét dài ≥ 45 px (ở 160 dpi, ~7 mm), trong ≤ 1,5 giây. Ngắn / chậm hơn thì chỉ nhắc, **không phạt**.
    - Thẳng (độ thẳng ≥ 0,78, tức cả cung cong ~130° của ngón cái) thì xét hướng trội: ngang / lên / xuống.
    - Móc nhỏ lúc chạm xuống / nhấc tay (≤ 25% quãng đường) bị bỏ, rồi xét phần chính.
    - V = xuống tới điểm thấp nhất rồi lên: mỗi cạnh ≥ 20% quãng đường, 2 đầu cao hơn đáy ≥ 30% cỡ nét. Đáy tròn hay lệch (kiểu dấu ✓) vẫn được; V nông quá tính là vuốt ngang.
    - Z = ngang → chéo ngược xuống → ngang, vẽ được cả 2 chiều. Thử 3 mức làm gọn nét nên góc bo tròn vẫn nhận.
    - Chỉ nét nguệch ngoạc / vòng tròn mới là "vuốt sai" (bị hở).
    - Hệ số vuốt 0,7–1,3 = 60% tốc độ + 40% độ đúng hình.
  - **4 chiêu (hệ số damage / thời gian vung đòn):**

    | Chiêu | Vuốt | Hệ số | Vung đòn |
    |---|---|---|---|
    | Nhanh | Ngang | ×1 | 0,15 s |
    | Mạnh | Lên | ×1,6 | 0,35 s |
    | Riêng | V | ×2,2 | 0,45 s |
    | Tuyệt chiêu | Z | ×3,2 | 0,7 s, cần đầy Nộ |

  - Chiêu chưa thuộc (Mode Huấn luyện) vẫn dùng được nhưng ×0,8.
  - **Đánh:**
    - Hồi chiêu 0,6 s tính từ lúc ra đòn.
    - **Bộ đệm vuốt:** vuốt lúc còn bận (hồi chiêu / đang vung đòn / đang hở) mà chỉ còn ≤ 0,35 s là rảnh thì nét được giữ lại, rảnh ra là ra đòn ngay. Nét sau thay nét trước. Vuốt sai lúc bận (kể cả lúc hồi chiêu) thì bỏ qua, không phạt.
    - Vuốt sai lúc rảnh thì hở 0,5 s: không đánh, không đỡ được.
    - Đòn Mạnh / Riêng trúng thì cắt đòn đang vung của đối thủ (hất tung). Đòn Nhanh không cắt. **Tuyệt chiêu không bị cắt** (vẫn bị đỡ / phản đòn).
  - **Combo:** đòn trúng cách đòn trúng trước ≤ 1,2 s thì nối combo, mỗi bậc +10% damage, tối đa +40% (từ đòn thứ 5). Bị trúng đòn / bị đỡ / bị phản đòn / để quá giờ thì mất combo. Phản đòn tính là 1 đòn trúng.
  - **Nộ (0–100):** đánh trúng +12, bị trúng / bị phản đòn +8, đỡ được +8, phản đòn +25. Đầy thì vuốt Z ra Tuyệt chiêu, dùng hết Nộ. Vuốt Z lúc chưa đầy: không ra đòn, không phạt, vạch Nộ chớp đỏ.
  - **Đỡ:**
    - Thế đỡ kéo dài 0,45 s, chỉ chịu 20% damage. Đang hồi chiêu đánh vẫn đỡ được, nhưng giữa 2 lần đỡ phải chờ 0,6 s.
    - Đỡ ≤ 0,2 s trước khi đòn trúng thì phản đòn: bên tấn công ăn ATK × 0,8 của bên đỡ và bị hở 1 s.
  - **SPD:** bên chậm hơn phải chờ 0,3 s mới ra được đòn đầu. 2 đòn trúng cùng lúc thì bên nhanh tính trước.
  - **Bot 3 mức (3 hình nộm)** khác nhau ở:
    - Tốc độ phản ứng: 0,45 / 0,3 / 0,16 s.
    - Xác suất đỡ: 15 / 40 / 70%. Mức Khó canh đỡ sát đòn để phản đòn.
    - Nhịp đánh (nghỉ 1–2 / 0,6–1,2 / 0,3–0,7 s), độ đẹp nét vuốt, tỉ lệ vuốt sai, chiêu hay dùng.
    - Đầy Nộ thì xả Tuyệt chiêu với xác suất 0 / 40 / 80% mỗi lần ra đòn (hình nộm Dễ không biết dùng).
  - Mô phỏng chạy theo bước cố định 1/30 s, random có seed, nên cùng đầu vào thì cùng kết quả (dùng lại được cho PvP).
- **G9d · Phase 7.1 — cảm giác đánh (bạn góp ý: "vuốt đúng nhưng thú cứ đứng yên, không ra chiêu ngay nên thấy không đã, gameplay hơi chậm"):**
  - Nguyên nhân tìm được:
    1. **Lỗi màn đấu:** sự kiện của chính nét vừa vuốt (ra đòn / đỡ / vuốt sai) bị xóa trước khi vẽ. Thú không lấy đà, không hiện khiên, không báo vuốt sai; chỉ giật lúc đòn trúng (0,25–0,9 s sau). Đã sửa.
    2. Vuốt trong lúc hồi chiêu bị bỏ qua mà không báo gì → thêm bộ đệm 0,35 s.
    3. Bộ nhận dạng quá khắt khe với nét cong / có móc → nét đúng thành "vuốt sai" + bị hở 1 s → đã nới (G9b).
    4. Nhịp chậm: vung đòn 0,25–0,9 s, hồi chiêu 0,8 s → còn 0,15–0,7 s và 0,6 s.
  - Hình: vuốt xong là thú chuyển động ngay và tới sát đối thủ đúng lúc đòn trúng:
    - Nhanh: phóng thẳng. Mạnh: nhún rồi bật nhảy. Riêng: xoay 1 vòng. Tuyệt chiêu: bay lên tụ lực rồi bổ xuống, chữ "TUYỆT CHIÊU!" + chậm lại 0,3 s.
    - Vệt chém theo màu chiêu, chữ "x3 COMBO", vạch Nộ 2 bên (đầy thì nhấp nháy + vầng cam dưới chân thú), vạch hồi chiêu dưới vạch của mình.
    - Vết ngón tay đổi màu lúc nhấc tay: vàng = nhận chiêu, đỏ = vuốt sai, xám = không tính / đang bận.
  - Trận dài vừa đủ: máu trong trận ×2,5. Đo bot đấu bot (400 trận mỗi cặp, chỉ số ngang nhau):

    | Luật | Thường vs Thường | Dễ vs Khó | Adult Thường vs Khó |
    |---|---|---|---|
    | Cũ | 12,5 s · 17 đòn trúng | 8,5 s | 16,8 s |
    | Mới, máu ×1 | 9,9 s · 14 đòn · 1,9 Tuyệt chiêu | 5,2 s | 12,3 s |
    | **Mới, máu ×2,5 (chọn)** | **24,6 s · 35 đòn · ~5 Tuyệt chiêu** | **11,6 s** | **29,6 s** |

    Trận cũ hết trong 8–17 giây: chưa kịp nối combo, chưa kịp dùng Tuyệt chiêu. Với máu ×2,5, trận ngang sức ~25–30 giây, mỗi bên dùng được 2–3 Tuyệt chiêu, vẫn còn xa mốc 90 giây.
    Đo lại sau mỗi lần chỉnh số: `cd Tools/BattleBalance && dotnet run -c Release`.
  - Luật trận đổi nên tăng `NetProto.Version` lên 2: bản game cũ được báo "hãy cập nhật".
- **G9c · Sân tập (Phase 3.2, `BattleSessionConfig`):**
  - Huấn luyện:
    - Bài học = chiêu chưa thuộc đầu tiên, theo thứ tự ngang → lên → V → Z.
    - Mỗi lần thú thật sự ra chiêu đang học thì +1 (kể cả nét lấy từ bộ đệm); đủ 10 thì thuộc.
    - Vuốt sai không bị hở. Tuyệt chiêu không cần Nộ (để học vuốt Z), thanh Nộ không hiện.
    - Tốc độ trận ×0,75; hình nộm đang vung đòn thì ×0,3 (slow-mo).
    - Hình nộm đánh nhẹ (×0,5), 2 bên nhiều máu (không ai gục), tối đa 10 phút.
  - Đấu Bot: hình nộm Dễ / Thường / Khó = chỉ số thú ×0,7 / ×1 / ×1,25, Bot mức tương ứng, 90 giây.
  - Điểm tập mỗi đòn trúng:

    | Đòn | Điểm |
    |---|---|
    | Ngang | Tốc độ +1 |
    | Lên | Sức mạnh +1,5 |
    | V | Sức mạnh +1, Tốc độ +1 |
    | Z | Sức mạnh +2, Tốc độ +1,5 |
    | Phản đòn | Tốc độ +1,5 |

  - Thắng Dễ / Thường / Khó: Sức mạnh +3 / +5 / +8, Tốc độ +2 / +3 / +5. Vẫn theo giới hạn 30 điểm / hướng / ngày.
  - Tốn sức: Huấn luyện năng lượng −6, no −4; Đấu năng lượng −10, no −6.
  - Ốm / đang ngủ: không vào được.
- **G10 · Rank:** Elo khởi đầu 1000, K = 32. Bronze < 1100 ≤ Silver < 1300 ≤ Gold < 1500 ≤ Diamond < 1700 ≤ Master. Hết mùa reset mềm về 1000 + (Elo − 1000)/2. Tìm 30 giây chưa có trận thì nới khoảng; 60 giây thì gợi ý đấu Bot.
- **G10b · Cách cài cụ thể (logic Phase 4, chạy offline, `RankService` + `Matchmaker` + `LocalNetworkManager`):**
  - Elo làm tròn tới số nguyên. Thắng / thua người ngang cơ: ±16.
  - Bậc cao nhất từng đạt của từng con thú ghi lên bia mộ. Elo là của người chơi, thú mới giữ nguyên Elo.
  - Thắng PvP +30 Coin. Ốm / đang ngủ không đấu được.
  - Mùa 1 bắt đầu 01/01/2026 (UTC), mỗi mùa 30 ngày.
    - Sang mùa được tính khi mở game: vắng nhiều mùa thì reset mềm nhiều lần.
    - Đồng hồ chỉnh lùi không đổi gì.
  - Ghép trận:
    - Lúc đầu Elo ±100 + lực chiến ±10% (so với bên mạnh hơn), tính theo người chờ lâu hơn.
    - Chờ 30 giây: nới ra ±200 / ±20%. Hai số này tôi tự đặt, GDD chỉ ghi "nới khoảng".
    - Chờ 60 giây: gợi ý đấu Bot nhưng vẫn tìm tiếp.
    - Người chờ lâu nhất được ghép trước, với người gần Elo nhất.
  - Mạng giả lập:
    - "Người chơi giả" vào hàng chờ cách nhau 2–9 giây, Elo ±250, lực chiến ±30% quanh mình.
    - Ghép trúng người giả thì máy tự điều khiển đối thủ bằng Bot.
- **G10c · Người chơi ảo cho PvP (bạn chốt: "tạo nhiều bot như người chơi thật" để không thiếu người tìm trận):**
  - 600 người chơi ảo cố định theo số hiệu. Mỗi người luôn có cùng biệt danh, cùng thú (DNA + tên), cùng lối đánh, 35% có đeo phụ kiện.
  - Khi ghép trận:
    - Elo của họ đặt quanh Elo của bạn (±180), lực chiến ±20%.
    - Thú cùng giai đoạn tiến hóa với thú bạn. Dạng Adult hợp lối đánh: Hăng → Lửa / Bóng Tối, Thủ → Thép, Kỹ thuật → Nước, Cân bằng → Ánh Sáng.
    - Chỉ số tính đúng công thức G8 rồi chỉnh cho khớp lực chiến.
    - Số chiêu đã thuộc theo giai đoạn: Ultimate thuộc đủ 4.
  - Cách đánh: `BotConfig.Persona(tay nghề, lối đánh)`.
    - Tay nghề = (Elo − 850) / 900, ±0,08 tùy người. 0 ≈ hình nộm Dễ, 0,5 ≈ Thường, 1 ≈ Khó.
    - Hăng: đánh dồn, ít đỡ. Thủ: đánh thưa, hay đỡ, sớm biết canh phản đòn. Kỹ thuật: hay ra chiêu riêng / Ultimate.
  - Mật độ: cứ 1,5–6 giây có 1 người chơi ảo vào hàng chờ, không gặp lại 12 người gần nhất.
    - Đo 30 lần tìm: ngắn nhất 1,8 giây, ở giữa 10,5 giây, lâu nhất 30 giây.
  - Khi có máy chủ riêng: tìm người thật trước, chờ 6 giây chưa có ai mới thả người chơi ảo vào (G10d). Elo / Coin tính như trận thật.
  - Lưu ý phát hành: nhiều game có làm vậy. Nên ghi trong điều khoản / mô tả app rằng khi ít người, trận có thể gặp đối thủ do máy điều khiển.
- **G10d · Máy chủ riêng (bạn chốt: tự host thay Firebase + Photon, Firebase chỉ dùng FCM; `Server/README.md`):**
  - Tài khoản khách:
    - Mở game là có, không cần email / mật khẩu.
    - Máy chủ đặt sẵn biệt danh cùng kiểu với người chơi ảo, đổi ở hòm thư (2–16 ký tự).
    - Đăng nhập Google / Apple: làm sau.
  - Mã chuyển máy:
    - Dạng `XXXX-XXXX-XXXX`, bỏ chữ dễ nhầm I / O / 0 / 1. Dùng 1 lần, hạn 7 ngày.
    - Máy mới nhập mã thì nhận tài khoản + thú: dữ liệu tải về, đóng game mở lại mới thay.
    - Máy cũ mất quyền dùng tài khoản, lần nối sau tự thành tài khoản mới.
  - Giờ máy chủ:
    - Đồng bộ lúc mở game, lúc quay lại game và mỗi 10 phút.
    - Lệch dưới 1 giây bỏ qua. Mất mạng thì dùng giờ máy + độ lệch lần trước.
  - Sao lưu: tải file game lên mỗi 2 phút nếu có đổi + khi ẩn app. Máy đang giữ tài khoản luôn là bản đúng.
  - Ghép trận:
    - Người thật được ghép với nhau trước, đúng luật G10b.
    - Chờ 6 giây chưa có ai thì người chơi ảo (G10c) bắt đầu tới hàng của người đó. Người chơi ảo đánh ngay trên máy chủ.
    - Máy người chơi không phân biệt được đối thủ ảo: mã người chơi cùng dạng, không có cờ "ảo".
  - Trận PvP:
    - Máy chủ chạy trận (30 bước / giây) và quyết định kết quả.
    - Nét vuốt gửi lên máy chủ. Máy chủ chốt bước nào thì cả 2 máy áp ở đúng bước đó.
    - Cùng seed + cùng chỉ số tới từng bit nên 2 máy ra y hệt máy chủ (test kiểm cả lúc 2 bên SPD bằng nhau).
    - Máy chạy chậm hơn máy chủ 2 bước làm đệm, mạng nghẽn thì chạy dồn cho kịp.
    - Trễ khoảng 0,1–0,2 giây. Đòn Mạnh / Riêng / Tuyệt chiêu vung 0,35–0,7 giây nên vẫn kịp đỡ; đòn Nhanh (0,15 giây) gần như không đỡ kịp: đòn Nhanh để rỉa máu + nối combo.
    - Vuốt xong thú trên máy mình nhún lấy đà ngay, trong lúc chờ máy chủ chốt nét (che cảm giác trễ mạng).
    - Thoát app / mất mạng giữa trận, hoặc rời trận lúc đang hiện thẻ đối thủ = bỏ cuộc = thua (vẫn trừ Elo).
  - Chống gian lận (giai đoạn thử nghiệm):
    - Máy chủ tính Elo + Coin thắng PvP, chặn chỉ số thú vượt trần của giai đoạn, tự tính lực chiến.
    - Coin / đồ / thú vẫn do máy tính, máy chủ chỉ sao lưu.
  - Không cấu hình máy chủ (file `Resources/PawFitServer.txt` trống): chơi offline như trước (mạng giả lập, Elo tính trên máy).
- **G11 · Kinh tế:**
  - Thức ăn cơ bản miễn phí, khởi đầu 200 Coin, thắng PvP +30, quảng cáo +20 Coin / +20 năng lượng.
  - Không có HUD: shop = quầy hàng trên map; đấu Bot = 3 hình nộm; PvP = cổng đấu trường; cài đặt = hòm thư ở cổng.
- **G11b · Cách cài cụ thể (Phase 5.1, `EconomyConfig` + `EconomyService` + `ShopService`):**
  - Ví nằm trong file lưu (bản 3) và là của người chơi: thú mất vẫn còn. File cũ chưa có ví thì được cấp 200 Coin. Ví tối đa 999.999 Coin.
  - Quầy hàng là khu `Shop` ở phía trước bên phải vườn, có lối đá dẫn tới. Mua xong thú dùng ngay, không có túi đồ:

    | Món | Giá | Tác dụng | Không bán khi |
    |---|---|---|---|
    | Thuốc | 60 (Ốm) / 150 (Nguy kịch) | Bắt đầu hồi phục theo G4 | Thú khỏe hoặc chỉ Mệt · đã uống trong đợt hồi phục này |
    | Bánh thưởng | 25 | No +15, vui +25 | Đang ngủ · no ≥ 92 |
    | Nước tăng lực | 30 | Năng lượng +30 | Đang ngủ · năng lượng ≥ 92 |

  - Quảng cáo: +20 Coin, hoặc +20 năng lượng (không mời khi năng lượng ≥ 90).
    - 5 lượt / ngày tính chung 2 loại, reset lúc 0 giờ theo giờ trên máy. Chỉnh đồng hồ lùi không được thêm lượt. Phase 4.1 chuyển sang giờ server.
    - Chỉ xem hết mới có thưởng và mới tính lượt.
    - Bản dev dùng `MockAdService` (3 giây). Phase 5.3 thay bằng SDK thật qua `IAdService`.
  - Thắng hình nộm Dễ / Thường / Khó: +5 / +10 / +15 Coin. Bỏ cuộc và Huấn luyện không có Coin.
  - Số tôi điền thêm: thưởng Coin khi thắng Bot (để có nguồn Coin trước khi có PvP), giá + tác dụng của bánh và nước.
- **G12 · Phụ kiện + trang trí (Phase 5.2, `CosmeticsService`, giá ở `EconomyConfig`):** phần này tôi tự thiết kế vì GDD gốc chưa có, bạn sửa thoải mái.
  - Không đổi chỉ số trận (PvP công bằng). Lúc đầu chỉ để đẹp; từ Phase 8.4 mỗi món giúp việc chăm bé + bé dùng được (G20).
  - Mua 1 lần là có mãi. Đồ là của người chơi: thú mất vẫn còn, thú mới nở vẫn đeo. File lưu bản 4 (`GameSave.Cosmetics`).
  - Phụ kiện gắn vào tâm đầu (`IPetAvatar.HeadAnchor`), cỡ theo bán kính đầu, nên vừa cả Baby lẫn Adult. Gen 2 chỉ cần cài lại `HeadAnchor`.

    | Phụ kiện | Giá |
    |---|---|
    | Nơ | 80 |
    | Mũ len | 120 |
    | Khăn quàng | 100 |
    | Vương miện | 250 |

    - Mỗi lúc đeo 1 món. Mua xong tự đeo; chạm món đã có để đeo / tháo.
  - Trang trí có chỗ cố định trong vườn và là vật cản (NavMesh nướng lại khi mua):

    | Trang trí | Giá | Chỗ |
    |---|---|---|
    | Dây đèn lồng | 150 | Giữa hồ và sân tập |
    | Ghế gỗ | 120 | Trước hồ |
    | Cối xay gió | 300 | Sau chòi ăn |
    | Vòm hoa | 200 | Phía trước bên trái |

  - Mua hết khoảng 1.320 Coin, tức khoảng 2 tuần xem quảng cáo + thắng Bot.
- **G13 · Hòm thư = cài đặt (đã cài):** hòm thư đứng cạnh cổng đấu trường. Chạm vào để bật / tắt âm thanh, xem + copy mã DNA, xem phiên bản game.
  - Có máy chủ riêng: thêm biệt danh, mã chuyển máy (máy cũ), nhập mã chuyển máy (máy mới).
  - Trên điện thoại: thêm bật / tắt thông báo nhắc chăm bé (G14).
- **G14 · Thông báo nhắc chăm thú (thông báo cục bộ, không cần máy chủ; tôi tự đặt số, bạn sửa ở `NotificationConfig`):**
  - Ẩn / tắt app thì hẹn lịch, mở lại app thì xóa hết (đang ở trong game rồi).
  - Cách tính: mô phỏng tới trước 8 ngày bằng đúng luật nhu cầu + sức khỏe của game (bản sao dữ liệu).
  - Lấy lần đầu mỗi chuyện xảy ra:
    - Đói / buồn ngủ / lấm lem: đúng ngưỡng biểu cảm ở vườn (no < 35, năng lượng < 30 khi thức, sạch < 40).
    - Mệt (bỏ bê 24 giờ), ốm (72 giờ), ốm lại, ốm nặng (120 giờ), "còn 1 ngày để cứu bé" (24 giờ trước lúc mất), trứng nở được.
    - Chuyện đã có sẵn lúc rời game thì không báo lại. Thú đang ngủ thì không báo buồn ngủ.
  - Giờ yên tĩnh 22:00–08:00 (giờ trên máy): dời tới 8:00.
  - Chuyện cách nhau dưới 60 phút thì gộp 1 thông báo, ví dụ "Kem cần bạn! Bé đang đói, buồn ngủ và lấm lem".
  - Không báo trong 15 phút đầu sau khi tắt app. Tối đa 8 thông báo.
  - Hỏi quyền:
    - Chơi 90 giây trong vườn thì hiện thẻ giải thích "Bật thông báo?" (1 lần).
    - Đồng ý mới hiện hộp thoại xin quyền của máy (Android 13+ / iOS).
    - "Để sau" = tắt, bật lại ở hòm thư. Máy đã chặn thì hòm thư mở Cài đặt của máy.
  - Android: kênh "Nhắc chăm thú", icon dấu chân trắng, khởi động lại điện thoại vẫn giữ lịch.
  - Cài đặt lưu file riêng của máy (`settings`), không nằm trong file game, không đồng bộ cloud.
  - Sau này thư thông báo (thưởng mùa giải...) cũng gửi về hòm thư này.
- **G15 · Thám hiểm (Phase 7.2, `AdventureService` + `AdventureConfig`; bạn góp ý "ít chỗ để khám phá giữ người dùng", tôi tự thiết kế):**
  - Lối vào: biển gỗ "THÁM HIỂM" ở góc sau bên trái vườn, lối đá dẫn ra khe hở hàng rào, đường đất đi xa ra ngoài vườn. Chạm biển = bản đồ.
  - Bản đồ 5 vùng × 5 màn, màn 5 là trùm (to hơn, đánh giỏi hơn). Mỗi màn = 1 trận với 1 sinh vật hoang (bot có lối đánh riêng), sân đấu theo vùng:

    | Vùng | Sinh vật ngang bé giai đoạn | Sân đấu |
    |---|---|---|
    | 1 · Đồi Cỏ Gió | Baby | đồi xanh, bụi cây, hoa |
    | 2 · Rừng Nấm | Child | thân cây tối, nấm đỏ / tím |
    | 3 · Hồ Sương | Teen | mặt hồ, cỏ lau, sương |
    | 4 · Núi Lửa | Adult | núi lửa, vũng dung nham |
    | 5 · Hang Pha Lê | Ultimate | tinh thể xanh / tím, nhũ đá |

  - Sinh vật: chỉ số = công thức G8 của giai đoạn vùng × 0,7 / 0,8 / 0,9 / 0,98 / 1,05 (màn 1 → trùm). Tay nghề bot tăng dần theo vùng + màn. Vùng 1 mới thuộc chiêu ngang + lên; từ trùm vùng 2 thuộc đủ 4 chiêu. Màu lông theo vùng, hình cố định theo màn.
  - Ý đồ: độ khó bám theo tiến hóa → **bé lớn lên mới đi xa được**, có lý do quay lại mỗi ngày. Đo bằng bot (người chơi khá = tay nghề 0,5, đúng giai đoạn của vùng): 3 màn đầu gần như chắc thắng, màn 4 ~75–100%, trùm 96% / 65% / 46% / 30% / 15% theo vùng. Lớn thêm 1 giai đoạn thì vùng cũ dễ hẳn (để gom đủ 3 sao).
  - Sao: thắng = 1 sao; thắng còn ≥ 40% máu = 2 sao; ≥ 70% = 3 sao. Giữ số sao tốt nhất. Tổng 75 sao.
  - Mở khóa: màn sau mở khi màn trước có sao; thắng trùm thì mở vùng sau.
  - Coin: thắng lần đầu 10 + 4 × số thứ tự màn (trùm ×2: 52 → 212 Coin); mỗi sao mới sau đó +50%; thắng lại không thêm sao +20%. Cả bản đồ 3 sao ≈ 3.500 Coin (giá đồ ở quầy 80–300).
  - Mỗi trận tốn năng lượng + no như đấu Bot (10 / 6). Năng lượng dưới 15 thì không đi được ("bé mệt rồi"). Thú ốm / đang ngủ không đi được.
  - Bản đồ (thẻ trong vườn): tổng sao, nút "Đánh tiếp" màn kế, sao từng vùng; trong vùng: sao + độ khó so với bé (Dễ / Vừa / Khó / Rất khó, so lực chiến); màn: thưởng + lối đánh của sinh vật.
  - Hết trận: thẻ kết quả có sao + Coin + "Mở màn / vùng mới", nút **Đánh lại** bên cạnh Về vườn.
  - Lưu ở file game (`GameSave.Adventure`, bản lưu 6). Thú mất vẫn giữ tiến độ: bé mới đi tiếp từ chỗ cũ.
  - Mẹo hướng dẫn: chơi 6,5 phút (sau mẹo sân tập) thì chỉ đường tới biển Thám hiểm.
- **G16 · Việc mỗi ngày (Phase 7.3, `DailyService` + `DailyConfig`; tôi tự thiết kế để có lý do quay lại mỗi ngày):**
  - "Ngày" theo giờ trên máy (như lượt quảng cáo). Lưu ở `GameSave.Daily` (bản lưu 7). Thú mất vẫn giữ.
  - **Quà mở game theo chuỗi ngày** (nhận ở hòm thư): ngày 1–7 = 20 / 25 / 30 / 35 / 40 / 50 / 80 Coin. Sang ngày 8 quay lại ngày 1. Bỏ 1 ngày không mở game thì chuỗi về ngày 1. Mở game mà còn quà chưa nhận thì có dòng nhắc.
  - **3 nhiệm vụ mỗi ngày** (nhận Coin ở hòm thư), cố định theo ngày + người chơi (mở lại game không đổi được):
    - 1 việc chăm: cho ăn 3 lần / tắm 1 lần / vuốt ve 5 lần (2 lần vuốt cách nhau ≥ 4 giây) / tập ở sân tập 1 lần (10–15 Coin);
    - 1 việc đấu: thắng 2 trận Thám hiểm / kiếm 1 sao mới (không ra khi đã đủ 75 sao) / thắng 2 trận bất kỳ / đấu 1 trận PvP (20–25 Coin);
    - 60% là đào 2 kho báu, còn lại là 1 việc chăm khác (15 Coin).
    - Xong cả 3: thêm 40 Coin. Đếm qua sự kiện của game (`CareDone`, `AdventureWon`, `BattleEnded`), không phải sửa từng chỗ.
    - Xong 1 nhiệm vụ: hiện dòng báo ở vườn; xong trong trận thì báo lúc về vườn.
  - **Kho báu trong vườn:** mỗi ngày 2 ụ đất có dấu X lấp lánh, chọn từ 12 chỗ cỏ trống cố định (`GardenLayout.DigSpots`). Chạm ụ đất = bé chạy tới, tới nơi tự đào (bé phải đứng cách ≤ 1,6 m, đang thức).
    - Đào được: 70% là 10–25 Coin; 15% bánh thưởng (+15 no, +15 vui) + 5 Coin; 15% nước tăng lực (+15 năng lượng) + 5 Coin.
    - Phần thưởng cố định theo ngày + điểm + người chơi: mở lại game không đổi được.
  - **Cờ đỏ hòm thư** dựng lên + dấu "!" nhấp nhô khi có quà / thưởng chờ nhận; nhận hết thì hạ cờ.
  - Cộng lại 1 ngày chơi đủ ≈ 20–80 (quà) + 45–55 (nhiệm vụ) + 40 (thưởng thêm) + 20–50 (kho báu) ≈ 125–225 Coin, đủ mua 1 món ở quầy sau 1–2 ngày.
  - Mẹo hướng dẫn: kho báu (sau 100 giây chơi), hòm thư có nhiệm vụ (sau 220 giây).
- **G17 · Âm thanh (Phase 8.1; bạn góp ý "chưa nghe âm thanh, lâu lâu mới nghe 1 lần"):**
  - Mọi âm thanh tạo bằng code (`SoundSynth`, C# thuần, 22.050 Hz), không cần file âm thanh. Tạo sẵn ở luồng nền lúc mở game nên không giật.
  - Nhạc nền: vườn (90 nhịp / phút, nhẹ nhàng) và trận đấu (128 nhịp / phút). Lặp liền mạch, đổi màn thì nhạc hòa dần sang bài kia.
  - Vườn: gió + lá xào xạc lặp liên tục, chim hót ngẫu nhiên 4–12 giây / lần. Tiếng nước hồ to dần khi bé tới gần hồ.
  - Bé:
    - bước chân trên cỏ (đi nhanh thì dồn + to hơn);
    - tắm: nước sủi liên tục; ăn: nhai liên tục + nuốt; ngủ: tiếng thở;
    - ngáp, kêu đói, khóc, kêu vui, cười khúc khích khi bị cù, gừ gừ khi được vuốt.
  - Trận: tiếng vung đòn theo loại chiêu, trúng nhẹ / mạnh / chí mạng, đỡ, phản đòn, combo 2–5, Nộ đầy, hạ gục, thắng / thua, gong bắt đầu.
  - Giao diện: mở thẻ, bấm nút, nhận / tiêu Coin.
  - Hòm thư: bật / tắt **âm thanh** và **nhạc nền** riêng (lưu ở file cài đặt của máy).
- **G18 · Vuốt ve + chạm vào bé (Phase 8.2; bạn góp ý "vuốt giống di chuyển hơn, bấm vào pet không giống đang chăm sóc"; `StrokeTracker`):**
  - Kéo bắt đầu trên người bé được phân loại theo bán kính bé trên màn hình (r):
    - xoa qua lại (đổi hướng ≥ 1 lần, đi ≥ 0,5 r) hoặc xoa vòng (≥ 1,6 r) = **vuốt ve**;
    - kéo ra khỏi người bé (1,15 r) khi chưa rõ ý = **dắt đi**; đang vuốt mà kéo xa quá 2,4 r = chuyển sang dắt.
  - Mỗi 0,8 r đường xoa = 1 lượt: +1,5 vui (2 lượt cách nhau ≥ 0,6 giây) + nửa điểm Chăm sóc của 1 lần chạm.
  - Được vuốt: bé đứng yên lim dim, nghiêng đầu dụi theo tay, vẫy đuôi nhanh, tai thả lỏng, tim bay đều, gừ gừ. Đang ngủ vẫn vuốt được (không đánh thức).
  - Chạm 1 lần: bé quay lại nhìn, nhảy mừng, mắt híp, tim + kêu vui. Chạm 3 lần liền = cù: cười khúc khích, lắc mình. Bé ốm: chạm = lau người.
- **G19 · Đồ chơi + trò chơi trong vườn (Phase 8.3, `PlayService` + `PlayConfig`; bạn góp ý "làm vài trò chơi với pet, ví dụ ném banh"):**
  - Mua ở quầy ▸ **Đồ chơi cho bé...**, mua 1 lần là có mãi (`CosmeticsData.Toys`, thú mất vẫn còn):

    | Đồ chơi | Giá | Chỗ | Chơi thế nào |
    |---|---|---|---|
    | Bóng cao su | 60 | gần ổ ngủ, bên phải | Vuốt quả bóng để ném (hướng + lực theo ngón tay, vật lý thật: nảy, lăn, đụng rào bật lại). Bé chạy nhặt, ngậm mang về chỗ bạn ném. Chạm bóng = đá nhẹ ra xa, bé cũng chạy nhặt |
    | Cần lông vũ | 90 | gần ổ ngủ, bên trái | Giữ cần rồi kéo đi khắp vườn: chùm lông bay theo ngón tay, bé đuổi theo vồ |
    | Máy thổi bong bóng | 140 | giữa ổ ngủ và sân tập | Chạm máy = thổi 8 bong bóng (chờ 4 giây). Bong bóng vọt lên rồi chìm dần, bé nhảy đớp quả thấp (≤ 1,7 m). Bạn chạm bong bóng cũng vỡ |

  - Mỗi lượt chơi (bé phải thức, khỏe, năng lượng ≥ 10):

    | Trò | Vui | Điểm Tốc độ | Năng lượng | No | 2 lượt cách nhau |
    |---|---|---|---|---|---|
    | Nhặt bóng mang về (ném dưới 2 m không tính) | +6 | +1,5 | −1,5 | −0,6 | 1 giây |
    | Vồ lông vũ | +4 | +1 | −1 | −0,4 | 1,2 giây |
    | Đớp bong bóng | +2,5 | +0,5 | −0,5 | −0,2 | 0,5 giây |
    | Bé tự đẩy bóng chơi | +3 | +0,3 | −0,5 | −0,2 | 8 giây |
    | Đuổi bướm ở vòm hoa (G20) | +4 | +0,6 | −0,8 | −0,3 | 5 giây |

  - Bé tự chơi: có bóng mà bé rảnh + chán (vui < 75, năng lượng > 25) thì mỗi lần tự quyết có 20% tới hất bóng 3 lần.
  - Nhiệm vụ ngày mới (chỉ ra khi đã có đồ chơi): "Chơi với bé 3 lần" (15 Coin).
- **G20 · Đồ mua có tác dụng (Phase 8.4, `CarePerks` + `PerksConfig`; bạn góp ý "mua trang trí không có ý nghĩa, ít ra thú phải tương tác được"):**
  - Không đổi chỉ số trận (PvP công bằng), chỉ giúp việc chăm bé. Tác dụng tính cả lúc tắt app (tính bù khi mở lại) và lúc hẹn thông báo, nên giờ báo vẫn khớp.

    | Đồ trang trí | Tác dụng | Bé dùng thế nào |
    |---|---|---|
    | Ghế gỗ | Năng lượng (lúc thức) hao chậm 15% | Bé năng lượng < 65 thì có lúc (35% mỗi lần tự quyết) ra ghế, nhảy lên ngồi nghỉ 9 giây: +4 năng lượng, +2 vui (10 phút tối đa 1 lần). Chạm ghế, hoặc dắt bé thả lên ghế = bé ra ngồi. Vuốt ve bé trên ghế được |
    | Dây đèn lồng | Ngủ hồi năng lượng nhanh hơn 25% | 18:00–6:00 (giờ trên máy) đèn sáng ấm. Chạm = từng lồng lóe sáng + chuông, bé ngước nhìn, nhảy lên |
    | Cối xay gió | Mỗi ngày 1 túi bánh: +20 no, +10 vui | Túi bánh + sao lấp lánh ở chân cối. Chạm cối = bé chạy tới ăn. Bé no > 90 thì để dành. Vào vườn còn bánh thì có dòng nhắc |
    | Vòm hoa | Vui hao chậm 15% | 3 con bướm bay quanh vòm. 30–60 giây 1 lần (bé rảnh, cách vòm < 16 m) 1 con bay tới rủ bé đuổi: vồ trúng 2 lần = 1 lượt chơi. Chạm vòm = gọi bướm ngay |

    | Phụ kiện (chỉ món đang đeo) | Tác dụng |
    |---|---|
    | Nơ | Vui hao chậm 10% |
    | Mũ len | Năng lượng hao chậm 10% |
    | Khăn quàng | No hao chậm 10% |
    | Vương miện | Cả 4 nhu cầu hao chậm 5% |

  - Nhiều món cộng dồn, mỗi nhu cầu cắt tối đa 40%. Ví dụ ghế + mũ len: năng lượng hao 4 → 3/giờ.
  - Quầy hàng ghi tác dụng từng món. Vừa mua có dòng chỉ cách dùng. Bảng DEV có dòng "Đồ đã mua: hao chậm ...".
  - Túi bánh lưu ngày đã ăn ở `GameSave.Daily.WindmillDay` (trường mới có mặc định, file cũ vẫn đọc được).

## 3. Bảng gene DNA (đã cài trong `PetDna`)

| Gene | Khoảng | Bit | Ghi chú |
|---|---|---|---|
| Gen | 1..15 | 4 | 1 = Gen 1, 2 = Gen 2 |
| EyeShape | 0..5 | 3 | Round, Almond, Cat, Sleepy, Puppy, Sharp |
| EyeColor | 0..11 | 4 | bảng 12 màu |
| FurHue / FurSat / FurVal | 0..63 / 0..7 / 0..7 | 6+3+3 | màu lông HSV |
| BellyTone | 0..3 | 2 | vùng bụng / mõm sáng |
| Pattern + PatternHue | 0..2 / 0..7 | 2+3 | Solid, Stripes, Spots |
| Ear | 0..3 | 2 | Pointed, Round, Folded, Fin |
| HornCount + Horn | 0..3 / 0..2 | 2+2 | Straight, Long, Curved |
| Tail | 0..4 | 3 | Fox, Tuft, Puff, Fin, Curl |
| HeadSize / Chubby / LegLength | 0..15 | 4+4+4 | → blendshape head_big / body_chubby / legs_long |
| PawsDark / EarTipDark | 0..3 | 2+2 | chân / chóp tai sẫm |

## 4. Bộ thú Gen 1 — 5 dạng Adult

| Dạng | Dáng | Màu / điểm nhận diện |
|---|---|---|
| Lửa | cân đối, chân dài | đỏ cam, chân + lưng sẫm, sọc than hồng phát sáng, 2 sừng cong đen chóp sáng |
| Nước | thấp, thân dài (rái cá) | xanh biển, đốm phát sáng xanh, tai vây, đuôi vây, lông ngắn bóng |
| Ánh Sáng | cao thanh, chân mảnh | kem trắng xù, 1 sừng vàng phát sáng, chóp lông vàng |
| Thép | chắc nịch, chân to | xám bạc sọc như giáp, 2 sừng kim loại, tai tròn |
| Bóng Tối | thon dài | tím đen, sọc tím phát sáng, 3 sừng dài, mắt đỏ |
