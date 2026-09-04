using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>نقطه‌ی ورود بازی؛ قبل از همه‌ی سامانه‌ها ساخته می‌شود.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private GameManager _gameManager;
        private UIManager _ui;
        private RuntimeLogger _runtimeLogger;

        private void Awake()
        {
            EnsureRuntimeLogger();
            EnsureEventSystem();
            LoadLocalization();
            LoadVersion();
            _gameManager = GetComponent<GameManager>();
            if (_gameManager == null) _gameManager = gameObject.AddComponent<GameManager>();
            _ui = GetComponent<UIManager>();
            if (_ui == null) _ui = gameObject.AddComponent<UIManager>();
            _gameManager.Initialize(_ui);
            _ui.Initialize(_gameManager);
        }

        /// <summary>بارگذاری جدول بومی‌سازی و بازیابی زبانِ ذخیره‌شده‌ی بازیکن.</summary>
        private void LoadLocalization()
        {
            TextAsset table = Resources.Load<TextAsset>("Localization/LocalizationTable");
            LocalizationManager.LoadFromTextAsset(table);
            LocalizationManager.ApplySavedLanguage();
        }

        /// <summary>بارگذاری پیکربندی نسخه برای نمایش و نگهداری.</summary>
        private void LoadVersion()
        {
            TextAsset version = Resources.Load<TextAsset>("VersionConfig");
            GameVersion.LoadFromTextAsset(version);
        }

        private void Start()
        {
            _ui.BeginPresentation();
            if (_runtimeLogger != null) _runtimeLogger.Initialize();
        }

        /// <summary>ثبت‌کننده‌ی زمان اجرا را روی همین گره فعال می‌کند تا خطاها و کرش‌ها ذخیره شوند.</summary>
        private void EnsureRuntimeLogger()
        {
            _runtimeLogger = GetComponent<RuntimeLogger>();
            if (_runtimeLogger == null) _runtimeLogger = gameObject.AddComponent<RuntimeLogger>();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventObject = new GameObject(WorldParts.InputEventSystem);
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<StandaloneInputModule>();
        }
    }
}
