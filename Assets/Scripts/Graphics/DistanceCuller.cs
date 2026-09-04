using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BaziBaqa
{
    /// <summary>
    /// بودجه‌ی دید (گام ۷): اجزایِ صحنه که از دیدِ دوربینِ ایزومتریک بیرون‌اند یا آن‌سویِ
    /// فاصله‌ی نمایه‌اند، رندر/سایه نمی‌شوند. این تنها کاهنده‌ی draw callِ پروژه است چون
    /// هندسه در زمانِ اجرا ساخته می‌شود و Occlusion Cullingِ بک‌شدهٔ ادیتور روی چنین
    /// جهانی اثر ندارد (داده‌ی occlusion از اشیایِ صحنه در ادیتور بیک می‌شود).
    ///
    /// چرا `CullingGroup` نیست؟ آن API برای مجموعه‌هایِ ایستا طراحی شده و باید آرایه‌ی
    /// موقعیت را با هر جابه‌جاییِ بازمانده‌ها دوباره بدهیم؛ در عوض یک پاسِ ۴ هرتزی با
    /// ۲۲۰ هدف، کمتر از یک‌هزارمِ یک فریم هزینه دارد و رفتارش دقیقاً قابل‌ پیش‌بینی است.
    ///
    /// مرزِ امن: فقط `Renderer.enabled` و `shadowCastingMode` را عوض می‌کند؛ هیچ collider،
    /// هیچ GameObject فعال/غیرفعال و هیچ مقدارِ بازی دست نمی‌خورد ⇒ انتخابِ لمسی و شبیه‌سازی
    /// حتی وقتی چیزی دیده نمی‌شود کار می‌کند.
    /// </summary>
    [DefaultExecutionOrder(-4)]
    public sealed class DistanceCuller : MonoBehaviour
    {
        private const int StateFull = 0;        // نزدیک: رندر + سایه
        private const int StateNoShadow = 1;    // میانی: رندر بدونِ سایه
        private const int StateHidden = 2;      // دور یا بیرونِ حجم دید: بی‌رندر
        public static DistanceCuller Instance { get; private set; }

        private sealed class Target
        {
            public Transform Root;
            public Renderer[] Renderers;
            public int State = StateFull;
        }

        private readonly List<Target> _targets = new List<Target>();
        private readonly Plane[] _planes = new Plane[6];
        private readonly List<Target> _pool = new List<Target>();

        [SerializeField] private float scanSeconds = 0.25f;

        private float _timer = 999f;
        private float _cullDistance = 70f;
        private float _shadowDistance = 42f;
        private int _maxTargets = 220;
        private int _visibleCount;
        private int _shadowlessCount;
        private int _hiddenCount;
        private int _worldGeneration = -1;
        private Camera _camera;
        private bool _enabledForTier = true;

        public int TrackedTargets { get { return _targets.Count; } }
        public int HiddenCount { get { return _hiddenCount; } }
        public int ShadowlessCount { get { return _shadowlessCount; } }
        public float CullDistance { get { return _cullDistance; } }
        public float ShadowCasterDistance { get { return _shadowDistance; } }

        public string Report()
        {
            return "cull targets=" + _targets.Count + " full=" + _visibleCount
                + " noShadow=" + _shadowlessCount + " hidden=" + _hiddenCount
                + " far=" + _cullDistance.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                + " shadowFar=" + _shadowDistance.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                + (_enabledForTier ? string.Empty : " idle");
        }

        private void Awake()
        {
            Instance = this;
            ApplyTier();
        }

        private void OnEnable()
        {
            Instance = this;
            _timer = 999f;
        }

        private void OnDisable()
        {
            RestoreAll();
            if (Instance == this) Instance = null;
        }

        /// <summary>فاصله‌ها از نمایه‌ی کیفیت می‌آیند (تغییرِ کیفیت = بازاعمالِ بودجه).</summary>
        public void ApplyTier()
        {
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().Current;
            _cullDistance = Mathf.Max(8f, tier.cullDistance);
            _shadowDistance = Mathf.Clamp(tier.shadowCasterDistance, 1f, _cullDistance);
            _maxTargets = tier.maxCulledObjects;
            // روی ultra هم این کار می‌ماند (فقط آستانه‌ها بالاست)؛ بودجه‌ی گرافیکیِ بالاتر
            // یعنی صحنه‌ی شلوغ‌تر، نه اجازه‌ی رندرِ بی‌حد.
            _enabledForTier = true;
        }

        /// <summary>
        /// شیارِ QA/تست: یک شیء را دستی زیرِ نظر می‌گیرد (بدونِ این، فهرست فقط از ریشه‌هایِ
        /// جهان پر می‌شود و نمی‌شد رفتارِ نوارها را در صحنهٔ خالیِ تست سنجید).
        /// </summary>
        public bool Track(GameObject gameObject)
        {
            if (gameObject == null || _targets.Count >= _maxTargets) return false;
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0) return false;
            Target target = _pool.Count > 0 ? _pool[_pool.Count - 1] : new Target();
            if (_pool.Count > 0) _pool.RemoveAt(_pool.Count - 1);
            target.Root = gameObject.transform;
            target.Renderers = renderers;
            target.State = StateFull;
            _targets.Add(target);
            return true;
        }

        /// <summary>فهرست را خالی و همه‌ی رندرها را برگردانده می‌کند (QA/تست).</summary>
        public void ClearTracked()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                if (target.Renderers != null) ApplyState(target, StateFull);
            }
            ReleaseTargets();
        }

        /// <summary>یک پاسِ فوری (در بازی از تایمرِ ۰٫۲۵ ثانیه‌ای استفاده می‌شود؛ فهرست را دست نمی‌زند).</summary>
        public void ScanNow()
        {
            RunPass();
        }

        /// <summary>بازخوانیِ اجزا (پس از ساختِ جهان).</summary>
        public void Refresh()
        {
            _timer = 999f;
            _worldGeneration = -1;
            Scan(true);
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = scanSeconds;
            Scan(false);
        }

        private void Scan(bool force)
        {
            GameManager manager = GameManager.Instance;
            WorldGenerator world = manager != null ? manager.World : null;
            if (world != null && world.WorldRoot != null)
            {
                int generation = world.WorldRoot.GetInstanceID();
                if (force || generation != _worldGeneration)
                {
                    _worldGeneration = generation;
                    Collect(world);
                }
            }
            else if (_targets.Count == 0)
            {
                return;     // نه جهان، نه هدفِ دستی ⇒ چیزی برای سنجیدن نیست
            }
            else
            {
                // جهان رفته ولی فهرست مانده ⇒ هیچ شیئی نباید «مخفی» بماند
                RestoreAll();
                return;
            }

            RunPass();
        }

        /// <summary>خودِ سنجشِ فاصله/حجمِ دید (از Scan و از شیارِ تست جدا شده است).</summary>
        private void RunPass()
        {
            _camera = Camera.main;
            if (_camera == null || _targets.Count == 0) return;

            Vector3 cameraPosition = _camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(_camera, _planes);

            int full = 0, noShadow = 0, hidden = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                if (target.Root == null || target.Renderers == null || target.Renderers.Length == 0)
                {
                    target.State = StateFull;
                    continue;
                }

                Vector3 position = target.Root.position;
                float distance = Vector3.Distance(cameraPosition, position);
                int wanted;
                if (distance > _cullDistance)
                {
                    wanted = StateHidden;
                }
                else if (!IntersectsFrustum(target))
                {
                    // بیرونِ حجم دید: رندر را خاموش می‌کنیم تا سایه‌اش هم روی زمین نماند
                    wanted = StateHidden;
                }
                else if (distance > _shadowDistance)
                {
                    wanted = StateNoShadow;
                }
                else
                {
                    wanted = StateFull;
                }

                if (wanted == StateFull) full++;
                else if (wanted == StateNoShadow) noShadow++;
                else hidden++;

                if (wanted != target.State)
                {
                    ApplyState(target, wanted);
                }
            }
            _visibleCount = full;
            _shadowlessCount = noShadow;
            _hiddenCount = hidden;
        }

        /// <summary>
        /// تستِ حجمِ دید رویِ Boundsِ ترکیبیِ رندرها (کره‌ای به‌اندازه‌ی نصفِ قطرِ بزرگ).
        /// کمی محافظه‌کارانه است: کره همیشه از باندِ تنگ‌تر بیرون می‌زند ⇒ هیچ وقت شیءِ
        /// نیمه‌دیده ناپدید نمی‌شود، فقط گاهی دیرتر خاموش می‌شود.
        /// </summary>
        private bool IntersectsFrustum(Target target)
        {
            Bounds bounds = new Bounds(target.Root.position, Vector3.zero);
            Renderer[] renderers = target.Renderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                bounds.Encapsulate(renderer.bounds);
            }
            float radius = bounds.extents.magnitude;
            if (GeometryUtility.TestPlanesAABB(_planes, bounds)) return true;
            // یک حبابِ بزرگ‌تر برای اجزایِ در حالِ حرکت (تازه به لبه‌ی صفحه رسیده‌اند)
            bounds.Expand(radius * 0.6f + 1f);
            return GeometryUtility.TestPlanesAABB(_planes, bounds);
        }

        private static void ApplyState(Target target, int state)
        {
            target.State = state;
            Renderer[] renderers = target.Renderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                switch (state)
                {
                    case StateHidden:
                        renderer.enabled = false;
                        break;
                    case StateNoShadow:
                        renderer.enabled = true;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        break;
                    default:
                        renderer.enabled = true;
                        renderer.shadowCastingMode = renderer.receiveShadows
                            ? ShadowCastingMode.On
                            : ShadowCastingMode.ShadowsOnly;
                        break;
                }
            }
        }

        private void Collect(WorldGenerator world)
        {
            ReleaseTargets();
            AppendRoot(world.ActorRoot);
            AppendRoot(world.BuildingRoot);
            AppendRoot(world.ResourceRoot);
        }

        private void AppendRoot(Transform root)
        {
            if (root == null) return;
            int childCount = root.childCount;
            for (int i = 0; i < childCount && _targets.Count < _maxTargets; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null) continue;
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(false);
                if (renderers == null || renderers.Length == 0) continue;
                Target target = _pool.Count > 0 ? _pool[_pool.Count - 1] : new Target();
                if (_pool.Count > 0) _pool.RemoveAt(_pool.Count - 1);
                target.Root = child;
                target.Renderers = renderers;
                target.State = StateFull;
                _targets.Add(target);
            }
        }

        private void ReleaseTargets()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                target.Renderers = null;
                target.Root = null;
                target.State = StateFull;
                if (_pool.Count < 512) _pool.Add(target);
            }
            _targets.Clear();
            _visibleCount = 0;
            _shadowlessCount = 0;
            _hiddenCount = 0;
        }

        /// <summary>همه‌چیز دوباره دیده می‌شود (خاموش‌شدنِ مدیر، تغییرِ جهان، تست).</summary>
        public void RestoreAll()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                if (target.State != StateFull)
                {
                    ApplyState(target, StateFull);
                }
            }
            _visibleCount = _targets.Count;
            _shadowlessCount = 0;
            _hiddenCount = 0;
        }

        private void OnDestroy()
        {
            ReleaseTargets();
        }
    }
}
