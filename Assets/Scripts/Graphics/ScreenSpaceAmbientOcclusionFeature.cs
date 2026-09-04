#if BAZI_UNIVERSAL
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BaziBaqa
{
    /// <summary>
    /// مه‌اینکِ محیطی (SSAO) به‌عنوان یک Universal Renderer Feature.
    ///
    /// چرا Renderer Feature و نه یک افکتِ پس‌پردازِ معمولی؟ چون AO باید بعد از نورپردازیِ
    /// اشیای کدر و پیش از Bloom/ColorAdjustments اعمال شود تا لبه‌هایِ تیره در Bloom نسوزند.
    ///
    /// طراحی مقاوم:
    ///  * Feature همیشه در صف است؛ خاموش/روشن شدن روی GPU با `_BaziAOParams.w` انجام می‌شود که
    ///    <see cref="MaterialLibrary"/> از نمایه‌ی کیفیت می‌نویسد ⇒ برای تغییر کیفیت لازم نیست
    ///    آرایه‌ی Renderer Features داخل فایل URP دست بخورد (جایی که GUIDها شکننده‌اند).
    ///  * اگر پشتیبانی از Depth Texture روی URP Asset روشن نباشد، Pass اصلاً صف نمی‌شود
    ///    (نه خطا، نه سربار). تشخیص با reflectionِ محافظه‌کارانه است: اگر مطمئن نشدیم، خاموش.
    ///  * RTها با GetTemporaryRT گرفته و در همان فریم آزاد می‌شوند ⇒ نشتی حافظه نداریم.
    /// </summary>
    public sealed class ScreenSpaceAmbientOcclusionFeature : ScriptableRendererFeature
    {
        /// <summary>تنظیماتِ داخل فایل URP (هنرمند می‌تواند از Inspector عوض کند؛ نمایه‌ی کیفیت برنده است).</summary>
        [Serializable]
        public sealed class Options
        {
            public bool enabled = true;
            public RenderPassEvent timing = RenderPassEvent.BeforeRenderingPostProcessing;
            [Range(0.25f, 1f)] public float resolutionScale = 0.75f;
            [Range(0f, 2f)] public float intensity = 0.8f;
            [Range(0.05f, 2f)] public float radius = 0.45f;
            [Range(1f, 32f)] public float samples = 8f;
            public bool blur = true;
            public bool sceneView = false;
        }

        [SerializeField] private Options options = new Options();

        private AmbientOcclusionPass _pass;

        public Options Settings
        {
            get
            {
                if (options == null) options = new Options();
                return options;
            }
        }

        public override void Create()
        {
            if (_pass != null) _pass.Release();
            _pass = new AmbientOcclusionPass(Settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Options settings = Settings;
            if (_pass == null || renderer == null || !settings.enabled) return;
            if (renderingData.cameraData == null || renderingData.cameraData.camera == null) return;
            if (!settings.sceneView && renderingData.cameraData.camera.cameraType == CameraType.SceneView) return;
            // فقط دوربینِ پایه؛ دوربین‌های overlay عمقِ مستقلِ خودشان را ندارند
            if (renderingData.cameraData.renderType != CameraRenderType.Base) return;

            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (!PipelineSupportsDepth(asset))
            {
                MaterialLibrary.SetAmbientOcclusionParams(0f, 0f, 0f, false);
                return;
            }

            _pass.ApplyOptions(settings);
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// نامِ فیلدِ «Depth Texture» روی URP Asset بین نسخه‌ها عوض شده است
        /// (m_SupportsCameraDepthTexture در برابر m_RequireDepthTexture) ⇒ هر دو املا
        /// امتحان می‌شوند. اگر هیچ‌کدام پیدا نشد، false برمی‌گردد: بی‌خیلِ AO شدن بهتر از
        /// صحنه‌ی تاریک‌شده با یک بافتِ عمقِ بسته‌نشده است.
        /// </summary>
        internal static bool PipelineSupportsDepth(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return false;
            return ReadBool(asset, "supportsCameraDepthTexture", "requireDepthTexture");
        }

        internal static bool PipelineSupportsOpaqueTexture(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return false;
            return ReadBool(asset, "supportsCameraOpaqueTexture", "requireOpaqueTexture");
        }

        private static bool ReadBool(object target, params string[] candidateNames)
        {
            if (target == null) return false;
            Type type = target.GetType();
            foreach (string name in candidateNames)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
                {
                    try
                    {
                        return (bool)property.GetValue(target, null);
                    }
                    catch (Exception error)
                    {
                        GameLogger.Warn("Failed to read '" + name + "' from " + type.Name + ": " + error.Message);
                    }
                }
            }
            return false;
        }

        // ==================================================================
        /// <summary>یک Pass با سه گام: تولید AO از عمق، نرم‌کردن، ضرب روی رنگِ صحنه.</summary>
        private sealed class AmbientOcclusionPass : ScriptableRenderPass
        {
            private static readonly int AOParamsId = Shader.PropertyToID("_BaziAOParams");
            private static readonly int AOTextureId = Shader.PropertyToID("_BaziAOTexture");
            private static readonly int AoTempId = Shader.PropertyToID("_BaziAOTemp");
            private static readonly int BlurTempId = Shader.PropertyToID("_BaziAOBlur");
            private static readonly int ColorTempId = Shader.PropertyToID("_BaziAOColor");

            private readonly Material _material;
            private Options _settings;
            private bool _released;

            public AmbientOcclusionPass(Options settings)
            {
                _settings = settings != null ? settings : new Options();
                renderPassEvent = _settings.timing;

                Shader shader = MaterialLibrary.ResolveShader(
                    "Hidden/BaziBaqa/ScreenSpaceAO",
                    "res:Shaders/BaziBaqa-ScreenSpaceAO");
                if (shader == null)
                {
                    Debug.LogWarning("BaziBaqa AO: shader Hidden/BaziBaqa/ScreenSpaceAO not found; effect stays off.");
                    return;
                }
                _material = new Material(shader);
                _material.name = "BaziBaqa ScreenSpaceAO";
            }

            public void ApplyOptions(Options settings)
            {
                if (settings == null) return;
                _settings = settings;
                if (renderPassEvent != settings.timing) renderPassEvent = settings.timing;
                // پارامترها را هم اینجا می‌نویسیم تا اگر نمایه‌ی کیفیت اجرا نشد، Feature خودکفا باشد
                MaterialLibrary.SetAmbientOcclusionParams(settings.intensity, settings.radius, settings.samples, settings.enabled);
            }

            public void Release()
            {
                if (_released) return;
                _released = true;
                if (_material != null) UnityEngine.Object.Destroy(_material);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || _released || _settings == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("BaziBaqa.SSAO");
                bool allocated = false;
                bool colorAllocated = false;
                try
                {
                    RenderTargetIdentifier colorTarget = renderingData.cameraData.renderer != null
                        ? renderingData.cameraData.renderer.cameraColorTarget
                        : new RenderTargetIdentifier();
                    if (colorTarget.nameID <= 0) return;

                    RenderTextureDescriptor aoDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                    aoDescriptor.depthBufferBits = 0;
                    aoDescriptor.stencilBufferBits = 0;
                    aoDescriptor.msaaSampleCount = 1;
                    aoDescriptor.colorFormat = RenderTextureFormat.R8;
                    aoDescriptor.useMipMap = false;
                    aoDescriptor.enableRandomWrite = false;
                    float scale = Mathf.Clamp(_settings.resolutionScale, 0.25f, 1f);
                    aoDescriptor.width = Mathf.Max(2, Mathf.RoundToInt(aoDescriptor.width * scale));
                    aoDescriptor.height = Mathf.Max(2, Mathf.RoundToInt(aoDescriptor.height * scale));

                    cmd.GetTemporaryRT(AoTempId, aoDescriptor, FilterMode.Bilinear);
                    cmd.GetTemporaryRT(BlurTempId, aoDescriptor, FilterMode.Bilinear);
                    allocated = true;

                    RenderTargetIdentifier aoTarget = new RenderTargetIdentifier(AoTempId);
                    RenderTargetIdentifier blurTarget = new RenderTargetIdentifier(BlurTempId);

                    cmd.SetGlobalVector(AOParamsId, new Vector4(
                        Mathf.Clamp01(_settings.intensity),
                        Mathf.Max(0.05f, _settings.radius),
                        Mathf.Max(1f, _settings.samples),
                        _settings.enabled ? 1f : 0f));

                    // ۱) تولید و ۲) نرم‌کردن (منبعِ Pass تولید فقط راه‌اندازِ فول‌اسکرین است؛ عمق از global خوانده می‌شود)
                    cmd.Blit(colorTarget, aoTarget, _material, 0);
                    if (_settings.blur) cmd.Blit(aoTarget, blurTarget, _material, 1);
                    else cmd.Blit(aoTarget, blurTarget);
                    cmd.SetGlobalTexture(AOTextureId, blurTarget);

                    // ۳) ترکیب روی رنگِ صحنه؛ Blit مستقیم به خودِ هدف مجاز نیست ⇒ یک RT هم‌اندازه
                    RenderTextureDescriptor colorDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                    colorDescriptor.useMipMap = false;
                    colorDescriptor.enableRandomWrite = false;
                    cmd.GetTemporaryRT(ColorTempId, colorDescriptor, FilterMode.Bilinear);
                    colorAllocated = true;
                    RenderTargetIdentifier combinedTarget = new RenderTargetIdentifier(ColorTempId);
                    cmd.Blit(colorTarget, combinedTarget, _material, 2);
                    cmd.Blit(combinedTarget, colorTarget);
                }
                catch (Exception error)
                {
                    // هر خطا در یک افکتِ تزئینی نباید بازی را متوقف کند
                    Debug.LogWarning("BaziBaqa AO: pass failed and was skipped: " + error.Message);
                }
                finally
                {
                    if (allocated)
                    {
                        cmd.ReleaseTemporaryRT(AoTempId);
                        cmd.ReleaseTemporaryRT(BlurTempId);
                    }
                    if (colorAllocated) cmd.ReleaseTemporaryRT(ColorTempId);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }
    }
}
#endif // BAZI_UNIVERSAL
