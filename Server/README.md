# Máy chủ riêng của PawFit

Máy chủ viết bằng **.NET 8 (ASP.NET Core) + SQLite**. Nó dùng lại đúng code luật game trong `Assets/PawFit/Scripts` (Core / Pet / Battle / Online), nên máy chủ chạy trận bằng cùng `BattleSimulation` với điện thoại. Firebase chỉ để dành cho thông báo đẩy (FCM) sau này.

## Máy chủ làm gì

| Việc | Cách làm |
|---|---|
| Tài khoản | **Tài khoản khách:** mở game là có, không cần email / mật khẩu. Máy chủ đặt sẵn biệt danh, người chơi đổi ở hòm thư. Máy chủ chỉ lưu bản băm SHA-256 của mã đăng nhập. |
| Chuyển máy | Máy cũ: **Hòm thư ▸ Mã chuyển máy** → được mã `XXXX-XXXX-XXXX` (dùng 1 lần, hạn 7 ngày). Máy mới: **Hòm thư ▸ Nhập mã chuyển máy** → nhận tài khoản + thú, đóng game rồi mở lại. Từ lúc đó máy cũ mất quyền dùng tài khoản này. |
| Giờ máy chủ | Chống chỉnh giờ điện thoại. Game đồng bộ giờ lúc mở game, lúc quay lại game và mỗi 10 phút. |
| Sao lưu | Game tải file lên máy chủ mỗi 2 phút (chỉ khi có thay đổi) và mỗi lần ẩn app. |
| Rank | Elo / bậc / mùa do **máy chủ** tính, bằng đúng `RankRules` của game. |
| PvP | Máy chủ ghép **người thật với nhau trước**. Chờ 6 giây chưa có ai thì **người chơi ảo** bắt đầu tới, ghép theo đúng luật ±100 Elo / ±10% lực chiến, nới khoảng sau 30 giây. Người chơi ảo đánh ngay trên máy chủ, và máy người chơi không phân biệt được là ảo. **Máy chủ chạy trận và quyết định kết quả.** 2 điện thoại chỉ chạy lại các nét vuốt máy chủ đã chốt, nên hai bên khớp nhau tới từng bit. Thoát app / mất mạng giữa trận tính là bỏ cuộc (thua). |

## 1. Chạy thử trên máy Mac (không cần thuê gì)

```bash
brew install --cask dotnet-sdk
```

```bash
cd gaao/Server/PawFit.Server
```

```bash
dotnet run
```

Thấy dòng `Now listening on: http://0.0.0.0:5080` là chạy rồi. Dữ liệu nằm ở `Server/PawFit.Server/data/pawfit.db`. Lần đầu chạy, macOS có thể hỏi có cho `dotnet` nhận kết nối không: bấm **Cho phép**.

Kiểm tra máy chủ:

```bash
curl http://localhost:5080/
```

Phải ra `PawFit server OK · proto 1`.

**Nối điện thoại vào máy Mac** (cùng Wi-Fi, chỉ áp dụng cho bản **Dev**):

1. Xem IP của Mac (ví dụ ra `192.168.1.10`):

   ```bash
   ipconfig getifaddr en0
   ```

2. Chọn 1 trong 2 cách:
   - **Trên điện thoại:** mở game ▸ nút **DEV** ▸ mục **Máy chủ** ▸ gõ `192.168.1.10:5080` ▸ **Lưu**. Tắt hẳn game rồi mở lại.
   - **Trước khi build:** ghi `http://192.168.1.10:5080` vào `Assets/PawFit/Resources/PawFitServer.txt` (dòng không bắt đầu bằng `#`).
3. Mở **Đấu trường ▸ Tìm trận**. Mở game trên 2 máy thì 2 người tìm thấy nhau. Chỉ có 1 máy thì khoảng 6–15 giây sau sẽ gặp người chơi ảo.

Bản Dev được nối `http://`. Bản Release bắt buộc `https://`, vì Android / iOS chặn `http`. Menu build đã tự đặt việc này.

## 2. Test máy chủ (không cần Unity)

```bash
dotnet test Server/PawFit.Server.Tests
```

8 test, khoảng 10 giây. Test chạy máy chủ thật ở một cổng ngẫu nhiên, rồi dùng đúng code mạng của game để nối vào. Các tình huống được thử:

- tài khoản khách, lưu game, chuyển máy;
- 2 người đánh trọn 1 trận qua WebSocket;
- người chơi ảo trên máy chủ;
- rớt mạng giữa trận = bỏ cuộc;
- chặn mã sai, bản game cũ, chỉ số gian.

## 3. Đưa lên mạng thật (VPS + tên miền)

**Cần chuẩn bị:**

- **VPS Ubuntu 22.04 / 24.04**, gói nhỏ nhất (1 vCPU, 1–2 GB RAM) là đủ cho giai đoạn thử nghiệm. Nên chọn khu vực **Singapore** để độ trễ từ Việt Nam thấp.
- **1 tên miền**, ví dụ `pawfit.ten-ban.com`, có bản ghi **A** trỏ về IP của VPS.

**Trên VPS:**

```bash
curl -fsSL https://get.docker.com | sh
```

```bash
git clone https://github.com/tiendat171195/gaao.git && cd gaao && git checkout claude/hopeful-shannon-3602l1
```

