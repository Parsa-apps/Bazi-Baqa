using UnityEngine;

namespace BaziBaqa
{
    public sealed class SurvivorAgent : MonoBehaviour
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public SurvivorRole Role { get; private set; }
        public SurvivorState State { get; private set; }
        public float Health { get; private set; }
        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Morale { get; private set; }
        public bool IsAlive { get; private set; }
        public string TaskDescription { get; private set; }

        private SurvivorBrain _brain;
        private ResourceNode _targetNode;
        private float _decisionTimer;
        private float _workTimer;
        private float _needTimer;
        private Vector3 _patrolTarget;
        private TextMesh _nameLabel;

        public void Initialize(SurvivorSaveData data)
        {
            Id = data.id;
            DisplayName = string.IsNullOrEmpty(data.displayName) ? "بازمانده" : data.displayName;
            Role = data.role;
            _brain = new SurvivorBrain(Role);
            State = data.state;
            Health = Mathf.Clamp(data.health, 0f, 100f);
            Hunger = Mathf.Clamp(data.hunger, 0f, 100f);
            Thirst = Mathf.Clamp(data.thirst, 0f, 100f);
            Morale = Mathf.Clamp(data.morale, 0f, 100f);
            IsAlive = data.alive && Health > 0f;
            TaskDescription = "در حال آماده‌سازی";
            _nameLabel = GetComponentInChildren<TextMesh>();
            if (_nameLabel != null) _nameLabel.text = PersianText.Process(DisplayName);
            if (GameManager.Instance != null) GameManager.Instance.RegisterSurvivor(this);
            if (!IsAlive) Fall();
        }

        private void Update()
        {
            if (!IsAlive || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            float delta = Time.deltaTime;
            SimulateNeeds(delta);
            _decisionTimer -= delta;
            _needTimer -= delta;

            if (_needTimer <= 0f)
            {
                _needTimer = 2.5f;
                HandleNeeds();
            }
            if (_decisionTimer <= 0f) ChooseTask();
            ExecuteTask(delta);
        }

        public void NotifyDamage(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Max(0f, Health - Mathf.Max(0f, amount));
            Morale = Mathf.Max(0f, Morale - amount * 0.15f);
            if (Health <= 0f) Fall();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Min(100f, Health + Mathf.Max(0f, amount));
            State = SurvivorState.Healing;
            TaskDescription = "در حال درمان";
        }

        public SurvivorSaveData ToSaveData()
        {
            return new SurvivorSaveData
            {
                id = Id,
                displayName = DisplayName,
                role = Role,
                state = State,
                health = Health,
                hunger = Hunger,
                thirst = Thirst,
                morale = Morale,
                alive = IsAlive,
                position = new SerializableVector3(transform.position)
            };
        }

        private void SimulateNeeds(float delta)
        {
            float drainMultiplier = GameManager.Instance.Weather.IsSevere ? 1.18f : 1f;
            Hunger = Mathf.Max(0f, Hunger - delta * 0.55f * drainMultiplier);
            Thirst = Mathf.Max(0f, Thirst - delta * 0.8f * drainMultiplier);
            if (Hunger <= 0f || Thirst <= 0f)
            {
                Health = Mathf.Max(0f, Health - delta * 1.4f);
                Morale = Mathf.Max(0f, Morale - delta * 0.5f);
                if (Health <= 0f) Fall();
            }
            else
            {
                Morale = Mathf.Clamp(Morale + delta * 0.04f, 0f, 100f);
            }
        }

        private void HandleNeeds()
        {
            if (Hunger < 30f && GameManager.Instance.Resources.TrySpend(ResourceType.Food, 2))
            {
                Hunger = Mathf.Min(100f, Hunger + 38f);
                State = SurvivorState.Eating;
                TaskDescription = "در حال خوردن غذا";
                GameManager.Instance.Audio.PlayClick();
            }
            if (Thirst < 30f && GameManager.Instance.Resources.TrySpend(ResourceType.Water, 2))
            {
                Thirst = Mathf.Min(100f, Thirst + 45f);
                TaskDescription = "در حال نوشیدن آب";
            }
        }

        private void ChooseTask()
        {
            _decisionTimer = Random.Range(2.5f, 5.5f);
            if (Hunger < 25f || Thirst < 25f)
            {
                State = SurvivorState.Resting;
                TaskDescription = "در حال استراحت";
                _targetNode = null;
                return;
            }

            if (Role == SurvivorRole.Medic)
            {
                SurvivorAgent patient = GameManager.Instance.FindMostInjuredSurvivor(this);
                if (patient != null && Vector3.Distance(transform.position, patient.transform.position) > 2.2f)
                {
                    _patrolTarget = patient.transform.position;
                    State = SurvivorState.Healing;
                    TaskDescription = "کمک به زخمی‌ها";
                    return;
                }
                if (patient != null)
                {
                    patient.Heal(3.5f);
                    State = SurvivorState.Healing;
                    TaskDescription = "درمان یک هم‌گروهی";
                    return;
                }
            }

            if (Role == SurvivorRole.Guard)
            {
                _targetNode = null;
                State = SurvivorState.Guarding;
                TaskDescription = "گشت‌زنی اطراف اردوگاه";
                Vector3 home = GameManager.Instance.Construction.GetHomePosition();
                _patrolTarget = home + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-4f, 4f));
                return;
            }

