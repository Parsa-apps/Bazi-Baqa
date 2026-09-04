using UnityEngine;

namespace BaziBaqa
{
    public sealed class EnemyAgent : MonoBehaviour
    {
        public int WaveNumber { get; private set; }
        public float Health { get; private set; } = 48f;
        public bool IsAlive { get { return Health > 0f && gameObject.activeSelf; } }

        private float _attackTimer;
        private float _retargetTimer;
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
            if (!GameManager.Instance.Clock.IsNight)
            {
                MoveTo(GameManager.Instance.Construction.GetHomePosition(), Time.deltaTime, 0.9f);
                return;
            }

            _attackTimer -= Time.deltaTime;
            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = 2.2f;
                FindTarget();
            }

            Vector3 targetPosition = _survivorTarget != null ? _survivorTarget.transform.position : (_buildingTarget != null ? _buildingTarget.transform.position : GameManager.Instance.Construction.GetHomePosition());
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > 1.65f)
            {
                MoveTo(targetPosition, Time.deltaTime, 1.3f + GameManager.Instance.Clock.Day * 0.02f);
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
                GameEvents.Notify("یک سایه شکست خورد.");
                if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(4, "دفاع از اردوگاه");
                GameManager.Instance.OnEnemyLost(this);
                Destroy(gameObject);
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
