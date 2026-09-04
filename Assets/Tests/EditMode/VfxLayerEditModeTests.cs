using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌هایِ گام ۴ (آب/آتش/انفجار): قراردادِ پارامترهایِ شیدرها، استخرِ افکت‌ها و
    /// این که لایه‌ی VFX هیچ وابستگیِ بازگشتی به Gameplay نساخته است.
    /// </summary>
    public class VfxLayerEditModeTests
    {
        private const string ShaderFolder = "Assets/Resources/Shaders";
        private const string GraphicsDir = "Assets/Scripts/Graphics";

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

        [Test]
        public void WaterShader_HasWaveContractAndSafeFallback()
        {
            string water = Read(ShaderFolder + "/BaziBaqa-Water.shader");
            StringAssert.Contains("Shader \"BaziBaqa/Water\"", water);
            StringAssert.Contains("\"RenderPipeline\" = \"UniversalPipeline\"", water);
            StringAssert.Contains("Fallback \"BaziBaqa/Surface\"", water,
                "زیرِ Built-in باید به شیدرِ دومیِ پروژه برگردد، نه ارغوانی");
            StringAssert.Contains("CBUFFER_START(UnityPerMaterial)", water, "SRP Batcher");
            StringAssert.Contains("BaziBaqaCore.hlsl", water, "مه ارتفاعی از همِ helper مشترک می‌آید");
            StringAssert.Contains("BaziApplyHeightFog", water);
            StringAssert.Contains("MixFog", water, "مه‌ی کلاسیکِ صحنه هم اعمال می‌شود");
            StringAssert.Contains("_BaziAtmosphere", water, "خیسیِ باران باید موج/کف را عوض کند");
            StringAssert.Contains("_WorldSpaceCameraPos", water, "فزنل بدونِ وابستگی به helper هایِ تازه‌ی URP حساب می‌شود");
            Assert.IsFalse(water.Contains("GetWorldSpaceViewDir"), "امضایِ این متد بینِ نسخه‌هایِ URP فرق می‌کند؛ دستی حسابش می‌کنیم");

            foreach (string property in new[] { "_BaziWaterDeep", "_BaziWaterShallow", "_BaziWaterSky", "_BaziWaterWave", "_BaziWaterScroll", "_BaziWaterSmoothness" })
            {
                StringAssert.Contains(property + "(", water, "MaterialLibrary.Water این را می‌نویسد: " + property);
                StringAssert.Contains(property, water.Substring(water.IndexOf("CBUFFER_START", StringComparison.Ordinal)),
                    "باید داخلِ CBUFFER باشد تا SRP Batcher نشکند: " + property);
            }
            // بافتِ جزئیات بیرونِ CBUFFER است (بافت‌ها یونیفورم نیستند) ولی property‌اش باید باشد
            StringAssert.Contains("_BaziWaterDetail(\"Detail Normal\"", water);
            StringAssert.Contains("TEXTURE2D(_BaziWaterDetail)", water);
        }

        [Test]
        public void FireShader_IsSelfContainedAndWindAware()
        {
            string fire = Read(ShaderFolder + "/BaziBaqa-Fire.shader");
            StringAssert.Contains("Shader \"Hidden/BaziBaqa/Fire\"", fire);
            StringAssert.Contains("Blend SrcAlpha One", fire, "افکتِ نورانی باید additive بکشد");
            StringAssert.Contains("ZWrite Off", fire);
            StringAssert.Contains("Cull Off", fire, "ذره‌هایِ Billboard از هر دو رو دیده می‌شوند");
            StringAssert.Contains("Fog { Mode Off }", fire, "مه روی آتش/دودِ رها نشیند (دود خودش مه را شبیه‌سازی می‌کند)");
            StringAssert.Contains("float4  _BaziWindState;", fire, "این فایل core را include نمی‌کند ⇒ declaration لازم دارد");
            StringAssert.Contains("UnityObjectToClipPos", fire, "با CGPROGRAM/UnityCG در هر دو خطِ رندر کامپایل می‌شود");
            Assert.IsFalse(fire.Contains("HLSLPROGRAM"), "شیدرِ ذرات عمداً از UnityCG نوشته شده تا به URP گره نخورد");
            Assert.IsFalse(Regex.IsMatch(fire, "CBUFFER_START"), "یک متریالِ یکتا برای همه‌ی ذرات؛ CBUFFER لازم نیست");
            foreach (string property in new[] { "_BaziFireHot", "_BaziFireCold", "_BaziFireSmoke", "_BaziFireParams", "_BaziFireMode", "_BaziFireWind" })
            {
                StringAssert.Contains(property, fire);
            }
        }

        [Test]
        public void VfxDirector_PoolsEverythingAndNeverLeaksMaterials()
        {
            string vfx = Read(GraphicsDir + "/VfxDirector.cs");
            StringAssert.Contains("ObjectPool<Burst>", vfx);
            StringAssert.Contains("PoolSize", vfx);
            StringAssert.Contains("_active.Count < BudgetForCurrentTier()", vfx, "سقفِ افکتِ هم‌زمان از بودجه‌ی ذراتِ همان سطح");
            StringAssert.Contains("!burst.InUse", vfx, "release دوباره استخر را فاسد می‌کند");
            StringAssert.Contains("ParticleSystemStopBehavior.StopEmittingAndClear", vfx);
            StringAssert.Contains("emission.rateOverTime = 0f", vfx, "ذره‌ها فقط با Emit یک‌باره پخش می‌شوند (بدونِ انتشارِ پیوسته در انفجار)");
            StringAssert.Contains("renderer.material = mode == 1", vfx, "متریال از MaterialLibrary می‌آید، نه از شیدرِ پکیج");
            StringAssert.Contains("ShadowCastingMode.Off", vfx);
            Assert.IsFalse(vfx.Contains("GameObject.Instantiate"), "هیچ Instantiate در لایه‌ی VFX مجاز نیست؛ استخر به‌جای آن است");
            Assert.IsFalse(vfx.Contains("Instantiate("), "ساختنِ Prefab در زمان اجرا یعنی GC روی موبایل");
            // ساختنِ GameObject فقط برای «پرکردنِ استخر» مجاز است، نه داخلِ Play/Update
            foreach (string method in new[] { "PlayExplosion", "PlayImpact", "PlaySplash", "Update" })
            {
                int at = vfx.IndexOf("public void " + method, StringComparison.Ordinal);
                if (at < 0) at = vfx.IndexOf("private void " + method, StringComparison.Ordinal);
                Assert.Greater(at, 0, "متد " + method + " پیدا نشد");
                int end = vfx.IndexOf("\n        }", at, StringComparison.Ordinal);
                Assert.Greater(end, at, "بدنه‌ی " + method + " بسته نشده است");
                string body = vfx.Substring(at, end - at);
                Assert.IsFalse(body.Contains("new GameObject"), method + " نباید GameObject تازه بسازد (استخر هست)");
                Assert.IsFalse(body.Contains("AddComponent"), method + " نباید کامپوننت تازه اضافه کند");
            }
            Assert.IsFalse(Regex.IsMatch(vfx, "renderer\\.material\\.Set\\w+\\(\""), "تغییرِ متریالِ کش‌شده همه‌ی افکت‌ها را عوض می‌کند");

            string library = Read(GraphicsDir + "/MaterialLibrary.cs");
            StringAssert.Contains("public static Material Water(", library);
            StringAssert.Contains("public static Material Fire(", library);
            StringAssert.Contains("if (!IsUniversal)", library, "آبِ بدونِ URP باید به Surface برگردد");
            StringAssert.Contains("private const string WaterShaderName = \"BaziBaqa/Water\";", library);
            StringAssert.Contains("private const string FireShaderName = \"Hidden/BaziBaqa/Fire\";", library);
        }

        [Test]
        public void GameplayFiles_DoNotDependOnTheVfxLayer()
        {
            // فاز ۳ قرضِ معماری‌اش را پس می‌دهد: لایه‌ی بصری gameplay را صدا می‌زند، نه برعکس.
            string[] gameplay =
            {
                "Assets/Scripts/World/WorldGenerator.cs",
                "Assets/Scripts/Systems/BuildingController.cs",
                "Assets/Scripts/Systems/WeatherSystem.cs",
                "Assets/Scripts/Systems/EnemyDirector.cs",
                "Assets/Scripts/Systems/ConstructionSystem.cs",
                "Assets/Scripts/World/WorldVFX.cs"
            };
            foreach (string file in gameplay)
            {
                if (!File.Exists(ProjectPath(file))) continue;
                string text = File.ReadAllText(ProjectPath(file));
                Assert.IsFalse(text.Contains("VfxDirector"), file + " نباید لایه‌ی VFX را بشناسد");
            }

            string director = Read(GraphicsDir + "/GraphicsDirector.cs");
            StringAssert.Contains("GetOrAdd<VfxDirector>()", director, "VfxDirector باید خودکار نصب شود");
        }
    }
}
