using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// انیمیشنِ ساختمانیِ رویه‌ای (گام ۵): «رشد» هنگام ساخت، پالسِ ارتقا، لرزش و دوده‌ی
    /// آسیب‌دیده، و فروپاشیِ ملایم وقتی سلامت به صفر رسید. هیچ مقدارِ بازی تغییر نمی‌کند؛
    /// فقط scale/رنگ‌متریالِ instance (MaterialPropertyBlock) نوشته می‌شود تا کشِ سراسری
    /// آلوده نشود.
    /// </summary>
    public sealed class BuildingMotion : MonoBehaviour
    {
        private const float GrowSeconds = 0.42f;
        private const float ShakeSeconds = 0.38f;

        private BuildingController _building;
        private Transform[] _parts;
        private Vector3[] _partBaseScale = new Vector3[0];
        private Vector3[] _partBasePos = new Vector3[0];
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;

        private float _grow = 1f;
        private float _shake;
        private float _pulse;
        private float _collapse;
        private int _lastLevel = -1;
        private float _lastHealth = -1f;
        private bool _bound;

        public float Growth { get { return _grow; } }
        public bool IsGrowing { get { return _grow < 0.995f; } }
        public float DamageVisual { get; private set; }
        public string Report()
        {
            return "grow=" + _grow.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " damage=" + DamageVisual.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " renderers=" + (_renderers != null ? _renderers.Length : 0);
        }

        public BuildingMotion Configure(GameObject buildingRoot)
        {
            _building = buildingRoot != null ? buildingRoot.GetComponentInParent<BuildingController>() : null;
            if (_building == null) _building = GetComponentInParent<BuildingController>();
            CacheParts();
            if (_building != null)
            {
                _lastLevel = _building.Level;
                _lastHealth = _building.Health;
                // ساختمانی که از ذخیره می‌آید نباید دوباره «رشد» کند
                _grow = 1f;
            }
            else
            {
                _grow = 0f;
            }
            _bound = true;
            return this;
        }

        /// <summary>ساختِ تازه: از صفر رشد می‌کند (فراخوانی‌شده توسط MotionDirector).</summary>
        public void PlayConstruction()
        {
            _grow = 0f;
            _pulse = 0.5f;
        }

        private void Update()
        {
            if (!_bound) Configure(gameObject);
            float dt = Time.deltaTime;

            if (_building != null)
            {
                if (_building.Level != _lastLevel)
                {
                    _lastLevel = _building.Level;
                    _pulse = 1f;                      // ارتقا ⇒ پالسِ کوتاه
                    _grow = Mathf.Min(_grow, 0.62f);  // کمی جمع می‌شود و دوباره باز می‌گردد
                CacheParts();                     // مقیاسِ پایه با ارتقا عوض می‌شود
                }
                float health = _building.Health;
                if (_lastHealth >= 0f && health < _lastHealth - 0.5f)
                {
                    _shake = Mathf.Clamp01(_shake + (health < _lastHealth - 18f ? 1f : 0.5f));
                }
                _lastHealth = health;
                _collapse = Mathf.MoveTowards(_collapse, _building.IsOperational ? 0f : 1f, dt * 1.5f);
            }

            _grow = Mathf.Min(1f, _grow + dt / GrowSeconds);
            _shake = Mathf.MoveTowards(_shake, 0f, dt / ShakeSeconds);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 2.6f);

            // ریشه را بازی کنترل می‌کند (CreateBuildingVisual + UpdateVisuals برای ارتقا) ⇒
            // همه‌ی انیمیشن روی فرزندان است تا هیچ وقت با منطقِ ساختمان در ست نباشیم.
            float eased = Mathf.Max(0.02f, EaseOutBack(_grow));
            float bounce = 1f + _pulse * 0.08f;
            float sink = Mathf.Lerp(-0.4f, 0f, _grow) - _collapse * 0.55f;
            float shakeX = 0f, shakeZ = 0f;
            if (_shake > 0.001f)
            {
                float amount = _shake * _shake * 0.09f;
                shakeX = (Mathf.PerlinNoise(Time.time * 24f, 1.7f) * 2f - 1f) * amount;
                shakeZ = (Mathf.PerlinNoise(Time.time * 21f, 5.3f) * 2f - 1f) * amount;
            }
            for (int i = 0; i < _parts.Length; i++)
            {
                Transform part = _parts[i];
                if (part == null) continue;
                Vector3 baseScale = _partBaseScale[i];
                Vector3 basePos = _partBasePos[i];
                float pop = Mathf.Clamp01(eased + i * 0.06f);
                part.localScale = baseScale * Mathf.Max(0.02f, Mathf.Min(1f, pop * bounce));
                part.localPosition = new Vector3(basePos.x + shakeX, basePos.y + sink, basePos.z + shakeZ);
            }

            ApplyDamageLook();
        }

        private void CacheParts()
        {
            int count = transform.childCount;
            _parts = new Transform[count];
            _partBaseScale = new Vector3[count];
            _partBasePos = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Transform child = transform.GetChild(i);
                _parts[i] = child;
                _partBaseScale[i] = child.localScale;
                _partBasePos[i] = child.localPosition;
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = Mathf.Clamp01(x) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private void ApplyDamageLook()
        {
            if (_building == null) return;
            float max = 100f + (_building.Level - 1) * 45f;
            float ratio = Mathf.Clamp01(_building.Health / Mathf.Max(1f, max));
            DamageVisual = 1f - ratio;
            float value = Mathf.Clamp01(DamageVisual * 1.15f + _collapse * 0.5f);

            // فقط وقتی واقعاً آسیب دیده property block می‌فرستیم (هزینه‌ی set روی همه‌ی رندررها)
            if (value < 0.02f && _block == null) return;

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(false);
            }
            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetFloat("_BaziDamage", value);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void OnDisable()
        {
            if (_block == null || _renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(null);
            }
        }
    }
}
