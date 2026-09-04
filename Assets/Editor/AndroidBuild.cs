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

        [MenuItem("BaziBaqa/Build Options/Prepare Android Settings")]
        public static void PrepareAndroidSettings()
        {
            ConfigureAndroidPlayer(false);
            Debug.Log("[BaziBaqa] تنظیمات Android آماده شد: IL2CPP، ARM64، minSdk از VersionConfig.json، بسته‌ی "
                + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
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
                    + " | حجم: " + (report.totalSize / (1024 * 1024)) + " MB | نسخه: " + PlayerSettings.bundleVersion
                    + " (" + PlayerSettings.Android.bundleVersionCode + ")");
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

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
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
            return true;
        }
    }
}
