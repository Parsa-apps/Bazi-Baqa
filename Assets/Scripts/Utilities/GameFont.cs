using TMPro;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// تنها نقطه‌ی ورودِ فونت در بازی. دو بک‌اند را هماهنگ می‌کند:
    ///   • TextMeshPro (مسیر اصلی): assetِ فونتِ Vazirmatn SDF با atlas وکتوری؛
    ///   • Text قدیمیِ Unity UI (فقط مسیرِ بازگشت و برچسب‌های سه‌بعدی جهان): فونتِ TTF با includeFontData.
    /// چرا لایه‌بسته؟ چون ممکن است در یک کلونِ تازه هنوز TMP Essential Resources import نشده باشد؛
    /// در این حالت رابط نباید بی‌متن شود، پس همان متن با فونتِ Vazirmatn (Text قدیمی) رندر می‌شود.
    /// بیک کردنِ assetِ اصلی: منوی `BaziBaqa > Typography > Bake Persian TMP Font Asset`.
    /// </summary>
    public static class GameFont
    {
        /// <summary>نام assetِ فونتِ TMP در `Assets/Resources/Fonts/` (بدون پسوند).</summary>
        public const string TmpAssetResource = "Fonts/Vazirmatn SDF";

        /// <summary>مسیرِ فایلِ asset در پروژه؛ همان جایی که بیکر می‌نویسد.</summary>
        public const string TmpAssetPath = "Assets/Resources/Fonts/Vazirmatn SDF.asset";

        /// <summary>همراهِ ضخیمِ assetِ فونت (اختیاری؛ اگر بیک نشده باشد همان asset استفاده می‌شود).</summary>
        public const string TmpBoldAssetResource = "Fonts/Vazirmatn-Bold SDF";
        public const string TmpBoldAssetPath = "Assets/Resources/Fonts/Vazirmatn-Bold SDF.asset";

        /// <summary>فونتِ TTFِ منبع؛ هم برای مسیرِ قدیمی و هم برای بیک کردنِ assetِ TMP.</summary>
        public const string SourceFontResource = "Fonts/Vazirmatn";

        /// <summary>سبکِ ضخیم (Vazirmatn-Bold) برای بیکرِ Editor.</summary>
        public const string SourceBoldFontResource = "Fonts/Vazirmatn-Bold";

        /// <summary>متنِ مجموعه‌حروف (glyph set)؛ برای بیکر و برای دروازه‌ی پوششِ حروف.</summary>
        public const string GlyphResource = "Fonts/PersianGlyphs";

        private static Font _persian;
        private static TMP_FontAsset _tmpAsset;
        private static bool _tmpAttempted;
        private static bool _tmpLogged;
        private static string _glyphs;

        /// <summary>فونتِ TTF برای مسیرِ قدیمی (Text) و برچسب‌های سه‌بعدی TextMesh.</summary>
        public static Font Persian
        {
            get
            {
                if (_persian == null)
                {
                    _persian = Resources.Load<Font>(SourceFontResource);
                    if (_persian == null) _persian = Resources.Load<Font>("Fonts/DejaVuSans");
                    if (_persian == null) _persian = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                return _persian;
            }
        }

        /// <summary>
        /// assetِ فونتِ TMP. اول assetِ بیک‌شده بارگذاری می‌شود؛ اگر نبود، یک assetِ داینامیک از روی
        /// همان TTF ساخته می‌شود تا حروف فارسی در PlayMode هم درست دیده شوند (گلیف‌ها هنگام نیاز
        /// به atlas اضافه می‌شوند). اگر هیچ‌کدام ممکن نبود null برمی‌گردد و UIText به Text قدیمی برمی‌گردد.
        /// </summary>
        public static TMP_FontAsset TmpAsset
        {
            get
            {
                if (_tmpAttempted) return _tmpAsset;
                _tmpAttempted = true;

                if (GameTextBackend.TmpSettingsReady)
                {
                    _tmpAsset = Resources.Load<TMP_FontAsset>(TmpAssetResource);
                    if (_tmpAsset == null) _tmpAsset = CreateRuntimeFontAsset();
                }

                if (_tmpAsset == null && !_tmpLogged)
                {
                    _tmpLogged = true;
                    Debug.LogWarning("[BaziBaqa Typography] TMP font asset unavailable; UI falls back to the legacy Text renderer. " +
                                     "Run the menu 'BaziBaqa > Typography > Bake Persian TMP Font Asset' to enable the TextMeshPro path.");
                }

                return _tmpAsset;
            }
        }

        /// <summary>assetِ ضخیم اگر بیک شده باشد؛ در غیر این صورت null (همان assetِ معمولی استفاده می‌شود).</summary>
        public static TMP_FontAsset TmpBoldAsset
        {
            get
            {
                if (!GameTextBackend.TmpSettingsReady) return null;
                return Resources.Load<TMP_FontAsset>(TmpBoldAssetResource);
            }
        }

        /// <summary>آیا مسیرِ TMP آماده است؟ (TMP Settings + یک assetِ فونتِ قابل استفاده)</summary>
        public static bool UsingTextMeshPro
        {
            get { return TmpAsset != null && GameTextBackend.TmpSettingsReady; }
        }

        /// <summary>
        /// کاراکترهایی که باید در atlas باشند. فایلِ `Assets/Resources/Fonts/PersianGlyphs.txt`
        /// منبعِ واحد است تا دروازه‌ی پوششِ حروف و بیکر همیشه یک چیز را بخوانند.
        /// </summary>
        public static string GlyphCharacters
        {
            get
            {
                if (_glyphs == null) _glyphs = ParseGlyphText(Resources.Load<TextAsset>(GlyphResource));
                return _glyphs;
            }
        }

        /// <summary>خط‌های توضیحی (#) حذف و کاراکترهای تکراری یک‌بار می‌شوند.</summary>
        public static string ParseGlyphText(TextAsset asset)
        {
            if (asset == null) return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(asset.text.Length);
            bool[] seen = new bool[char.MaxValue + 1];
            string[] lines = asset.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                for (int c = 0; c < line.Length; c++)
                {
                    char character = line[c];
                    if (seen[character]) continue;
                    seen[character] = true;
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        /// <summary>assetِ داینامیک از روی TTF؛ فقط وقتی TMP Settings نصب باشد صدا زده می‌شود.</summary>
        private static TMP_FontAsset CreateRuntimeFontAsset()
        {
            Font source = Resources.Load<Font>(SourceFontResource);
            if (source == null) return null;

            try
            {
                // samplingPoint بزرگ + atlas 2048 ⇒ حروف فارسی روی موبایل تار نمی‌شوند.
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                    source, 90, 9, UnityEngine.TextCore.GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic);
                if (asset == null) return null;

                string glyphs = GlyphCharacters;
                if (glyphs.Length > 0) asset.TryAddCharacters(glyphs, true);
                asset.name = "Vazirmatn SDF (runtime)";
                return asset;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[BaziBaqa Typography] Could not build the dynamic TMP font asset: " + exception.Message);
                return null;
            }
        }

        /// <summary>پاک‌سازی کش؛ برای تست‌ها و بعد از import دوباره‌ی Assetها.</summary>
        public static void ResetCache()
        {
            _persian = null;
            _tmpAsset = null;
            _tmpAttempted = false;
            _tmpLogged = false;
            _glyphs = null;
        }
    }
}
