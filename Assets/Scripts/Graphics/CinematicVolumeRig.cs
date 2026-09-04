using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// استکِ سینماییِ پس‌پرداز (Bloom، Color Adjustments، Tonemapping، Vignette، DoF، Grain،
    /// Chromatic Aberration، Lift/Gamma/Gain) که روی یک <c>Volume</c> سراسری زنده است.
    ///
    /// چرا در زمان اجرا ساخته می‌شود و نه به‌صورت فایلِ Volume Profile؟ چون فایلِ Profile
    /// باید ارجاع به شیدرها/باft‌های پکیج URP داشته باشد و آن GUIDها بدون باز شدنِ پروژه در
    /// Unity قابل‌تولید نیستند ⇒ هر چیزی که دستی ساخته شود، در ریپو می‌شکند. این کلاس همان
    /// پروفایل را در اولین فریم می‌سازد؛ پس نتیجه در ویرایشگر، در بیلد و در Test Runner یکی است.
    ///
    /// اگر URP نصب نباشد، این کامپوننت هیچ Volume ای نمی‌سازد (زیرِ Built-in کسی آن را
    /// ارزیابی نمی‌کند) و فقط وضعیت را لاگ می‌کند ⇒ بازی اجرا می‌شود، فقط بدون پس‌پرداز.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CinematicVolumeRig : MonoBehaviour
    {
        /// <summary>یک «حال‌وهوا»ی نور/رنگ (صبح، ظهر، غروب، شب، طوفان)؛ همه مقادیر نرمال‌اند.</summary>
        [Serializable]
        public struct CinematicMood
        {
            public float exposure;        // استاپ نور (منفی = تیره‌تر)
            public float contrast;        // درصدِ کنتراستِ اضافه
            public float saturation;      // درصدِ اشباع
            public float temperature;     // -100..100 (سرد تا گرم)
            public float tint;            // -100..100 (سبز تا ارغوانی)
            public float bloom;           // شدتِ اضافه روی نمایه
            public float vignette;        // 0..1
            public float grain;           // 0..1
            public float depthOfField;    // 0..1 (میزانِ محوِ پشتِ سر)
            public float shadowLift;      // 0..1 (بازکردنِ سیاهی‌ها، حسِ فیلمی)
            public Vector3 lift;          // رنگِ سایه‌ها
            public Vector3 gain;          // رنگِ روشنی‌ها
            public float fogDensity;      // 0..1 (نرمال؛ به مه‌ی ارتفاعی هم می‌رود)
            public Color fogColor;
            public float ambientScale;    // مقیاس نور محیطی
            public float wetness;         // 0..1 (رطوبتِ سطح‌ها)
            public float dust;            // 0..1 (گردوغبارِ معلق)

            public static CinematicMood Neutral
            {
                get
                {
                    return new CinematicMood
                    {
                        exposure = 0f,
                        contrast = 0f,
                        saturation = 0f,
                        temperature = 0f,
                        tint = 0f,
                        bloom = 1f,
                        vignette = 1f,
                        grain = 1f,
                        depthOfField = 1f,
                        shadowLift = 0.08f,
                        lift = new Vector3(1f, 1f, 1f),
                        gain = new Vector3(1f, 1f, 1f),
                        fogDensity = 0.35f,
                        fogColor = new Color(0.62f, 0.72f, 0.78f),
                        ambientScale = 1f,
                        wetness = 0f,
                        dust = 0f
                    };
                }
            }

            public static CinematicMood Lerp(CinematicMood a, CinematicMood b, float t)
            {
                t = Mathf.Clamp01(t);
                CinematicMood result = new CinematicMood
                {
                    exposure = Mathf.LerpUnclamped(a.exposure, b.exposure, t),
                    contrast = Mathf.LerpUnclamped(a.contrast, b.contrast, t),
                    saturation = Mathf.LerpUnclamped(a.saturation, b.saturation, t),
                    temperature = Mathf.LerpUnclamped(a.temperature, b.temperature, t),
                    tint = Mathf.LerpUnclamped(a.tint, b.tint, t),
                    bloom = Mathf.LerpUnclamped(a.bloom, b.bloom, t),
                    vignette = Mathf.LerpUnclamped(a.vignette, b.vignette, t),
                    grain = Mathf.LerpUnclamped(a.grain, b.grain, t),
                    depthOfField = Mathf.LerpUnclamped(a.depthOfField, b.depthOfField, t),
                    shadowLift = Mathf.LerpUnclamped(a.shadowLift, b.shadowLift, t),
                    lift = Vector3.LerpUnclamped(a.lift, b.lift, t),
                    gain = Vector3.LerpUnclamped(a.gain, b.gain, t),
                    fogDensity = Mathf.LerpUnclamped(a.fogDensity, b.fogDensity, t),
                    fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                    ambientScale = Mathf.LerpUnclamped(a.ambientScale, b.ambientScale, t),
                    wetness = Mathf.LerpUnclamped(a.wetness, b.wetness, t),
                    dust = Mathf.LerpUnclamped(a.dust, b.dust, t)
                };
                return result;
            }
        }

        [SerializeField] private int priority = 100;
        [SerializeField] private bool applyToSceneView = false;

        private Volume _volume;
        private VolumeProfile _profile;
        private CinematicMood _mood = CinematicMood.Neutral;
        private CinematicMood _targetMood = CinematicMood.Neutral;
        private GraphicsProfile.TierSettings _tier;
        private bool _warnedNoPipeline;

        public Volume Volume { get { return _volume; } }
        public VolumeProfile Profile { get { return _profile; } }
        public CinematicMood Mood { get { return _mood; } }
        public bool IsActive { get { return _volume != null && _volume.enabled && _profile != null; } }

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            GraphicsProfile profile = GraphicsProfile.Load();
            _tier = profile.Current;
            if (_profile != null) ApplyTier(_tier);
        }

        /// <summary>ساختِ دوباره‌ی استک (پس از تعویض خطِ رندر یا کیفیت).</summary>
        public void Rebuild()
        {
            if (!MaterialLibrary.IsUniversal)
            {
                if (!_warnedNoPipeline)
                {
                    _warnedNoPipeline = true;
                    GameLogger.Warn("Graphics: URP is not active, cinematic post-processing skipped. "
                        + "Run menu: BaziBaqa/Rendering/Install URP Assets");
                }
                return;
            }
#if BAZI_UNIVERSAL
            if (_volume == null)
            {
                _volume = GetComponent<Volume>();
                if (_volume == null) _volume = gameObject.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = priority;
            }
            _profile = BuildProfile();
            _volume.sharedProfile = _profile;
            _volume.enabled = true;
            _tier = GraphicsProfile.Load().Current;
            ApplyTier(_tier);
            ApplyMoodImmediate();
#endif
        }

        /// <summary>
        /// نمایه‌ی کیفیت روی استکِ پس‌پرداز: Bloom/Tonemap/Vignette/Grain/AO/DoF همه از یک‌جا
        /// می‌آیند، پس «کیفیت پایین» یعنی واقعاً بارِ GPU کمتر، نه فقط رزولوشنِ کمتر.
        /// </summary>
        public void ApplyTier(GraphicsProfile.TierSettings tier)
        {
            if (tier == null) return;
            _tier = tier;
#if BAZI_UNIVERSAL
            if (_profile == null) return;
            MaterialLibrary.SetAmbientOcclusionParams(tier.ao ? tier.aoIntensity : 0f, tier.aoRadius, tier.aoSampleCount, tier.ao);
            ApplyMoodImmediate();
#endif
        }

        /// <summary>تنظیم نرمِ حال‌وهوا (فاز ۲ این فاز: نور سینمایی)؛ میان‌یابی در Update.</summary>
        public void SetMood(CinematicMood mood)
        {
            _targetMood = mood;
        }

        public void SnapMood(CinematicMood mood)
        {
            _targetMood = mood;
            _mood = mood;
            ApplyMoodImmediate();
        }

        private void Update()
        {
            if (_mood.fogDensity != _targetMood.fogDensity || !MoodClose(_mood, _targetMood))
            {
                _mood = CinematicMood.Lerp(_mood, _targetMood, Mathf.Clamp01(Time.deltaTime * 1.4f));
                ApplyMoodImmediate();
            }
        }

        private static bool MoodClose(CinematicMood a, CinematicMood b)
        {
            return Mathf.Abs(a.exposure - b.exposure) < 0.002f
                && Mathf.Abs(a.bloom - b.bloom) < 0.002f
                && Mathf.Abs(a.ambientScale - b.ambientScale) < 0.002f;
        }

        private void ApplyMoodImmediate()
        {
#if BAZI_UNIVERSAL
            if (_profile == null) return;
            GraphicsProfile.TierSettings tier = _tier ?? GraphicsProfile.Load().Current;
            CinematicMood mood = _mood;

            if (_overrides != null)
            {
                for (int i = 0; i < _overrides.Count; i++) _overrides[i].Apply(tier, mood);
            }
#endif
        }

