using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// تاج طلایی متحرک در صفحه‌ی شروع: با نور نبض می‌زند، هاله‌ی طلایی انتشار می‌دهد و به‌آرامی
    /// بزرگ و کوچک می‌شود تا حس یک استودیو بازی‌سازی حرفه‌ای را القا کند.
    /// </summary>
    public sealed class CrownPulse : MonoBehaviour
    {
        private Image _image;
        private Transform _halo;
        private float _time;

        public void Configure(Transform halo)
        {
            _halo = halo;
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void Update()
        {
            if (_image == null) return;
            _time += Time.unscaledDeltaTime;

            // نبض اندازه
            float scale = 1f + Mathf.Sin(_time * 2f) * 0.06f;
            transform.localScale = Vector3.one * scale;

            // درخشش طلایی
            Color c = _image.color;
            c.a = 1f;
            _image.color = c;

            // هاله‌ی طلایی
            if (_halo == null) return;
            float haloScale = 1f + Mathf.Sin(_time * 1.6f) * 0.12f;
            _halo.localScale = Vector3.one * haloScale;
        }
    }
}
