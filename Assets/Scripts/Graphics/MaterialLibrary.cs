using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace BaziBaqa
{
    /// <summary>
    /// تنها منبعِ متریال/شیدر در بازی.
    ///
    /// چرا لازم است؟ با تعویض خطِ رندر از Built-in به URP، هر `Shader.Find("Standard")` و هر
    /// متریالِ ذره‌ی پیش‌فرض، ارغوانی می‌شود. این کلاس به‌جای حدس‌زدن، فهرستِ کاندید دارد:
    /// شیدرِ اختصاصیِ پروژه ⇒ شیدرِ URP ⇒ شیدرِ built-in ⇒ Diffuse؛ و در هر مرحله فقط اگر
    /// واقعاً در پروژه وجود داشت از آن استفاده می‌کند. پس بازی در چهار حالتِ ممکن
    /// (با URP / بدون URP / با پکیجِ ناقص / در بیلدِ استریپ‌شده) هرگز بی‌رندر نمی‌ماند.
    ///
    /// نکته‌ی حجم: همه‌ی شیدرهای اختصاصی زیر <c>Assets/Resources/Shaders</c> هستند؛ چون
    /// Resources همیشه در بیلد می‌ماند، `Shader.Find` در نجوی player هم کار می‌کند و نیازی به
    /// «Always Included Shaders» (که فقط دستی/ویرایشگرِ Graph ست می‌شود) نیست.
    /// </summary>
    public static class MaterialLibrary
    {
        public enum SurfaceStyle
        {
            /// <summary>رنگیِ ساده، بدون نقشه (جایگزینِ مکعب‌های فاز قبل).</summary>
            Tinted,
            Ground,
            Rock,
            Bark,
            Foliage,
            Panel,
            Metal,
            Ember
        }

        private const string SurfaceShaderName = "BaziBaqa/Surface";
        private const string SurfaceShaderResource = "Shaders/BaziBaqa-Surface";
        private const string EmissiveShaderName = "BaziBaqa/Emissive";
        private const string EmissiveShaderResource = "Shaders/BaziBaqa-Emissive";
        private const string SkyShaderName = "Hidden/BaziBaqa/Sky";
        private const string SkyShaderResource = "Shaders/BaziBaqa-Sky";
        private const string WaterShaderName = "BaziBaqa/Water";
        private const string WaterShaderResource = "Shaders/BaziBaqa-Water";
        private const string GlassShaderName = "Hidden/BaziBaqa/UIGlass";
        private const string GlassShaderResource = "Shaders/BaziBaqa-UIGlass";
        private const string FireShaderName = "Hidden/BaziBaqa/Fire";
        private const string FireShaderResource = "Shaders/BaziBaqa-Fire";

        private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Shader> _shaderCache = new Dictionary<string, Shader>();
        private static readonly List<string> _diagnostics = new List<string>();

        private static bool? _resolutionLogged;
        private static string _resolvedSurfaceShaderPath = string.Empty;

        public static int CachedMaterialCount { get { return _materials.Count; } }
        public static IReadOnlyList<string> Diagnostics { get { return _diagnostics; } }
        public static string ResolvedSurfaceShaderPath { get { return _resolvedSurfaceShaderPath; } }

        /// <summary>آیا خطِ رندرِ قابل‌برنامه‌ریزی (URP یا HDRP) فعال است؟</summary>
        public static bool IsScriptablePipeline
        {
            get
            {
                return GraphicsSettings.currentRenderPipeline != null
                    || GraphicsSettings.defaultRenderPipeline != null;
            }
        }

        public static bool IsUniversal
        {
            get
            {
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline
                    : GraphicsSettings.defaultRenderPipeline;
                if (asset == null) return false;
                string name = asset.GetType().FullName ?? string.Empty;
                return name.IndexOf("Universal", System.StringComparison.Ordinal) >= 0;
            }
        }

        // ------------------------------------------------------------------ شیدر

        /// <summary>
        /// اولین شیدرِ موجود از فهرستِ کاندید را برمی‌گرداند (نام‌ها به ترتیبِ اولویت).
        /// خروجی هیچ‌وقت null نیست: اگر هیچ‌کدام پیدا نشد، Diffuse/VertexLitِ built-in و در
        /// بدترین حالتِ مطلق، «Unlit/Color» برگردانده می‌شود تا صحنه هرگز ارغوانی نشود.
        /// </summary>
        public static Shader ResolveShader(params string[] candidates)
        {
            if (candidates != null)
            {
                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrEmpty(candidate)) continue;
                    if (_shaderCache.TryGetValue(candidate, out Shader cached) && cached != null) return cached;

                    Shader found;
                    if (candidate.StartsWith("res:", System.StringComparison.Ordinal))
                    {
                        // در بیلدِ استریپ‌شده Shader.Find ممکن است خالی برگرداند؛ Resources همیشه پیدا می‌شود
                        found = Resources.Load<Shader>(candidate.Substring(4));
                    }
                    else
                    {
                        found = Shader.Find(candidate);
                        if (found == null)
                        {
                            found = Resources.Load<Shader>(SurfaceShaderResourceFor(candidate));
                        }
                    }
                    if (found != null)
                    {
                        _shaderCache[candidate] = found;
                        return found;
                    }
                    _shaderCache[candidate] = null;
                }
            }

            foreach (string fallback in new[] { "Legacy Shaders/Diffuse", "Diffuse", "VertexLit", "Unlit/Color", "Standard" })
            {
                Shader shader = Shader.Find(fallback);
                if (shader != null) return shader;
            }
            return null;
        }

        /// <summary>شیدرِ سطحِ پروژه (BaziBaqa/Surface) با همه‌ی مسیرهای جایگزین.</summary>
        public static Shader SurfaceShader
        {
            get
            {
                Shader shader = ResolveShader(SurfaceShaderName, SurfaceShaderResource);
                if (shader != null && string.IsNullOrEmpty(_resolvedSurfaceShaderPath))
                {
                    _resolvedSurfaceShaderPath = shader.name;
                    LogOnce();
                }
                return shader;
            }
        }

        /// <summary>«BaziBaqa/Surface» ⇒ «Shaders/BaziBaqa-Surface» (قرار دادِ پوشه‌ی Resources/Shaders).</summary>
        private static string SurfaceShaderResourceFor(string shaderName)
        {
            // قراردادِ پروژه: فایل «Assets/Resources/Shaders/BaziBaqa-Surface.shader»
            // شیدرِ «BaziBaqa/Surface» است. بیرون از این قرارداد حدس نمی‌زنیم.
            if (string.IsNullOrEmpty(shaderName)) return string.Empty;
            if (!shaderName.StartsWith("BaziBaqa/", System.StringComparison.Ordinal)) return string.Empty;
            return "Shaders/" + shaderName.Replace("/", "-");
        }

        private static void LogOnce()
        {
            if (_resolutionLogged.HasValue && _resolutionLogged.Value) return;
            _resolutionLogged = true;
            GraphicsProfile profile = GraphicsProfile.Load();
            GraphicsProfile.TierSettings tier = profile.Current;
            GameLogger.System("MaterialLibrary: shader=" + (_resolvedSurfaceShaderPath ?? "null")
                + " | pipeline=" + (IsUniversal ? "URP" : (IsScriptablePipeline ? "SRP-other" : "built-in"))
                + " | quality=" + SafeQualityName()
                + " | " + profile.Describe()
                + " | scale=" + tier.renderScale.ToString("F2", CultureInfo.InvariantCulture));
        }

        private static string SafeQualityName()
        {
            int index = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            return names != null && index >= 0 && index < names.Length ? names[index] : index.ToString(CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ بافت

        public const string TextureFolder = "Textures/Graphics";

        /// <summary>نامِ فایلِ شیدرِ آسمان؛ برای گزارش‌ها و تست‌ها (Hidden ⇒ فقط از MaterialLibrary).</summary>
        public const string SkyShaderFile = "BaziBaqa-Sky";

        private static Texture2D LoadTexture(string resourcePath, bool linear)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            Texture2D texture = Resources.Load<Texture2D>(TextureFolder + "/" + resourcePath);
            if (texture == null)
            {
                texture = Resources.Load<Texture2D>(resourcePath);
            }
            if (texture == null) return null;

            // تورسِ بافتِ محیطی: تکرار + سه‌خطی + آنیزوتروپی. این‌ها را روی خودِ فایلِ .meta هم
            // نوشته‌ایم؛ تکرارِ آن اینجا عمداً است تا اگر کسی فایل را جای دیگری برد، تصویر خراب نشود.
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = Mathf.Max(1, texture.anisoLevel);
            return texture;
        }

        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        public static Texture2D Texture(string name, bool linear = false)
        {
            string key = name + (linear ? "|linear" : "|srgb");
            if (_textureCache.TryGetValue(key, out Texture2D cached)) return cached;
            Texture2D texture = LoadTexture(name, linear);
            _textureCache[key] = texture;
            return texture;
        }

        // ------------------------------------------------------------------ متریال

        /// <summary>متریالِ سطحِ اصلی؛ کش‌شده با کلیدِ پایدار، پس Draw Call ها قابل‌ادغام می‌مانند.</summary>
        public static Material Surface(SurfaceStyle style, Color tint, float metallic = 0f, float smoothness = 0.35f, bool wind = false)
        {
            string key = "surface|" + style + "|" + ColorKey(tint) + "|" + metallic.ToString("F3", CultureInfo.InvariantCulture)
                + "|" + smoothness.ToString("F3", CultureInfo.InvariantCulture) + "|" + wind;
            if (_materials.TryGetValue(key, out Material existing) && existing != null) return existing;

            Shader shader = SurfaceShader;
            Material material = new Material(shader);
            material.name = "BaziSurface_" + style;

            Color color = tint;
            color.a = 1f;
            material.SetVector("_BaziColor", color);
            material.SetFloat("_BaziMetallic", Mathf.Clamp01(metallic));
            material.SetFloat("_BaziRoughness", Mathf.Clamp01(1f - smoothness * 0.9f));
            material.SetFloat("_BaziDetailBlend", style == SurfaceStyle.Tinted ? 0f : 0.32f);

            ApplyTextures(material, style);

            // رنگِ رأس‌ها: زمین/سنگ/تنه/برگ از vertex color استفاده می‌کنند. شیدر مقدارِ
            // نبودنِ رنگ را هم درست می‌گیرد (رنگِ پیش‌فرضِ مش ≈ سفید) ⇒ برای هر مشی امن است.
            if (style == SurfaceStyle.Ground || style == SurfaceStyle.Rock
                || style == SurfaceStyle.Bark || style == SurfaceStyle.Foliage)
            {
                material.EnableKeyword("_BAZI_VERTEX_COLOR_ON");
            }
            if (style == SurfaceStyle.Foliage)
            {
                material.EnableKeyword("_BAZI_ALPHA_CLIP_ON");
                material.SetFloat("_Cutoff", 0.42f);
            }

            if (wind)
            {
                material.EnableKeyword("_BAZI_WIND_ON");
                material.SetFloat("_BaziWindStrength", style == SurfaceStyle.Foliage ? 0.055f : 0.018f);
                material.SetFloat("_BaziWindFlex", style == SurfaceStyle.Foliage ? 1.4f : 3.2f);
            }

            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// متریالِ رنگیِ ساده — همان چیزی که <c>WorldGenerator.CreateMaterial</c> قبلاً می‌ساخت؛
        /// فقط این‌جا شیدر از حل‌کننده‌ی چندمرحله‌ای می‌آید، نه از حدسِ «Standard».
        /// </summary>
        public static Material Tinted(Color color, float metallic = 0f)
        {
            return Surface(SurfaceStyle.Tinted, color, metallic);
        }

        /// <summary>متریالِ نورانی (چراغ ساختمان، آتش، چشم دشمن)؛ در URP با HDR روشن می‌ماند.</summary>
        public static Material Emissive(Color color, float intensity = 2f)
        {
            string key = "emissive|" + ColorKey(color) + "|" + intensity.ToString("F2", CultureInfo.InvariantCulture);
            if (_materials.TryGetValue(key, out Material existing) && existing != null) return existing;

            Shader shader = ResolveShader(EmissiveShaderName, "res:" + EmissiveShaderResource, "Standard", "Unlit/Color");
            Material material = new Material(shader);
            material.name = "BaziEmissive";
            material.color = color;
            material.SetFloat("_Mode", 2f);                     // Standard: Emission (Built-in)
            material.EnableKeyword("_EMISSION");
            material.SetVector("_BaziEmissiveColor", new Vector4(color.r, color.g, color.b, Mathf.Max(0f, intensity)));
            material.SetFloat("_BaziEmissiveIntensity", 1f);
            if (material.HasProperty("_EmissionColor")) material.SetVector("_EmissionColor", color * Mathf.Max(0f, intensity));
            if (material.HasProperty("_BaziColor"))
            {
                material.SetVector("_BaziColor", color);
                material.SetVector("_BaziEmissionColor", new Vector4(color.r, color.g, color.b, Mathf.Max(0f, intensity)));
                material.EnableKeyword("_BAZI_EMISSIVE_ON");
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// متریال ذرات (آتش، دود، جرقه، درخشش آب). تا پیش از این، ParticleSystemRenderer هیچ
        /// متریالی نداشت ⇒ در هر دو خطِ رندر با پیام «material missing» رنگِ ارغوانی می‌شد.
        /// از شیدرِ خودِ پروژه استفاده می‌شود (نه شیدرِ پکیج) تا در بیلد استریپ نشود.
        /// </summary>
        public static Material Particle(Color tint, bool additive = true, bool stretch = false)
        {
            string key = "particle|" + ColorKey(tint) + "|" + additive + "|" + stretch;
            if (_materials.TryGetValue(key, out Material existing) && existing != null) return existing;

            Shader shader = ResolveShader(EmissiveShaderName, "res:" + EmissiveShaderResource, "Standard", "Unlit/Color");
            Material material = new Material(shader);
            material.name = "BaziParticle";
            if (material.HasProperty("_BaziEmissiveColor"))
            {
                material.SetVector("_BaziEmissiveColor", new Vector4(tint.r, tint.g, tint.b, 1f));
            }
            material.color = tint;
            material.SetFloat("_BaziNightBoost", 1f);
            if (additive)
            {
                material.SetFloat("_BaziSrcBlend", 1f);       // One
                material.SetFloat("_BaziDstBlend", 1f);       // One  ⇒ جمعِ نورانی، مناسبِ آتش و جرقه
                material.renderQueue = 3100;                   // Transparent+
            }
            else
            {
                material.SetFloat("_BaziSrcBlend", 5f);       // SrcAlpha
                material.SetFloat("_BaziDstBlend", 10f);      // OneMinusSrcAlpha ⇒ دود
                material.renderQueue = 3000;
            }
            material.SetFloat("_BaziZWrite", 0f);
            material.SetFloat("_BaziCull", 0f);               // بک‌ساید هم دیده شود (ذره هیچ ضخامتی ندارد)
            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// متریال شفاف (نشانگر ساخت، پیش‌نمای ساختمان). پیش از این داخل ConstructionSystem
        /// ساخته می‌شد و `_Mode=2` روی Standard داشت که با URP هیچ اثری ندارد ⇒ نشانگر، مات و
        /// زیر خطِ رندرِ جدید بی‌رندر می‌ماند. این‌جا هر دو حالت درست ست می‌شوند.
        /// </summary>
        public static Material Ghost(Color color, float alpha = 0.48f)
        {
            string key = "ghost|" + ColorKey(color) + "|" + alpha.ToString("F2", CultureInfo.InvariantCulture);
            if (_materials.TryGetValue(key, out Material existing) && existing != null) return existing;

            Shader shader = ResolveShader("Universal Render Pipeline/Lit", "Standard", "UI/Default");
            Material material = new Material(shader);
            material.name = "BaziGhost";
            color.a = Mathf.Clamp01(alpha);
            material.color = color;
            if (material.HasProperty("_BaziColor")) material.SetVector("_BaziColor", color);
            material.SetFloat("_Surface", 1f);              // Transparent (URP Lit)
            material.SetFloat("_Mode", 3f);                 // Fade (Standard)
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cutoff", 0.0f);
            material.SetFloat("_BaziCull", 0f);             // هر دو رویه ⇒ نشانگر از هر زاویه‌ای دیده می‌شود
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = 3000;
            _materials[key] = material;
            return material;
        }

        /// <summary>شدتِ سوسوی ذراتِ مرتبط با آتش (فاز ۴ از همین‌جا تغذیه می‌کند).</summary>
        public static void SetParticleFlicker(Material material, float amount, float speed)
        {
            if (material == null) return;
            material.SetFloat("_BaziFlicker", Mathf.Clamp01(amount));
            material.SetFloat("_BaziFlickerSpeed", Mathf.Max(0.1f, speed));
        }

        private static void ApplyTextures(Material material, SurfaceStyle style)
        {
            switch (style)
            {
                case SurfaceStyle.Ground:
                    SetTexture(material, "_BaziBaseMap", "GroundAlbedo");
                    SetTexture(material, "_BaziNormalMap", "GroundNormal", true);
                    SetTexture(material, "_BaziMaskMap", "GroundMask", true);
                    SetTexture(material, "_BaziDetailMap", "DetailNoise");
                    material.SetFloat("_BaziDetailScale", 1.15f);
                    material.SetFloat("_BaziNormalScale", 1.6f);
                    material.SetFloat("_BaziOcclusionStrength", 0.8f);
                    material.SetFloat("_BaziRoughness", 0.95f);
                    break;
                case SurfaceStyle.Rock:
                    SetTexture(material, "_BaziBaseMap", "RockAlbedo");
                    SetTexture(material, "_BaziNormalMap", "RockNormal", true);
                    SetTexture(material, "_BaziMaskMap", "RockMask", true);
                    SetTexture(material, "_BaziDetailMap", "DetailNoise");
                    material.SetFloat("_BaziDetailScale", 0.85f);
                    material.SetFloat("_BaziNormalScale", 1.9f);
                    material.SetFloat("_BaziRoughness", 0.9f);
                    break;
                case SurfaceStyle.Bark:
                    SetTexture(material, "_BaziBaseMap", "BarkAlbedo");
                    SetTexture(material, "_BaziNormalMap", "BarkNormal", true);
                    material.SetFloat("_BaziDetailScale", 0.4f);
                    material.SetFloat("_BaziNormalScale", 2.2f);
                    material.SetFloat("_BaziRoughness", 0.98f);
                    break;
                case SurfaceStyle.Foliage:
                    SetTexture(material, "_BaziBaseMap", "LeafCluster");
                    SetTexture(material, "_BaziNormalMap", "LeafNormal", true);
                    material.SetFloat("_BaziRoughness", 0.85f);
                    material.SetFloat("_BaziNormalScale", 1.1f);
                    material.SetFloat("_BaziMoistDarken", 0.5f);
                    break;
                case SurfaceStyle.Panel:
                case SurfaceStyle.Metal:
                    SetTexture(material, "_BaziBaseMap", "PanelAlbedo");
                    SetTexture(material, "_BaziNormalMap", "PanelNormal", true);
                    SetTexture(material, "_BaziMaskMap", "PanelMask", true);
                    SetTexture(material, "_BaziDamageMap", "DamageMask", true);
                    material.EnableKeyword("_BAZI_DAMAGE_ON");
                    material.SetFloat("_BaziDetailScale", 0.7f);
                    material.SetFloat("_BaziNormalScale", style == SurfaceStyle.Metal ? 0.7f : 1.2f);
                    material.SetFloat("_BaziRoughness", style == SurfaceStyle.Metal ? 0.45f : 0.8f);
                    material.SetFloat("_BaziMoistDarken", 0.55f);
                    material.SetFloat("_BaziSmoothnessBoost", 0.7f);
                    break;
                case SurfaceStyle.Ember:
                    material.SetVector("_BaziEmissionColor", new Vector4(1.4f, 0.55f, 0.16f, 1f));
                    material.EnableKeyword("_BAZI_EMISSIVE_ON");
                    break;
                default:
                    // Tinted: بدون نقشه؛ فقط رنگِ خالص با نورپردازیِ URP/built-in
                    break;
            }
        }

        private static void SetTexture(Material material, string property, string resourceName, bool linear = false)
        {
            if (!material.HasProperty(property)) return;
            Texture texture = Texture(resourceName, linear);
            if (texture != null) material.SetTexture(property, texture);
        }

        private static string ColorKey(Color color)
        {
            return color.r.ToString("F3", CultureInfo.InvariantCulture) + color.g.ToString("F3", CultureInfo.InvariantCulture)
                + color.b.ToString("F3", CultureInfo.InvariantCulture) + color.a.ToString("F3", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ حالت‌های جهانی

        /// <summary>
        /// بادِ محیطی: x=بسامدِ حرکت، y=شدت، z=فاز، w=توانِ ارتفاع (چقدر بالا أكثر از پایین).
        /// تنها نویسنده <c>WindField</c> است؛ <c>GraphicsDirector</c> فقط مقدارِ اولیه می‌دهد.
        /// </summary>
        public static void SetWind(Vector4 state)
        {
            Shader.SetGlobalVector("_BaziWindState", state);
        }

        /// <summary>همان SetWind، با چهار عددِ جدا (خواناییِ بیشتر در فراخوان‌ها).</summary>
        public static void SetWind(float speed, float strength, float phase, float heightPower)
        {
            Shader.SetGlobalVector("_BaziWindState", new Vector4(
                Mathf.Max(0f, speed),
                Mathf.Clamp(strength, 0f, 4f),
                phase,
                Mathf.Clamp(heightPower, 0.5f, 8f)));
        }

        /// <summary>مه ارتفاعی + رنگ آن (چگالی/افت/سقف/کف) — توسط سامانه‌ی نور و هوا نوشته می‌شود.</summary>
        public static void SetHeightFog(Vector4 density, Color tint)
        {
            Shader.SetGlobalVector("_BaziHeightFog", density);
            Shader.SetGlobalVector("_BaziFogTint", new Vector4(tint.r, tint.g, tint.b, tint.a));
        }

        /// <summary>
        /// وضعیتِ کلی اتمسفر: x=شب، y=مقیاس نور محیطی، z=رطوبت، w=گردوغبار.
        /// هر سامانه‌ای که یکی از این‌ها را عوض می‌کند، همین‌جا می‌نویسد (تک‌نویسنده، بدون تزاحم).
        /// </summary>
        public static void SetAtmosphere(float night01, float ambientScale, float wetness, float dust)
        {
            Shader.SetGlobalVector("_BaziAtmosphere", new Vector4(
                Mathf.Clamp01(night01),
                Mathf.Max(0.01f, ambientScale),
                Mathf.Clamp01(wetness),
                Mathf.Clamp01(dust)));
        }

        /// <summary>پارامترهایِ مه‌اینکِ محیطی برای Renderer Feature (بدون نیاز به دسترسی به خودِ Feature).</summary>
        public static void SetAmbientOcclusionParams(float intensity, float radius, float samples, bool enabled)
        {
            Shader.SetGlobalVector("_BaziAOParams", new Vector4(
                enabled ? Mathf.Clamp(intensity, 0f, 2f) : 0f,
                Mathf.Clamp(radius, 0.02f, 4f),
                enabled ? Mathf.Clamp(samples, 1f, 64f) : 0f,
                enabled ? 1f : 0f));
        }

        /// <summary>خواندنِ یکی از ثابت‌های جهانی (برای دیباگ و تست‌ها؛ هیچ مقدارِ تازه‌ای نمی‌نویسد).</summary>
        public static Vector4 GetGlobalVector(string shaderProperty)
        {
            return Shader.GetGlobalVector(shaderProperty);
        }

        /// <summary>نامِ شیدرِ property‌ی اتمسفر؛ تست‌ها و ابزارها همین را می‌خوانند.</summary>
        public static Vector4 AtmosphereState { get { return GetGlobalVector("_BaziAtmosphere"); } }

        /// <summary>
        /// متریالِ آسمانِ رویه‌ای (یک نمونه‌ی کش‌شده که SkyLightingRig هر فریم پارامترهایش را
        /// تنظیم می‌کند). برخلافِ Surface/Emissive اینجا از زنجیره‌ی جایگزین استفاده نمی‌کنیم:
        /// اگر شیدرِ آسمان نبود باید null برگردد تا Rig دوربین را روی SolidColor بگذارد؛
        /// یک شیدرِ Diffuse به‌عنوانِ آسمان، آسمانِ سفیدِ بی‌معنی می‌سازد.
        /// </summary>
        public static Material Sky()
        {
            Shader shader;
            _shaderCache.TryGetValue(SkyShaderName, out shader);
            if (shader == null) shader = Resources.Load<Shader>(SkyShaderResource);
            if (shader == null) shader = Shader.Find(SkyShaderName);
            if (shader == null)
            {
                _shaderCache[SkyShaderName] = null;
                return null;
            }
            _shaderCache[SkyShaderName] = shader;

            string key = "sky:" + shader.name;
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;
            material = new Material(shader)
            {
                name = "BaziBaqa Sky",
                hideFlags = HideFlags.HideAndDontSave
            };
            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// متریالِ آب. زیرِ URP شیدرِ اختصاصیِ پروژه را می‌گیرد (موج + fresnel + کف) و زیرِ
        /// Built-in به همان متریالِ Surface برمی‌گردد: موج را از دست می‌دهیم، ولی صحنه هرگز
        /// ارغوانی نمی‌شود. پارامترها را <c>VfxDirector</c>/<c>WaterTint</c> با نور و هوا ست می‌کند.
        /// </summary>
        public static Material Water(Color deep, Color shallow, Color sky, Vector4 wave, float smoothness = 0.82f)
        {
            if (!IsUniversal)
            {
                return Surface(SurfaceStyle.Metal, deep, 0.42f, Mathf.Clamp01(smoothness));
            }
            string key = "water|" + ColorKey(deep) + "|" + ColorKey(shallow) + "|" + ColorKey(sky)
                + "|" + wave.ToString("F2", CultureInfo.InvariantCulture) + "|" + smoothness.ToString("F2", CultureInfo.InvariantCulture);
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;

            Shader shader = ResolveShader(WaterShaderName, "res:" + WaterShaderResource);
            material = new Material(shader) { name = "BaziWater" };
            material.SetColor("_BaziWaterDeep", deep);
            material.SetColor("_BaziWaterShallow", shallow);
            material.SetColor("_BaziWaterSky", sky);
            material.SetVector("_BaziWaterWave", wave);
            material.SetVector("_BaziWaterScroll", new Vector4(0.03f, -0.021f, 1f, 0.6f));
            material.SetFloat("_BaziWaterSmoothness", Mathf.Clamp01(smoothness));
            Texture detail = Texture("DetailNoise", false);
            if (detail != null) material.SetTexture("_BaziWaterDetail", detail);
            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// متریالِ شعله/دود/جرقه (ذرات). mode: 0=آتش، 1=دود، 2=جرقه. اگر شیدرِ آتش نبود،
        /// از متریالِ ذراتِ معمولی استفاده می‌کنیم تا هیچ افکتی ناپدید نشود.
        /// </summary>
        public static Material Fire(Color hot, Color cold, int mode, float intensity = 3.2f)
        {
            string key = "fire|" + ColorKey(hot) + "|" + ColorKey(cold) + "|" + mode + "|" + intensity.ToString("F2", CultureInfo.InvariantCulture);
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;

            Shader shader = ResolveShader(FireShaderName, "res:" + FireShaderResource);
            if (shader == null || shader.name != FireShaderName)
            {
                material = Particle(hot, true);
                _materials[key] = material;
                return material;
            }
            material = new Material(shader) { name = "BaziFire" + mode };
            material.SetColor("_BaziFireHot", hot);
            material.SetColor("_BaziFireCold", cold);
            material.SetColor("_BaziFireSmoke", new Color(cold.r * 0.22f, cold.g * 0.2f, cold.b * 0.2f, 1f));
            material.SetFloat("_BaziFireMode", Mathf.Clamp(mode, 0, 2));
            material.SetVector("_BaziFireParams", new Vector4(1.1f + mode * 0.2f, 0.65f, 1.45f, Mathf.Max(0.05f, intensity)));
            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// متریالِ شیشه‌ایِ پنل‌ها و دکمه‌ها (گام ۶). یک متریالِ مشترک برای همه ⇒ یک batch.
        /// اگر شیدرِ UI حل نشد null برمی‌گردد و `Image` همان رنگِ تختِ قبل را نگه می‌دارد.
        /// </summary>
        public static Material Glass()
        {
            Shader shader = ResolveShader(GlassShaderName, "res:" + GlassShaderResource);
            if (shader == null || shader.name != GlassShaderName)
            {
                _diagnostics.Add("ui-glass=unavailable (panels render flat)");
                return null;
            }

            const string key = "ui-glass";
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;
            material = new Material(shader)
            {
                name = "BaziBaqa UIGlass",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetVector("_BaziGlassParams", new Vector4(0.34f, 0.012f, 0.055f, 0.03f));
            material.SetColor("_BaziGlassTint", new Color(0.14f, 0.36f, 0.44f, 1f));
            material.SetColor("_BaziGlassEdge", new Color(0.55f, 0.96f, 1f, 0.7f));
            material.SetVector("_BaziGlassSweep", new Vector4(0f, 0.18f, 0.42f, 0.3f));
            material.SetVector("_BaziGlassNoise", new Vector4(9f, 0.045f, 0.3f, 0f));
            _materials[key] = material;
            return material;
        }

        /// <summary>تنظیمِ شدتِ لبه/جاروب برای کل رابط (مقدارِ ۰ یعنی حالتِ بی‌جنب‌وجوشِ منو).</summary>
        public static void SetGlassMood(float edgeStrength, float sweepAmount, float tintBrightness)
        {
            Material material = Glass();
            if (material == null) return;
            Color edge = material.GetColor("_BaziGlassEdge");
            edge.a = Mathf.Clamp01(edgeStrength);
            material.SetColor("_BaziGlassEdge", edge);
            Vector4 sweep = material.GetVector("_BaziGlassSweep");
            sweep.w = Mathf.Clamp01(sweepAmount);
            material.SetVector("_BaziGlassSweep", sweep);
            Vector4 noise = material.GetVector("_BaziGlassNoise");
            noise.z = Mathf.Clamp01(0.12f + 0.5f * tintBrightness);
            material.SetVector("_BaziGlassNoise", noise);
        }

        /// <summary>فازِ جاروبِ نور (۰..۱)؛ توسط نخستین پنلِ فعال رانده می‌شود، نه هر پنل.</summary>
        public static void SetGlassSweepPhase(float phase)
        {
            Material material;
            // عمداً بدونِ Glass(): وقتی شیدر در دسترس نیست، هر فریم نباید دوباره تلاش/یادداشت کند
            if (!_materials.TryGetValue("ui-glass", out material) || material == null) return;
            Vector4 sweep = material.GetVector("_BaziGlassSweep");
            sweep.x = phase;
            material.SetVector("_BaziGlassSweep", sweep);
        }

        /// <summary>پاک‌سازیِ کش (برای تست‌ها و Reload دامنه‌ی ویرایشگر).</summary>
        public static void ResetCache()
        {
            _materials.Clear();
            _shaderCache.Clear();
            _textureCache.Clear();
            _diagnostics.Clear();
            _resolutionLogged = null;
            _resolvedSurfaceShaderPath = string.Empty;
        }
    }
}
