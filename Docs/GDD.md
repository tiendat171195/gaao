# PawFit — GDD ngắn

Thiết kế gốc do bạn chốt + các con số / giả định tôi điền thêm (G1–G11). Mọi số nằm trong config, chỉnh được.

## 1. Lõi

- **1 người = 1 thú, chết vĩnh viễn.** Bỏ bê 3 ngày thì bệnh, 7 ngày không chữa thì chết. Khi thú chết có Khu tưởng niệm; 24 giờ sau nhận trứng mới, trứng thừa hưởng 1 trait của thú cũ.
- **Vườn (màn dọc):** 1 vườn cố định nhưng rộng, không có nút, không có thanh chỉ số.
  - Trạng thái hiện bằng hình ảnh.
  - Bố trí: chòi ăn bên trái, hồ tắm bên phải, sân tập phía sau, ổ ngủ ở giữa, tưởng niệm ở góc.
  - Điều khiển: chạm đất thì thú chạy tới; kéo thú để dắt; chạm thú thì thú phản ứng; kéo đồ ăn thả cho thú để cho ăn; kéo thú ra hồ để tắm.
- **Thú:** 1 base mesh + DNA, sinh lúc chạy bằng blendshape + material (+ shader lông ở Gen 2). Style ngầu + cute, không cartoon phẳng.
- **Tiến hóa:** Baby (ngày 0) → Child (3) → Teen (7) → Adult (14) → Ultimate (30, nếu đủ điều kiện).
  - Đầu vào: CareScore 7 ngày + TrainingVector (Sức mạnh / Tốc độ / Chăm sóc / Bóng tối) + seed và tính cách ẩn (10–20%).
  - MVP: 5 dạng Adult — Lửa, Nước, Ánh Sáng, Thép, Bóng Tối.
- **Chỉ số:** HP, ATK, DEF, SPD, Crit. Lực chiến = tổng có trọng số. SPD quyết định ai đánh trước.
- **Chiến đấu:** chỉ vuốt, real-time, không nút.
  - Vuốt ngang = đánh nhanh; vuốt lên = đánh mạnh / hất tung; vuốt xuống = đỡ / phản đòn; vuốt V hoặc Z = chiêu đặc biệt / Ultimate.
  - Cooldown 0.8 giây; vuốt sai bị hở 1 giây.
  - Mode Huấn luyện có bóng mờ gesture, slow-mo, cho vuốt lại, vuốt đúng 10 lần thì thuộc chiêu.
  - PvP 90 giây, màn hình chỉ có 2 thú + vạch HP mờ.
- **Ranking:** ghép đối thủ theo Elo ±100 và Lực chiến ±10%. Bậc Bronze → Silver → Gold → Diamond → Master. Mùa giải 30 ngày.
- **Kinh tế:** Coin; không nạp tiền. Quảng cáo tự nguyện, tối đa 5 lần/ngày (`IAdService` + `MockAdService`).

## 2. Số liệu & giả định (G1–G11)

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
  - Ngang = chiêu hệ 1 · Lên = chiêu hệ 2 · V = chiêu riêng · Z = Ultimate · Xuống = đỡ (≤ 0.2s trước đòn = phản đòn).
  - Damage = ATK × hệ số chiêu × hệ số vuốt (0.7–1.3) × 100/(100+DEF); crit ×1.5.
  - Hết 90 giây: bên còn nhiều %HP hơn thắng.
- **G9b · Cách cài cụ thể (Phase 3.1, `BattleConfig` / `SwipeConfig` / `BotConfig`):**
  - **Nhận dạng vuốt:**
    - Nét dài ≥ 60 px (ở 160 dpi), trong ≤ 1,2 giây.
    - Thẳng (độ thẳng ≥ 0,86) thì xét hướng trội: ngang / lên / xuống.
    - V = xuống rồi lên. Z = ngang → chéo ngược xuống → ngang, vẽ được cả 2 chiều.
    - Hệ số vuốt 0,7–1,3 = 60% tốc độ + 40% độ đúng hình.
  - **4 chiêu (hệ số damage / thời gian vung đòn):**

    | Chiêu | Vuốt | Hệ số | Vung đòn |
    |---|---|---|---|
    | Nhanh | Ngang | ×1 | 0,25 s |
    | Mạnh | Lên | ×1,6 | 0,5 s |
    | Riêng | V | ×2,2 | 0,6 s |
    | Ultimate | Z | ×3,2 | 0,9 s |

  - Chiêu chưa thuộc (Mode Huấn luyện) vẫn dùng được nhưng ×0,8.
  - **Đánh:**
    - Hồi chiêu 0,8 s tính từ lúc ra đòn.
    - Vuốt sai thì hở 1 s: không đánh, không đỡ được.
    - Đòn Mạnh / Riêng / Ultimate trúng thì cắt đòn đang vung của đối thủ (hất tung). Đòn Nhanh không cắt.
  - **Đỡ:**
    - Thế đỡ kéo dài 0,45 s, chỉ chịu 20% damage. Đang hồi chiêu đánh vẫn đỡ được, nhưng giữa 2 lần đỡ phải chờ 0,6 s.
    - Đỡ ≤ 0,2 s trước khi đòn trúng thì phản đòn: bên tấn công ăn ATK × 0,8 của bên đỡ và bị hở 1 s.
  - **SPD:** bên chậm hơn phải chờ 0,5 s mới ra được đòn đầu. 2 đòn trúng cùng lúc thì bên nhanh tính trước.
  - **Bot 3 mức (3 hình nộm)** khác nhau ở:
    - Tốc độ phản ứng: 0,45 / 0,3 / 0,16 s.
    - Xác suất đỡ: 15 / 40 / 70%. Mức Khó canh đỡ sát đòn để phản đòn.
    - Nhịp đánh, độ đẹp nét vuốt, tỉ lệ vuốt sai, chiêu hay dùng.
  - Mô phỏng chạy theo bước cố định 1/30 s, random có seed, nên cùng đầu vào thì cùng kết quả (dùng lại được cho PvP).
- **G9c · Sân tập (Phase 3.2, `BattleSessionConfig`):**
  - Huấn luyện:
    - Bài học = chiêu chưa thuộc đầu tiên, theo thứ tự ngang → lên → V → Z.
    - Mỗi lần vuốt đúng chiêu đang học mà trận nhận đòn thì +1; đủ 10 thì thuộc.
    - Vuốt sai không bị hở.
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
  - Chỉ để đẹp: không đổi chỉ số (PvP công bằng), không đổi luật chăm sóc.
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
  - Cài đặt lưu file riêng của máy (`settings`), không nằm trong file game, không đồng bộ cloud.
  - Sau này thư thông báo (thưởng mùa giải...) cũng gửi về hòm thư này.

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