Repo đang để riêng tư thì VPS cần đăng nhập GitHub trước khi `git clone`: cài `gh` rồi chạy `gh auth login`, hoặc thêm deploy key cho repo.

```bash
cd Server && cp .env.example .env && nano .env
```

Trong `.env`, sửa `PAWFIT_DOMAIN=` thành tên miền của bạn rồi lưu lại.

```bash
sudo ufw allow 80 && sudo ufw allow 443
```

```bash
docker compose up -d --build
```

Đợi khoảng 1 phút để Caddy tự xin chứng chỉ HTTPS (Let's Encrypt), rồi mở `https://tên-miền/` trên trình duyệt. Thấy `PawFit server OK` là xong.

Cuối cùng ghi `https://tên-miền` vào `Assets/PawFit/Resources/PawFitServer.txt` rồi build lại game (Release dùng được luôn).

**Cập nhật máy chủ** (dữ liệu nằm trong volume `pawfit-data` nên không mất):

```bash
cd gaao && git pull && cd Server && docker compose up -d --build
```

**Xem log:**

```bash
docker compose logs -f pawfit
```

**Sao lưu dữ liệu** (máy chủ dừng vài giây):

```bash
docker compose stop pawfit && docker run --rm -v server_pawfit-data:/data -v "$PWD":/backup alpine cp /data/pawfit.db /backup/pawfit-$(date +%F).db && docker compose start pawfit
```

## 4. Cấu hình

Cấu hình đặt ở `appsettings.json`, mục `PawFit`, hoặc bằng biến môi trường `PawFit__Tên` (ví dụ `PawFit__BotDelaySeconds=10`).

| Tên | Mặc định | Ý nghĩa |
|---|---|---|
| `DbPath` | `data/pawfit.db` | File SQLite. Docker: `/data/pawfit.db`. |
| `BehindProxy` | `false` | `true` khi chạy sau Caddy, để biết IP thật của người chơi. |
| `BotDelaySeconds` | `6` | Chờ người thật bấy nhiêu giây rồi mới thả người chơi ảo. |
| `GuestPerHourPerIp` | `30` | Mỗi IP tạo tối đa bấy nhiêu tài khoản khách mỗi giờ. |
| `RedeemPerHourPerIp` | `30` | Mỗi IP nhập mã chuyển máy tối đa bấy nhiêu lần mỗi giờ. |
| `ReadyTimeoutSeconds` | `15` | Một bên chưa vào màn đấu sau bấy nhiêu giây thì trận vẫn bắt đầu. |
| `FrameEveryTicks` | `1` | Cứ bấy nhiêu bước gửi nhịp trận 1 lần (1 = 30 tin / giây). |
| `TransferCodeDays` | `7` | Số ngày mã chuyển máy còn hạn. |

Cổng mặc định là `5080`. Đổi bằng `--urls http://0.0.0.0:6000` hoặc biến `ASPNETCORE_URLS`.

## 5. API

| Đường dẫn | Việc |
|---|---|
| `GET /` | Kiểm tra máy chủ còn sống. |
| `GET /v1/time` | Giờ máy chủ. |
| `POST /v1/auth/guest` | Tạo tài khoản khách. |
| `POST /v1/auth/redeem` | Nhập mã chuyển máy. |
| `GET /v1/me` | Hồ sơ + giờ + rank + bản lưu mới nhất. |
| `PUT /v1/me/name` | Đổi biệt danh. |
| `GET /v1/save` · `PUT /v1/save` | Tải về / tải lên file game. 409 = máy chủ có bản mới hơn. |
| `POST /v1/transfer-code` | Xin mã chuyển máy. |
| `GET /v1/pvp` | WebSocket tìm trận + PvP. |

Đường dẫn nào cần đăng nhập thì gửi header `Authorization: Bearer <mã đăng nhập>`. Tin nhắn có dạng như trong `Assets/PawFit/Scripts/Online/Protocol.cs`. Khi đổi luật trận hoặc tin nhắn thì tăng `NetProto.Version`: bản game cũ sẽ được báo "hãy cập nhật".

## 6. Giới hạn của giai đoạn thử nghiệm

- **Coin, đồ, thú vẫn do điện thoại tự tính.** Máy chủ chỉ sao lưu. Máy chủ quyết định Elo, kết quả PvP và Coin thưởng PvP. Muốn chống gian lận Coin / thú thì phải chuyển cả kinh tế lên máy chủ (việc lớn, làm khi cần).
- **Chỉ số thú lúc tìm trận do máy gửi lên.** Máy chủ chặn số vượt trần của giai đoạn đó, và tự tính lực chiến từ chỉ số.
- **Tài khoản khách mất nếu xóa app mà chưa lấy mã chuyển máy.** Đăng nhập Google / Apple làm sau. Trên iOS, có đăng nhập Google thì bắt buộc có thêm "Sign in with Apple".
- **1 máy chủ, 1 file SQLite.** Đủ cho vài nghìn người. Khi đông hơn:
  - chuyển sang PostgreSQL;
  - tách phần PvP ra nhiều máy hoặc sang Photon. Giao diện `INetworkManager` giữ nguyên, nên không phải sửa màn tìm trận / màn đấu.
- **Giờ máy chủ chỉ có tác dụng khi có mạng.** Chỉnh giờ lúc tắt app thì lần mở đầu tiên vẫn lợi được vài giây cho tới khi đồng bộ xong. Giờ bị chỉnh lùi về thì không bị trừ ngược.
