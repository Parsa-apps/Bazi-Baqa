using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// مدیریت‌کننده‌ی مرکزی متن‌های بازی. همه‌ی متن‌ها از جدول‌های خارجی (JSON) خوانده می‌شوند تا
    /// سخت‌کدکردن حذف شود و تغییر زبان در آینده ممکن باشد. تا وقتی جدول بارگذاری نشده، از
    /// «پیش‌فرض‌های داخلی» استفاده می‌شود تا همه‌ی سامانه‌ها همچنان کار کنند.
    /// </summary>
    public static class LocalizationManager
    {
        private const string PlayerPrefsLanguageKey = "bazi_baqa_language";
        private static readonly Dictionary<string, Dictionary<string, string>> _tables = new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<string, string> _defaults = new Dictionary<string, string>();
        private static string _language = "fa";
        private static string _defaultLanguage = "fa";
        private static bool _loaded;

        static LocalizationManager()
        {
            RegisterBuiltInDefaults();
        }

        public static string Language { get { return _language; } }
        public static string DefaultLanguage { get { return _defaultLanguage; } }
        public static bool IsRtl { get { return string.Equals(GetDirection(), "rtl", StringComparison.OrdinalIgnoreCase); } }
        public static bool Loaded { get { return _loaded; } }

        /// <summary>بارگذاری جدول از TextAsset که در Resources قرار دارد.</summary>
        public static void LoadFromTextAsset(TextAsset asset)
        {
            if (asset == null) return;
            LoadFromJson(asset.text);
        }

        /// <summary>تجزیه‌ی JSON جدول و پر کردن حافظه؛ اشتباه بدون خطا گزارش و ادامه می‌دهد.</summary>
        public static void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(json);
                if (table == null) return;
                if (!string.IsNullOrEmpty(table.defaultLanguage)) _defaultLanguage = table.defaultLanguage;
                if (_tables.Count == 0 && table.languages != null && table.languages.Count > 0)
                {
                    for (int i = 0; i < table.languages.Count; i++)
                    {
                        LocalizationLanguage lang = table.languages[i];
                        if (lang == null || string.IsNullOrEmpty(lang.language)) continue;
                        Dictionary<string, string> map = new Dictionary<string, string>();
                        if (!string.IsNullOrEmpty(lang.direction))
                            map["__direction"] = lang.direction;
                        if (lang.entries != null)
                        {
                            for (int e = 0; e < lang.entries.Count; e++)
                            {
                                LocalizationEntry entry = lang.entries[e];
                                if (entry != null && !string.IsNullOrEmpty(entry.key))
                                    map[entry.key] = entry.value ?? string.Empty;
                            }
                        }
                        _tables[lang.language] = map;
                    }
                }
                if (!_tables.ContainsKey(_language)) _language = _defaultLanguage;
                _loaded = true;
                GameLogger.Info("جدول بومی‌سازی بارگذاری شد؛ زبان فعلی: «" + _language + "».");
            }
            catch (Exception exception)
            {
                GameLogger.Info("بارگذاری جدول بومی‌سازی ناموفق بود: " + exception.Message);
            }
        }

        /// <summary>تغییر زبان و حفظ انتخاب بازیکن.</summary>
        public static void SetLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) return;
            _language = language;
            if (!_tables.ContainsKey(_language)) _language = _defaultLanguage;
            PlayerPrefs.SetString(PlayerPrefsLanguageKey, _language);
            PlayerPrefs.Save();
        }

        /// <summary>بازیابی زبان انتخاب‌شده‌ی قبلی؛ اگر نبود، زبان پیش‌فرض می‌ماند.</summary>
        public static void ApplySavedLanguage()
        {
            if (PlayerPrefs.HasKey(PlayerPrefsLanguageKey))
                SetLanguage(PlayerPrefs.GetString(PlayerPrefsLanguageKey));
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            // اول زبان فعلی، بعد زبان پیش‌فرض، بعد پیش‌فرض داخلی، و در نهایت خودِ کلید.
            if (_tables.TryGetValue(_language, out Dictionary<string, string> current) && current.TryGetValue(key, out string value))
                return value;
            if (!string.Equals(_language, _defaultLanguage, StringComparison.Ordinal)
                && _tables.TryGetValue(_defaultLanguage, out Dictionary<string, string> fallback) && fallback.TryGetValue(key, out value))
                return value;
            if (_defaults.TryGetValue(key, out value))
                return value;
            return key;
        }

        /// <summary>گرفتن متن و پر کردن جایگاه‌ها ({0}، {1}، …).</summary>
        public static string Get(string key, params object[] args)
        {
            string value = Get(key);
            if (args == null || args.Length == 0) return value;
            try { return string.Format(value, args); }
            catch (FormatException) { return value; }
        }

        /// <summary>ثبت یک پیش‌فرض (برای لغات پایه که همه‌جا لازم است).</summary>
        public static void Register(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return;
            _defaults[key] = fallback ?? string.Empty;
        }

        private static string GetDirection()
        {
            if (_tables.TryGetValue(_language, out Dictionary<string, string> lang) && lang.TryGetValue("__direction", out string dir))
                return dir;
            return "rtl";
        }

        // پیش‌فرض‌های فارسی برای لغات پایه، تا بدون بارگذاری جدول هم بازی درست کار کند.
        private static void RegisterBuiltInDefaults()
        {
            string[,] values = new string[,]
            {
                { "game.title", "سرزمین بقا" },
                { "game.tagline", "با همکاری، زنده می‌مانیم" },
                { "game.studio", "Parsa Apps" },
                { "game.director", "فرشاد پارسا" },
                { "game.loading", "در حال آماده‌سازی سرزمین بقا" },
                { "game.footer", "ساخته شده توسط Parsa Apps  •  مدیریت: فرشاد پارسا" },
                { "game.credits", "ساخته شده توسط\nParsa Apps\n\nمدیریت: فرشاد پارسا\n\nوب‌سایت رسمی:\nParsa-apps.github.io" },
                { "resource.wood", "چوب" },
                { "resource.stone", "سنگ" },
                { "resource.food", "غذا" },
                { "resource.gold", "طلا" },
                { "resource.energy", "انرژی" },
                { "resource.water", "آب" },
                { "building.camp", "اردوگاه" },
                { "building.house", "خانه" },
                { "building.storage", "انبار" },
                { "building.farm", "مزرعه" },
                { "building.watchtower", "برج دیده‌بانی" },
                { "building.workshop", "کارگاه" },
                { "building.wall", "دیوار دفاعی" },
                { "building.solarstation", "نیروگاه خورشیدی" },
                { "role.gatherer", "جمع‌آور" },
                { "role.builder", "سازنده" },
                { "role.medic", "پزشک" },
                { "role.guard", "نگهبان" },
                { "role.scout", "پیشاهنگ" },
                { "role.farmer", "کشاورز" },
                { "weather.clear", "آسمان صاف" },
                { "weather.rain", "بارانی" },
                { "weather.fog", "مه‌آلود" },
                { "weather.storm", "توفانی" }
            };
            for (int i = 0; i < values.GetLength(0); i++) Register(values[i, 0], values[i, 1]);
        }

        // ---------- ساختارهای JSON (قابل خواندن توسط JsonUtility) ----------

        [Serializable]
        public class LocalizationTable
        {
            public string defaultLanguage;
            public List<LocalizationLanguage> languages;
        }

        [Serializable]
        public class LocalizationLanguage
        {
            public string language;
            public string direction;
            public List<LocalizationEntry> entries;
        }

        [Serializable]
        public class LocalizationEntry
        {
            public string key;
            public string value;
        }
    }

    /// <summary>رابطِ کوتاه برای خواندن متن محلی‌شده در سراسر پروژه.</summary>
    public static class Loc
    {
        public static string Get(string key)
        {
            return LocalizationManager.Get(key);
        }

        public static string Get(string key, params object[] args)
        {
            return LocalizationManager.Get(key, args);
        }

        public static string Language { get { return LocalizationManager.Language; } }
        public static bool IsRtl { get { return LocalizationManager.IsRtl; } }
        public static void SetLanguage(string language) { LocalizationManager.SetLanguage(language); }
        public static void ApplySavedLanguage() { LocalizationManager.ApplySavedLanguage(); }
    }
}
