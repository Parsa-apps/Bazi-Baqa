using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// مدیر حملات شب. حمله‌ها به‌صورت موج‌های هماهنگ از یک سمت برای احساس «محاصره» انجام می‌شود
    /// و با هر روز تعداد و توان آن‌ها افزایش می‌یابد. دشمنان وارد شونده در یک سمت مشترک ظاهر می‌شوند
    /// تا حس حمله‌ی گروهی را القا کنند.
    /// </summary>
    public sealed class EnemyDirector : MonoBehaviour
    {
        private readonly List<EnemyAgent> _enemies = new List<EnemyAgent>();
        private bool _waveStarted;

        public IReadOnlyList<EnemyAgent> Enemies { get { return _enemies; } }

        public void Initialize()
        {
            _waveStarted = false;
            if (GameManager.Instance != null) GameManager.Instance.Clock.NightChanged += OnNightChanged;
        }

        public void Clear()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null) Destroy(_enemies[i].gameObject);
            }
            _enemies.Clear();
            _waveStarted = false;
        }

        public void Register(EnemyAgent enemy)
        {
            if (enemy != null && !_enemies.Contains(enemy)) _enemies.Add(enemy);
        }

        public void Unregister(EnemyAgent enemy)
        {
            _enemies.Remove(enemy);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.Clock.NightChanged -= OnNightChanged;
        }

        private void OnNightChanged(bool night)
        {
            if (!night) return;
            _waveStarted = false;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || !GameManager.Instance.Clock.IsNight) return;
            if (_waveStarted) return;
            _waveStarted = true;
            SpawnWave(GameManager.Instance.Clock.Day);
        }

        private void SpawnWave(int day)
        {
            // تعداد با روز، اما محدود به بودجه‌ی دستگاه‌های متوسط.
            int count = Mathf.Clamp(2 + day, 3, 12);

            // یک سمت حمله به‌صورت تصادفی ساده انتخاب می‌شود تا حس محاصره گروهی ایجاد شود.
            bool fromEast = Random.value > 0.5f;
            float side = fromEast ? 1f : -1f;

            for (int i = 0; i < count; i++)
            {
                float z = Random.Range(-WorldGenerator.WorldDepth * 0.34f, WorldGenerator.WorldDepth * 0.34f);
                float x = side * (WorldGenerator.WorldWidth * 0.42f - i * 0.5f);
                Vector3 position = new Vector3(x, 0f, z);
                GameManager.Instance.World.CreateEnemyVisual(position, i + 1);
            }
            GameEvents.Notify(Loc.Get("toast.night_wave", Loc.Num(count), Loc.Get(fromEast ? "label.east" : "label.west")));
            GameManager.Instance.Audio.PlayAlert();
        }
    }
}
