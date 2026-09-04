using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// ممیزی اولیه‌ی کارایی: یافتن منبع تکراری، منبعِ به‌ظاهر بلااستفاده و راهنمای کاهش Draw Call.
    /// این ابزار یک بررسی ایستا (بدون اجرای بازی) انجام می‌دهد و گزارش می‌نویسد.
    /// اجرا: BaziBaqa/Audit/Performance &amp; Assets.
    /// </summary>
    public static class PerformanceAudit
    {
        private const string ReportPath = "PerformanceAuditReport.txt";

        [MenuItem("BaziBaqa/Audit/Performance & Assets")]
        public static void Run()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("گزارش بهینه‌سازی اولیه");
            sb.AppendLine("======================");
            sb.AppendLine();

            int duplicates = ReportDuplicateAssets(sb);
            int unused = ReportUnusedAssets(sb);
            sb.AppendLine("راهنمای کاهش Draw Call / حافظه:");
            sb.AppendLine("  • همه‌ی متن‌های UI باید از یک فونت و Material مشترک استفاده کنند تا Batching مؤثر باشد.");
            sb.AppendLine("  • تعداد Canvas را به حداقل برسانید و از Raycast Target غیرضروری روی متن‌ها پرهیز کنید.");
            sb.AppendLine("  • از Dynamic Batching (Material مشترک) برای سطوح زمین/ساختمان استفاده کنید.");
            sb.AppendLine("  • شیءهای پرتکرار را از ObjectPool بگیرید (مانند نشانگرِ ساخت که اکنون استخر می‌شود).");
            sb.AppendLine("  • از Instantiate/Destroy مکرر در حلقه پرهیز کنید؛ به‌جای آن فعال/غیرفعال (SetActive) کنید.");
            sb.AppendLine();

            sb.AppendLine("جمع: " + duplicates + " منبع تکراری، " + unused + " منبعِ به‌ظاهر بلااستفاده.");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[BaziBaqa Performance] گزارش کارایی نوشته شد: " + Path.GetFullPath(ReportPath) + "\n" + sb.ToString());
        }

        private static int ReportDuplicateAssets(StringBuilder sb)
        {
            // هش محتوایی فایل‌ها را می‌گیریم؛ فایل‌های .meta را رد می‌کنیم.
            Dictionary<string, List<string>> byHash = new Dictionary<string, List<string>>();
            string[] files = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
            int duplicates = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i].Replace("\\", "/");
                if (f.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (f.EndsWith("/", StringComparison.Ordinal)) continue;
                long len = new FileInfo(f).Length;
                if (len == 0) continue;
                string hash;
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                using (FileStream stream = File.OpenRead(f))
                {
                    byte[] digest = sha.ComputeHash(stream);
                    hash = BitConverter.ToString(digest).Replace("-", "");
                }
                if (!byHash.TryGetValue(hash, out List<string> list)) byHash[hash] = list = new List<string>();
                list.Add(f);
            }
            foreach (KeyValuePair<string, List<string>> kv in byHash)
            {
                if (kv.Value.Count > 1)
                {
                    duplicates += kv.Value.Count - 1;
                    sb.AppendLine("  • منبع تکراری (محتوا یکسان):");
                    for (int i = 0; i < kv.Value.Count; i++) sb.AppendLine("      - " + kv.Value[i]);
                }
            }
            if (duplicates == 0) sb.AppendLine("  ✓ هیچ منبع تکراری (با محتوای یکسان) یافت نشد.");
            return duplicates;
        }

        private static int ReportUnusedAssets(StringBuilder sb)
        {
            // منبع‌هایی که در هیچ صحنه/اسکریپت/json یا مسیر Resources ظاهر نشده‌اند، «به‌ظاهر بلااستفاده» هستند.
            HashSet<string> referenced = new HashSet<string>();
            foreach (string scene in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
                CollectText(File.ReadAllText(scene), referenced);
            foreach (string cs in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                CollectText(File.ReadAllText(cs), referenced);
            foreach (string json in Directory.GetFiles("Assets", "*.json", SearchOption.AllDirectories))
                CollectText(File.ReadAllText(json), referenced);

            // برای نام‌گذاریهایی که در Resources.Load با نام (بدون پسوند) اشاره می‌شود، نام فایل را هم می‌سنجیم.
            int unused = 0;
            string[] files = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i].Replace("\\", "/");
                if (f.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (!IsAuditableAsset(f)) continue;
                string nameNoExt = Path.GetFileNameWithoutExtension(f);
                if (referenced.Contains(f)) continue;
                if (referenced.Contains(nameNoExt)) continue;
                if (f.StartsWith("Assets/Resources/", StringComparison.Ordinal)) continue; // ناحیه‌ی Resources با نام بارگذاری می‌شود.
                unused++;
                sb.AppendLine("  • به‌ظاهر بلااستفاده: " + f);
            }
            return unused;
        }

        private static bool IsAuditableAsset(string path)
        {
            if (path.StartsWith("Assets/Editor/", StringComparison.Ordinal)) return false; // ابزار Editor
            if (path.StartsWith("Assets/Scripts/", StringComparison.Ordinal)) return false;
            if (path.StartsWith("Assets/Tests/", StringComparison.Ordinal)) return false;
            if (path.StartsWith("Assets/Scenes/", StringComparison.Ordinal)) return false; // صحنه‌ها رسما در بیلد هستند
            if (path.EndsWith(".unity", StringComparison.Ordinal)) return false;
            if (path.EndsWith(".asmdef", StringComparison.Ordinal)) return false;
            if (path.EndsWith(".json", StringComparison.Ordinal)) return false;
            return true;
        }

        private static void CollectText(string content, HashSet<string> set)
        {
            // هم مسیرها و هم نام‌های بدون پسوند را برای یافتن ارجاع در نظر می‌گیریم.
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim().Trim(' ', '"');
                if (trimmed.Length > 8) set.Add(trimmed);
            }
        }
    }
}
