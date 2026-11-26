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
- **G4 · Chữa:**
  - Mệt: chăm đủ là hết.
  - Ốm / Nguy kịch: dùng thuốc (60 / 150 Coin), hoặc chăm kỹ (cháo + chăn + lau người, ít nhất 1 lượt mỗi 12h). Cả 2 cách khỏi sau 24h / 48h.
  - Đang hồi phục mà quá 24h không chăm thì tái phát. Thú ốm không chịu xuống hồ tắm.
- **G5 · Chết & trứng:** bia mộ ghi tên, tuổi, rank cao nhất, mã DNA. Sau 24h có trứng thừa hưởng 1 gene hình dạng; chạm trứng để nở và đặt tên.
- **G6 · Mã DNA:** `PF-XXXX-XXXX-XXXX` = 55 bit gene → 11 ký tự Base32 + 1 ký tự kiểm tra (đã cài: `DnaCodec`). Chỉ chứa gene hình dạng và trường `Gen`; seed + tính cách ẩn không có trong mã.
- **G7 · Tiến hóa:**
  - Lửa: Sức mạnh trội.
  - Nước: Tốc độ trội.
  - Ánh Sáng: Chăm sóc trội và CareScore ≥ 70.
  - Bóng Tối: Bóng tối trội, hoặc CareScore < 40.
  - Thép: tập cân bằng.
  - Cộng nhiễu 10–20%. Từ Teen, DNA trượt dần về dạng đang nghiêng. Ultimate cần CareScore ≥ 80 + thuộc đủ 4 chiêu + 7 ngày không bệnh.
- **G8 · Chỉ số:**
  - Baby: HP 100 / ATK 10 / DEF 10 / SPD 10 / Crit 5%.
  - Hệ số theo giai đoạn: ×1 / 1.5 / 2.2 / 3.2 / 4.2.
  - Theo dạng: Lửa +25% ATK, Nước +25% SPD, Ánh Sáng +25% HP, Thép +25% DEF, Bóng Tối +10 điểm Crit.
  - Lực chiến = 0.25·HP + 2·ATK + 1.5·DEF + 1.5·SPD + 10·Crit%.
- **G9 · Chiến đấu:**
  - Ngang = chiêu hệ 1 · Lên = chiêu hệ 2 · V = chiêu riêng · Z = Ultimate · Xuống = đỡ (≤ 0.2s trước đòn = phản đòn).
  - Damage = ATK × hệ số chiêu × hệ số vuốt (0.7–1.3) × 100/(100+DEF); crit ×1.5.
  - Hết 90 giây: bên còn nhiều %HP hơn thắng.
- **G10 · Rank:** Elo khởi đầu 1000, K = 32. Bronze < 1100 ≤ Silver < 1300 ≤ Gold < 1500 ≤ Diamond < 1700 ≤ Master. Hết mùa reset mềm về 1000 + (Elo − 1000)/2. Tìm 30 giây chưa có trận thì nới khoảng; 60 giây thì gợi ý đấu Bot.
- **G11 · Kinh tế:**
  - Thức ăn cơ bản miễn phí, khởi đầu 200 Coin, thắng PvP +30, quảng cáo +20 Coin / +20 năng lượng.
  - Không có HUD: shop = quầy hàng trên map; đấu Bot = 3 hình nộm; PvP = cổng đấu trường; cài đặt = hòm thư ở cổng.

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
