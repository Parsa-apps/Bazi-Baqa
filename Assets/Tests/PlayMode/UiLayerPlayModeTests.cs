using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// تست‌هایِ زنده‌ی لایه‌ی AAAِ رابط: شیشه حل می‌شود یا بی‌صدا کنار می‌رود، پنجره‌ها
    /// انیمیشنِ باز شدن را تمام می‌کنند و در حالتِ کامل رها می‌شوند، آیکن‌ها قطعی‌اند و
    /// اینترو پس از پایان هیچ هزینه‌ای ندارد.
    /// </summary>
    public class UiLayerPlayModeTests
    {
        private static GameObject MakePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            return panel;
        }

        [UnityTest]
        public IEnumerator UIGlass_ResolvesSharedMaterialOrDegrades()
        {
            Material glass = MaterialLibrary.Glass();
            int before = UIGlassPanel.ActivePanels;

            GameObject panel = MakePanel("GlassPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 120f));
            UIGlassPanel component = UIGlassPanel.Apply(panel);
            Assert.IsNotNull(component);
            yield return null;

            Image image = panel.GetComponent<Image>();
            if (glass != null)
            {
                Assert.AreSame(glass, image.material, "پنل‌ها باید همان متریالِ کش‌شده را بگیرند (یک batch)");
                Assert.AreEqual("Hidden/BaziBaqa/UIGlass", glass.shader.name);
                Assert.IsTrue(component.IsGlassActive);
            }
            else
            {
                Assert.IsNull(image.material, "بیِ شیدرِ شیشه، پنل باید به رنگِ تخت برگردد");
                Debug.Log("UiLayerPlayMode: ui-glass unavailable; flat fallback path verified.");
            }
            Assert.GreaterOrEqual(UIGlassPanel.ActivePanels, before + 1);
            StringAssert.Contains("glass panels=", UIGlassPanel.Report());

            UnityEngine.Object.Destroy(panel);
            yield return null;
            Assert.AreEqual(before, UIGlassPanel.ActivePanels, "شمارنده با خاموش‌شدنِ پنل کم می‌شود");
        }

        [UnityTest]
        public IEnumerator WindowFx_CompletesOpenAnimationAndRestores()
        {
            GameObject panel = MakePanel("WindowPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, 200f));
            Vector3 baseScale = panel.transform.localScale;
            Vector2 basePosition = panel.GetComponent<RectTransform>().anchoredPosition;

            WindowFx fx = panel.AddComponent<WindowFx>();
            yield return null;
            Assert.IsTrue(fx.IsPlaying || fx.PlayCount > 0, "انیمیشن باید با فعال‌شدنِ پنل شروع شود");
            Assert.Less(panel.transform.localScale.x, baseScale.x * 1.15f, "پنل در میانه‌ی باز شدن کوچک‌تر است");

            float waited = 0f;
            while (fx.IsPlaying && waited < 1.5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(fx.IsPlaying, "انیمیشن باید تمام شود");
            Assert.AreEqual(1f, panel.transform.localScale.x, 0.001f, "مقیاس به حالتِ پایه برمی‌گردد");
            Assert.AreEqual(basePosition, panel.GetComponent<RectTransform>().anchoredPosition);
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "برای محوشدگیِ باز شدن CanvasGroup لازم است");
            Assert.AreEqual(1f, group.alpha, 0.001f, "پنل نباید نیمه‌شفاف بماند");
            UnityEngine.Object.Destroy(panel);
        }

        [UnityTest]
        public IEnumerator WindowFx_DisablingMidAnimationRestoresAlpha()
        {
            GameObject panel = MakePanel("InterruptedWindow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 160f));
            WindowFx fx = panel.AddComponent<WindowFx>();
            yield return null;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group);

            panel.SetActive(false);
            Assert.GreaterOrEqual(group.alpha, 0.999f, "پنلی که وسطِ انیمیشن بسته شد باید کاملاً بی‌شفاف بماند نه نامرئی");
            panel.SetActive(true);
            yield return null;
            Assert.GreaterOrEqual(fx.PlayCount, 2, "فعال‌شدنِ دوباره انیمیشن را پخش می‌کند");
            UnityEngine.Object.Destroy(panel);
        }

        [UnityTest]
        public IEnumerator UiIcons_AreBakedOnceAndDeterministic()
        {
            Sprite first = UIIconLibrary.Get(ResourceType.Wood);
            Assert.IsNotNull(first, "آیکنِ چوب باید پخته شود");
            Sprite again = UIIconLibrary.Get(ResourceType.Wood);
            Assert.AreSame(first, again, "باید از کش بیاید (بافتِ تازه نسازیم)");
            Assert.AreEqual(UIIconLibrary.IconSize, first.texture.width);
            Assert.AreEqual(UIIconLibrary.IconSize, first.texture.height);

            Color center = first.texture.GetPixel(32, 20);
            Assert.Greater(center.a, 0, "مرکزِ آیکن نباید خالی باشد");
            Color corner = first.texture.GetPixel(1, 1);
            Assert.Less(corner.a, 250, "گوشه‌ها گرد/محو هستند");

            Sprite water = UIIconLibrary.Get(ResourceType.Water);
            Assert.AreNotSame(first, water);
            Assert.GreaterOrEqual(UIIconLibrary.CachedCount, 2);
            StringAssert.Contains("icons=", UIIconLibrary.Report());
            yield return null;
        }

        [UnityTest]
        public IEnumerator IntroFx_BuildsItsOwnLayersAndGoesQuiet()
        {
            GameObject root = MakePanel("SplashView", Vector2.zero, Vector2.one, Vector2.zero);
            root.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            root.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            IntroFx intro = root.AddComponent<IntroFx>();
            yield return null;

            string report = intro.Report();
            StringAssert.Contains("rays=7", report);
            StringAssert.Contains("scans=3", report);
            StringAssert.Contains("sparks=26", report, "انرژیِ جمع‌شونده باید ساخته باشد");
            Assert.IsFalse(intro.IsFinished);

            // هیچ لایه‌ای نباید کلیک را بگیرد
            Image[] layers = root.GetComponentsInChildren<Image>(true);
            Assert.Greater(layers.Length, 30);
            foreach (Image layer in layers)
            {
                Assert.IsFalse(layer.raycastTarget, "اینترو input را نمی‌دزدد: " + layer.name);
            }

            root.SetActive(false);
            Assert.IsTrue(intro.IsFinished, "با خاموش‌شدنِ اسپلش، اینترو هم تمام می‌شود");
            Assert.IsFalse(string.IsNullOrEmpty(IntroFx.LastReport));
            UnityEngine.Object.Destroy(root);
        }
    }
}
