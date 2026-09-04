using System;
using UnityEngine;

namespace BaziBaqa
{
    public enum TechnologyType
    {
        Cooperation = 0,
        WaterPurification = 1,
        ReinforcedWalls = 2,
        FieldRotation = 3
    }

    public sealed class TechnologySystem : MonoBehaviour
    {
        /// <summary>هزینه‌ی هر پژوهش (امتیاز فناوری).</summary>
        public const int ResearchCost = 2;

        public int Points { get; private set; }
        public int UnlockedMask { get; private set; }
        public event Action StateChanged;

        public void Initialize(GameSaveData save)
        {
            Points = Mathf.Max(0, save == null ? 0 : save.technologyPoints);
            UnlockedMask = save == null ? 0 : save.unlockedTechnologyMask;
        }

        public bool IsUnlocked(TechnologyType technology)
        {
            return (UnlockedMask & (1 << (int)technology)) != 0;
        }

        public void AddPoints(int amount)
        {
            Points = Mathf.Max(0, Points + amount);
            StateChanged?.Invoke();
        }

        public bool Research(TechnologyType technology)
        {
            int index = (int)technology;
            if (IsUnlocked(technology))
            {
                GameEvents.Notify(Loc.Get("toast.tech_already"));
                return false;
            }
            if (Points < ResearchCost)
            {
                GameEvents.Notify(Loc.Get("toast.tech_points", Loc.Num(ResearchCost)));
                return false;
            }
            if (technology == TechnologyType.WaterPurification && !IsUnlocked(TechnologyType.Cooperation))
            {
                GameEvents.Notify(Loc.Get("toast.tech_need_prerequisite"));
                return false;
            }

            Points -= ResearchCost;
            UnlockedMask |= 1 << index;
            if (technology == TechnologyType.Cooperation)
            {
                NotifyUnlocked(technology);
            }
            else if (technology == TechnologyType.WaterPurification)
            {
                GameManager.Instance.Resources.Add(ResourceType.Water, 18, "research");
                NotifyUnlocked(technology);
            }
            else if (technology == TechnologyType.ReinforcedWalls)
            {
                NotifyUnlocked(technology);
            }
            else
            {
                GameManager.Instance.Resources.Add(ResourceType.Food, 20, "research");
                NotifyUnlocked(technology);
            }
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(12, "research");
            StateChanged?.Invoke();
            GameManager.Instance.SaveSoon();
            return true;
        }

        /// <summary>پیام باز شدن فناوری؛ متن از کلیدِ همان فناوری خوانده می‌شود.</summary>
        private void NotifyUnlocked(TechnologyType technology)
        {
            GameEvents.Notify(Loc.Get(GameText.TechnologyKey(technology) + ".unlocked"));
        }

        public void CopyTo(GameSaveData save)
        {
            save.technologyPoints = Points;
            save.unlockedTechnologyMask = UnlockedMask;
        }
    }

}
