using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
#if BAZI_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// نصب‌کننده‌ی خطِ رندر URP برای «سرزمین بقا».
    ///
    /// چرا اسکریپت و نه فایل‌های آماده در مخزن؟ چون `UniversalRenderPipelineAsset` و
    /// `UniversalRendererData` به اسکریپت‌ها/شیدرهای *داخل پکیج* ارجاع می‌دهند؛ GUIDِ آن ارجاع‌ها
    /// بدون باز شدن پروژه قابل‌تولید نیست و اگر دستی در YAML نوشته شوند، یونیتی آن‌ها را
    /// «Missing Reference» می‌کند. این ابزار همان کاری را می‌کند که منویِ خودِ یونیتی می‌کند — با API —
    /// پس فایل‌های تولیدی همیشه معتبرند و قابل‌تکرار.
    ///
    /// اصلِ ایمنی: اگر هر چیزی قابل‌تأیید نباشد، خطِ رندر **فعال نمی‌شود**؛ فقط گزارش می‌دهد
    /// چه مرحله‌ای مانده. بازی هیچ‌وقت به حالتِ نیمه‌نصب رها نمی‌شود.
    /// </summary>
    public static class RenderingPipelineSetup
    {
        private const string SettingsFolder = "Assets/Settings/URP";
        private const string ReportPath = "Logs/RenderingSetupReport.txt";
        private const string RendererSuffix = "_Renderer";

        // فهرستِ نام‌هایِ سریال‌شده (serialized) که بین نسخه‌های URP جابه‌جا شده‌اند؛ اولین نامِ موجود نوشته می‌شود.
        private static readonly string[] DepthTextureNames = { "m_SupportsCameraDepthTexture", "m_RequireDepthTexture" };
        private static readonly string[] OpaqueTextureNames = { "m_SupportsCameraOpaqueTexture", "m_RequireOpaqueTexture" };
        private static readonly string[] SoftShadowsNames = { "m_SoftShadowsSupported", "m_UseSoftShadows" };
        private static readonly string[] PostProcessNames = { "m_PostProcessData" };

        // ------------------------------------------------------------------ منوها

        [MenuItem("BaziBaqa/Rendering/Install URP Assets")]
        public static void InstallMenu()
        {
            Install(true);
        }

        [MenuItem("BaziBaqa/Rendering/Validate Rendering Setup")]
        public static void ValidateMenu()
        {
            Report report = Validate();
            WriteReport(report, false);
            report.ShowDialog();
        }

        [MenuItem("BaziBaqa/Rendering/Unassign Render Pipeline (Safe Revert)")]
        public static void RevertMenu()
        {
            Unassign();
            Debug.Log("BaziBaqa Rendering: خطِ رندر از ProjectSettings/QualitySettings جدا شد؛ فایل‌های URP در "
                + SettingsFolder + " می‌مانند (برای حذف کامل، پوشه را پاک کنید).");
        }

        /// <summary>ورودیِ خطِ فرمان: `Unity -batchmode -quit -executeMethod BaziBaqa.EditorTools.RenderingPipelineSetup.InstallBatch`</summary>
        public static void InstallBatch()
        {
            Report report = Install(false);
            WriteReport(report, true);
            if (!report.Success)
            {
                throw new InvalidOperationException("URP install failed: " + report.FirstError);
            }
        }

        /// <summary>اعتبارسنجیِ خشک (بدون نوشتن فایل) برای CI.</summary>
        public static void ValidateBatch()
        {
            Report report = Validate();
            WriteReport(report, false);
            if (!report.Success)
            {
                throw new InvalidOperationException("URP validation failed: " + report.FirstError);
            }
        }

        // ------------------------------------------------------------------ نصب

        public static Report Install(bool interactive)
        {
            Report report = new Report();
            report.Log("حالت: " + (interactive ? "منوی ویرایشگر" : "خط فرمان"));
#if !BAZI_UNIVERSAL
            report.Fail("پکیج `com.unity.render-pipelines.universal` در این پروژه resolve نشده است. "
                + "اول `Packages > Package Manager > Universal RP > Install` یا باز کردنِ پروژه با اینترنت؛ "
                + "سپس همین منو را دوباره اجرا کنید.");
            return report;
#else
            EnsureFolder(SettingsFolder, report);
            GraphicsProfile profile = GraphicsProfile.Load(true);
            List<string> issues = new List<string>();
            profile.Validate(issues);
            for (int i = 0; i < issues.Count; i++) report.Warn("نمایه‌ی گرافیک: " + issues[i]);

            UniversalRenderPipelineAsset primary = null;
            Dictionary<string, UniversalRenderPipelineAsset> created = new Dictionary<string, UniversalRenderPipelineAsset>();

            for (int i = 0; i < profile.Tiers.Count; i++)
            {
                GraphicsProfile.TierSettings tier = profile.Tiers[i];
                UniversalRenderPipelineAsset asset = CreatePipelineAsset(tier, report);
                if (asset == null) continue;
                created[tier.id] = asset;
                if (primary == null) primary = asset;
            }

            if (primary == null)
            {
                report.Fail("هیچ URP Asset ای ساخته نشد؛ نصب متوقف شد (بازی در همان حالتِ قبل اجرا می‌ماند).");
                return report;
            }

            AssignToQualityLevels(created, primary, report);

            report.Success = report.Errors.Count == 0;
            return report;
#endif
        }

#if BAZI_UNIVERSAL
        private static UniversalRenderPipelineAsset CreatePipelineAsset(GraphicsProfile.TierSettings tier, Report report)
        {
            if (tier == null) return null;
            string rendererPath = SettingsFolder + "/" + "BaziBaqa" + RendererSuffix + "_" + tier.id + ".asset";
            string assetPath = SettingsFolder + "/BaziBaqa_" + tier.id + ".asset";

            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "BaziBaqa Renderer " + tier.id;
                AssetDatabase.CreateAsset(rendererData, rendererPath);
                report.Log("ساخته شد: " + rendererPath);
            }

            // PostProcessData داخل پکیج URP است؛ با جست‌وجو پیدا می‌شود (به مسیرِ فایل وابسته نیست)
            AttachPostProcessData(rendererData, report);
            AttachAmbientOcclusionFeature(rendererData, tier, report);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            // بازخوانی تا OnEnable اجرا شود و URP شیدرهای داخلی‌اش را خودش پر کند
            rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                report.Fail("Renderer Data بعد از ذخیره قابل‌بارگذاری نبود: " + rendererPath);
                return null;
            }

            UniversalRenderPipelineAsset asset = UniversalRenderPipelineAsset.Create(rendererData);
            if (asset == null)
            {
                report.Fail("UniversalRenderPipelineAsset.Create برای سطح «" + tier.id + "» نتیجه نداد.");
                return null;
            }

            asset.name = "BaziBaqa " + tier.id;
            ApplyAssetSettings(asset, tier, report);

            UniversalRenderPipelineAsset existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                report.Log("ساخته شد: " + assetPath);
            }
            else
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                asset = existing;
                report.Log("به‌روزرسانی شد: " + assetPath);
            }
            return asset;
        }

        private static void ApplyAssetSettings(UniversalRenderPipelineAsset asset, GraphicsProfile.TierSettings tier, Report report)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SetInt(serialized, "m_MainLightShadowmapResolution", Mathf.Max(256, tier.shadowResolution), report, asset);
            SetInt(serialized, "m_AdditionalLightsShadowmapResolution", Mathf.Max(256, tier.shadowResolution / 2), report, asset);
            SetInt(serialized, "m_ShadowCascadeCount", Mathf.Clamp(tier.shadowCascades, 1, 4), report, asset);
            SetFloat(serialized, "m_ShadowDistance", Mathf.Max(4f, tier.shadowDistance), report, asset);
            SetFloat(serialized, "m_ShadowDepthBias", Mathf.Clamp(tier.shadowDepthBias, 0.01f, 10f), report, asset);
            SetFloat(serialized, "m_ShadowNormalBias", Mathf.Clamp(tier.shadowNormalBias, 0f, 5f), report, asset);
            SetFloat(serialized, "m_RenderScale", Mathf.Clamp(tier.renderScale, 0.4f, 1.5f), report, asset);
            SetBool(serialized, "m_SupportsHDR", tier.hdr, report, asset);
            SetInt(serialized, "m_MSAA", Mathf.Max(1, tier.msaa), report, asset);
            SetBoolFirst(serialized, DepthTextureNames, true, report, asset);
            SetBoolFirst(serialized, OpaqueTextureNames, tier.opaqueTexture, report, asset);
            SetBoolFirst(serialized, SoftShadowsNames, tier.softShadows, report, asset);
            // نورهای اضافه: Per Pixel تا چراغ‌های شب/آتش واقعی روشن بمانند (0 = خاموش)
            SetInt(serialized, "m_AdditionalLightsRenderingMode", tier.maxAdditionalLights > 0 ? 2 : 0, report, asset);
            SetInt(serialized, "m_ColorGradingMode", tier.hdr ? 1 : 0, report, asset);
            SetInt(serialized, "m_ColorGradingLutSize", tier.hdr ? 32 : 16, report, asset);
            SetBool(serialized, "m_MixedLightingSupported", true, report, asset);
            SetBool(serialized, "m_SupportsDynamicBatching", false, report, asset);   // SRP Batcher بهتر است
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void AttachPostProcessData(UniversalRendererData rendererData, Report report)
        {
            SerializedObject serialized = new SerializedObject(rendererData);
            SerializedProperty target = null;
            foreach (string name in PostProcessNames)
            {
                target = serialized.FindProperty(name);
                if (target != null) break;
            }
            if (target == null)
            {
                report.Warn("فیلد `m_PostProcessData` روی UniversalRendererData پیدا نشد؛ اگر در کنسول "
                    + "خطای «Post Process Data not set» دیدید، از منوی Create > Rendering > URP Asset "
                    + "with Universal Renderer استفاده کنید (نسخه‌ی URP با انتظاراتِ این ابزار فرق دارد).");
                return;
            }
            if (target.objectReferenceValue != null) return;

            Guid[] guids = AssetDatabase.FindAssets("t:PostProcessData");
            foreach (Guid guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                UnityEngine.Object data = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (data == null) continue;
                target.objectReferenceValue = data;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                report.Log("PostProcessData از " + path + " متصل شد.");
                return;
            }
            report.Warn("منبعِ PostProcessData در پکیج URP پیدا نشد؛ پس‌پرداز ممکن است کامل کار نکند.");
        }

        private static void AttachAmbientOcclusionFeature(UniversalRendererData rendererData, GraphicsProfile.TierSettings tier, Report report)
        {
            SerializedObject serialized = new SerializedObject(rendererData);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            if (features == null)
            {
                report.Warn("`m_RendererFeatures` روی Renderer Data پیدا نشد؛ AO نصب نمی‌شود (بقیه‌ی تنظیمات سالم است).");
                return;
            }

            ScreenSpaceAmbientOcclusionFeature feature = null;
            for (int i = 0; i < features.arraySize; i++)
            {
                UnityEngine.Object candidate = features.GetArrayElementAtIndex(i).objectReferenceValue;
                feature = candidate as ScreenSpaceAmbientOcclusionFeature;
                if (feature != null) break;
            }

            bool created = false;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusionFeature>();
                feature.name = "BaziBaqa Screen Space AO " + tier.id;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                features.InsertArrayElementAtIndex(features.arraySize);
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                created = true;
            }

            // تنظیمات از نمایه؛ همان منبعی که MaterialLibrary هم از آن می‌خواند ⇒ دو نویسنده نداریم
            SerializedObject featureSerialized = new SerializedObject(feature);
            SerializedProperty options = featureSerialized.FindProperty("options");
            if (options != null)
            {
                SerializedProperty enabled = options.FindPropertyRelative("enabled");
                SerializedProperty intensity = options.FindPropertyRelative("intensity");
                SerializedProperty radius = options.FindPropertyRelative("radius");
                SerializedProperty samples = options.FindPropertyRelative("samples");
                SerializedProperty scale = options.FindPropertyRelative("resolutionScale");
                if (enabled != null) enabled.boolValue = tier.ao;
                if (intensity != null) intensity.floatValue = tier.aoIntensity;
                if (radius != null) radius.floatValue = tier.aoRadius;
                if (samples != null) samples.floatValue = tier.aoSampleCount;
                if (scale != null) scale.floatValue = tier.shadowResolution >= 2048 ? 1f : 0.75f;
                // `timing` عمداً دست‌نخورده می‌ماند: RenderPassEvent مقادیرِ متوالی ندارد و
                // نوشتنِ enumValueIndexِ اشتباه، زمانِ Pass را خراب می‌کند. پیش‌فرضِ Options درست است.
                featureSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(feature);
            if (created) report.Log("Feature اضافه شد: ScreenSpaceAmbientOcclusion (" + tier.id + ")");
        }
        private static void AssignToQualityLevels(Dictionary<string, UniversalRenderPipelineAsset> assets, UniversalRenderPipelineAsset fallback, Report report)
        {
            string[] names = QualitySettings.names;
            int previous = QualitySettings.GetQualityLevel();
            GraphicsProfile profile = GraphicsProfile.Load();
            for (int level = 0; level < names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                GraphicsProfile.TierSettings tier = profile.GetByQualityLevel(level);
                UniversalRenderPipelineAsset asset = null;
                if (tier != null) assets.TryGetValue(tier.id, out asset);
                if (asset == null) asset = fallback;
                QualitySettings.renderPipeline = asset;
                report.Log("کیفیت «" + names[level] + "» ⇒ " + asset.name);
            }
            QualitySettings.SetQualityLevel(previous, false);
            GraphicsSettings.defaultRenderPipeline = fallback;
            report.Log("GraphicsSettings.defaultRenderPipeline = " + fallback.name);
            AssetDatabase.SaveAssets();
        }

