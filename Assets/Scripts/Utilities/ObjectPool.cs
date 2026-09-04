using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// یک استخر شیء ساده برای کاهش تخصیص‌های مکرر (مثل برچسب‌های شناور یا نشانگرها).
    /// این نسخه بدون وابستگی به MonoBehaviour و قابل استفاده در رابط کاربری و جهان است.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _available = new Stack<T>();
        private readonly System.Func<T> _factory;

        public ObjectPool(System.Func<T> factory)
        {
            _factory = factory;
        }

        public T Get()
        {
            return _available.Count > 0 ? _available.Pop() : _factory();
        }

        public void Release(T item)
        {
            if (item == null) return;
            _available.Push(item);
        }

        public int Count { get { return _available.Count; } }
    }
}
