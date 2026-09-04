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

        // برچسب‌های کنسول برای توسعه‌دهنده‌اند (در کنسول Unity خوانده می‌شوند)؛ بنابراین ASCII می‌مانند.
        private const string InfoTag = "[BaziBaqa]";
        private const string SaveTag = "[Save]";
        private const string SystemTag = "[System]";

        /// <summary>پیام‌های راه‌اندازی/جدول که پیش از آماده شدنِ Loc ثبت می‌شوند، باید بی‌خطر باشند.</summary>
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
            GameEvents.Notify(Loc.Get("log.warn_prefix") + " " + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(InfoTag + " " + message);
            GameEvents.Notify(Loc.Get("log.error_prefix") + " " + message);
            Logged?.Invoke(message);
        }

        public static void Error(string message, Exception exception)
        {
            string detail = message + " — " + exception.Message;
            Debug.LogError(InfoTag + " " + detail);
            Debug.LogException(exception);
            GameEvents.Notify(Loc.Get("log.error_prefix") + " " + message);
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
                Error(label != null ? label() : Loc.Get("log.generic_failure"), exception);
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
                Error(label != null ? label() : Loc.Get("log.generic_failure"), exception);
            }
        }
    }
}
