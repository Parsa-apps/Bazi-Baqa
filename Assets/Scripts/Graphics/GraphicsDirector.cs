using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// میزبانِ همه‌ی سامانه‌هایِ لایه‌ی گرافیک: پس‌پردازِ سینمایی، نورِ سینمایی، باد/محیط،
    /// افکت‌های ویژه و مدیرِ کیفیتِ بصری.
    ///
    /// چرا خودش را نصب می‌کند (<c>RuntimeInitializeOnLoadMethod</c>)؟ چون قرار بود لایه‌ی
    /// Visual بدون دست‌زدن به سامانه‌هایِ سالمِ Gameplay اضافه شود. صحنه، <c>GameBootstrap</c>
    /// و پریفب‌ها دست‌نخورده می‌مانند؛ اگر این فایل حذف شود، بازی دقیقاً مثل قبل اجرا می‌شود.
    ///
    /// ترتیب اجرا: نصب بعد از Awake صحنه ⇒ <c>GameManager.Instance</c> وجود دارد، ولی هنوز
    /// هیچ داده‌ای بارگذاری نشده؛ پس این کلاس هیچ وابستگیِ اجباری به داده ندارد و فقط
    /// وضعیت‌های جهانیِ شیدر را مقداردهی اولیه می‌کند.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class GraphicsDirector : MonoBehaviour
    {
        public static GraphicsDirector Instance { get; private set; }

        [SerializeField] private bool autoInstallChildren = true;

        private readonly List<MonoBehaviour> _children = new List<MonoBehaviour>();
        private int _lastQualityLevel = -1;
        private int _lastWorldGeneration;
        private float _retryTimer;
        private Camera _camera;
        private bool _bootLogged;

        public IReadOnlyList<MonoBehaviour> Children { get { return _children; } }
        public CinematicVolumeRig Volume { get; private set; }
        public SkyLightingRig Sky { get; private set; }
        public WindField Wind { get; private set; }
        public FoliageScatter Foliage { get; private set; }
        public VfxDirector Vfx { get; private set; }
        public bool IsInstalled { get { return Instance == this; } }

        /// <summary>نصبِ خودکار در اولین فریمِ هر صحنه (شاملِ صحنه‌های تست).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnSceneLoad()
        {
            Ensure();
        }

        /// <summary>
        /// ساختِ مدیرِ گرافیک (idempotent). در EditMode که RuntimeInitializeOnLoadMethod اجرا
        /// نمی‌شود، تست‌ها و ابزارهای ویرایشگر همین را صدا می‌زنند.
        /// </summary>
        public static GraphicsDirector Ensure()
        {
            if (Instance != null) return Instance;

            GraphicsDirector existing = FindFirstObjectByType<GraphicsDirector>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject(WorldParts.GraphicsDirector);
            GraphicsDirector director = host.AddComponent<GraphicsDirector>();
            DontDestroyOnLoad(host);
            return director;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Install();
        }

        private void OnEnable()
        {
            // هیچ ثابتِ جهانی این‌جا نوشته نمی‌شود: باد کارِ WindField است (که در Awake همان
            // لحظه اول مقدار می‌دهد) و مه/اتمسفر کارِ SkyLightingRig. دو نویسنده برای یک
            // بردارِ جهانی، یعنی نتیجه‌ای که به ترتیبِ اجرا بستگی دارد؛ دروازه‌ی
            // Tools/project_lint.py و تست‌های EditMode همین را تضمین می‌کنند.
        }

        private void Install()
        {
            if (!autoInstallChildren) return;

            Volume = GetOrAdd<CinematicVolumeRig>();
            if (Volume != null) _children.Add(Volume);

            Sky = GetOrAdd<SkyLightingRig>();
            if (Sky != null) _children.Add(Sky);

            Wind = GetOrAdd<WindField>();
            if (Wind != null) _children.Add(Wind);

            Foliage = GetOrAdd<FoliageScatter>();
            if (Foliage != null) _children.Add(Foliage);

            Vfx = GetOrAdd<VfxDirector>();
            if (Vfx != null) _children.Add(Vfx);

            // فازهای بعدیِ گرافیک همین‌جا اضافه می‌شوند (محیط زنده، VFX، کیفیت)
            GraphicsProfile profile = GraphicsProfile.Load();
            List<string> issues = new List<string>();
            profile.Validate(issues);
            for (int i = 0; i < issues.Count; i++)
            {
                GameLogger.Warn("GraphicsProfile issue: " + issues[i]);
            }

            RenderPipelineBridge.ApplyCurrentQuality(true);
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null) component = gameObject.AddComponent<T>();
            return component;
        }

        private void Update()
        {
            // ۱) دوربین ممکن است بعد از ما ساخته شود ⇒ تا سه ثانیه هر نیم‌ثانیه تلاش می‌کنیم
            if (_camera == null)
            {
                _retryTimer -= Time.unscaledDeltaTime;
                if (_retryTimer <= 0f)
                {
                    _retryTimer = 0.5f;
                    _camera = Camera.main;
                    if (_camera != null) RenderPipelineBridge.ApplyCurrentQuality(true);
                }
            }

            // ۲) تغییرِ کیفیت (از PerformanceManager یا منو) ⇒ بازاعمالِ لایه‌ی بصری
            int quality = QualitySettings.GetQualityLevel();
            if (quality != _lastQualityLevel)
            {
                _lastQualityLevel = quality;
                RenderPipelineBridge.ApplyCurrentQuality(true);
                if (Volume != null) Volume.Rebuild();
                if (Sky != null)
                {
                    // بودجه‌ی سایه/چراغ/آسمان با سطحِ کیفیت عوض می‌شود ⇒ بی‌درجا بازاعمال
                    Sky.Refresh();
                    Sky.ApplyNow();
                }
                if (Wind != null) Wind.ApplyTier();
                if (Foliage != null) Foliage.Refresh();
                if (Vfx != null) Vfx.Refresh();
            }

            // ۳) بازسازیِ جهان: نورِ اصلی و ریشه‌ها تازه‌اند ⇒ Rigِ نور باید دوباره پیدا کند
            WorldGenerator world = GameManager.Instance != null ? GameManager.Instance.World : null;
            int generation = world != null && world.WorldRoot != null ? world.WorldRoot.GetInstanceID() : 0;
            if (generation != _lastWorldGeneration)
            {
                _lastWorldGeneration = generation;
                if (Sky != null) Sky.Refresh();
                if (Foliage != null) Foliage.Refresh();
                if (Vfx != null) Vfx.Refresh();
            }

            // ۴) اولین باری که جهان ساخته شد، یک گزارشِ کامل می‌نویسیم (برای ممیزیِ صحنه)
            if (!_bootLogged && GameManager.Instance != null && GameManager.Instance.World != null)
            {
                _bootLogged = true;
                GameLogger.System(Report());
            }
        }

        /// <summary>گزارشِ وضعیتِ لایه‌ی گرافیک؛ ابزارِ ویرایشگر و تست‌ها همین را خوانده و بررسی می‌کنند.</summary>
        public string Report()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("GraphicsDirector | ").Append(RenderPipelineBridge.Describe());
            builder.Append(" | volume=").Append(Volume != null && Volume.IsActive ? "active" : "inactive");
            builder.Append(" | materials=").Append(MaterialLibrary.CachedMaterialCount);
            builder.Append(" | ").Append(Sky != null ? Sky.Report() : "sky=absent");
            builder.Append(" | ").Append(Wind != null ? Wind.Report() : "wind=absent");
            builder.Append(" | ").Append(Foliage != null ? Foliage.Report() : "foliage=absent");
            builder.Append(" | ").Append(Vfx != null ? Vfx.Report() : "vfx=absent");
            return builder.ToString();
        }

        /// <summary>بازخوانیِ دستی (پس از نصبِ URP Asset یا تغییرِ فایل نمایه).</summary>
        public void Refresh()
        {
            GraphicsProfile.Load(true);
            if (Volume != null) Volume.Rebuild();
            if (Sky != null) { Sky.Refresh(); Sky.ApplyNow(); }
            if (Wind != null) { Wind.ApplyTier(); Wind.ApplyNow(); }
            if (Foliage != null) Foliage.Refresh();
            if (Vfx != null) Vfx.Refresh();
            RenderPipelineBridge.ApplyCurrentQuality(true);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }
    }
}
