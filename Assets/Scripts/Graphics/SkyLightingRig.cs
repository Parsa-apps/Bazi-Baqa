using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;      // AmbientMode / FogMode / DefaultReflectionMode

namespace BaziBaqa
{
    /// <summary>
    /// تنها نویسنده‌ی نور، مه، نور محیطی، آسمان و بودجه‌ی چراغ‌های شبانه.
    ///
    /// چرا این‌جا و نه در <c>WeatherSystem</c>؟ هوای بازی یک «وضعیتِ منطقی» است (باران/مه/توفان)
    /// و نور و آسمان یک «تصویرِ پیوسته» است که از ساعتِ روز و آن وضعیت هم‌زمان ساخته می‌شود.
    /// وقتی هر دو فایل RenderSettings را می‌نوشتند، ترتیبِ اجرا نتیجه را عوض می‌کرد؛ پس حالا
    /// WeatherSystem فقط خبرِ تغییرِ هوا را می‌دهد (<see cref="NotifyWeather"/>) و همین کلاس
    /// مقدارها را در بازه‌ی چند ثانیه به آن سمت می‌کِشَد. دروازه‌ی FOG_WRITERS در
    /// <c>Tools/project_lint.py</c> همین تک‌نویسنده‌بودن را نگه می‌دارد.
    ///
    /// حذفِ این فایل چیزی را می‌شکند؟ نه: اگر پوشه‌ی Graphics نباشد، صحنه با تنظیماتِ
    /// پیش‌فرضِ یونیتی (آسمانِ ساده، بدونِ مه ارتفاعی) رندر می‌شود.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class SkyLightingRig : MonoBehaviour
    {
        /// <summary>یک کلیدِ زمانیِ چرخه‌ی شبانه‌روزی (صبح/ظهر/عصر/غروب/شبِ عمیق).</summary>
        private struct Key
        {
            public float time;
            public float elevation;     // درجه؛ ارتفاعِ خورشید (زیرِ صفر = ماه)
            public float azimuth;       // درجه
            public float intensity;
            public Color sunColor;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public Color fogColor;
            public float fogDensity;
            public float night;         // 0..1 (چراغ‌ها، ستاره‌ها، درخششِ پنجره‌ها)
            public float cloud;
            public Color zenith;
            public Color horizon;
        }

        // ترتیبِ time باید صعودی باشد و کلیدِ آخر = کلیدِ اول در t=1 (حلقه‌ی شبانه‌روزی).
        private static readonly Key[] Keys =
        {
            new Key
            {
                time = 0.00f, elevation = 34f, azimuth = 215f, intensity = 0.10f,
                sunColor = new Color(0.42f, 0.52f, 0.78f),
                ambientSky = new Color(0.05f, 0.07f, 0.14f),
                ambientEquator = new Color(0.06f, 0.08f, 0.13f),
                ambientGround = new Color(0.03f, 0.04f, 0.07f),
                fogColor = new Color(0.07f, 0.10f, 0.17f), fogDensity = 0.030f,
                night = 1f, cloud = 0.22f,
                zenith = new Color(0.015f, 0.03f, 0.075f),
                horizon = new Color(0.07f, 0.10f, 0.19f)
            },
            new Key
            {
                // پیش‌سحر: آسمان سرد و مه‌آلود، نورِ کم از افق
                time = 0.17f, elevation = -4f, azimuth = 108f, intensity = 0.14f,
                sunColor = new Color(0.52f, 0.55f, 0.72f),
                ambientSky = new Color(0.10f, 0.13f, 0.22f),
                ambientEquator = new Color(0.13f, 0.14f, 0.19f),
                ambientGround = new Color(0.06f, 0.07f, 0.10f),
                fogColor = new Color(0.16f, 0.20f, 0.27f), fogDensity = 0.041f,
                night = 0.86f, cloud = 0.36f,
                zenith = new Color(0.05f, 0.09f, 0.20f),
                horizon = new Color(0.30f, 0.26f, 0.30f)
            },
            new Key
            {
                // صبحِ زود: طلایىِ گرم، سایه‌های بلند
                time = 0.27f, elevation = 8f, azimuth = 96f, intensity = 0.78f,
                sunColor = new Color(1f, 0.74f, 0.48f),
                ambientSky = new Color(0.30f, 0.36f, 0.46f),
                ambientEquator = new Color(0.36f, 0.34f, 0.30f),
                ambientGround = new Color(0.16f, 0.15f, 0.13f),
                fogColor = new Color(0.62f, 0.55f, 0.47f), fogDensity = 0.026f,
                night = 0.10f, cloud = 0.26f,
                zenith = new Color(0.24f, 0.42f, 0.66f),
                horizon = new Color(0.92f, 0.62f, 0.38f)
            },
            new Key
            {
                // ظهر: خنثی و روشن، مه‌ی کم، سایه‌ی کوتاه
                time = 0.50f, elevation = 66f, azimuth = 168f, intensity = 1.16f,
                sunColor = new Color(1f, 0.96f, 0.88f),
                ambientSky = new Color(0.46f, 0.58f, 0.72f),
                ambientEquator = new Color(0.52f, 0.55f, 0.54f),
                ambientGround = new Color(0.24f, 0.24f, 0.20f),
                fogColor = new Color(0.70f, 0.78f, 0.82f), fogDensity = 0.009f,
                night = 0f, cloud = 0.14f,
                zenith = new Color(0.14f, 0.38f, 0.72f),
                horizon = new Color(0.66f, 0.78f, 0.88f)
            },
            new Key
            {
                // بعدازظهر: گرم‌تر، غبارِ معلق در هوا
                time = 0.68f, elevation = 24f, azimuth = 246f, intensity = 0.94f,
                sunColor = new Color(1f, 0.86f, 0.62f),
                ambientSky = new Color(0.40f, 0.44f, 0.50f),
                ambientEquator = new Color(0.46f, 0.42f, 0.36f),
                ambientGround = new Color(0.22f, 0.19f, 0.15f),
                fogColor = new Color(0.74f, 0.66f, 0.54f), fogDensity = 0.018f,
                night = 0f, cloud = 0.20f,
                zenith = new Color(0.20f, 0.40f, 0.68f),
                horizon = new Color(0.90f, 0.70f, 0.46f)
            },
            new Key
            {
                // غروب: قرمزِ اشباع، سایه‌های بسیار بلند، مهِ کفِ دره
                time = 0.78f, elevation = 1f, azimuth = 262f, intensity = 0.52f,
                sunColor = new Color(1f, 0.52f, 0.26f),
                ambientSky = new Color(0.26f, 0.22f, 0.30f),
                ambientEquator = new Color(0.34f, 0.24f, 0.20f),
                ambientGround = new Color(0.12f, 0.09f, 0.10f),
                fogColor = new Color(0.55f, 0.32f, 0.26f), fogDensity = 0.034f,
                night = 0.38f, cloud = 0.30f,
                zenith = new Color(0.10f, 0.12f, 0.30f),
                horizon = new Color(0.95f, 0.42f, 0.22f)
            },
            new Key
            {
                // شبِ رو به جلو: آبیِ عمیق، ستاره‌ها
                time = 0.88f, elevation = 28f, azimuth = 20f, intensity = 0.12f,
                sunColor = new Color(0.40f, 0.50f, 0.76f),
                ambientSky = new Color(0.06f, 0.08f, 0.16f),
                ambientEquator = new Color(0.07f, 0.09f, 0.14f),
                ambientGround = new Color(0.03f, 0.04f, 0.07f),
                fogColor = new Color(0.08f, 0.11f, 0.18f), fogDensity = 0.030f,
                night = 0.94f, cloud = 0.20f,
                zenith = new Color(0.02f, 0.035f, 0.08f),
                horizon = new Color(0.08f, 0.11f, 0.20f)
            },
            new Key
            {
                // بازگشت به ابتدای چرخه (باید با کلیدِ اول برابر باشد)
                time = 1.00f, elevation = 34f, azimuth = 215f, intensity = 0.10f,
                sunColor = new Color(0.42f, 0.52f, 0.78f),
                ambientSky = new Color(0.05f, 0.07f, 0.14f),
                ambientEquator = new Color(0.06f, 0.08f, 0.13f),
                ambientGround = new Color(0.03f, 0.04f, 0.07f),
                fogColor = new Color(0.07f, 0.10f, 0.17f), fogDensity = 0.030f,
                night = 1f, cloud = 0.22f,
                zenith = new Color(0.015f, 0.03f, 0.075f),
                horizon = new Color(0.07f, 0.10f, 0.19f)
            }
        };

        /// <summary>نمونه‌ی فعال (برای هوا و تست‌ها؛ بدونِ Registryِ سراسری).</summary>
        public static SkyLightingRig Instance { get; private set; }

        /// <summary>نامِ گره‌ای که اگر Rig به‌تنهایی در صحنه باشد، انتظار می‌رود روی آن باشد.</summary>
        public const string NodeName = WorldParts.LightingRig;

        [SerializeField] private bool driveSkyMaterial = true;
        [SerializeField] private float blendSpeed = 0.85f;
        [SerializeField] private float lampRebuildSeconds = 0.6f;

        private Light _sun;
        private bool _ownsSunLight;
        private Material _skyMaterial;
        private bool _skyApplied;
        private Camera _camera;
        private float _retryTimer;

        // وضعیتِ نرم‌شده‌ی فعلی (هر فریم به سمتِ هدفِ کلیدها می‌رود)
        private float _night;
        private float _elevation;
        private float _azimuth;
        private float _intensity;
        private float _fogDensity;
        private Color _sunColor = Color.white;
        private Color _ambientSky = Color.gray;
        private Color _ambientEquator = Color.gray;
        private Color _ambientGround = Color.gray;
        private Color _fogColor = Color.gray;
        private Color _zenith = Color.gray;
        private Color _horizon = Color.gray;
        private float _cloud;

        // سواری‌هایِ هوایی (از WeatherSystem) که به‌آرامی نرم می‌شوند
        private WeatherType _weather = WeatherType.Clear;
        private float _weatherCloud;
        private float _weatherWetness;
        private float _weatherDust;
        private float _weatherDim;
        private float _weatherHaze;

        // بودجه‌ی چراغ: بدونِ sort و allocation؛ انتخابِ N نزدیک‌ترین با یک حداقل‌گیریِ ساده
        private const int MaxTrackedLamps = 32;
        private readonly Light[] _lamps = new Light[MaxTrackedLamps];
        private readonly float[] _lampScores = new float[MaxTrackedLamps];
        private readonly bool[] _lampPicked = new bool[MaxTrackedLamps];
        private int _lampCount;
        private int _lampsOn;
        private int _lampGeneration = -1;
        private float _lampTimer;

        public WeatherType Weather { get { return _weather; } }
        public float NightAmount { get { return _night; } }
        public float SunElevation { get { return _elevation; } }
        public bool UsesProceduralSky { get { return driveSkyMaterial && _skyMaterial != null && _skyApplied; } }
        public int LampCount { get { return _lampCount; } }
        public int LampsActive { get { return _lampsOn; } }
        public Color FogColor { get { return _fogColor; } }
        public float FogDensity { get { return _fogDensity; } }

        /// <summary>بردارِ جهتِ آسمان به سمتِ خورشید/ماه (برای شیدرِ آسمان و تست‌ها).</summary>
        public Vector3 SunDirection
        {
            get
            {
                return Quaternion.Euler(90f - _elevation, _azimuth, 0f) * Vector3.back;
            }
        }

        /// <summary>گزارشِ یک‌خطیِ ASCII برای لاگ/دیباگ (متنِ بازی نیست).</summary>
        public string Report()
        {
            return "sky=" + (UsesProceduralSky ? "procedural" : "solid")
                + " night=" + _night.ToString("F2", CultureInfo.InvariantCulture)
                + " sun=" + _intensity.ToString("F2", CultureInfo.InvariantCulture)
                + " elev=" + _elevation.ToString("F0", CultureInfo.InvariantCulture)
                + " fog=" + _fogDensity.ToString("F3", CultureInfo.InvariantCulture)
                + " cloud=" + _cloud.ToString("F2", CultureInfo.InvariantCulture)
                + " weather=" + _weather
                + " lamps=" + _lampsOn + "/" + _lampCount
                + " sunLight=" + (_sun != null ? _sun.name : "none");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResolveSun();
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            // تنظیماتِ محیطی را روی «مقدس‌های» خودِ موتور برمی‌گردانیم تا صحنه‌ی بعدی آلوده نشود
            if (RenderSettings.skybox == _skyMaterial) RenderSettings.skybox = null;
            _skyApplied = false;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            if (_ownsSunLight && _sun != null) Destroy(_sun.gameObject);
            // آرایه‌ی ثابت است؛ فقط شمارنده صفر می‌شود (هیچ allocation تازه‌ای لازم نیست)
            _lampCount = 0;
            _lampsOn = 0;
            _lampGeneration = -1;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().Current;

            // ۱) ساعتِ بازی؛ اگر GameClock در دسترس نبود از زمانِ واقعی استفاده می‌کنیم تا
            //    صحنه‌های ویرایشگر/تست هم آسمانِ زنده داشته باشند.
            float time = 0.34f;
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.Clock != null)
            {
                time = Mathf.Repeat(manager.Clock.NormalizedTime, 1f);
            }
            else
            {
                time = Mathf.Repeat(Time.time / 180f + 0.3f, 1f);
            }

            // ۲) کلیدِ همسایه‌ها + میان‌یابیِ نرم (نیم‌ثانیه تا دو ثانیه طول می‌کشد تا پرش نبینیم)
            SampleKeys(time, out Key a, out Key b, out float u);
            float t = Mathf.SmoothStep(0f, 1f, u);
            float dt = Mathf.Clamp01(Time.deltaTime * blendSpeed);

            _night = Mathf.Lerp(_night, Mathf.Lerp(a.night, b.night, t), dt);
            _elevation = Mathf.Lerp(_elevation, Mathf.Lerp(a.elevation, b.elevation, t), dt);
            _azimuth = Mathf.LerpAngle(_azimuth, Mathf.LerpAngle(a.azimuth, b.azimuth, t), dt);
            _intensity = Mathf.Lerp(_intensity, Mathf.Lerp(a.intensity, b.intensity, t), dt);
            _fogDensity = Mathf.Lerp(_fogDensity, Mathf.Lerp(a.fogDensity, b.fogDensity, t), dt);
            _cloud = Mathf.Lerp(_cloud, Mathf.Lerp(a.cloud, b.cloud, t), dt);
            _sunColor = Color.Lerp(_sunColor, Color.Lerp(a.sunColor, b.sunColor, t), dt);
            _ambientSky = Color.Lerp(_ambientSky, Color.Lerp(a.ambientSky, b.ambientSky, t), dt);
            _ambientEquator = Color.Lerp(_ambientEquator, Color.Lerp(a.ambientEquator, b.ambientEquator, t), dt);
            _ambientGround = Color.Lerp(_ambientGround, Color.Lerp(a.ambientGround, b.ambientGround, t), dt);
            _fogColor = Color.Lerp(_fogColor, Color.Lerp(a.fogColor, b.fogColor, t), dt);
            _zenith = Color.Lerp(_zenith, Color.Lerp(a.zenith, b.zenith, t), dt);
            _horizon = Color.Lerp(_horizon, Color.Lerp(a.horizon, b.horizon, t), dt);

            // ۳) سواری‌هایِ هوا (باران/مه/توفان) روی همان کلیدِ زمانی می‌نشینند
            // حال‌وهوایِ سینمایی (CinematicVolumeRig) یک «سواری» روی همین عدد‌هاست، نه نویسنده
            CinematicVolumeRig volume = GraphicsDirector.Instance != null ? GraphicsDirector.Instance.Volume : null;
            CinematicVolumeRig.CinematicMood mood = volume != null ? volume.Mood : CinematicVolumeRig.CinematicMood.Neutral;

            float sunScale = Mathf.Lerp(1f, 0.34f, _weatherDim);
            float fogScale = (1f + _weatherHaze * 1.6f) * Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(mood.fogDensity));
            float cloud = Mathf.Clamp01(_cloud + _weatherCloud * (1f - _cloud * 0.35f));
            float ambientScale = Mathf.Lerp(1f, 0.72f, _weatherDim) * Mathf.Clamp(mood.ambientScale, 0.4f, 1.8f);
            float night = Mathf.Clamp01(_night + _weatherCloud * 0.12f);
            float wetness = Mathf.Clamp01(Mathf.Max(_weatherWetness, mood.wetness));
            float dust = Mathf.Clamp01(Mathf.Max(_weatherDust, mood.dust));

