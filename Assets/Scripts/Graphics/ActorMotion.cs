using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// انیماتورِ رویه‌ایِ تک‌نفره (گام ۵): راه‌رفتن، دویدن، ایستادن، ضربه‌زدن، خوردنِ ضربه و
    /// افتادن — با خواندنِ وضعیت، بدونِ هیچ دست‌کاریِ منطق.
    ///
    /// چرا «خواندنی»؟ SurvivorAgent/EnemyAgent تصمیم‌ساز هستند و انیمیشن فقط بازتابِ اوست؛
    /// اگر حرکت از آن‌ها فرمان داده می‌شد، یک تغییرِ کوچکِ گیم‌پلی می‌توانست کلِ چرخه‌ی
    /// حرکتی را بشکند. پس اینجا فقط position/health/state خوانده می‌شود.
    ///
    /// چرا استخوان و Animator نیست؟ پروژه باید بدونِ Asset باز شود؛ اندام‌ها از همان مشِ
    /// کپسولِ بدن (بدونِ Collider) ساخته می‌شوند تا انتخابِ لمسی/ریکست‌ها دست‌نخورده بماند.
    /// </summary>
    public sealed class ActorMotion : MonoBehaviour
    {
        private const string LimbPrefix = "Limb_";

        private Transform _torso;
        private Transform _backpack;
        private Transform _leftArm;
        private Transform _rightArm;
        private Transform _leftLeg;
        private Transform _rightLeg;
        private Vector3 _torsoBaseScale;

        private SurvivorAgent _survivor;
        private EnemyAgent _enemy;

        private Vector3 _lastPosition;
        private float _speed;
        private float _speedSmoothed;
        private float _phase;
        private float _attack;
        private float _flinch;
        private float _collapse;
        private float _lastHealth = -1f;
        private bool _isEnemy;
        private Quaternion _bodyBaseRotation;

        public float Speed { get { return _speedSmoothed; } }
        public bool IsCollapsed { get { return _collapse > 0.6f; } }
        public bool HasLimbs { get { return _leftArm != null && _rightLeg != null; } }
        public string Report()
        {
            return "speed=" + _speedSmoothed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " phase=" + _phase.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                + " attack=" + _attack.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " flinch=" + _flinch.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + (IsCollapsed ? " down" : string.Empty);
        }

        /// <summary>
        /// اجزایِ لازم را پیدا و در صورت نبود می‌سازد. Idempotent است ⇒ هر بار فراخوانی
        /// (مثلاً بعد از بازسازیِ جهان) بی‌خطر و بدونِ نشتی است.
        /// </summary>
        public ActorMotion Configure(GameObject actorRoot)
        {
            _survivor = GetComponentInParent<SurvivorAgent>();
            _enemy = GetComponentInParent<EnemyAgent>();
            _isEnemy = _survivor == null && _enemy != null;

            Transform root = actorRoot != null ? actorRoot.transform : transform;
            _torso = FindChild(root, WorldParts.ActorTorso);
            _backpack = FindChild(root, WorldParts.ActorBackpack);
            if (_torso == null) return this;

            _torsoBaseScale = _torso.localScale;
            _bodyBaseRotation = _torso.localRotation;
            BuildLimbs(_torso);
            _lastPosition = root.position;
            _lastHealth = CurrentHealth;
            return this;
        }

        private float CurrentHealth
        {
            get
            {
                if (_survivor != null) return _survivor.IsAlive ? _survivor.Health : 0f;
                if (_enemy != null) return _enemy.IsAlive ? _enemy.Health : 0f;
                return -1f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Transform root = transform;

            // ۱) سرعت از جابه‌جاییِ خودِ گره می‌آید (هیچ فیلدِ حرکتی در Agent نیست) ⇒ هیچ وابستگی
            Vector3 delta = root.position - _lastPosition;
            _lastPosition = root.position;
            _speed = dt > 0.0001f ? delta.magnitude / dt : 0f;
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, _speed, Mathf.Clamp01(dt * 9f));

            // ۲) سلامت: افتِ سلامت یعنی «ضربه خوردم»؛ بهبودِ آرام یعنی «دارو خوردم»
            float health = CurrentHealth;
            if (health >= 0f && _lastHealth >= 0f && health < _lastHealth - 0.01f)
            {
                _flinch = Mathf.Clamp01(_flinch + (health < _lastHealth - 12f ? 1f : 0.55f));
            }
            if (health >= 0f) _lastHealth = health;

            bool alive = health < 0f || health > 0.5f;
            _collapse = Mathf.MoveTowards(_collapse, alive ? 0f : 1f, dt * 1.7f);

            // ۳) فازِ راه‌رفتن: گام با سرعت بزرگ می‌شود، بسامد هم کمی بالاتر می‌رود
            float stride = Mathf.Clamp01(_speedSmoothed / 3.2f);
            _phase += dt * (1.4f + 4.6f * stride);

            // ۴) حالت‌هایِ بازمانده: ضربه/ساخت‌وساز دست‌ها را بالا می‌برد
            float work = 0f;
            if (_survivor != null)
            {
                switch (_survivor.State)
                {
                    case SurvivorState.Gathering:
                    case SurvivorState.Building:
                    case SurvivorState.Returning: work = 0.85f; break;
                    case SurvivorState.Fleeing: work = 0.45f; break;
                    case SurvivorState.Injured: work = 0.15f; break;
                    default: work = 0f; break;
                }
            }
            if (_enemy != null) work = 0.75f + 0.25f * stride;
            _attack = Mathf.Lerp(_attack, work, Mathf.Clamp01(dt * 6f));
            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 2.6f);

            // ۵) جهت و افتادنِ کلیِ بدن کارِ خودِ Agent است (SurvivorAgent/EnemyAgent هر دو
            // transform.rotation را می‌نویسند) ⇒ اینجا اصلاً به ریشه دست نمی‌زنیم؛ فقط اجزایِ
            // فرزندِ تازه (اندام‌ها) و فرمِ بدن که بازی آن‌ها را نمی‌نویسد.

            // ۶) بدن: فرمِ گام، تکانِ ضربه، خمیده‌شدنِ دویدن
            // localPositionِ تنه را SurvivorAgent با «گام‌هایِ» خودش می‌نویسد ⇒ ما فقط فرم
            // (scale/rotation) را کنترل می‌کنیم تا دو انیماتور همدیگر را نبینند.
            float bob = Mathf.Sin(_phase * 2f) * (0.02f + 0.055f * stride);
            float squash = 1f - _flinch * 0.16f + stride * 0.03f + bob * 0.6f;
            _torso.localScale = new Vector3(
                _torsoBaseScale.x * (1f + _flinch * 0.12f),
                _torsoBaseScale.y * squash,
                _torsoBaseScale.z * (1f + _flinch * 0.12f));
            _torso.localRotation = _bodyBaseRotation
                * Quaternion.Euler(stride * 9f + _attack * 6f + _flinch * 14f, 0f, _flinch * 5f);

            // ۷) اندام‌ها
            if (_leftArm != null)
            {
                float swing = Mathf.Sin(_phase * 2f) * (0.18f + 0.85f * stride);
                float lift = _attack * (0.9f + 0.5f * Mathf.Sin(_phase * 6f));
                SetLimb(_leftArm, swing + lift * 0.7f);
                SetLimb(_rightArm, -swing + lift);
                SetLimb(_leftLeg, -swing * 1.15f);
                SetLimb(_rightLeg, swing * 1.15f);
            }

            // ۸) کوله/چشم: کوله با بدن تکان می‌خورد؛ چشمِ دشمن در حالتِ حمله می‌درخشد
            if (_backpack != null)
            {
                Vector3 local = _backpack.localPosition;
                _backpack.localPosition = new Vector3(local.x, local.y + bob * 0.5f, local.z + _flinch * 0.05f);
            }

            // ۹) افتادن: خودِ Agent ریشه را ۹۰ درجه می‌خواباند؛ ما شل‌شدنِ اندام‌ها و له‌شدنِ بدن
            // را اضافه می‌کنیم تا «افتادن» فیزیکی به نظر برسد نه یک چرخشِ ناگهانی.
            if (_collapse > 0.01f && _leftArm != null)
            {
                float limp = _collapse;
                SetLimb(_leftArm, 1.35f * limp);
                SetLimb(_rightArm, -1.2f * limp);
                SetLimb(_leftLeg, 0.55f * limp);
                SetLimb(_rightLeg, -0.45f * limp);
                _torso.localScale = _torsoBaseScale * new Vector3(1f + 0.22f * limp, 1f - 0.34f * limp, 1f + 0.22f * limp);
            }
        }

        private static void SetLimb(Transform limb, float angleRadians)
        {
            if (limb == null) return;
            limb.localRotation = Quaternion.Euler(angleRadians * Mathf.Rad2Deg * 0.55f, 0f, 0f);
        }

        private void BuildLimbs(Transform torso)
        {
            if (_leftArm != null) return;
            _leftArm = MakeLimb(torso, "Arm_L", new Vector3(-0.36f, -0.06f, 0f), new Vector3(0.13f, 0.5f, 0.15f));
            _rightArm = MakeLimb(torso, "Arm_R", new Vector3(0.36f, -0.06f, 0f), new Vector3(0.13f, 0.5f, 0.15f));
            _leftLeg = MakeLimb(torso, "Leg_L", new Vector3(-0.14f, -0.78f, 0f), new Vector3(0.16f, 0.52f, 0.18f));
            _rightLeg = MakeLimb(torso, "Leg_R", new Vector3(0.14f, -0.78f, 0f), new Vector3(0.16f, 0.52f, 0.18f));
        }

        private Transform MakeLimb(Transform parent, string suffix, Vector3 localPosition, Vector3 scale)
        {
            // از همان مشِ والد (کپسول) استفاده می‌کنیم: بدونِ Mesh تازه، بدونِ Collider،
            // و هیچ تأثیری روی RaycastHitهایِ انتخابِ لمسی ندارد.
            Mesh shared = null;
            MeshFilter parentFilter = parent.GetComponent<MeshFilter>();
            if (parentFilter != null) shared = parentFilter.sharedMesh;
            if (shared == null) return null;

            GameObject limb = new GameObject(LimbPrefix + suffix);
            limb.transform.SetParent(parent, false);
            limb.transform.localPosition = localPosition;
            limb.transform.localScale = scale;
            MeshFilter filter = limb.AddComponent<MeshFilter>();
            filter.sharedMesh = shared;
            MeshRenderer renderer = limb.AddComponent<MeshRenderer>();
            Color tint = _isEnemy ? new Color(0.3f, 0.03f, 0.06f) : new Color(0.16f, 0.19f, 0.22f);
            renderer.sharedMaterial = MaterialLibrary.Surface(MaterialLibrary.SurfaceStyle.Panel, tint, 0.05f, 0.25f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return limb.transform;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
            }
            // یک سطحِ عمیق‌تر (بعضی اجزایِ بدن زیرِ گره‌هایِ میانی‌اند)
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
