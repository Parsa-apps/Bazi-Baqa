using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BaziBaqa
{
    public sealed class ConstructionSystem : MonoBehaviour
    {
        private readonly List<BuildingController> _buildings = new List<BuildingController>();
        private BuildingType _placingType;
        private bool _placing;
        private GameObject _ghost;
        private Camera _camera;

        public IReadOnlyList<BuildingController> Buildings { get { return _buildings; } }
        public bool IsPlacing { get { return _placing; } }
        public BuildingType PlacingType { get { return _placingType; } }

        public void Initialize(List<BuildingSaveData> savedBuildings)
        {
            _camera = Camera.main;
            CancelPlacement();
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null) Destroy(_buildings[i].gameObject);
            }
            _buildings.Clear();

            if (savedBuildings == null || savedBuildings.Count == 0)
            {
                CreateFromSave(new BuildingSaveData
                {
                    id = "camp-main",
                    type = BuildingType.Camp,
                    level = 1,
                    health = 100f,
                    position = new SerializableVector3(Vector3.zero)
                });
                return;
            }

            for (int i = 0; i < savedBuildings.Count; i++) CreateFromSave(savedBuildings[i]);
        }

        public void SelectForPlacement(BuildingType type)
        {
            if (!GameManager.Instance.IsPlaying) return;
            CancelPlacement();
            _placingType = type;
            _placing = true;
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ghost.name = "نشانگر ساخت";
            _ghost.transform.localScale = new Vector3(1.7f, 0.04f, 1.5f);
            _ghost.transform.position = GameManager.Instance.World.ClampToIsland(Vector3.zero);
            Renderer renderer = _ghost.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateGhostMaterial();
            GameEvents.Notify("محل ساخت «" + GameText.BuildingName(type) + "» را روی زمین لمس کنید.");
        }

        public void CancelPlacement()
        {
            _placing = false;
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        public void Upgrade(BuildingController building)
        {
            if (building == null) return;
            List<ResourceCost> costs = GetUpgradeCosts(building.Type, building.Level);
            if (!GameManager.Instance.Resources.TrySpend(costs))
            {
                GameEvents.Notify("منابع کافی برای ارتقا وجود ندارد.");
                return;
            }
            building.ApplyUpgrade();
            GameEvents.Notify(GameText.BuildingName(building.Type) + " ارتقا یافت.");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(3, "ارتقای ساختمان");
            GameManager.Instance.SaveSoon();
        }

        public BuildingController FindNearest(Vector3 position, float maxDistance = 999f)
        {
            BuildingController result = null;
            float best = maxDistance * maxDistance;
            for (int i = 0; i < _buildings.Count; i++)
            {
                BuildingController building = _buildings[i];
                if (building == null || !building.IsOperational) continue;
                float distance = (building.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    result = building;
                }
            }
            return result;
        }

        public Vector3 GetHomePosition()
        {
            BuildingController camp = FindByType(BuildingType.Camp);
            return camp == null ? Vector3.zero : camp.transform.position;
        }

        public BuildingController FindByType(BuildingType type)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null && _buildings[i].Type == type && _buildings[i].IsOperational) return _buildings[i];
            }
            return null;
        }

        public int GetDefensePower()
        {
            int defense = 0;
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null && _buildings[i].IsOperational)
                {
                    defense += _buildings[i].DefensePower;
                }
            }
            return defense;
        }

        public List<BuildingSaveData> GetSaveData()
        {
            List<BuildingSaveData> result = new List<BuildingSaveData>();
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null && _buildings[i].IsOperational) result.Add(_buildings[i].ToSaveData());
            }
            return result;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            if (_placing && _ghost != null)
            {
                Vector3 position = GetPointerGroundPosition();
                _ghost.transform.position = position;
                if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1)) TryPlaceAt(position);
                if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended && !IsPointerOverUi(Input.GetTouch(0).fingerId)) TryPlaceAt(position);
            }

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null) _buildings[i].Tick(Time.deltaTime);
            }
        }

        private void TryPlaceAt(Vector3 position)
        {
            if (!CanPlace(position))
            {
                GameEvents.Notify("این محل برای ساخت مناسب نیست.");
                return;
            }

            List<ResourceCost> costs = GetBuildCosts(_placingType);
            if (!GameManager.Instance.Resources.TrySpend(costs))
            {
                GameEvents.Notify("منابع کافی برای ساخت وجود ندارد.");
                return;
            }

            BuildingSaveData data = new BuildingSaveData
            {
                id = Guid.NewGuid().ToString("N"),
                type = _placingType,
                level = 1,
                health = 100f,
                position = new SerializableVector3(position)
            };
            CreateFromSave(data);
            GameEvents.Notify(GameText.BuildingName(_placingType) + " ساخته شد.");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(4, "ساخت ساختمان");
            if (GameManager.Instance.Achievements != null) GameManager.Instance.Achievements.RegisterBuild();
            if (GameManager.Instance.Quests != null) GameManager.Instance.Quests.TryComplete();
            GameManager.Instance.Audio.PlayBuild();
            GameManager.Instance.SaveSoon();
            CancelPlacement();
        }

        private void CreateFromSave(BuildingSaveData data)
        {
            if (data == null) return;
            if (string.IsNullOrEmpty(data.id)) data.id = Guid.NewGuid().ToString("N");
            GameObject visual = GameManager.Instance.World.CreateBuildingVisual(data.type, data.position == null ? Vector3.zero : data.position.ToVector3(), Mathf.Max(1, data.level), GameManager.Instance.World.BuildingRoot);
            BuildingController controller = visual.AddComponent<BuildingController>();
            controller.Initialize(data);
            _buildings.Add(controller);
        }

        private bool CanPlace(Vector3 position)
        {
            position = GameManager.Instance.World.ClampToIsland(position);
            if (position.magnitude < 3f && _placingType != BuildingType.Camp) return false;
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i] != null && Vector3.Distance(_buildings[i].transform.position, position) < 3.1f) return false;
            }
            return true;
        }

        private Vector3 GetPointerGroundPosition()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return Vector3.zero;
            Vector2 pointer = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            Ray ray = _camera.ScreenPointToRay(pointer);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance)) return GameManager.Instance.World.ClampToIsland(ray.GetPoint(distance));
            return Vector3.zero;
        }

        public static List<ResourceCost> GetBuildCosts(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.House: return Costs(new ResourceCost(ResourceType.Wood, 35), new ResourceCost(ResourceType.Stone, 18));
                case BuildingType.Storage: return Costs(new ResourceCost(ResourceType.Wood, 48), new ResourceCost(ResourceType.Stone, 24));
                case BuildingType.Farm: return Costs(new ResourceCost(ResourceType.Wood, 32), new ResourceCost(ResourceType.Food, 8));
                case BuildingType.WatchTower: return Costs(new ResourceCost(ResourceType.Wood, 42), new ResourceCost(ResourceType.Stone, 36), new ResourceCost(ResourceType.Gold, 4));
                case BuildingType.Workshop: return Costs(new ResourceCost(ResourceType.Wood, 46), new ResourceCost(ResourceType.Stone, 30), new ResourceCost(ResourceType.Energy, 8));
                case BuildingType.Wall: return Costs(new ResourceCost(ResourceType.Wood, 22), new ResourceCost(ResourceType.Stone, 26));
                case BuildingType.SolarStation: return Costs(new ResourceCost(ResourceType.Stone, 28), new ResourceCost(ResourceType.Gold, 12));
                default: return Costs();
            }
        }

        public static List<ResourceCost> GetUpgradeCosts(BuildingType type, int currentLevel)
        {
            int multiplier = Mathf.Max(1, currentLevel);
            return Costs(new ResourceCost(ResourceType.Wood, 22 * multiplier), new ResourceCost(ResourceType.Stone, 16 * multiplier), new ResourceCost(ResourceType.Gold, 2 * multiplier));
        }

        private static List<ResourceCost> Costs(params ResourceCost[] costs)
        {
            return new List<ResourceCost>(costs);
        }

        private static Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("UI/Default");
            Material material = new Material(shader);
            material.color = new Color(0.2f, 0.85f, 0.7f, 0.48f);
            material.SetFloat("_Mode", 2f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;
            return material;
        }

        private static bool IsPointerOverUi(int fingerId)
        {
            if (EventSystem.current == null) return false;
            return fingerId < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
