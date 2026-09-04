using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// نمایه‌ی گرافیک بازی: تنها جایی که «سِختِ» بصری تعریف می‌شود.
    ///
    /// فلسفه: هیچ مقدارِ گرافیکی داخل کدِ سیستم‌ها پنهان نمی‌ماند. همه در
    /// <c>Assets/Resources/Graphics/GraphicsProfile.json</c> زندگی می‌کنند تا هنرمن/برنامه‌ریز
    /// بدون کامپایل دوباره، سطحِ بصری را تغییر دهد؛ اگر فایل نبود، مقادیرِ امنِ پیش‌فرض
    /// (همان‌هایی که فاز قبل رفتار بازی را حفظ می‌کنند) به کار می‌آیند ⇒ هرگز بازی نمی‌شکند.
    ///
    /// هر سطحِ کیفیت یک <see cref="Tier"/> دارد و <see cref="QualityForTier"/> شماره‌ی
    /// <c>QualitySettings</c> را می‌دهد؛ پس تعویض کیفیت، کلِ لایه‌ی بصری را یک‌جا عوض می‌کند.
    /// </summary>
    [Serializable]
    public sealed class GraphicsProfile
    {
        public const string ResourcePath = "Graphics/GraphicsProfile";
        public const int CurrentVersion = 4;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private string defaultTier = "medium";
        [SerializeField] private List<TierSettings> tiers = new List<TierSettings>();

        /// <summary>نمایه‌ی بارگذاری‌شده؛ همیشه غیر null (در بدترین حالت پیش‌فرض‌ها).</summary>
        public static GraphicsProfile Loaded { get; private set; }

        public int Version { get { return version; } }
        public string DefaultTierName { get { return string.IsNullOrEmpty(defaultTier) ? "medium" : defaultTier; } }
        public IReadOnlyList<TierSettings> Tiers { get { return tiers; } }

        // ------------------------------------------------------------------ بارگذاری

        /// <summary>بارگذاری از Resources؛ در صورت نبود/خرابی، پیش‌فرض‌های ساخته‌شده در کد.</summary>
        public static GraphicsProfile Load(bool force = false)
        {
            if (!force && Loaded != null) return Loaded;
            GraphicsProfile profile = null;
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                try
                {
                    profile = JsonUtility.FromJson<GraphicsProfile>(asset.text);
                }
                catch (Exception error)
                {
                    GameLogger.Warn("GraphicsProfile.json is invalid; falling back to built-in defaults: " + error.Message);
                    profile = null;
                }
            }

            if (profile == null || profile.tiers == null || profile.tiers.Count == 0)
            {
                profile = CreateDefault();
                GameLogger.Info("GraphicsProfile not found in Resources; using built-in defaults");
            }

            profile.Normalize();
            Loaded = profile;
            return profile;
        }

        /// <summary>پیش‌فرض‌های سه‌سطحی؛ مقادیر عمداً محافظه‌کارانه‌اند (هدف: اندروید میان‌رده).</summary>
        public static GraphicsProfile CreateDefault()
        {
            GraphicsProfile profile = new GraphicsProfile
            {
                version = CurrentVersion,
                defaultTier = "medium"
            };
            profile.tiers.Add(TierSettings.Create("low", 0, 0.72f, false, 0, false, false));
            profile.tiers.Add(TierSettings.Create("medium", 1, 0.9f, false, 0, true, true));
            profile.tiers.Add(TierSettings.Create("high", 2, 1.0f, true, 4, true, true));
            profile.tiers.Add(TierSettings.Create("ultra", 2, 1.0f, true, 4, true, true));
            return profile;
        }

        private void Normalize()
        {
            if (version <= 0) version = CurrentVersion;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i] == null) tiers[i] = TierSettings.Create("tier" + i.ToString(CultureInfo.InvariantCulture), i, 1f, false, 0, false, false);
                else tiers[i].Normalize(i);
            }
        }

        // ------------------------------------------------------------------ دسترسی

        /// <summary>تمام نام‌های سطح‌ها، به ترتیبِ فایل.</summary>
        public string[] TierNames()
        {
            string[] names = new string[tiers.Count];
            for (int i = 0; i < tiers.Count; i++) names[i] = tiers[i].id;
            return names;
        }

        public TierSettings Get(string tierName)
        {
            if (!string.IsNullOrEmpty(tierName))
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    if (string.Equals(tiers[i].id, tierName, StringComparison.OrdinalIgnoreCase)) return tiers[i];
                }
            }
            return GetByQualityLevel(QualitySettings.GetQualityLevel());
        }

        /// <summary>سطحِ مرتبط با شماره‌ی کیفیت فعلی یونیتی (خارج از بازه ⇒ نزدیک‌ترین سطح).</summary>
        public TierSettings GetByQualityLevel(int qualityLevel)
        {
            if (tiers.Count == 0) return TierSettings.Create("fallback", 0, 1f, false, 0, false, false);
            TierSettings match = null;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].qualityLevel == qualityLevel) return tiers[i];
                if (tiers[i].qualityLevel <= qualityLevel) match = tiers[i];
            }
            return match != null ? match : tiers[0];
        }

        public TierSettings Current { get { return GetByQualityLevel(QualitySettings.GetQualityLevel()); } }

        /// <summary>
        /// خطاهای سازگاریِ نمایه (برای دروازه‌های ویرایشگر/تست): آرایه‌ی خالی، کیفیتِ تکراری،
        /// renderScale خارج از بازه، سایه‌ی بزرگ‌ترِ سطح‌های پایین‌تر و ...
        /// </summary>
        public void Validate(List<string> issues)
        {
            if (issues == null) return;
            if (version != CurrentVersion) issues.Add("version must be " + CurrentVersion + " but was " + version);
            if (tiers == null || tiers.Count == 0) { issues.Add("no quality tiers defined (tiers is empty)"); return; }
            if (string.IsNullOrEmpty(defaultTier)) issues.Add("defaultTier is empty");
            else
            {
                bool found = false;
                for (int i = 0; i < tiers.Count; i++) found |= string.Equals(tiers[i].id, defaultTier, StringComparison.OrdinalIgnoreCase);
                if (!found) issues.Add("defaultTier \"" + defaultTier + "\" is not listed in tiers");
            }

            HashSet<int> qualityLevels = new HashSet<int>();
            for (int i = 0; i < tiers.Count; i++)
            {
                TierSettings tier = tiers[i];
                if (string.IsNullOrEmpty(tier.id)) issues.Add("tier " + i + " has no id");
                if (tier.renderScale < 0.4f || tier.renderScale > 1.5f) issues.Add(tier.id + ": renderScale out of the safe 0.4..1.5 range (" + tier.renderScale.ToString("F2", CultureInfo.InvariantCulture) + ")");
                if (tier.shadowResolution < 256 || tier.shadowResolution > 4096) issues.Add(tier.id + ": shadowResolution outside 256..4096 (" + tier.shadowResolution + ")");
                if (tier.qualityLevel < 0 || tier.qualityLevel >= QualitySettings.names.Length) issues.Add(tier.id + ": qualityLevel " + tier.qualityLevel + " is outside the project quality levels (" + QualitySettings.names.Length + ")");
                if (tier.particleBudget < 32) issues.Add(tier.id + ": particleBudget is too low (" + tier.particleBudget + ")");
                if (tier.cullDistance < 8f) issues.Add(tier.id + ": cullDistance is dangerously small (" + tier.cullDistance.ToString("F0", CultureInfo.InvariantCulture) + ")");
                if (tier.maxCulledObjects < 16) issues.Add(tier.id + ": maxCulledObjects is too small to be useful (" + tier.maxCulledObjects + ")");
                if (tier.ao && tier.aoIntensity <= 0.001f) issues.Add(tier.id + ": ambient occlusion is on but its intensity is zero");
            }

            // سطوحِ بالاتر نباید از سطوحِ پایین‌تر ضعیف‌تر باشند (تنظیمِ معکوس، خطای رایجِ دستی)
            for (int i = 1; i < tiers.Count; i++)
            {
                if (tiers[i].renderScale + 0.001f < tiers[i - 1].renderScale) issues.Add(tiers[i].id + ": renderScale is lower than the tier below it (" + tiers[i - 1].id + ")");
                if (tiers[i].shadowResolution < tiers[i - 1].shadowResolution) issues.Add(tiers[i].id + ": shadowResolution is lower than " + tiers[i - 1].id);
            }
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("GraphicsProfile v").Append(version).Append(" | tiers: ");
            for (int i = 0; i < tiers.Count; i++)
            {
                TierSettings tier = tiers[i];
                builder.Append(tier.id).Append("(scale ").Append(tier.renderScale.ToString("F2", CultureInfo.InvariantCulture))
                       .Append(", shadow ").Append(tier.shadowResolution)
                       .Append(tier.ao ? ", AO" : string.Empty)
                       .Append(tier.hdr ? ", HDR" : string.Empty)
                       .Append(tier.proceduralSky ? ", sky" : ", flat")
                       .Append(" lamps ").Append(tier.lampBudget)
                       .Append(" foliage ").Append(tier.foliageCount)
                       .Append(i + 1 < tiers.Count ? ") | " : ")");
            }
            return builder.ToString();
        }

        // ==================================================================
        /// <summary>تنظیماتِ یک سطحِ کیفیت (renderScale تا بودجه‌ی افکت‌ها).</summary>
        [Serializable]
        public sealed class TierSettings
        {
            public string id = "medium";
            public int qualityLevel = 1;

            [Range(0.4f, 1.5f)] public float renderScale = 0.9f;
            public bool hdr = false;
            public int msaa = 0;
            public bool depthTexture = true;
            public bool opaqueTexture = true;

            public int shadowResolution = 1024;
            public int shadowCascades = 1;
            public float shadowDistance = 60f;
            public float shadowDepthBias = 1.2f;
            public float shadowNormalBias = 0.5f;
            public bool softShadows = false;

            // ---- نورپردازیِ سینمایی (گام ۲) ----
            public bool proceduralSky = true;             // شیدرِ آسمان یا SolidColor
            public float ambientScale = 1.0f;              // ضریبِ نورِ محیطیِ Trilight
            [Range(0f, 1f)] public float shadowStrength = 0.82f;
            public float heightFogCeiling = 24f;           // ارتفاعی که مه ارتفاعی تا آنجاست
            [Range(0, 8)] public int lampBudget = 3;       // بیشترین چراغِ هم‌زمانِ شبانه

            // ---- محیط زنده (گام ۳) ----
            public int foliageCount = 700;                  // بوته‌های چمن (در یک draw call)
            public float windScale = 1.0f;                  // ضریبِ شدتِ بادِ شیدرها

            // ---- بودجه‌ی دید (گام ۷): CullingGroup سه نوار می‌سازد ----
            // نزدیک = کامل، میانی = بی‌سایه، دور = بی‌رندر. همه‌یِ اینها انتخابِ بصری‌اند؛
            // منطقِ بازی هیچ‌وقت از «دیده‌شدن» چیزی نمی‌پرسد.
            public float shadowCasterDistance = 42f;
            public float cullDistance = 70f;
            public int maxCulledObjects = 220;

            public bool ao = true;
            [Range(0f, 2f)] public float aoIntensity = 0.7f;
            public float aoRadius = 0.4f;
            [Range(1f, 64f)] public float aoSampleCount = 8f;

            [Range(0f, 4f)] public float bloom = 0.75f;
            [Range(0f, 3f)] public float bloomThreshold = 0.92f;
            [Range(-100f, 100f)] public float contrast = 12f;
            [Range(-100f, 100f)] public float saturation = 8f;
            [Range(-1f, 1f)] public float colorFilterLift = 0f;
            [Range(0f, 1f)] public float vignette = 0.22f;
            [Range(0f, 1f)] public float grain = 0.05f;
            [Range(0f, 1f)] public float chromatic = 0.12f;
            public bool depthOfField = true;

            public float lodBias = 1.0f;
            public int textureLimit = 0;              // mipmapLimit: 0 = بدون محدودیتِ اضافه
            public int particleBudget = 600;
            public int maxAdditionalLights = 4;
            public bool reflections = true;
            public int targetFrameRate = 60;

            public static TierSettings Create(string id, int qualityLevel, float renderScale, bool hdr, int msaa, bool ao, bool depthOfField)
            {
                return new TierSettings
                {
                    id = id,
                    qualityLevel = qualityLevel,
                    renderScale = renderScale,
                    hdr = hdr,
                    msaa = msaa,
                    ao = ao,
                    depthOfField = depthOfField
                };
            }

            public void Normalize(int index)
            {
                if (string.IsNullOrEmpty(id)) id = "tier" + index.ToString(CultureInfo.InvariantCulture);
                renderScale = Mathf.Clamp(renderScale, 0.4f, 1.5f);
                shadowResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(shadowResolution), 256, 4096);
                shadowCascades = Mathf.Clamp(shadowCascades, 1, 4);
                shadowDistance = Mathf.Clamp(shadowDistance, 5f, 300f);
                aoIntensity = Mathf.Clamp(aoIntensity, 0f, 2f);
                aoSampleCount = Mathf.Clamp(aoSampleCount, 1f, 64f);
                aoRadius = Mathf.Clamp(aoRadius, 0.02f, 4f);
                particleBudget = Mathf.Clamp(particleBudget, 32, 8000);
                maxAdditionalLights = Mathf.Clamp(maxAdditionalLights, 0, 8);
                targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 120);
                ambientScale = Mathf.Clamp(ambientScale, 0.35f, 1.6f);
                shadowStrength = Mathf.Clamp(shadowStrength, 0f, 1f);
                heightFogCeiling = Mathf.Clamp(heightFogCeiling, 4f, 160f);
                lampBudget = Mathf.Clamp(lampBudget, 0, 8);
                foliageCount = Mathf.Clamp(foliageCount, 0, 4096);
                windScale = Mathf.Clamp(windScale, 0f, 2f);
                cullDistance = Mathf.Clamp(cullDistance, 8f, 400f);
                shadowCasterDistance = Mathf.Clamp(shadowCasterDistance, 1f, 400f);
                // سایه نباید از خودِ شیء زنده‌تر باشد (تصویرِ سایه‌ی معلق روی زمین)
                if (shadowCasterDistance > cullDistance) shadowCasterDistance = cullDistance;
                maxCulledObjects = Mathf.Clamp(maxCulledObjects, 16, 512);
                // چراغ‌های بیشتر از بودجه‌ی نورِ اضافه‌ی URP، بی‌اثر و گران‌اند
                if (lampBudget > maxAdditionalLights) lampBudget = Mathf.Max(0, maxAdditionalLights);
            }
        }
    }
}
