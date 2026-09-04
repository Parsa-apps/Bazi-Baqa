using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// تنها راهِ نوشتنِ متن در رابطِ بازی. یک لایه‌ی نازک روی سه حالت:
    ///   • TextMeshProUGUI + assetِ فونتِ Vazirmatn SDF (مسیرِ اصلی؛ شکل‌دهیِ حروف، bidi و کرنینگ را خودِ TMP انجام می‌دهد)؛
    ///   • Text قدیمی با فونتِ TTF (فقط وقتی TMP آماده نیست) تا رابط هرگز بی‌متن نماند؛
    ///   • یک کامپوننتِ TMP سه‌بعدی که از قبل روی گیم‌ابجکت هست (متنِ چیده‌شده در Editor)؛ فقط .text تازه می‌شود.
    /// کدِ بازی هیچ‌وقت مستقیم `Text` یا `TextMeshProUGUI` نمی‌سازد؛ فقط همین فایل و PersianText این کار را
    /// می‌کنند و `Tools/project_lint.py` قانون را می‌گیرد.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIText : MonoBehaviour
    {
        private TextMeshProUGUI _tmp;
        private Text _legacy;
        private TMP_Text _external;
        private string _value = string.Empty;
        private string _key;
        private object[] _arguments;
        private TextAnchor _anchor = TextAnchor.MiddleCenter;
        private float _size = 16f;
        private Color _color = Color.white;
        private bool _bold;
        private bool _wordWrap = true;
        private bool _autoFit = true;
        private bool _raycast;
        private bool _adopted;

        /// <summary>شمارشگرِ بک‌اندها؛ برای تست و گزارشِ وضعیتِ تایپوگرافی.</summary>
        public static int TmpLabelCount { get; private set; }
        public static int LegacyLabelCount { get; private set; }

        public static void ResetCounters()
        {
            TmpLabelCount = 0;
            LegacyLabelCount = 0;
        }

        /// <summary>یک برچسب تازه روی `parent` می‌سازد و بک‌اند مناسب را انتخاب می‌کند.</summary>
        public static UIText Create(Transform parent, string name, string value, float size, Color color, TextAnchor anchor)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            UIText label = labelObject.AddComponent<UIText>();
            label._size = size;
            label._color = color;
            label._anchor = anchor;
            label.Set(value, false);
            label.Apply();
            return label;
        }

        /// <summary>اگر برچسبی از قبل روی گیم‌ابجکت هست (صحنه‌ی دست‌ساز)، همین‌جا وصل می‌شود.</summary>
        public static UIText Attach(GameObject target)
        {
            UIText label = target.GetComponent<UIText>();
            return label != null ? label : target.AddComponent<UIText>();
        }

        public string text { get { return _value; } set { Set(value); } }
        public float fontSize { get { return _size; } set { _size = Mathf.Max(1f, value); Apply(); } }
        public Color color { get { return _color; } set { _color = value; Apply(); } }
        public TextAnchor alignment { get { return _anchor; } set { _anchor = value; Apply(); } }
        public bool bold { get { return _bold; } set { _bold = value; Apply(); } }
        public bool wordWrap { get { return _wordWrap; } set { _wordWrap = value; Apply(); } }
        public bool autoFit { get { return _autoFit; } set { _autoFit = value; Apply(); } }

        /// <summary>کلیدِ بومی‌سازیِ آخرین متن (null یعنی متنِ مستقیم داده شده است).</summary>
        public string Key { get { return _key; } }

        public Graphic Target
        {
            get { return _tmp != null ? (Graphic)_tmp : (Graphic)_legacy; }
        }

        public RectTransform Rect
        {
            get { return (RectTransform)transform; }
        }

        /// <summary>متنِ بازی استفاده می‌کند: متن را می‌گیرد و روی بک‌اند می‌نشاند.</summary>
        public void Set(string value, bool dirtyNow = true)
        {
            _value = value ?? string.Empty;
            _key = null;
            _arguments = null;
            if (dirtyNow) Apply();
        }

        /// <summary>
        /// کلید + آرگومان‌های قالب را نگه می‌دارد و متن را از جدول می‌خواند. با این روش
        /// تغییرِ زبان فقط یک `Apply()` لازم دارد؛ نیازی به ساختنِ دوباره‌ی کل UI نیست.
        /// </summary>
        public void SetKey(string key, params object[] args)
        {
            _key = key;
            _arguments = args;
            _value = args == null || args.Length == 0 ? Loc.Get(key) : Loc.Get(key, args);
            Apply();
        }

        public void SetBold(bool value)
        {
            _bold = value;
            Apply();
        }

        public void SetRaycastTarget(bool value)
        {
            _raycast = value;
            Graphic graphic = Target;
            if (graphic != null) graphic.raycastTarget = value;
        }

        private void OnEnable()
        {
            EnsureBackend();
            Apply();
        }

        private void EnsureBackend()
        {
            if (_tmp != null || _legacy != null || _external != null) return;

            _tmp = GetComponent<TextMeshProUGUI>();
            if (_tmp == null)
            {
                _legacy = GetComponent<Text>();
                if (_legacy == null)
                {
                    // متنِ TMPِ ازپیش‌چیده‌شده در Editor (مثلاً TextMeshPro سه‌بعدی) را حفظ می‌کنیم.
                    _external = GetComponent<TMP_Text>();
                    if (_external != null) return;

                    if (GameTextBackend.Prepare(out TMP_FontAsset fontAsset))
                    {
                        _tmp = gameObject.AddComponent<TextMeshProUGUI>();
                        _tmp.font = fontAsset;
                        TmpLabelCount++;
                    }
                    else if (GetComponentInParent<Canvas>() != null)
                    {
                        _legacy = gameObject.AddComponent<Text>();
                        LegacyLabelCount++;
                    }
                    else
                    {
                        // هیچ بومِ UI روی این گیم‌ابجکت نیست؛ اضافه کردن Text خطای یونیتی می‌دهد.
                        Debug.LogWarning("[BaziBaqa Typography] UIText on a GameObject without a parent Canvas was ignored: " + name);
                        return;
                    }
                }
            }

            if (!_adopted)
            {
                _adopted = true;
                AdoptExistingValues();
            }
        }

        /// <summary>
        /// برای برچسب‌های ازپیش‌ساخته‌شده در Editor، مقدارهایِ تنظیم‌شده را می‌خوانیم تا
        /// Apply() آن‌ها را با پیش‌فرض‌هایِ این کلاس عوض نکند.
        /// </summary>
        private void AdoptExistingValues()
        {
            if (_tmp != null)
            {
                _size = _tmp.fontSize;
                _color = _tmp.color;
                _anchor = GameTextBackend.ToUnityAlignment(_tmp.alignment);
                _wordWrap = _tmp.enableWordWrapping;
                _autoFit = _tmp.enableAutoSizing;
                _bold = _tmp.fontStyle == FontStyles.Bold;
                _value = _tmp.text;
            }
            else if (_legacy != null)
            {
                _size = _legacy.fontSize;
                _color = _legacy.color;
                _anchor = _legacy.alignment;
                _wordWrap = _legacy.horizontalOverflow == HorizontalWrapMode.Wrap;
                _autoFit = _legacy.resizeTextForBestFit;
                _bold = _legacy.fontStyle == UnityEngine.FontStyle.Bold;
                _value = _legacy.text;
            }
        }

        /// <summary>همه‌ی تنظیمات را روی بک‌اندِ فعال می‌نویسد.</summary>
        public void Apply()
        {
            EnsureBackend();

            if (_external != null)
            {
                _external.text = GameTextBackend.LocalizeDigits(_value);
                return;
            }

            TextAnchor anchor = GameTextBackend.ResolveAnchor(_anchor);

            if (_tmp != null)
            {
                TMP_FontAsset fontAsset = _bold ? (GameFont.TmpBoldAsset ?? GameFont.TmpAsset) : GameFont.TmpAsset;
                if (fontAsset != null && !ReferenceEquals(_tmp.font, fontAsset)) _tmp.font = fontAsset;
                _tmp.fontSize = _size;
                _tmp.color = _color;
                _tmp.alignment = GameTextBackend.ToTmpAlignment(anchor);
                _tmp.fontStyle = _bold ? FontStyles.Bold : FontStyles.Normal;
                _tmp.enableWordWrapping = _wordWrap;
                _tmp.overflowMode = TextOverflowModes.Overflow;
                _tmp.enableAutoSizing = _autoFit;
                _tmp.fontSizeMin = Mathf.Max(8f, _size - 6f);
                _tmp.fontSizeMax = _size;
                _tmp.raycastTarget = _raycast;
                // TMP شکل‌دهیِ حروف و bidi را خودش انجام می‌دهد؛ فقط ارقام محلی‌سازی می‌شوند.
                _tmp.text = GameTextBackend.LocalizeDigits(_value);
                _tmp.SetAllDirty();
                return;
            }

            if (_legacy != null)
            {
                _legacy.font = GameFont.Persian;
                _legacy.fontSize = Mathf.RoundToInt(_size);
                _legacy.color = _color;
                _legacy.alignment = anchor;
                _legacy.fontStyle = _bold ? UnityEngine.FontStyle.Bold : UnityEngine.FontStyle.Normal;
                _legacy.horizontalOverflow = _wordWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                _legacy.verticalOverflow = VerticalWrapMode.Truncate;
                _legacy.resizeTextForBestFit = _autoFit;
                _legacy.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(_size) - 5);
                _legacy.resizeTextMaxSize = Mathf.RoundToInt(_size);
                _legacy.supportRichText = true;
                _legacy.raycastTarget = _raycast;
                // مسیرِ قدیمی به شکل‌دهیِ دستی نیاز دارد (PersianText) تا حروف برعکس دیده نشوند.
                _legacy.text = PersianText.Process(_value);
            }
        }

        /// <summary>کلیدها را دوباره از جدول می‌خواند (رویدادِ تغییر زبان).</summary>
        public void Reload()
        {
            if (_key == null) return;
            _value = _arguments == null || _arguments.Length == 0 ? Loc.Get(_key) : Loc.Get(_key, _arguments);
            Apply();
        }
    }
}