#endif

        private static void Unassign()
        {
            string[] names = QualitySettings.names;
            int previous = QualitySettings.GetQualityLevel();
            for (int level = 0; level < names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(previous, false);
            GraphicsSettings.defaultRenderPipeline = null;
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ اعتبارسنجی

        public static Report Validate()
        {
            Report report = new Report();
            report.Log("MaterialLibrary: " + MaterialLibrary.ResolvedSurfaceShaderPath
                + " | universal=" + MaterialLibrary.IsUniversal);

            // ۱) پکیج
            string manifest = SafeRead("Packages/manifest.json");
            if (manifest.IndexOf("com.unity.render-pipelines.universal", StringComparison.Ordinal) < 0)
            {
                report.Fail("در manifest ردیفِ URP نیست ⇒ خطِ رندر قابل‌انتخاب نیست.");
            }
            else
            {
                report.Log("manifest: URP ثبت شده است.");
            }

            // ۲) شیدرهای پروژه: باید لود شوند و خطای کامپایل نداشته باشند
            string[] shaderPaths = {
                "Assets/Resources/Shaders/BaziBaqa-Surface.shader",
                "Assets/Resources/Shaders/BaziBaqa-Emissive.shader",
                "Assets/Resources/Shaders/BaziBaqa-ScreenSpaceAO.shader",
                "Assets/Resources/Shaders/BaziBaqaCore.hlsl",
                "Assets/Resources/Shaders/BaziBaqaCore.cginc"
            };
            foreach (string path in shaderPaths)
            {
                if (!File.Exists(path))
                {
                    report.Fail("فایل شیدر نیست: " + path);
                    continue;
                }
                if (path.EndsWith(".shader", StringComparison.Ordinal))
                {
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader == null)
                    {
                        report.Fail("شیدر لود نشد: " + path);
                        continue;
                    }
#if UNITY_EDITOR
                    if (ShaderUtil.ShaderHasError(shader))
                    {
                        report.Fail("شیدر خطای کامپایل دارد: " + path);
                        int messageCount = ShaderUtil.GetShaderMessageCount(shader);
                        for (int m = 0; m < messageCount; m++)
                        {
                            ShaderMessage message = ShaderUtil.GetShaderMessage(shader, m);
                            if (message != null) report.Log("  [" + message.severity + "] " + message.file + ":" + message.line + " " + message.message);
                        }
                    }
                    else
                    {
                        report.Log("شیدر سالم: " + path + " (variants=" + shader.variantCount + ")");
                    }
#endif
                }
            }

            // ۳) وضعیتِ خطِ رندر
            RenderPipelineAsset assigned = GraphicsSettings.defaultRenderPipeline;
            if (assigned == null)
            {
                report.Warn("هیچ Render Pipeline Asset ای به پروژه وصل نیست؛ بازی با Built-in اجرا می‌شود "
                    + "(پس‌پردازِ سینمایی غیرفعال). از BaziBaqa/Rendering/Install URP Assets استفاده کنید.");
            }
            else
            {
                report.Log("خطِ رندر: " + assigned.GetType().Name + " (" + assigned.name + ")");
                for (int level = 0; level < QualitySettings.names.Length; level++)
                {
                    report.Log("  کیفیت «" + QualitySettings.names[level] + "» shadowDistance="
                        + QualitySettings.shadowDistance.ToString("F0") + " lodBias="
                        + QualitySettings.lodBias.ToString("F2"));
                }
            }

            // ۴) متریال‌ها: حل‌شدنِ شیدر و نه ارغوانی
            Shader surface = MaterialLibrary.SurfaceShader;
            if (surface == null) report.Fail("شیدرِ سطح حل نشد ⇒ همه‌چیز ارغوانی می‌شود.");
            else report.Log("شیدرِ سطح: " + surface.name);

            // ۵) نمایه
            GraphicsProfile profile = GraphicsProfile.Load(true);
            List<string> issues = new List<string>();
            profile.Validate(issues);
            for (int i = 0; i < issues.Count; i++) report.Warn("نمایه: " + issues[i]);
            report.Log(profile.Describe());
            report.Success = report.Errors.Count == 0;
            return report;
        }

        // ------------------------------------------------------------------ ابزارها

        private static void EnsureFolder(string folder, Report report)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    report.Log("پوشه ساخته شد: " + next);
                }
                current = next;
            }
        }

        private static void SetInt(SerializedObject serialized, string name, int value, Report report, UnityEngine.Object target)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) { report.Log("  (فیلد " + name + " در این نسخه URP نیست؛ رد شد)"); return; }
            property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string name, float value, Report report, UnityEngine.Object target)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) { report.Log("  (فیلد " + name + " در این نسخه URP نیست؛ رد شد)"); return; }
            property.floatValue = value;
        }

        private static void SetBool(SerializedObject serialized, string name, bool value, Report report, UnityEngine.Object target)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) { report.Log("  (فیلد " + name + " در این نسخه URP نیست؛ رد شد)"); return; }
            property.boolValue = value;
        }

        private static void SetBoolFirst(SerializedObject serialized, string[] names, bool value, Report report, UnityEngine.Object target)
        {
            foreach (string name in names)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property != null)
                {
                    property.boolValue = value;
                    return;
                }
            }
            report.Log("  (هیچ‌کدام از نام‌های " + string.Join("/", names) + " پیدا نشد؛ این تنظیم رد شد)");
        }

        private static string SafeRead(string relativePath)
        {
            try
            {
                string full = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
                return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void WriteReport(Report report, bool install)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("BaziBaqa Rendering Setup Report");
            builder.AppendLine("================================");
            builder.AppendLine("mode: " + (install ? "install" : "validate"));
            builder.AppendLine("time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            for (int i = 0; i < report.Lines.Count; i++) builder.AppendLine(report.Lines[i]);
            if (report.Errors.Count > 0)
            {
                builder.AppendLine("");
                builder.AppendLine("ERRORS:");
                for (int i = 0; i < report.Errors.Count; i++) builder.AppendLine("  - " + report.Errors[i]);
            }
            if (report.Warnings.Count > 0)
            {
                builder.AppendLine("");
                builder.AppendLine("WARNINGS:");
                for (int i = 0; i < report.Warnings.Count; i++) builder.AppendLine("  - " + report.Warnings[i]);
            }
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(ReportPath, builder.ToString());
                Debug.Log("BaziBaqa Rendering: گزارش نوشته شد → " + Path.GetFullPath(ReportPath));
            }
            catch (Exception error)
            {
                Debug.LogWarning("BaziBaqa Rendering: نوشتن گزارش ممکن نشد: " + error.Message);
            }
        }

        // ==================================================================
        /// <summary>خروجیِ ابزار؛ در تست‌های EditMode هم همین ساختار خوانده می‌شود.</summary>
        public sealed class Report
        {
            public bool Success = true;
            public readonly List<string> Lines = new List<string>();
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            public string FirstError { get { return Errors.Count > 0 ? Errors[0] : string.Empty; } }

            public void Log(string message)
            {
                Lines.Add(message);
                Debug.Log("BaziBaqa Rendering: " + message);
            }

            public void Warn(string message)
            {
                Warnings.Add(message);
                Debug.LogWarning("BaziBaqa Rendering: " + message);
            }

            public void Fail(string message)
            {
                Errors.Add(message);
                Success = false;
                Lines.Add("ERROR: " + message);
                Debug.LogError("BaziBaqa Rendering: " + message);
            }

            public void ShowDialog()
            {
                string body = (Success ? "همه‌چیز درست است." : Errors.Count + " خطا پیدا شد.") + "\n"
                    + string.Join("\n", Lines.ToArray());
                if (Warnings.Count > 0) body += "\n\nهشدارها:\n" + string.Join("\n", Warnings.ToArray());
                if (Errors.Count > 0) body += "\n\nخطاها:\n" + string.Join("\n", Errors.ToArray());
                EditorUtility.DisplayDialog("بررسی تنظیمات رندر", body.Length > 3000 ? body.Substring(0, 3000) : body, "باشه");
            }
        }
    }
}
