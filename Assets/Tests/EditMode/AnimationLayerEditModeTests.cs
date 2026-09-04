using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌های «انیمیشن حرفه‌ای» (فاز ۳، گام ۵): لایه‌ی انیمیشن فقط می‌خواند و فقط
    /// گره‌هایِ خودش را می‌نویسد. ریشه‌ی بازمانده/دشمن و مقیاسِ ریشه‌ی ساختمان در اختیارِ
    /// Gameplay است؛ اگر انیماتور آن‌ها را لمس کند، جهت‌گیری، افتادن و ارتقای ساختمان
    /// می‌شکند. همه‌ی بررسی‌ها روی متنِ کد (بدون کامنت) است ⇒ در batchmode هم معتبرند.
    /// </summary>
    public class AnimationLayerEditModeTests
    {
        private const string GraphicsDir = "Assets/Scripts/Graphics";
        private const string ScriptsDir = "Assets/Scripts";

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

        /// <summary>کامنت‌های خطی را حذف می‌کند؛ ادعا درباره‌ی کد باشد نه توضیح.</summary>
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

        private static int CountMatches(string text, string pattern)
        {
            return Regex.Matches(text, pattern).Count;
        }

        [Test]
        public void ActorMotion_NeverWritesTheActorRoot()
        {
            string code = CodeOnly(Read(GraphicsDir + "/ActorMotion.cs"));
            // جهت‌گیری و افتادنِ ریشه کارِ SurvivorAgent/EnemyAgent است
            Assert.IsFalse(Regex.IsMatch(code, @"(?<![A-Za-z_.])transform\.position\s*(\+|-|\*)?="),
                "نوشتن روی transform.positionِ ریشه با حرکتِ Agent در ست می‌افتد");
            Assert.IsFalse(Regex.IsMatch(code, @"(?<![A-Za-z_.])transform\.rotation\s*="),
                "نوشتن روی transform.rotationِ ریشه جهت‌گیری/افتادنِ بازی را خراب می‌کند");
            Assert.IsFalse(Regex.IsMatch(code, @"(?<![A-Za-z_.])transform\.localScale\s*="),
                "مقیاسِ ریشه‌ی شخصیت را انیماتور نباید عوض کند");
            Assert.IsFalse(Regex.IsMatch(code, @"(?<![A-Za-z_.])transform\.localPosition\s*="),
                "موقعیتِ محلیِ ریشه مالِ ساختِ جهان است");

            // و در عوض واقعاً چیزی را انیمیت می‌کند
            StringAssert.Contains("_torso.localScale", code);
            StringAssert.Contains("_torso.localRotation", code);
            Assert.IsTrue(Regex.IsMatch(code, @"_leftArm\s*=\s*MakeLimb"), "اندام‌ها باید ساخته شوند");
        }

        [Test]
        public void ActorMotion_OnlyReadsAgentState()
        {
            string code = CodeOnly(Read(GraphicsDir + "/ActorMotion.cs"));
            StringAssert.Contains("_survivor.State", code, "حالتِ کار از Agent خوانده می‌شود");
            StringAssert.Contains("_survivor.Health", code);
            StringAssert.Contains("_enemy.Health", code);

            // هیچ دستورِ بازی از این‌جا صادر نمی‌شود
            string[] banned =
            {
                "NotifyDamage", "Heal(", "BoostMorale", "SetTask", "Time.timeScale",
                "_survivor.Health =", "_enemy.Health =", "IsAlive ="
            };
            foreach (string token in banned)
            {
                Assert.IsFalse(code.Contains(token), "لایه‌ی انیمیشن نباید گیم‌پلی را صدا بزند: " + token);
            }
        }

        [Test]
        public void Limbs_UseSharedMeshAndAddNoColliders()
        {
            string code = CodeOnly(Read(GraphicsDir + "/ActorMotion.cs"));
            // CreatePrimitive یک Collider می‌آورد و انتخابِ لمسی/ریکست را عوض می‌کند
            Assert.IsFalse(code.Contains("CreatePrimitive"), "Primitive تازه نباید در صحنه ساخته شود");
            Assert.IsFalse(code.Contains("Collider"), "اندام‌ها نباید Collider داشته باشند");
            Assert.IsFalse(code.Contains("Raycast"), "لایه‌ی انیمیشن نباید فیزیک را درگیر کند");
            StringAssert.Contains("filter.sharedMesh = shared", code,
                "اندام‌ها از همان مشِ والد استفاده می‌کنند (بدون mesh تازه)");
            // AddComponent فقط برای دو مؤلفه‌ی نمایشی
            Assert.AreEqual(2, CountMatches(code, @"AddComponent<"),
                "فقط MeshFilter/MeshRenderer ساخته می‌شوند");
        }

        [Test]
        public void BuildingMotion_AnimatesChildrenAndUsesPropertyBlocks()
        {
            string code = CodeOnly(Read(GraphicsDir + "/BuildingMotion.cs"));
            // BuildingController.UpdateVisuals مقیاسِ ریشه را می‌نویسد ⇒ ما به ریشه دست نمی‌زنیم
            Assert.IsFalse(Regex.IsMatch(code, @"transform\.localScale\s*="), "ریشه مالِ منطقِ ساختمان است");
            Assert.IsFalse(Regex.IsMatch(code, @"(?<![A-Za-z_.])transform\.position\s*(\+|-|\*)?="),
                "جابه‌جاییِ ریشه‌ی ساختمان منطق بازی را می‌شکند");
            StringAssert.Contains("CacheParts()", code);
            StringAssert.Contains("_parts[i]", code);

            // آسیبِ دیده فقط روی instance نوشته می‌شود، نه روی متریالِ کش‌شده‌ی مشترک
            StringAssert.Contains("new MaterialPropertyBlock()", code);
            StringAssert.Contains("SetPropertyBlock(_block)", code);
            Assert.IsFalse(code.Contains("sharedMaterial.Set"), "متریالِ مشترک نباید آلوده شود");
            Assert.IsFalse(code.Contains(".color ="), "رنگِ متریالِ ساختمان کارِ این لایه نیست");
            StringAssert.Contains("_BaziDamage", code, "آسیب باید به شیدر برسد");
        }

        [Test]
        public void MotionDirector_IsBudgetedAndIdempotent()
        {
            string code = CodeOnly(Read(GraphicsDir + "/MotionDirector.cs"));
            StringAssert.Contains("MaxAnimatedActors = 48", code, "سقفِ انیمیشن شخصیت‌ها");
            StringAssert.Contains("MaxAnimatedBuildings = 96", code, "سقفِ انیمیشن ساختمان‌ها");
            StringAssert.Contains("GetComponent<ActorMotion>()", code, "نباید هر بار مؤلفه‌ی تازه اضافه شود");
            StringAssert.Contains("GetComponent<BuildingMotion>()", code);
            StringAssert.Contains("GetInstanceID()", code, "بازسازیِ جهان با نسلِ ریشه تشخیص داده می‌شود");
            Assert.AreEqual(2, CountMatches(code, @"AddComponent<"), "فقط دو مؤلفه‌ی انیمیشن");
            Assert.IsFalse(code.Contains("UnityEngine.Random"), "هیچ شانسِ سراسری در لایه‌ی انیمیشن");
            // در Update نباید هر فریم حافظه تازه گرفت
            string update = ExtractMethod(code, "private void Update()");
            Assert.IsFalse(update.Contains("new List"), "Update نباید لیست تازه بسازد");
        }

        [Test]
        public void MotionLayer_IsInstalledOnlyByGraphicsDirector()
        {
            string director = Read(GraphicsDir + "/GraphicsDirector.cs");
            StringAssert.Contains("Motion = GetOrAdd<MotionDirector>();", director);
            StringAssert.Contains("Motion.ApplyTier()", director);
            StringAssert.Contains("Motion.Refresh()", director);
            StringAssert.Contains("Motion.Report()", director);

            string root = ProjectPath(ScriptsDir);
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace("\\", "/");
                if (normalized.Contains("/Graphics/")) continue;
                string text = File.ReadAllText(normalized);
                Assert.IsFalse(text.Contains("MotionDirector") || text.Contains("ActorMotion")
                    || text.Contains("BuildingMotion"),
                    "هیچ فایلِ Gameplay نباید لایه‌ی انیمیشن را بشناسد: " + normalized);
            }
        }

        [Test]
        public void DamagedBuildings_HaveShaderSupport()
        {
            // BuildingMotion فقط وقتی دیده می‌شود که کلیدواژه‌ی آسيب روی متریالِ ساختمان روشن باشد
            string library = Read(GraphicsDir + "/MaterialLibrary.cs");
            int panel = library.IndexOf("case SurfaceStyle.Panel", StringComparison.Ordinal);
            Assert.Greater(panel, 0, "سبکِ Panel باید در MaterialLibrary باشد");
            string block = library.Substring(panel, Math.Min(1200, library.Length - panel));
            StringAssert.Contains("_BAZI_DAMAGE_ON", block,
                "تخته/فلز باید کلیدواژه‌ی آسيب را روشن کند تا _BaziDamage اثر کند");

            string shader = Read("Assets/Resources/Shaders/BaziBaqa-Surface.shader");
            StringAssert.Contains("_BAZI_DAMAGE_ON", shader);
            StringAssert.Contains("_BaziDamage(", shader, "شیدر باید propertyِ آسيب را مصرف کند");
        }

        /// <summary>بدنه‌ی یک متد (از سرِ امضا تا آکولادِ هم‌تراز) برای بررسیِ موضعی.</summary>
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
    }
}
