using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    public sealed class GlowPulse : MonoBehaviour
    {
        private Image _image;
        private Color _baseColor;
        private float _time;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null) _baseColor = _image.color;
        }

        private void Update()
        {
            if (_image == null) return;
            _time += Time.unscaledDeltaTime;
            float pulse = 0.72f + Mathf.Sin(_time * 2.1f) * 0.16f;
            _image.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * pulse);
            transform.localScale = Vector3.one * (1f + Mathf.Sin(_time * 1.7f) * 0.025f);
        }
    }
}
