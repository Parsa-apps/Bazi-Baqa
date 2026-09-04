using System.IO;
using UnityEditor;
using UnityEngine;
using BaziBaqa;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// مدیریت نسخه برای انتشار. نسخه‌ی «منبع حقیقت» در Assets/Resources/VersionConfig.json است.
    /// این ابزار آن را می‌خواند، روی PlayerSettings اعمال می‌کند و امکان افزایش نسخه را می‌دهد تا
    /// Version Name / Version Code / Bundle Identifier همیشه هماهنگ بمانند.
    /// </summary>
    public static class VersionManager
    {
        private const string ConfigPath = "Assets/Resources/VersionConfig.json";

        [MenuItem("BaziBaqa/Version/Apply Version To Player Settings")]
        public static void ApplyMenuItem()
        {
            Apply();
        }

        [MenuItem("BaziBaqa/Version/Bump Patch")]
        public static void BumpPatch()
        {
            Bump(2);
        }

        [MenuItem("BaziBaqa/Version/Bump Minor")]
        public static void BumpMinor()
        {
            Bump(1);
        }

        [MenuItem("BaziBaqa/Version/Bump Major")]
        public static void BumpMajor()
        {
            Bump(0);
        }

        [MenuItem("BaziBaqa/Version/Open Version Config")]
        public static void OpenConfig()
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath));
        }

        private static VersionConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new VersionConfig();
                string json = File.ReadAllText(ConfigPath);
                VersionConfig config = JsonUtility.FromJson<VersionConfig>(json);
                return config ?? new VersionConfig();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[BaziBaqa Version] خواندن پیکربندی نسخه ناموفق بود: " + exception.Message);
                return new VersionConfig();
            }
        }

        private static void Save(VersionConfig config)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, JsonUtility.ToJson(config, true));
            AssetDatabase.ImportAsset(ConfigPath);
        }

        private static void Apply()
        {
            VersionConfig config = Load();
            PlayerSettings.companyName = config.company;
            PlayerSettings.productName = config.product;
            PlayerSettings.bundleVersion = config.versionName;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, config.bundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, config.bundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, config.bundleId);
            PlayerSettings.Android.bundleVersionCode = config.versionCode;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)config.minSdkVersion;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)config.targetSdkVersion;
            Debug.Log("[BaziBaqa Version] نسخه روی PlayerSettings اعمال شد: " + config.versionName + " (" + config.versionCode + ") با بسته‌ی " + config.bundleId);
        }

        private static void Bump(int semverIndex)
        {
            VersionConfig config = Load();
            config.versionName = BumpSemver(config.versionName, semverIndex);
            config.versionCode = Mathf.Max(1, config.versionCode + 1);
            Save(config);
            Apply();
            Debug.Log("[BaziBaqa Version] نسخه افزایش یافت به: " + config.versionName + " (" + config.versionCode + ")");
        }

        private static string BumpSemver(string version, int index)
        {
            string[] parts = (string.IsNullOrEmpty(version) ? "0.0.0" : version).Split('.');
            while (parts.Length < 3) parts = AppendSegment(parts);
            if (int.TryParse(parts[index], out int value))
            {
                parts[index] = (value + 1).ToString();
                // هر افزایشِ بالاتر، بخش‌های پایین‌تر را صفر می‌کند.
                for (int i = index + 1; i < 3; i++) parts[i] = "0";
            }
            return string.Join(".", parts);
        }

        private static string[] AppendSegment(string[] parts)
        {
            string[] next = new string[parts.Length + 1];
            for (int i = 0; i < parts.Length; i++) next[i] = parts[i];
            next[parts.Length] = "0";
            return next;
        }
    }
}
