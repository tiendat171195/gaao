# Build Android trên Mac (Phase 5.3)

Làm lần lượt từ trên xuống. Lần đầu mất khoảng 1–2 giờ, chủ yếu là chờ tải Unity và chờ build. Những lần sau chỉ còn bước 9 (lấy code mới) và bước 6 (build).

## 0. Cần chuẩn bị

- Mac chạy macOS 12 trở lên (chip Apple M hoặc Intel đều được), ổ đĩa trống khoảng 25 GB, mạng ổn định.
- Điện thoại Android 64-bit (hầu hết máy từ 2017 trở lại đây) + cáp USB cắm được vào Mac.
- Tài khoản Unity (miễn phí) và tài khoản GitHub có quyền đọc repo `tiendat171195/gaao`.

## 1. Cài Git

Mở **Terminal** (Spotlight `⌘ Space`, gõ "Terminal"), chạy:

```bash
git --version
```

- Nếu ra số phiên bản (ví dụ `git version 2.39...`) là có rồi.
- Nếu macOS hỏi cài **Command Line Developer Tools**, bấm **Install**, đợi xong rồi chạy lại lệnh trên. Build Android không cần cài cả Xcode.

## 2. Lấy code về máy

Nên để project ở thư mục **không đồng bộ iCloud**, ví dụ `~/UnityProjects`. Desktop / Documents đang bật iCloud Drive sẽ làm Unity chạy chậm và hay lỗi file.

**Cách A — GitHub Desktop (dễ nhất, nhất là khi repo để private):**

1. Tải tại desktop.github.com, mở lên và đăng nhập GitHub.
2. **File ▸ Clone Repository** ▸ tab **GitHub.com** ▸ chọn `tiendat171195/gaao`.
3. **Local Path** chọn `~/UnityProjects/gaao` ▸ **Clone**.
4. Bấm ô **Current Branch** ở trên ▸ chọn `claude/hopeful-shannon-3602l1` (nằm trong mục *Other branches* / *origin*).

**Cách B — Terminal:**

```bash
mkdir -p ~/UnityProjects && cd ~/UnityProjects
```

```bash
git clone -b claude/hopeful-shannon-3602l1 https://github.com/tiendat171195/gaao.git
```

- Nếu Terminal hỏi Username / Password: Username là tên GitHub. Password **không phải** mật khẩu GitHub mà là *Personal Access Token*: GitHub ▸ Settings ▸ Developer settings ▸ Personal access tokens, tạo token có quyền đọc repo `gaao`.
- Thấy rắc rối thì dùng Cách A.

## 3. Cài Unity Hub + Unity 6.3 + module Android

1. Tải **Unity Hub** tại unity.com/download, cài, mở lên, đăng nhập. Chọn giấy phép **Personal** (miễn phí) khi được hỏi.
2. Tab **Installs** ▸ **Install Editor** ▸ chọn bản **Unity 6.3 (LTS)**, tức `6000.3.x`. Project ghi `6000.3.0f1`, nhưng bản `6000.3` nào mới nhất cũng được.
3. Ở bước chọn module, tick:
   - **Android Build Support**, mở ra tick cả **OpenJDK** và **Android SDK & NDK Tools**.
   - (iOS Build Support để sau, khi build iOS.)
4. Tick đồng ý giấy phép Android SDK ▸ **Install**. Bước này tải khoảng 6–8 GB.

Lỡ cài Unity rồi mà chưa có Android: **Installs** ▸ bấm ⚙ ở bản 6.3 ▸ **Add modules** ▸ tick như bước 3.

## 4. Mở project lần đầu

1. Unity Hub ▸ tab **Projects** ▸ **Add** ▸ **Add project from disk** ▸ chọn thư mục `~/UnityProjects/gaao`.
2. Nếu dòng project có cảnh báo phiên bản: bấm vào số phiên bản ▸ chọn bản 6.3 vừa cài ▸ **Continue / Change version**.
3. Mở project. Lần đầu Unity import và tải package (URP, Test Framework, Newtonsoft), mất khoảng **5–15 phút**.
4. Nếu Unity hỏi **Enter Safe Mode?**, nghĩa là code có lỗi compile.
   - Chọn **Enter Safe Mode**.
   - Mở **Window ▸ General ▸ Console**, chụp hoặc copy các dòng đỏ gửi tôi. Tôi chưa chạy được Unity thật nên có thể lỗi ở chỗ này.
