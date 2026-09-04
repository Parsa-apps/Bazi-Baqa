using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// ابزار یافتن متن‌های سخت‌کدشده‌ی فارسی. چون هدف «حذف Hardcode Text» است، این ابزار
    /// همه‌ی رشته‌های فارسی را که مستقیماً در اسکریپت نوشته‌اند پیدا و گزارش می‌کند تا
    /// مرحله‌به‌مرحله به جدول بومی‌سازی منتقل شوند. اجرا: BaziBaqa/Audit/Find Hardcoded Persian Text.
    /// </summary>
    public static class LocalizationAudit
    {
        [MenuItem("BaziBaqa/Audit/Find Hardcoded Persian Text")]
        public static void Run()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("گزارش متن‌های سخت‌کدشده‌ی فارسی");
            sb.AppendLine("==============================");
            sb.AppendLine("این متن‌ها هنوز مستقیم در اسکریپت نوشته شده‌اند و باید به جدول بومی‌سازی منتقل شوند.");
            sb.AppendLine();

            int total = 0, fileCount = 0;
            foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                List<string> found = FindPersianLiterals(file);
                if (found.Count == 0) continue;
                fileCount++;
                total += found.Count;
                sb.AppendLine(file.Replace("\\", "/"));
                // فقط چند نمونه را نشان می‌دهیم تا گزارش خوانا بماند.
                int shown = found.Count > 5 ? 5 : found.Count;
                for (int i = 0; i < shown; i++) sb.AppendLine("    - " + found[i]);
                if (found.Count > shown) sb.AppendLine("    … و " + (found.Count - shown) + " مورد دیگر");
                sb.AppendLine();
            }

            sb.AppendLine("جمع کل: " + total + " رشته در " + fileCount + " فایل.");
            string path = "LocalizationAuditReport.txt";
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[BaziBaqa Localization] " + total + " رشته‌ی فارسی سخت‌کدشده در " + fileCount + " فایل یافت شد. گزارش: " + Path.GetFullPath(path) + "\n" + sb.ToString());
        }

        private static List<string> FindPersianLiterals(string file)
        {
            List<string> lines = new List<string>();
            string content;
            try { content = File.ReadAllText(file); } catch { return lines; }

            Regex regex = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"");
            foreach (Match m in regex.Matches(content))
            {
                string literal = m.Groups[1].Value;
                if (ContainsPersian(literal))
                {
                    string trimmed = literal.Replace("\n", "\\n");
                    if (trimmed.Length > 42) trimmed = trimmed.Substring(0, 39) + "…";
                    lines.Add("\"" + trimmed + "\"");
                }
            }
            return lines;
        }

        private static bool ContainsPersian(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= '\u0600' && c <= '\u06FF') return true;
            }
            return false;
        }
    }
}
