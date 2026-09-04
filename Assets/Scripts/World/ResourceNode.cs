using UnityEngine;

namespace BaziBaqa
{
    public sealed class ResourceNode : MonoBehaviour
    {
        public ResourceType type;
        public int amount = 40;
        public int capacity = 40;

        private float _pulse;
        private Vector3 _baseScale;
        private Renderer _renderer;

        public bool IsDepleted { get { return amount <= 0; } }

        public void Initialize(ResourceType resourceType, int resourceAmount)
        {
            type = resourceType;
            amount = Mathf.Max(0, resourceAmount);
            capacity = amount;
            _baseScale = transform.localScale;
            _renderer = GetComponentInChildren<Renderer>();
        }

        public int Gather(int requested)
        {
            if (requested <= 0 || IsDepleted) return 0;
            int gathered = Mathf.Min(requested, amount);
            amount -= gathered;
            return gathered;
        }

        private void Update()
        {
            if (IsDepleted)
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }

            if (_renderer != null && !_renderer.enabled) _renderer.enabled = true;
            _pulse += Time.deltaTime * 2.2f;
            float ratio = capacity <= 0 ? 0.2f : Mathf.Clamp01((float)amount / capacity);
            float wave = 1f + Mathf.Sin(_pulse) * 0.025f;
            transform.localScale = _baseScale * Mathf.Lerp(0.72f, 1f, ratio) * wave;
        }
    }
}