#if BAZI_UNIVERSAL
        // ==================================================================
        // لایه‌ی انتزاع: هر افکت یک <see cref="VolumeOverride"/> است؛ اگر یک نسخه‌ی URP
        // عضوی را نداشته باشد، فقط همان افکت نادیده گرفته می‌شود (نه خطای کامپایل، نه null).
        private readonly List<VolumeOverride> _overrides = new List<VolumeOverride>();

        private sealed class VolumeOverride
        {
            public string key;
            public Action<GraphicsProfile.TierSettings, CinematicVolumeRig.CinematicMood> apply;
            public void Apply(GraphicsProfile.TierSettings tier, CinematicVolumeRig.CinematicMood mood)
            {
                if (apply == null) return;
                try
                {
                    apply(tier, mood);
                }
                catch (Exception error)
                {
                    Debug.LogWarning("BaziBaqa Volume: override '" + key + "' failed: " + error.Message);
                }
            }
        }

        /// <summary>
        /// مقداردهیِ یک <c>ParameterOverride&lt;enum&gt;</c> با نامِ عضو. تنها جایی که عمداً
        /// reflection به‌کار می‌رود: عضوهای enumِ DepthOfField در نسخه‌های URP جابه‌جا شده‌اند
        /// و خطایِ کامپایلِ کلِ بازی به‌خاطر یک افکتِ تزئینی، قابل‌قبول نیست.
        /// </summary>
        private static void TrySetEnum(object parameter, params string[] candidateNames)
        {
            if (parameter == null) return;
            System.Reflection.PropertyInfo valueProperty = parameter.GetType().GetProperty("value");
            if (valueProperty == null || !valueProperty.CanWrite) return;
            Type enumType = valueProperty.PropertyType;
            if (!enumType.IsEnum) return;
            foreach (string name in candidateNames)
            {
                foreach (object candidate in Enum.GetValues(enumType))
                {
                    if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        valueProperty.SetValue(parameter, candidate, null);
                        return;
                    }
                }
            }
        }

        private VolumeProfile BuildProfile()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _overrides.Clear();

            Bloom bloom = profile.Add<Bloom>();
            bloom.active = true;
            _overrides.Add(new VolumeOverride
            {
                key = "bloom",
                apply = (tier, mood) =>
                {
                    bloom.intensity.Override(Mathf.Max(0f, tier.bloom * mood.bloom));
                    bloom.threshold.Override(Mathf.Clamp(tier.bloomThreshold, 0f, 3f));
                    bloom.highQualityFiltering.Override(tier.shadowResolution >= 1024);
                }
            });

            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.active = true;
            _overrides.Add(new VolumeOverride
            {
                key = "color",
                apply = (tier, mood) =>
                {
                    colorAdjustments.postExposure.Override(mood.exposure);
                    colorAdjustments.contrast.Override(Mathf.Clamp(tier.contrast + mood.contrast, -100f, 100f));
                    colorAdjustments.saturation.Override(Mathf.Clamp(tier.saturation + mood.saturation, -100f, 100f));
                    colorAdjustments.temperature.Override(Mathf.Clamp(mood.temperature, -100f, 100f));
                    colorAdjustments.tint.Override(Mathf.Clamp(mood.tint, -100f, 100f));
                }
            });

            Vignette vignette = profile.Add<Vignette>();
            vignette.active = true;
            _overrides.Add(new VolumeOverride
            {
                key = "vignette",
                apply = (tier, mood) =>
                {
                    vignette.intensity.Override(Mathf.Clamp01(tier.vignette * mood.vignette));
                    vignette.smoothness.Override(0.42f);
                }
            });

            ChromaticAberration chromatic = profile.Add<ChromaticAberration>();
            _overrides.Add(new VolumeOverride
            {
                key = "chromatic",
                apply = (tier, mood) =>
                {
                    float amount = Mathf.Clamp01(tier.chromatic) * 0.01f;
                    chromatic.active = amount > 0.0005f;
                    chromatic.amount.Override(new Vector2(amount, amount * 0.75f));
                }
            });

            FilmGrain grain = profile.Add<FilmGrain>();
            _overrides.Add(new VolumeOverride
            {
                key = "grain",
                apply = (tier, mood) =>
                {
                    float value = Mathf.Clamp01(tier.grain * mood.grain);
                    grain.active = value > 0.0005f;
                    grain.intensity.Override(value);
                    grain.response.Override(0.6f);
                }
            });

            LiftGammaGain lift = profile.Add<LiftGammaGain>();
            lift.active = true;
            _overrides.Add(new VolumeOverride
            {
                key = "lift",
                apply = (tier, mood) =>
                {
                    float liftValue = Mathf.Clamp01(mood.shadowLift);
                    lift.lift.Override(new Vector4(mood.lift.x, mood.lift.y, mood.lift.z, liftValue * 0.25f));
                    lift.gain.Override(new Vector4(mood.gain.x, mood.gain.y, mood.gain.z, 1f));
                }
            });

            DepthOfField depthOfField = profile.Add<DepthOfField>();
            _overrides.Add(new VolumeOverride
            {
                key = "dof",
                apply = (tier, mood) =>
                {
                    bool enabled = tier.depthOfField && mood.depthOfField > 0.01f;
                    depthOfField.active = enabled;
                    if (!enabled) return;
                    depthOfField.focusDistance.Override(26f);
                    depthOfField.focalLength.Override(50f);
                    depthOfField.aperture.Override(Mathf.Lerp(5.6f, 2.0f, Mathf.Clamp01(mood.depthOfField)));
                    depthOfField.nearFocusRange.Override(0.8f);
                    TrySetEnum(depthOfField.mode, "Bokeh", "Gaussian");   // Bokeh = کیفیتِ بهتر روی موبایلِ میان‌رده
                    depthOfField.highQualitySampling.Override(tier.shadowResolution >= 2048);
                }
            });
            return profile;
        }
#endif
    }
}
