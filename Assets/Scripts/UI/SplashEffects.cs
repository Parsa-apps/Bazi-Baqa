using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// ذرات انرژی و برق‌های بسیار سبک برای Splash؛ بدون ذرات سنگین یا Asset خارجی.
    /// </summary>
    public sealed class SplashEffects : MonoBehaviour
    {
        private readonly List<Image> _particles = new List<Image>();
        private readonly List<float> _speeds = new List<float>();
        private readonly List<Image> _lightning = new List<Image>();
        private float _time;

        private void Awake()
        {
            for (int i = 0; i < 20; i++)
            {
                GameObject particleObject = new GameObject(WorldParts.EnergyParticle, typeof(RectTransform), typeof(Image));
                particleObject.transform.SetParent(transform, false);
                RectTransform rect = particleObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * Random.Range(3f, 9f);
                rect.anchoredPosition = new Vector2(Random.Range(-390f, 390f), Random.Range(-360f, 430f));
                Image image = particleObject.GetComponent<Image>();
                image.color = new Color(0.2f, 0.85f, 0.76f, Random.Range(0.2f, 0.75f));
                image.raycastTarget = false;
                _particles.Add(image);
                _speeds.Add(Random.Range(12f, 32f));
            }
            for (int i = 0; i < 3; i++)
            {
                GameObject boltObject = new GameObject(WorldParts.Lightning, typeof(RectTransform), typeof(Image));
                boltObject.transform.SetParent(transform, false);
                RectTransform rect = boltObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(Random.Range(1f, 3f), Random.Range(85f, 170f));
                rect.anchoredPosition = new Vector2(Random.Range(-320f, 320f), Random.Range(80f, 320f));
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-28f, 28f));
                Image image = boltObject.GetComponent<Image>();
                image.color = new Color(0.65f, 0.94f, 1f, 0f);
                image.raycastTarget = false;
                _lightning.Add(image);
            }
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i] == null) continue;
                RectTransform rect = _particles[i].rectTransform;
                rect.anchoredPosition += Vector2.up * _speeds[i] * Time.unscaledDeltaTime;
                if (rect.anchoredPosition.y > 520f) rect.anchoredPosition = new Vector2(Random.Range(-390f, 390f), -420f);
                Color color = _particles[i].color;
                color.a = 0.2f + (Mathf.Sin(_time * 2f + i) + 1f) * 0.18f;
                _particles[i].color = color;
            }
            for (int i = 0; i < _lightning.Count; i++)
            {
                Color color = _lightning[i].color;
                float flash = Mathf.Max(0f, Mathf.Sin(_time * (2.1f + i * 0.7f) + i * 2f));
                color.a = flash > 0.96f ? 0.85f : 0f;
                _lightning[i].color = color;
            }
        }
    }
}
