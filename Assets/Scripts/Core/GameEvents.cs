using System;
using UnityEngine;

namespace BaziBaqa
{
    public static class GameEvents
    {
        public static event Action<string> Notification;
        public static event Action OnGameStateChanged;

        public static void Notify(string message)
        {
            Notification?.Invoke(message);
        }

        public static void StateChanged()
        {
            OnGameStateChanged?.Invoke();
        }
    }
}
