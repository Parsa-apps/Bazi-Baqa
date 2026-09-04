using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
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
            int count = Mathf.Clamp(1 + day / 2, 1, 6);
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 position = new Vector3(side * (WorldGenerator.WorldWidth * 0.39f - i), 0f, Random.Range(-WorldGenerator.WorldDepth * 0.35f, WorldGenerator.WorldDepth * 0.35f));
                GameManager.Instance.World.CreateEnemyVisual(position, i + 1);
            }
            GameEvents.Notify("موج شبانه رسید؛ نگهبان‌ها را آماده کنید.");
            GameManager.Instance.Audio.PlayAlert();
        }
    }
}
