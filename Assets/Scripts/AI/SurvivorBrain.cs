using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// تصمیم‌های شغلی بازمانده را از مدل حرکتی جدا می‌کند تا رفتارها بعداً قابل گسترش باشند.
    /// </summary>
    public sealed class SurvivorBrain
    {
        public SurvivorRole Role { get; private set; }
        public ResourceType PreferredResource { get; private set; }
        public float MovementSpeed { get; private set; }

        public SurvivorBrain(SurvivorRole role)
        {
            Role = role;
            MovementSpeed = role == SurvivorRole.Scout ? 3.2f : 2.8f;
            switch (role)
            {
                case SurvivorRole.Builder: PreferredResource = ResourceType.Stone; break;
                case SurvivorRole.Farmer: PreferredResource = ResourceType.Food; break;
                case SurvivorRole.Scout: PreferredResource = ResourceType.Water; break;
                default: PreferredResource = ResourceType.Wood; break;
            }
        }

        public bool ShouldHelp(float health)
        {
            return Role == SurvivorRole.Medic && health < 92f;
        }

        public float GatheringMultiplier()
        {
            if (Role == SurvivorRole.Farmer || Role == SurvivorRole.Gatherer) return 1.15f;
            if (Role == SurvivorRole.Scout) return 0.9f;
            return 1f;
        }
    }
}