5. Nếu Package Manager báo lỗi phiên bản: **Window ▸ Package Manager**, chọn package đó ▸ cài bản được gợi ý.
6. Menu trên cùng ▸ **PawFit ▸ Setup Project**. Nếu hiện hộp thoại bảo khởi động lại Unity thì tắt Unity rồi mở lại project.

## 5. Chạy thử trong Unity (nên làm trước khi build)

1. Mở scene `Assets/PawFit/Scenes/Main.unity` (cửa sổ Project, nháy đúp).
2. Cửa sổ **Game** ▸ ô độ phân giải ▸ chọn hoặc thêm **1080x1920 (Portrait)**.
3. Bấm **Play** ▶. Thấy vườn + thú là được. Nút **DEV** ở góc trên trái mở bảng test.
4. Console có lỗi đỏ thì gửi tôi trước rồi mới build.

## 6. Build file APK

Menu **PawFit ▸ Build Android ▸ ...**:

| Lệnh | Dùng khi |
|---|---|
| **Dev (APK)** | Test trên điện thoại: có bảng DEV + quảng cáo giả lập. **Build cái này trước.** |
| **Dev + chạy trên điện thoại (USB)** | Như trên, build xong tự cài + mở game trên điện thoại đang cắm USB (xem bước 7) |
| **Release (APK)** | Bản như phát hành: không có bảng DEV, không có quảng cáo |

- Lệnh tự chuyển project sang Android. Lần đầu Unity import lại toàn bộ cho Android mất khoảng 5–10 phút.
- Lệnh tự đặt:
  - Tên gói `com.pawfit.game`, phiên bản `0.5.3`.
  - IL2CPP + ARM64, vì máy Android mới chỉ chạy app 64-bit.
- Lần build đầu mất khoảng **10–20 phút**: IL2CPP dịch code và Gradle tải thư viện. Các lần sau nhanh hơn nhiều.
- Xong thì Finder tự mở `~/UnityProjects/gaao/Builds/Android/PawFit-dev.apk`. Thư mục `Builds/` không bị đưa lên git.
- APK ký bằng khóa debug của Unity: cài thẳng vào máy được, nhưng chưa đưa lên Google Play được.

Muốn làm bằng tay (không dùng menu): **File ▸ Build Profiles** ▸ chọn **Android** ▸ **Switch Platform** ▸ tick **Development Build** ▸ **Build**. Với cách này phải tự đặt **Player Settings ▸ Other Settings**: Scripting Backend = **IL2CPP**, Target Architectures = chỉ **ARM64**.

## 7. Cài vào điện thoại

**Bật chế độ nhà phát triển trên điện thoại (làm 1 lần):**

1. **Cài đặt ▸ Thông tin điện thoại**. Samsung: thêm bước **Thông tin phần mềm**. Chạm **Số hiệu bản tạo / Build number** 7 lần liên tiếp.
   - Xiaomi: chạm **Phiên bản MIUI / HyperOS** 7 lần.
2. **Cài đặt ▸ Tùy chọn nhà phát triển** ▸ bật **Gỡ lỗi USB / USB debugging**.
   - Xiaomi: bật thêm **Cài đặt qua USB**.
3. Cắm cáp vào Mac. Điện thoại hỏi "Cho phép gỡ lỗi USB?" ▸ tick "Luôn cho phép" ▸ **Cho phép**.

**Rồi chọn 1 trong 3 cách:**

- **Cách 1 (dễ nhất):** menu **PawFit ▸ Build Android ▸ Dev + chạy trên điện thoại (USB)**. Unity tự cài và mở game.
- **Cách 2:** gửi file `PawFit-dev.apk` sang điện thoại (Google Drive, Telegram, Zalo...), mở file trên điện thoại ▸ cho phép **cài ứng dụng không rõ nguồn gốc** ▸ **Cài đặt**.
- **Cách 3 (Terminal):** dùng `adb` có sẵn trong Unity.

  ```bash
  ADB="$(ls -d /Applications/Unity/Hub/Editor/6000.3*/PlaybackEngines/AndroidPlayer/SDK/platform-tools | tail -1)/adb"
  ```

  ```bash
  "$ADB" devices
  ```

  ```bash
  "$ADB" install -r ~/UnityProjects/gaao/Builds/Android/PawFit-dev.apk
  ```

  Lệnh `devices` phải ra 1 dòng có chữ `device`. Nếu ra `unauthorized` thì xem lại điện thoại và bấm **Cho phép**.

