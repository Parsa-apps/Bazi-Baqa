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
        // نور/مه/آسمان در لایه‌ی گرافیک است (SkyLightingRig)؛ این فایل فقط «تغییرِ هوا» را خبر می‌دهد
        private float _rainRate;

        public void Initialize()
        {
            _changeTimer = 24f;
            CreateRainEffect();
            SetWeather(WeatherType.Clear, false);
            if (SkyLightingRig.Instance != null) SkyLightingRig.Instance.Refresh();
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
        }

        public void SetWeather(WeatherType weather, bool announce)
        {
            Current = weather;
            float rainRate = RainRateFor(weather);
            _rainRate = rainRate;
            SetRain(rainRate > 0f, rainRate);

            // لایه‌ی گرافیک اگر نصب باشد، مه/نور/آسمان را به‌سمتِ این هوا می‌بَرَد؛ اگر نباشد
            // صحنه با همان تنظیماتِ پیش‌فرضِ موتور روشن می‌ماند (هیچ وابستگیِ اجباری‌ای ساخته نشده).
            SkyLightingRig rig = SkyLightingRig.Instance;
            if (rig != null) rig.NotifyWeather(weather, rainRate);

            WeatherChanged?.Invoke(weather);
            if (announce) GameEvents.Notify(Loc.Get("toast.weather_changed", GameText.WeatherName(weather)));
        }

        /// <summary>شدتِ بارانِ هر هوا (۰ تا ۱)؛ همان عددی که ذرات و شیدرها استفاده می‌کنند.</summary>
        private static float RainRateFor(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain: return 0.45f;
                case WeatherType.Storm: return 0.95f;
                default: return 0f;
            }
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

        /// <summary>شدتِ فعلیِ باران (برای EnvironmentFx و ذراتِ گام‌های بعدی).</summary>
        public float RainIntensity { get { return _rainRate; } }

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
