using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// کمک کوچک و بدون وابستگی برای نمایش متن فارسی در UI سبک بازی.
    /// در محصول نهایی می‌توان این لایه را با atlas فارسی TextMeshPro جایگزین کرد.
    /// </summary>
    public sealed class PersianText : MonoBehaviour
    {
        [TextArea]
        public string sourceText;
        public bool processOnEnable = true;

        private Text _label;

        private void Awake()
        {
            _label = GetComponent<Text>();
        }

        private void OnEnable()
        {
            if (processOnEnable)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_label == null)
            {
                _label = GetComponent<Text>();
            }

            if (_label != null)
            {
                ApplyDirection(_label);
                _label.text = Process(sourceText);
            }
        }

        public static void Set(Text label, string value)
        {
            if (label == null) return;
            ApplyDirection(label);
            label.text = Process(value);
        }

        /// <summary>
        /// چینشِ راست‌به‌چپ فقط برای زبان‌های راست‌به‌چپ تحمیل می‌شود و فقط جایی که
        /// فرستنده جهت را مشخص نکرده (چپ/پیش‌فرض) اصلاح می‌شود؛ چینشِ عمدی (مثلاً MiddleCenter)
        /// دست‌نخورده می‌ماند.
        /// </summary>
        private static void ApplyDirection(Text label)
        {
            if (!LocalizationManager.IsRtl) return;
            TextAnchor anchor = label.alignment;
            if (anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft || anchor == TextAnchor.None)
            {
                label.alignment = TextAnchor.MiddleRight;
            }
        }

        /// <summary>
        /// نمایش متن در Text قدیمیِ Unity (شکل‌دهی دستیِ حروف + ارقام فارسی).
        /// مخصوص لایه‌ی قدیمی است؛ TextMeshPro خودش شکل‌دهی و RTL را انجام می‌دهد و
        /// نباید از این تابع استفاده کند (وگرنه حروف دوبار برعکس می‌شوند).
        /// </summary>
        public static string LegacyDisplay(string value)
        {
            return Process(value);
        }

        public static string Process(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // Unity Text در بعضی نسخه‌ها الگوریتم دوطرفه‌ی فارسی را کامل اجرا نمی‌کند.
            // هر واژه‌ی فارسی را به شکل دیداری می‌چینیم و گروه‌های عددی را دست‌نخورده نگه می‌داریم.
            string[] words = value.Split(new[] { ' ' }, System.StringSplitOptions.None);
            StringBuilder result = new StringBuilder(value.Length + 8);
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0) result.Append(' ');
                result.Append(ProcessWord(words[i]));
            }
            // در زبان‌های راست‌به‌چپ ارقام فارسی خوانایی را بالا می‌برد؛ در چپ‌به‌راست دست نمی‌زنیم.
            string shaped = result.ToString();
            return LocalizationManager.IsRtl ? GameClock.ToPersianDigits(shaped) : shaped;
        }

        private static string ProcessWord(string word)
        {
            if (string.IsNullOrEmpty(word) || IsNumberOnly(word)) return word;

            bool hasRtl = false;
            for (int i = 0; i < word.Length; i++)
            {
                if (IsArabic(word[i]))
                {
                    hasRtl = true;
                    break;
                }
            }
            if (!hasRtl) return word;

            string shaped = ShapeArabic(word);
            char[] chars = shaped.ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }

        private static bool IsNumberOnly(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsDigit(c) && c != ':' && c != '/' && c != '-' && c != '٫' && c != '٬') return false;
            }
            return true;
        }

        private static bool IsArabic(char c)
        {
            return (c >= '\u0600' && c <= '\u06FF') || (c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF');
        }

        private static string ShapeArabic(string word)
        {
            StringBuilder shaped = new StringBuilder(word.Length);
            for (int i = 0; i < word.Length; i++)
            {
                char current = word[i];
                if (!ArabicForms.TryGetValue(current, out ArabicForm form))
                {
                    shaped.Append(current);
                    continue;
                }

                bool joinsPrevious = i > 0 && CanJoinAfter(word[i - 1]) && form.ConnectsToPrevious;
                bool joinsNext = i < word.Length - 1 && form.ConnectsToNext && CanJoinBefore(word[i + 1]);
                shaped.Append(joinsPrevious ? (joinsNext ? form.Medial : form.Final) : (joinsNext ? form.Initial : form.Isolated));
            }
            return shaped.ToString();
        }

        private static bool CanJoinAfter(char c)
        {
            return ArabicForms.TryGetValue(c, out ArabicForm form) && form.ConnectsToNext;
        }

        private static bool CanJoinBefore(char c)
        {
            return ArabicForms.TryGetValue(c, out ArabicForm form) && form.ConnectsToPrevious;
        }

        private struct ArabicForm
        {
            public char Isolated;
            public char Final;
            public char Initial;
            public char Medial;
            public bool ConnectsToPrevious;
            public bool ConnectsToNext;

            public ArabicForm(char isolated, char final, char initial, char medial, bool previous, bool next)
            {
                Isolated = isolated;
                Final = final;
                Initial = initial;
                Medial = medial;
                ConnectsToPrevious = previous;
                ConnectsToNext = next;
            }
        }

        private static readonly Dictionary<char, ArabicForm> ArabicForms = new Dictionary<char, ArabicForm>
        {
            { 'ا', new ArabicForm('\uFE8D', '\uFE8E', '\uFE8D', '\uFE8E', true, false) },
            { 'آ', new ArabicForm('\uFE81', '\uFE82', '\uFE81', '\uFE82', true, false) },
            { 'ب', new ArabicForm('\uFE8F', '\uFE90', '\uFE91', '\uFE92', true, true) },
            { 'پ', new ArabicForm('\uFB56', '\uFB57', '\uFB58', '\uFB59', true, true) },
            { 'ت', new ArabicForm('\uFE95', '\uFE96', '\uFE97', '\uFE98', true, true) },
            { 'ث', new ArabicForm('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C', true, true) },
            { 'ج', new ArabicForm('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0', true, true) },
            { 'چ', new ArabicForm('\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D', true, true) },
            { 'ح', new ArabicForm('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4', true, true) },
            { 'خ', new ArabicForm('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8', true, true) },
            { 'د', new ArabicForm('\uFEA9', '\uFEAA', '\uFEA9', '\uFEAA', true, false) },
            { 'ذ', new ArabicForm('\uFEAB', '\uFEAC', '\uFEAB', '\uFEAC', true, false) },
            { 'ر', new ArabicForm('\uFEAD', '\uFEAE', '\uFEAD', '\uFEAE', true, false) },
            { 'ز', new ArabicForm('\uFEAF', '\uFEB0', '\uFEAF', '\uFEB0', true, false) },
            { 'ژ', new ArabicForm('\uFB8A', '\uFB8B', '\uFB8A', '\uFB8B', true, false) },
            { 'س', new ArabicForm('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4', true, true) },
            { 'ش', new ArabicForm('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8', true, true) },
            { 'ص', new ArabicForm('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC', true, true) },
            { 'ض', new ArabicForm('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0', true, true) },
            { 'ط', new ArabicForm('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4', true, true) },
            { 'ظ', new ArabicForm('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8', true, true) },
            { 'ع', new ArabicForm('\uFEC9', '\uFECA', '\uFECB', '\uFECC', true, true) },
            { 'غ', new ArabicForm('\uFECD', '\uFECE', '\uFECF', '\uFED0', true, true) },
            { 'ف', new ArabicForm('\uFED1', '\uFED2', '\uFED3', '\uFED4', true, true) },
            { 'ق', new ArabicForm('\uFED5', '\uFED6', '\uFED7', '\uFED8', true, true) },
            { 'ک', new ArabicForm('\uFB8E', '\uFB8F', '\uFB90', '\uFB91', true, true) },
            { 'ك', new ArabicForm('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC', true, true) },
            { 'گ', new ArabicForm('\uFB92', '\uFB93', '\uFB94', '\uFB95', true, true) },
            { 'ل', new ArabicForm('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0', true, true) },
            { 'م', new ArabicForm('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4', true, true) },
            { 'ن', new ArabicForm('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8', true, true) },
            { 'ه', new ArabicForm('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC', true, true) },
            { 'و', new ArabicForm('\uFEED', '\uFEEE', '\uFEED', '\uFEEE', true, false) },
            { 'ی', new ArabicForm('\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF', true, true) },
            { 'ي', new ArabicForm('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4', true, true) },
            { 'ء', new ArabicForm('\uFE80', '\uFE80', '\uFE80', '\uFE80', false, false) },
            { 'ئ', new ArabicForm('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C', true, true) },
            { 'ؤ', new ArabicForm('\uFE85', '\uFE86', '\uFE85', '\uFE86', true, false) }
        };
    }
}
