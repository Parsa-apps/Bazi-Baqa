using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public enum EquipmentType
    {
        Tool,
        Weapon,
        Armor
    }

    [Serializable]
    public class EquipmentUpgradeCost
    {
        public ResourceType type;
        public int amount;

        public EquipmentUpgradeCost(ResourceType resourceType, int resourceAmount)
        {
            type = resourceType;
            amount = resourceAmount;
        }
    }

    /// <summary>
    /// سیستم تجهیزات: ابزارها، سلاح‌ها و زره که با ارتقا، اثر مستقیم بر جمع‌آوری، حمله و دفاع دارند.
    /// هر ارتقا به «مرحله‌ی گروه» و منابع نیاز دارد و جلوه‌ی پیشرفت در بازی را ملموس می‌کند.
    /// </summary>
    public sealed class EquipmentSystem : MonoBehaviour
    {
        private EquipmentSaveState _state = new EquipmentSaveState();

        public int ToolLevel { get { return _state.tool; } }
        public int WeaponLevel { get { return _state.weapon; } }
        public int ArmorLevel { get { return _state.armor; } }

        // اثرها: هر چه سطح بالاتر، اثر قوی‌تر (اما محدود تا بازی متعادل بماند)
        public float ToolGatherBonus { get { return Mathf.Clamp((ToolLevel - 1) * 0.06f, 0f, 0.5f); } }
        public float WeaponDamageBonus { get { return Mathf.Clamp((WeaponLevel - 1) * 1.1f, 0f, 6f); } }
        public float ArmorReduction { get { return Mathf.Clamp((ArmorLevel - 1) * 0.08f, 0f, 0.45f); } }

        public void Initialize(GameSaveData save)
        {
            _state = save != null && save.equipment != null ? save.equipment : new EquipmentSaveState();
            if (_state.tool < 1) _state.tool = 1;
            if (_state.weapon < 1) _state.weapon = 1;
            if (_state.armor < 1) _state.armor = 1;
        }

        public int Level(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Tool: return ToolLevel;
                case EquipmentType.Weapon: return WeaponLevel;
                default: return ArmorLevel;
            }
        }

        public string Name(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Tool: return "ابزار (کشاورزی/جمع‌آوری)";
                case EquipmentType.Weapon: return "سلاح (دفاع/حمله)";
                default: return "زره (کاهش آسیب)";
            }
        }

        public bool CanUpgrade(EquipmentType type, out string reason)
        {
            int level = Level(type);
            if (level >= 5)
            {
                reason = "این تجهیزات به بالاترین سطح رسیده است.";
                return false;
            }
            int requiredLevel = level + 1;
            if (GameManager.Instance.Progression != null && GameManager.Instance.Progression.Level < requiredLevel)
            {
                reason = "برای این ارتقا به مرحله‌ی گروه " + GameClock.ToPersianDigits(requiredLevel.ToString()) + " نیاز دارید.";
                return false;
            }
            List<EquipmentUpgradeCost> costs = GetUpgradeCosts(type, level);
            for (int i = 0; i < costs.Count; i++)
            {
                if (GameManager.Instance.Resources.Get(costs[i].type) < costs[i].amount)
                {
                    reason = "منابع کافی نیست؛ به " + GameText.ResourceName(costs[i].type) + " بیشتری نیاز دارید.";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        public bool Upgrade(EquipmentType type)
        {
            if (!CanUpgrade(type, out string reason))
            {
                GameEvents.Notify("ارتقا ممکن نیست: " + reason);
                return false;
            }
            int level = Level(type);
            List<EquipmentUpgradeCost> costs = GetUpgradeCosts(type, level);
            for (int i = 0; i < costs.Count; i++)
            {
                if (!GameManager.Instance.Resources.TrySpend(costs[i].type, costs[i].amount)) return false;
            }
            switch (type)
            {
                case EquipmentType.Tool: _state.tool++; break;
                case EquipmentType.Weapon: _state.weapon++; break;
                default: _state.armor++; break;
            }
            GameEvents.Notify(Name(type) + " به سطح " + GameClock.ToPersianDigits(Level(type).ToString()) + " ارتقا یافت.");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(10, "ارتقای تجهیزات");
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayBuild();
            GameManager.Instance.SaveSoon();
            return true;
        }

        public static List<EquipmentUpgradeCost> GetUpgradeCosts(EquipmentType type, int currentLevel)
        {
            int level = Mathf.Max(1, currentLevel);
            return new List<EquipmentUpgradeCost>
            {
                new EquipmentUpgradeCost(ResourceType.Wood, 20 * level),
                new EquipmentUpgradeCost(ResourceType.Stone, 14 * level),
                new EquipmentUpgradeCost(ResourceType.Gold, 3 * level)
            };
        }

        public void CopyTo(GameSaveData save)
        {
            if (save == null) return;
            save.equipment = _state;
        }
    }
}
