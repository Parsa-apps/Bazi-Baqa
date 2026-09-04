using System.Collections;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// باز شدنِ پنجره‌ها (گام ۶): پنل با کمی بزرگ‌نماییِ بیش‌ازحد، محوشدگی و (فقط اگر
    /// اندازه‌اش ثابت باشد) لغزشِ عمودی ظاهر می‌شود. همه‌چیز با زمانِ غیرمقیّد است تا
    /// در منویِ توقف‌کرده هم روان بماند.
    ///
    /// روی «دکمه‌ها» نصب نمی‌شود؛ آن‌ها `ButtonFx` دارند و هر دو `localScale` را می‌نویسند ⇒
    /// یکی باید عقب بنشیند (تستِ ایستای UiLayerEditModeTests همین را نگه می‌دارد).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindowFx : MonoBehaviour
    {
        [SerializeField] private float duration = 0.22f;
        [SerializeField] private float slidePixels = 16f;
        [SerializeField] private float startScale = 0.93f;
        [SerializeField] private bool staggerWithSiblings = true;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _baseAnchoredPosition;
        private Vector3 _baseScale = Vector3.one;
        private Coroutine _routine;
        private int _playCount;

        public bool IsPlaying { get { return _routine != null; } }
        public int PlayCount { get { return _playCount; } }
        public string Report()
        {
            return "window plays=" + _playCount + " dur=" + duration.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Awake()
        {
            _rect = transform as RectTransform;
            if (_rect != null)
            {
                _baseAnchoredPosition = _rect.anchoredPosition;
                _baseScale = _rect.localScale;
                if (_baseScale.sqrMagnitude < 0.0001f) _baseScale = Vector3.one;
            }
        }

        private void OnEnable()
        {
            Play();
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            Restore();
        }

        public void Play()
        {
            if (_rect == null) Awake();
            if (_rect == null) return;
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
            _playCount++;
        }

        private void Restore()
        {
            if (_rect != null)
            {
                _rect.localScale = _baseScale;
                _rect.anchoredPosition = _baseAnchoredPosition;
            }
            if (_group != null) _group.alpha = 1f;
        }

        private IEnumerator Run()
        {
            // ترتیبِ باز شدنِ کارت‌ها: تا سه‌چهار دهمِ ثانیه تاخیر، آن‌هم فشرده
            float delay = staggerWithSiblings ? Mathf.Min(_rect.GetSiblingIndex() * 0.022f, 0.13f) : 0f;
            bool canSlide = _rect.anchorMin == _rect.anchorMax;
            float length = Mathf.Max(0.05f, duration);

            _rect.localScale = _baseScale * startScale;
            _group.alpha = 0f;
            if (canSlide) _rect.anchoredPosition = _baseAnchoredPosition - new Vector2(0f, slidePixels);

            float elapsed = -delay;
            while (elapsed < length)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / length);
                float eased = EaseOutBack(t);
                _rect.localScale = _baseScale * Mathf.Lerp(startScale, 1f, eased);
                _group.alpha = Mathf.Clamp01(elapsed / (length * 0.6f));
                if (canSlide)
                {
                    _rect.anchoredPosition = Vector2.Lerp(
                        _baseAnchoredPosition - new Vector2(0f, slidePixels),
                        _baseAnchoredPosition,
                        eased);
                }
                yield return null;
            }

            _rect.localScale = _baseScale;
            _rect.anchoredPosition = _baseAnchoredPosition;
            _group.alpha = 1f;
            _routine = null;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = Mathf.Clamp01(x) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
