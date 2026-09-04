using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// تصمیم‌گیرنده‌ی بک‌اندِ متن و نگاشتِ مفاهیم مشترک (چینش، فونت، آماده‌سازی).
    /// همه‌ی شرط‌های «آیا TMP قابل استفاده است؟» فقط همین‌جا جمع شده‌اند تا یک‌جا تغییر کنند.
    /// </summary>
    public static class GameTextBackend
    {
        /// <summary>کلیدِ PlayerPrefs برای آزمونِ دستیِ دو مسیر (۱ = فقط Text قدیمی).</summary>
        public const string BackendPreference = "bazi_baqa_text_backend";

        private static bool _settingsChecked;
        private static bool _settingsReady;

        /// <summary>با true، همه‌ی برچسب‌ها حتی با TMP موجود، با Text قدیمی رندر می‌شوند (فقط برای عیب‌یابی).</summary>
        public static bool ForceLegacyBackend { get; set; }

        static GameTextBackend()
        {
            ForceLegacyBackend = PlayerPrefs.GetInt(BackendPreference, 0) == 1;
        }

        /// <summary>
        /// آیا TMP Settings در پروژه import شده است؟ اگر «TMP Essential Resources» import نشده باشد،
        /// استفاده از TextMeshProUGUI فقط خطا می‌دهد و چیزی رندر نمی‌شود؛ پس مسیرِ قدیمی انتخاب می‌شود.
        /// </summary>
        public static bool TmpSettingsReady
        {
            get
            {
                if (!_settingsChecked)
                {
                    _settingsChecked = true;
                    try
                    {
                        _settingsReady = TMP_Settings.instance != null;
                    }
                    catch (System.Exception)
                    {
                        _settingsReady = false;
                    }
                }

                return _settingsReady;
            }
        }

        /// <summary>حالتِ نهاییِ بک‌اند (برای گزارش و تست).</summary>
        public static bool TmpReady
        {
            get { return !ForceLegacyBackend && TmpSettingsReady && GameFont.TmpAsset != null; }
        }

        /// <summary>نامِ بک‌اندِ فعال؛ در لاگِ شروع و در تست‌های PlayMode خوانده می‌شود.</summary>
        public static string ActiveBackendName
        {
            get { return TmpReady ? "TextMeshPro (Vazirmatn SDF)" : "UnityEngine.UI.Text (Vazirmatn TTF)"; }
        }

        /// <summary>
        /// اگر مسیرِ TMP قابل استفاده باشد true برمی‌گرداند و assetِ فونت را تحویل می‌دهد.
        /// در غیر این صورت false ⇒ فراخوان باید به Text قدیمی برگردد.
        /// </summary>
        public static bool Prepare(out TMP_FontAsset fontAsset)
        {
            fontAsset = TmpReady ? GameFont.TmpAsset : null;
            return fontAsset != null;
        }

        /// <summary>
        /// سیاستِ جهت: در زبان‌های راست‌به‌چپ، برچسب‌هایی که جهتِ صریح ندارند به راست می‌چینند
        /// (چینشِ عمدیِ سازنده دست‌نخورده می‌ماند). برای هر دو بک‌اند یکسان است.
        /// </summary>
        public static TextAnchor ResolveAnchor(TextAnchor requested)
        {
            if (!LocalizationManager.IsRtl) return requested;
            switch (requested)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                case TextAnchor.None:
                    return requested == TextAnchor.UpperLeft ? TextAnchor.UpperRight
                        : (requested == TextAnchor.LowerLeft ? TextAnchor.LowerRight : TextAnchor.MiddleRight);
                default:
                    return requested;
            }
        }

        /// <summary>
        /// ارقام را به قلمروِ زبان می‌برد. مسیرِ TMP شکل‌دهیِ حروف را خودش انجام می‌دهد،
        /// ولی تبدیلِ رقم (۱۲ در برابر 12) باید همان‌جا اعمال شود.
        /// </summary>
        public static string LocalizeDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return LocalizationManager.IsRtl ? GameClock.ToPersianDigits(value) : value;
        }

        /// <summary>بازگرداندنِ چینشِ TMP به TextAnchor (برای خواندنِ مقدارِ تنظیم‌شده در Editor).</summary>
        public static TextAnchor ToUnityAlignment(TextAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TextAlignmentOptions.TopLeft: return TextAnchor.UpperLeft;
                case TextAlignmentOptions.TopCenter: return TextAnchor.UpperCenter;
                case TextAlignmentOptions.TopRight: return TextAnchor.UpperRight;
                case TextAlignmentOptions.MidlineLeft: return TextAnchor.MiddleLeft;
                case TextAlignmentOptions.MidlineRight: return TextAnchor.MiddleRight;
                case TextAlignmentOptions.BottomLeft: return TextAnchor.LowerLeft;
                case TextAlignmentOptions.BottomCenter: return TextAnchor.LowerCenter;
                case TextAlignmentOptions.BottomRight: return TextAnchor.LowerRight;
                default: return TextAnchor.MiddleCenter;
            }
        }

        /// <summary>نگاشتِ TextAnchor به enumِ چینشِ TMP.</summary>
        public static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.TopCenter;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.BottomCenter;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                case TextAnchor.None:
                case TextAnchor.MiddleCenter:
                default: return TextAlignmentOptions.Midline;
            }
        }

        /// <summary>
        /// بازسازیِ همه‌ی برچسب‌های زنده (تغییر زبان، تعویض بک‌اند). `Resources.FindObjectsOfTypeAll`
        /// عمداً استفاده می‌شود تا برچسب‌های غیرفعال هم جا نمانند.
        /// </summary>
        public static void RebuildAll()
        {
            _settingsChecked = false;
            GameFont.ResetCache();
            UIText[] labels = UnityEngine.Object.FindObjectsOfType<UIText>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                // برچسب‌های کلید‌محور متن را دوباره از جدول می‌خوانند؛ بقیه فقط بازچینی می‌شوند.
                labels[i].Reload();
                labels[i].Apply();
            }
        }
    }
}
