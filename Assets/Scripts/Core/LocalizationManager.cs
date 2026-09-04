using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// مدیریت‌کننده‌ی مرکزی متن‌های بازی (Localization Manager).
    ///
    /// اصول طراحی:
    ///  • جدول بیرونی: همه‌ی متن‌ها در <c>Assets/Resources/Localization/LocalizationTable.json</c>
    ///    زندگی می‌کنند؛ در کد هیچ رشته‌ی قابل‌مشاهده‌ای وجود ندارد.
    ///  • کلیدمحور: هر متن با یک کلیدِ نقطه‌ای (مثل <c>hud.day</c>) خوانده می‌شود.
    ///  • چندزبانه: افزودن زبان تازه فقط یک ورودی در جدول است؛ <c>SetLanguage</c> همه‌چیز را عوض می‌کند.
    ///  • جهت متن: جدول برای هر زبان «direction» دارد (rtl/ltr) و <see cref="IsRtl"/> از همان می‌آید.
    ///  • لایه‌های بازگشت: زبان فعلی ← زبان پیش‌فرض ← مقدارِ قابل‌تشخیص + ثبت خطا (تا مشکل پنهان نماند).
    ///  • رویداد: <see cref="LanguageChanged"/> به متن‌های وابسته (LocalizedText / UIManager) خبر می‌دهد.
    /// </summary>
    public static class LocalizationManager
    {
        public const string TableResourcePath = "Localization/LocalizationTable";
        private const string PlayerPrefsLanguageKey = "bazi_baqa_language";

        private static readonly Dictionary<string, Dictionary<string, string>> _tables = new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<string, string> _directions = new Dictionary<string, string>();
        private static readonly HashSet<string> _reportedMissing = new HashSet<string>();
        private static readonly List<string> _missingKeys = new List<string>();

        private static string _language = "fa";
        private static string _defaultLanguage = "fa";
        private static bool _loaded;
        private static bool _autoLoadAttempted;

        /// <summary>با تغییر زبان، همه‌ی متن‌های وابسته باید خود را تازه کنند.</summary>
        public static event Action<string> LanguageChanged;

        public static string Language { get { return _language; } }
        public static string DefaultLanguage { get { return _defaultLanguage; } }
        public static bool Loaded { get { EnsureLoaded(); return _loaded; } }
        public static bool IsRtl { get { return string.Equals(Direction, "rtl", StringComparison.OrdinalIgnoreCase); } }
        public static int MissingKeyCount { get { return _missingKeys.Count; } }
        public static IReadOnlyList<string> MissingKeys { get { return _missingKeys; } }

        /// <summary>جهت متن زبان فعال (rtl یا ltr).</summary>
        public static string Direction
        {
            get
            {
                EnsureLoaded();
                string direction;
                if (_directions.TryGetValue(_language, out direction) && !string.IsNullOrEmpty(direction)) return direction;
                return string.Equals(_language, "fa", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr";
            }
        }

        /// <summary>زبان‌هایی که در جدول تعریف شده‌اند (به همان ترتیب فایل).</summary>
        public static IEnumerable<string> AvailableLanguages
        {
            get
            {
                EnsureLoaded();
                return _tables.Keys;
            }
        }

        /// <summary>زبانِ بعدی در فهرست؛ برای دکمه‌ی تغییر زبانِ تنظیمات. اگر یکی باشد null برمی‌گرداند.</summary>
        public static string NextLanguage(string current)
        {
            EnsureLoaded();
            List<string> list = new List<string>(_tables.Keys);
            if (list.Count < 2) return null;
            int index = list.IndexOf(current);
            if (index < 0) index = 0;
            return list[(index + 1) % list.Count];
        }

        /// <summary>نمایش نامِ زبان با خودِ زبان (عرفِ فهرست زبان‌ها: ترجمه نمی‌شود).</summary>
        public static string DisplayName(string language)
        {
            return Loc.Get("language." + language);
        }

        /// <summary>تعداد کلیدهای یک زبان (برای ممیزی و تست‌ها).</summary>
        public static int KeyCount(string language)
        {
            EnsureLoaded();
            Dictionary<string, string> table;
            return _tables.TryGetValue(language, out table) ? table.Count : 0;
        }

        // ---------- بارگذاری ----------

        /// <summary>بارگذاری جدول از TextAsset (مسیر Resources/Localization/LocalizationTable).</summary>
        public static void LoadFromTextAsset(TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("[BaziBaqa Localization] Localization table not found: " + TableResourcePath);
                return;
            }
            LoadFromJson(asset.text);
        }

        /// <summary>
        /// تجزیه‌ی JSON جدول و پر کردن حافظه. اگر تجزیه ناموفق بود، بازی اجرا می‌ماند ولی
        /// متن‌ها به مقدار قابل‌تشخیص برمی‌گردند و خطا در کنسول ثبت می‌شود.
        /// </summary>
        public static void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(json);
                if (table == null) return;
                if (!string.IsNullOrEmpty(table.defaultLanguage)) _defaultLanguage = table.defaultLanguage;
                if (table.languages != null && table.languages.Count > 0)
                {
                    _tables.Clear();
                    _directions.Clear();
                    for (int i = 0; i < table.languages.Count; i++)
                    {
                        LocalizationLanguage language = table.languages[i];
                        if (language == null || string.IsNullOrEmpty(language.language)) continue;
                        Dictionary<string, string> map = new Dictionary<string, string>();
                        if (language.entries != null)
                        {
                            for (int e = 0; e < language.entries.Count; e++)
                            {
                                LocalizationEntry entry = language.entries[e];
                                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                                map[entry.key] = entry.value ?? string.Empty;
                            }
                        }
                        _tables[language.language] = map;
                        _directions[language.language] = string.IsNullOrEmpty(language.direction) ? "ltr" : language.direction;
                    }
                }
                _missingKeys.Clear();
                _reportedMissing.Clear();
                _loaded = true;
                if (!_tables.ContainsKey(_language)) _language = _defaultLanguage;
                GameLogger.Info("Localization table loaded; language: " + _language + ", keys: " + KeyCount(_language) + ".");
                RaiseLanguageChanged();
            }
            catch (Exception exception)
            {
                Debug.LogError("[BaziBaqa Localization] Failed to parse the localization table: " + exception.Message);
            }
        }

        /// <summary>اگر هنوز جدولی بار نشده، یک‌بار از Resources بخوان (امنیت برای تست و صحنه‌های مستقیم).</summary>
        public static void EnsureLoaded()
        {
            if (_loaded || _autoLoadAttempted) return;
            _autoLoadAttempted = true;
            TextAsset asset = Resources.Load<TextAsset>(TableResourcePath);
            if (asset != null) LoadFromJson(asset.text);
            else Debug.LogError("[BaziBaqa Localization] No localization table at Resources/" + TableResourcePath + ".");
        }

        /// <summary>بازخوانی جدول (مثلاً پس از ویرایش در Editor).</summary>
        public static void Reload()
        {
            _loaded = false;
            _autoLoadAttempted = false;
            EnsureLoaded();
        }

        // ---------- زبان ----------

        /// <summary>تغییر زبان + ذخیره‌ی انتخاب بازیکن. زبانِ ناشناختی به پیش‌فرض برمی‌گردد.</summary>
        public static void SetLanguage(string language)
        {
            SetLanguage(language, true);
        }

        public static void SetLanguage(string language, bool persist)
        {
            if (string.IsNullOrEmpty(language)) return;
            EnsureLoaded();
            string resolved = _tables.ContainsKey(language) ? language : _defaultLanguage;
            bool changed = !string.Equals(resolved, _language, StringComparison.Ordinal);
            _language = resolved;
            if (persist)
            {
                PlayerPrefs.SetString(PlayerPrefsLanguageKey, _language);
                PlayerPrefs.Save();
            }
            if (changed) RaiseLanguageChanged();
        }

        /// <summary>بازیابی زبان انتخاب‌شده‌ی قبلی بازیکن.</summary>
        public static void ApplySavedLanguage()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsLanguageKey)) return;
            SetLanguage(PlayerPrefs.GetString(PlayerPrefsLanguageKey), false);
        }

        // ---------- خواندن متن ----------

        /// <summary>گرفتن متن با کلید. زبان فعلی ← زبان پیش‌فرض ← مقدار قابل‌تشخیص.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureLoaded();

            Dictionary<string, string> table;
            string value;
            if (_tables.TryGetValue(_language, out table) && table.TryGetValue(key, out value)) return value;
            if (!string.Equals(_language, _defaultLanguage, StringComparison.Ordinal)
                && _tables.TryGetValue(_defaultLanguage, out table) && table.TryGetValue(key, out value)) return value;

            ReportMissing(key);
            return UnresolvedMarker(key);
        }

        /// <summary>گرفتن متن و پر کردن جایگاه‌ها ({0}، {1}، …).</summary>
        public static string Get(string key, params object[] args)
        {
            string value = Get(key);
            if (args == null || args.Length == 0) return value;
            try { return string.Format(value, args); }
            catch (FormatException) { return value; }
            catch (Exception exception)
            {
                Debug.LogWarning("[BaziBaqa Localization] Format of key '" + key + "' does not match its arguments: " + exception.Message);
                return value;
            }
        }

        /// <summary>تلاش برای گرفتن متن؛ false یعنی کلید در هیچ زبانی نبود.</summary>
        public static bool TryGet(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            EnsureLoaded();
            Dictionary<string, string> table;
            if (_tables.TryGetValue(_language, out table) && table.TryGetValue(key, out value)) return true;
            if (_tables.TryGetValue(_defaultLanguage, out table) && table.TryGetValue(key, out value)) return true;
            return false;
        }

        public static bool Has(string key)
        {
            string ignored;
            return TryGet(key, out ignored);
        }

        /// <summary>عدد لاتین → عدد فارسی (یا لاتین در زبان‌های چپ‌به‌راست).</summary>
        public static string Number(int value)
        {
            return Number(value.ToString());
        }

        /// <summary>عدد اعشاری با گردکردن به نزدیک‌ترین صحیح.</summary>
        public static string Number(float value)
        {
            return Number((int)Mathf.Round(value));
        }

        public static string Number(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return IsRtl ? GameClock.ToPersianDigits(value) : value;
        }

        /// <summary>متنِ حل‌نشده (کلید خام) در UI؛ برای تست و ممیز.</summary>
        public static bool IsUnresolved(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.StartsWith("[loc:", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal);
        }

        /// <summary>پاک‌سازی وضعیت (فقط برای تست‌ها).</summary>
        public static void ResetForTests()
        {
            _tables.Clear();
            _directions.Clear();
            _missingKeys.Clear();
            _reportedMissing.Clear();
            _loaded = false;
            _autoLoadAttempted = false;
            _language = "fa";
            _defaultLanguage = "fa";
        }

        private static string UnresolvedMarker(string key)
        {
            return "[loc:" + key + "]";
        }

        private static void ReportMissing(string key)
        {
            if (_reportedMissing.Add(key))
            {
                _missingKeys.Add(key);
                Debug.LogError("[BaziBaqa Localization] Key '" + key + "' is missing from the table. Add it to "
                    + "Assets/Resources/Localization/LocalizationTable.json.");
            }
        }

        private static void RaiseLanguageChanged()
        {
            if (LanguageChanged != null) LanguageChanged(_language);
        }

        // ---------- ساختارهای JSON (قابل تجزیه توسط JsonUtility) ----------

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

    /// <summary>
    /// رابطِ کوتاه خواندن متن محلی‌شده در سراسر پروژه.
    /// همه‌ی متن‌های قابل‌مشاهده‌ی بازی باید از همین‌جا خوانده شوند.
    /// </summary>
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

        /// <summary>میان‌برِ خواندن عدد (با ارقام فارسی در زبان فارسی).</summary>
        public static string Num(int value)
        {
            return LocalizationManager.Number(value);
        }

        public static string Num(string value)
        {
            return LocalizationManager.Number(value);
        }

        public static string Num(float value)
        {
            return LocalizationManager.Number(value);
        }

        public static bool Has(string key) { return LocalizationManager.Has(key); }
        public static string Language { get { return LocalizationManager.Language; } }
        public static bool IsRtl { get { return LocalizationManager.IsRtl; } }
        public static string Direction { get { return LocalizationManager.Direction; } }
        public static void SetLanguage(string language) { LocalizationManager.SetLanguage(language); }
        public static void ApplySavedLanguage() { LocalizationManager.ApplySavedLanguage(); }
        public static event Action<string> LanguageChanged
        {
            add { LocalizationManager.LanguageChanged += value; }
            remove { LocalizationManager.LanguageChanged -= value; }
        }
    }
}
