using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaziBaqa
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private GameManager _gameManager;
        private UIManager _ui;

        private void Awake()
        {
            EnsureEventSystem();
            _gameManager = GetComponent<GameManager>();
            if (_gameManager == null) _gameManager = gameObject.AddComponent<GameManager>();
            _ui = GetComponent<UIManager>();
            if (_ui == null) _ui = gameObject.AddComponent<UIManager>();
            _gameManager.Initialize(_ui);
            _ui.Initialize(_gameManager);
        }

        private void Start()
        {
            _ui.BeginPresentation();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventObject = new GameObject("سامانه لمس");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<StandaloneInputModule>();
        }
    }
}