            ResourceType resourceType = _brain == null ? ResourceType.Wood : _brain.PreferredResource;
            _targetNode = GameManager.Instance.World.FindNearestNode(resourceType, transform.position);
            if (_targetNode == null) _targetNode = GameManager.Instance.World.FindNearestNode(transform.position);
            if (_targetNode != null)
            {
                State = SurvivorState.Gathering;
                TaskDescription = "جمع‌آوری " + GameText.ResourceName(_targetNode.type);
            }
            else
            {
                State = SurvivorState.Idle;
                TaskDescription = "در انتظار فرمان";
            }
        }

        private void ExecuteTask(float delta)
        {
            switch (State)
            {
                case SurvivorState.Gathering:
                    if (_targetNode == null || _targetNode.IsDepleted)
                    {
                        _targetNode = null;
                        _decisionTimer = 0f;
                        return;
                    }
                    MoveTo(_targetNode.transform.position, delta, _brain == null ? 2.8f : _brain.MovementSpeed);
                    if (Vector3.Distance(transform.position, _targetNode.transform.position) < 1.65f)
                    {
                        _workTimer -= delta;
                        if (_workTimer <= 0f)
                        {
                            _workTimer = 1.2f;
                            int baseAmount = Role == SurvivorRole.Farmer ? 3 : 2;
                            int amount = _targetNode.Gather(Mathf.CeilToInt(baseAmount * (_brain == null ? 1f : _brain.GatheringMultiplier())));
                            if (amount > 0) GameManager.Instance.Resources.Add(_targetNode.type, amount, "جمع‌آوری توسط " + DisplayName);
                        }
                    }
                    break;
                case SurvivorState.Healing:
                    MoveTo(_patrolTarget, delta, 2.4f);
                    if (Vector3.Distance(transform.position, _patrolTarget) < 1.7f)
                    {
                        _decisionTimer = 0f;
                    }
                    break;
                case SurvivorState.Guarding:
                    MoveTo(_patrolTarget, delta, 2.1f);
                    if (Vector3.Distance(transform.position, _patrolTarget) < 1.2f) _decisionTimer = 0f;
                    AttackNearbyEnemy();
                    break;
                case SurvivorState.Resting:
                case SurvivorState.Eating:
                    MoveTo(GameManager.Instance.Construction.GetHomePosition(), delta, 2.2f);
                    if (Vector3.Distance(transform.position, GameManager.Instance.Construction.GetHomePosition()) < 2.3f) State = SurvivorState.Idle;
                    break;
                default:
                    if (_targetNode == null) MoveTo(GameManager.Instance.Construction.GetHomePosition(), delta, 1.4f);
                    break;
            }
        }

        private void AttackNearbyEnemy()
        {
            EnemyAgent enemy = GameManager.Instance.FindNearestEnemy(transform.position, 3.5f);
            if (enemy != null) enemy.TakeDamage(3.5f * Time.deltaTime);
        }

        private void MoveTo(Vector3 target, float delta, float speed)
        {
            if (GameManager.Instance.Technology != null && GameManager.Instance.Technology.IsUnlocked(TechnologyType.Cooperation)) speed *= 1.12f;
            target = GameManager.Instance.World.ClampToIsland(target);
            Vector3 next = Vector3.MoveTowards(transform.position, target, speed * delta);
            next.y = 0f;
            transform.position = next;
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), delta * 7f);
        }

        private void Fall()
        {
            IsAlive = false;
            State = SurvivorState.Injured;
            TaskDescription = "از دست رفته";
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            GameEvents.Notify(DisplayName + " دیگر توان ادامه ندارد.");
            GameManager.Instance.OnSurvivorLost(this);
        }
    }
}
