using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PawFit.Core
{
    /// <summary>Đổi object <-> chuỗi (JSON). Tách riêng để đổi thư viện JSON không ảnh hưởng chỗ khác.</summary>
    public interface ISerializer
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string text);
    }

    /// <summary>JSON bằng Newtonsoft (Unity: package com.unity.nuget.newtonsoft-json). Enum lưu dạng chữ cho dễ đọc.</summary>
    public sealed class NewtonsoftSerializer : ISerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Converters = { new StringEnumConverter() },
        };

        public string Serialize<T>(T value) => JsonConvert.SerializeObject(value, Settings);

        public T Deserialize<T>(string text) => JsonConvert.DeserializeObject<T>(text, Settings);
    }

    /// <summary>Lưu / đọc dữ liệu theo khóa: file JSON trên máy. Có máy chủ riêng thì OnlineService sao lưu thêm file game lên máy chủ.</summary>
    public interface ISaveService
    {
        bool Exists(string key);
        void Save<T>(string key, T data);
        bool TryLoad<T>(string key, out T data);
        void Delete(string key);
    }

    /// <summary>
    /// Lưu file JSON vào 1 thư mục. Ghi an toàn: ghi file tạm -> giữ bản cũ làm .bak -> thay file.
    /// Nếu app chết giữa chừng, lần sau vẫn đọc được từ .bak.
    /// </summary>
    public sealed class JsonFileSaveService : ISaveService
    {
        private readonly string _dir;
        private readonly ISerializer _serializer;

        public JsonFileSaveService(string directory, ISerializer serializer)
        {
            _dir = directory ?? throw new ArgumentNullException(nameof(directory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Directory.CreateDirectory(_dir);
        }

        public bool Exists(string key) => File.Exists(PathOf(key)) || File.Exists(PathOf(key) + ".bak");

        public void Save<T>(string key, T data)
        {
            string path = PathOf(key);
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, _serializer.Serialize(data));
            if (File.Exists(path))
            {
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            File.Move(tmp, path);
            if (File.Exists(bak)) File.Delete(bak);
        }

        public bool TryLoad<T>(string key, out T data)
        {
            string path = PathOf(key);
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    data = _serializer.Deserialize<T>(File.ReadAllText(candidate));
                    if (data != null) return true;
                }
                catch (Exception)
                {
                    // file hỏng: thử bản dự phòng tiếp theo
                }
            }
            data = default;
            return false;
        }

        public void Delete(string key)
        {
            string path = PathOf(key);
            foreach (string f in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(f)) File.Delete(f);
        }

        private string PathOf(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Khóa lưu không hợp lệ: " + key, nameof(key));
            return Path.Combine(_dir, key + ".json");
        }
    }

    /// <summary>Lưu trong RAM (vẫn qua JSON để giống thật) — dùng cho unit test.</summary>
    public sealed class InMemorySaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store = new Dictionary<string, string>();
        private readonly ISerializer _serializer;

        public InMemorySaveService(ISerializer serializer) { _serializer = serializer; }

        public bool Exists(string key) => _store.ContainsKey(key);

        public void Save<T>(string key, T data) => _store[key] = _serializer.Serialize(data);

        public bool TryLoad<T>(string key, out T data)
        {
            if (_store.TryGetValue(key, out var text))
            {
                data = _serializer.Deserialize<T>(text);
                return data != null;
            }
            data = default;
            return false;
        }

        public void Delete(string key) => _store.Remove(key);

        /// <summary>Chuỗi JSON đang lưu (để test xem nội dung).</summary>
        public string RawJson(string key) => _store.TryGetValue(key, out var t) ? t : null;
    }
}
