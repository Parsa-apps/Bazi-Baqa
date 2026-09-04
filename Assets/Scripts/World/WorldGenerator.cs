using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>تولید جهان؛ پیش از سامانه‌های وابسته به دنیا ساخته می‌شود.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class WorldGenerator : MonoBehaviour
    {
        public const float WorldWidth = 64f;
        public const float WorldDepth = 44f;

        public Transform WorldRoot { get; private set; }
        public Transform TerrainRoot { get; private set; }
        public Transform ResourceRoot { get; private set; }
        public Transform ActorRoot { get; private set; }
        public Transform BuildingRoot { get; private set; }
        public Transform EffectRoot { get; private set; }
        public IReadOnlyList<ResourceNode> ResourceNodes { get { return _resourceNodes; } }

        private readonly List<ResourceNode> _resourceNodes = new List<ResourceNode>();
        private readonly List<GameObject> _generatedObjects = new List<GameObject>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private AmbientLife _ambientLife;
        private WorldVFX _worldVfx;
        private System.Random _random;
        private Material _groundMaterial;
        private Material _waterMaterial;
        private Material _treeMaterial;
        private Material _leafMaterial;
        private Material _rockMaterial;
        private Font _worldFont;

        public void Initialize()
        {
            WorldRoot = CreateRoot(WorldParts.WorldRoot);
            TerrainRoot = CreateRoot(WorldParts.TerrainRoot, WorldRoot);
            ResourceRoot = CreateRoot(WorldParts.ResourceRoot, WorldRoot);
            ActorRoot = CreateRoot(WorldParts.ActorRoot, WorldRoot);
            BuildingRoot = CreateRoot(WorldParts.BuildingRoot, WorldRoot);
            EffectRoot = CreateRoot(WorldParts.EffectRoot, WorldRoot);
            _worldFont = GameFont.Persian;
            ConfigureMaterials();
            ConfigureEnvironment();
            _ambientLife = WorldRoot.gameObject.AddComponent<AmbientLife>();
            _ambientLife.Initialize();
            _worldVfx = WorldRoot.gameObject.AddComponent<WorldVFX>();
        }

        public void Generate(int seed)
        {
            if (WorldRoot == null) Initialize();
            ClearGeneratedWorld();
            _resourceNodes.Clear();
            _random = new System.Random(seed);
            CreateTerrain();
            CreateResourceNodes();
            CreateNaturalProps();
            if (_worldVfx != null) _worldVfx.Initialize(Vector3.zero);
        }

        public void ClearGeneratedWorld()
        {
            // ریشه‌ها در تمام عمر صحنه باقی می‌مانند؛ فقط محتوای جهان قبلی حذف می‌شود.
            ClearChildren(TerrainRoot);
            ClearChildren(ResourceRoot);
            ClearChildren(ActorRoot);
            ClearChildren(BuildingRoot);
            ClearChildren(EffectRoot);
            if (_ambientLife != null) _ambientLife.Clear();
            if (_worldVfx != null) _worldVfx.Clear();
            _resourceNodes.Clear();
            _generatedObjects.Clear();
        }

        public ResourceNode FindNearestNode(ResourceType type, Vector3 from)
        {
            ResourceNode result = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _resourceNodes.Count; i++)
            {
                ResourceNode node = _resourceNodes[i];
                if (node == null || node.IsDepleted || node.type != type) continue;
                float distance = (node.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = node;
                }
            }
            return result;
        }

        public ResourceNode FindNearestNode(Vector3 from)
        {
            ResourceNode result = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _resourceNodes.Count; i++)
            {
                ResourceNode node = _resourceNodes[i];
                if (node == null || node.IsDepleted) continue;
                float distance = (node.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = node;
                }
            }
            return result;
        }

        public Vector3 ClampToIsland(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -WorldWidth * 0.47f, WorldWidth * 0.47f);
            position.z = Mathf.Clamp(position.z, -WorldDepth * 0.43f, WorldDepth * 0.43f);
            position.y = 0f;
            return position;
        }

        public GameObject CreateBuildingVisual(BuildingType type, Vector3 position, int level, Transform parent)
        {
            GameObject root = new GameObject(GameText.BuildingName(type)); // نام ریشه از بومی‌سازی می‌آید (فقط در هیرارشی دیده می‌شود)
            root.transform.SetParent(parent == null ? BuildingRoot : parent, false);
            root.transform.position = ClampToIsland(position);
            root.transform.localScale = Vector3.one * (1f + (level - 1) * 0.08f);

            Color main = BuildingColor(type);
            GameObject body = CreatePrimitive(PrimitiveType.Cube, root.transform, WorldParts.BuildingBody, root.transform.position + Vector3.up * 0.65f);
            body.transform.localScale = new Vector3(type == BuildingType.Wall ? 3.5f : 2.4f, 1.3f, type == BuildingType.Wall ? 0.45f : 2.1f);
            SetMaterial(body, CreateMaterial(main, 0.1f));

            if (type == BuildingType.Camp || type == BuildingType.House)
            {
                GameObject roof = CreatePrimitive(PrimitiveType.Cylinder, root.transform, WorldParts.BuildingRoof, root.transform.position + Vector3.up * 1.55f);
                roof.transform.localScale = new Vector3(1.4f, 0.35f, 1.2f);
                SetMaterial(roof, CreateMaterial(new Color(0.2f, 0.12f, 0.1f), 0.05f));
            }
            else if (type == BuildingType.WatchTower)
            {
                body.transform.localScale = new Vector3(0.85f, 2.7f, 0.85f);
                body.transform.position = root.transform.position + Vector3.up * 1.35f;
                GameObject beacon = CreatePrimitive(PrimitiveType.Sphere, root.transform, WorldParts.BuildingBeacon, root.transform.position + Vector3.up * 3f);
                beacon.transform.localScale = Vector3.one * 0.45f;
                SetMaterial(beacon, CreateMaterial(new Color(1f, 0.72f, 0.18f), 0.8f));
            }
            else if (type == BuildingType.SolarStation)
            {
                body.transform.localScale = new Vector3(2.8f, 0.3f, 1.8f);
                body.transform.position = root.transform.position + Vector3.up * 0.35f;
                body.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                SetMaterial(body, CreateMaterial(new Color(0.08f, 0.22f, 0.35f), 0.45f));
                GameObject mast = CreatePrimitive(PrimitiveType.Cylinder, root.transform, WorldParts.BuildingMast, root.transform.position + Vector3.up * 1f);
                mast.transform.localScale = new Vector3(0.08f, 0.9f, 0.08f);
                SetMaterial(mast, CreateMaterial(new Color(0.62f, 0.67f, 0.72f), 0.25f));
            }
            else if (type == BuildingType.Farm)
            {
                body.transform.localScale = new Vector3(3.2f, 0.16f, 2.5f);
                body.transform.position = root.transform.position + Vector3.up * 0.12f;
                SetMaterial(body, CreateMaterial(new Color(0.26f, 0.42f, 0.16f), 0f));
                for (int i = 0; i < 5; i++)
                {
                    GameObject crop = CreatePrimitive(PrimitiveType.Capsule, root.transform, WorldParts.BuildingCrop, root.transform.position + new Vector3(-1.1f + i * 0.55f, 0.38f, 0f));
                    crop.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
                    SetMaterial(crop, CreateMaterial(new Color(0.55f, 0.74f, 0.2f), 0.05f));
                }
            }
            else if (type == BuildingType.Workshop)
            {
                GameObject chimney = CreatePrimitive(PrimitiveType.Cylinder, root.transform, WorldParts.BuildingChimney, root.transform.position + new Vector3(0.7f, 1.5f, 0.4f));
                chimney.transform.localScale = new Vector3(0.25f, 0.7f, 0.25f);
                SetMaterial(chimney, CreateMaterial(new Color(0.16f, 0.17f, 0.19f), 0.05f));
            }

            TextMesh label = CreateWorldLabel(GameText.BuildingLabel(type, level), root.transform, root.transform.position + Vector3.up * 2.7f);
            label.color = Color.white;
            _generatedObjects.Add(root);
            return root;
        }

        public GameObject CreateSurvivorVisual(SurvivorSaveData data)
        {
            GameObject root = new GameObject(data.displayName);
            root.transform.SetParent(ActorRoot, false);
            root.transform.position = ClampToIsland(data.position == null ? Vector3.zero : data.position.ToVector3());

            GameObject body = CreatePrimitive(PrimitiveType.Capsule, root.transform, WorldParts.ActorTorso, root.transform.position + Vector3.up * 0.9f);
            body.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            SetMaterial(body, CreateMaterial(RoleColor(data.role), 0.05f));
            GameObject backpack = CreatePrimitive(PrimitiveType.Cube, root.transform, WorldParts.ActorBackpack, root.transform.position + new Vector3(0f, 0.78f, -0.36f));
            backpack.transform.localScale = new Vector3(0.32f, 0.4f, 0.18f);
            SetMaterial(backpack, CreateMaterial(new Color(0.12f, 0.16f, 0.2f), 0.05f));

            CreateWorldLabel(data.displayName, root.transform, root.transform.position + Vector3.up * 2.05f).color = Color.white;
            SurvivorAgent agent = root.AddComponent<SurvivorAgent>();
            agent.Initialize(data);
            _generatedObjects.Add(root);
            return root;
        }

        public GameObject CreateEnemyVisual(Vector3 position, int number)
        {
            GameObject root = new GameObject(WorldParts.Enemy + number);
            root.transform.SetParent(ActorRoot, false);
            root.transform.position = ClampToIsland(position);
            GameObject body = CreatePrimitive(PrimitiveType.Capsule, root.transform, WorldParts.ActorTorso, root.transform.position + Vector3.up * 0.75f);
            body.transform.localScale = new Vector3(0.6f, 0.75f, 0.6f);
            SetMaterial(body, CreateMaterial(new Color(0.38f, 0.04f, 0.08f), 0.15f));
            GameObject eye = CreatePrimitive(PrimitiveType.Sphere, root.transform, WorldParts.EnemyEye, root.transform.position + new Vector3(0f, 1.2f, 0.45f));
            eye.transform.localScale = Vector3.one * 0.16f;
            SetMaterial(eye, CreateMaterial(new Color(1f, 0.15f, 0.08f), 0.9f));
            EnemyAgent enemy = root.AddComponent<EnemyAgent>();
            enemy.Initialize(number);
            _generatedObjects.Add(root);
            return root;
        }

        private void CreateTerrain()
        {
            int xSegments = 32;
            int zSegments = 22;
            Mesh mesh = new Mesh { name = WorldParts.GroundMesh };
            Vector3[] vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[xSegments * zSegments * 6];

            for (int z = 0; z <= zSegments; z++)
            {
                for (int x = 0; x <= xSegments; x++)
                {
                    int index = z * (xSegments + 1) + x;
                    float px = Mathf.Lerp(-WorldWidth * 0.5f, WorldWidth * 0.5f, x / (float)xSegments);
                    float pz = Mathf.Lerp(-WorldDepth * 0.5f, WorldDepth * 0.5f, z / (float)zSegments);
                    float edge = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(px) / (WorldWidth * 0.5f), Mathf.Abs(pz) / (WorldDepth * 0.5f)));
                    float noise = Mathf.PerlinNoise((px + 70f) * 0.08f + (_random.NextDouble() > 0.5 ? 1f : 0f), (pz + 40f) * 0.08f) * 0.8f;
                    vertices[index] = new Vector3(px, Mathf.Max(0f, edge * 1.9f + noise - 0.7f), pz);
                    uv[index] = new Vector2(x / (float)xSegments, z / (float)zSegments);
                }
            }

            int triangle = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int root = z * (xSegments + 1) + x;
                    triangles[triangle++] = root;
                    triangles[triangle++] = root + xSegments + 1;
                    triangles[triangle++] = root + 1;
                    triangles[triangle++] = root + 1;
                    triangles[triangle++] = root + xSegments + 1;
                    triangles[triangle++] = root + xSegments + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            GameObject ground = new GameObject(WorldParts.Ground);
            ground.transform.SetParent(TerrainRoot, false);
            MeshFilter filter = ground.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _groundMaterial;
            MeshCollider collider = ground.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            _generatedObjects.Add(ground);

            GameObject water = CreatePrimitive(PrimitiveType.Cube, TerrainRoot, WorldParts.Water, new Vector3(0f, -1.25f, 0f));
            water.transform.localScale = new Vector3(WorldWidth * 1.7f, 1.5f, WorldDepth * 1.8f);
            SetMaterial(water, _waterMaterial);
            _generatedObjects.Add(water);
        }

        private void CreateResourceNodes()
        {
            for (int i = 0; i < 14; i++)
            {
                Vector3 position = RandomIslandPosition(7f);
                CreateResourceNode(ResourceType.Wood, position, 55 + _random.Next(0, 30), i);
            }
            for (int i = 0; i < 10; i++)
            {
                Vector3 position = RandomIslandPosition(6f);
                CreateResourceNode(ResourceType.Stone, position, 45 + _random.Next(0, 26), 20 + i);
            }
            for (int i = 0; i < 8; i++)
            {
                Vector3 position = RandomIslandPosition(5f);
                CreateResourceNode(ResourceType.Food, position, 36 + _random.Next(0, 25), 40 + i);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = RandomIslandPosition(8f);
                CreateResourceNode(ResourceType.Water, position, 42 + _random.Next(0, 22), 60 + i);
            }
        }

        private void CreateResourceNode(ResourceType type, Vector3 position, int amount, int index)
        {
            PrimitiveType primitive = type == ResourceType.Stone ? PrimitiveType.Cube : PrimitiveType.Cylinder;
            GameObject nodeObject = CreatePrimitive(primitive, ResourceRoot, WorldParts.ResourceNode + type + "_" + index, position + Vector3.up * 0.45f);
            if (type == ResourceType.Wood)
            {
                nodeObject.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f);
                SetMaterial(nodeObject, _treeMaterial);
                GameObject leaves = CreatePrimitive(PrimitiveType.Sphere, nodeObject.transform, WorldParts.TreeCanopy, nodeObject.transform.position + Vector3.up * 0.85f);
                leaves.transform.localScale = Vector3.one * 1.4f;
                SetMaterial(leaves, _leafMaterial);
            }
            else if (type == ResourceType.Stone)
            {
                nodeObject.transform.localScale = new Vector3(1.1f, 0.8f, 0.9f);
                SetMaterial(nodeObject, _rockMaterial);
            }
            else if (type == ResourceType.Food)
            {
                nodeObject.transform.localScale = new Vector3(0.85f, 0.35f, 0.85f);
                SetMaterial(nodeObject, CreateMaterial(new Color(0.56f, 0.25f, 0.08f), 0.03f));
                for (int i = 0; i < 3; i++)
                {
                    GameObject berry = CreatePrimitive(PrimitiveType.Sphere, nodeObject.transform, WorldParts.Berry, nodeObject.transform.position + new Vector3(-0.35f + i * 0.35f, 0.45f, 0f));
                    berry.transform.localScale = Vector3.one * 0.22f;
                    SetMaterial(berry, CreateMaterial(new Color(0.8f, 0.15f, 0.2f), 0.05f));
                }
            }
            else
            {
                nodeObject.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
                SetMaterial(nodeObject, CreateMaterial(new Color(0.08f, 0.38f, 0.7f), 0.25f));
            }

            ResourceNode node = nodeObject.AddComponent<ResourceNode>();
            node.Initialize(type, amount);
            _resourceNodes.Add(node);
            _generatedObjects.Add(nodeObject);
            if (type == ResourceType.Wood && _ambientLife != null) _ambientLife.RegisterTree(nodeObject.transform);
        }

        private void CreateNaturalProps()
        {
            for (int i = 0; i < 24; i++)
            {
                Vector3 position = RandomIslandPosition(3f);
                if (_random.NextDouble() > 0.35)
                {
                    GameObject tree = CreatePrimitive(PrimitiveType.Cylinder, TerrainRoot, WorldParts.Tree, position + Vector3.up * 0.8f);
                    tree.transform.localScale = new Vector3(0.22f, 1.25f, 0.22f);
                    SetMaterial(tree, _treeMaterial);
                    GameObject leaves = CreatePrimitive(PrimitiveType.Sphere, tree.transform, WorldParts.TreeCanopy, tree.transform.position + Vector3.up * 1.25f);
                    leaves.transform.localScale = Vector3.one * 1.1f;
                    SetMaterial(leaves, _leafMaterial);
                    _generatedObjects.Add(tree);
                    if (_ambientLife != null) _ambientLife.RegisterTree(tree.transform);
                }
                else
                {
                    GameObject rock = CreatePrimitive(PrimitiveType.Sphere, TerrainRoot, WorldParts.Rock, position + Vector3.up * 0.22f);
                    rock.transform.localScale = new Vector3(0.7f, 0.35f, 0.55f);
                    SetMaterial(rock, _rockMaterial);
                    _generatedObjects.Add(rock);
                }
            }
        }

        private Vector3 RandomIslandPosition(float safeRadius)
        {
            float x = Mathf.Lerp(-WorldWidth * 0.43f, WorldWidth * 0.43f, (float)_random.NextDouble());
            float z = Mathf.Lerp(-WorldDepth * 0.38f, WorldDepth * 0.38f, (float)_random.NextDouble());
            Vector3 result = new Vector3(x, 0f, z);
            if (result.magnitude < safeRadius) result += new Vector3(safeRadius + 2f, 0f, safeRadius + 1f);
            return ClampToIsland(result);
        }

        /// <summary>
        /// نورِ اصلیِ جهان این‌جا ساخته می‌شود (چون به WorldRoot وصل است و با Clear پاک می‌شود)،
        /// اما رنگ/شدت/مه/آسمان را SkyLightingRig می‌نویسد؛ این‌جا فقط مقادیرِ اولیه‌ی امن گذاشته
        /// می‌شود تا اگر لایه‌ی گرافیک نصب نبود، صحنه تاریک نماند.
        /// </summary>
        private void ConfigureEnvironment()
        {
            GameObject lightObject = new GameObject(WorldParts.SunLight);
            lightObject.transform.SetParent(WorldRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.88f, 0.7f);
            _generatedObjects.Add(lightObject);
        }

        private void ConfigureMaterials()
        {
            _groundMaterial = CreateMaterial(new Color(0.21f, 0.34f, 0.2f), 0f);
            _waterMaterial = CreateMaterial(new Color(0.03f, 0.2f, 0.32f), 0.35f);
            _treeMaterial = CreateMaterial(new Color(0.25f, 0.14f, 0.07f), 0.02f);
            _leafMaterial = CreateMaterial(new Color(0.1f, 0.35f, 0.16f), 0.08f);
            _rockMaterial = CreateMaterial(new Color(0.3f, 0.34f, 0.35f), 0.16f);
        }

        private Transform CreateRoot(string name, Transform parent = null)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            _generatedObjects.Add(root);
            return root.transform;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }

        private static GameObject CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 position)
        {
            GameObject objectInstance = GameObject.CreatePrimitive(type);
            objectInstance.name = name;
            objectInstance.transform.SetParent(parent, true);
            objectInstance.transform.position = position;
            return objectInstance;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        /// <summary>
        /// متریالِ رنگیِ جهان. از فاز ۳ به بعد دیگر `Shader.Find(«Standard»)» نیست:
        /// MaterialLibrary شیدر را بین URP/built-in حل می‌کند و متریال‌ها را مالکیت دارد،
        /// پس با تعویض خطِ رندر هیچ سطحی ارغوانی نمی‌شود و متریالِ تکراری ساخته نمی‌شود.
        /// </summary>
        private Material CreateMaterial(Color color, float metallic)
        {
            Material material = MaterialLibrary.Tinted(color, metallic);
            return material;
        }

        private TextMesh CreateWorldLabel(string value, Transform parent, Vector3 position)
        {
            GameObject labelObject = new GameObject(WorldParts.Label);
            labelObject.transform.SetParent(parent, true);
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            TextMesh textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = PersianText.Process(value);
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.08f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.font = _worldFont;
            return textMesh;
        }

        private static Color BuildingColor(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Camp: return new Color(0.75f, 0.4f, 0.18f);
                case BuildingType.House: return new Color(0.58f, 0.32f, 0.2f);
                case BuildingType.Storage: return new Color(0.25f, 0.44f, 0.56f);
                case BuildingType.Farm: return new Color(0.35f, 0.56f, 0.2f);
                case BuildingType.WatchTower: return new Color(0.38f, 0.4f, 0.44f);
                case BuildingType.Workshop: return new Color(0.5f, 0.27f, 0.2f);
                case BuildingType.Wall: return new Color(0.34f, 0.36f, 0.4f);
                case BuildingType.SolarStation: return new Color(0.16f, 0.38f, 0.55f);
                default: return Color.white;
            }
        }

        private void OnDestroy()
        {
            // متریال‌ها در MaterialLibrary زندگی می‌کنند (بینِ بازسازیِ جهان ها قابل‌استفاده‌ی دوباره‌اند)؛
            // این‌جا فقط فهرستِ محلی پاک می‌شود تا چیزی نابود نشود که دیگری به آن ارجاع دارد.
            _materials.Clear();
        }

        private static Color RoleColor(SurvivorRole role)
        {
            switch (role)
            {
                case SurvivorRole.Gatherer: return new Color(0.25f, 0.73f, 0.7f);
                case SurvivorRole.Builder: return new Color(0.95f, 0.64f, 0.22f);
                case SurvivorRole.Medic: return new Color(0.86f, 0.4f, 0.52f);
                case SurvivorRole.Guard: return new Color(0.56f, 0.36f, 0.83f);
                case SurvivorRole.Scout: return new Color(0.26f, 0.61f, 0.88f);
                case SurvivorRole.Farmer: return new Color(0.46f, 0.78f, 0.24f);
                default: return Color.white;
            }
        }
    }
}
