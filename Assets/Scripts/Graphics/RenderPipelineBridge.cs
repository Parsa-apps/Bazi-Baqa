using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace BaziBaqa
{
    /// <summary>
    /// پلِ بین «نمایه‌ی گرافیک» و تنظیماتِ واقعیِ موتور.
    ///
    /// قانونِ تک‌نویسنده: هیچ سیستمِ دیگری مستقیمِ <c>QualitySettings</c> را برای کارهای
    /// بصری عوض نکند؛ همه از این‌جا رد می‌شوند. این تنها راهی است که می‌توان تضمین کرد
    /// با یک خط تغییر، هم URP Asset، هم تنظیماتِ کیفیت و هم متریال‌ها هم‌راستا می‌مانند.
    ///
    /// مرزِ وظیفه با <c>PerformanceManager</c>: آنجا عددِ fps و سطحِ کیفیتِ انتخابی است
    /// (تصمیم)، اینجا نحوه‌ی پیاده‌سازیِ بصریِ همان سطح (اجرا). targetFrameRate هرگز از این
    /// کلاس نوشته نمی‌شود تا دو نویسنده با هم نزاع نکنند.
    /// </summary>
    public static class RenderPipelineBridge
    {
        private static int _lastAppliedQuality = -1;
        private static int _applyCount;
        private static Camera _lastCamera;

        public static int ApplyCount { get { return _applyCount; } }
        public static int LastAppliedQuality { get { return _lastAppliedQuality; } }

        /// <summary>نامِ خطِ رندرِ فعال؛ برای لاگ و تست‌ها (URP / built-in / …).</summary>
        public static string PipelineName
        {
            get
            {
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline
                    : GraphicsSettings.defaultRenderPipeline;
                if (asset == null) return "built-in";
                string typeName = asset.GetType().Name ?? string.Empty;
                if (typeName.IndexOf("Universal", StringComparison.Ordinal) >= 0) return "Universal (URP)";
                return typeName.Length == 0 ? "custom" : typeName;
            }
        }

        public static string GraphicsDevice
        {
            get { return SystemInfo.graphicsDeviceType.ToString(); }
        }

        public static bool SupportsPostProcessing { get { return MaterialLibrary.IsUniversal; } }

        /// <summary>اعمالِ یک سطحِ کیفیت روی دوربین و تنظیماتِ موتور.</summary>
        public static void ApplyTier(GraphicsProfile.TierSettings tier, Camera camera)
        {
            if (tier == null) return;
            _applyCount++;
            _lastAppliedQuality = QualitySettings.GetQualityLevel();
            _lastCamera = camera;

            if (camera != null)
            {
                camera.allowHDR = tier.hdr;
                camera.allowMSAA = tier.msaa > 1;
                // عمق برای SSAO/DoF/بارانِ عمق‌دار لازم است؛ بدون آن افکت‌ها خودشان خاموش می‌شوند
                if (camera.depthTextureMode != DepthTextureMode.Depth && tier.ao && MaterialLibrary.IsUniversal)
                {
                    camera.depthTextureMode = DepthTextureMode.Depth;
                }
            }

            // تنظیماتِ قابل‌اعمال در هر دو خطِ رندر
            QualitySettings.shadowDistance = tier.shadowDistance;
            QualitySettings.lodBias = tier.lodBias;
            QualitySettings.masterTextureLimit = Mathf.Clamp(tier.textureLimit, 0, 3);
            QualitySettings.anisotropicFiltering = tier.shadowResolution >= 1024
                ? AnisotropicFiltering.Enable
                : AnisotropicFiltering.Disable;
            QualitySettings.skinWeights = tier.renderScale >= 1f ? SkinWeights.TwoBones : SkinWeights.OneBone;
            QualitySettings.shadows = tier.shadowResolution > 0 ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.shadowCascades = Mathf.Clamp(tier.shadowCascades, 0, 4);
            QualitySettings.shadowResolution = ResolveShadowResolution(tier.shadowResolution);
            QualitySettings.antiAliasing = MaterialLibrary.IsUniversal ? 0 : Mathf.Max(0, tier.msaa);
            SetRealtimeReflectionProbes(tier.reflections);

            MaterialLibrary.SetAmbientOcclusionParams(tier.ao ? tier.aoIntensity : 0f, tier.aoRadius, tier.aoSampleCount, tier.ao);
        }

        /// <summary>اعمالِ سطحِ کیفیتِ فعلی (با یافتنِ دوربین اصلی).</summary>
        public static void ApplyCurrentQuality(bool force = false)
        {
            int level = QualitySettings.GetQualityLevel();
            if (!force && level == _lastAppliedQuality) return;
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().GetByQualityLevel(level);
            Camera camera = Camera.main != null ? Camera.main : _lastCamera;
            ApplyTier(tier, camera);
        }

        private static void SetRealtimeReflectionProbes(bool enabled)
        {
            try
            {
                QualitySettings.realtimeReflectionProbes = enabled;
            }
            catch (Exception error)
            {
                Debug.Log("BaziBaqa: realtimeReflectionProbes not settable: " + error.Message);
            }
        }

        private static ShadowResolution ResolveShadowResolution(int pixels)
        {
            if (pixels >= 4096) return ShadowResolution.VeryHigh;
            if (pixels >= 2048) return ShadowResolution.High;
            if (pixels >= 1024) return ShadowResolution.Medium;
            return ShadowResolution.Low;
        }

        /// <summary>گزارشِ یک‌خطی از وضعیت؛ در لاگ شروع و در تست‌های PlayMode خوانده می‌شود.</summary>
        public static string Describe()
        {
            GraphicsProfile profile = GraphicsProfile.Load();
            GraphicsProfile.TierSettings tier = profile.Current;
            StringBuilder builder = new StringBuilder();
            builder.Append("pipeline=").Append(PipelineName)
                .Append(" | device=").Append(GraphicsDevice)
                .Append(" | quality=").Append(SafeQualityName())
                .Append(" | scale=").Append(tier.renderScale.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | hdr=").Append(tier.hdr)
                .Append(" | msaa=").Append(tier.msaa)
                .Append(" | ao=").Append(tier.ao)
                .Append(" | dof=").Append(tier.depthOfField)
                .Append(" | shadow=").Append(tier.shadowResolution)
                .Append("x").Append(tier.shadowCascades)
                .Append(" | surface=").Append(MaterialLibrary.ResolvedSurfaceShaderPath);
            return builder.ToString();
        }

        private static string SafeQualityName()
        {
            int index = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            return names != null && index >= 0 && index < names.Length ? names[index] : index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>بازنشانیِ حالتِ داخلی (فقط برای تست).</summary>
        public static void ResetState()
        {
            _lastAppliedQuality = -1;
            _applyCount = 0;
            _lastCamera = null;
        }
    }
}
