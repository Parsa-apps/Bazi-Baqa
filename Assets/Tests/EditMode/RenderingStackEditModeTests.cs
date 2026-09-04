using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌هایِ لایه‌ی گرافیک (فاز ۳). برخلاف تست‌هایِ منطق، این‌ها «زیرساختِ بصری» را
    /// می‌سنجند: فایل‌های شیدر، تگ‌هایِ خطِ رندر، چیدمانِ CBUFFER (سازگاری با SRP Batcher)،
    /// تنظیماتِ ایمپورتِ بافت‌ها (sRGB/Repeat) و هماهنگیِ نمایه با QualitySettings.
    /// همه بررسی‌ها ایستا و روی فایل‌ها هستند ⇒ در batchmode هم بدون صحنه‌ی باز پاس می‌شوند.
    /// </summary>
    public class RenderingStackEditModeTests
    {
        private const string ShaderFolder = "Assets/Resources/Shaders";
        private const string TextureFolder = "Assets/Resources/Textures/Graphics";
        private const string ProfilePath = "Assets/Resources/Graphics/GraphicsProfile.json";

        private static string ProjectPath(string relative)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Read(string relative)
        {
            string path = ProjectPath(relative);
            Assert.IsTrue(File.Exists(path), "فایل انتظار می‌رفت وجود داشته باشد: " + relative);
            return File.ReadAllText(path);
        }

        [Test]
        public void UrpPackage_IsDeclaredAndAssembliesAreGuardedByDefine()
        {
            string manifest = Read("Packages/manifest.json");
            StringAssert.Contains("com.unity.render-pipelines.universal", manifest,
                "پکیج URP در manifest ثبت نشده؛ کل فاز ۳ روی همین خطِ رندر سوار است.");
            // پس‌پردازِ قدیمی (com.unity.postprocessing) با URP تضاد دارد؛ نباید برگردد
            StringAssert.DoesNotContain("\"com.unity.postprocessing\"", manifest,
                "پکیجِ قدیمیِ Post Processing نباید همراه URP باشد.");

            string[] asmdefs = {
                "Assets/Scripts/BaziBaqa.Runtime.asmdef",
                "Assets/Editor/BaziBaqa.Editor.asmdef",
                "Assets/Tests/EditMode/BaziBaqa.Tests.asmdef",
                "Assets/Tests/PlayMode/BaziBaqa.PlayModeTests.asmdef"
            };
            foreach (string asmdef in asmdefs)
            {
                string json = Read(asmdef);
                StringAssert.Contains("Unity.RenderPipelines.Universal.Runtime", json, asmdef + " به اسمبلی URP ارجاع ندارد");
                StringAssert.Contains("BAZI_UNIVERSAL", json, asmdef + " define‌ی نسخه‌محورِ BAZI_UNIVERSAL را ندارد");
            }
        }

        [Test]
        public void GraphicsProfile_MatchesRuntimeContract()
        {
            string json = Read(ProfilePath);
            StringAssert.Contains("\"version\": 1", json.Replace("\r", string.Empty), "نسخه‌ی نمایه باید ۱ باشد");

            GraphicsProfile profile = GraphicsProfile.Load(true);
            Assert.Greater(profile.Tiers.Count, 0, "حداقل یک سطحِ کیفیتی لازم است");
            Assert.AreEqual(profile.TierNames().Length, profile.Tiers.Count, "نامِ هر سطح باید یکتا باشد");

            List<string> issues = new List<string>();
            profile.Validate(issues);
            Assert.AreEqual(0, issues.Count, "نمایه بی‌خطا باید باشد: " + string.Join(" | ", issues.ToArray()));

            // هر سطحِ کیفیتِ پروژه باید به یک نمایه نگاشت شود و renderScale در بازه‌ی امن بماند
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                GraphicsProfile.TierSettings tier = profile.GetByQualityLevel(level);
                Assert.IsNotNull(tier, "سطحِ کیفیت " + level + " بدون نمایه ماند");
                Assert.GreaterOrEqual(tier.renderScale, 0.4f, tier.id + ": renderScale برای اندروید میان‌رده خیلی کوچک است");
                Assert.LessOrEqual(tier.renderScale, 1.5f, tier.id + ": renderScale بالای ۱.۵ در موبایل معنادار نیست");
                Assert.GreaterOrEqual(tier.particleBudget, 32);
            }
        }

        [Test]
        public void SurfaceShader_HasUniversalPathAndBuiltInFallback()
        {
            string shader = Read(ShaderFolder + "/BaziBaqa-Surface.shader");
            Assert.IsTrue(shader.Contains("\"RenderPipeline\" = \"UniversalPipeline\""),
                "مسیر URP در شیدرِ سطح نیست ⇒ زیر URP ارغوانی می‌شود");
            Assert.IsTrue(shader.Contains("LightMode\" = \"UniversalForward"), "Passِ فورواردِ URP نیست");
            Assert.IsTrue(shader.Contains("LightMode\" = \"ShadowCaster"), "بدون ShadowCaster هیچ سایه‌ای روی زمین نمی‌افتد");
            Assert.IsTrue(shader.Contains("LightMode\" = \"DepthOnly"), "برای AO/DoF به DepthOnly نیاز است");
            Assert.IsTrue(shader.Contains("Fallback"), "Fallback برای شرایطِ غیرمنتظره لازم است");
            // مسیرِ پشتیبان باید داخل CGPROGRAM باشد، وگرنه کامپایل URP را می‌شکند
            Assert.IsTrue(shader.Contains("CGPROGRAM") && shader.Contains("BaziBaqaCore.cginc"),
                "مسیرِ Built-in (CGPROGRAM) حذف شده؛ بازی بدون URP خراب رندر می‌شود");
            StringAssert.Contains("BaziApplyWind", shader, "بادِ GPU باید داخل همین شیدر باشد، نه Update روی CPU");
        }

        [Test]
        public void ShaderFiles_HlsPrograms_NeverIncludeBuiltInCginc_AndShareCbuffer()
        {
            string folder = ProjectPath(ShaderFolder);
            Assert.IsTrue(Directory.Exists(folder), "پوشه‌ی شیدرها نیست: " + ShaderFolder);
            string[] files = Directory.GetFiles(folder, "*.shader", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(files.Length, 3, "شیدرِ سطح، نورانی و AO لازم است");

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                string nice = "Assets/" + Path.GetFileName(file);
                Assert.AreEqual(Count(text, "{"), Count(text, "}"), "آکولاد نامتوازن: " + file);

                MatchCollection programs = Regex.Matches(text, "HLSLPROGRAM(.*?)ENDHLSL", RegexOptions.Singleline);
                Assert.Greater(programs.Count, 0, "هیچ بلوک HLSLPROGRAM ای در " + file + " نیست");
                foreach (Match program in programs)
                {
                    string body = program.Groups[1].Value;
                    Assert.IsFalse(body.Contains("UnityCG.cginc"),
                        "UnityCG.cginc داخل HLSLPROGRAM ⇒ شکستِ کامپایل در URP: " + file);
                    Assert.IsFalse(body.Contains("Lighting.cginc"), "Lighting.cginc فقط برای CGPROGRAM است: " + file);
                }

                // چیدمانِ یکسانِ UnityPerMaterial در همه Passها ⇒ SRP Batcher فعال می‌ماند
                List<string> layouts = new List<string>();
                foreach (Match match in Regex.Matches(text, "CBUFFER_START\\(UnityPerMaterial\\)(.*?)CBUFFER_END", RegexOptions.Singleline))
                {
                    SortedSet<string> members = new SortedSet<string>();
                    foreach (Match member in Regex.Matches(match.Groups[1].Value, @"(float4|float3|float2|float|int|half4|half3|half2|half)\s+(_?\w+)"))
                    {
                        members.Add(member.Groups(2).Value);
                    }
                    layouts.Add(string.Join(",", members));
                }
                if (layouts.Count > 1)
                {
                    for (int i = 1; i < layouts.Count; i++)
                    {
                        Assert.AreEqual(layouts[0], layouts[i],
                            "چیدمان UnityPerMaterial بین Passهای " + Path.GetFileName(file) + " فرق کرد ⇒ SRP Batcher غیرفعال می‌شود");
                    }
                }
            }
        }

        [Test]
        public void Shaders_AreImportedAndCompileWithoutErrors()
        {
            string[] names = { "BaziBaqa-Surface", "BaziBaqa-Emissive", "BaziBaqa-ScreenSpaceAO" };
            foreach (string name in names)
            {
                string path = ShaderFolder + "/" + name + ".shader";
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.IsNotNull(shader, "شیدر ایمپورت نشده: " + path + " (فایل .meta را بررسی کنید)");
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "شیدر خطای کامپایل دارد: " + path
                    + " → " + ShaderMessageCount(shader));
            }

            // قراردادِ نام‌گذاری: MaterialLibrary با «Shaders/<file>» از Resources می‌خواند
            foreach (string name in names)
            {
                string resourcePath = ShaderResourceFolder.Replace("Assets/Resources/", string.Empty) + "/" + name;
                Shader loaded = Resources.Load<Shader>(resourcePath);
                Assert.IsNotNull(loaded, "شیدر از Resources بارگذاری نشد ⇒ در بیلد هم پیدا نمی‌شود: " + name);
            }
            Assert.IsNotNull(Shader.Find("BaziBaqa/Surface"), "نامِ شیدر با قرارداردِ MaterialLibrary نمی‌خواند");
            Assert.IsNotNull(Shader.Find("Hidden/BaziBaqa/ScreenSpaceAO"), "شیدرِ AO با نامِ انتظاررفته تعریف نشده");
        }

        [Test]
        public void ImportedTextures_UseRepeatWrapAndCorrectColorSpace()
        {
            string folder = ProjectPath(TextureFolder);
            Assert.IsTrue(Directory.Exists(folder), "بافت‌هایِ رویه‌ای تولید نشده‌اند: " + TextureFolder);
            string[] pngs = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(pngs.Length, 12, "مجموعه‌ی بافت‌ها کامل نیست (albedo/normal/mask برای زمین، سنگ، پوست، پنل + ماسک آسیب + نویز)");

            foreach (string png in pngs)
            {
                string name = Path.GetFileNameWithoutExtension(png);
                string metaPath = png + ".meta";
                Assert.IsTrue(File.Exists(metaPath), "متای بافت نیست: " + name + " ⇒ Unity آن را با Wrapِ Clamp و بدون Mipmap می‌گیرد");
                string meta = File.ReadAllText(metaPath);
                StringAssert.Contains("TextureImporter:", meta, "ایمپورترِ بافت ست نشده: " + name);
                StringAssert.Contains("wrapU: 0", meta, "wrap باید Repeat (0) باشد تا تایل‌شدن درز نداشته باشد: " + name);
                StringAssert.Contains("enableMipMap: 1", meta, "mipmap برای کاهشِ نویزِ دوردست لازم است: " + name);

                bool linearExpected = name.IndexOf("Normal", StringComparison.Ordinal) >= 0
                    || name.IndexOf("Mask", StringComparison.Ordinal) >= 0
                    || name.IndexOf("Noise", StringComparison.Ordinal) >= 0;
                if (linearExpected)
                {
                    StringAssert.Contains("sRGBTexture: 0", meta, "نقشه‌ی نرمال/ماسک باید خطی باشد (sRGB خام) وگرنه عدد می‌سوزد: " + name);
                }
                else
                {
                    StringAssert.Contains("sRGBTexture: 1", meta, "بافتِ رنگی باید sRGB باشد: " + name);
                }

                // سرِ فایل PNG: امضا + IHDR با ابعادِ مربع و ۸ بیت
                byte[] header = File.ReadAllBytes(png);
                Assert.Greater(header.Length, 32, "فایل بافت خالی است: " + name);
                Assert.AreEqual(0x89, header[0], "امضای PNG غلط است: " + name);
                int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                Assert.GreaterOrEqual(width, 128, "ابعادِ بافت خیلی کوچک است: " + name);
                Assert.LessOrEqual(width, 1024, "ابعادِ بافت برای اندروید میان‌رده بزرگ است: " + name);
                Assert.AreEqual(width, height, "بافت باید مربع باشد: " + name);
            }
        }

        [Test]
        public void MaterialLibrary_IsTheOnlyShaderResolver()
        {
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets");
            List<string> offenders = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.EndsWith("MaterialLibrary.cs", StringComparison.Ordinal)) continue;
                string text = File.ReadAllText(file);
                if (text.Contains("Shader.Find(")) offenders.Add(Path.GetFileName(file));
            }
            Assert.AreEqual(0, offenders.Count,
                "همه‌ی حل‌کننده‌هایِ شیدر باید از MaterialLibrary.ResolveShader رد شوند (متخلفان: "
                + string.Join(", ", offenders.ToArray()) + ")");

            // MaterialLibrary باید همیشه یک کاندیدِ built-in داشته باشد (URP نبودنِ پکیج = نه crash)
            string resolver = Read("Assets/Scripts/Graphics/MaterialLibrary.cs");
            StringAssert.Contains("Legacy Shaders/Diffuse", resolver, "زنجیره‌ی fallback با Diffuse تمام نمی‌شود");
            StringAssert.Contains("SetGlobalVector", resolver, "باد/مه/اتمسفر باید جهانی ست شوند (تک‌نویسنده)");
        }

        [Test]
        public void CinematicVolumeRig_CoversEveryRequiredEffect()
        {
            string rig = Read("Assets/Scripts/Graphics/CinematicVolumeRig.cs");
            string[] required = { "Bloom", "Tonemapping", "ColorAdjustments", "Vignette", "DepthOfField", "FilmGrain", "ChromaticAberration", "LiftGammaGain" };
            foreach (string effect in required)
            {
                StringAssert.Contains("Add<" + effect + ">", rig, "افکتِ «" + effect + "» در استکِ سینمایی نیست (بخشِ ۱ فاز ۳)");
            }
            StringAssert.Contains("ACES", rig, "تونمپینگ باید ACES باشد تا حسِ فیلمی بدهد");
            StringAssert.Contains("#if BAZI_UNIVERSAL", rig, "بدون define، نبودِ پکیج URP کل assembly را نمی‌سازد");
        }

        [Test]
        public void AmbientOcclusionFeature_IsRenderFeatureAndRuntimeTunable()
        {
            string feature = Read("Assets/Scripts/Graphics/ScreenSpaceAmbientOcclusionFeature.cs");
            StringAssert.Contains("ScriptableRendererFeature", feature);
            StringAssert.Contains("ScriptableRenderPass", feature);
            StringAssert.Contains("BeforeRenderingPostProcessing", feature,
                "AO باید پیش از پس‌پرداز ترکیب شود، وگرنه در Bloom می‌سوزد");
            StringAssert.Contains("_BaziAOParams", feature, "شدتِ AO باید از globalِ نمایه بیاید، نه از فایل URP");
            StringAssert.Contains("ReleaseTemporaryRT", feature, "هر RT موقت باید آزاد شود (نشتیِ حافظه در موبایل کشنده است)");

            string shader = Read(ShaderFolder + "/BaziBaqa-ScreenSpaceAO.shader");
            StringAssert.Contains("DeclareDepthTexture.hlsl", shader, "AO بدون خواندنِ عمقِ صحنه معنا ندارد");
        }

        [Test]
        public void GraphicsDirector_InstallsWithoutTouchingGameplayFiles()
        {
            string director = Read("Assets/Scripts/Graphics/GraphicsDirector.cs");
            StringAssert.Contains("RuntimeInitializeOnLoadMethod", director,
                "لایه‌ی گرافیک باید خودش نصب شود تا GameBootstrap/صحنه دست‌نخورده بمانند");
            StringAssert.Contains("GraphicsDirector", director);

            // هیچ فایلِ Gameplay نباید برای «زیبایی» ویرایش شده باشد؛ فقط MaterialLibrary/WorldVFX/…
            string[] untouched = {
                "Assets/Scripts/Core/GameBootstrap.cs",
                "Assets/Scripts/Core/GameManager.cs",
                "Assets/Scripts/Core/GameClock.cs",
                "Assets/Scripts/Systems/WeatherSystem.cs",
                "Assets/Scripts/Systems/PerformanceManager.cs",
                "Assets/Scripts/AI/SurvivorAgent.cs",
                "Assets/Scripts/AI/EnemyAgent.cs"
            };
            foreach (string file in untouched)
            {
                string text = Read(file);
                Assert.IsFalse(text.Contains("MaterialLibrary"),
                    file + " نباید لایه‌ی گرافیک را صدا بزند (جداسازیِ فاز ۳)");
            }
        }

        private static int Count(string text, string token)
        {
            int total = 0;
            int index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                total++;
                index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }
            return total;
        }

        private static string ShaderMessageCount(Shader shader)
        {
            int count = ShaderUtil.GetShaderMessageCount(shader);
            if (count == 0) return "بدون پیام";
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Math.Min(count, 6); i++)
            {
                ShaderMessage message = ShaderUtil.GetShaderMessage(shader, i);
                if (message != null) builder.Append("[").Append(message.severity).Append("] ").Append(message.message).Append(" ");
            }
            return builder.Length == 0 ? "بدون پیام" : builder.ToString();
        }
    }
}
