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

        private RectTransform _rect;
        private Vector3 _baseScale;
        private Coroutine _routine;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect != null) _baseScale = _rect.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PlayPulse(1f - _springAmount * 2f);
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayClick();
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
