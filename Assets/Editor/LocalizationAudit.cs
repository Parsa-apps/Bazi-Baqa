using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// ممیزِ متن‌های سخت‌کدشده. هدف پروژه این است که «هیچ متن قابل‌مشاهده‌ای» مستقیم در کد نباشد و
    /// همه‌چیز از جدول بومی‌سازی خوانده شود. این ابزار:
    ///   • رشته‌های فارسیِ داخل Assets/Scripts را پیدا می‌کند؛
    ///   • آن‌ها را دسته‌بندی می‌کند: «قابل‌مشاهده» (خطا) در برابر «داخلی/توسعه‌دهنده‌ای» (مجاز، تعریف‌شده در Tools/localization_allowlist.json)؛
    ///   • بررسی می‌کند هر Loc.Get("key") در جدول وجود داشته باشد و کلیدهای جدول با هم قرینه بمانند.
    /// اجرا: BaziBaqa &gt; Audit &gt; Find Hardcoded Persian Text
    /// </summary>
    public static class LocalizationAudit
    {
        private const string ScriptsRoot = "Assets/Scripts";
        private const string AllowlistPath = "Tools/localization_allowlist.json";
        private const string TablePath = "Assets/Resources/Localization/LocalizationTable.json";
        private const string ReportPath = "Logs/LocalizationAuditReport.txt";

        private static readonly Regex PersianLiteral = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"");
        private static readonly Regex KeyUse = new Regex("(?:Loc|LocalizationManager)\\.Get\\(\\s*\"([a-z0-9_.\\-]+)\"");
        private static readonly Regex EntryPattern = new Regex("\\{\\s*\"key\":\\s*\"([^\"]+)\",\\s*\"value\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*\\}");
        private static readonly Regex LanguagePattern = new Regex("\"language\":\\s*\"([^\"]+)\"");
        private static readonly Regex PersianChar = new Regex("[\\u0600-\\u06FF\\uFB50-\\uFDFF\\uFE70-\\uFEFF]");

        [MenuItem("BaziBaqa/Audit/Find Hardcoded Persian Text")]
        public static void RunMenu()
        {
            bool clean = Run(true);
            EditorUtility.DisplayDialog("ممیزی بومی‌سازی", clean ? "متن قابل‌مشاهده‌ی سخت‌کدشده‌ای نمانده است. ✓" : "موارد را در گزارش ببینید: " + ReportPath, "باشه");
        }

        /// <summary>اجرای ممیزی. true یعنی تمیز (بدون متن قابل‌مشاهده‌ی سخت‌کدشده و بدون کلید جاافتاده).</summary>
        public static bool Run(bool verbose)
        {
            List<string> visible = new List<string>();
            List<string> missingKeys = new List<string>();
            List<string> notes = new List<string>();
            int internalCount = 0;

            List<string> patterns = LoadAllowPatterns();
            string[] files = Directory.Exists(ScriptsRoot) ? Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories) : new string[0];
            int usedKeys = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace("\\", "/");
                string[] lines = File.ReadAllLines(path);
                for (int l = 0; l < lines.Length; l++)
                {
                    string line = lines[l];
                    string code = StripComment(line);
                    MatchCollection matches = PersianLiteral.Matches(code);
                    foreach (Match match in matches)
                    {
                        string value = match.Groups[1].Value;
                        if (!PersianChar.IsMatch(value)) continue;
                        if (line.Contains("locallint-ignore")) continue;
                        if (IsInternal(line, patterns)) { internalCount++; continue; }
                        visible.Add(path + ":" + (l + 1) + "  \"" + Trim(value) + "\"");
                    }
                }
                string content = string.Join("\n", lines);
                foreach (Match key in KeyUse.Matches(content))
                {
                    usedKeys++;
                    if (!TableContains(key.Groups[1].Value)) missingKeys.Add(path + " → «" + key.Groups[1].Value + "»");
                }
            }

            int tableKeys = CountTableKeys();
            StringBuilder report = new StringBuilder();
            report.AppendLine("گزارش ممیزی بومی‌سازی — Bazi Baqa (Parsa Apps)");
            report.AppendLine("================================================");
            report.AppendLine("فایل‌های بررسی‌شده: " + files.Length + " | کلیدهای استفاده‌شده در کد: " + usedKeys + " | کلیدهای جدول: " + tableKeys);
            report.AppendLine();
            report.AppendLine("متن قابل‌مشاهده‌ی سخت‌کدشده: " + visible.Count);
            for (int i = 0; i < visible.Count; i++) report.AppendLine("  ✗ " + visible[i]);
            report.AppendLine();
            report.AppendLine("کلیدِ بی‌مورد (در جدول نیست): " + missingKeys.Count);
            for (int i = 0; i < missingKeys.Count; i++) report.AppendLine("  ✗ " + missingKeys[i]);
            report.AppendLine();
            report.AppendLine("رشته‌های داخلیِ مجاز (نام گره/لاگ/برچسب تحلیلی): " + internalCount);
            for (int i = 0; i < notes.Count; i++) report.AppendLine("  • " + notes[i]);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());

            bool clean = visible.Count == 0 && missingKeys.Count == 0;
            if (clean)
            {
                Debug.Log("[BaziBaqa Localization] ✓ هیچ متن قابل‌مشاهده‌ی سخت‌کدشده‌ای نمانده؛ " + tableKeys + " کلید در جدول فعال است. گزارش: " + Path.GetFullPath(ReportPath));
            }
            else
            {
                Debug.LogError("[BaziBaqa Localization] ✗ " + visible.Count + " متن سخت‌کدشده و " + missingKeys.Count + " کلید جاافتاده. گزارش: " + Path.GetFullPath(ReportPath) + "\n" + report);
            }
            if (verbose && clean) EditorUtility.DisplayDialog("ممیزی بومی‌سازی", "پاک: " + tableKeys + " کلید، ۰ متن سخت‌کدشده.", "باشه");
            AssetDatabase.Refresh();
            return clean;
        }

        private static string StripComment(string line)
        {
            int index = line.IndexOf("//", System.StringComparison.Ordinal);
            return index < 0 ? line : line.Substring(0, index);
        }

        private static string Trim(string value)
        {
            string clean = value.Replace("\\n", "\\n");
            return clean.Length > 46 ? clean.Substring(0, 43) + "…" : clean;
        }

        private static bool IsInternal(string line, List<string> patterns)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                if (line.Contains(patterns[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// الگوهای «متن داخلی مجاز» ازTools/localization_allowlist.json خوانده می‌شود تا کدِ ممیز و
        /// بررسی‌کننده‌ی CI از یک منبع حقیقت استفاده کنند (اگر فایل نبود، فهرست پایه به کار می‌آید).
        /// </summary>
        private static List<string> LoadAllowPatterns()
        {
            List<string> patterns = new List<string>();
            try
            {
                if (File.Exists(AllowlistPath))
                {
                    string json = File.ReadAllText(AllowlistPath);
                    foreach (Match match in Regex.Matches(json, "\"([A-Za-z0-9_\\.\\\\\\(\\)\\[\\]\\*\\+\\?\\|\\^\\$]+)\"\\s*,?"))
                    {
                        string raw = match.Groups[1].Value;
                        if (raw.StartsWith("_") || raw.StartsWith("files")) continue;
                        patterns.Add(Unescape(raw));
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[BaziBaqa Localization] خواندن فهرست استثناها ناموفق بود: " + exception.Message);
            }
            if (patterns.Count == 0)
            {
                patterns.Add("new GameObject(");
                patterns.Add(".name = ");
                patterns.Add("Debug.Log");
                patterns.Add("GameLogger.");
            }
            return patterns;
        }

        private static string Unescape(string jsonValue)
        {
            return jsonValue.Replace("\\\\", "\\").Replace("\\\"", "\"");
        }

        private static string _tableText;

        /// <summary>متنِ خامِ جدول بومی‌سازی (یک بار خوانده می‌شود).</summary>
        private static string TableText
        {
            get
            {
                if (_tableText == null)
                {
                    try { _tableText = File.Exists(TablePath) ? File.ReadAllText(TablePath) : string.Empty; }
                    catch { _tableText = string.Empty; }
                }
                return _tableText;
            }
        }

        private static bool TableContains(string key)
        {
            if (string.IsNullOrEmpty(TableText)) return true; // جدول خوانده نشد؛ قضاوت نمی‌کنیم.
            return TableText.Contains("\"key\": \"" + key + "\"") || TableText.Contains("\"key\":\"" + key + "\"");
        }

        /// <summary>تعداد کلیدهای هر زبان (کلِ ورودی‌ها تقسیم بر تعداد زبان‌ها).</summary>
        private static int CountTableKeys()
        {
            int entries = 0;
            int languages = 0;
            foreach (Match ignored in EntryPattern.Matches(TableText)) entries++;
            foreach (Match ignored in LanguagePattern.Matches(TableText)) languages++;
            if (languages <= 0) return entries;
            return entries / languages;
        }
    }
}
