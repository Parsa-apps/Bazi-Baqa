using NUnit.Framework;

namespace BaziBaqa.Tests
{
    public class GameplayLogicTests
    {
        [Test]
        public void XpRequiredForLevelIsIncreasing()
        {
            Assert.Greater(ProgressionMath.XpRequiredForLevel(2), ProgressionMath.XpRequiredForLevel(1));
            Assert.AreEqual(18, ProgressionMath.XpRequiredForLevel(1));
            Assert.AreEqual(26, ProgressionMath.XpRequiredForLevel(2));
        }

        [Test]
        public void AdvanceRaisesLevelAndKeepsRemainingXp()
        {
            int level = 1;
            int xp = ProgressionMath.Advance(50, ref level, ProgressionSystem.MaxLevel);
            Assert.AreEqual(3, level);
            Assert.AreEqual(6, xp);
        }

        [Test]
        public void AdvanceWithoutEnoughXpKeepsLevel()
        {
            int level = 1;
            int xp = ProgressionMath.Advance(5, ref level, ProgressionSystem.MaxLevel);
            Assert.AreEqual(1, level);
            Assert.AreEqual(5, xp);
        }

        [Test]
        public void AdvanceIsCappedAtMaxLevel()
        {
            int level = ProgressionSystem.MaxLevel;
            int xp = ProgressionMath.Advance(500, ref level, ProgressionSystem.MaxLevel);
            Assert.AreEqual(ProgressionSystem.MaxLevel, level);
            Assert.Less(xp, ProgressionMath.XpRequiredForLevel(level));
        }

        [Test]
        public void HouseBuildCostsAreValid()
        {
            var costs = ConstructionSystem.GetBuildCosts(BuildingType.House);
            Assert.IsTrue(costs.Count > 0);
            Assert.AreEqual(ResourceType.Wood, costs[0].type);
            Assert.AreEqual(35, costs[0].amount);
        }

        [Test]
        public void UpgradeCostsScaleWithLevel()
        {
            var levelOne = ConstructionSystem.GetUpgradeCosts(BuildingType.Camp, 1);
            var levelThree = ConstructionSystem.GetUpgradeCosts(BuildingType.Camp, 3);
            Assert.AreEqual(22, levelOne[0].amount);
            Assert.AreEqual(66, levelThree[0].amount);
        }

        [Test]
        public void PersianDigitsAreConverted()
        {
            Assert.AreEqual("۱۲۳", GameClock.ToPersianDigits("123"));
            Assert.AreEqual("۰۵", GameClock.ToPersianDigits("05"));
        }

        [Test]
        public void PersianTextKeepsTheLengthOfSourceText()
        {
            string source = "همکاری گروهی";
            string processed = PersianText.Process(source);
            Assert.AreEqual(source.Length, processed.Length);
            Assert.IsNotEmpty(processed);
        }

        [Test]
        public void PersianTextConvertsLatinDigitsToFarsi()
        {
            // با فونت Vazirmatn اعداد فارسی به شکل ۰-۹ نمایش داده می‌شوند.
            Assert.AreEqual("۱۲", PersianText.Process("12"));
            Assert.AreEqual("۱۲۳", PersianText.Process("123"));
            Assert.AreEqual("۰۵", PersianText.Process("05"));
        }

        [Test]
        public void GameVersionParsesConfig()
        {
            GameVersion.LoadFromJson("{\"versionName\":\"0.2.0\",\"versionCode\":3,\"bundleId\":\"com.parsaapps.bazibaqa\"}");
            Assert.AreEqual("0.2.0", GameVersion.VersionName);
            Assert.AreEqual(3, GameVersion.VersionCode);
            Assert.AreEqual("com.parsaapps.bazibaqa", GameVersion.BundleId);
        }

        [Test]
        public void ResourceNamesArePersian()
        {
            Assert.AreEqual("چوب", GameText.ResourceName(ResourceType.Wood));
            Assert.AreEqual("انرژی", GameText.ResourceName(ResourceType.Energy));
            Assert.AreEqual("برج دیده‌بانی", GameText.BuildingName(BuildingType.WatchTower));
        }

        [Test]
        public void QuestDefinitionsAreAvailable()
        {
            Assert.Greater(QuestsDefinition.Count, 0);
            QuestDefinition definition = QuestsDefinition.Data(0);
            Assert.IsNotEmpty(definition.id);
            Assert.Greater(definition.rewardAmount, 0);
            Assert.Greater(definition.rewardXp, 0);
        }

        [Test]
        public void NewSaveInitializesMetaSystems()
        {
            GameSaveData save = GameSaveData.CreateNew(99);
            Assert.IsNotNull(save.achievements);
            Assert.IsNotNull(save.dailyReward);
            // سه مأموریتِ ابتدایی صادر شده‌اند؛ بنابراین نشانگر صدور از ۳ شروع می‌شود.
            Assert.AreEqual(3, save.questIndex);
            Assert.AreEqual(3, save.saveVersion);
        }
        [Test]
        public void NewSaveInitializesExpandedSystems()
        {
            GameSaveData save = GameSaveData.CreateNew(7);
            Assert.IsNotNull(save.equipment);
            Assert.IsNotNull(save.story);
            Assert.IsNotNull(save.raid);
            Assert.AreEqual(1, save.equipment.tool);
            Assert.AreEqual(0, save.story.lastDecisionDay);
            Assert.AreEqual(0, save.raid.lastRaidDay);
        }

        [Test]
        public void EquipmentCostsAreValid()
        {
            var costs = EquipmentSystem.GetUpgradeCosts(EquipmentType.Weapon, 2);
            Assert.AreEqual(ResourceType.Wood, costs[0].type);
            Assert.AreEqual(40, costs[0].amount);
            Assert.Greater(costs.Count, 0);
        }


        [Test]
        public void QuestRotationNeverDuplicatesDefinitions()
        {
            // شبیه‌سازی چرخش مأموریت‌ها: همان منطق Initialize/ClaimAll را بدون صحنه بازتولید می‌کنیم.
            const int count = QuestsDefinition.Count;
            const int active = 3;
            var seen = new System.Collections.Generic.HashSet<int>();
            int issued = 3;
            var window = new System.Collections.Generic.List<int>();
            for (int i = 0; i < active; i++) window.Add(PositiveMod(issued - active + i, count));

            // تا ۲۰ بار ادعا، هر بار باید تعریفِ متمایز جایگزین شود.
            for (int step = 0; step < 20; step++)
            {
                int claimIndex = step % window.Count;
                int claimed = window[claimIndex];
                Assert.False(seen.Contains(claimed), "def re-issued: " + claimed);
                seen.Add(claimed);
                window.RemoveAt(claimIndex);
                int newId = PositiveMod(issued, count);
                issued++;
                window.Add(newId);
                // پنجره همیشه شامل تعاریف متمایز است.
                Assert.AreEqual(window.Count, new System.Collections.Generic.HashSet<int>(window).Count, "duplicate within window");
            }
            Assert.AreEqual(count, QuestsDefinition.Count);
        }

        private static int PositiveMod(int value, int modulus)
        {
            if (modulus <= 0) return 0;
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

}
}