            // ۴) نور اصلی
            if (_sun == null) ResolveSun();
            if (_sun != null)
            {
                if (!_sun.enabled) _sun.enabled = true;
                _sun.transform.rotation = Quaternion.Euler(90f - _elevation, _azimuth, 0f);
                _sun.color = _sunColor;
                _sun.intensity = Mathf.Max(0f, _intensity * sunScale);
                ApplyShadowSettings(_sun, tier);
                if (RenderSettings.sun != _sun) RenderSettings.sun = _sun;
            }

            // ۵) نور محیطی + مه (تنها جایِ نوشتنِ RenderSettings در پروژه)
            float ambientBoost = tier.ambientScale * ambientScale;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _ambientSky * ambientBoost;
            RenderSettings.ambientEquatorColor = _ambientEquator * ambientBoost;
            RenderSettings.ambientGroundColor = _ambientGround * ambientBoost;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;      // با fogDensityِ نرمِ ما هم‌خوان است
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = Mathf.Clamp(_fogDensity * fogScale, 0f, 0.12f);

            // ۶) ثابت‌هایِ جهانیِ شیدرها (مه ارتفاعی + اتمسفر)؛ شیدرهای پروژه همین‌ها را می‌خوانند
            float ceiling = Mathf.Max(6f, tier.heightFogCeiling);
            MaterialLibrary.SetHeightFog(new Vector4(
                Mathf.Clamp(_fogDensity * fogScale * 2.4f, 0f, 0.25f),
                wetness,
                ceiling,
                -2f), Color.Lerp(_fogColor, mood.fogColor, 0.35f));
            MaterialLibrary.SetAtmosphere(night, ambientBoost * 0.75f + 0.25f, wetness,
                Mathf.Clamp01(dust + (1f - night) * 0.05f));

