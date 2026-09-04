using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// لایه‌ی افکت‌های سنگین (گام ۴): آتش و دود و جرقه و موجِ انفجار، با Object Pool.
    ///
    /// سه قانونِ این فایل:
    /// ۱) هیچ GameObject تازه‌ای داخلِ Update/Play ساخته نمی‌شود — همه از استخر می‌آیند و
    ///    بعد از تمام‌شدن «خاموش» می‌شوند (گاربیج‌کالکتر روی موبایل یعنی همون لگِ معروف).
    /// ۲) هیچ سیستمِ Gameplay ای ما را صدا نمی‌زند. افکت‌ها یا از رویدادِ محیطی (آتش اردوگاه،
    ///    باران) ساخته می‌شوند یا از API عمومی (<see cref="PlayExplosion"/>) که UI/نبرد در گام‌های
    ///    بعدی می‌توانند استفاده کنند؛ پس اگر این فایل حذف شود، بازی بی‌نقص کار می‌کند.
    /// ۳) ذره‌ها همه با Shuriken و متریالِ خودِ پروژه‌اند (VFX Graph نه؛ روی Android/GLES3 پرهزینه
    ///    و نیازمند Compute است).
    /// </summary>
    [DefaultExecutionOrder(-8)]
    public sealed class VfxDirector : MonoBehaviour
    {
        /// <summary>یک گروهِ افکتِ کامل: شعله + دود + جرقه + حلقه‌ی ضربه.</summary>
        private sealed class Burst
        {
            public GameObject Root;
            public ParticleSystem Flame;
            public ParticleSystem Smoke;
            public ParticleSystem Sparks;
            public ParticleSystem Shockwave;
            public float ReleaseAt;
            public bool InUse;
        }

        private const int PoolSize = 6;
        private const float ShockwaveSeconds = 0.42f;

        public static VfxDirector Instance { get; private set; }

        [SerializeField] private bool enhanceWorldFire = true;
        [SerializeField] private float maxDrawDistance = 68f;
        [SerializeField] private int sparkCount = 26;
        [SerializeField] private int flameCount = 34;
        [SerializeField] private int smokeCount = 16;

        private readonly ObjectPool<Burst> _pool = new ObjectPool<Burst>(CreateBurst);
        private readonly List<Burst> _active = new List<Burst>(PoolSize);
        private readonly List<Transform> _worldFire = new List<Transform>();
        private Transform _effectRoot;
        private int _lastEffectRootId = -1;
        private int _rebindTick;
        private int _played;
        private int _released;

        public int ActiveEffects { get { return _active.Count; } }
        public int PooledEffects { get { return _pool.Count; } }
        public int PoolCapacity { get { return PoolSize; } }
        public int PlayedCount { get { return _played; } }
        public int ReleasedCount { get { return _released; } }
        public int WorldFireCount { get { return _worldFire.Count; } }

        public string Report()
        {
            return "vfx active=" + _active.Count + "/" + PoolSize
                + " pooled=" + _pool.Count
                + " played=" + _played + " released=" + _released
                + " worldFire=" + _worldFire.Count
                + " budget=" + BudgetForCurrentTier();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            for (int i = 0; i < PoolSize; i++)
            {
                _pool.Release(CreateBurst());
            }
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                StopBurst(_active[i], true);
            }
            _active.Clear();
            _worldFire.Clear();
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            _pool.Clear(burst =>
            {
                if (burst != null && burst.Root != null) Destroy(burst.Root);
            });
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Burst burst = _active[i];
                if (burst.ReleaseAt > now) continue;
                StopBurst(burst, false);
                _active.RemoveAt(i);
            }

            // هر ~۰٫۷ ثانیه آتش‌هایِ جهان را دوباره پیدا و متریال‌دهی می‌کنیم (بدونِ هیچ دست‌کاریِ داده)
            _rebindTick++;
            if ((_rebindTick & 31) == 0)
            {
                RebindWorldFire();
            }
        }

        // ------------------------------------------------------------------ API

        /// <summary>انفجار: شعله‌ی کوتاه + دود + حلقه‌ی جرقه + موجِ ضربه. اگر استخر پر باشد، چیزی پخش نمی‌شود.</summary>
        public void PlayExplosion(Vector3 position, float radius)
        {
            radius = Mathf.Clamp(radius, 0.6f, 12f);
            if (!CanPlay()) return;
            if (!TryFarEnough(position)) return;
            Burst burst = _pool.Get();
            if (burst == null) return;

            burst.Root.transform.SetPositionAndRotation(position, Quaternion.identity);
            burst.Root.transform.localScale = Vector3.one * radius;
            burst.InUse = true;
            burst.ReleaseAt = Time.unscaledTime + 1.9f;

            Emit(burst.Flame, flameCount, 1f + radius * 0.06f);
            Emit(burst.Smoke, smokeCount, 1.35f);
            Emit(burst.Shockwave, sparkCount, 1.6f + radius * 0.1f);
            Emit(burst.Sparks, sparkCount + (int)(radius * 4f), 1.25f);
            // رنگ‌ها درونِ خودِ امیتر پخته شده‌اند؛ متریالِ آتش کشِ سراسری است و نباید
            // per-effect تغییر کند (وگرنه همه‌ی انفجارهایِ دیگر هم هم‌رنگ می‌شوند).

            _active.Add(burst);
            _played++;
        }

        /// <summary>برخوردِ ساده (تیر/چکش/ضربه): فقط جرقه و یک حلقه‌ی کوچک.</summary>
        public void PlayImpact(Vector3 position, Color tint)
        {
            if (!CanPlay() || !TryFarEnough(position)) return;
            Burst burst = _pool.Get();
            if (burst == null) return;

            burst.Root.transform.SetPositionAndRotation(position, Quaternion.identity);
            burst.Root.transform.localScale = Vector3.one * 0.5f;
            burst.InUse = true;
            burst.ReleaseAt = Time.unscaledTime + 0.6f;
            Emit(burst.Sparks, 12, 0.85f);
            Emit(burst.Shockwave, 8, 0.7f);
            if (tint.grayscale > 0.01f && burst.Sparks != null)
            {
                ParticleSystem.MainModule sparks = burst.Sparks.main;
                sparks.startColor = tint;      // فقط همین امیتر، نه متریالِ مشترک
            }
            _active.Add(burst);
            _played++;
        }

        /// <summary>پاشیدنِ آب (ورود به آب/باران روی زمین).</summary>
        public void PlaySplash(Vector3 position)
        {
            if (!CanPlay() || !TryFarEnough(position)) return;
            Burst burst = _pool.Get();
            if (burst == null) return;
            burst.Root.transform.SetPositionAndRotation(position, Quaternion.identity);
            burst.Root.transform.localScale = Vector3.one * 0.62f;
            burst.InUse = true;
            burst.ReleaseAt = Time.unscaledTime + 0.75f;
            Emit(burst.Sparks, 16, 0.8f);
            if (burst.Sparks != null)
            {
                ParticleSystem.MainModule sparks = burst.Sparks.main;
                sparks.startColor = new Color(0.55f, 0.8f, 0.95f);
            }
            _active.Add(burst);
            _played++;
        }

        /// <summary>بازخوانیِ ریشه‌ها (پس از ساختِ دوباره‌ی جهان).</summary>
        public void Refresh()
        {
            _effectRoot = null;
            _lastEffectRootId = -1;
            _worldFire.Clear();
            RebindWorldFire();
        }

        // ------------------------------------------------------------------ استخر

        /// <summary>سقفِ افکت‌هایِ هم‌زمان از بودجه‌ی ذراتِ همان سطحِ کیفیت؛ استخر را پر نمی‌کند.</summary>
        /// <summary>سقفِ افکت‌هایِ هم‌زمان؛ فاصله در خودِ Play*ها سنجیده می‌شود.</summary>
        private bool CanPlay()
        {
            return _active.Count < BudgetForCurrentTier();
        }

        private static Burst CreateBurst()
        {
            Burst burst = new Burst();
            GameObject root = new GameObject("BaziBurst");
            root.SetActive(false);
            burst.Root = root;

            burst.Flame = BuildEmitter(root.transform, "Flame", 0,
                new Color(1f, 0.72f, 0.28f), new Color(1f, 0.34f, 0.08f), 0.62f, 3.4f, false);
            burst.Smoke = BuildEmitter(root.transform, "Smoke", 1, new Color(0.34f, 0.33f, 0.33f), new Color(0.09f, 0.085f, 0.08f), 2.1f, 1.6f, true);
            burst.Sparks = BuildEmitter(root.transform, "Sparks", 2, new Color(1f, 0.9f, 0.62f), new Color(1f, 0.5f, 0.16f), 0.9f, 6.5f, false);
            burst.Shockwave = BuildEmitter(root.transform, "Shockwave", 2, new Color(0.92f, 0.96f, 1f), new Color(0.6f, 0.72f, 0.95f), 0.35f, 9f, false, true);
            return burst;
        }

        private static ParticleSystem BuildEmitter(
            Transform parent, string name, int mode, Color startColor, Color tint2, float size, float speed, bool stretch, bool ring = false)
        {
            GameObject host = new GameObject(name);
            host.transform.SetParent(parent, false);

            ParticleSystem system = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = startColor;
            main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (stretch)
            {
                main.gravityModifier = -0.06f;
            }
            else if (mode == 0)
            {
                main.gravityModifier = -0.22f;      // شعله به بالا شتاب می‌گیرد
            }
            else
            {
                main.gravityModifier = 0.65f;        // جرقه می‌افتد
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;              // همه‌چیز با Emit() و یک‌باره بیرون می‌رود

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            if (mode == 2)
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.32f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.18f;
            }

            if (ring)
            {
                shape.radiusThickness = 1f;          // حلقه‌ی توخالی ⇒ موجِ ضربه
            }

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(mode == 1 ? 0.4f : -0.2f, mode == 1 ? 1.6f : 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystem.ColorOverLifetimeModule colorModule = system.colorOverLifetime;
            colorModule.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(tint2, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = host.AddComponent<ParticleSystemRenderer>();
            renderer.material = mode == 1
                ? MaterialLibrary.Fire(startColor, tint2, 1, 1.1f)
                : MaterialLibrary.Fire(startColor, tint2, mode, mode == 2 ? 4.2f : 3.4f);
            renderer.sortingOrder = 1200;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.8f;
                renderer.velocityScale = 0.24f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static void Emit(ParticleSystem system, int count, float sizeScale)
        {
            if (system == null) return;
            ParticleSystem.MainModule main = system.main;
            main.startSizeMultiplier = Mathf.Clamp(sizeScale, 0.2f, 6f);
            if (!system.isPlaying) system.Play(true);
            system.Emit(Mathf.Clamp(count, 1, 96));
        }

        private void StopBurst(Burst burst, bool immediate)
        {
            if (burst == null || !burst.InUse) return;
            if (burst.Flame != null) burst.Flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst.Smoke != null) burst.Smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst.Sparks != null) burst.Sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst.Shockwave != null) burst.Shockwave.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst.Root != null) burst.Root.SetActive(false);
            burst.InUse = false;
            _released++;
            _pool.Release(burst);
        }

        // ------------------------------------------------------------------ جهان

        private int BudgetForCurrentTier()
        {
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().Current;
            // رویِ سطح‌هایِ پایین‌تر استخر کوچک‌تر از حداکثر است (کمترین ذره، همان حس)
            return Mathf.Clamp(tier.particleBudget / 220, 2, PoolSize);
        }

        private bool TryFarEnough(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null) return true;
            return (camera.transform.position - position).sqrMagnitude <= maxDrawDistance * maxDrawDistance;
        }

        private void RebindWorldFire()
        {
            if (!enhanceWorldFire) return;
            GameManager manager = GameManager.Instance;
            WorldGenerator world = manager != null ? manager.World : null;
            Transform root = world != null ? world.EffectRoot : null;
            if (root == null)
            {
                if (_worldFire.Count > 0) _worldFire.Clear();
                return;
            }
            int id = root.GetInstanceID();
            if (id == _lastEffectRootId) return;
            _lastEffectRootId = id;
            _effectRoot = root;
            _worldFire.Clear();

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                string name = children[i].name;
                if (name != WorldParts.Fire && name != WorldParts.Spark && name != WorldParts.Smoke) continue;
                _worldFire.Add(children[i]);
                ParticleSystemRenderer renderer = children[i].GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                int mode = name == WorldParts.Smoke ? 1 : (name == WorldParts.Spark ? 2 : 0);
                Color hot = mode == 1 ? new Color(0.42f, 0.4f, 0.4f) : (mode == 2 ? new Color(1f, 0.92f, 0.66f) : new Color(1f, 0.7f, 0.26f));
                Color cold = mode == 1 ? new Color(0.1f, 0.1f, 0.11f) : new Color(1f, 0.3f, 0.07f);
                renderer.material = MaterialLibrary.Fire(hot, cold, mode, mode == 0 ? 3.2f : 2.2f);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
