using System;
using System.Text;
using PawFit.Online;
using UnityEngine;
using UnityEngine.Networking;

namespace PawFit.App
{
    /// <summary>
    /// HTTP tới máy chủ riêng bằng UnityWebRequest (cách chuẩn của Unity trên Android / iOS: dùng chứng chỉ HTTPS của máy).
    /// Chỉ chuyển lời gọi, không có luật game. Callback chạy trên luồng chính.
    /// </summary>
    public sealed class UnityHttpTransport : IHttpTransport
    {
        public void Send(HttpCall call, Action<HttpReply> done)
        {
            var req = new UnityWebRequest(call.Url, call.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(1, call.TimeoutSeconds),
            };
            if (call.Body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(call.Body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(call.Token)) req.SetRequestHeader("Authorization", "Bearer " + call.Token);
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            op.completed += _ =>
            {
                var reply = new HttpReply();
                if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    reply.Status = 0;   // không tới được máy chủ
                    reply.Error = req.error;
                }
                else
                {
                    reply.Status = (int)req.responseCode;
                    reply.Body = req.downloadHandler != null ? req.downloadHandler.text : null;
                    if (req.result != UnityWebRequest.Result.Success) reply.Error = req.error;
                }
                req.Dispose();
                done?.Invoke(reply);
            };
        }
    }

    /// <summary>
    /// Địa chỉ máy chủ riêng: bản Dev gõ ở bảng DEV (lưu file tài khoản) &gt; file Resources/PawFitServer.txt (đặt trước khi build)
    /// &gt; trống = chơi offline (PvP với người chơi ảo trên máy, như trước khi có máy chủ).
    /// </summary>
    public static class ServerUrl
    {
        public static string Resolve(AccountData account)
        {
            if (Debug.isDebugBuild && account != null && !string.IsNullOrWhiteSpace(account.ServerUrlOverride))
                return Clean(account.ServerUrlOverride);
            var file = Resources.Load<TextAsset>("PawFitServer");
            if (file == null) return null;
            foreach (string line in file.text.Split('\n'))
            {
                string s = line.Trim();
                if (s.Length > 0 && !s.StartsWith("#", StringComparison.Ordinal)) return Clean(s);
            }
            return null;
        }

        /// <summary>Thêm http:// nếu quên, bỏ dấu / cuối. Không giống địa chỉ thì trả null.</summary>
        public static string Clean(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string s = url.Trim().TrimEnd('/');
            if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                s = "http://" + s;
            return Uri.TryCreate(s, UriKind.Absolute, out _) ? s : null;
        }
    }
}
