using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌های «رابطِ AAA» (فاز ۳، گام ۶): شیشه‌ایِ UI قراردادِ UGUI را می‌شکند یا نه،
    /// جاروبِ نور یک متریالِ مشترک است یا ۴۰ متریال، انیماتورِ پنجره با بازخوردِ دکمه
    /// نمی‌جنگد، آیکن‌ها قطعی‌اند و اینترو input را نمی‌دزدد.
    /// </summary>
    public class UiLayerEditModeTests
    {
        private const string UiDir = "Assets/Scripts/UI";
        private const string ShaderDir = "Assets/Resources/Shaders";
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

        [Test]
        public void UiGlassShader_HonoursUguiContract()
        {
            string shader = Read(ShaderDir + "/BaziBaqa-UIGlass.shader");
            StringAssert.Contains("UNITY_UI_CLIP_RECT", shader, "RectMask2D/اسکرول بدونِ این clip نمی‌شوند");
            StringAssert.Contains("UNITY_UI_ALPHACLIP", shader);
            StringAssert.Contains("Blend SrcAlpha OneMinusSrcAlpha", shader, "پنلِ شیشه‌ای باید نیمه‌شفاف باشد");
            StringAssert.Contains("ZTest [unity_GUIZTestMode]", shader, "ZTest باید از Canvas بیاید وگرنه روی world دیده می‌شود");
            StringAssert.Contains("ZWrite Off", shader);
            StringAssert.Contains("Fallback \"UI/Default\"", shader, "بیِ URP باید همان تصویرِ ساده بماند");
            StringAssert.Contains("UniversalPipeline", shader);
            // _ClipRect از Canvas با property block می‌آید ⇒ نباید داخلِ cbufferِ متریال برود
            Assert.IsFalse(Regex.IsMatch(shader, @"CBUFFER_START\(UnityPerMaterial\)[^}]*_ClipRect", RegexOptions.Singleline),
                "_ClipRect داخلِ UnityPerMaterial یعنی ماسک‌ها از کار می‌افتند");
            StringAssert.Contains("Shader \"Hidden/BaziBaqa/UIGlass\"", shader, "شیدرِ UI نباید در منوی Shader ظاهر شود");
        }

        [Test]
        public void UiGlassPanel_UsesOneSharedMaterialForAllPanels()
        {
            string code = CodeOnly(Read(UiDir + "/UIGlassPanel.cs"));
            StringAssert.Contains("MaterialLibrary.Glass()", code, "متریال از حل‌کننده‌ی مرکزی می‌آید");
            Assert.IsFalse(code.Contains("new Material("), "هر پنل نباید متریالِ تازه بسازد (batch می‌شکند)");
            StringAssert.Contains("_image.material = _material", code);
            StringAssert.Contains("if (_driver == null) _driver = this;", code, "فقط یک پنل جاروب را می‌راند");
            StringAssert.Contains("_activeCount = Mathf.Max(0, _activeCount - 1)", code, "شمارنده نباید منفی شود");
            // حالتِ بدونِ شیدر: پنل باید به رنگِ تخت برگردد، نه ناپدید شدن
            StringAssert.Contains("_image.material = null", code);
        }

        [Test]
        public void WindowFx_AndButtonFx_DontFightOverScale()
        {
            string manager = Read(UiDir + "/UIManager.cs");
            StringAssert.Contains("if (name != \"Button\")", manager,
                "دکمه‌ها localScale را به ButtonFx می‌دهند؛ WindowFx روی آن‌ها ننشیند");
            StringAssert.Contains("UIGlassPanel.Apply(panel);", manager);
            Assert.AreEqual(1, CountOccurrences(manager, "AddComponent<ButtonFx>"),
                "بازخوردِ لمسی یک‌جا و یک‌بار وصل می‌شود");

            string button = CodeOnly(Read(UiDir + "/ButtonFx.cs"));
            StringAssert.Contains("PlayRipple()", button, "ریپلِ نور روی لمس");
            StringAssert.Contains("image.raycastTarget = false", button, "ریپل نباید کلیک را بگیرد");
            StringAssert.Contains("#if UNITY_ANDROID || UNITY_IOS", button, "لرزشِ دستگاه فقط روی موبایل");
            StringAssert.Contains("public static bool HapticsEnabled", button, "قابل‌خاموش‌شدن از تنظیمات/QA");
        }

        [Test]
        public void WindowFx_UsesUnscaledTimeAndAlwaysRestores()
        {
            string code = CodeOnly(Read(UiDir + "/WindowFx.cs"));
            StringAssert.Contains("Time.unscaledDeltaTime", code, "منو با pause هم باید باز شود");
            StringAssert.Contains("_rect.localScale = _baseScale", code);
            StringAssert.Contains("_group.alpha = 1f", code, "هیچ پنلی نباید نیمه‌شفاف بماند");
            Assert.IsTrue(Regex.IsMatch(code, @"private void OnDisable\(\)[\s\S]*?Restore\(\);"),
                "خاموش‌شدنِ وسطِ انیمیشن باید وضعیت را برگرداند");
            StringAssert.Contains("bool canSlide = _rect.anchorMin == _rect.anchorMax", code,
                "روی rect کشیده anchoredPosition ننویس (offsetMin/Max را بهم می‌ریزد)");
        }

        [Test]
        public void UiIconLibrary_IsDeterministicAndCached()
        {
            string code = CodeOnly(Read(UiDir + "/UIIconLibrary.cs"));
            Assert.IsFalse(code.Contains("Random"), "آیکن‌ها نباید به شانسِ سراسری دست بزنند");
            Assert.AreEqual(1, CountOccurrences(code, "new Texture2D"), "بافت فقط داخلِ Bake ساخته می‌شود");
            StringAssert.Contains("_sprites.TryGetValue(key, out cached)", code, "یک بار پخت، بعد کش");
            StringAssert.Contains("HideFlags.HideAndDontSave", code, "بافت/اسپرایت با Reload دامنه گم نشود");
            StringAssert.Contains("image.raycastTarget = false", code, "آیکن انتخابِ لمسی را نمی‌دزدد");
            StringAssert.Contains("const int IconSize = 64", code, "اندازه‌ی ثابت ⇒ حافظه‌ی قابل‌پیش‌بینی");
            StringAssert.Contains("rect.anchorMin = new Vector2(1f, 0.5f)", code, "آیکن در ابتدایِ راست می‌نشیند (RTL)");
        }

        [Test]
        public void IntroFx_TouchesNobodyElsesObjects()
        {
            string code = CodeOnly(Read(UiDir + "/IntroFx.cs"));
            // مالکیتِ جدا: GlowPulse/CrownPulse/SplashEffects ترنسفورم‌هایِ خودشان را دارند
            foreach (string token in new[] { "GetComponent<GlowPulse>", "GetComponent<CrownPulse>", "GetComponent<SplashEffects>", "SendMessage" })
            {
                Assert.IsFalse(code.Contains(token), "اینترو نباید به مؤلفه‌هایِ دیگر وصل شود: " + token);
            }
            StringAssert.Contains("image.raycastTarget = false", code, "اینترو هیچ وقتِ کلیک را نمی‌گیرد");
            StringAssert.Contains("SetVisible(false);", code, "پس از پایان، هزینه‌ی فریم صفر می‌شود");
            StringAssert.Contains("if (_root == null || _finished) return;", code);
            Assert.IsFalse(code.Contains("Destroy("), "فرزندان با خودِ SplashView پاک می‌شوند (Destroy دستی لازم نیست)");
        }

        [Test]
        public void UiLayerHooks_AreAdditiveForGameplay()
        {
            string root = ProjectPath(ScriptsDir);
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace("\\", "/");
                if (normalized.Contains("/UI/")) continue;
                string text = File.ReadAllText(normalized);
                foreach (string token in new[] { "UIGlassPanel", "WindowFx", "IntroFx", "UIIconLibrary", "ButtonFx" })
                {
                    Assert.IsFalse(text.Contains(token),
                        "لایه‌ی UI نباید توسط منطقِ بازی صدا زده شود: " + token + " در " + normalized);
                }
            }
            // و هوک‌ها فقط سه نقطه‌یِ مشخصِ UIManager‌اند
            string manager = Read(UiDir + "/UIManager.cs");
            Assert.AreEqual(1, CountOccurrences(manager, "AddComponent<IntroFx>"), "اینترو یک‌جا وصل می‌شود");
            Assert.AreEqual(1, CountOccurrences(manager, "UIIconLibrary.AttachBadge"), "آیکن یک‌جا وصل می‌شود");
            Assert.AreEqual(1, CountOccurrences(manager, "UIGlassPanel.Apply(panel)"), "شیشه از CreatePanel می‌آید");
        }
    }
}