## 8. Thử gì trên điện thoại

- [ ] Mở game: vườn màn dọc, thấy thú Gen 1, không phải khối vuông tạm. Thú hồng tím = lỗi shader: bấm **DEV** ▸ **URP Lit**, báo tôi.
- [ ] Chạm đất thú chạy tới; kéo đất để kéo camera; kéo thú để dắt; kéo táo từ khay ở chòi ăn để cho ăn.
- [ ] Nút **DEV** bấm được. Dòng "Hình: ... fps" ở mục cuối bảng DEV: ghi lại số fps gửi tôi (mục tiêu 60, thấp hơn 30 là phải tối ưu).
- [ ] Quầy hàng: mua bánh, xem quảng cáo giả 3 giây, mua phụ kiện / trang trí. Có tiếng "ting".
- [ ] Chạm hình nộm ▸ Huấn luyện: màn hình xoay ngang, vuốt ra chiêu. Bấm Xong thì xoay lại dọc.
- [ ] Vuốt tắt hẳn app (đóng khỏi đa nhiệm) rồi mở lại: thú, Coin, đồ đã mua vẫn còn.

## 9. Lần sau lấy code mới

- GitHub Desktop: **Fetch origin** ▸ **Pull origin**.
- Terminal:

  ```bash
  cd ~/UnityProjects/gaao && git pull
  ```

Rồi quay lại Unity, đợi nó tự compile và build lại (bước 6).

Nên commit + push các file Unity tự sinh sau lần mở đầu (các file `.meta`, `ProjectSettings/`, `Assets/PawFit/Settings/`, scene `Main.unity`, `LitBase.mat`). Lần sau mở nhanh hơn, và tôi xem được cài đặt project của bạn. GitHub Desktop: gõ tóm tắt ▸ **Commit to claude/hopeful-shannon-3602l1** ▸ **Push origin**.

## 10. Lỗi hay gặp

| Hiện tượng | Cách xử lý |
|---|---|
| Hộp thoại "Unity này chưa cài Android Build Support" | Làm bước 3 phần "Add modules" |
| Hộp thoại "Chưa chạy menu PawFit ▸ Setup Project" | Chạy **PawFit ▸ Setup Project** (bước 4.6) rồi build lại |
| Lỗi đỏ có chữ JDK / SDK / NDK / Gradle not found | **Unity ▸ Settings ▸ External Tools**: tick hết các ô "... installed with Unity (recommended)" |
| Build lỗi Gradle (Could not resolve / timeout) | Kiểm tra mạng (lần đầu Gradle phải tải thư viện) rồi build lại. Vẫn lỗi thì gửi tôi |
| Điện thoại báo "Không cài được ứng dụng" | Gỡ bản PawFit cũ trên máy rồi cài lại. Máy quá cũ (32-bit) thì không chạy được bản ARM64 |
| Game mở rồi tắt ngay | Cắm USB, chạy `"$ADB" logcat -s Unity` (xem bước 7, cách 3), mở game lại, copy các dòng in ra gửi tôi |
| Thú là khối vuông tròn | Model Gen 1 chưa nạp được: trong Unity chuột phải `Assets/PawFit/Resources/Pets/Gen1` ▸ **Reimport**, build lại |

## 11. Gửi lỗi cho tôi thế nào

- Lỗi trong Unity: cửa sổ **Console** ▸ bấm dòng đỏ ▸ copy cả đoạn chữ bên dưới (stack trace).
- Lỗi trên điện thoại: chụp màn hình + log `adb logcat -s Unity` (xem bảng ở bước 10).
- Ghi kèm: tên máy Android, phiên bản Android, bản Unity (`6000.3.x`), Mac chip M hay Intel.
