using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// جلوه‌های حرفه‌ای برای دکمه‌ها: انیمیشن نور هنگام لمس، حرکت نرم ضربان و لرزش خفیف (حس لمسی).
    /// فقط روی عناصری فعال است که EventSystem آن‌ها را لمس می‌کند؛ نرخ فریم را پایین نمی‌آورد.
    /// </summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float _springAmount = 0.08f;
        [SerializeField] private float _pulseDuration = 0.12f;
        [SerializeField] private bool _haptics = true;
        [SerializeField] private float _rippleDuration = 0.3f;

        /// <summary>لرزشِ دستگاه سراسری خاموش/روشن می‌شود (تنظیماتِ بازی و QA).</summary>
        public static bool HapticsEnabled = true;

        private RectTransform _rect;
        private Vector3 _baseScale;
        private Coroutine _routine;
        private Coroutine _rippleRoutine;
        private Image _ripple;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect != null) _baseScale = _rect.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PlayPulse(1f - _springAmount * 2f);
            PlayRipple();
            if (_haptics && HapticsEnabled) Vibrate();
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayClick();
        }

        /// <summary>لرزشِ سبک؛ فقط روی دستگاه‌هایِ موبایل (در ویرایشگر بی‌اثر است).</summary>
        private static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private void PlayRipple()
        {
            if (_rect == null) return;
            if (_ripple == null)
            {
                GameObject rippleObject = new GameObject("ButtonRipple", typeof(RectTransform), typeof(Image));
                rippleObject.transform.SetParent(_rect, false);
                RectTransform rippleRect = rippleObject.GetComponent<RectTransform>();
                rippleRect.anchorMin = rippleRect.anchorMax = new Vector2(0.5f, 0.5f);
                rippleRect.sizeDelta = new Vector2(_rect.rect.width * 0.7f, _rect.rect.height * 1.4f);
                Image image = rippleObject.GetComponent<Image>();
                image.color = new Color(0.75f, 1f, 0.98f, 0.24f);
                image.raycastTarget = false;
                _ripple = image;
            }
            _ripple.enabled = true;
            if (_rippleRoutine != null) StopCoroutine(_rippleRoutine);
            _rippleRoutine = StartCoroutine(RippleRun());
        }

        private IEnumerator RippleRun()
        {
            float elapsed = 0f;
            RectTransform rippleRect = _ripple.rectTransform;
            Vector2 baseSize = rippleRect.sizeDelta;
            Color baseColor = _ripple.color;
            while (elapsed < _rippleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _rippleDuration);
                rippleRect.sizeDelta = baseSize * (1f + t * 0.85f);
                Color color = baseColor;
                color.a = baseColor.a * (1f - t) * (1f - t);
                _ripple.color = color;
                yield return null;
            }
            if (_ripple != null) _ripple.enabled = false;
            _rippleRoutine = null;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PlayPulse(1f + _springAmount);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PlayPulse(1f + _springAmount * 0.5f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PlayPulse(1f);
        }

        private void PlayPulse(float target)
        {
            if (_rect == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PulseTo(target));
        }

        private IEnumerator PulseTo(float target)
        {
            float elapsed = 0f;
            Vector3 start = _rect.localScale;
            Vector3 end = _baseScale * target;
            while (elapsed < _pulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _pulseDuration);
                _rect.localScale = Vector3.Lerp(start, end, t * t * (3f - 2f * t));
                yield return null;
            }
            _rect.localScale = end;
        }
    }
}
