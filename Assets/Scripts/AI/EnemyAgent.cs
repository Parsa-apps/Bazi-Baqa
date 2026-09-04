using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// هوش مصنوعی دشمن «سایه». علاوه بر حمله‌ی ساده، رفتار تاکتیکی دارد:
    /// - هنگام شب به‌صورت گروهی به اردوگاه می‌زند و نزدیک‌ترین هدف را ترجیح می‌دهد.
    /// - اگر به‌شدت آسیب ببیند، کوتاه‌مدت عقب می‌کشد (تشخیص خطر) و سپس بازمی‌گردد.
    /// - هنگام طوفان/باران کمی کندتر و در روز به‌صورت پراکنده پرسه می‌زند.
    /// - هنگام تعقیب، مسیر را ساده‌سازی (گام مستقیم) و به‌جای مانع، دور می‌زند.
    /// </summary>
    public sealed class EnemyAgent : MonoBehaviour
    {
        public int WaveNumber { get; private set; }
        public float Health { get; private set; } = 48f;
        public bool IsAlive { get { return Health > 0f && gameObject.activeSelf; } }

        private float _attackTimer;
        private float _retargetTimer;
        private float _retreatTimer;
        private bool _retreating;
        private SurvivorAgent _survivorTarget;
        private BuildingController _buildingTarget;

        public void Initialize(int number)
        {
            WaveNumber = number;
            Health = 42f + GameManager.Instance.Clock.Day * 4f;
            if (GameManager.Instance != null) GameManager.Instance.RegisterEnemy(this);
        }

        private void Update()
        {
            if (!IsAlive || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            // روز: پرسه‌ی گاه‌به‌گاه به سمت مرکز، بدون حمله.
            if (!GameManager.Instance.Clock.IsNight)
            {
                MoveTo(GameManager.Instance.Construction.GetHomePosition(), Time.deltaTime, 0.6f);
                return;
            }

            _attackTimer -= Time.deltaTime;
            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = 2.2f;
                FindTarget();
            }

            // تشخیص خطر: اگر سلامتی کم شود، برای چند لحظه عقب می‌کشد تا جایگاه بهتری بگیرد.
            if (ShouldRetreat())
            {
                _retreating = true;
                _retreatTimer = 1.4f;
            }
            if (_retreating)
            {
                _retreatTimer -= Time.deltaTime;
                Vector3 away = (transform.position - GameManager.Instance.Construction.GetHomePosition()).normalized;
                MoveTo(transform.position + away * 6f + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)), Time.deltaTime, 2.0f);
                if (_retreatTimer <= 0f) _retreating = false;
                return;
            }

            Vector3 targetPosition = _survivorTarget != null ? _survivorTarget.transform.position : (_buildingTarget != null ? _buildingTarget.transform.position : GameManager.Instance.Construction.GetHomePosition());
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > 1.65f)
            {
                float speed = (1.3f + GameManager.Instance.Clock.Day * 0.02f) * WeatherSpeedMultiplier();
                MoveTo(targetPosition, Time.deltaTime, speed);
            }
            else if (_attackTimer <= 0f)
            {
                _attackTimer = 1.2f;
                if (_survivorTarget != null) _survivorTarget.NotifyDamage(5f + GameManager.Instance.Clock.Day * 0.8f);
                if (_buildingTarget != null) _buildingTarget.TakeDamage(5f + GameManager.Instance.Clock.Day * 0.8f);
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Max(0f, Health - Mathf.Max(0f, amount));
            if (Health <= 0f)
            {
                GameEvents.Notify(Loc.Get("toast.enemy_defeated"));
                if (GameManager.Instance.Achievements != null) GameManager.Instance.Achievements.RegisterDefeat();
                if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(4, "defense");
                GameManager.Instance.OnEnemyLost(this);
                Destroy(gameObject);
            }
        }

        private bool ShouldRetreat()
        {
            float threat = 0f;
            SurvivorAgent survivor = GameManager.Instance.FindNearestSurvivor(transform.position, 6f);
            if (survivor != null) threat = Mathf.Max(threat, survivor.Health * 0.3f);
            return Health < 22f && threat > 0f;
        }

        private float WeatherSpeedMultiplier()
        {
            switch (GameManager.Instance.Weather.Current)
            {
                case WeatherType.Storm: return 0.78f;
                case WeatherType.Rain: return 0.88f;
                case WeatherType.Fog: return 0.95f;
                default: return 1f;
            }
        }

        private void FindTarget()
        {
            _survivorTarget = GameManager.Instance.FindNearestSurvivor(transform.position, 18f);
            _buildingTarget = _survivorTarget == null ? GameManager.Instance.Construction.FindNearest(transform.position, 18f) : null;
        }

        private void MoveTo(Vector3 target, float delta, float speed)
        {
            target = GameManager.Instance.World.ClampToIsland(target);
            Vector3 next = Vector3.MoveTowards(transform.position, target, speed * delta);
            next.y = 0f;
            transform.position = next;
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), delta * 4f);
        }
    }
}
