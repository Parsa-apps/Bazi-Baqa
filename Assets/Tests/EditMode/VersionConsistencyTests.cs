using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using BaziBaqa;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// هماهنگیِ نسخه (فاز ۲٫۵ — بخش ۴). «منبع حقیقت» فایلِ Assets/Resources/VersionConfig.json است
    /// و VersionManager آن را روی PlayerSettings می‌نشاند. این تست‌ها همان قرارداد را در سطحِ
    /// فایل‌های مخزن می‌سنجند؛ پس حتی پیش از اجرای Unity هم ثابت می‌کنند بیلد و استور دو نسخه‌ی
    /// متفاوت نمی‌گویند. (تستِ زنده‌ی PlayerSettings در QualityGateEditModeTests است.)
    /// </summary>
    public class VersionConsistencyTests
    {
        private const string ConfigPath = "Assets/Resources/VersionConfig.json";
        private const string SettingsPath = "ProjectSettings/ProjectSettings.asset";

        [Test]
        public void VersionConfigIsSemanticAndMonotonic()
        {
            string json = File.ReadAllText(ConfigPath);
            GameVersion.LoadFromTextAsset(new TextAsset(json));

            Assert.IsMatch(GameVersion.VersionName, @"^\d+\.\d+\.\d+$",
                "Version Name باید سه‌بخشی باشد تا در Google Play قابل ردیابی بماند.");
            Assert.GreaterOrEqual(GameVersion.VersionCode, 1, "Build Number باید ۱ یا بیشتر باشد.");
            Assert.AreEqual("com.parsaapps.bazibaqa", GameVersion.BundleId, "شناسه‌ی بسته نباید تغییر کند (پوششِ آپدیت استور).");
            Assert.AreEqual("Parsa Apps", GameVersion.CompanyName);
        }

        [Test]
        public void PlayerSettingsYamlAlreadyCarriesTheSameVersion()
        {
            string settings = File.ReadAllText(SettingsPath);
            string json = File.ReadAllText(ConfigPath);
            GameVersion.LoadFromTextAsset(new TextAsset(json));

            Assert.AreEqual(GameVersion.VersionName, Yaml(settings, "bundleVersion"),
                "ProjectSettings.bundleVersion با VersionConfig.json نمی‌خواند؛ VersionManager.Apply اجرا نشده است.");
            Assert.AreEqual(GameVersion.VersionCode.ToString(), Yaml(settings, "AndroidBundleVersionCode"),
                "Build Number اندروید با VersionConfig.json نمی‌خواند.");
            Assert.AreEqual(GameVersion.MinSdkVersion.ToString(), Yaml(settings, "AndroidMinSdkVersion"));
            Assert.AreEqual(GameVersion.TargetSdkVersion.ToString(), Yaml(settings, "AndroidTargetSdkVersion"));
            Assert.AreEqual(GameVersion.ProductName, Yaml(settings, "productName"));
            Assert.AreEqual(GameVersion.CompanyName, Yaml(settings, "companyName"));
            StringAssert.Contains("Android: " + GameVersion.BundleId, settings,
                "شناسه‌ی بسته‌ی Android در ProjectSettings با پیکربندی یکی نیست.");
        }

        [Test]
        public void BuildSettingsAndSceneListArePreparedForRelease()
        {
            string buildSettings = File.ReadAllText("ProjectSettings/EditorBuildSettings.asset");
            StringAssert.Contains("Assets/Scenes/Main.unity", buildSettings, "صحنه‌ی Main باید در Build Settings باشد.");
            StringAssert.Contains("enabled: 1", buildSettings, "صحنه‌ی Main باید فعال باشد.");
        }

        [Test]
        public void InputHandlerIsSelectedExplicitly()
        {
            string settings = File.ReadAllText(SettingsPath);
            Match match = Regex.Match(settings, @"^\s*activeInputHandler:\s*(\d+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, "activeInputHandler در ProjectSettings تنظیم نشده است؛ یونیتی پیامِ هشدار می‌دهد.");
            Assert.AreEqual("0", match.Groups[1].Value,
                "پروژه از Input Manager کلاسیک استفاده می‌کند؛ مقدار باید 0 باشد تا در کنسول هشداری نماند.");
        }

        [Test]
        public void RuntimeDisplayAndSummaryUseTheLoadedConfig()
        {
            GameVersion.LoadFromTextAsset(new TextAsset(File.ReadAllText(ConfigPath)));
            Assert.AreEqual(GameVersion.VersionName + " (" + GameVersion.VersionCode + ")", GameVersion.Display);
            StringAssert.Contains(GameVersion.BundleId, GameVersion.Summary);
            StringAssert.Contains(GameVersion.MinSdkVersion.ToString(), GameVersion.Summary);
        }

        [Test]
        public void AboutPanelVersionLinesExistInTheLocalizationTable()
        {
            // نسخه در بازی از جدول خوانده می‌شود؛ اگر کلید نبود، پنجره‌ی «درباره» [loc:…] نشان می‌دهد.
            TextAsset table = Resources.Load<TextAsset>(LocalizationManager.TableResourcePath);
            Assert.IsNotNull(table, "جدول بومی‌سازی در Resources پیدا نشد.");
            LocalizationManager.LoadFromTextAsset(table);
            Assert.IsFalse(LocalizationManager.IsUnresolved(Loc.Get("ui.about.version", GameVersion.Display)));
            Assert.IsFalse(LocalizationManager.IsUnresolved(Loc.Get("ui.about.release_summary", GameVersion.BundleId,
                Loc.Num(GameVersion.MinSdkVersion), Loc.Num(GameVersion.TargetSdkVersion))));
            StringAssert.Contains(GameVersion.Display, Loc.Get("ui.about.version", GameVersion.Display));
        }

        [Test]
        public void EmptyConfigFallsBackToSafeDefaults()
        {
            GameVersion.LoadFromJson("{}");
            Assert.IsNotEmpty(GameVersion.VersionName, "پیکربندیِ خالی نباید بازی را بشکند؛ باید پیش‌فرض بماند.");

            GameVersion.LoadFromJson("{\"versionName\":\"\",\"versionCode\":0,\"bundleId\":\"\"}");
            Assert.AreEqual("0.1.0", GameVersion.VersionName);
            Assert.AreEqual("com.parsaapps.bazibaqa", GameVersion.BundleId);

            // مقدارهایِ تست را به وضعیتِ واقعی برگردانیم.
            GameVersion.LoadFromTextAsset(new TextAsset(File.ReadAllText(ConfigPath)));
        }

        private static string Yaml(string settings, string key)
        {
            Match match = Regex.Match(settings, @"^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, "کلید «" + key + "» در ProjectSettings.asset پیدا نشد.");
            string value = match.Groups[1].Value;
            return value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)
                ? value.Substring(1, value.Length - 2)
                : value;
        }
    }
}
