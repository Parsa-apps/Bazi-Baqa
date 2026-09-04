using System;

namespace BaziBaqa
{
    /// <summary>
    /// گذرگاه رویدادهای سبک بازی. فقط دو نوع اشتراک‌گذاری وجود دارد تا وابستگی‌ها کم بماند:
    ///   • Notification — پیام کوتاهِ قابل‌مشاهده برای بازیکن (متن باید از بومی‌سازی بیاید).
    ///   • StateChanged — تغییر وضعیت کلی بازی؛ UI و سامانه‌های نمایشی خود را تازه می‌کنند.
    /// توجه: رویدادها را بیرون از این کلاس فقط می‌توان با += / -= عضو کرد و صادر کردنشان
    /// از طریق Notify / RaiseStateChanged انجام می‌شود (وگرنه Unity خطای CS0070 می‌دهد).
    /// </summary>
    public static class GameEvents
    {
        public static event Action<string> Notification;
        public static event Action StateChanged;

        public static void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Notification?.Invoke(message);
        }

        /// <summary>اعلام تغییر وضعیت بازی به مشترک‌ها (HUD و پنل‌ها).</summary>
        public static void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

        /// <summary>تعداد مشترک‌های فعال؛ ابزار عیب‌یابی نشتی رویداد در تست‌ها.</summary>
        public static int NotificationSubscriberCount
        {
            get { return Notification == null ? 0 : Notification.GetInvocationList().Length; }
        }

        public static int StateSubscriberCount
        {
            get { return StateChanged == null ? 0 : StateChanged.GetInvocationList().Length; }
        }

        /// <summary>پاک‌سازی همه‌ی اشتراک‌ها؛ فقط برای تست‌ها و بستن کامل بازی.</summary>
        public static void ClearSubscribers()
        {
            Notification = null;
            StateChanged = null;
        }
    }
}
