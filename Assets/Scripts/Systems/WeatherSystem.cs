using System;
using UnityEngine;

namespace BaziBaqa
{
    public sealed class WeatherSystem : MonoBehaviour
    {
        public WeatherType Current { get; private set; } = WeatherType.Clear;
        public bool IsSevere { get { return Current == WeatherType.Storm; } }
        public event Action<WeatherType> WeatherChanged;

        private float _changeTimer;
        private ParticleSystem _rain;
        private Light _sun;
        private Color _clearFog;

        public void Initialize()
        {
            _changeTimer = 24f;
            _clearFog = new Color(0.18f, 0.28f, 0.31f);
            _sun = FindObjectOfType<Light>();
            CreateRainEffect();
            SetWeather(WeatherType.Clear, false);
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            _changeTimer -= Time.deltaTime;
            if (_changeTimer <= 0f)
            {
                _changeTimer = UnityEngine.Random.Range(22f, 38f);
                WeatherType next = (WeatherType)UnityEngine.Random.Range(0, 4);
                SetWeather(next, true);
            }
            UpdateDayLight();
        }

        public void SetWeather(WeatherType weather, bool announce)
        {
            Current = weather;
            switch (weather)
            {
                case WeatherType.Clear:
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = _clearFog;
                    RenderSettings.fogDensity = 0.012f;
                    SetRain(false, 0f);
                    break;
                case WeatherType.Rain:
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.16f, 0.23f, 0.28f);
                    RenderSettings.fogDensity = 0.019f;
                    SetRain(true, 0.45f);
                    break;
                case WeatherType.Fog:
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.46f, 0.53f, 0.53f);
                    RenderSettings.fogDensity = 0.035f;
                    SetRain(false, 0f);
                    break;
                case WeatherType.Storm:
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.08f, 0.12f, 0.17f);
                    RenderSettings.fogDensity = 0.027f;
                    SetRain(true, 0.95f);
                    break;
            }
            WeatherChanged?.Invoke(weather);
            if (announce) GameEvents.Notify(Loc.Get("toast.weather_changed", GameText.WeatherName(weather)));
        }

        private void UpdateDayLight()
        {
            if (_sun == null) return;
            float day = GameManager.Instance.Clock.NormalizedTime;
            float daylight = Mathf.Clamp01(Mathf.Sin((day - 0.2f) * Mathf.PI * 1.25f));
            float weatherMultiplier = Current == WeatherType.Storm ? 0.48f : (Current == WeatherType.Fog ? 0.7f : 1f);
            _sun.intensity = Mathf.Lerp(0.18f, 1.12f, daylight) * weatherMultiplier;
            _sun.color = Color.Lerp(new Color(0.35f, 0.42f, 0.58f), new Color(1f, 0.88f, 0.7f), daylight);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.1f, 0.14f, 0.23f), new Color(0.35f, 0.43f, 0.47f), daylight) * weatherMultiplier;
        }

        private void CreateRainEffect()
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null) return;
            GameObject rainObject = new GameObject(WorldParts.Rain);
            rainObject.transform.SetParent(GameManager.Instance.World.EffectRoot, false);
            rainObject.transform.position = new Vector3(0f, 13f, 0f);
            _rain = rainObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _rain.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 17f;
            main.startSize = 0.045f;
            main.startColor = new Color(0.55f, 0.78f, 0.95f, 0.6f);
            main.maxParticles = 750;
            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(WorldGenerator.WorldWidth, 0.1f, WorldGenerator.WorldDepth);
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void SetRain(bool enabled, float rate)
        {
            if (_rain == null) return;
            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.rateOverTime = enabled ? rate * 420f : 0f;
            if (enabled)
            {
                if (!_rain.isPlaying) _rain.Play();
            }
            else
            {
                _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
