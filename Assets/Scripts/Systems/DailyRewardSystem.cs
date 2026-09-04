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
        /// <summary>پاداش پایه و گام‌های ردیف روزانه؛ منبع یکتای اعداد برای UI و منطق.</summary>
        public const int BaseFood = 12;
        public const int BaseGold = 2;
        public const int FoodPerStreak = 4;
        public const int MaxStreakBonus = 3;

        public static int BonusFor(int streak)
        {
            return Mathf.Clamp(streak, 0, MaxStreakBonus);
        }

        public static int FoodReward(int bonus)
        {
            return BaseFood + bonus * FoodPerStreak;
        }

        public static int GoldReward(int bonus)
        {
            return BaseGold + bonus;
        }

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

            int bonus = BonusFor(Streak - 1);
            int food = FoodReward(bonus);
            int gold = GoldReward(bonus);
            GameManager.Instance.Resources.Add(ResourceType.Food, food, "daily-reward");
            GameManager.Instance.Resources.Add(ResourceType.Gold, gold, "daily-reward");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(5, "daily-reward");
            GameEvents.Notify(Loc.Get("toast.daily_reward", Loc.Num(food), Loc.Num(gold), Loc.Num(Streak)));
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
