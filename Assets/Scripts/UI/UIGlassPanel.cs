using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// پنلِ شیشه‌ای (گام ۶): همان `Image` را با متریالِ رویه‌ایِ شیشه رندر می‌کند و لبه‌ی
    /// نئونی/جاروبِ نور را روشن نگه می‌دارد. فقط یک پنل جاروب را می‌راند تا ۴۰ پنل
    /// هر فریم یک property سراسری ننویسند.
    ///
    /// اگر شیدر حل نشود (بیِ URP/شیدرِ حذف‌شده) متریال null می‌ماند و پنل دقیقاً مثلِ
    /// قبلِ این فاز رندر می‌شود ⇒ هیچ حالتی وجود ندارد که رابط ناپدید شود.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIGlassPanel : MonoBehaviour
    {
        private static UIGlassPanel _driver;
        private static float _phase;
        private static int _activeCount;
        private static int _missingShaderReports;

        [SerializeField] private bool applyToSelf = true;

        private Image _image;
        private Material _material;
        private Color _tint = Color.white;

        public static int ActivePanels { get { return _activeCount; } }
        public static float SweepPhase { get { return _phase; } }
        public static bool GlassAvailable { get { return MaterialLibrary.Glass() != null; } }
        public bool IsGlassActive { get { return _material != null; } }
        public static string Report()
        {
            return "glass panels=" + _activeCount + " phase=" +
                _phase.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " shader=" + (GlassAvailable ? "on" : "off") +
                (_missingShaderReports > 0 ? " fallback=" + _missingShaderReports : string.Empty);
        }

        /// <summary>اتصالِ idempotent (از CreatePanel فراخوانی می‌شود).</summary>
        public static UIGlassPanel Apply(GameObject panel)
        {
            if (panel == null) return null;
            UIGlassPanel component = panel.GetComponent<UIGlassPanel>();
            if (component == null) component = panel.AddComponent<UIGlassPanel>();
            component.Rebuild();
            return component;
        }

        private void OnEnable()
        {
            _activeCount++;
            if (_driver == null) _driver = this;
            ApplyMaterial();
        }

        private void OnDisable()
        {
            _activeCount = Mathf.Max(0, _activeCount - 1);
            if (_driver == this) _driver = null;
        }

        private void Update()
        {
            if (_driver != this) return;
            // جاروبِ کند: روی موبایل هیچ هزینه‌ای ندارد (یک SetVector در فریم) ولی رابط «زنده» است
            _phase = Mathf.Repeat(_phase + Time.unscaledDeltaTime * 0.05f, 1f);
            MaterialLibrary.SetGlassSweepPhase(_phase);
        }

        public void Rebuild()
        {
            _image = GetComponent<Image>();
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (!applyToSelf || _image == null) _image = GetComponent<Image>();
            if (_image == null) return;
            if (_material == null) _material = MaterialLibrary.Glass();
            if (_material == null)
            {
                // یک‌بار یادآوری، بعد سکوت (لاگِ هر فریم در ویرایشگر خواندنِ گزارش را سخت می‌کند)
                if (_missingShaderReports < 3)
                {
                    _missingShaderReports++;
                    GameLogger.Info("UIGlassPanel: ui-glass shader unavailable, panels stay flat");
                }
                _image.material = null;
                return;
            }
            _image.material = _material;
            _tint = _image.color;
        }

        /// <summary>مُودِ رابط: منو (جاروبِ پررنگ)، HUD (کم‌رنگ)، مودال (تیره و با لبه‌ی طلایی).</summary>
        public static void ApplyMood(string mood)
        {
            switch (mood)
            {
                case "menu":
                    MaterialLibrary.SetGlassMood(0.85f, 0.4f, 0.75f);
                    break;
                case "modal":
                    MaterialLibrary.SetGlassMood(0.6f, 0.12f, 0.35f);
                    break;
                case "hud":
                    MaterialLibrary.SetGlassMood(0.45f, 0.16f, 0.5f);
                    break;
                default:
                    MaterialLibrary.SetGlassMood(0.7f, 0.28f, 0.6f);
                    break;
            }
        }

        /// <summary>رنگِ پنل را با همان شفافیتِ قبلی نرم می‌کند (برای fadeِ هم‌زمانِ شیشه).</summary>
        public void SetAlpha(float alpha)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;
            Color color = _image.color;
            color.a = Mathf.Clamp01(alpha);
            _image.color = color;
        }
    }
}
