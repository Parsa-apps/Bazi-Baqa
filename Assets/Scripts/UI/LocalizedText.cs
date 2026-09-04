using System;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// یک متن را به یک «کلید بومی‌سازی» گره می‌زند. با تغییر زبان (Loc.SetLanguage)
    /// خودکار تازه می‌شود؛ پس هیچ اسکریپتی لازم ندارد متن‌ها را دستی دوباره پر کند.
    /// نوشتنِ متن از طریقِ UIText انجام می‌شود، بنابراین بک‌اند (TMP یا Text قدیمی) خودش انتخاب می‌شود
    /// و مقدارهایِ تنظیم‌شده در Editor حفظ می‌مانند.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Localization key, resolved from Assets/Resources/Localization/LocalizationTable.json")]
        public string key;

        [Tooltip("Format arguments ({0}, {1}) for texts with numbers.")]
        public string[] formatArgs = Array.Empty<string>();

        [SerializeField] private string[] _arguments;

        private UIText _label;

        public string Key
        {
            get { return key; }
            set { key = value; Refresh(); }
        }

        private void Awake()
        {
            _label = UIText.Attach(gameObject);
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
            if (_label == null) _label = UIText.Attach(gameObject);
            _label.SetKey(key, (object[])(formatArgs ?? Array.Empty<string>()));
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
            if (_label == null) _label = UIText.Attach(gameObject);
            _label.SetKey(key, _arguments ?? Array.Empty<object>());
        }

        /// <summary>متنِ آماده (بدون جدول) روی برچسب می‌گذارد؛ برای متن‌هایِ زنده‌یِ Editor.</summary>
        public void Apply(string value)
        {
            if (_label == null) _label = UIText.Attach(gameObject);
            _label.Set(value);
        }
    }
}
