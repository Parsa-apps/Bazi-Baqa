using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌های «بهینه‌سازیِ گرافیک» (فاز ۳، گام ۷): بودجه‌ی دید در نمایه هست و واقعی
    /// مصرف می‌شود، کالر فقط رندر را خاموش می‌کند (نه شیء، نه فیزیک)، QualitySettings همچنان
    /// یک نویسنده دارد و بادِ هر سطح واقعاً به شیدر می‌رسد.
    /// </summary>
    public class PerformanceLayerEditModeTests
    {
        private const string GraphicsDir = "Assets/Scripts/Graphics";
        private const string ScriptsDir = "Assets/Scripts";
        private const string ProfileJson = "Assets/Resources/Graphics/GraphicsProfile.json";

        private static string ProjectPath(string relative)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Read(string relative)
        {
            string path = ProjectPath(relative);
            Assert.IsTrue(File.Exists(path), "فایل لازم نیست: " + relative);
            return File.ReadAllText(path);
        }

        private static string CodeOnly(string text)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                int cut = line.IndexOf("//", StringComparison.Ordinal);
                builder.Append(cut >= 0 ? line.Substring(0, cut) : line).Append('\n');
            }
            return builder.ToString();
        }

        /// <summary>بدنه‌ی یک متد (آکولادِ هم‌تراز) برای بررسیِ موضعیِ allocations.</summary>
        private static string ExtractMethod(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            int open = code.IndexOf('{', start);
            if (open < 0) return string.Empty;
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}')
                {
                    depth--;
                    if (depth == 0) return code.Substring(open, i - open + 1);
                }
            }
            return code.Substring(open);
        }

        /// <summary>مقدارِ یک کلیدِ عددی در بلوکِ «id: tier» (لنگر روی خودِ تیر، نه defaultTier).</summary>
        private static float ReadTierNumber(string json, string tierId, string key)
        {
            Match idMatch = null;
            foreach (Match match in Regex.Matches(json, "\"id\"\\s*:\\s*\"" + tierId + "\""))
            {
                idMatch = match;
            }
            Assert.IsNotNull(idMatch, "تیرِ " + tierId + " در نمایه نیست");
            string tail = json.Substring(idMatch.Index, Math.Min(2400, json.Length - idMatch.Index));
            Match value = Regex.Match(tail, "\"" + key + "\"\\s*:\\s*(-?[0-9.]+)");
            Assert.IsTrue(value.Success, key + " در تیرِ " + tierId + " تعریف نشده");
            return float.Parse(value.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void ProfileV4_CarriesVisibilityBudgets()
        {
            string profile = Read(GraphicsDir + "/GraphicsProfile.cs");
            StringAssert.Contains("public const int CurrentVersion = 4", profile);
            StringAssert.Contains("public float shadowCasterDistance", profile);
            StringAssert.Contains("public float cullDistance", profile);
            StringAssert.Contains("public int maxCulledObjects", profile);
            StringAssert.Contains("if (shadowCasterDistance > cullDistance) shadowCasterDistance = cullDistance", profile,
                "سایه نباید از شیء زنده‌تر باشد");
            StringAssert.Contains("cullDistance is dangerously small", profile, "Validate هم آن را می‌سنجد");

            string json = Read(ProfileJson);
            StringAssert.Contains("\"version\": " + GraphicsProfile.CurrentVersion, json.Replace("\r", string.Empty),
                "فایل و کد هم‌نسخه بمانند");
        }

        [Test]
        public void VisibilityBudgets_AreMonotonicAcrossTiers()
        {
            string json = Read(ProfileJson);
            string[] tiers = { "low", "medium", "high", "ultra" };
            float previousCull = 0f;
            for (int i = 0; i < tiers.Length; i++)
            {
                float cull = ReadTierNumber(json, tiers[i], "cullDistance");
                float shadow = ReadTierNumber(json, tiers[i], "shadowCasterDistance");
                int maxObjects = (int)ReadTierNumber(json, tiers[i], "maxCulledObjects");
                Assert.GreaterOrEqual(cull, 30f, tiers[i] + ": فاصله‌ی کالینگ از این کم‌تر، زمینِ بازی را ناپدید می‌کند");
                Assert.LessOrEqual(shadow, cull, tiers[i] + ": سایه از حدِ دید دورتر است");
                Assert.GreaterOrEqual(maxObjects, 16);
                Assert.Greater(cull, previousCull, tiers[i] + ": بودجه‌ی دید باید با کیفیت بالا برود");
                previousCull = cull;
            }
        }

        [Test]
        public void DistanceCuller_OnlyTogglesRenderers()
        {
            string code = CodeOnly(Read(GraphicsDir + "/DistanceCuller.cs"));
            StringAssert.Contains("renderer.enabled = false", code);
            StringAssert.Contains("renderer.shadowCastingMode = ShadowCastingMode.Off", code);
            // هیچ‌وقت GameObject را خاموش/نابود نمی‌کند و فیزیک را دست نمی‌زند
            foreach (string banned in new[] { "SetActive(", ".Destroy(", "Collider", "rigidbody", "Instantiate(" })
            {
                Assert.IsFalse(code.Contains(banned), "کالر نباید این کار را بکند: " + banned);
            }
            StringAssert.Contains("public void RestoreAll()", code);
            Assert.IsTrue(Regex.IsMatch(code, @"private void OnDisable\(\)[\s\S]*?RestoreAll\(\);"),
                "خاموش‌شدنِ مدیر باید همه‌چیز را برگرداند");
        }

        [Test]
        public void DistanceCuller_IsRateLimitedAndAllocationFree()
        {
            string code = CodeOnly(Read(GraphicsDir + "/DistanceCuller.cs"));
            StringAssert.Contains("private float _timer = 999f;", code, "پاسِ زمان‌سنجیده، نه هر فریم");
            StringAssert.Contains("GeometryUtility.CalculateFrustumPlanes(_camera, _planes)", code,
                "اورلودِ بی‌allocation با بافرِ ۶ عنصری");
            StringAssert.Contains("new Plane[6]", code);
            StringAssert.Contains("private readonly List<Target> _pool", code, "Targetها دوباره استفاده می‌شوند");

            string update = ExtractMethod(code, "private void Update()");
            Assert.IsFalse(update.Contains("new "), "Update نباید چیزی تازه بسازد");
            string runPass = ExtractMethod(code, "private void RunPass()");
            Assert.IsFalse(runPass.Contains("new List"), "پاسِ سنجش نباید لیست تازه بسازد");
            Assert.IsFalse(runPass.Contains("GetComponentInChildren<Renderer>"),
                "جمع‌آوریِ رندرها در پاسِ هر ۰٫۲۵ ثانیه تکرار نشود (فقط در Collect)");
        }

        [Test]
        public void QualitySettings_StillHasOneWriter()
        {
            string root = ProjectPath(ScriptsDir);
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOrder.AllDirectories))
            {
                string normalized = file.Replace("\\", "/");
                string code = CodeOnly(File.ReadAllText(normalized));
                foreach (Match match in Regex.Matches(code, @"QualitySettings\.([A-Za-z0-9_]+)\s*(\+|-|\*|/)?=[^=]"))
                {
                    Assert.IsTrue(normalized.Contains("/Graphics/RenderPipelineBridge.cs"),
                        "تنظیماتِ کیفیتِ بصری فقط در RenderPipelineBridge نوشته می‌شود؛ " +
                        match.Value + " در " + normalized);
                }
            }
        }

        [Test]
        public void Bridge_AppliesHardwareBudgetsFromProfile()
        {
            string code = Read(GraphicsDir + "/RenderPipelineBridge.cs");
            StringAssert.Contains("QualitySettings.particleRaycastBudget = Mathf.Clamp(tier.particleBudget", code);
            StringAssert.Contains("QualitySettings.asyncUploadTimeSlice", code);
            StringAssert.Contains("QualitySettings.asyncUploadBufferSize", code);
            StringAssert.Contains("QualitySettings.asyncUploadPersistentBuffer", code);
            StringAssert.Contains("QualitySettings.masterTextureLimit = Mathf.Clamp(tier.textureLimit, 0, 3)", code,
                "مقیاسِ بافت همان ستونِ نمایه است (حافظه‌ی روی گوشیِ متوسط)");
        }

        [Test]
        public void WindScale_IsActuallyConsumed()
        {
            string wind = CodeOnly(Read(GraphicsDir + "/WindField.cs"));
            StringAssert.Contains("_tierScale = Mathf.Clamp(tier.windScale, 0.25f, 2f)", wind);
            StringAssert.Contains("MaterialLibrary.SetWind(_speed, _strength * _tierScale, _phase, gustSharpness)", wind,
                "ضریبِ نمایه باید در بردارِ سراسریِ باد دیده شود");
        }

        [Test]
        public void Culler_IsInstalledByGraphicsDirector()
        {
            string director = Read(GraphicsDir + "/GraphicsDirector.cs");
            StringAssert.Contains("Cull = GetOrAdd<DistanceCuller>();", director);
            StringAssert.Contains("Cull.ApplyTier()", director);
            StringAssert.Contains("Cull.Refresh()", director);
            StringAssert.Contains("Cull.Report()", director);

            // و هیچ فایلِ Gameplay ای آن را نمی‌شناسد
            string root = ProjectPath(ScriptsDir);
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOrder.AllDirectories))
            {
                string normalized = file.Replace("\\", "/");
                if (normalized.Contains("/Graphics/")) continue;
                Assert.IsFalse(File.ReadAllText(normalized).Contains("DistanceCuller"),
                    "منطقِ بازی نباید از کالر چیزی بخواهد: " + normalized);
            }
        }
    }
}
