using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// بهینه‌ساز زمان اجرا برای دستگاه‌های متوسط. کیفیت را بر اساس نرخ فریم به‌صورت خودکار کم می‌کند
    /// تا بازی روی گوشی‌های ضعیف‌تر هم روان بماند؛ هیچ کیفیتی بدون نیاز کاهش نمی‌یابد.
    /// </summary>
    public sealed class PerformanceManager : MonoBehaviour
    {
        private const float LowFpsThreshold = 30f;
        private const float HighFpsThreshold = 55f;

        private float _fpsSampleTimer;
        private int _frames;
        private bool _downgraded;
        private int _currentLevel;

        public int CurrentQualityLevel { get { return _currentLevel; } }

        public void Initialize()
        {
            _currentLevel = QualitySettings.GetQualityLevel();
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            _fpsSampleTimer += Time.unscaledDeltaTime;
            _frames++;
            if (_fpsSampleTimer < 2.5f) return;

            float fps = _frames / _fpsSampleTimer;
            bool low = fps < LowFpsThreshold;
            bool high = fps > HighFpsThreshold;
            _fpsSampleTimer = 0f;
            _frames = 0;

            if (low && _currentLevel > 0)
            {
                _currentLevel--;
                QualitySettings.SetQualityLevel(_currentLevel, true);
                _downgraded = true;
                GameLogger.Info("برای روان‌تر شدن، کیفیت به «" + QualitySettings.names[_currentLevel] + "» کاهش یافت.");
            }
            else if (high && !_downgraded && _currentLevel < QualitySettings.names.Length - 1)
            {
                _currentLevel++;
                QualitySettings.SetQualityLevel(_currentLevel, true);
                GameLogger.Info("کیفیت به «" + QualitySettings.names[_currentLevel] + "» ارتقا یافت.");
            }
        }
    }
}
