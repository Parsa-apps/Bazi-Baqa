using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// تک‌نویسنده‌ی ثابتِ جهانیِ باد (<c>_BaziWindState</c>). شیدرِ Surface و ذراتِ محیطی همین
    /// یک بردار را می‌خوانند؛ هیچ شیئی در صحنه باد را جدا حساب نمی‌کند ⇒ هم‌خوانیِ کامل و
    /// هزینه‌ی صفر (بدون update روی صدها transform).
    ///
    /// باد از سه چیز ساخته می‌شود: شدتِ پایه‌ی سطحِ کیفیت، هوایِ فعلی (از WeatherSystem، فقط
    /// خواندن) و «بَرگشت» (gust) که با Perlin روی زمان می‌لنگد تا درخت‌ها یکنواخت تکان نخورند.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class WindField : MonoBehaviour
    {
        public static WindField Instance { get; private set; }

        // پایه
        [SerializeField] private float baseStrength = 0.34f;
        [SerializeField] private float baseSpeed = 0.9f;
        [SerializeField] private float frequency = 0.75f;

        // بَرگشت
        [SerializeField] private float gustInterval = 5.5f;
        [SerializeField] private float gustAmount = 0.55f;
        [SerializeField] private float gustSharpness = 2.4f;

        // جهت
        [SerializeField] private Vector2 defaultDirection = new Vector2(1f, 0.35f);
        [SerializeField] private float directionTurnSpeed = 0.12f;

        private Vector2 _direction;
        private float _strength;
        private float _speed;
        private float _gust;
        private float _phase;
        private float _gustTimer;
        private float _clock;
        private WeatherType _weather = WeatherType.Clear;

        /// <summary>شدتِ بی‌بعدِ فعلی (۰ تا ~۱٫۶)؛ برای ذراتِ گام ۴ هم همین خوانده می‌شود.</summary>
        public float Strength { get { return _strength; } }
        public float Speed { get { return _speed; } }
        public float Gust { get { return _gust; } }
        public Vector2 Direction { get { return _direction; } }
        public WeatherType LastWeather { get { return _weather; } }

        /// <summary>گزارشِ ASCII برای لاگ/تست (متنِ بازی نیست).</summary>
        public string Report()
        {
            return "wind strength=" + _strength.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " speed=" + _speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " gust=" + _gust.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " dir=" + _direction.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ","
                + _direction.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " weather=" + _weather;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _direction = defaultDirection.sqrMagnitude > 0.0001f ? defaultDirection.normalized : Vector2.up;
            ApplyImmediate();
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            // باد را خاموش نمی‌کنیم (شیدرها مقدارِ آخر را نگه می‌دارند) تا قطع‌وصلوعِ Rig پرش نسازد
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;

            // ۱) هوایِ فعلی را فقط «می‌خوانیم»؛ هیچ چیزی در WeatherSystem عوض نمی‌کنیم
            WeatherType weather = _weather;
            float rain = 0f;
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.Weather != null)
            {
                weather = manager.Weather.Current;
                rain = manager.Weather.RainIntensity;
            }
            if (weather != _weather)
            {
                _weather = weather;
            }

            float targetStrength;
            float targetSpeed;
            Vector2 targetDirection = _direction;
            switch (weather)
            {
                case WeatherType.Storm:
                    targetStrength = 1.35f + rain * 0.25f;
                    targetSpeed = 2.05f;
                    targetDirection = new Vector2(-1f, -0.45f);
                    break;
                case WeatherType.Rain:
                    targetStrength = 0.66f;
                    targetSpeed = 1.35f;
                    targetDirection = new Vector2(0.85f, -0.5f);
                    break;
                case WeatherType.Fog:
                    targetStrength = 0.16f;
                    targetSpeed = 0.42f;
                    targetDirection = defaultDirection;
                    break;
                default:
                    targetStrength = baseStrength;
                    targetSpeed = baseSpeed;
                    targetDirection = defaultDirection;
                    break;
            }

            // ۲) شب، باد آرام‌تر است؛ نیمه‌شب تا ۴۰٪ کاهش
            SkyLightingRig rig = SkyLightingRig.Instance;
            if (rig != null)
            {
                targetStrength *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(rig.NightAmount));
            }

            // ۳) بَرگشت‌ها: یک موجِ نرم روی شدت (Perlin ⇒ بدونِ پرش و بدونِ Randomِ سراسری)
            _gustTimer += dt;
            if (_gustTimer >= gustInterval)
            {
                _gustTimer -= gustInterval;
            }
            float gustCurve = Mathf.PerlinNoise(_clock * 0.21f, 3.7f) - 0.42f;
            _gust = Mathf.Clamp01(gustCurve * 2.1f) * gustAmount;

            float blend = Mathf.Clamp01(dt * 1.25f);
            _strength = Mathf.Lerp(_strength, targetStrength * (1f + _gust), blend);
            _speed = Mathf.Lerp(_speed, targetSpeed * (1f + _gust * 0.35f), blend);
            _direction = Vector2.Lerp(_direction, targetDirection.normalized, Mathf.Clamp01(dt * directionTurnSpeed)).normalized;
            _phase = Mathf.Repeat(_phase + dt * frequency * (1f + _gust * 0.5f), 100f);

            ApplyImmediate();
        }

        /// <summary>نوشتنِ همان لحظه‌ایِ ثابتِ جهانی (تست‌ها و بعد از تغییرِ کیفیت همین را صدا می‌زنند).</summary>
        public void ApplyNow()
        {
            Update();
        }

        /// <summary>باد را روی شدتِ سطحِ کیفیت فعلی تنظیم می‌کند (بدونِ تغییرِ منطقِ بازی).</summary>
        public void ApplyTier()
        {
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().Current;
            // روی دستگاه‌های ضعیف‌تر همان باد می‌ماند (هزینه‌اش در شیدر است، نه CPU) و فقط
            // بسامدِ بَرگشت کم می‌شود تا تکانِ درخت‌ها ارزان‌تر به نظر برسد.
            frequency = Mathf.Lerp(0.45f, 0.9f, Mathf.Clamp01(tier.renderScale));
            gustSharpness = tier.qualityLevel <= 0 ? 1.6f : 2.4f;
        }

        private void ApplyImmediate()
        {
            MaterialLibrary.SetWind(_speed, _strength, _phase, gustSharpness);
        }

        /// <summary>مقدارِ جهانیِ باد را می‌خواند (تست‌ها؛ چیزی نمی‌نویسد).</summary>
        public static Vector4 GlobalState { get { return MaterialLibrary.GetGlobalVector("_BaziWindState"); } }
    }
}
