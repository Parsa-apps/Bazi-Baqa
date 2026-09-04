using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// ممیزی حرفه‌ای پروژه — پیش از انتشار یک اسکن ایستا و بدون مزاحمت انجام می‌دهد تا پروژه
    /// بدون خطا باز شود. موارد بررسی‌شده:
    ///   • صحنه‌های بیلد روی دیسک وجود دارند.
    ///   • هیچ مرجع اسکریپتِ ازدست‌رفته (broken script) در صحنه‌ها و پریفب‌ها نباشد.
    ///   • هیچ GUID تکراری در فایل‌های .meta نباشد.
    ///   • هیچ فایلِ بدون فایل .meta در شاخه‌ی Assets نباشد.
    ///   • ترتیب اجرای اسکریپت‌های حیاتی مشخص (DefaultExecutionOrder) باشد.
    /// این ابزار به‌صورت منو (BaziBaqa/Audit) و تابعی برای بیلد قابل فراخوانی است.
    /// </summary>
    public static class ProjectAudit
    {
        private const string Title = "[BaziBaqa Audit]";
        private static readonly string ProjectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");

        // کلاس‌های حیاتی که باید ترتیب اجرای صریح داشته باشند تا وابستگی‌ها در Awake درست برقرار شود.
        private static readonly string[] CriticalOrderedTypes =
        {
            "GameBootstrap", "GameManager", "UIManager", "RuntimeLogger", "PerformanceManager", "AudioManager", "WorldGenerator"
        };

        [MenuItem("BaziBaqa/Audit/Run Full Audit")]
        public static void RunFullAuditMenu()
        {
            RunFullAudit(false);
        }

        /// <summary>
        /// اجرای ممیزی کامل. خروجی bool یعنی آیا مشکل سخت (hard) وجود دارد یا نه؛
        /// مشکلات نرم فقط گزارش می‌شوند. از قبل از هر بیلد باید فراخوانی شود.
        /// </summary>
        public static bool RunFullAudit(bool verbose)
        {
            StringBuilder report = new StringBuilder();
            bool hard = false;

            hard |= CheckMissingMetas(report, verbose);
            hard |= CheckDuplicateGuids(report, verbose);
            hard |= CheckBuildScenes(report, verbose);
            hard |= CheckMissingScriptRefs(report, verbose);
            bool orderOk = CheckExecutionOrder(report, verbose);
            hard |= !orderOk;

            string text = report.ToString().Trim();
            if (hard)
            {
                Debug.LogError(Title + " ✗ مشکل سخت یافت شد.\n" + text);
            }
            else
            {
                Debug.Log(Title + " ✓ همه‌ی بررسی‌های سخت سالم بودند." + (text.Length > 0 ? "\n" + text : ""));
            }
            return !hard;
        }

        private static bool CheckMissingMetas(StringBuilder report, bool verbose)
        {
            bool hard = false;
            string[] files = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
            int missing = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i].Replace("\\", "/");
                if (f.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (!File.Exists(f + ".meta")) missing++;
            }
            if (missing > 0)
            {
                hard = true;
                report.AppendLine($"  ✗ {missing} فایل بدون .meta یافت شد (Unity هنگام باز شدن تولید می‌کند، اما بهتر است نسخه‌نگهداری شوند).");
            }
            else if (verbose)
            {
                report.AppendLine("  ✓ همه‌ی فایل‌های Assets دارای .meta هستند.");
            }
            return hard;
        }

        private static bool CheckDuplicateGuids(StringBuilder report, bool verbose)
        {
            bool hard = false;
            Dictionary<string, List<string>> owners = new Dictionary<string, List<string>>();
            string[] metas = Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories);
            for (int i = 0; i < metas.Length; i++)
            {
                string path = metas[i].Replace("\\", "/");
                string guid = ReadGuid(path);
                if (string.IsNullOrEmpty(guid)) continue;
                if (!owners.TryGetValue(guid, out List<string> list)) owners[guid] = list = new List<string>();
                list.Add(path);
            }
            foreach (KeyValuePair<string, List<string>> kv in owners)
            {
                if (kv.Value.Count > 1)
                {
                    hard = true;
                    report.AppendLine($"  ✗ GUID تکراری {kv.Key} در: {string.Join(" ، ", kv.Value)}");
                }
            }
            if (!hard && verbose) report.AppendLine("  ✓ هیچ GUID تکراری وجود ندارد.");
            return hard;
        }

        private static bool CheckBuildScenes(StringBuilder report, bool verbose)
        {
            bool hard = false;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                hard = true;
                report.AppendLine("  ✗ هیچ صحنه‌ای در Build Settings ثبت نشده است.");
                return hard;
            }
            int enabledCount = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene s = scenes[i];
                if (!s.enabled) continue;
                enabledCount++;
                string full = s.path;
                if (string.IsNullOrEmpty(full) || !File.Exists(full))
                {
                    hard = true;
                    report.AppendLine($"  ✗ صحنه‌ی فعال بیلد روی دیسک نیست: {full}");
                }
                else if (verbose)
                {
                    report.AppendLine($"  ✓ صحنه‌ی بیلد موجود است: {full}");
                }
            }
            if (enabledCount == 0)
            {
                hard = true;
                report.AppendLine("  ✗ هیچ صحنه‌ی فعالی در بیلد وجود ندارد.");
            }
            return hard;
        }

        /// <summary>
        /// بررسی static مرجع اسکریپت‌ها: برای هر صحنه و پریفب YAML، به‌دنبال
        /// m_Script با fileID: 11500000 می‌گردد و GUID آن را باید یک فایل .meta داشته باشد.
        /// اگر GUID حل نشود یعنی اسکریپت حذف/جابه‌جا شده و مرجع شکسته است.
        /// </summary>
        private static bool CheckMissingScriptRefs(StringBuilder report, bool verbose)
        {
            bool hard = false;
            Dictionary<string, string> metaDict = new Dictionary<string, string>();
            string[] metas = Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories);
            for (int i = 0; i < metas.Length; i++)
            {
                string p = metas[i].Replace("\\", "/");
                string guid = ReadGuid(p);
                if (!string.IsNullOrEmpty(guid) && !metaDict.ContainsKey(guid)) metaDict[guid] = p;
            }

            List<string> yamlFiles = new List<string>();
            yamlFiles.AddRange(Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories));
            yamlFiles.AddRange(Directory.GetFiles("Assets/Prefabs", "*.prefab", SearchOption.AllDirectories));

            HashSet<string> reported = new HashSet<string>();
            for (int f = 0; f < yamlFiles.Count; f++)
            {
                string yaml = yamlFiles[f].Replace("\\", "/");
                if (!File.Exists(yaml)) continue;
                string content = File.ReadAllText(yaml);
                // هر مظهرِ (سطر) m_Script: {fileID: 11500000, guid: XXXXXXXXX..., type: 3}
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    content,
                    @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*3\}");
                HashSet<string> seen = new HashSet<string>();
                for (int m = 0; m < matches.Count; m++)
                {
                    string guid = matches[m].Groups[1].Value.ToLowerInvariant();
                    if (seen.Contains(guid)) continue;
                    seen.Add(guid);
                    if (!metaDict.ContainsKey(guid))
                    {
                        if (reported.Add(guid))
                        {
                            hard = true;
                            report.AppendLine($"  ✗ مرجع اسکریپت شکسته (guid {guid}) در {yaml} — اسکریپت موجود نیست.");
                        }
                    }
                }
                if (verbose && seen.Count == 0)
                {
                    report.AppendLine($"  ? هیچ ارجاع اسکریپتی در {yaml} یافت نشد.");
                }
            }
            if (!hard && verbose) report.AppendLine("  ✓ هیچ مرجع اسکریپتِ شکسته در صحنه‌ها/پریفب‌ها وجود ندارد.");
            return hard;
        }

        private static bool CheckExecutionOrder(StringBuilder report, bool verbose)
        {
            bool all = true;
            for (int i = 0; i < CriticalOrderedTypes.Length; i++)
            {
                string name = CriticalOrderedTypes[i];
                string file = FindScriptFile(name);
                if (file == null)
                {
                    report.AppendLine($"  ? اسکریپت {name} پیدا نشد.");
                    continue;
                }
                string content = File.ReadAllText(file);
                if (!content.Contains("[DefaultExecutionOrder"))
                {
                    all = false;
                    report.AppendLine($"  ! {name} بدون [DefaultExecutionOrder] است؛ برای ترتیب اجرای قابل پیش‌بینی آن را تنظیم کنید.");
                }
                else if (verbose)
                {
                    report.AppendLine($"  ✓ {name} ترتیب اجرای صریح دارد.");
                }
            }
            return all;
        }

        private static string FindScriptFile(string className)
        {
            string[] files = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string content = File.ReadAllText(files[i].Replace("\\", "/"));
                if (content.Contains("class " + className)) return files[i].Replace("\\", "/");
            }
            return null;
        }

        private static string ReadGuid(string metaPath)
        {
            try
            {
                string[] lines = File.ReadAllLines(metaPath);
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (t.StartsWith("guid:", StringComparison.Ordinal))
                        return t.Substring("guid:".Length).Trim();
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
