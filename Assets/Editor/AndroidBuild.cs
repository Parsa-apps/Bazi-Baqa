using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// ابزار خودکار خروجی Android. با منوی «BaziBaqa» در Editor می‌توان خروجی APK یا AAB را
    /// بدون باز کردن پنجره‌ی Build Settings تولید کرد. این اسکریپت فقط در Editor کامپایل می‌شود
    /// و هیچ‌گونه وابستگی به Runtime بازی ندارد.
    ///
    /// جریان کار: ممیزی پروژه → پیکربندی Player → بیلد → گزارش.
    /// پوشه‌ی خروجی پیش‌فرض: &lt;project&gt;/Builds/Android
    ///
    /// نکته‌ی سازگاری: در Unity 2022.3 گزینه‌های قدیمیِ androidBuildSystem / androidBuildType /
    /// androidETC2Fallback حذف یا منسوخ شده‌اند؛ به‌جای آن‌ها از EditorUserBuildSettings.development
    /// و AndroidBuildSubtarget استفاده می‌شود تا کنسول یونیتی بدون Warning بماند.
    /// </summary>
    public static class AndroidBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string BuildFolder = "Builds/Android";

        /// <summary>
        /// هماهنگ‌سازیِ کاملِ تنظیمات Android: نسخه از VersionConfig.json روی PlayerSettings،
        /// بعد IL2CPP/ARM64/SDKها. منبعِ حقیقت فقط همان فایل است تا بیلد و استور دو چیز نگویند.
        /// </summary>
        [MenuItem("BaziBaqa/Build Options/Prepare Android Settings")]
        public static void PrepareAndroidSettings()
        {
            VersionManager.Apply();
            ConfigureAndroidPlayer(false);
            VersionConfig config = VersionManager.Config;
            Debug.Log("[BaziBaqa] تنظیمات Android آماده شد: IL2CPP، ARM64، ASTC، نسخه "
                + config.versionName + " (" + config.versionCode + ")، minSdk " + config.minSdkVersion
                + "، targetSdk " + config.targetSdkVersion
                + "، بسته‌ی " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        }

        [MenuItem("BaziBaqa/Build/Build APK (Debug)")]
        public static void BuildApkDebug()
        {
            Build("BaziBaqa_Debug.apk", BuildOptions.Development | BuildOptions.ConnectWithProfiler, false, true);
        }

        [MenuItem("BaziBaqa/Build/Build APK (Release)")]
        public static void BuildApkRelease()
        {
            Build("BaziBaqa.apk", BuildOptions.None, false, false);
        }

        [MenuItem("BaziBaqa/Build/Build AAB (Google Play)")]
        public static void BuildAppBundle()
        {
            Build("BaziBaqa.aab", BuildOptions.None, true, false);
        }

        /// <summary>نقطه‌ی ورود CI: ساخت خروجی با خط فرمان Unity (بدون منو).</summary>
        public static void BuildFromCommandLine()
        {
            string output = System.Environment.GetEnvironmentVariable("BAZIBAAQA_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "BaziBaqa.aab";
            bool bundle = output.EndsWith(".aab", System.StringComparison.OrdinalIgnoreCase);
            Build(output, BuildOptions.None, bundle, false);
        }

        private static void Build(string outputFile, BuildOptions options, bool appBundle, bool development)
        {
            // دروازه‌ی کیفیت: پیش از ساخت، پروژه را ممیزی کن تا با مرجع شکسته یا صحنه‌ی خراب خروجی نگیریم.
            if (!ProjectAudit.RunFullAudit(false))
            {
                Debug.LogError("[BaziBaqa] ممیزی پروژه ناموفق بود؛ برای جلوگیری از خروجی معیوب، بیلد متوقف شد. ابتدا موارد بالا را برطرف کنید.");
                return;
            }

            // نسخه همیشه از VersionConfig.json به PlayerSettings برسد (Build Number تکراری در استور نشود).
            VersionManager.Apply();
            if (VersionManager.Verify() > 0)
            {
                Debug.LogError("[BaziBaqa] نسخه با PlayerSettings هماهنگ نیست؛ بیلد متوقف شد.");
                return;
            }

            VersionConfig config = VersionManager.Config;
            outputFile = StampVersion(outputFile, config.versionName);

            if (!ConfigureAndroidPlayer(appBundle, development))
            {
                Debug.LogError("[BaziBaqa] پیکربندی Android ناموفق بود؛ خروجی ساخته نشد.");
                return;
            }

            string buildFolderAbsolute = Path.GetFullPath(BuildFolder);
            Directory.CreateDirectory(buildFolderAbsolute);
            string outputPath = Path.Combine(buildFolderAbsolute, outputFile);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.Android,
                locationPathName = outputPath,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildResult result = report.summary.result;
            if (result == BuildResult.Succeeded)
            {
                Debug.Log("[BaziBaqa] خروجی " + (appBundle ? "AAB" : "APK") + " با موفقیت ساخته شد: " + outputPath
                    + " | حجم: " + (report.totalSize / (1024 * 1024)) + " MB | نسخه: " + config.versionName
                    + " (" + config.versionCode + ") | بسته: " + config.bundleId);
            }
            else
            {
                Debug.LogError("[BaziBaqa] خروجی ناقص بود؛ وضعیت: " + result + " | خطاها: " + report.summary.totalErrors);
            }
        }

        /// <summary>پیکربندی Player برای Android. اگر appBundle درست باشد خروجی AAB وگرنه APK می‌شود.</summary>
        private static bool ConfigureAndroidPlayer(bool appBundle)
        {
            return ConfigureAndroidPlayer(appBundle, false);
        }

        private static bool ConfigureAndroidPlayer(bool appBundle, bool development)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[BaziBaqa] تعویض پلتفرم به Android ناموفق بود.");
                    return false;
                }
            }

            VersionConfig config = VersionManager.Config;
            // SDKها از VersionConfig.json (عددِ API level برابر مقدارِ enum است)؛ اگر نسخه‌ی Unity
            // آن سطح را نشناسد، AndroidApiLevelAuto انتخاب می‌شود (نه خطای کامپایل، نه بیلدِ ناقص).
            PlayerSettings.Android.minSdkVersion = ToSdk(config.minSdkVersion, AndroidSdkVersions.AndroidApiLevel26);
            PlayerSettings.Android.targetSdkVersion = ToSdk(config.targetSdkVersion, AndroidSdkVersions.AndroidApiLevelAuto);

            // امضای خروجی: اگر Keystore در پروژه هست استفاده می‌شود، وگرنه debug keystore تا بیلد نشکند.
            string keystore = FindKeystorePath();
            PlayerSettings.Android.useCustomKeystore = !string.IsNullOrEmpty(keystore);
            if (!string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.keystorePass = System.Environment.GetEnvironmentVariable("BAZIBAAQA_KEYSTORE_PASS") ?? string.Empty;
                PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable("BAZIBAAQA_KEYALIAS_PASS") ?? string.Empty;
                string alias = System.Environment.GetEnvironmentVariable("BAZIBAAQA_KEYALIAS");
                if (!string.IsNullOrEmpty(alias)) PlayerSettings.Android.keyaliasName = alias;
                Debug.Log("[BaziBaqa] keystore سفارشی فعال: " + keystore);
            }

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.High);
            // مدهای متمایز: Development برای تست روی دستگاه، Release برای انتشار.
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            return true;
        }

        private static AndroidSdkVersions ToSdk(int apiLevel, AndroidSdkVersions fallback)
        {
            return System.Enum.IsDefined(typeof(AndroidSdkVersions), apiLevel)
                ? (AndroidSdkVersions)apiLevel
                : fallback;
        }

        /// <summary>نامِ فایلِ خروجی با نسخه برچسب می‌خورد: BaziBaqa.aab → BaziBaqa-0.2.0.aab</summary>
        private static string StampVersion(string outputFile, string versionName)
        {
            if (string.IsNullOrEmpty(outputFile) || string.IsNullOrEmpty(versionName)) return outputFile;
            string extension = Path.GetExtension(outputFile);
            string stem = Path.GetFileNameWithoutExtension(outputFile);
            if (stem.Contains(versionName)) return outputFile;
            return stem + "-" + versionName + extension;
        }

        /// <summary>Keystore استودیو: متغیرِ محیطی یا Assets/Keystore/*.keystore.</summary>
        private static string FindKeystorePath()
        {
            string fromEnvironment = System.Environment.GetEnvironmentVariable("BAZIBAAQA_KEYSTORE");
            if (!string.IsNullOrEmpty(fromEnvironment) && File.Exists(fromEnvironment)) return fromEnvironment;

            string folder = Path.Combine("Assets", "Keystore");
            if (!Directory.Exists(folder)) return null;
            string[] files = Directory.GetFiles(folder, "*.keystore", SearchOption.AllDirectories);
            if (files.Length == 0) return null;
            return files[0].Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }
}
