using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BaziBaqa.EditorTools
{
    /// <summary>
    /// اعتبارسنجی زمان اجرا در Editor — همان بررسی‌هایی که دستی در Unity انجام می‌شود را یک‌جا و
    /// خودکار اجرا می‌کند و در فایل گزارش می‌نویسد:
    ///   ۱) صحت کامپایل (نبودِ خطای اسکریپت) و پایش کنسول در طول اجرا.
    ///   ۲) بارگذاری صحنه‌ی اصلی و سالم‌بودن همه‌ی کامپوننت‌ها (نبود Missing Script).
    ///   ۳) چرخه‌ی ذخیره/بارگذاری روی دیسک (persistentDataPath) با مقایسه‌ی داده‌ها.
    ///   ۴) آزمون تعامل UI: بوم، دکمه‌های قابل‌کلیک، فونت متن‌ها و نبودِ کلیدِ خام در نمایش.
    ///   ۵) ممیزی بومی‌سازی / فونت فارسی / هماهنگی نسخه (هر بخش ابزار خودش را دارد).
    /// اجرای دستی: منوی BaziBaqa &gt; Validation &gt; Run Runtime Validation
    /// اجرای خودکار: Unity -batchmode -quit -projectPath . -executeMethod BaziBaqa.EditorTools.RuntimeValidation.ValidateBatch
    /// تست‌های EditMode/PlayMode با Test Runner یا `-runTests` اجرا می‌شوند (Tools/unity_validation.sh).
    /// </summary>
    public static class RuntimeValidation
    {
        private const string ReportPath = "Logs/UnityValidationReport.txt";
        private static readonly List<string> ConsoleErrors = new List<string>();
        private static readonly List<string> ConsoleWarnings = new List<string>();
        private static readonly List<string> Failures = new List<string>();
        private static readonly List<string> Notes = new List<string>();
        private static bool _capturing;

        [MenuItem("BaziBaqa/Validation/Run Runtime Validation")]
        public static void RunMenu()
        {
            int failures = Run();
            EditorUtility.DisplayDialog("اعتبارسنجی زمان اجرا",
                failures == 0 ? "همه‌ی بررسی‌ها پاس شد. ✓\nگزارش: " + ReportPath
                               : failures + " مورد نیازمند رفع است. ✗\nگزارش: " + ReportPath,
                "باشه");
        }

        /// <summary>نقطه‌ی ورود خط فرمان؛ کد خروجیِ غیرصفر یعنی معتبرسازی پاس نشده است.</summary>
        public static void ValidateBatch()
        {
            EditorApplication.Exit(Run() == 0 ? 0 : 1);
        }

        /// <summary>اجرای کامل مجموعه‌ی بررسی‌ها. تعداد موارد ناموفق را برمی‌گرداند.</summary>
        public static int Run()
        {
            Failures.Clear();
            Notes.Clear();
            lock (ConsoleErrors) ConsoleErrors.Clear();
            lock (ConsoleWarnings) ConsoleWarnings.Clear();

            AssetDatabase.Refresh();
            StartCapturing();
            try
            {
                CheckCompilation();
                CheckSceneLoading();
                CheckSaveLoadCycle();
                CheckUserInterface();
                CheckTypography();
                LocalizationAudit.Run(false);
                VersionAudit();
            }
            finally
            {
                StopCapturing();
            }

            WriteReport();
            if (Failures.Count == 0)
            {
                Debug.Log("[BaziBaqa Validation] ✓ اعتبارسنجی زمان اجرا پاس شد. (" + Notes.Count + " بررسی انجام شد)");
            }
            else
            {
                Debug.LogError("[BaziBaqa Validation] ✗ " + Failures.Count + " مشکل یافت شد:\n• " + string.Join("\n• ", Failures.ToArray()));
            }
            return Failures.Count;
        }

        /// <summary>
        /// assetِ فونتِ TMP را اگر نبود می‌سازد و بعد پوششِ حروف و تنظیمات import را می‌سنجد.
        /// بدون این مرحله ممکن است بازی با فونتِ پیش‌فرضِ TMP باز شود و حروفِ فارسی نیفتد.
        /// </summary>
        private static void CheckTypography()
        {
            if (!File.Exists(GameFont.TmpAssetPath))
            {
                Notes.Add("assetِ فونتِ TMP در پروژه نبود؛ بیکِ خودکار انجام شد.");
                TypographyBaker.Bake(false);
            }

            int problems = TypographyBaker.Validate(true, false);
            if (problems > 0)
            {
                Failures.Add("تایپوگرافی فارسی: " + problems + " مشکل (جزئیات با برچسب [BaziBaqa Typography] در کنسول است).");
            }
            else
            {
                Notes.Add("تایپوگرافی: بک‌اندِ فعال = " + GameTextBackend.ActiveBackendName);
            }
        }

        // ---------- ۱) کامپایل و کنسول ----------

        private static void StartCapturing()
        {
            if (_capturing) return;
            _capturing = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        private static void StopCapturing()
        {
            if (!_capturing) return;
            _capturing = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                lock (ConsoleErrors) ConsoleErrors.Add(type + ": " + message + "\n" + stackTrace);
            }
            else if (type == LogType.Warning)
            {
                lock (ConsoleWarnings) ConsoleWarnings.Add(message);
            }
        }

        private static void CheckCompilation()
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                Fail("کامپایل اسکریپت‌ها در Editor خطا دارد (EditorUtility.scriptCompilationFailed).");
                return;
            }
            Note("کامپایل اسکریپت‌ها بدون خطا است.");
        }

        // ---------- ۲) بارگذاری صحنه ----------

        private static void CheckSceneLoading()
        {
            const string scenePath = "Assets/Scenes/Main.unity";
            if (!File.Exists(scenePath))
            {
                Fail("صحنه‌ی " + scenePath + " روی دیسک نیست.");
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail("باز کردن صحنه‌ی Main ناموفق بود.");
                return;
            }
            GameObject[] objects = scene.GetRootGameObjects();
            if (objects.Length == 0)
            {
                Fail("صحنه‌ی Main هیچ گره ریشه‌ای ندارد.");
                return;
            }
            int components = 0;
            bool bootstrapFound = false;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].GetComponent<GameBootstrap>() != null) bootstrapFound = true;
                Component[] found = objects[i].GetComponentsInChildren<Component>(true);
                for (int c = 0; c < found.Length; c++)
                {
                    if (found[c] == null)
                    {
                        Fail("کامپوننتِ ازدست‌رفته (Missing Script) روی «" + objects[i].name + "».");
                        continue;
                    }
                    components++;
                    SerializedObject serialized = new SerializedObject(found[c]);
                    SerializedProperty script = serialized.FindProperty("m_Script");
                    if (script != null && found[c] is MonoBehaviour && script.objectReferenceValue == null)
                        Fail("مرجع اسکریپتِ شکسته روی «" + found[c].GetType().Name + "» در " + objects[i].name + ".");
                }
            }
            if (!bootstrapFound) Fail("صحنه‌ی Main هیچ GameBootstrap ای ندارد؛ بازی اجرا نمی‌شود.");
            Note("بارگذاری صحنه: " + objects.Length + " ریشه، " + components + " کامپوننت سالم، GameBootstrap " + (bootstrapFound ? "یافت شد" : "نیست") + ".");
        }

        // ---------- ۳) ذخیره/بارگذاری ----------

        private static void CheckSaveLoadCycle()
        {
            SaveSystem save = new SaveSystem();
            GameSaveData original = GameSaveData.CreateNew(4242);
            original.day = 3;
            original.dayTime = 0.5f;
            original.resources.Set(ResourceType.Wood, 137);
            original.questIndex = 5;
            if (!save.Save(original))
            {
                Fail("نوشتن فایل ذخیره ناموفق بود: " + save.SavePath);
                return;
            }
            if (!save.HasSave()) Fail("فایل ذخیره نوشته شد ولی HasSave مقدار false برمی‌گرداند.");
            GameSaveData loaded = save.Load();
            if (loaded == null)
            {
                Fail("بارگذاری فایل ذخیره ناموفق بود.");
                return;
            }
            if (loaded.resources == null || loaded.resources.Get(ResourceType.Wood) != 137) Fail("مقدار منابع پس از بارگذاری حفظ نشد.");
            if (loaded.day != 3) Fail("شماره‌ی روز پس از بارگذاری حفظ نشد.");
            if (loaded.saveVersion != SaveSystem.CurrentSaveVersion) Fail("نسخه‌ی ذخیره پس از مهاجرت درست نیست.");
            if (loaded.survivors == null || loaded.survivors.Count == 0) Fail("بازمانده‌ها پس از بارگذاری خالی هستند.");
            Note("چرخه‌ی ذخیره/بارگذاری سالم است (مسیر: " + save.SavePath + ").");
        }

        // ---------- ۴) تعامل UI ----------

        private static void CheckUserInterface()
        {
            GameObject host = new GameObject("[validation-ui]");
            try
            {
                GameManager game = host.AddComponent<GameManager>();
                UIManager ui = host.AddComponent<UIManager>();
                game.Initialize(ui);
                ui.Initialize(game);
                game.StartNewGame();

                Canvas canvas = host.GetComponentInChildren<Canvas>(true);
                if (canvas == null) Fail("بوم رابط کاربری ساخته نشد.");
                else if (canvas.gameObject.GetComponent<GraphicRaycaster>() == null)
                    Fail("بوم فاقد GraphicRaycaster است؛ هیچ دکمه‌ای با لمس کار نمی‌کند.");

                Button[] buttons = host.GetComponentsInChildren<Button>(true);
                if (buttons.Length == 0)
                {
                    Fail("هیچ دکمه‌ای در UI پیدا نشد.");
                    return;
                }
                int clickable = 0;
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].interactable && buttons[i].targetGraphic != null && buttons[i].targetGraphic.raycastTarget) clickable++;
                }
                if (clickable == 0) Fail("هیچ دکمه‌ای قابل‌کلیک نیست (raycastTarget / targetGraphic بررسی شود).");

                // پنل‌ها و مودال‌ها را باز و سپس با «دکمه‌ی بازگشت» می‌بندیم (تست تعامل واقعی).
                ui.ShowTutorial();
                bool backHandled = ui.HandleBack();
                if (!backHandled) Fail("HandleBack پس از باز کردن راهنما چیزی نبست.");

                int labels = 0;
                Graphic[] graphics = host.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    Text legacy = graphics[i] as Text;
                    if (legacy != null)
                    {
                        labels++;
                        if (legacy.font == null) Fail("برچسب UI بدون فونت: «" + Shorten(legacy.text) + "»");
                        if (LooksLikeRawKey(legacy.text)) Fail("کلیدِ خام در UI نمایش داده می‌شود (کلید در جدول نیست): " + legacy.text);
                    }
                }
                Note("بررسی UI: " + buttons.Length + " دکمه (" + clickable + " قابل‌کلیک)، " + labels + " برچسب متنی.");
            }
            catch (Exception exception)
            {
                Fail("آزمون UI با استثنا متوقف شد: " + exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---------- ۵) نسخه ----------

        private static void VersionAudit()
        {
            string expected = PlayerSettings.bundleVersion;
            int expectedCode = PlayerSettings.Android.bundleVersionCode;
            string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (string.IsNullOrEmpty(expected)) Fail("PlayerSettings.bundleVersion خالی است.");
            if (expectedCode < 1) Fail("کد نسخه‌ی اندروید باید دست‌کم ۱ باشد.");
            if (string.IsNullOrEmpty(identifier) || !identifier.StartsWith("com.parsaapps.", StringComparison.Ordinal))
                Fail("شناسه‌ی بسته‌ی اندروید انتظار می‌رفت با com.parsaapps. آغاز شود: " + identifier);
            Note("نسخه‌ی ثبت‌شده در Player: " + expected + " (" + expectedCode + ") — " + identifier);
        }

        // ---------- گزارش ----------

        private static void WriteReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("گزارش اعتبارسنجی زمان اجرا — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Unity: " + Application.unityVersion + " | پلتفرم فعال: " + EditorUserBuildSettings.activeBuildTarget);
            report.AppendLine("============================================================");
            for (int i = 0; i < Notes.Count; i++) report.AppendLine("  ok   " + Notes[i]);
            lock (ConsoleWarnings)
            {
                for (int i = 0; i < ConsoleWarnings.Count; i++) report.AppendLine("  warn " + ConsoleWarnings[i]);
            }
            lock (ConsoleErrors)
            {
                for (int i = 0; i < ConsoleErrors.Count; i++) report.AppendLine("  ERR  " + ConsoleErrors[i]);
            }
            report.AppendLine("------------------------------------------------------------");
            if (Failures.Count == 0) report.AppendLine("نتیجه: PASS");
            else for (int i = 0; i < Failures.Count; i++) report.AppendLine("  FAIL " + Failures[i]);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, report.ToString());
            if (ConsoleErrors.Count > 0)
            {
                Fail("حین اجرا " + ConsoleErrors.Count + " پیام خطا در کنسول ثبت شد (جزئیات در گزارش).");
                File.AppendAllText(ReportPath, "\n\n" + string.Join("\n", ConsoleErrors.ToArray()) + "\n");
            }
            AssetDatabase.Refresh();
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 28 ? value : value.Substring(0, 25) + "…";
        }

        /// <summary>اگر متن، همان «کلید» بومی‌سازی باشد یعنی جدول آن کلید را ندارد.</summary>
        private static bool LooksLikeRawKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.IndexOf('.') < 0) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!allowed) return false;
            }
            return true;
        }

        private static void Fail(string message)
        {
            Failures.Add(message);
            Debug.LogError("[BaziBaqa Validation] ✗ " + message);
        }

        private static void Note(string message)
        {
            Notes.Add(message);
            Debug.Log("[BaziBaqa Validation] • " + message);
        }
    }
}
