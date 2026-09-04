using UnityEngine;

namespace BaziBaqa
{
    public sealed class TrainingSystem : MonoBehaviour
    {
        public int MaximumGroupSize { get { return 10; } }

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
                GameEvents.Notify("برای تربیت نیرو ابتدا یک کارگاه بسازید.");
                return;
            }
            if (GameManager.Instance.AliveSurvivorCount() >= MaximumGroupSize)
            {
                GameEvents.Notify("گروه به ظرفیت ده نفر رسیده است.");
                return;
            }

            ResourceCost[] costs =
            {
                new ResourceCost(ResourceType.Food, 12),
                new ResourceCost(ResourceType.Energy, 5),
                new ResourceCost(ResourceType.Gold, 3)
            };
            if (!GameManager.Instance.Resources.TrySpend(costs))
            {
                GameEvents.Notify("برای تربیت نیرو غذا، انرژی و طلا کافی نیست.");
                return;
            }
            GameManager.Instance.RecruitSurvivor(SurvivorRole.Guard);
            GameManager.Instance.Audio.PlayBuild();
        }
    }
}
