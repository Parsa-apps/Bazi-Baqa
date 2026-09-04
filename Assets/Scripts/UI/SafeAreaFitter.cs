using UnityEngine;

namespace BaziBaqa
{
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (_rect == null) return;
            if (_lastSafeArea != Screen.safeArea || _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height) Apply();
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 anchorMin = new Vector2(safe.x / Screen.width, safe.y / Screen.height);
            Vector2 anchorMax = new Vector2((safe.x + safe.width) / Screen.width, (safe.y + safe.height) / Screen.height);
            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
