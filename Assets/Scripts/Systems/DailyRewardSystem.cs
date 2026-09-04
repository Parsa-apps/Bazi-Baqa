using System;
using UnityEngine;

namespace BaziBaqa
{
    [Serializable]
    public class DailyRewardSaveState
    {
        public int lastDay;
        public int streak;
    }

    /// <summary>
    /// پاداش روزانه. هر روزی که بازیکن وارد بازی شود یک پاداش می‌گیرد؛ بازی‌های پیاپی (ردیف روزانه)
    /// پاداش را بیشتر می‌کند. این سیستم انگیزه‌ی بازگشت روزانه را فراهم می‌کند.
    /// </summary>
    public sealed class DailyRewardSystem : MonoBehaviour
    {
        public int Streak { get; private set; }
        public bool Claimable { get; private set; }
        public bool ClaimedToday { get; private set; }

        private int _lastDay;

        public void Initialize(GameSaveData save)
        {
            Streak = save == null ? 0 : save.dailyReward.streak;
            _lastDay = save == null ? 0 : save.dailyReward.lastDay;
            ClaimedToday = _lastDay == GameManager.Instance.Clock.Day;
            Claimable = !ClaimedToday;
        }

        public bool TryClaim()
        {
            if (ClaimedToday || !Claimable) return false;

            ClaimedToday = true;
            int today = GameManager.Instance.Clock.Day;
            Streak = _lastDay == today - 1 ? Streak + 1 : 1;
            _lastDay = today;

            int bonus = Mathf.Min(Streak - 1, 3);
            GameManager.Instance.Resources.Add(ResourceType.Food, 12 + bonus * 4, "پاداش روزانه");
            GameManager.Instance.Resources.Add(ResourceType.Gold, 2 + bonus, "پاداش روزانه");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(5, "پاداش روزانه");
            GameEvents.Notify("پاداش روزانه: " + (12 + bonus * 4) + " غذا و " + (2 + bonus) + " طلا (ردیف " + GameClock.ToPersianDigits(Streak.ToString()) + ")");
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayQuest();
            GameManager.Instance.SaveSoon();
            return true;
        }

        public void Refresh(GameSaveData save)
        {
            save.dailyReward.streak = Streak;
            save.dailyReward.lastDay = _lastDay;
        }
    }
}
