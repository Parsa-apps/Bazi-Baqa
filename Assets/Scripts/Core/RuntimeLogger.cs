using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// ثبت‌کننده‌ی زمان اجرا: همه‌ی خطاها، هشدارها و کرش‌های احتمالی Unity (از طریق
    /// Application.logMessageReceived) گرفته و همراه با تاریخچه در فایلی روی حافظه‌ی دستگاه
    /// نگهداری می‌شوند. این فایل به تیم کمک می‌کند تا مشکل را بدون نیاز به بازگشت از کاربر بازآفرینی کند.
    /// </summary>
    /// <summary>تسخیر خطاها بسیار زود انجام می‌شود تا هیچ لاگی از دست نرود.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class RuntimeLogger : MonoBehaviour
    {
        private const int MaxLines = 400;
        private const string LogFile = "bazi_baqa_log.txt";

        private readonly Queue<string> _buffer = new Queue<string>();
        private bool _registered;
        private string _path;

        public void Initialize()
        {
            if (_registered) return;
            _registered = true;
            _path = Path.Combine(Application.persistentDataPath, LogFile);
            try
            {
                // در شروع، محتوای قبلی را پاک می‌کنیم تا لاگِ هر جلسه تمیز باشد.
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (Exception exception)
            {
                GameLogger.Info("پاک‌سازی لاگ قبلی ناموفق: " + exception.Message);
            }
            Application.logMessageReceived += OnLogMessage;
            GameLogger.Info("ثبت‌کننده‌ی زمان اجرا فعال شد.");
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log) return; // فقط خطا و هشدار و کرش.
            WriteLine("[" + type + "] " + message + "\n" + stackTrace);
        }

        private void WriteLine(string line)
        {
            _buffer.Enqueue(line);
            if (_buffer.Count > MaxLines) _buffer.Dequeue();
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.AppendAllText(_path, line + "\n");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("نوشتن لاگ ناموفق بود: " + exception.Message);
            }
        }

        public void Flush()
        {
            try
            {
                if (string.IsNullOrEmpty(_path)) return;
                Directory.CreateDirectory(Application.persistentDataPath);
                File.AppendAllText(_path, "\n-- پایان جلسه --\n");
            }
            catch (Exception exception)
            {
                GameLogger.Info("ذخیره‌ی پایان جلسه ناموفق: " + exception.Message);
            }
        }

        public string GetLogPath() { return _path; }

        /// <summary>خروجی گرفتن لاگ‌های نگه‌داری‌شده برای اهداف تشخیصی.</summary>
        public string GetBuffered()
        {
            return string.Join("\n", _buffer.ToArray());
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                Application.logMessageReceived -= OnLogMessage;
                _registered = false;
            }
            Flush();
        }
    }
}
