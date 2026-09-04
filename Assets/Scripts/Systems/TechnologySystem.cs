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
                GameEvents.Notify("این فناوری قبلاً باز شده است.");
                return false;
            }
            if (Points < 2)
            {
                GameEvents.Notify("برای پژوهش به دو امتیاز فناوری نیاز دارید.");
                return false;
            }
            if (technology == TechnologyType.WaterPurification && !IsUnlocked(TechnologyType.Cooperation))
            {
                GameEvents.Notify("ابتدا فناوری همکاری را باز کنید.");
                return false;
            }

            Points -= 2;
            UnlockedMask |= 1 << index;
            if (technology == TechnologyType.Cooperation)
            {
                GameEvents.Notify("همکاری گروهی باز شد؛ بازمانده‌ها سریع‌تر کار می‌کنند.");
            }
            else if (technology == TechnologyType.WaterPurification)
            {
                GameManager.Instance.Resources.Add(ResourceType.Water, 18, "پژوهش تصفیه آب");
                GameEvents.Notify("تصفیه‌ی آب باز شد؛ ذخیره‌ی آب افزایش یافت.");
            }
            else if (technology == TechnologyType.ReinforcedWalls)
            {
                GameEvents.Notify("دیوارهای تقویت‌شده، دفاع پایگاه را بیشتر کردند.");
            }
            else
            {
                GameManager.Instance.Resources.Add(ResourceType.Food, 20, "پژوهش کشت چرخشی");
                GameEvents.Notify("کشت چرخشی باز شد؛ مزرعه پربازده‌تر شد.");
            }
            StateChanged?.Invoke();
            GameManager.Instance.SaveSoon();
            return true;
        }

        public void CopyTo(GameSaveData save)
        {
            save.technologyPoints = Points;
            save.unlockedTechnologyMask = UnlockedMask;
        }
    }

}
