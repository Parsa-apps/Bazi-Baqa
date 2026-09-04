using NUnit.Framework;

namespace BaziBaqa.Tests
{
    public class LocalizationTests
    {
        [Test]
        public void ResourceNamesComeFromLocalization()
        {
            Assert.AreEqual("چوب", Loc.Get("resource.wood"));
            Assert.AreEqual("برج دیده‌بانی", Loc.Get("building.watchtower"));
        }

        [Test]
        public void GameTextRoutesThroughLocalization()
        {
            Assert.AreEqual("چوب", GameText.ResourceName(ResourceType.Wood));
            Assert.AreEqual("اردوگاه", GameText.BuildingName(BuildingType.Camp));
            Assert.AreEqual("جمع‌آور", GameText.RoleName(SurvivorRole.Gatherer));
            Assert.AreEqual("توفانی", GameText.WeatherName(WeatherType.Storm));
        }

        [Test]
        public void MissingKeyFallsBackToKey()
        {
            // تا وقتی جدول بارگذاری نشده، کلیدِ ناپیدا باید خودِ کلید را برگرداند تا قابل تشخیص باشد.
            Assert.AreEqual("some.missing.key", Loc.Get("some.missing.key"));
        }

        [Test]
        public void FormattingWorksWithPlaceholders()
        {
            string result = Loc.Get("game.footer");
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void DefaultLanguageIsFarsi()
        {
            Assert.AreEqual("fa", LocalizationManager.DefaultLanguage);
            Assert.AreEqual("fa", LocalizationManager.Language);
        }
    }
}
