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
    /// خروجی پیش‌فرض در پوشه‌ی &lt;project&gt;/Builds/Android ذخیره می‌شود.
    /// </summary>
    public static class AndroidBuild
    {
        private const string BundleId = "com.parsaapps.bazibaqa";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string BuildFolder = "Builds/Android";

        [MenuItem("BaziBaqa/Build Options/Prepare Android Settings")]
        public static void PrepareAndroidSettings()
        {
            ConfigureAndroidPlayer();
            Debug.Log("[BaziBaqa] تنظیمات Android آماده شد: IL2CPP، ARM64، minSdk 26، بسته‌ی " + BundleId);
        }

        [MenuItem("BaziBaqa/Build/Build APK (تست)")]
        public static void BuildApkDebug()
        {
            Build(BuildPlayerOptions, "BaziBaqa_Debug.apk", BuildOptions.None);
        }

        [MenuItem("BaziBaqa/Build/Build APK (Release)")]
        public static void BuildApkRelease()
        {
            Build(BuildPlayerOptions, "BaziBaqa.apk", BuildOptions.None);
        }

        [MenuItem("BaziBaqa/Build/Build AAB (Google Play)")]
        public static void BuildAppBundle()
        {
            Build(BuildPlayerOptions, "BaziBaqa.aab", BuildOptions.None);
        }

        private static void Build(string[] scenes, string outputFile, BuildOptions options)
        {
            if (!ConfigureAndroidPlayer())
            {
                Debug.LogError("[BaziBaqa] پیکربندی Android ناموفق بود؛ خروجی ساخته نشد.");
                return;
            }

            string buildFolderAbsolute = Path.GetFullPath(BuildFolder);
            Directory.CreateDirectory(buildFolderAbsolute);
            string outputPath = Path.Combine(buildFolderAbsolute, outputFile);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = outputPath,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildResult result = report.summary.result;
            if (result == BuildResult.Succeeded)
            {
                Debug.Log("[BaziBaqa] خروجی با موفقیت ساخته شد: " + outputPath);
            }
            else
            {
                Debug.LogError("[BaziBaqa] خروجی ناقص بود؛ وضعیت: " + result + " | خطاها: " + report.summary.totalErrors);
            }
        }

        private static string[] BuildPlayerOptions
        {
            get { return new[] { ScenePath }; }
        }

        private static bool ConfigureAndroidPlayer()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[BaziBaqa] تعویض پلتفرم به Android ناموفق بود.");
                    return false;
                }
            }

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidETC2Fallback = AndroidETC2Fallback.Quality32;
            return true;
        }
    }
}
