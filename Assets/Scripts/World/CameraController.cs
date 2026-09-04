using UnityEngine;
using UnityEngine.EventSystems;

namespace BaziBaqa
{
    public sealed class CameraController : MonoBehaviour
    {
        public float minZoom = 13f;
        public float maxZoom = 30f;
        public float dragSpeed = 0.035f;

        private Camera _camera;
        private Vector3 _focus = Vector3.zero;
        private bool _dragging;
        private Vector2 _lastPointer;
        private float _lastPinchDistance;

        public void Initialize()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 21f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 160f;
            _camera.backgroundColor = new Color(0.035f, 0.075f, 0.1f);
            transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            ApplyFocus();
        }

        private void Update()
        {
            if (_camera == null || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            HandleTouchAndMouse();
            ApplyFocus();
        }

        public Vector3 ScreenToGround(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                return GameManager.Instance.World.ClampToIsland(ray.GetPoint(distance));
            }
            return _focus;
        }

        private void HandleTouchAndMouse()
        {
            if (Input.touchCount == 2)
            {
                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);
                float distance = Vector2.Distance(first.position, second.position);
                if (_lastPinchDistance > 0f)
                {
                    _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize - (distance - _lastPinchDistance) * 0.018f, minZoom, maxZoom);
                }
                _lastPinchDistance = distance;
                _dragging = false;
                return;
            }
            _lastPinchDistance = 0f;

            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId))
                {
                    _dragging = true;
                    _lastPointer = touch.position;
                }
                else if (_dragging && (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
                {
                    Pan(touch.position - _lastPointer);
                    _lastPointer = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _dragging = false;
                }
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1))
            {
                _dragging = true;
                _lastPointer = Input.mousePosition;
            }
            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 current = Input.mousePosition;
                Pan(current - _lastPointer);
                _lastPointer = current;
            }
            if (Input.GetMouseButtonUp(0)) _dragging = false;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize - scroll * 1.2f, minZoom, maxZoom);
            }
        }

        private void Pan(Vector2 delta)
        {
            Vector3 right = transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            _focus -= right * delta.x * dragSpeed * (_camera.orthographicSize / 20f);
            _focus -= forward * delta.y * dragSpeed * (_camera.orthographicSize / 20f);
            _focus.x = Mathf.Clamp(_focus.x, -15f, 15f);
            _focus.z = Mathf.Clamp(_focus.z, -10f, 10f);
        }

        private void ApplyFocus()
        {
            transform.position = _focus - transform.forward * 42f;
        }

        private static bool IsPointerOverUi(int fingerId)
        {
            if (EventSystem.current == null) return false;
            return fingerId < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
