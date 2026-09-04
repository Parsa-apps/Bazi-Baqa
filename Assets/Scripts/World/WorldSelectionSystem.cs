using UnityEngine;
using UnityEngine.EventSystems;

namespace BaziBaqa
{
    public sealed class WorldSelectionSystem : MonoBehaviour
    {
        private Camera _camera;
        private Vector2 _pointerDown;
        private bool _tracking;

        public void Initialize(Camera camera)
        {
            _camera = camera;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || GameManager.Instance.Construction.IsPlacing) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId))
                {
                    _pointerDown = touch.position;
                    _tracking = true;
                }
                else if (touch.phase == TouchPhase.Ended && _tracking)
                {
                    _tracking = false;
                    if ((touch.position - _pointerDown).sqrMagnitude < 100f) SelectAt(touch.position);
                }
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1))
            {
                _pointerDown = Input.mousePosition;
                _tracking = true;
            }
            if (Input.GetMouseButtonUp(0) && _tracking)
            {
                _tracking = false;
                Vector2 pointer = Input.mousePosition;
                if ((pointer - _pointerDown).sqrMagnitude < 100f) SelectAt(pointer);
            }
        }

        private void SelectAt(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 160f)) return;
            BuildingController building = hit.collider.GetComponentInParent<BuildingController>();
            if (building != null && building.IsOperational) GameManager.Instance.UI.ShowBuildingDetails(building);
        }

        private static bool IsPointerOverUi(int fingerId)
        {
            if (EventSystem.current == null) return false;
            return fingerId < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
