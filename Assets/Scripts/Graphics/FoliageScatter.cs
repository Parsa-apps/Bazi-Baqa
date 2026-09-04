using System;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// بوته‌های چمنِ رویه‌ای: صدها بوته در **یک** mesh ترکیب می‌شوند (یک draw call)، بدونِ
    /// هیچ Asset، بدونِ Update روی تک‌تکِ آن‌ها و بدونِ دست‌زدن به منطقِ بازی.
    ///
    /// دو قانونِ ایمنی که در این فایل رعایت شده:
    /// ۱) هیچ‌وقت از <c>UnityEngine.Random</c> استفاده نمی‌کنیم (همانِ Globalِ Shared با
    ///    WorldGenerator است؛ یک تماسِ تازه، جای منابع/دشمن‌ها را عوض می‌کرد). بذرِ خودمان را
    ///    از <c>System.Random</c> می‌گیریم ⇒ چیدمانِ gameplay مو‌به‌مو ثابت می‌ماند.
    /// ۲) سطحِ زمین را با Raycast می‌خوانیم و هیچ گره‌ی تازه‌ای به داده‌های بازی نمی‌دهیم؛
    ///    اگر Ground نبود، بوته‌ای ساخته نمی‌شود (هیچ NullReference در بازی ساخته نمی‌شود).
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class FoliageScatter : MonoBehaviour
    {
        public static FoliageScatter Instance { get; private set; }

        private const int VertsPerTuft = 8;         // دو مربعِ متقاطع
        private const int TrisPerTuft = 12;
        private const float TuftHeight = 0.62f;
        private const float TuftWidth = 0.54f;
        private const int MaxTufts = 4096;
        private const string ScatterName = "Grass Scatter";

        [SerializeField] private float minGroundHeight = 0.35f;
        [SerializeField] private float maxGroundHeight = 2.4f;
        [SerializeField] private float campClearance = 8.5f;
        [SerializeField] private float rayHeight = 34f;
        [SerializeField] private int seed = 20250915;

        private GameObject _host;
        private Mesh _mesh;
        private int _tuftCount;
        private int _lastTerrainId = -1;
        private int _lastBudget = -1;
        private float _retryTimer;

        public int TuftCount { get { return _tuftCount; } }
        public int VertexCount { get { return _mesh != null ? _mesh.vertexCount : 0; } }
        public bool HasMesh { get { return _mesh != null; } }
        public int DrawCalls { get { return _host != null ? 1 : 0; } }

        public string Report()
        {
            return "foliage tufts=" + _tuftCount + " in " + DrawCalls + " draw call"
                + (_host != null ? string.Empty : " (idle)")
                + " budget=" + _lastBudget;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            _retryTimer = 0f;
        }

        private void OnDisable()
        {
            Clear();
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Update()
        {
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer > 0f) return;
            _retryTimer = 0.5f;

            WorldGenerator world = GameManager.Instance != null ? GameManager.Instance.World : null;
            Transform terrain = world != null ? world.TerrainRoot : null;
            if (terrain == null)
            {
                if (_tuftCount > 0) Clear();
                return;
            }

            int budget = CurrentBudget();
            int terrainId = terrain.GetInstanceID();
            if (_host != null && terrainId == _lastTerrainId && budget == _lastBudget) return;

            Rebuild(terrain, budget);
        }

        /// <summary>بازسازیِ فوری (پس از نصبِ لایه‌ی گرافیک یا تغییرِ کیفیت).</summary>
        public void Refresh()
        {
            _retryTimer = 0f;
            Update();
        }

        private int CurrentBudget()
        {
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().Current;
            return Mathf.Clamp(tier.foliageCount, 0, MaxTufts);
        }

        private void Clear()
        {
            if (_host != null)
            {
                Destroy(_host);
                _host = null;
            }
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
            _tuftCount = 0;
            _lastTerrainId = -1;
        }

        private void Rebuild(Transform terrain, int budget)
        {
            Clear();
            _lastTerrainId = terrain.GetInstanceID();
            _lastBudget = budget;
            if (budget <= 0) return;

            Vector3[] vertices = new Vector3[budget * VertsPerTuft];
            Color32[] colors = new Color32[budget * VertsPerTuft];
            Vector2[] uv = new Vector2[budget * VertsPerTuft];
            int[] triangles = new int[budget * TrisPerTuft];

            // بذرِ ثابت ⇒ در هر اجرا همان منظره (برای تست‌ها و برای بازسازیِ جهان هم مهم است)
            Random random = new Random(seed ^ (terrain.GetInstanceID() & 0xffff));
            Vector3 size = new Vector3(WorldGenerator.WorldWidth, 0f, WorldGenerator.WorldDepth);
            int placed = 0;
            int attempts = 0;
            int attemptLimit = budget * 6;
            Ray ray = new Ray();
            RaycastHit hit;

            while (placed < budget && attempts < attemptLimit)
            {
                attempts++;
                float x = (float)(random.NextDouble() * 2.0 - 1.0) * size.x * 0.48f;
                float z = (float)(random.NextDouble() * 2.0 - 1.0) * size.z * 0.46f;
                if (x * x + z * z < campClearance * campClearance) continue;      // وسط اردوگاه خالی می‌ماند

                ray.origin = new Vector3(x, rayHeight, z);
                ray.direction = Vector3.down;
                if (!Physics.Raycast(ray, out hit, rayHeight * 2f)) continue;
                if (hit.transform == null || hit.transform.name != WorldParts.Ground) continue;

                float ground = hit.point.y;
                if (ground < minGroundHeight || ground > maxGroundHeight) continue;   // کنارِ آب و قله‌ها چمن نیست

                AppendTuft(ref placed, x, ground, z, vertices, colors, uv, triangles, random);
            }

            _tuftCount = placed;
            if (placed == 0) return;

            _mesh = new Mesh
            {
                name = WorldParts.GrassMesh,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            _mesh.vertices = vertices;
            _mesh.colors32 = colors;
            _mesh.uv = uv;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            _mesh.UploadMeshData(true);

            Material material = MaterialLibrary.Surface(
                MaterialLibrary.SurfaceStyle.Foliage, new Color(0.28f, 0.44f, 0.16f), 0f, 0.18f, true);

            _host = new GameObject(ScatterName);
            _host.transform.SetParent(terrain, false);
            MeshFilter filter = _host.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            MeshRenderer renderer = _host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // بوته‌ها سایه نمی‌اندازند
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _host.isStatic = true;
        }

        private static void AppendTuft(
            ref int placed, float x, float ground, float z,
            Vector3[] vertices, Color32[] colors, Vector2[] uv, int[] triangles, Random random)
        {
            int v = placed * VertsPerTuft;
            int t = placed * TrisPerTuft;
            placed++;

            float height = TuftHeight * (float)(0.62 + random.NextDouble() * 0.78);
            float width = TuftWidth * (float)(0.68 + random.NextDouble() * 0.6);
            float yaw = (float)(random.NextDouble() * Math.PI);
            float lean = (float)(random.NextDouble() * 0.34 - 0.17);
            float ca = (float)Math.Cos(yaw);
            float sa = (float)Math.Sin(yaw);

            // دو تیغه‌ی متقاطع؛ رأس‌های بالا کمی کج می‌شوند تا بوته «سرتاسرِ ورق» نباشد
            Vector3 right = new Vector3(ca * width, 0f, sa * width);
            Vector3 forward = new Vector3(-sa * width, 0f, ca * width);
            Vector3 basePoint = new Vector3(x, ground, z);
            Vector3 topOffset = new Vector3(lean * width, height, lean * width * 0.6f);

            Vector3[] corners =
            {
                basePoint - right, basePoint + right, basePoint + right + topOffset, basePoint - right + topOffset,
                basePoint - forward, basePoint + forward, basePoint + forward + topOffset, basePoint - forward + topOffset
            };

            // رنگِ سرِ بوته روشن‌تر و ته تیره‌تر (جایِ AOِ واقعی؛ vertex color هم _BAZI_VERTEX_COLOR_ON را تغذیه می‌کند)
            Color32 low = ToColor32(0.30f, 0.36f, 0.14f);
            Color32 high = ToColor32(0.55f, 0.72f, 0.26f);
            float tint = (float)(0.78 + random.NextDouble() * 0.42);

            for (int i = 0; i < VertsPerTuft; i++)
            {
                vertices[v + i] = corners[i];
                bool upper = i == 2 || i == 3 || i == 6 || i == 7;
                Color32 color = upper ? high : low;
                colors[v + i] = new Color32(
                    (byte)Mathf.Clamp((int)(color.r * tint), 0, 255),
                    (byte)Mathf.Clamp((int)(color.g * tint), 0, 255),
                    (byte)Mathf.Clamp((int)(color.b * tint * 0.9f), 0, 255),
                    255);
                uv[v + i] = new Vector2(i == 1 || i == 2 || i == 5 || i == 6 ? 1f : 0f, upper ? 1f : 0f);
            }

            for (int blade = 0; blade < 2; blade++)
            {
                int b = v + blade * 4;
                triangles[t++] = b;
                triangles[t++] = b + 1;
                triangles[t++] = b + 2;
                triangles[t++] = b;
                triangles[t++] = b + 2;
                triangles[t++] = b + 3;
                // رویِ دیگرِ تیغه (بدونِ Cull Off، تا شیدر تک‌رو باقی بماند)
                triangles[t++] = b;
                triangles[t++] = b + 2;
                triangles[t++] = b + 1;
                triangles[t++] = b;
                triangles[t++] = b + 3;
                triangles[t++] = b + 2;
            }
        }

        private static Color32 ToColor32(float r, float g, float b)
        {
            return new Color32(
                (byte)Mathf.Clamp((int)(r * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(g * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(b * 255f), 0, 255),
                255);
        }
    }
}
