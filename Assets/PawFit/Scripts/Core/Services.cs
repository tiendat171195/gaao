using System;
using System.Collections.Generic;

namespace PawFit.Core
{
    /// <summary>
    /// Kho dịch vụ đơn giản: App (composition root) đăng ký các Service, View lấy ra dùng.
    /// Logic (Service) KHÔNG gọi ServiceLocator — nhận phụ thuộc qua constructor để dễ test.
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException("Chưa đăng ký service " + typeof(T).Name);
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s))
            {
                service = (T)s;
                return true;
            }
            service = null;
            return false;
        }

        public void Clear() => _services.Clear();
    }

    /// <summary>
    /// Kênh sự kiện: Service bắn sự kiện khi state đổi, View lắng nghe để vẽ lại.
    /// Chỉ dùng trên main thread.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>Đăng ký nghe sự kiện T. Gọi Dispose() trên kết quả để hủy đăng ký.</summary>
        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
            return new Subscription(() => list.Remove(handler));
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            // chép ra mảng để handler tự hủy đăng ký trong lúc chạy vẫn an toàn
            foreach (var d in list.ToArray()) ((Action<T>)d)(evt);
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }

    /// <summary>Ghi log không phụ thuộc Unity (App gắn UnityLog, test dùng NullLog).</summary>
    public interface ILog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public sealed class NullLog : ILog
    {
        public static readonly NullLog Instance = new NullLog();
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
