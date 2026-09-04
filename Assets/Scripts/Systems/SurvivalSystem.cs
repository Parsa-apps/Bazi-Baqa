using UnityEngine;

namespace BaziBaqa
{
    public sealed class SurvivalSystem : MonoBehaviour
    {
        public float TeamMorale { get; private set; }
        public bool IsCritical { get; private set; }

        private float _checkTimer;
        private bool _warnedFood;
        private bool _warnedWater;

        public void Initialize()
        {
            _checkTimer = 4f;
            TeamMorale = 75f;
            IsCritical = false;
            _warnedFood = false;
            _warnedWater = false;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = 4f;

            int alive = GameManager.Instance.AliveSurvivorCount();
            if (alive <= 0) return;
            float totalHealth = 0f;
            float totalMorale = 0f;
            int count = 0;
            for (int i = 0; i < GameManager.Instance.Survivors.Count; i++)
            {
                SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                if (survivor == null || !survivor.IsAlive) continue;
                totalHealth += survivor.Health;
                totalMorale += survivor.Morale;
                count++;
            }
            TeamMorale = count == 0 ? 0f : totalMorale / count;
            IsCritical = totalHealth / Mathf.Max(1, count) < 35f || TeamMorale < 25f || GameManager.Instance.Resources.Get(ResourceType.Food) < alive * 2 || GameManager.Instance.Resources.Get(ResourceType.Water) < alive * 2;

            if (GameManager.Instance.Resources.Get(ResourceType.Food) < alive * 2)
            {
                if (!_warnedFood) GameEvents.Notify("ذخیره‌ی غذا کم است؛ جمع‌آور و کشاورز را فعال نگه دارید.");
                _warnedFood = true;
            }
            else _warnedFood = false;
            if (GameManager.Instance.Resources.Get(ResourceType.Water) < alive * 2)
            {
                if (!_warnedWater) GameEvents.Notify("آب گروه رو به پایان است؛ پیشاهنگ را به چشمه بفرستید.");
                _warnedWater = true;
            }
            else _warnedWater = false;
        }
    }
}
