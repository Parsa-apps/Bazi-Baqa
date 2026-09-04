using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// استخر شیء برای کاهش تخصیص‌های مکرر. از ساخت/نابودیِ مکرر جلوگیری می‌کند (مثلاً نشانگرِ ساخت
    /// یا افکت‌های پرتکرار). بدون وابستگی به MonoBehaviour بوده و برای رابط کاربری و جهان کاربرد دارد.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _available = new Stack<T>();
        private readonly Func<T> _factory;

        public ObjectPool(Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            _factory = factory;
        }

        /// <summary>گرفتن یک نمونه؛ اگر خالی بود، جدید می‌سازد.</summary>
        public T Get()
        {
            return _available.Count > 0 ? _available.Pop() : _factory();
        }

        /// <summary>برگرداندن نمونه برای استفاده‌ی دوباره.</summary>
        public void Release(T item)
        {
            if (item == null) return;
            _available.Push(item);
        }

        /// <summary>تعداد نمونه‌های نگه‌داری‌شده.</summary>
        public int Count { get { return _available.Count; } }

        /// <summary>
        /// خالی‌کردن کامل استخر و آزادسازی منابع (مثلاً هنگام نابودی سامانه‌ی والد).
        /// اگر نمونه‌ای در دستِ بیرون باشد، آن را نمی‌توان آزاد کرد؛ فقط مواردِ موجود آزاد می‌شوند.
        /// </summary>
        public void Clear(Action<T> onDispose)
        {
            while (_available.Count > 0)
            {
                T item = _available.Pop();
                if (item != null) onDispose?.Invoke(item);
            }
        }
    }
}
