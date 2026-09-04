using System;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>پیکربندی نسخه‌ی بازی؛ منبع واحدِ حقیقت برای انتشار.</summary>
    [Serializable]
    public class VersionConfig
    {
        public string versionName = "0.1.0";
        public int versionCode = 1;
        public string bundleId = "com.parsaapps.bazibaqa";
        public string company = "Parsa Apps";
        public string product = "سرزمین بقا";
        public int minSdkVersion = 26;
        public int targetSdkVersion = 34;
    }

    /// <summary>
    /// سامانه‌ی مدیریت نسخه در زمان اجرا. مقادیر از VersionConfig.json خوانده می‌شود تا
    /// «Version Name»، «Version Code» و «Bundle Identifier» همیشه منسجم باشند. ابزار Editor
    /// (VersionManager) همین پیکربندی را در PlayerSettings اعمال می‌کند.
    /// </summary>
    public static class GameVersion
    {
        private static VersionConfig _config = new VersionConfig();

        public static string VersionName { get { return _config.versionName; } }
        public static int VersionCode { get { return _config.versionCode; } }
        public static string BundleId { get { return _config.bundleId; } }
        public static string CompanyName { get { return _config.company; } }
        public static string ProductName { get { return _config.product; } }
        public static int MinSdkVersion { get { return _config.minSdkVersion; } }
        public static int TargetSdkVersion { get { return _config.targetSdkVersion; } }

        /// <summary>نام + کد، مثل «0.1.0 (1)».</summary>
        public static string Display { get { return VersionName + " (" + VersionCode + ")"; } }

        public static void LoadFromTextAsset(TextAsset asset)
        {
            if (asset == null) return;
            LoadFromJson(asset.text);
        }

        public static void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                VersionConfig parsed = JsonUtility.FromJson<VersionConfig>(json);
                if (parsed != null)
                {
                    if (string.IsNullOrEmpty(parsed.versionName)) parsed.versionName = "0.1.0";
                    if (string.IsNullOrEmpty(parsed.bundleId)) parsed.bundleId = "com.parsaapps.bazibaqa";
                    _config = parsed;
                }
            }
            catch (Exception exception)
            {
                GameLogger.Info("خواندن پیکربندی نسخه ناموفق بود: " + exception.Message);
            }
        }
    }
}
