using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public enum AchievementId
    {
        Builder,
        Defender,
        Scavenger,
        FirstNight,
        Survivor,
        Rich,
        BringerOfLife
    }

    [Serializable]
    public class AchievementSaveState
    {
        public int mask;
        public int scalar;
        public int gathering;
    }

    /// <summary>
    /// سیستم دستاوردها. رویدادهای کلیدی بازی (ساخت، دفاع، جمع‌آوری، زنده ماندن) را رصد می‌کند و
    /// هنگام رسیدن به هدف، دستاورد را باز کرده و پاداش می‌دهد. وضعیت در فایل ذخیره ثبت می‌شود.
    /// </summary>
    public sealed class AchievementSystem : MonoBehaviour
    {
        private const int RewardGold = 3;

        /// <summary>آستانه‌های دستاوردهایی که با شمارش پیش می‌روند.</summary>
        public const int GatherTarget = 120;
        public const int RichGoldThreshold = 240;

        private int _unlockedMask;
        private int _totalBuilds;
        private int _totalGathered;

        public void Initialize(GameSaveData save)
        {
            _unlockedMask = save == null ? 0 : save.achievements.mask;
            _totalBuilds = save == null ? 0 : save.achievements.scalar;
            _totalGathered = save == null ? 0 : save.achievements.gathering;
        }

        public void RegisterBuild()
        {
            _totalBuilds++;
            Evaluate(AchievementId.Builder, _totalBuilds >= 2);
            GameManager.Instance.SaveSoon();
        }

        public void RegisterDefeat()
        {
            Evaluate(AchievementId.Defender, true);
        }

        public void RegisterDay(int day)
        {
            Evaluate(AchievementId.FirstNight, day >= 2);
            Evaluate(AchievementId.Survivor, day >= 5);
            RegisterProsperity();
        }

        public void Refresh(GameSaveData save)
        {
            save.achievements.mask = _unlockedMask;
            save.achievements.scalar = _totalBuilds;
            save.achievements.gathering = _totalGathered;
        }

        public bool IsUnlocked(AchievementId id)
        {
            return (_unlockedMask & (1 << (int)id)) != 0;
        }

        /// <summary>جمع‌آوری منبع؛ دستاورد «جمع‌آور» را پیش می‌برد.</summary>
        public void RegisterGather(int amount)
        {
            _totalGathered += Mathf.Max(0, amount);
            Evaluate(AchievementId.Scavenger, _totalGathered >= GatherTarget);
        }

        /// <summary>دستاورد ثروت: وقتی ذخیره‌ی طلا از آستانه گذشت.</summary>
        public void RegisterProsperity()
        {
            ResourceSystem resources = GameManager.Instance.Resources;
            if (resources == null) return;
            Evaluate(AchievementId.Rich, resources.Get(ResourceType.Gold) >= RichGoldThreshold);
        }

        private void Evaluate(AchievementId id, bool threshold)
        {
            if (!threshold || IsUnlocked(id)) return;
            _unlockedMask |= 1 << (int)id;
            string name = GameText.AchievementName(id);
            GameManager.Instance.Resources.Add(ResourceType.Gold, RewardGold, "achievement");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(8, "achievement");
            GameEvents.Notify(Loc.Get("toast.achievement_unlocked", name, Loc.Num(RewardGold)));
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayQuest();
            GameManager.Instance.SaveSoon();
        }
    }
}
