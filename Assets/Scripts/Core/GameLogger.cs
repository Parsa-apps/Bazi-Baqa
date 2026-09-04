using System;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// ابزار خطایابی و پایداری بازی. همه‌ی منطق حساس (ذخیره، بارگذاری، رویدادها) از این مسیر عبور می‌کند
    /// تا هر خطای پیش‌بینی‌نشده ثبت شود و به‌جای کرش، یک پیام فارسی به بازیکن نمایش داده شود.
    /// دو سطح عمل دارد: (۱) ثبت در کنسول Unity و (۲) نمایش پیام فارسی در UI.
    /// </summary>
    public static class GameLogger
    {
        /// <summary>فایل‌های جانبی (مثل RuntimeLogger) برای همگام‌سازی لگ‌ها به این رویداد گوش می‌دهند.</summary>
        public static event Action<string> Logged;

        private const string InfoTag = "[بازی بقا]";
        private const string SaveTag = "[ذخیره]";
        private const string SystemTag = "[سیستم]";

        public static void Info(string message)
        {
            Debug.Log(InfoTag + " " + message);
        }

        /// <summary>لاگ مخصوص عملیات ذخیره‌سازی و بارگذاری.</summary>
        public static void Save(string message)
        {
            Debug.Log(SaveTag + " " + message);
        }

        /// <summary>لاگ مخصوص شروع، راه‌اندازی و رفتار سامانه‌های اصلی.</summary>
        public static void System(string message)
        {
            Debug.Log(SystemTag + " " + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(InfoTag + " " + message);
            GameEvents.Notify("هشدار: " + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(InfoTag + " " + message);
            GameEvents.Notify("خطا: " + message);
            Logged?.Invoke(message);
        }

        public static void Error(string message, Exception exception)
        {
            string detail = message + " — " + exception.Message;
            Debug.LogError(InfoTag + " " + detail);
            Debug.LogException(exception);
            GameEvents.Notify("خطا: " + message);
            Logged?.Invoke(detail);
        }

        public static T Try<T>(Func<T> action, T fallback, Func<string> label = null)
        {
            try
            {
                return action();
            }
            catch (Exception exception)
            {
                Error(label != null ? label() : "یک عملیات ناموفق بود", exception);
                return fallback;
            }
        }

        /// <summary>اجرای یک عملیات بدون بازگشت؛ در صورت خطا ثبت و به‌جای کرش، پیام فارسی نمایش داده می‌شود.</summary>
        public static void Try(Action action, Func<string> label = null)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Error(label != null ? label() : "یک عملیات ناموفق بود", exception);
            }
        }
    }
}
