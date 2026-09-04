using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BaziBaqa
{
    /// <summary>
    /// یک متن را به یک «کلید بومی‌سازی» گره می‌زند. با تغییر زبان (Loc.SetLanguage)
    /// خودکار تازه می‌شود؛ پس هیچ اسکریپتی لازم ندارد متن‌ها را دستی دوباره پر کند.
    /// روی هر دو نوع متن بازی کار می‌کند: TextMeshPro (متن اصلی UI) و Text قدیمی (سازگار با
    /// صحنه‌های دستیِ آینده/پریفب‌ها).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Localization key, resolved from Assets/Resources/Localization/LocalizationTable.json")]
        public string key;

        [Tooltip("Format arguments ({0}, {1}) for texts with numbers.")]
        public string[] formatArgs = Array.Empty<string>();

        [SerializeField] private string[] _arguments;

        private TMP_Text _tmp;
        private Text _legacy;

        public string Key
        {
            get { return key; }
            set { key = value; Refresh(); }
        }

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            _legacy = GetComponent<Text>();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        private void OnDestroy()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(string language)
        {
            Refresh();
        }

        /// <summary>متن را دوباره از جدول می‌خواند و روی کامپوننت می‌گذارد.</summary>
        public void Refresh()
        {
            if (_arguments != null && _arguments.Length > 0)
            {
                ApplyFromArguments();
                return;
            }
            if (string.IsNullOrEmpty(key)) return;
            Apply(formatArgs != null && formatArgs.Length > 0 ? Loc.Get(key, formatArgs) : Loc.Get(key));
        }

        /// <summary>تنظیم کلید + آرگومان‌ها از کد (راه اصلی استفاده در UIManager).</summary>
        public void Configure(string keyValue, params object[] args)
        {
            key = keyValue;
            _arguments = args == null ? null : Array.ConvertAll(args, value => value == null ? string.Empty : value.ToString());
            ApplyFromArguments();
        }

        private void ApplyFromArguments()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_arguments == null || _arguments.Length == 0)
            {
                Apply(Loc.Get(key));
                return;
            }
            Apply(Loc.Get(key, _arguments));
        }

        private void Apply(string value)
        {
            if (_tmp != null) { _tmp.text = value; return; }
            if (_legacy != null) { _legacy.text = PersianText.LegacyDisplay(value); }
        }
    }
}
