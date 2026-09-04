using UnityEngine;

namespace BaziBaqa
{
    public sealed class BuildingController : MonoBehaviour
    {
        public string Id { get; private set; }
        public BuildingType Type { get; private set; }
        public int Level { get; private set; }
        public float Health { get; private set; }
        public bool IsOperational { get { return Health > 0f && gameObject.activeSelf; } }
        public int DefensePower
        {
            get
            {
                switch (Type)
                {
                    case BuildingType.WatchTower: return 18 * Level;
                    case BuildingType.Wall: return 9 * Level;
                    case BuildingType.Camp: return 4 * Level;
                    default: return 0;
                }
            }
        }

        private float _productionTimer;
        private Renderer[] _renderers;

        public void Initialize(BuildingSaveData data)
        {
            Id = data.id;
            Type = data.type;
            Level = Mathf.Max(1, data.level);
            Health = data.health <= 0f ? MaxHealth : Mathf.Min(data.health, MaxHealth);
            _renderers = GetComponentsInChildren<Renderer>();
            UpdateVisuals();
        }

        public void Tick(float deltaTime)
        {
            if (!IsOperational) return;
            _productionTimer += deltaTime;
            if (_productionTimer < 8f) return;
            _productionTimer = 0f;

            ResourceSystem resources = GameManager.Instance.Resources;
            switch (Type)
            {
                case BuildingType.Camp:
                    if (GameManager.Instance.Technology.IsUnlocked(TechnologyType.WaterPurification) && resources.TrySpend(ResourceType.Energy, 1)) resources.Add(ResourceType.Water, 3 * Level, "water-purification");
                    break;
                case BuildingType.Farm:
                    int harvest = 2 * Level;
                    if (GameManager.Instance.Technology.IsUnlocked(TechnologyType.FieldRotation)) harvest += Level;
                    resources.Add(ResourceType.Food, harvest, "farm-production");
                    break;
                case BuildingType.SolarStation:
                    if (!GameManager.Instance.Clock.IsNight) resources.Add(ResourceType.Energy, 3 * Level, "solar-production");
                    break;
                case BuildingType.Workshop:
                    if (resources.TrySpend(ResourceType.Energy, 1)) resources.Add(ResourceType.Gold, 1, "workshop-production");
                    break;
            }
        }

        public void ApplyUpgrade()
        {
            Level = Mathf.Min(Level + 1, 5);
            Health = MaxHealth;
            UpdateVisuals();
        }

        public void TakeDamage(float amount)
        {
            if (!IsOperational) return;
            float damageMultiplier = GameManager.Instance.Technology != null && GameManager.Instance.Technology.IsUnlocked(TechnologyType.ReinforcedWalls) ? 0.72f : 1f;
            Health = Mathf.Max(0f, Health - Mathf.Max(0f, amount) * damageMultiplier);
            if (Health <= 0f)
            {
                gameObject.SetActive(false);
                GameEvents.Notify(Loc.Get("toast.building_destroyed", GameText.BuildingName(Type)));
            }
        }

        public BuildingSaveData ToSaveData()
        {
            return new BuildingSaveData
            {
                id = Id,
                type = Type,
                level = Level,
                health = Health,
                position = new SerializableVector3(transform.position)
            };
        }

        private float MaxHealth { get { return 100f + (Level - 1) * 45f; } }

        private void UpdateVisuals()
        {
            if (_renderers == null) return;
            float scale = 1f + (Level - 1) * 0.04f;
            transform.localScale = Vector3.one * scale;
        }
    }
}
