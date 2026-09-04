using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// آزمون‌های «اعتبارسنجی زمان اجرا» روی مسیر واقعی بازی: بارگذاری صحنه، ساخت بوم و تعامل با
    /// دکمه‌ها، چرخه‌ی ذخیره/بارگذاری از راه UI و سلامت فونت/چینش متن فارسی.
    /// این‌ها همان بررسی‌هایی هستند که دستی در Unity Console انجام می‌شود، اما خودکار و قابل تکرار.
    /// </summary>
    public class RuntimeValidationPlayModeTests
    {
        private Scene _loadedScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_loadedScene);
            }
            _loadedScene = default(Scene);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Construction?.CancelPlacement();
                GameManager.Instance.ReturnToMenu();
                Object.Destroy(GameManager.Instance.gameObject);
            }
            if (EventSystem.current != null) Object.Destroy(EventSystem.current.gameObject);
            GameEvents.ClearSubscribers();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneLoading_MainSceneBuildsBootstrapWithoutMissingReferences()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
            yield return operation;

            Scene scene = SceneManager.GetSceneByName("Main");
            _loadedScene = scene;
            Assert.IsTrue(scene.IsValid() && scene.isLoaded, "صحنه‌ی Main باید بارگذاری شود.");

            GameObject[] roots = scene.GetRootGameObjects();
            Assert.Greater(roots.Length, 0, "صحنه‌ی Main نباید خالی باشد.");

            GameBootstrap bootstrap = null;
            for (int i = 0; i < roots.Length; i++)
            {
                bootstrap = roots[i].GetComponent<GameBootstrap>();
                if (bootstrap != null) break;
            }
            Assert.IsNotNull(bootstrap, "روی یکی از گره‌های ریشه باید GameBootstrap نشسته باشد.");

            // هیچ کامپوننتی نباید «Missing Script» باشد (مقدار null در آرایه‌ی Component ها).
            Component[] components = bootstrap.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
                Assert.IsNotNull(components[i], "کامپوننتِ ازدست‌رفته روی GameBootstrap: " + bootstrap.gameObject.name);

            yield return null; // یک فریم برای Awake/Start
            Assert.IsNotNull(GameManager.Instance, "GameManager باید پس از بیدار شدن بوت‌استرپ ساخته شده باشد.");
        }

        [UnityTest]
        public IEnumerator UserInterface_BuildPanelAndBackButton_StayResponsive()
        {
            BootstrapGame();
            UIManager ui = GameManager.Instance.UI;
            Assert.IsNotNull(ui);

            // باز کردن پنل‌ها و بستن با «دکمه‌ی بازگشت» (رفتار واقعیِ اندروید).
            ui.ShowMap();
            Assert.IsTrue(ui.HandleBack(), "بازگشت باید پنل نقشه را ببندد.");
            ui.ShowQuestsPanel();
            Assert.IsTrue(ui.HandleBack(), "بازگشت باید پنل مأموریت‌ها را ببندد.");
            ui.ShowAchievementsPanel();
            Assert.IsTrue(ui.HandleBack(), "بازگشت باید پنل دستاوردها را ببندد.");
            ui.ShowBuildingDetails(GameManager.Instance.Construction.FindByType(BuildingType.Camp));
            Assert.IsTrue(ui.HandleBack(), "بازگشت باید پنجره‌ی جزئیات را ببندد.");
            Assert.IsFalse(ui.HandleBack(), "وقتی پنلی باز نیست بازگشت نباید چیزی ببندد.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UserInterface_EveryButton_IsClickableThroughRaycaster()
        {
            BootstrapGame();
            yield return null;

            Button[] buttons = Object.FindObjectsOfType<Button>(true);
            Assert.Greater(buttons.Length, 6, "نوار عملیات باید دست‌کم هفت دکمه داشته باشد.");
            GraphicRaycaster raycaster = Object.FindObjectOfType<GraphicRaycaster>();
            Assert.IsNotNull(raycaster, "بوم بدون GraphicRaycaster هیچ لمسی را نمی‌گیرد.");

            int clickable = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                Assert.IsNotNull(button.targetGraphic, "دکمه بدون targetGraphic بازخورد لمسی ندارد: " + button.name);
                Assert.IsTrue(button.GetComponent<CanvasGroup>() == null || button.GetComponent<CanvasGroup>().alpha > 0f,
                    "دکمه پشت CanvasGroup شفاف گیرا نیست.");
                if (button.interactable && button.targetGraphic != null && button.targetGraphic.raycastTarget) clickable++;
            }
            Assert.Greater(clickable, 6, "دکمه‌های قابل‌کلیک بیش از حد کم هستند: " + clickable);
        }

        [UnityTest]
        public IEnumerator UserInterface_PersianTextIsRenderedWithGameFont()
        {
            BootstrapGame();
            yield return null;

            int checkedLabels = 0;
            Graphic[] graphics = Object.FindObjectsOfType<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                TextMeshProUGUI tmp = graphics[i] as TextMeshProUGUI;
                if (tmp != null)
                {
                    Assert.IsNotNull(tmp.font, "متن TMP بدون فونت‌اسست رندر می‌شود.");
                    if (!string.IsNullOrEmpty(tmp.text))
                    {
                        Assert.IsFalse(LooksLikeRawLocalizationKey(tmp.text), "کلیدِ خام در UI نمایش داده می‌شود: " + tmp.text);
                        checkedLabels++;
                    }
                    continue;
                }
                Text legacy = graphics[i] as Text;
                if (legacy == null) continue;
                Assert.IsNotNull(legacy.font, "متن بدون فونت رندر می‌شود.");
                checkedLabels++;
            }
            Assert.Greater(checkedLabels, 10, "انتظار می‌رفت دست‌کم ده برچسب متنی ساخته شده باشد.");
        }

        [UnityTest]
        public IEnumerator UserInterface_RtlPersianLayout_UsesRightAlignment()
        {
            BootstrapGame();
            yield return null;

            TextMeshProUGUI[] labels = Object.FindObjectsOfType<TextMeshProUGUI>(true);
            int rightAligned = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                TextAlignmentOptions alignment = labels[i].alignment;
                bool rightOrCenter = (alignment & TextAlignmentOptions.Right) == TextAlignmentOptions.Right
                    || (alignment & TextAlignmentOptions.MidlineRight) == TextAlignmentOptions.MidlineRight
                    || (alignment & TextAlignmentOptions.TopRight) == TextAlignmentOptions.TopRight
                    || (alignment & TextAlignmentOptions.BottomRight) == TextAlignmentOptions.BottomRight
                    || (alignment & TextAlignmentOptions.Center) == TextAlignmentOptions.Center;
                if (rightOrCenter) rightAligned++;
            }
            if (labels.Length > 0)
                Assert.GreaterOrEqual(rightAligned, labels.Length / 2, "چینش راست‌به‌چپ باید روی بیشترِ برچسب‌ها اعمال شده باشد.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SaveLoad_ThroughUserInterface_RestoresProgress()
        {
            BootstrapGame();
            GameManager game = GameManager.Instance;
            int dayBefore = game.Clock.Day;
            game.Resources.Current.Set(ResourceType.Gold, 314);
            game.SaveGame();

            game.Resources.Current.Set(ResourceType.Gold, 1);
            Assert.AreEqual(1, game.Resources.Current.gold);

            GameSaveData loaded = game.Save.Load();
            Assert.IsNotNull(loaded, "فایل ذخیره باید بعد از SaveGame خوانده شود.");
            Assert.AreEqual(314, loaded.resources.gold, "طلا باید در فایل ذخیره مانده باشد.");

            game.StartFromSave(loaded, true);
            Assert.AreEqual(314, game.Resources.Current.gold, "اعمال ذخیره باید مقدار را برگرداند.");
            Assert.AreEqual(dayBefore, game.Clock.Day);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Notification_ReachesHudLabel()
        {
            BootstrapGame();
            yield return null;
            string expected = "آزمون اعلان";
            GameEvents.Notify(expected);
            yield return null;

            bool found = false;
            TextMeshProUGUI[] labels = Object.FindObjectsOfType<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].text != null && labels[i].text.Contains(expected)) found = true;
            }
            Text[] legacyLabels = Object.FindObjectsOfType<Text>(true);
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                if (legacyLabels[i].text != null && legacyLabels[i].text.Contains(expected)) found = true;
            }
            Assert.IsTrue(found, "اعلان باید برچسب نوتیفیکیشن HUD را پر کند.");
        }

        private static void BootstrapGame()
        {
            if (GameManager.Instance == null)
            {
                GameObject host = new GameObject("[test-runtime-ui]");
                GameManager game = host.AddComponent<GameManager>();
                UIManager ui = host.AddComponent<UIManager>();
                game.Initialize(ui);
                ui.Initialize(game);
            }
            GameManager.Instance.StartNewGame();
        }

        private static bool LooksLikeRawLocalizationKey(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('.') < 0) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!allowed) return false;
            }
            return true;
        }
    }
}
