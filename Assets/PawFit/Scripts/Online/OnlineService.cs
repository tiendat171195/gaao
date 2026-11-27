using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    public enum OnlineState { Offline = 0, Connecting = 1, Online = 2 }

    /// <summary>Tài khoản trên máy chủ riêng. Lưu file riêng "account" (không nằm trong file game).</summary>
    public sealed class AccountData
    {
        public string PlayerId;

        /// <summary>Mã đăng nhập (bí mật, máy chủ chỉ giữ bản băm). Mất mã = mất tài khoản, nên có mã chuyển máy.</summary>
        public string Token;

        /// <summary>Biệt danh (đối thủ PvP thấy).</summary>
        public string Name;

        /// <summary>Bản lưu game trên máy chủ mà file trên máy đang dựa vào.</summary>
        public long SaveRevision;

        /// <summary>Dấu nội dung lần tải lên gần nhất: file game không đổi thì không tải lại.</summary>
        public string UploadedHash;

        /// <summary>Vừa nhập mã chuyển máy, chưa tải được dữ liệu về: cấm tải file trên máy lên (kẻo đè mất thú đã chuyển).</summary>
        public bool NeedsDownload;

        /// <summary>Giờ máy chủ − giờ máy lần đồng bộ gần nhất (giây), dùng ngay lúc mở app khi chưa kịp đồng bộ.</summary>
        public double ClockOffsetSeconds;

        /// <summary>Bản Dev: địa chỉ máy chủ gõ ở bảng DEV (trống = dùng địa chỉ trong bản build).</summary>
        public string ServerUrlOverride;
    }

    public sealed class OnlineConfig
    {
        /// <summary>Đang chơi thì cứ bấy nhiêu giây tải file game lên 1 lần (nếu có đổi). Ẩn app thì tải ngay.</summary>
        public float UploadEverySeconds = 120f;

        /// <summary>Không nối được máy chủ thì thử lại sau bấy nhiêu giây.</summary>
        public float RetryEverySeconds = 30f;

        /// <summary>Đồng bộ lại giờ mỗi bấy nhiêu giây.</summary>
        public float ResyncEverySeconds = 600f;
    }

    /// <summary>Trạng thái nối máy chủ vừa đổi (hòm thư / bảng DEV hiện theo).</summary>
    public readonly struct OnlineChanged
    {
        public readonly OnlineState State;
        public OnlineChanged(OnlineState state) { State = state; }
    }

    /// <summary>
    /// Tài khoản + đồng bộ với máy chủ riêng (Phase 4.1, C# thuần):
    ///  * Mở game lần đầu: tạo tài khoản khách (không cần email / mật khẩu), máy chủ đặt sẵn biệt danh.
    ///  * Mỗi lần nối: lấy giờ máy chủ (<see cref="ServerClock"/>) + rank thật (<see cref="RankService.Adopt"/>).
    ///  * Tải file game lên máy chủ (sao lưu) mỗi 2 phút nếu có đổi + khi ẩn app.
    ///  * Mã chuyển máy: máy cũ xin mã, máy mới nhập mã → tài khoản + thú chuyển sang máy mới (máy cũ mất quyền).
    ///    Dữ liệu tải về được đặt chờ ở khóa "restore", lần mở game sau mới thay file game (<see cref="ApplyPendingRestore"/>).
    ///  * Máy chủ báo mã đăng nhập hết hiệu lực (đã chuyển máy / máy chủ làm lại dữ liệu): tự tạo tài khoản mới, giữ thú trên máy.
    /// Gọi <see cref="Update"/> mỗi frame trên luồng chính.
    /// </summary>
    public sealed class OnlineService
    {
        public const string AccountKey = "account";
        public const string PendingRestoreKey = "restore";

        private readonly ServerApi _api;
        private readonly ISaveService _save;
        private readonly ISerializer _serializer;
        private readonly PetService _pets;
        private readonly RankService _rank;
        private readonly ServerClock _clock;
        private readonly EventBus _bus;
        private readonly ILog _log;
        private readonly OnlineConfig _cfg;
        private readonly AccountData _account;
        private float _retryIn = -1f, _uploadIn, _resyncIn;
        private bool _uploading;
        private bool _downloading;

        public OnlineService(ServerApi api, ISaveService save, ISerializer serializer, PetService pets, RankService rank,
                             ServerClock clock, EventBus bus, ILog log = null, OnlineConfig cfg = null)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _rank = rank ?? throw new ArgumentNullException(nameof(rank));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _log = log ?? NullLog.Instance;
            _cfg = cfg ?? new OnlineConfig();
            _account = LoadAccount(save);
            _api.Token = _account.Token;
        }

        public OnlineState State { get; private set; } = OnlineState.Offline;
        public AccountData Account => _account;
        public ServerApi Api => _api;
        public string PlayerId => _account.PlayerId;
        public string Name => _account.Name;
        public string LastError { get; private set; }

        /// <summary>Vừa nhận dữ liệu từ mã chuyển máy: phải mở lại game. Từ lúc này không tải gì lên nữa.</summary>
        public bool RestartPending { get; private set; }

        /// <summary>Dữ liệu từ mã chuyển máy đã sẵn: App báo người chơi đóng game rồi mở lại.</summary>
        public event Action RestartNeeded;

        public static AccountData LoadAccount(ISaveService save) =>
            save != null && save.TryLoad(AccountKey, out AccountData a) && a != null ? a : new AccountData();

        /// <summary>
        /// Gọi lúc mở game, TRƯỚC khi PetService đọc file: vừa nhập mã chuyển máy thì thay file game bằng bản đã tải về.
        /// Trả true nếu vừa thay.
        /// </summary>
        public static bool ApplyPendingRestore(ISaveService save)
        {
            if (save == null || !save.TryLoad(PendingRestoreKey, out GameSave g) || g == null) return false;
            save.Save(PetService.SaveKey, g);
            save.Delete(PendingRestoreKey);
            return true;
        }

        /// <summary>Nối máy chủ: chưa có tài khoản thì tạo tài khoản khách, rồi lấy giờ + rank, rồi tải file game lên.</summary>
        public void Start()
        {
            if (State != OnlineState.Offline || RestartPending) return;
            SetState(OnlineState.Connecting);
            if (string.IsNullOrEmpty(_account.Token)) RegisterGuest();
            else FetchMe();
        }

        public void Update(float deltaSeconds)
        {
            _api.Update();
            if (RestartPending) return;
            float dt = Math.Max(0f, deltaSeconds);
            if (State == OnlineState.Offline && _retryIn >= 0f)
            {
                _retryIn -= dt;
                if (_retryIn < 0f) Start();
            }
            if (State != OnlineState.Online) return;
            _uploadIn -= dt;
            if (_uploadIn <= 0f)
            {
                _uploadIn = _cfg.UploadEverySeconds;
                UploadIfChanged();
            }
            _resyncIn -= dt;
            if (_resyncIn <= 0f)
            {
                _resyncIn = _cfg.ResyncEverySeconds;
                SyncTime();
            }
        }

        /// <summary>Ẩn app: tải file game lên ngay (có thể bị tắt hẳn sau đó).</summary>
        public void OnPause()
        {
            if (State == OnlineState.Online && !RestartPending) UploadIfChanged();
        }

        /// <summary>Quay lại app: đồng bộ giờ lại (giờ máy có thể đã bị chỉnh lúc ẩn app), mất nối thì nối lại ngay.</summary>
        public void OnResume()
        {
            if (RestartPending) return;
            if (State == OnlineState.Online) SyncTime();
            else if (State == OnlineState.Offline) _retryIn = 0f;
        }

        // ------------------------------------------------------------------

        private void RegisterGuest()
        {
            _api.Guest(new GuestRequest { Proto = NetProto.Version }, r =>
            {
                if (!r.Ok || r.Value == null || string.IsNullOrEmpty(r.Value.Token))
                {
                    Fail(r.Error);
                    return;
                }
                _account.PlayerId = r.Value.PlayerId;
                _account.Token = r.Value.Token;
                _account.Name = r.Value.Name;
                _account.SaveRevision = 0;
                _account.UploadedHash = null;
                _account.NeedsDownload = false;
                _api.Token = _account.Token;
                SaveAccount();
                _log.Info("[PawFit] Tạo tài khoản khách " + _account.PlayerId + " (" + _account.Name + ")");
                FetchMe();
            });
        }

        private void FetchMe()
        {
            _api.Token = _account.Token;
            _api.Me(r =>
            {
                if (r.Status == 401)   // mã đăng nhập hết hiệu lực: tạo tài khoản mới, giữ nguyên thú trên máy
                {
                    _log.Warn("[PawFit] Tài khoản " + _account.PlayerId + " không còn trên máy chủ, tạo tài khoản mới");
                    _account.Token = null;
                    _account.PlayerId = null;
                    _account.NeedsDownload = false;
                    RegisterGuest();
                    return;
                }
                if (!r.Ok || r.Value == null)
                {
                    Fail(r.Error);
                    return;
                }
                MeReply me = r.Value;
                _clock.Sync(NetProto.FromMs(me.UtcMs), r.RoundTripSeconds);
                _account.ClockOffsetSeconds = _clock.OffsetSeconds;
                _account.PlayerId = me.PlayerId;
                _account.Name = me.Name;
                // máy đang giữ tài khoản là bản đúng: máy chủ có bản mới hơn (lỡ 2 máy cùng ghi) thì lần tải sau ghi đè
                if (me.SaveRevision > _account.SaveRevision && !_account.NeedsDownload) _account.SaveRevision = me.SaveRevision;
                SaveAccount();
                _rank.Adopt(me.Rank);   // rank thật nằm ở máy chủ
                _resyncIn = _cfg.ResyncEverySeconds;
                _uploadIn = _cfg.UploadEverySeconds;
                SetState(OnlineState.Online);
                _log.Info("[PawFit] Đã nối máy chủ " + _api.BaseUrl + " · " + _account.Name + " · lệch giờ " +
                          _clock.OffsetSeconds.ToString("0.0") + " giây");
                if (_account.NeedsDownload) DownloadForRestart(null);
                else UploadIfChanged();
            });
        }

        private void SyncTime()
        {
            _api.Time(r =>
            {
                if (!r.Ok || r.Value == null) return;
                _clock.Sync(NetProto.FromMs(r.Value.UtcMs), r.RoundTripSeconds);
                _account.ClockOffsetSeconds = _clock.OffsetSeconds;
                SaveAccount();
            });
        }

        /// <summary>Tải file game lên nếu khác lần trước.</summary>
        public void UploadIfChanged()
        {
            if (_uploading || RestartPending || _account.NeedsDownload || State != OnlineState.Online) return;
            _pets.Save();
            if (!_save.TryLoad(PetService.SaveKey, out GameSave g) || g == null) return;
            string json = _serializer.Serialize(g);
            string hash = NetProto.Hash(json);
            if (hash == _account.UploadedHash) return;
            _uploading = true;
            _api.PutSave(new SavePut { BaseRevision = _account.SaveRevision, Data = json }, r =>
            {
                _uploading = false;
                if (RestartPending) return;
                if (r.Ok && r.Value != null)
                {
                    _account.SaveRevision = r.Value.Revision;
                    _account.UploadedHash = hash;
                    SaveAccount();
                    return;
                }
                if (r.Status == 409)   // máy chủ có bản mới hơn (lỡ 2 máy cùng ghi): máy đang giữ tài khoản ghi đè ở lần sau
                {
                    _account.SaveRevision = r.ConflictRevision;
                    SaveAccount();
                    _uploadIn = 1f;
                    return;
                }
                if (r.Status == 401 || r.Unreachable)
                {
                    Fail(r.Error);
                    return;
                }
                _log.Warn("[PawFit] Lưu game lên máy chủ lỗi: " + r.Error);
            });
        }

        // ------------------------------------------------------------------
        // Biệt danh + mã chuyển máy (hòm thư)
        // ------------------------------------------------------------------

        public void Rename(string name, Action<bool, string> done)
        {
            string clean = NetProto.CleanName(name);
            if (clean == null)
            {
                done?.Invoke(false, "Biệt danh cần " + NetProto.NameMin + "–" + NetProto.NameMax + " ký tự");
                return;
            }
            if (State != OnlineState.Online)
            {
                done?.Invoke(false, "Chưa nối được máy chủ");
                return;
            }
            _api.Rename(clean, r =>
            {
                if (!r.Ok || r.Value == null)
                {
                    done?.Invoke(false, r.Error);
                    return;
                }
                _account.Name = r.Value.Name;
                SaveAccount();
                done?.Invoke(true, _account.Name);
            });
        }

        /// <summary>Xin mã chuyển máy (dùng 1 lần, có hạn). Tải file game lên trước để máy mới nhận đúng bản mới nhất.</summary>
        public void RequestTransferCode(Action<TransferCodeReply, string> done)
        {
            if (State != OnlineState.Online)
            {
                done?.Invoke(null, "Chưa nối được máy chủ");
                return;
            }
            UploadIfChanged();
            _api.TransferCode(r => done?.Invoke(r.Ok ? r.Value : null, r.Ok ? null : r.Error));
        }

        /// <summary>
        /// Nhập mã chuyển máy ở máy mới: nhận tài khoản + tải dữ liệu game về (đặt chờ, mở lại game mới thay).
        /// done(ok, lời nhắn cho người chơi).
        /// </summary>
        public void RedeemTransferCode(string code, Action<bool, string> done)
        {
            string clean = NetProto.NormalizeCode(code);
            if (clean == null)
            {
                done?.Invoke(false, "Mã gồm " + NetProto.CodeLength + " chữ / số, dạng XXXX-XXXX-XXXX");
                return;
            }
            if (RestartPending)
            {
                done?.Invoke(false, "Đã nhận dữ liệu rồi, hãy mở lại game");
                return;
            }
            _api.Redeem(clean, r =>
            {
                if (!r.Ok || r.Value == null || string.IsNullOrEmpty(r.Value.Token))
                {
                    done?.Invoke(false, r.Status == 404 ? "Mã không đúng hoặc đã hết hạn" : r.Error);
                    return;
                }
                // từ giây này tài khoản đã thuộc máy này (máy cũ mất quyền): lưu ngay, kể cả khi bước tải về lỗi
                _account.PlayerId = r.Value.PlayerId;
                _account.Token = r.Value.Token;
                _account.Name = r.Value.Name;
                _account.SaveRevision = 0;
                _account.UploadedHash = null;
                _account.NeedsDownload = true;
                _api.Token = _account.Token;
                SaveAccount();
                DownloadForRestart(done);
            });
        }

        /// <summary>Tải dữ liệu game của tài khoản về, đặt chờ ở khóa "restore", báo App mở lại game.</summary>
        private void DownloadForRestart(Action<bool, string> done)
        {
            if (_downloading) return;
            _downloading = true;
            _api.GetSave(s =>
            {
                _downloading = false;
                if (!s.Ok || s.Value == null)
                {
                    done?.Invoke(false, "Đã nhận tài khoản nhưng chưa tải được dữ liệu (" + s.Error + "). Game sẽ tự thử lại khi có mạng.");
                    if (s.Unreachable) Fail(s.Error);
                    return;
                }
                if (string.IsNullOrEmpty(s.Value.Data))   // tài khoản chưa từng lưu game lên: giữ thú đang có trên máy
                {
                    _account.NeedsDownload = false;
                    _account.SaveRevision = s.Value.Revision;
                    SaveAccount();
                    done?.Invoke(true, "Đã nhận tài khoản " + _account.Name + " (chưa có dữ liệu thú trên máy chủ, giữ thú trên máy này)");
                    UploadIfChanged();
                    return;
                }
                GameSave g;
                try
                {
                    g = _serializer.Deserialize<GameSave>(s.Value.Data);
                }
                catch (Exception e)
                {
                    g = null;
                    _log.Error("[PawFit] Dữ liệu trên máy chủ hỏng: " + e.Message);
                }
                if (g == null)
                {
                    done?.Invoke(false, "Dữ liệu trên máy chủ bị hỏng");
                    return;
                }
                _save.Save(PendingRestoreKey, g);
                _account.NeedsDownload = false;
                _account.SaveRevision = s.Value.Revision;
                _account.UploadedHash = null;
                SaveAccount();
                RestartPending = true;
                string pet = g.Pet != null ? g.Pet.Name : "thú";
                _log.Info("[PawFit] Đã tải dữ liệu chuyển máy (" + pet + "), chờ mở lại game");
                done?.Invoke(true, "Đã nhận " + pet + " của " + _account.Name + ". Đóng game rồi mở lại để chơi tiếp.");
                RestartNeeded?.Invoke();
            });
        }

        // ------------------------------------------------------------------

        private void Fail(string error)
        {
            LastError = error;
            _retryIn = _cfg.RetryEverySeconds;
            SetState(OnlineState.Offline);
            _log.Warn("[PawFit] Máy chủ: " + error + " (thử lại sau " + _cfg.RetryEverySeconds.ToString("0") + " giây)");
        }

        private void SetState(OnlineState s)
        {
            if (State == s) return;
            State = s;
            _bus.Publish(new OnlineChanged(s));
        }

        private void SaveAccount() => _save.Save(AccountKey, _account);
    }
}
