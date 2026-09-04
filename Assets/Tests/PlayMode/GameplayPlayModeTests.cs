using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// تست‌های PlayMode روی سناریوهای اصلی بازی. هر تست، بازی را با GameBootstrap واقعی
    /// (همان مسیر صحنه‌ی اصلی) اجرا و در پایان کاملاً پاک‌سازی می‌کند تا تست‌ها مستقل بمانند.
    /// </summary>
    public class GameplayPlayModeTests
    {
        private GameObject _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("تست-بقا");
            _root.AddComponent<GameBootstrap>();
            yield return null; // اجازه بده Start (presentation) اجرا شود.
            Assert.IsNotNull(GameManager.Instance, "GameManager باید بعد از راه‌اندازی ساخته شود.");
            GameManager.Instance.StartNewGame();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Construction?.CancelPlacement();
                if (GameManager.Instance.CameraController != null)
                    Object.Destroy(GameManager.Instance.CameraController.gameObject);
                GameManager.Instance.ReturnToMenu();
            }
            if (_root != null) Object.Destroy(_root);
            if (EventSystem.current != null) Object.Destroy(EventSystem.current.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameStart_BootsToPlayingPhase()
        {
            Assert.AreEqual(GamePhase.Playing, GameManager.Instance.Phase);
            Assert.IsTrue(GameManager.Instance.IsPlaying);
            Assert.Greater(GameManager.Instance.AliveSurvivorCount(), 0, "سفر باید با دست‌کم یک بازمانده آغاز شود.");
            Assert.IsNotNull(GameManager.Instance.World);
            Assert.Greater(GameManager.Instance.Resources.Current.wood, 0, "منابع اولیه باید پر شده باشند.");
            yield break;
        }

        [UnityTest]
        public IEnumerator CollectResources_IncreasesStorage()
        {
            int before = GameManager.Instance.Resources.Get(ResourceType.Wood);
            GameManager.Instance.Resources.Add(ResourceType.Wood, 25, "تست");
            Assert.AreEqual(before + 25, GameManager.Instance.Resources.Get(ResourceType.Wood));
            yield break;
        }

        [UnityTest]
        public IEnumerator SpendResources_WhenInsufficient_KeepsBalance()
        {
            int before = GameManager.Instance.Resources.Get(ResourceType.Gold);
            bool spent = GameManager.Instance.Resources.TrySpend(ResourceType.Gold, before + 100, "تست");
            Assert.IsFalse(spent, "هزینه‌ی بیشتر از موجودی باید رد شود.");
            Assert.AreEqual(before, GameManager.Instance.Resources.Get(ResourceType.Gold));
            yield break;
        }

        [UnityTest]
        public IEnumerator BuildBuilding_SelectsAndCancelsPlacement()
        {
            ConstructionSystem construction = GameManager.Instance.Construction;
            Assert.IsNotNull(construction, "سامانه‌ی ساخت باید فعال باشد.");
            Assert.IsNotNull(construction.FindByType(BuildingType.Camp), "اردوگاه پیش‌فرض باید ساخته شده باشد.");

            construction.SelectForPlacement(BuildingType.House);
            Assert.IsTrue(construction.IsPlacing, "انتخابِ محل ساخت باید حالتِ ساخت را فعال کند.");
            Assert.AreEqual(BuildingType.House, construction.PlacingType);

            construction.CancelPlacement();
            Assert.IsFalse(construction.IsPlacing, "لغوِ ساخت باید حالتِ ساخت را غیرفعال کند.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SaveGame_WritesFileAndHasSave()
        {
            GameManager.Instance.SaveGame();
            Assert.IsTrue(GameManager.Instance.Save.HasSave(), "بعد از ذخیره باید فایل ذخیره موجود باشد.");
            yield break;
        }

        [UnityTest]
        public IEnumerator LoadGame_RestoresSavedState()
        {
            // یک مقدار مشخص را ذخیره و بعد تغییر و سپس بارگذاری می‌کنیم.
            GameManager.Instance.Resources.Current.Set(ResourceType.Wood, 77);
            GameManager.Instance.SaveGame();

            GameManager.Instance.Resources.Current.Set(ResourceType.Wood, 5);
            Assert.AreEqual(5, GameManager.Instance.Resources.Current.wood);

            GameSaveData loaded = GameManager.Instance.Save.Load();
            Assert.IsNotNull(loaded, "ذخیره باید قابل بارگذاری باشد.");
            Assert.AreEqual(77, loaded.resources.wood, "بارگذاری باید مقدار ذخیره‌شده را بازگرداند.");

            GameManager.Instance.StartFromSave(loaded, true);
            Assert.AreEqual(77, GameManager.Instance.Resources.Current.wood, "پس از اعمال ذخیره، منابع باید همان مقدار باشند.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Save_RoundTripKeepsVersionAndData()
        {
            GameSaveData save = GameSaveData.CreateNew(1234);
            save.questIndex = 5;
            GameManager.Instance.Save.Save(save);

            GameSaveData loaded = GameManager.Instance.Save.Load();
            Assert.IsNotNull(loaded, "ذخیره‌ی 작성‌شده باید بازخوانی شود.");
            Assert.AreEqual(SaveSystem.CurrentSaveVersion, loaded.saveVersion, "نسخه پس از چرخه باید ثابت بماند.");
            Assert.AreEqual(5, loaded.questIndex);
            Assert.IsNotNull(loaded.equipment, "ساختارهای سیستم باید سالم بمانند.");
            Assert.IsNotNull(loaded.story);
            Assert.IsNotNull(loaded.raid);
            // هیچ‌کدام از مجموعه‌ها نباید null باقی بماند (بازگردانی مهاجرت).
            Assert.IsNotNull(loaded.survivors);
            Assert.IsNotNull(loaded.buildings);
            Assert.IsNotNull(loaded.achievements);
            yield break;
        }

        [UnityTest]
        public IEnumerator QuestSystem_IssuesDistinctActiveQuests()
        {
            QuestSystem quests = GameManager.Instance.Quests;
            Assert.IsNotNull(quests);
            Assert.AreEqual(3, quests.Quests.Count, "سه مأموریت فعال باید صادر شوند.");

            var indices = new HashSet<int>();
            for (int i = 0; i < quests.Quests.Count; i++) indices.Add(quests.Quests[i].DefinitionIndex);
            Assert.AreEqual(3, indices.Count, "مأموریت‌های فعال باید تعاریف متمایز داشته باشند (بدون تکرار).");

            // با نشانگرِ صدورِ مشخص، بازسازی نباید تعریف تکراری در پنجره‌ی فعال بدهد.
            GameSaveData save = GameSaveData.CreateNew(9);
            save.questIndex = 11;
            quests.Initialize(save);
            var after = new HashSet<int>();
            for (int i = 0; i < quests.Quests.Count; i++) after.Add(quests.Quests[i].DefinitionIndex);
            Assert.AreEqual(3, after.Count, "چرخش مأموریت نباید تعریف تکراری تولید کند.");
            yield break;
        }

        [UnityTest]
        public IEnumerator RaidSystem_ResolvesChanceAndExecutesWithoutError()
        {
            RaidSystem raid = GameManager.Instance.Raid;
            Assert.IsNotNull(raid);
            float chance = raid.SuccessChance();
            Assert.GreaterOrEqual(chance, 0.2f);
            Assert.LessOrEqual(chance, 0.9f);

            // ExecuteRaid در هر حالت (شب/کمبود نگهبان/کافی نبودن منابع) بدون خطا برمی‌گردد.
            Assert.DoesNotThrow(() => raid.ExecuteRaid());
            yield break;
        }
    }
}
