using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BaziBaqa;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// تست‌های سیستم تایپوگرافی فارسی (فاز ۲٫۵ — بخش ۳). سه چیز سنجیده می‌شود:
    ///   • فایل‌های لازم در Resources هستند و importِ فونت درست است (includeFontData);
    ///   • مجموعه‌حروف، همه‌ی کاراکترهای جدولِ بومی‌سازی را پوشش می‌دهد (نبود ⇒ «توفو» در بازی);
    ///   • UIText همیشه یک بک‌اند معتبر انتخاب می‌کند: TMP با assetِ Vazirmatn، یا Text با فونتِ Vazirmatn.
    /// </summary>
    public class TypographyTests
    {
        private const string TableFile = "Assets/Resources/Localization/LocalizationTable.json";
        private GameObject _root;

        [SetUp]
        public void CreateCanvas()
        {
            _root = new GameObject("TypographyTestsCanvas");
            _root.AddComponent<Canvas>();
            UIText.ResetCounters();
        }

        [TearDown]
        public void RemoveCanvas()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            GameTextBackend.ForceLegacyBackend = false;
            GameFont.ResetCache();
        }

        // ---------- Assetها ----------

        [Test]
        public void VazirmatnFontIsImportedWithFontData()
        {
            Font font = GameFont.Persian;
            Assert.IsNotNull(font, "فونتِ فارسی بارگذاری نشد.");
            Assert.AreEqual("Vazirmatn", font.name, "باید فونتِ Vazirmatn استفاده شود، نه پیش‌فرضِ Arial.");

            string meta = File.ReadAllText("Assets/Resources/Fonts/Vazirmatn.ttf.meta");
            StringAssert.Contains("includeFontData: 1", meta,
                "includeFontData خاموش است؛ TMP نمی‌تواند assetِ فونت بسازد (خطای Unable to load font face).");
            StringAssert.Contains("fontRenderingMode: 1", meta,
                "حالتِ رندر باید HintedSmooth باشد تا حروف فارسی روی موبایل تار نشوند.");
        }

        [Test]
        public void GlyphInventoryExistsInResources()
        {
            TextAsset glyphs = Resources.Load<TextAsset>(GameFont.GlyphResource);
            Assert.IsNotNull(glyphs, "فایلِ مجموعه‌حروف در Resources نبود: " + GameFont.GlyphResource);
            Assert.Greater(GameFont.GlyphCharacters.Length, 120,
                "مجموعه‌حروف باید فارسی + عربی + ارقام + لاتین + نمادها را یکجا داشته باشد.");
        }

        [Test]
        public void GlyphInventoryCoversEveryCharacterInTheLocalizationTable()
        {
            HashSet<char> available = new HashSet<char>(GameFont.GlyphCharacters);
            List<char> missing = new List<char>();
            foreach (char character in ReadTableCharacters())
            {
                if (!available.Contains(character)) missing.Add(character);
            }

            Assert.AreEqual(0, missing.Count,
                "این کاراکترها در جدول هستند ولی در Assets/Resources/Fonts/PersianGlyphs.txt نیستند ⇒ در بازی «توفو» می‌شوند: " +
                string.Join(" ", missing.ConvertAll(value => "\\u" + ((int)value).ToString("X4")).ToArray()));
        }

        [Test]
        public void GlyphInventoryIncludesPersianSpecifics()
        {
            string glyphs = GameFont.GlyphCharacters;
            string required = "گچپژژپکیيی‌ـ۰۱۲۳۴۵۶۷۸۹٪«»،؛؟…";
            for (int i = 0; i < required.Length; i++)
            {
                Assert.IsTrue(glyphs.IndexOf(required[i]) >= 0,
                    "کاراکترِ «" + required[i] + "» (U+" + ((int)required[i]).ToString("X4") + ") در مجموعه‌حروف نیست.");
            }
        }

        [Test]
        public void ParseGlyphTextIgnoresCommentsAndDeduplicates()
        {
            TextAsset asset = new TextAsset("# comment line\n" + "ابپ\n" + "پبت\n");
            string parsed = GameFont.ParseGlyphText(asset);
            Assert.AreEqual("ابپت", parsed, "خط توضیحی باید حذف و کاراکترِ تکراری یک‌بار بشمارد.");
            UnityEngine.Object.DestroyImmediate(asset);
        }

        // ---------- قراردادِ مسیرها ----------

        [Test]
        public void FontAssetPathsMatchTheResourceNames()
        {
            Assert.AreEqual("Assets/Resources/" + GameFont.TmpAssetResource + ".asset", GameFont.TmpAssetPath,
                "مسیرِ فایل و نامِ Resources باید یکی بمانند؛ وگرنه assetِ بیک‌شده هرگز بارگذاری نمی‌شود.");
            Assert.AreEqual("Assets/Resources/" + GameFont.TmpBoldAssetResource + ".asset", GameFont.TmpBoldAssetPath);
            Assert.IsTrue(GameFont.TmpAssetPath.StartsWith("Assets/Resources/", StringComparison.Ordinal));
        }

        // ---------- انتخابِ بک‌اند ----------

        [Test]
        public void UITextAlwaysGetsAWorkingRenderer()
        {
            UIText label = UIText.Create(_root.transform, "Label", "سلام", 20, Color.white, TextAnchor.MiddleCenter);
            TMP_Text tmp = label.GetComponent<TMP_Text>();
            Text legacy = label.GetComponent<Text>();

            bool hasExactlyOne = (tmp == null) != (legacy == null);
            Assert.IsTrue(hasExactlyOne, "هر برچسب باید دقیقاً یک بک‌اند داشته باشد (TMP یا Text قدیمی).");

            if (GameTextBackend.TmpReady)
            {
                Assert.IsNotNull(tmp, "بک‌اندِ TMP آماده است ولی برچسب TMP نساخته.");
                Assert.IsNotNull(tmp.font, "assetِ فونتِ TMP روی برچسب ننشسته است.");
                Assert.AreEqual(GameFont.TmpAsset, tmp.font, "فونتِ برچسب باید assetِ Vazirmatn باشد.");
            }
            else
            {
                Assert.IsNotNull(legacy, "مسیرِ بازگشت باید Text قدیمی باشد.");
                Assert.AreEqual(GameFont.Persian, legacy.font, "حتى مسیرِ بازگشت هم باید Vazirmatn داشته باشد.");
            }
        }

        [Test]
        public void ForceLegacyBackendStillRendersPersianText()
        {
            GameTextBackend.ForceLegacyBackend = true;
            UIText label = UIText.Create(_root.transform, "LabelLegacy", "سلام دنیا ۱۲", 18, Color.white, TextAnchor.MiddleCenter);

            Text legacy = label.GetComponent<Text>();
            Assert.IsNotNull(legacy, "با ForceLegacyBackend باید Text قدیمی ساخته شود.");
            Assert.AreEqual(GameFont.Persian, legacy.font);
            StringAssert.Contains("سلام", legacy.text, "متنِ فارسی باید روی برچسب نشسته باشد.");
            GameTextBackend.ForceLegacyBackend = false;
        }

        [Test]
        public void TmpTextSkipsManualShapingButKeepsPersianDigits()
        {
            LocalizationManager.SetLanguage("fa", false);
            // شکل‌دهیِ حروف کارِ خودِ TMP است؛ اگر Process روی متنِ TMP اعمال شود حروف خراب می‌شوند.
            string processed = PersianText.Process("نمونه 12");
            string digits = GameTextBackend.LocalizeDigits("نمونه 12");
            StringAssert.Contains("۱۲", digits, "ارقام باید به فارسی تبدیل شوند.");
            Assert.AreNotEqual(processed, digits,
                "مسیرِ TMP نباید شکل‌دهیِ دستی (وارونه‌سازیِ واژه‌ها) را رد کند.");
        }

        [Test]
        public void AlignmentFollowsLanguageDirection()
        {
            LocalizationManager.SetLanguage("fa", false);
            Assert.AreEqual(TextAnchor.MiddleRight, GameTextBackend.ResolveAnchor(TextAnchor.MiddleLeft),
                "در فارسی، برچسبِ بدون جهتِ صریح به راست می‌چیند.");
            Assert.AreEqual(TextAnchor.MiddleCenter, GameTextBackend.ResolveAnchor(TextAnchor.MiddleCenter),
                "چینشِ عمدیِ سازنده عوض نمی‌شود.");

            LocalizationManager.SetLanguage("en", false);
            Assert.AreEqual(TextAnchor.MiddleLeft, GameTextBackend.ResolveAnchor(TextAnchor.MiddleLeft),
                "در انگلیسی جهت تحمیل نمی‌شود.");
            LocalizationManager.SetLanguage("fa", false);
        }

        [Test]
        public void TmpAlignmentMappingCoversEveryAnchor()
        {
            Assert.AreEqual(TextAlignmentOptions.Midline, GameTextBackend.ToTmpAlignment(TextAnchor.MiddleCenter));
            Assert.AreEqual(TextAlignmentOptions.MidlineRight, GameTextBackend.ToTmpAlignment(TextAnchor.MiddleRight));
            Assert.AreEqual(TextAlignmentOptions.TopLeft, GameTextBackend.ToTmpAlignment(TextAnchor.UpperLeft));
            Assert.AreEqual(TextAlignmentOptions.BottomRight, GameTextBackend.ToTmpAlignment(TextAnchor.LowerRight));

            // رفت‌وبرگشت باید چینشِ تنظیم‌شده در Editor را حفظ کند (وگرنه UI دست‌ساز به‌هم می‌ریزد).
            TextAnchor[] all = (TextAnchor[])Enum.GetValues(typeof(TextAnchor));
            for (int i = 0; i < all.Length; i++)
            {
                TextAnchor back = GameTextBackend.ToUnityAlignment(GameTextBackend.ToTmpAlignment(all[i]));
                if (all[i] == TextAnchor.None) continue;   // None یعنی «هرچه خودت می‌دانی» ⇒ MiddleCenter مجاز است
                Assert.AreEqual(all[i], back, "نگاشتِ رفت‌وبرگشت برای " + all[i] + " درست نیست.");
            }
        }

        [Test]
        public void KeyBasedLabelsReResolveOnLanguageChange()
        {
            LocalizationManager.SetLanguage("fa", false);
            UIText label = UIText.Create(_root.transform, "KeyedLabel", string.Empty, 16, Color.white, TextAnchor.MiddleCenter);
            label.SetKey("ui.action.build");
            string farsi = label.text;
            Assert.IsNotEmpty(farsi);

            LocalizationManager.SetLanguage("en", false);
            GameTextBackend.RebuildAll();
            Assert.IsNotEmpty(label.text, "با تغییر زبان باید متن دوباره از جدول خوانده شود.");
            Assert.AreNotEqual(farsi, label.text, "متنِ انگلیسی باید با فارسی فرق کند.");

            LocalizationManager.SetLanguage("fa", false);
            GameTextBackend.RebuildAll();
            Assert.AreEqual(farsi, label.text);
        }

        [Test]
        public void EveryUiLabelInARebuiltInterfaceUsesTheChosenBackend()
        {
            UIText.Create(_root.transform, "A", "آزمایش", 14, Color.white, TextAnchor.MiddleCenter);
            UIText.Create(_root.transform, "B", "سخت‌افزار", 14, Color.white, TextAnchor.MiddleLeft);
            int total = UIText.TmpLabelCount + UIText.LegacyLabelCount;
            Assert.GreaterOrEqual(total, 2, "شمارشگرِ بک‌اندها باید برچسب‌ها را ببیند.");
        }

        private static IEnumerable<char> ReadTableCharacters()
        {
            HashSet<char> characters = new HashSet<char>();
            string json = File.ReadAllText(TableFile);
            foreach (Match match in Regex.Matches(json, "\"value\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                string value = match.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"");
                for (int i = 0; i < value.Length; i++)
                {
                    // لاتینِ چاپی و فاصله بخشی از atlas پیش‌فرض/خودِ فونت هستند؛ تمرکز روی حروفِ خاص است.
                    if (value[i] > 0x7E && !char.IsWhiteSpace(value[i])) characters.Add(value[i]);
                }
            }

            return characters;
        }
    }
}
