using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// پلِ لایه‌ی بصری به اجزایِ صحنه (گام ۵): هر بازمانده/دشمن یک <see cref="ActorMotion"/> و
    /// هر ساختمان یک <see cref="BuildingMotion"/> می‌گیرد. هیچ فایلِ Gameplay ای این کلاس را
    /// نمی‌شناسد؛ ما گرهِ اجزا را «پیدا» می‌کنیم، نه اینکه آن‌ها ما را صدا بزنند.
    ///
    /// بودجه‌ی CPU: اجزایِ دور، یک‌درمیان/سه‌درمیان به‌روز می‌شوند (با lodBiasِ همان سطحِ کیفیت)
    /// و فهرست‌ها فقط وقتی بازسازی می‌شوند که ریشه‌ی صحنه عوض شده باشد.
    /// </summary>
    [DefaultExecutionOrder(-6)]
    public sealed class MotionDirector : MonoBehaviour
    {
        public const int MaxAnimatedActors = 48;
        public const int MaxAnimatedBuildings = 96;
        public const float FarDistance = 34f;

        public static MotionDirector Instance { get; private set; }

        [SerializeField] private float rescanSeconds = 0.35f;

        private readonly List<ActorMotion> _actors = new List<ActorMotion>();
        private float _timer = 999f;
        private int _actorFrame;
        private int _actorGeneration = -1;
        private int _buildingGeneration = -1;
        private int _skippedFar;
        private int _animatedActors;
        private int _animatedBuildings;
        private int _stride = 1;

        public int AnimatedActors { get { return _animatedActors; } }
        public int AnimatedBuildings { get { return _animatedBuildings; } }
        public int SkippedFarThisScan { get { return _skippedFar; } }

        public string Report()
        {
            return "motion actors=" + _animatedActors + "/" + MaxAnimatedActors
                + " buildings=" + _animatedBuildings + "/" + MaxAnimatedBuildings
                + " stride=" + _stride + " farSkip=" + _skippedFar;
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
            _actors.Clear();
            if (Instance == this) Instance = null;
        }

        /// <summary>بازخوانیِ اجزا (پس از ساختِ جهان یا تغییرِ کیفیت).</summary>
        public void Refresh()
        {
            _timer = 999f;
            _actorGeneration = -1;
            _buildingGeneration = -1;
            Rescan(true);
        }

        /// <summary>تنظیمِ گامِ به‌روزرسانی با سطحِ کیفیت (lodBias &gt; 1 ⇒ به‌روزرسانیِ تنک‌تر).</summary>
        public void ApplyTier()
        {
            GraphicsProfile.TierSettings tier = GraphicsProfile.Load().Current;
            _stride = tier.lodBias >= 1.3f ? 3 : (tier.qualityLevel <= 0 ? 2 : 1);
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = rescanSeconds;
                Rescan(false);
            }

            // گامِ تنک: وقتی stride > 1 است، اجزایِ دور یک فریم «خاموش» می‌شوند (Update در
            // MonoBehaviour ارزان است ولی ۴۸ سؤده‌ی GEM در فریم روی موبایل محسوس است).
            if (_stride > 1)
            {
                bool tick = (_actorFrame % _stride) == 0;
                _actorFrame++;
                int skipped = 0;
                for (int i = 0; i < _actors.Count; i++)
                {
                    ActorMotion motion = _actors[i];
                    if (motion == null) continue;
                    bool far = FarAway(motion.transform.position);
                    bool enabled = tick || !far;
                    if (motion.enabled != enabled) motion.enabled = enabled;
                    if (!enabled) skipped++;
                }
                _skippedFar = skipped;
            }
            else
            {
                _actorFrame++;
                if (_skippedFar != 0) _skippedFar = 0;
            }
        }

        private bool FarAway(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            return (camera.transform.position - position).sqrMagnitude > FarDistance * FarDistance;
        }

        private void Rescan(bool force)
        {
            GameManager manager = GameManager.Instance;
            WorldGenerator world = manager != null ? manager.World : null;
            Transform actorRoot = world != null ? world.ActorRoot : null;
            Transform buildingRoot = world != null ? world.BuildingRoot : null;

            if (actorRoot == null && buildingRoot == null)
            {
                _animatedActors = 0;
                _animatedBuildings = 0;
                _actors.Clear();
                return;
            }

            if (actorRoot != null)
            {
                int id = actorRoot.GetInstanceID();
                int actorId = actorRoot.GetInstanceID();
                if (force || actorId != _actorGeneration)
                {
                    _actorGeneration = actorId;
                    BindActors(actorRoot);
                }
            }
            if (buildingRoot != null)
            {
                int id = buildingRoot.GetInstanceID();
                bool regeneration = force || id != _buildingGeneration;
                if (regeneration)
                {
                    _buildingGeneration = id;
                }
                BindBuildings(buildingRoot, regeneration);
            }
        }

        private void BindActors(Transform actorRoot)
        {
            _actors.Clear();
            _animatedActors = 0;
            int count = actorRoot.childCount;
            for (int i = 0; i < count && _animatedActors < MaxAnimatedActors; i++)
            {
                GameObject actor = actorRoot.GetChild(i).gameObject;
                if (actor == null || !actor.activeSelf) continue;
                ActorMotion motion = actor.GetComponent<ActorMotion>();
                if (motion == null) motion = actor.AddComponent<ActorMotion>();
                motion.Configure(actor);
                _actors.Add(motion);
                _animatedActors++;
            }
        }

        private void BindBuildings(Transform buildingRoot, bool regeneration)
        {
            _animatedBuildings = 0;
            int count = buildingRoot.childCount;
            for (int i = 0; i < count && _animatedBuildings < MaxAnimatedBuildings; i++)
            {
                GameObject building = buildingRoot.GetChild(i).gameObject;
                if (building == null) continue;
                BuildingMotion motion = building.GetComponent<BuildingMotion>();
                if (motion == null)
                {
                    motion = building.AddComponent<BuildingMotion>();
                    motion.Configure(building);
                    // اگر فرزندهایِ تازه بعد از ساختِ جهان ظاهر شده‌اند، بازیکن همین حالا ساخته
                    // ⇒ انیمیشنِ رشد پخش می‌شود؛ در بازسازیِ کامل (شروع/بارگذاریِ ذخیره) نه.
                    if (!regeneration)
                    {
                        motion.PlayConstruction();
                    }
                }
                _animatedBuildings++;
            }
        }
    }
}
