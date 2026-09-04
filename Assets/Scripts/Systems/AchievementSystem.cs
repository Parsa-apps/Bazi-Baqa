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
    }

    /// <summary>
    /// سیستم دستاوردها. رویدادهای کلیدی بازی (ساخت، دفاع، جمع‌آوری، زنده ماندن) را رصد می‌کند و
    /// هنگام رسیدن به هدف، دستاورد را باز کرده و پاداش می‌دهد. وضعیت در فایل ذخیره ثبت می‌شود.
    /// </summary>
    public sealed class AchievementSystem : MonoBehaviour
    {
        private int _unlockedMask;
        private int _totalBuilds;

        public void Initialize(GameSaveData save)
        {
            _unlockedMask = save == null ? 0 : save.achievements.mask;
            _totalBuilds = save == null ? 0 : save.achievements.scalar;
        }

        public void RegisterBuild()
        {
            _totalBuilds++;
            Evaluate(AchievementId.Builder, _totalBuilds >= 2, "سازنده");
            GameManager.Instance.SaveSoon();
        }

        public void RegisterDefeat()
        {
            Evaluate(AchievementId.Defender, true, "مدافع اردوگاه");
        }

        public void RegisterDay(int day)
        {
            Evaluate(AchievementId.FirstNight, day >= 2, "دومین روز");
            Evaluate(AchievementId.Survivor, day >= 5, "بازمانده‌ی ماهر");
        }

        public void Refresh(GameSaveData save)
        {
            save.achievements.mask = _unlockedMask;
            save.achievements.scalar = _totalBuilds;
        }

        public bool IsUnlocked(AchievementId id)
        {
            return (_unlockedMask & (1 << (int)id)) != 0;
        }

        private void Evaluate(AchievementId id, bool threshold, string name)
        {
            if (!threshold || IsUnlocked(id)) return;
            _unlockedMask |= 1 << (int)id;
            GameManager.Instance.Resources.Add(ResourceType.Gold, 3, "دستاورد: " + name);
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(8, "دستاورد");
            GameEvents.Notify("دستاورد «" + name + "» باز شد؛ ۳ طلا جایزه گرفتید.");
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayQuest();
            GameManager.Instance.SaveSoon();
        }
    }
}
