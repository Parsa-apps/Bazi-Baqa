using System.IO;
using UnityEditor;
using UnityEngine;
using BaziBaqa;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// مدیریت نسخه برای انتشار. «منبع حقیقت» فایلِ Assets/Resources/VersionConfig.json است و
    /// این ابزار آن را روی PlayerSettings اعمال می‌کند (Version Name، Build Number، Package Identifier،
    /// نام شرکت/محصول و سطح‌های SDK) تا هیچ‌وقت دو جای مختلف دو نسخه‌ی متفاوت نگویند.
    ///   • منوهای Bump فقط فایل را عوض می‌کنند و بعد خودش Apply را اجرا می‌کند؛
    ///   • Verify تفاوت‌ها را گزارش می‌کند (بدون نوشتن)؛
    ///   • ApplyBatch/VerifyBatch برای خط فرمان/CI هستند و کد خروجیِ غیرصفر می‌دهند.
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

        [MenuItem("BaziBaqa/Version/Verify Version Consistency")]
        public static void VerifyMenu()
        {
            int problems = Verify();
            EditorUtility.DisplayDialog("مدیریت نسخه",
                problems == 0 ? "همه‌ی فیلدهای نسخه هماهنگ‌اند:\n" + GameVersion.Summary
                              : problems + " مورد ناهماهنگ (جزئیات در کنسول).",
                "باشه");
        }

        /// <summary>خط فرمان: `Unity -batchmode -quit -projectPath . -executeMethod BaziBaqa.EditorTools.VersionManager.ApplyBatch`</summary>
        public static void ApplyBatch()
        {
            Apply();
            EditorApplication.Exit(Verify() == 0 ? 0 : 1);
        }

        /// <summary>خط فرمان: فقط بررسیِ هماهنگی (بدون نوشتنِ چیزی).</summary>
        public static void VerifyBatch()
        {
            EditorApplication.Exit(Verify() == 0 ? 0 : 1);
        }

        [MenuItem("BaziBaqa/Version/Open Version Config")]
        public static void OpenConfig()
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath));
        }

        /// <summary>پیکربندیِ جاری؛ برای ابزارهایِ Editor دیگر (مثل AndroidBuild) همین لازم است.</summary>
        public static VersionConfig Config { get { return Load(); } }

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

        /// <summary>اعمالِ پیکربندی روی PlayerSettings (Version Name / Build Number / Package ID / SDK).</summary>
        public static void Apply()
        {
            VersionConfig config = Load();
            PlayerSettings.companyName = config.company;
            PlayerSettings.productName = config.product;
            PlayerSettings.bundleVersion = config.versionName;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, config.bundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, config.bundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, config.bundleId);
            PlayerSettings.Android.bundleVersionCode = config.versionCode;
            // در Unity 2022.3 عضوهایِ نمادینِ تازه برای API levelها نیست؛ پس تبدیلِ عددی با محافظ.
            PlayerSettings.Android.minSdkVersion = ToSdkVersion(config.minSdkVersion, AndroidSdkVersions.AndroidApiLevel26);
            PlayerSettings.Android.targetSdkVersion = ToSdkVersion(config.targetSdkVersion, AndroidSdkVersions.AndroidApiLevelAuto);
            Debug.Log("[BaziBaqa Version] اعمال شد → Version Name: " + config.versionName
                      + " | Build Number: " + config.versionCode
                      + " | Package: " + config.bundleId
                      + " | minSdk: " + config.minSdkVersion + ", targetSdk: " + config.targetSdkVersion
                      + " | Product: " + config.product);
        }

        /// <summary>
        /// مقدارِ عددی API level را به enum تبدیل می‌کند؛ اگر آن سطح در این نسخه‌ی Unity تعریف نشده
        /// بود (مثلاً API 36 در Unity 2022.3) به مقدارِ پیش‌فرض برمی‌گردد تا کامپایل/کنسول نشکند.
        /// </summary>
        private static AndroidSdkVersions ToSdkVersion(int apiLevel, AndroidSdkVersions fallback)
        {
            return System.Enum.IsDefined(typeof(AndroidSdkVersions), apiLevel)
                ? (AndroidSdkVersions)apiLevel
                : fallback;
        }

        /// <summary>
        /// بررسیِ هماهنگیِ «فایلِ پیکربندی» با PlayerSettings. هر اختلاف یک خطای کنسول است؛
        /// عددِ برگشتی تعدادِ موارد است (۰ یعنی همه‌چیز منسجم).
        /// </summary>
        public static int Verify()
        {
            VersionConfig config = Load();
            int problems = 0;

            problems += Compare("PlayerSettings.bundleVersion", PlayerSettings.bundleVersion, config.versionName);
            problems += Compare("PlayerSettings.Android.bundleVersionCode",
                PlayerSettings.Android.bundleVersionCode.ToString(), config.versionCode.ToString());
            problems += Compare("PlayerSettings.companyName", PlayerSettings.companyName, config.company);
            problems += Compare("PlayerSettings.productName", PlayerSettings.productName, config.product);
            problems += Compare("Package Identifier (Android)",
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android), config.bundleId);
            problems += Compare("Package Identifier (iPhone)",
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS), config.bundleId);
            problems += Compare("Package Identifier (Standalone)",
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone), config.bundleId);
            problems += Compare("AndroidMinSdkVersion",
                ((int)PlayerSettings.Android.minSdkVersion).ToString(), config.minSdkVersion.ToString());
            problems += Compare("AndroidTargetSdkVersion",
                ((int)PlayerSettings.Android.targetSdkVersion).ToString(), config.targetSdkVersion.ToString());

            if (config.versionCode < 1)
            {
                Debug.LogError("[BaziBaqa Version] Build Number باید ۱ یا بیشتر باشد: " + config.versionCode);
                problems++;
            }

            if (problems == 0)
            {
                Debug.Log("[BaziBaqa Version] ✓ نسخه منسجم است: " + config.versionName + " (" + config.versionCode
                          + ") — " + config.bundleId);
            }
            else
            {
                Debug.LogError("[BaziBaqa Version] ✗ " + problems + " ناهماهنگی؛ «BaziBaqa > Version > Apply Version To Player Settings» را اجرا کنید.");
            }

            return problems;
        }

        private static int Compare(string field, string actual, string expected)
        {
            if (string.Equals(actual, expected, System.StringComparison.Ordinal)) return 0;
            Debug.LogError("[BaziBaqa Version] " + field + " = «" + actual + "» ولی VersionConfig.json می‌گوید «" + expected + "»");
            return 1;
        }

        private static void Bump(int semverIndex)
        {
            VersionConfig config = Load();
            config.versionName = BumpSemver(config.versionName, semverIndex);
            config.versionCode = Mathf.Max(1, config.versionCode + 1);
            Save(config);
            Apply();
            Debug.Log("[BaziBaqa Version] نسخه افزایش یافت به: " + config.versionName + " (" + config.versionCode + ")");
            if (Verify() > 0) EditorUtility.DisplayDialog("مدیریت نسخه", "نسخه افزایش یافت ولی بعضی fields هنوز ناهماهنگ‌اند (کنسول را ببینید).", "باشه");
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
