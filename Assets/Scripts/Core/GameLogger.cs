using System;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// ابزار خطایابی و پایداری بازی. همه‌ی منطق حساس (ذخیره، بارگذاری، رویدادها) از این مسیر عبور می‌کند
    /// تا هر خطای پیش‌بینی‌نشده ثبت شود و به‌جای کرش، یک پیام فارسی به بازیکن نمایش داده شود.
    /// </summary>
    public static class GameLogger
    {
        public static event Action<string> Logged;

        public static void Info(string message)
        {
            Debug.Log("[بازی بقا] " + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning("[بازی بقا] " + message);
            GameEvents.Notify("هشدار: " + message);
        }

        public static void Error(string message)
        {
            Debug.LogError("[بازی بقا] " + message);
            GameEvents.Notify("خطا: " + message);
        }

        public static void Error(string message, Exception exception)
        {
            string detail = message + " — " + exception.Message;
            Debug.LogError("[بازی بقا] " + detail);
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
    }
}
