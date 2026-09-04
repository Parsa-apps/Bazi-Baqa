using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌هایِ «محیط زنده» (فاز ۳، گام ۳): باد از یک‌جا نوشته می‌شود، پراکندگیِ چمن
    /// هرگز به Randomِ سراسریِ بازی دست نمی‌زند، همه در یک mesh می‌مانند و رنگِ زیست‌بوم
    /// از Perlin می‌آید نه از شانس. همه‌ی بررسی‌ها رویِ متنِ فایل‌هاست ⇒ در batchmode هم پاس‌اند.
    /// </summary>
    public class EnvironmentVisualsEditModeTests
    {
        private const string GraphicsDir = "Assets/Scripts/Graphics";
        private const string ScriptsDir = "Assets/Scripts";

        private static string ProjectPath(string relative)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>کامنت‌ها را حذف می‌کند؛ ادعاها باید راجع بهِ کد باشند، نه توضیحِ بالایِ فایل.</summary>
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

        private static string Read(string relative)
        {
            string path = ProjectPath(relative);
            Assert.IsTrue(File.Exists(path), "فایل لازم نیست: " + relative);
            return File.ReadAllText(path);
        }

        [Test]
        public void WindField_IsTheOnlyWindWriter()
        {
            string wind = Read(GraphicsDir + "/WindField.cs");
            StringAssert.Contains("MaterialLibrary.SetWind(_speed, _strength * _tierScale, _phase, gustSharpness)", wind,
                "ضریبِ بادِ نمایه باید در همان فراخوانیِ سراسری بخورد"
                "WindField باید همان بردارِ قراردادِ شیدر را بنویسد");
            StringAssert.Contains("public void ApplyTier()", wind);
            StringAssert.Contains("public static Vector4 GlobalState", wind, "تست‌ها از همین‌جا می‌خوانند");

            // هیچ فایلِ دیگری (جز حل‌کننده و مقدارِ اولیه‌ی مدیر) نباید باد را بنویسد
            foreach (string file in Directory.GetFiles(ProjectPath(ScriptsDir), "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace("\\", "/");
                if (normalized.Contains("/Graphics/WindField.cs")) continue;
                if (normalized.Contains("/Graphics/MaterialLibrary.cs")) continue;
                string text = File.ReadAllText(normalized);
                foreach (string line in text.Split('\n'))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//")) continue;
                    int comment = line.IndexOf("//", StringComparison.Ordinal);
                    if (comment >= 0 && !line.Substring(0, comment).Contains("\"")) line = line.Substring(0, comment);
                    Assert.IsFalse(line.Contains("Shader.SetGlobalVector(\"_BaziWindState"),
                        "نوشتنِ مستقیمِ _BaziWindState خارج از MaterialLibrary ممنوع: " + normalized);
                    Assert.IsFalse(line.Contains("MaterialLibrary.SetWind("),
                        "باد فقط در WindField تنظیم می‌شود (مقدارِ اولیه‌ی GraphicsDirector مستثناست): " + normalized
                        + " :: " + trimmed);
                }
            }
        }

        [Test]
        public void WindField_UsesSmoothNoiseNotGlobalRandom()
        {
            string wind = Read(GraphicsDir + "/WindField.cs");
            Assert.IsFalse(CodeOnly(wind).Contains("UnityEngine.Random"), "بازی از Randomِ سراسری استفاده می‌کند؛ لایه‌ی گرافیک نباید آن را مصرف کند");
            Assert.IsFalse(Regex.IsMatch(wind, @"[^.\w]Random\.Range"), "Random.Range هم همان منبعِ مشترک را مصرف می‌کند");
            StringAssert.Contains("Mathf.PerlinNoise", wind, "بَرگشت‌ها باید پیوسته باشند، نه تصادفیِ پرشی");
            StringAssert.Contains("manager.Weather.Current", wind, "هوا فقط خوانده می‌شود");
            StringAssert.Contains("manager.Weather.RainIntensity", wind);
            StringAssert.Contains("Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(rig.NightAmount))", wind,
                "شب باید باد را آرام‌تر کند (حسِ سینمایی، نه هزینه‌ی اضافی)");
        }

        [Test]
        public void FoliageScatter_IsDeterministicSingleMeshAndPoolSafe()
        {
            string scatter = Read(GraphicsDir + "/FoliageScatter.cs");
            Assert.IsFalse(CodeOnly(scatter).Contains("UnityEngine.Random"), "بذرِ خودش را دارد تا چیدمانِ منابعِ بازی تکان نخورد");
            StringAssert.Contains("new Random(seed", scatter);
            StringAssert.Contains("_host.transform.SetParent(terrain, false)", scatter,
                "زیرِ TerrainRoot تا با World.Clear() خودش پاک شود (بدونِ نشتی)");
            StringAssert.Contains("ShadowCastingMode.Off", scatter, "سایه‌ی بوته‌ها روی موبایل توجیه ندارد");
            StringAssert.Contains("MaxTufts", scatter);
            StringAssert.Contains("hit.transform.name != WorldParts.Ground", scatter,
                "فقط رویِ مشِ زمین بوته می‌کاریم، نه روی آب یا ساختمان");
            StringAssert.Contains("UploadMeshData(true)", scatter, "مش بعد از ساخت ثابت است ⇒ RAM پس داده می‌شود");
            StringAssert.Contains("public void Refresh()", scatter, "بازسازیِ جهان/تغییرِ کیفیت همین را صدا می‌زند");

            string parts = Read("Assets/Scripts/World/WorldParts.cs");
            StringAssert.Contains("GrassMesh", parts, "نامِ گره‌ها/mesh‌ها باید در WorldParts ثبت باشد");
        }

        [Test]
        public void WorldGenerator_NaturalMaterials_UseSurfaceStylesAndBiomeColors()
        {
            string world = Read("Assets/Scripts/World/WorldGenerator.cs");
            StringAssert.Contains("MaterialLibrary.SurfaceStyle.Ground", world);
            StringAssert.Contains("MaterialLibrary.SurfaceStyle.Bark", world);
            StringAssert.Contains("MaterialLibrary.SurfaceStyle.Foliage", world);
            StringAssert.Contains("MaterialLibrary.SurfaceStyle.Rock", world);
            StringAssert.Contains("mesh.colors = biomeColors", world, "رنگِ زیست‌بوم باید رویِ مشِ زمین بنشیند");
            StringAssert.Contains("_waterMaterial = CreateMaterial", world, "آب به گام ۴ موکول است");

            // رنگِ زیست‌بوم نباید RNGِ مشترکِ بازی را مصرف کند
            int loop = world.IndexOf("for (int z = 0; z <= zSegments; z++)", StringComparison.Ordinal);
            Assert.Greater(loop, 0, "حلقه‌ی ساختِ زمین پیدا نشد");
            string body = world.Substring(loop, world.IndexOf("int triangle = 0;", StringComparison.Ordinal) - loop);
            StringAssert.Contains("biomeColors[index]", body);
            StringAssert.Contains("Mathf.PerlinNoise", body);
            // فقط همان یک تماسِ قدیمیِ ارتفاع مجاز است؛ رنگِ زیست‌بوم باید بدونِ RNG بسازد،
            // وگرنه چیدمانِ منابع/دشمن‌ها جابه‌جا می‌شود و فایل ذخیره بی‌معنی می‌گردد.
            Assert.AreEqual(1, CountOccurrences(CodeOnly(body), "_random.NextDouble"), "یک تماسِ قدیمی، نه بیشتر");
            foreach (string line in body.Split('\n'))
            {
                if (line.Contains("biome"))
                {
                    Assert.IsFalse(line.Contains("_random"), "رنگِ زیست‌بوم نباید RNGِ بازی را مصرف کند: " + line.Trim());
                }
            }
        }

        [Test]
        public void SurfaceShader_VertexColorAndAlphaClipAreWiredForNaturalStyles()
        {
            string shader = Read("Assets/Resources/Shaders/BaziBaqa-Surface.shader");
            StringAssert.Contains("_BAZI_VERTEX_COLOR_ON", shader);
            StringAssert.Contains("input.vertexColor", shader, "ورودیِ رنگِ رأس باید در URP هم باشد");
            StringAssert.Contains("_BAZI_ALPHA_CLIP_ON", shader);
            StringAssert.Contains("_Cutoff(", shader);

            string library = Read(GraphicsDir + "/MaterialLibrary.cs");
            StringAssert.Contains("material.EnableKeyword(\"_BAZI_VERTEX_COLOR_ON\")", library);
            StringAssert.Contains("material.EnableKeyword(\"_BAZI_ALPHA_CLIP_ON\")", library);
            StringAssert.Contains("style == SurfaceStyle.Foliage", library);
        }

        [Test]
        public void GraphicsProfile_V3_ScalesFoliageWithQuality()
        {
            string json = Read("Assets/Resources/Graphics/GraphicsProfile.json").Replace("\r", string.Empty);
            StringAssert.Contains("\"version\": " + GraphicsProfile.CurrentVersion, json, "فایل و کد هم‌نسخه بمانند");
            Assert.AreEqual(4, Regex.Matches(json, "\"foliageCount\"").Count, "برای هر چهار سطح");
            Assert.AreEqual(4, Regex.Matches(json, "\"windScale\"").Count);

            // «"id": …» لازم است؛ وگرنه defaultTier هم یک "medium" در بالایِ فایل است و
            // الگو، بلوکِ سطحِ قبلی را می‌خواند (این تست یک بار همین دام را زد).
            Match counts = Regex.Match(json, "\"id\": \"low\"[\\s\\S]*?\"foliageCount\":\\s*(\\d+)");
            Match medium = Regex.Match(json, "\"id\": \"medium\"[\\s\\S]*?\"foliageCount\":\\s*(\\d+)");
            Match ultra = Regex.Match(json, "\"id\": \"ultra\"[\\s\\S]*?\"foliageCount\":\\s*(\\d+)");
            Assert.IsTrue(counts.Success && medium.Success && ultra.Success, "مقادیرِ foliageCount خوانده نشد");
            int low = int.Parse(counts.Groups[1].Value);
            int mid = int.Parse(medium.Groups[1].Value);
            int top = int.Parse(ultra.Groups[1].Value);
            Assert.GreaterOrEqual(mid, low, "کیفیتِ بالاتر نباید بوته‌ی کمتری داشته باشد");
            Assert.GreaterOrEqual(top, mid);
            Assert.LessOrEqual(low, 400, "رویِ اندروید میان‌رده/ضعیف باید بودجه‌ی بوته کوچک بماند");

            string profile = Read(GraphicsDir + "/GraphicsProfile.cs");
            StringAssert.Contains("foliageCount = Mathf.Clamp(foliageCount, 0, 4096)", profile);
            StringAssert.Contains("windScale = Mathf.Clamp(windScale, 0f, 2f)", profile);
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }
            return count;
        }
    }
}