            // ۷) آسمان
            ApplySky(cloud, night, ambientScale);

            // ۸) بودجه‌ی چراغ‌های شبانه (ساختمان/آتش)
            _lampTimer -= Time.unscaledDeltaTime;
            if (_lampTimer <= 0f)
            {
                _lampTimer = lampRebuildSeconds;
                UpdateLamps(night, tier.lampBudget);
            }
        }

        /// <summary>فوری‌سازیِ همه‌چیز (برای تست‌ها و بعد از تغییرِ کیفیت؛ بدونِ انتظارِ میان‌یابی).</summary>
        public void ApplyNow()
        {
            Update();
        }

        /// <summary>بازیابیِ نور/دوربین/متریالِ آسمان (پس از بازسازیِ جهان لازم است).</summary>
        public void Refresh()
        {
            _camera = null;
            _retryTimer = 0f;
            _skyApplied = false;
            _lampTimer = 0f;
            ResolveSun();
        }

        /// <summary>فراخوانِ <c>WeatherSystem</c>؛ هیچ تنظیمِ رندر را مستقیم نمی‌نویسد.</summary>
        public void NotifyWeather(WeatherType weather, float rainRate)
        {
            _weather = weather;
            switch (weather)
            {
                case WeatherType.Rain:
                    _weatherCloud = 0.62f;
                    _weatherWetness = Mathf.Clamp01(0.45f + rainRate);
                    _weatherDust = 0f;
                    _weatherDim = 0.55f;
                    _weatherHaze = 0.18f;
                    break;
                case WeatherType.Fog:
                    _weatherCloud = 0.42f;
                    _weatherWetness = 0.28f;
                    _weatherDust = 0.12f;
                    _weatherDim = 0.42f;
                    _weatherHaze = 1f;
                    break;
                case WeatherType.Storm:
                    _weatherCloud = 1f;
                    _weatherWetness = Mathf.Clamp01(0.6f + rainRate);
                    _weatherDust = 0.22f;
                    _weatherDim = 1f;
                    _weatherHaze = 0.5f;
                    break;
                default:
                    _weatherCloud = 0.08f;
                    _weatherWetness = 0f;
                    _weatherDust = 0f;
                    _weatherDim = 0f;
                    _weatherHaze = 0f;
                    break;
            }
        }

        // ------------------------------------------------------------------ اجزای داخلی

        private void SampleKeys(float time, out Key lower, out Key upper, out float u)
        {
            lower = Keys[0];
            upper = Keys[1];
            u = 0f;
            float t = Mathf.Clamp01(time);
            for (int i = 0; i < Keys.Length - 1; i++)
            {
                Key candidate = Keys[i];
                Key next = Keys[i + 1];
                if (t >= candidate.time && t <= next.time)
                {
                    float span = Mathf.Max(0.0001f, next.time - candidate.time);
                    lower = candidate;
                    upper = next;
                    u = Mathf.Clamp01((t - candidate.time) / span);
                    return;
                }
            }
            lower = Keys[Keys.Length - 2];
            upper = Keys[Keys.Length - 1];
            u = 0f;
        }

        private void ResolveSun()
        {
            Light found = RenderSettings.sun;
            if (found == null || !found.isActiveAndEnabled || found.type != LightType.Directional)
            {
                found = FindDirectionalLight();
            }
            if (found == null && _camera != null)
            {
                found = _camera.GetComponentInParent<Light>();
            }
            if (found == null)
            {
                GameObject host = new GameObject(WorldParts.SunLight);
                Light created = host.AddComponent<Light>();
                created.type = LightType.Directional;
                _ownsSunLight = true;
                found = created;
            }
            else
            {
                _ownsSunLight = false;
            }
            _sun = found;
            _sun.enabled = true;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
            Light best = null;
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || !light.isActiveAndEnabled) continue;
                if (light.type != LightType.Directional) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }
            return best;
        }

        private void ApplyShadowSettings(Light light, GraphicsProfile.TierSettings tier)
        {
            // اندازه/کیفیتِ سایه کارِ QualitySettings و RenderPipelineBridge است؛ این‌جا فقط
            // «شدت و نرمی» را با شب‌‌وزمان هماهنگ می‌کنیم تا شب، سایه‌ی سیاهِ تخت نداشته باشیم.
            float dayAmount = Mathf.Clamp01(1f - _night);
            light.shadowStrength = Mathf.Lerp(0.18f, Mathf.Clamp01(tier.shadowStrength), dayAmount);
            light.shadowBias = Mathf.Clamp(tier.shadowDepthBias * 0.035f, 0.01f, 0.09f);
            light.shadowNormalBias = Mathf.Clamp(tier.shadowNormalBias * 0.4f, 0.02f, 0.4f);
            // نیم‌سازِ شب: نورِ ماه سایه‌ی تیز نمی‌سازد ⇒ LightShadows.None و صرفه‌جوییِ واقعی
            light.shadows = dayAmount < 0.12f ? LightShadows.None
                : (tier.softShadows ? LightShadows.Soft : LightShadows.Hard);
            light.useColor = true;
            light.useFlare = false;
        }

        private void ApplySky(float cloud, float night, float ambientScale)
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _retryTimer -= Time.unscaledDeltaTime;
                if (_retryTimer <= 0f)
                {
                    _retryTimer = 0.4f;
                    _camera = Camera.main;
                }
                if (_camera == null) return;
            }

            Material sky = driveSkyMaterial ? MaterialLibrary.Sky() : null;
            _skyMaterial = sky;
            if (sky == null)
            {
                // بدونِ شیدرِ آسمان: پس‌زمینه‌ی هم‌رنگِ افق (هیچ‌وقت مشکی/ارغوانی نیست)
                if (RenderSettings.skybox != null) RenderSettings.skybox = null;
                if (_skyApplied) _skyApplied = false;
                Color flat = Color.Lerp(_horizon, _fogColor, 0.35f);
                if (_camera.clearFlags != CameraClearFlags.SolidColor) _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(flat.r, flat.g, flat.b, 1f);
                return;
            }

            SetColor(sky, "_BaziSkyZenith", _zenith);
            SetColor(sky, "_BaziSkyHorizon", _horizon);
            SetColor(sky, "_BaziSkyGround", Color.Lerp(_ambientGround, _fogColor, 0.5f));
            SetColor(sky, "_BaziSkySunColor", _sunColor);
            sky.SetVector("_BaziSkySunDir", SunDirection);
            sky.SetFloat("_BaziSkySunSize", 0.9985f);
            sky.SetFloat("_BaziSkyNight", night);
            sky.SetFloat("_BaziSkyCloud", Mathf.Clamp01(cloud));
            sky.SetFloat("_BaziSkyCloudLevel", 0.22f);
            sky.SetFloat("_BaziSkyDust", Mathf.Clamp01(_weatherDust));
            sky.SetFloat("_BaziSkyExposure", Mathf.Lerp(0.75f, 1.15f, ambientScale));

            if (!_skyApplied)
            {
                RenderSettings.skybox = sky;
                    // بازتابِ پیش‌فرض از خودِ آسمان می‌آید (آبِ گام ۴ و فلزات همین را می‌خوانند)؛
                // رزولوشنِ probe را خودِ یونیتی از تنظیماتِ کیفیت برمی‌دارد.
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                if (_camera.clearFlags != CameraClearFlags.Skybox) _camera.clearFlags = CameraClearFlags.Skybox;
                _skyApplied = true;
            }
        }

        private static void SetColor(Material material, string property, Color color)
        {
            material.SetColor(property, new Color(color.r, color.g, color.b, 1f));
        }

        /// <summary>
        /// چراغ‌های نقطه‌ایِ ساختمان‌ها/آتش را فقط در شب و فقط برای Nِ نزدیک‌ترین روشن می‌گذارد.
        /// نورها متعلق به BuildingController هستند (تصمیمِ بازی)، ما فقط «چندتاشان روشن باشد» را
        /// مدیریت می‌کنیم؛ به همین دلیل هیچ ارجاعی به کلاسِ Gameplay نگه نمی‌داریم.
        /// </summary>
        private void UpdateLamps(float night, int budget)
        {
            GameManager manager = GameManager.Instance;
            Transform root = manager != null && manager.World != null ? manager.World.BuildingRoot : null;
            if (root == null)
            {
                _lampCount = 0;
                _lampsOn = 0;
                return;
            }

            // نسلِ ریشه عوض شده (بازسازیِ جهان) یا یکی از نورها از بین رفته ⇒ فهرست تازه
            if (_lampGeneration != root.GetInstanceID()) RebuildLampList(root);
            else
            {
                for (int i = 0; i < _lampCount; i++)
                {
                    if (_lamps[i] == null)
                    {
                        RebuildLampList(root);
                        break;
                    }
                }
            }

            Vector3 anchor = _camera != null
                ? _camera.transform.position
                : (root != null ? root.position : Vector3.zero);

            int limit = Mathf.Clamp(Mathf.Min(budget, _lampCount), 0, MaxTrackedLamps);
            for (int i = 0; i < _lampCount; i++)
            {
                _lampScores[i] = Vector3.SqrMagnitude(_lamps[i].transform.position - anchor);
                _lampPicked[i] = false;
            }
            _lampsOn = 0;
            for (int pick = 0; pick < limit; pick++)
            {
                int best = -1;
                float bestScore = float.MaxValue;
                for (int i = 0; i < _lampCount; i++)
                {
                    if (_lampPicked[i]) continue;
                    if (_lampScores[i] < bestScore)
                    {
                        bestScore = _lampScores[i];
                        best = i;
                    }
                }
                if (best < 0) break;
                _lampPicked[best] = true;
                _lampsOn++;
            }

            bool lit = night > 0.22f;
            for (int i = 0; i < _lampCount; i++)
            {
                Light lamp = _lamps[i];
                if (lamp == null) continue;
                bool on = lit && _lampPicked[i];
                if (lamp.enabled != on) lamp.enabled = on;
                if (!on) continue;
                lamp.intensity = Mathf.Lerp(0.4f, 1.6f, night);
                lamp.color = Color.Lerp(new Color(1f, 0.72f, 0.42f), new Color(1f, 0.84f, 0.62f), _night * 0.3f);
                lamp.shadows = LightShadows.None;        // سایه‌ی چراغ‌ها روی اندروید میان‌رده گران است
                lamp.range = Mathf.Clamp(lamp.range, 2.5f, 11f);
            }
        }

        private void RebuildLampList(Transform root)
        {
            _lampCount = 0;
            _lampGeneration = root.GetInstanceID();
            Light[] lights = root.GetComponentsInChildren<Light>(false);
            for (int i = 0; i < lights.Length && _lampCount < MaxTrackedLamps; i++)
            {
                Light light = lights[i];
                if (light == null || light.type == LightType.Directional) continue;
                if (light == _sun) continue;
                _lamps[_lampCount++] = light;
            }
        }
    }
}
