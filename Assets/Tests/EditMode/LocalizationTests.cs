using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BaziBaqa;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// تست‌های سیستم بومی‌سازی. اصلِ کار: هیچ متنی در کد نیست؛ همه در
    /// Assets/Resources/Localization/LocalizationTable.json زندگی می‌کنند. پس این تست‌ها
    /// اول جدول را از Resources بار می‌کنند (راه‌اندازی خودکار هم هست، ولی تست نباید به
    /// ترتیب اجرا وابسته باشد) و سپس «قرارداد» را می‌سنجند: پوشش کامل، جهت متن، قالب‌ها،
    /// رفتار کلیدِ گمشده و زنده‌ماندنِ بازی پس از تغییر زبان.
    /// </summary>
    public class LocalizationTests
    {
        private const string TableResource = LocalizationManager.TableResourcePath;

        [OneTimeSetUp]
        public void LoadRealTable()
        {
            TextAsset asset = Resources.Load<TextAsset>(TableResource);
            Assert.IsNotNull(asset, "جدول بومی‌سازی در Resources/" + TableResource + " پیدا نشد.");
            LocalizationManager.LoadFromTextAsset(asset);
            LocalizationManager.SetLanguage("fa", false);
        }

        [OneTimeTearDown]
        public void RestoreDefaultLanguage()
        {
            LocalizationManager.SetLanguage("fa", false);
        }

        // ---------- ساختار جدول ----------

        [Test]
        public void TableDefinesBothLanguagesWithDefaultFarsi()
        {
            Assert.AreEqual("fa", LocalizationManager.DefaultLanguage);
            Assert.AreEqual("fa", LocalizationManager.Language);
            Assert.GreaterOrEqual(LocalizationManager.KeyCount("fa"), 200, "جدول فارسی باید همه‌ی متن‌های بازی را پوشش دهد.");
            Assert.GreaterOrEqual(LocalizationManager.KeyCount("en"), LocalizationManager.KeyCount("fa") - 1);
            Assert.GreaterOrEqual(LocalizationManager.KeyCount("en"), 200, "جدول انگلیسی باید هم‌اندازه‌ی فارسی پر شده باشد.");
        }

        [Test]
        public void EnglishTableHasNoHoles()
        {
            int farsi = LocalizationManager.KeyCount("fa");
            int english = LocalizationManager.KeyCount("en");
            Assert.AreEqual(farsi, english, "هر کلیدِ فارسی باید معادلِ انگلیسی داشته باشد.");
        }

        [Test]
        public void DirectionComesFromTheTable()
        {
            LocalizationManager.SetLanguage("fa", false);
            Assert.AreEqual("rtl", LocalizationManager.Direction);
            Assert.IsTrue(LocalizationManager.IsRtl);

            LocalizationManager.SetLanguage("en", false);
            Assert.AreEqual("ltr", LocalizationManager.Direction);
            Assert.IsFalse(LocalizationManager.IsRtl);

            LocalizationManager.SetLanguage("fa", false);
            Assert.IsTrue(LocalizationManager.IsRtl);
        }

        [Test]
        public void EveryFarsiKeyResolvesInEveryLanguage()
        {
            TextAsset asset = Resources.Load<TextAsset>(TableResource);
            LocalizationManager.LocalizationTable table = JsonUtility.FromJson<LocalizationManager.LocalizationTable>(asset.text);
            Assert.IsNotNull(table);
            Assert.IsNotNull(table.languages);
            Assert.GreaterOrEqual(table.languages.Count, 2);

            foreach (LocalizationManager.LocalizationLanguage language in table.languages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(language.language), "زبان بدون شناسه در جدول.");
                Assert.IsFalse(string.IsNullOrEmpty(language.direction), "زبان «" + language.language + "» جهت ندارد.");
                foreach (LocalizationManager.LocalizationEntry entry in language.entries)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(entry.key), "ورودی بدون کلید در جدول.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.value),
                        "متنِ خالی برای «" + entry.key + "» در زبان " + language.language);
                    Assert.IsFalse(entry.value.StartsWith("[loc:", StringComparison.Ordinal),
                        "متنِ حل‌نشده در جدول: " + entry.key);
                    Assert.IsFalse(entry.value.IndexOf("\\n", StringComparison.Ordinal) >= 0,
                        "متن «" + entry.key + "» خط‌شکنِ دوباره‌اسکیپ‌شده دارد (باید \n واقعی باشد).");
                }
            }
        }

        [Test]
        public void MissingKeyIsVisibleAndReported()
        {
            string value = Loc.Get("test.definitely-not-a-key");
            Assert.AreEqual("[loc:test.definitely-not-a-key]", value);
            Assert.IsTrue(LocalizationManager.IsUnresolved(value));
            CollectionAssert.Contains(LocalizationManager.MissingKeys, "test.definitely-not-a-key");
        }

        // ---------- خواندن و قالب ----------

        [Test]
        public void ResourceNamesComeFromTheTable()
        {
            Assert.AreEqual("چوب", Loc.Get("resource.wood"));
            Assert.AreEqual("برج دیده‌بانی", Loc.Get("building.watchtower"));

            LocalizationManager.SetLanguage("en", false);
            Assert.AreEqual("Wood", Loc.Get("resource.wood"), "در زبان انگلیسی همان کلید باید معادلِ انگلیسی را بدهد.");
            Assert.AreEqual("Watchtower", Loc.Get("building.watchtower"));
            LocalizationManager.SetLanguage("fa", false);
        }

        [Test]
        public void GameTextRoutesThroughLocalization()
        {
            Assert.AreEqual("چوب", GameText.ResourceName(ResourceType.Wood));
            Assert.AreEqual("اردوگاه", GameText.BuildingName(BuildingType.Camp));
            Assert.AreEqual("جمع‌آور", GameText.RoleName(SurvivorRole.Gatherer));
            Assert.AreEqual("توفانی", GameText.WeatherName(WeatherType.Storm));
            Assert.AreEqual("ابزار (کشاورزی/جمع‌آوری)", GameText.EquipmentName(EquipmentType.Tool));
            Assert.AreEqual("سازنده", GameText.AchievementName(AchievementId.Builder));
            Assert.AreEqual("همکاری گروهی", GameText.TechnologyName(TechnologyType.Cooperation));
        }

        [Test]
        public void FormattedTextFillsItsPlaceholders()
        {
            string line = Loc.Get("hud.day_line", "7", "14:30");
            StringAssert.Contains("7", line);
            StringAssert.Contains("14:30", line);
            StringAssert.DoesNotContain("{0}", line);

            string cost = GameText.CostLine(new List<ResourceCost> { new ResourceCost(ResourceType.Wood, 20) });
            StringAssert.Contains("چوب", cost);
            StringAssert.Contains("۲۰", cost, "اعدادِ فارسی در زبان فارسی انتظار می‌رود.");

            Assert.AreEqual(Loc.Get("label.free"), GameText.CostLine(new List<ResourceCost>()));
        }

        [Test]
        public void NumbersFollowTheActiveLanguage()
        {
            LocalizationManager.SetLanguage("fa", false);
            Assert.AreEqual("۱۲۳", Loc.Num(123));

            LocalizationManager.SetLanguage("en", false);
            Assert.AreEqual("123", Loc.Num(123));
            LocalizationManager.SetLanguage("fa", false);
        }

        [Test]
        public void SwitchingLanguageChangesVisibleText()
        {
            string farsi = Loc.Get("menu.new_game");
            LocalizationManager.SetLanguage("en", false);
            string english = Loc.Get("menu.new_game");
            LocalizationManager.SetLanguage("fa", false);

            Assert.IsFalse(string.IsNullOrEmpty(english));
            Assert.AreNotEqual(farsi, english, "با تغییر زبان باید متن عوض شود.");
            Assert.AreEqual(farsi, Loc.Get("menu.new_game"));
        }

        [Test]
        public void LanguageChangedEventFiresOncePerChange()
        {
            int calls = 0;
            string lastLanguage = null;
            Action<string> handler = language =>
            {
                calls++;
                lastLanguage = language;
            };
            LocalizationManager.LanguageChanged += handler;

            LocalizationManager.SetLanguage("en", false);
            LocalizationManager.SetLanguage("en", false);
            LocalizationManager.SetLanguage("fa", false);
            LocalizationManager.LanguageChanged -= handler;

            Assert.AreEqual(2, calls, "هر تغییرِ واقعی یک اطلاع می‌دهد؛ تکرارِ همان زبان نباید رویداد بسازد.");
            Assert.AreEqual("fa", lastLanguage);
        }

        [Test]
        public void UnknownLanguageFallsBackToDefault()
        {
            LocalizationManager.SetLanguage("de", false);
            Assert.AreEqual("fa", LocalizationManager.Language, "زبانِ ناشناختی نباید بازی را به متن‌های خالی بیندازد.");
        }

        // ---------- قراردادِ پوشش: هر داده‌ای متن دارد ----------

        [Test]
        public void EveryEnumValueHasANameInEveryLanguage()
        {
            AssertEnumHasKeys(typeof(ResourceType), "resource.");
            AssertEnumHasKeys(typeof(BuildingType), "building.");
            AssertEnumHasKeys(typeof(SurvivorRole), "role.");
            AssertEnumHasKeys(typeof(WeatherType), "weather.");
            AssertEnumHasKeys(typeof(TechnologyType), "technology.");
            AssertEnumHasKeys(typeof(AchievementId), "achievement.");
            AssertEnumHasKeys(typeof(EquipmentType), "equipment.");
            AssertEnumHasKeys(typeof(SurvivorState), "status.");
        }

        [Test]
        public void EveryTechnologyHasAnUnlockMessage()
        {
            foreach (object value in Enum.GetValues(typeof(TechnologyType)))
            {
                string key = "technology." + value.ToString().ToLowerInvariant() + ".unlocked";
                AssertLocalizable(key);
            }
        }

        [Test]
        public void EveryQuestHasTitleAndDescription()
        {
            for (int i = 0; i < QuestsDefinition.Count; i++)
            {
                QuestDefinition definition = QuestsDefinition.Data(i);
                Assert.IsNotNull(definition);
                AssertLocalizable(definition.TitleKey);
                AssertLocalizable(definition.DescriptionKey);
                Assert.IsNotEmpty(definition.Title, "عنوان مأموریت از جدول خوانده می‌شود.");
            }
        }

        [Test]
        public void EveryStoryEventAndChoiceIsLocalized()
        {
            foreach (StoryEvent story in StoryDefinitions.All)
            {
                AssertLocalizable(GameText.StoryKey(story.id, "title"));
                AssertLocalizable(GameText.StoryKey(story.id, "body"));
                Assert.IsNotEmpty(story.Title);
                Assert.IsNotEmpty(story.Body);
                for (int i = 0; i < story.choices.Count; i++)
                {
                    StoryChoice choice = story.choices[i];
                    AssertLocalizable("story." + choice.eventId + "." + choice.id + ".title");
                    Assert.IsNotEmpty(choice.Title);
                }
            }
        }

        [Test]
        public void SurvivorNamePoolIsComplete()
        {
            for (int i = 0; i < GameText.SurvivorNameCount; i++)
            {
                string name = GameText.SurvivorNameAt(i);
                AssertLocalizable("survivor.name." + i);
                Assert.IsNotEmpty(name);
            }
            // استخر نام باید بی‌تکرار باشد وایرادِ «دو سارا در گروه» نگیریم
            HashSet<string> unique = new HashSet<string>();
            for (int i = 0; i < GameText.SurvivorNameCount; i++)
            {
                Assert.IsTrue(unique.Add(GameText.SurvivorNameAt(i)), "نامِ تکراری در استخرِ بازمانده‌ها: " + i);
            }
        }

        [Test]
        public void StartingSurvivorsUseTheNamePool()
        {
            GameSaveData save = GameSaveData.CreateNew(4242);
            Assert.AreEqual(GameText.StartingSurvivorCount, save.survivors.Count);
            for (int i = 0; i < save.survivors.Count; i++)
            {
                Assert.AreEqual(GameText.SurvivorNameAt(i), save.survivors[i].displayName);
            }
        }

        // ---------- LocalizedText ----------

        [Test]
        public void LocalizedTextResolvesItsKey()
        {
            GameObject host = new GameObject("LocalizedTextProbe");
            try
            {
                UnityEngine.UI.Text label = host.AddComponent<UnityEngine.UI.Text>();
                label.font = GameFont.Persian;
                LocalizedText text = host.AddComponent<LocalizedText>();
                text.Configure("hud.day_line", 4, "12:00");

                Assert.AreEqual("hud.day_line", text.Key);
                // لایه‌ی قدیمیِ Text متن را برای راست‌به‌چپ شکل می‌دهد؛ پس مقادیر را با همان شکل می‌سنجیم.
                Assert.AreEqual(PersianText.LegacyDisplay(Loc.Get("hud.day_line", 4, "12:00")), label.text,
                    "LocalizedText باید متنِ قالب‌دار را روی کامپوننت بگذارد.");

                text.key = "game.title";
                text.Refresh();
                Assert.AreEqual(PersianText.LegacyDisplay(Loc.Get("game.title")), label.text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---------- ابزار ----------

        private static void AssertEnumHasKeys(Type enumType, string prefix)
        {
            foreach (string name in Enum.GetNames(enumType))
            {
                AssertLocalizable(prefix + name.ToLowerInvariant());
            }
        }

        private static void AssertLocalizable(string key)
        {
            string value = Loc.Get(key);
            Assert.IsFalse(LocalizationManager.IsUnresolved(value), "کلید «" + key + "» در جدول بومی‌سازی نیست.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), "متنِ خالی برای کلید «" + key + "».");
        }
    }
}
