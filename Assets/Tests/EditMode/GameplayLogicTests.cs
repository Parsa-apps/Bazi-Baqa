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
            Assert.AreEqual(0, save.questIndex);
            Assert.AreEqual(2, save.saveVersion);
        }
    }
}
