using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using BaziBaqa;
using TMPro;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// ابزار تایپوگرافی فارسی. سه کار انجام می‌دهد:
    ///   ۱) import TMP Essential Resources (بدون آن TextMeshPro هیچ متنی رندر نمی‌کند)؛
    ///  ۲) بیک کردن assetِ فونتِ Vazirmatn برای TMP (معمولی + ضخیم) در `Assets/Resources/Fonts/`
    ///      و ثبت به‌عنوان فونتِ پیش‌فرضِ TMP، تا همه‌ی متن‌های بازی با atlasِ SDF رندر شوند؛
    ///   ۳) اعتبارسنجی: پوششِ حروفِ جدولِ بومی‌سازی، includeFontData و در دسترس بودن assetها.
    /// اجرا: BaziBaqa &gt; Typography &gt; …  (همچنین از Tools/unity_validation.sh صدا زده می‌شود)
    /// </summary>
    public static class TypographyBaker
    {
        private const string FontFolder = "Assets/Resources/Fonts";
        private const string GlyphFile = FontFolder + "/PersianGlyphs.txt";
        private const string TableFile = "Assets/Resources/Localization/LocalizationTable.json";
        private const string ImportEssentialsMenu = "Window/TextMeshPro/Import TMP Essential Resources";

        private static readonly string[] SourceFonts = { "Assets/Resources/Fonts/Vazirmatn.ttf", "Assets/Resources/Fonts/Vazirmatn-Bold.ttf" };

        // atlasِ ۲۰۴۸ با sampling point ۹۰ ⇒ حروف فارسی در رزولوشن موبایل/تبلت لبه‌ی تیز دارند.
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 2048;
        private const int AtlasHeight = 2048;

        /// <summary>assetها با این نام‌ها در Resources ساخته می‌شوند؛ GameFont همان‌ها را بارگذاری می‌کند.</summary>
        private static readonly (string sourceFont, string assetPath)[] Targets =
        {
            ("Assets/Resources/Fonts/Vazirmatn.ttf", GameFont.TmpAssetPath),
            ("Assets/Resources/Fonts/Vazirmatn-Bold.ttf", GameFont.TmpBoldAssetPath),
        };

        [MenuItem("BaziBaqa/Typography/Bake Persian TMP Font Asset")]
        public static void BakeMenu()
        {
            Bake(true);
        }

        [MenuItem("BaziBaqa/Typography/Validate Typography Setup")]
        public static void ValidateMenu()
        {
            int problems = Validate(false);
            if (problems == 0)
            {
                EditorUtility.DisplayDialog("تایپوگرافی فارسی", "همه‌چیز آماده است: TMP Settings، assetِ فونت و پوششِ حروف.", "باشه");
            }
        }

        [MenuItem("BaziBaqa/Typography/Repair Font Import Settings")]
        public static void RepairImportSettings()
        {
            int changed = 0;
            for (int i = 0; i < SourceFonts.Length; i++)
            {
                if (!File.Exists(SourceFonts[i])) continue;
                TrueTypeFontImporter importer = AssetImporter.GetAtPath(SourceFonts[i]) as TrueTypeFontImporter;
                if (importer == null) continue;
                if (!importer.includeFontData || importer.characterPadding != AtlasPadding
                    || importer.fontRenderingMode != FontRenderingMode.HintedSmooth)
                {
                    changed++;
                }

                importer.includeFontData = true;                     // بدون این، TMP «Unable to load font face» می‌گیرد
                importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
                importer.characterPadding = AtlasPadding;             // جا برای خط‌کشی/سایه‌ی دور حروف فارسی
                importer.forceTextureCase = FontTextureCase.Dynamic;
                importer.SaveAndReimport();
            }

            Debug.Log("[BaziBaqa Typography] تنظیمات import فونت اصلاح شد (" + changed + " فایل).");
        }

        /// <summary>
        /// بیک کردن assetِ فونت. از `RuntimeValidation` هم صدا زده می‌شود تا در حالتِ batchmode
        /// (بدون دیالوگ) اجرا شود. خروجی: asset + material کنارش، و ثبتِ فونتِ پیش‌فرضِ TMP.
        /// </summary>
        public static bool Bake(bool showDialogs)
        {
            if (!EnsureEssentials(showDialogs)) return false;

            string glyphs = ReadGlyphCharacters();
            if (string.IsNullOrEmpty(glyphs))
            {
                Debug.LogError("[BaziBaqa Typography] مجموعه‌حروف پیدا نشد: " + GlyphFile);
                return false;
            }

            bool anyBaked = false;
            for (int i = 0; i < Targets.Length; i++)
            {
                string sourceFont = Targets[i].sourceFont;
                string assetPath = Targets[i].assetPath;
                if (!File.Exists(sourceFont))
                {
                    if (i > 0) continue;                              // نسخه‌ی ضخیم اختیاری است
                    Debug.LogError("[BaziBaqa Typography] فونتِ منبع نبود: " + sourceFont);
                    return false;
                }

                if (BakeOne(sourceFont, assetPath, glyphs)) anyBaked = true;
            }

            if (!anyBaked) return false;

            // TMP Settings: فونتِ پیش‌فرض تا هر TextMeshProUGUI تازه‌ساخته‌شده (از جمله پریفب‌های آینده) فارسی باشد.
            TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameFont.TmpAssetPath);
            if (TMP_Settings.instance != null && regular != null && TMP_Settings.instance.defaultFontAsset != regular)
            {
                TMP_Settings.instance.defaultFontAsset = regular;
                EditorUtility.SetDirty(TMP_Settings.instance);
                AssetDatabase.SaveAssets();
            }

            GameFont.ResetCache();
            AssetDatabase.Refresh();
            Debug.Log("[BaziBaqa Typography] assetِ فونتِ فارسی آماده است؛ بک‌اندِ فعلیِ رابط: " + GameTextBackend.ActiveBackendName);
            if (showDialogs)
            {
                EditorUtility.DisplayDialog("تایپوگرافی فارسی",
                    "assetِ فونتِ Vazirmatn برای TextMeshPro ساخته شد.\n\nبک‌اندِ فعلی: " + GameTextBackend.ActiveBackendName,
                    "باشه");
            }

            return true;
        }

        private static bool BakeOne(string sourceFontPath, string assetPath, string glyphs)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (source == null)
            {
                Debug.LogWarning("[BaziBaqa Typography] فونت بارگذاری نشد: " + sourceFontPath);
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                    source, SamplingPointSize, AtlasPadding, UnityEngine.TextCore.GlyphRenderMode.SDFAA,
                    AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic);
                if (asset == null)
                {
                    Debug.LogError("[BaziBaqa Typography] TMP_FontAsset.CreateFontAsset نال برگرداند برای " + sourceFontPath);
                    return false;
                }

                // حروفِ متصل‌شونده و ارقام فارسی از ابتدا در atlas باشند؛ حالتِ Dynamic فقط جا می‌گذارد
                // کاراکترهایِ تازه (مثلاً یک آیکن در آینده) هنگام نیاز اضافه شوند. پوششِ جدول را
                // ValidateGlyphCoverage و تستِ EditMode می‌سنجند، پس این‌جا نتیجه را دور می‌ریزیم.
                asset.TryAddCharacters(glyphs, true);

                asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                asset.name = Path.GetFileNameWithoutExtension(assetPath);
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[BaziBaqa Typography] ساخته شد: " + assetPath + " (" + glyphs.Length + " کاراکتر درخواستی)");
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[BaziBaqa Typography] بیک کردن ناموفق بود: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// اگر TMP Essential Resources import نشده باشد، همان لحظه import می‌کند.
        /// بدون این Assetها، TextMeshProUGUI هیچ‌چیز رندر نمی‌کند و کنسول پر از خطا می‌شود.
        /// </summary>
        public static bool EnsureEssentials(bool showDialogs)
        {
            if (TMP_Settings.instance != null) return true;

            Debug.Log("[BaziBaqa Typography] TMP Essential Resources import نشده؛ در حال import…");
            bool executed = false;
            try
            {
                executed = EditorApplication.ExecuteMenuItem(ImportEssentialsMenu);
            }
            catch (System.Exception)
            {
                executed = false;
            }
            AssetDatabase.Refresh();

            if (!executed || TMP_Settings.instance == null)
            {
                const string message = "متن‌های TextMeshPro فعال نیست چون «TMP Essential Resources» import نشده است. " +
                                       "از منوی Window &gt; TextMeshPro &gt; Import TMP Essential Resources استفاده کنید.";
                Debug.LogError("[BaziBaqa Typography] " + message);
                if (showDialogs) EditorUtility.DisplayDialog("TMP Essential Resources", message, "باشه");
                return false;
            }

            return true;
        }

        /// <summary>
        /// چهار بررسی: TMP Settings، assetها، includeFontData و پوششِ حروفِ جدول.
        /// تعدادِ مشکل‌ها را برمی‌گرداند (۰ یعنی همه‌چیز آماده)؛ `fix` تلاش می‌کند خودش درستش کند.
        /// </summary>
        public static int Validate(bool fix)
        {
            int problems = 0;

            if (!EnsureEssentials(false))
            {
                if (strict)
                {
                    problems++;
                }
                else
                {
                    Debug.LogWarning("[BaziBaqa Typography] TMP Essential Resources import نشده؛ رابط با فونتِ Vazirmatn روی Text قدیمی رندر می‌شود. برای فعال‌کردنِ مسیرِ TMP منوی Bake را در Editor اجرا کنید.");
                }
            }

            for (int i = 0; i < Targets.Length; i++)
            {
                string assetPath = Targets[i].assetPath;
                if (i > 0 && !File.Exists(assetPath)) continue;       // نسخه‌ی ضخیم اختیاری است
                if (!File.Exists(assetPath))
                {
                    if (strict)
                    {
                        Debug.LogError("[BaziBaqa Typography] assetِ فونت نبود: " + assetPath + " (منوی Bake را اجرا کنید)");
                        problems++;
                    }
                    else
                    {
                        Debug.LogWarning("[BaziBaqa Typography] assetِ TMP بیک نشده است: " + assetPath + "؛ در زمان اجرا از فونتِ داینامیک یا Text قدیمی استفاده می‌شود.");
                    }
                    continue;
                }

                TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (asset == null)
                {
                    Debug.LogError("[BaziBaqa Typography] assetِ فونت قابل بارگذاری نبود: " + assetPath);
                    problems++;
                }
                else if (asset.sourceFontFile == null)
                {
                    Debug.LogError("[BaziBaqa Typography] assetِ فونت به فونتِ منبع وصل نیست (includeFontData؟): " + assetPath);
                    problems++;
                }
            }

            for (int i = 0; i < SourceFonts.Length; i++)
            {
                if (!File.Exists(SourceFonts[i])) continue;
                TrueTypeFontImporter importer = AssetImporter.GetAtPath(SourceFonts[i]) as TrueTypeFontImporter;
                if (importer == null) continue;
                if (!importer.includeFontData)
                {
                    if (fix)
                    {
                        RepairImportSettings();
                    }
                    else
                    {
                        Debug.LogError("[BaziBaqa Typography] includeFontData خاموش است: " + SourceFonts[i]);
                        problems++;
                    }
                }
            }

            problems += ValidateGlyphCoverage();
            if (problems == 0) Debug.Log("[BaziBaqa Typography] ✓ تایپوگرافی فارسی کامل است: " + GameTextBackend.ActiveBackendName);
            return problems;
        }

        /// <summary>هر کاراکترِ غیرلاتینِ جدول باید در مجموعه‌حروف باشد؛ وگرنه در بازی «توفو» می‌شود.</summary>
        private static int ValidateGlyphCoverage()
        {
            string glyphs = ReadGlyphCharacters();
            if (string.IsNullOrEmpty(glyphs) || !File.Exists(TableFile))
            {
                Debug.LogError("[BaziBaqa Typography] فایلِ مجموعه‌حروف یا جدولِ بومی‌سازی پیدا نشد.");
                return 1;
            }

            HashSet<char> available = new HashSet<char>(glyphs);
            HashSet<char> used = new HashSet<char>();
            foreach (Match match in Regex.Matches(File.ReadAllText(TableFile), "\"value\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                string value = match.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"");
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] > 0x7E && !char.IsWhiteSpace(value[i])) used.Add(value[i]);
                }
            }

            List<char> missing = new List<char>();
            foreach (char character in used)
            {
                if (!available.Contains(character)) missing.Add(character);
            }

            if (missing.Count == 0) return 0;
            Debug.LogError("[BaziBaqa Typography] " + missing.Count + " کاراکترِ جدول در مجموعه‌حروف نیست: " +
                           Describe(new string(missing.ToArray())) + " → به " + GlyphFile + " بیفزایید و دوباره Bake کنید.");
            return 1;
        }

        private static string ReadGlyphCharacters()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GlyphFile);
            if (asset == null) return string.Empty;
            return GameFont.ParseGlyphText(asset);
        }

        private static string Describe(string missing)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < missing.Length && i < 16; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append('\'').Append(missing[i]).Append("' U+").Append(((int)missing[i]).ToString("X4"));
            }

            return builder.ToString();
        }
    }
}
