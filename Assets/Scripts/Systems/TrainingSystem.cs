using UnityEngine;

namespace BaziBaqa
{
    public sealed class TrainingSystem : MonoBehaviour
    {
        /// <summary>حداکثر تعداد اعضای گروه.</summary>
        public const int MaximumGroupSize = 10;

        /// <summary>هزینه‌ی تربیت یک نگهبان؛ یک‌جا تعریف می‌شود تا UI و منطق یکی باشند.</summary>
        public static readonly ResourceCost[] GuardCost =
        {
            new ResourceCost(ResourceType.Food, 12),
            new ResourceCost(ResourceType.Energy, 5),
            new ResourceCost(ResourceType.Gold, 3)
        };

        /// <summary>خط هزینه برای پنجره‌ی تربیت (متن از جدول بومی‌سازی).</summary>
        public static string CostLine()
        {
            return GameText.CostLine(GuardCost);
        }

        public void Initialize()
        {
            // آموزش نیرو به‌صورت تقاضامحور انجام می‌شود تا در منوی اصلی هیچ هزینه‌ای مصرف نشود.
        }

        public bool CanTrain
        {
            get
            {
                return GameManager.Instance != null && GameManager.Instance.Construction.FindByType(BuildingType.Workshop) != null && GameManager.Instance.AliveSurvivorCount() < MaximumGroupSize;
            }
        }

        public void TrainGuard()
        {
            if (GameManager.Instance.Construction.FindByType(BuildingType.Workshop) == null)
            {
                GameEvents.Notify(Loc.Get("toast.train_needs_workshop"));
                return;
            }
            if (GameManager.Instance.AliveSurvivorCount() >= MaximumGroupSize)
            {
                GameEvents.Notify(Loc.Get("toast.train_group_full", Loc.Num(MaximumGroupSize)));
                return;
            }

            if (!GameManager.Instance.Resources.TrySpend(GuardCost))
            {
                GameEvents.Notify(Loc.Get("toast.train_no_resources"));
                return;
            }
            GameManager.Instance.RecruitSurvivor(SurvivorRole.Guard);
            if (GameManager.Instance.Quests != null) GameManager.Instance.Quests.TryComplete();
            GameManager.Instance.Audio.PlayBuild();
        }
    }
}
