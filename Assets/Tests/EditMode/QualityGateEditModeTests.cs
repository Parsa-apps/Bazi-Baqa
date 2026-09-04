using NUnit.Framework;
using UnityEngine;
using System.IO;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// نگهبان‌های کیفیت: پیش از هر بیلد باید این بررسی‌ها پاس شوند. برخلاف تست‌های منطق بازی،
    /// این‌ها «زیرساخت» را می‌سنجند — چرخه‌ی ذخیره/بارگذاری روی دیسک، مهاجرت نسخه‌ی ذخیره،
    /// تعادل رویدادها و هماهنگی پیکربندی نسخه با PlayerSettings.
    /// </summary>
    public class QualityGateEditModeTests
    {
        [Test]
        public void SaveSystem_CorruptedMainFile_RecoversFromBackup()
        {
            SaveSystem save = new SaveSystem();
            try
            {
                GameSaveData original = GameSaveData.CreateNew(777);
                original.resources.Set(ResourceType.Stone, 321);
                Assert.IsTrue(save.Save(original), "ذخیره‌ی نخست باید موفق باشد.");

                GameSaveData second = GameSaveData.CreateNew(777);
                second.resources.Set(ResourceType.Stone, 654);
                Assert.IsTrue(save.Save(second), "ذخیره‌ی دوم باید موفق باشد و نسخه‌ی پشتیبان بسازد.");

                File.WriteAllText(save.SavePath, "{ this is not json ");
                GameSaveData loaded = save.Load();
                Assert.IsNotNull(loaded, "با خراب‌شدن فایل اصلی، نسخه‌ی پشتیبان باید بارگذاری شود.");
                Assert.AreEqual(321, loaded.resources.stone, "محتوای پشتیبان باید همان مقدار قبلی باشد.");
            }
            finally
            {
                save.Delete();
            }
        }

        [Test]
        public void SaveSystem_MigratesOldSaveVersions()
        {
            SaveSystem save = new SaveSystem();
            try
            {
                GameSaveData legacy = GameSaveData.CreateNew(12);
                legacy.saveVersion = 1;
                legacy.equipment = null;
                legacy.raid = null;
                legacy.story = null;
                legacy.settings = null;
                string json = JsonUtility.ToJson(legacy);
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(save.SavePath, json);

                GameSaveData loaded = save.Load();
                Assert.IsNotNull(loaded);
                Assert.AreEqual(SaveSystem.CurrentSaveVersion, loaded.saveVersion, "مهاجرت باید نسخه را به‌روز کند.");
                Assert.IsNotNull(loaded.equipment);
                Assert.IsNotNull(loaded.raid);
                Assert.IsNotNull(loaded.story);
                Assert.IsNotNull(loaded.settings);
            }
            finally
            {
                save.Delete();
            }
        }

        [Test]
        public void VersionConfig_MatchesPlayerSettings()
        {
            TextAsset asset = Resources.Load<TextAsset>("VersionConfig");
            Assert.IsNotNull(asset, "VersionConfig.json باید در Resources باشد (منبع واحد حقیقت نسخه).");
            GameVersion.LoadFromJson(asset.text);

            Assert.AreEqual(GameVersion.VersionName, UnityEditor.PlayerSettings.bundleVersion,
                "VersionManager.Apply() باید bundleVersion را با VersionConfig.json هم‌خوان کند.");
            Assert.AreEqual(GameVersion.VersionCode, UnityEditor.PlayerSettings.Android.bundleVersionCode,
                "کد نسخه‌ی اندروید باید با پیکربندی یکی باشد.");
            Assert.AreEqual(GameVersion.BundleId, UnityEditor.PlayerSettings.GetApplicationIdentifier(UnityEditor.BuildTargetGroup.Android));
            Assert.AreEqual(GameVersion.CompanyName, UnityEditor.PlayerSettings.companyName);
            Assert.GreaterOrEqual(GameVersion.MinSdkVersion, 26, "minSdk برای الزام گوگل‌پلی باید ۲۶ یا بالاتر باشد.");
            Assert.GreaterOrEqual(GameVersion.TargetSdkVersion, 34, "targetSdk باید ۳۴ یا بالاتر باشد.");
        }

        [Test]
        public void Resources_LoadEveryRuntimeAsset()
        {
            // هر منبعی که در زمان اجرا با Resources.Load خوانده می‌شود باید وجود داشته باشد.
            Assert.IsNotNull(Resources.Load<TextAsset>("Localization/LocalizationTable"), "جدول بومی‌سازی پیدا نشد.");
            Assert.IsNotNull(Resources.Load<TextAsset>("VersionConfig"), "پیکربندی نسخه پیدا نشد.");
            Assert.IsNotNull(Resources.Load<Font>("Fonts/Vazirmatn"), "فونت Vazirmatn پیدا نشد.");
        }

        [Test]
        public void GameEvents_KeepsSubscriberBalance()
        {
            // یک بار ثبت و یک بار حذف؛ اگر رویدادها تعادل خود را از دست بدهند، اعلان‌ها چند برابر می‌شوند.
            int calls = 0;
            System.Action<string> handler = message => calls++;
            GameEvents.Notification += handler;
            GameEvents.Notification -= handler;
            GameEvents.Notify("آزمون تعادل رویداد");
            Assert.AreEqual(0, calls, "پس از حذف، هندلر نباید صدا زده شود.");

            GameEvents.Notification += handler;
            GameEvents.Notify("آزمون تعادل رویداد");
            GameEvents.Notification -= handler;
            Assert.AreEqual(1, calls, "رویداد باید دقیقاً یک بار صادر شود.");
        }
    }
}
