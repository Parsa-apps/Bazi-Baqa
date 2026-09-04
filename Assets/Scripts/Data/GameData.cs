using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public enum ResourceType
    {
        Wood,
        Stone,
        Food,
        Gold,
        Energy,
        Water
    }

    public enum SurvivorRole
    {
        Gatherer,
        Builder,
        Medic,
        Guard,
        Scout,
        Farmer
    }

    public enum SurvivorState
    {
        Idle,
        Gathering,
        Returning,
        Building,
        Healing,
        Guarding,
        Resting,
        Eating,
        Fleeing,
        Injured
    }

    public enum BuildingType
    {
        Camp,
        House,
        Storage,
        Farm,
        WatchTower,
        Workshop,
        Wall,
        SolarStation
    }

    public enum WeatherType
    {
        Clear,
        Rain,
        Fog,
        Storm
    }

    [Serializable]
    public class ResourceState
    {
        public int wood = 90;
        public int stone = 60;
        public int food = 105;
        public int gold = 20;
        public int energy = 45;
        public int water = 90;

        public ResourceState Clone()
        {
            return new ResourceState
            {
                wood = wood,
                stone = stone,
                food = food,
                gold = gold,
                energy = energy,
                water = water
            };
        }

        public int Get(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return wood;
                case ResourceType.Stone: return stone;
                case ResourceType.Food: return food;
                case ResourceType.Gold: return gold;
                case ResourceType.Energy: return energy;
                case ResourceType.Water: return water;
                default: return 0;
            }
        }

        public void Set(ResourceType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type)
            {
                case ResourceType.Wood: wood = value; break;
                case ResourceType.Stone: stone = value; break;
                case ResourceType.Food: food = value; break;
                case ResourceType.Gold: gold = value; break;
                case ResourceType.Energy: energy = value; break;
                case ResourceType.Water: water = value; break;
            }
        }

        public void Add(ResourceType type, int amount)
        {
            Set(type, Get(type) + amount);
        }
    }

    [Serializable]
    public class SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3() { }

        public SerializableVector3(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public class SurvivorSaveData
    {
        public string id;
        public string displayName;
        public SurvivorRole role;
        public SurvivorState state;
        public float health = 100f;
        public float hunger = 100f;
        public float thirst = 100f;
        public float morale = 75f;
        public SerializableVector3 position = new SerializableVector3(Vector3.zero);
        public bool alive = true;
    }

    [Serializable]
    public class BuildingSaveData
    {
        public string id;
        public BuildingType type;
        public int level = 1;
        public float health = 100f;
        public SerializableVector3 position = new SerializableVector3(Vector3.zero);
    }

    [Serializable]
    public class SettingsSaveData
    {
        public bool soundEnabled = true;
        public bool vibrationEnabled = true;
        public bool tutorialCompleted;
    }

    [Serializable]
    public class EquipmentSaveState
    {
        public int tool = 1;
        public int weapon = 1;
        public int armor = 1;
    }

    [Serializable]
    public class StorySaveState
    {
        public int lastDecisionDay;
    }

    [Serializable]
    public class RaidSaveState
    {
        public int lastRaidDay;
        public int wins;
        public int losses;
    }

    [Serializable]
    public class GameSaveData
    {
    public int saveVersion = 3;
    public int seed = 14729;
    public int day = 1;
    public float dayTime = 0.28f;
    public int playerLevel = 1;
    public int playerXp;
    public int technologyPoints;
    public int unlockedTechnologyMask;
    public ResourceState resources = new ResourceState();
    public SettingsSaveData settings = new SettingsSaveData();
    public List<SurvivorSaveData> survivors = new List<SurvivorSaveData>();
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    public int questIndex;
    public AchievementSaveState achievements = new AchievementSaveState();
    public DailyRewardSaveState dailyReward = new DailyRewardSaveState();
    public EquipmentSaveState equipment = new EquipmentSaveState();
    public StorySaveState story = new StorySaveState();
    public RaidSaveState raid = new RaidSaveState();

        public static GameSaveData CreateNew(int newSeed)
        {
            GameSaveData data = new GameSaveData();
            data.seed = newSeed;
            // سه مأموریتِ اول به‌عنوان پنجره‌ی شروعِ متمایز صادر شده‌اند (شاخص‌های ۰،۱،۲).
            data.questIndex = 3;
            for (int i = 0; i < GameText.StartingSurvivorCount; i++)
            {
                SurvivorRole[] roles = { SurvivorRole.Gatherer, SurvivorRole.Builder, SurvivorRole.Medic, SurvivorRole.Guard, SurvivorRole.Scout, SurvivorRole.Farmer };
                Vector3[] offsets =
                {
                    new Vector3(-2f, 0f, -1f), new Vector3(2f, 0f, -1f), new Vector3(-1f, 0f, 2f),
                    new Vector3(2f, 0f, 2f), new Vector3(-3f, 0f, 2f), new Vector3(3f, 0f, 1f)
                };
                data.survivors.Add(CreateSurvivor(GameText.SurvivorNameAt(i), roles[i % roles.Length], offsets[i % offsets.Length]));
            }
            data.buildings.Add(new BuildingSaveData
            {
                id = "camp-main",
                type = BuildingType.Camp,
                level = 1,
                health = 100f,
                position = new SerializableVector3(new Vector3(0f, 0f, 0f))
            });
            data.buildings.Add(new BuildingSaveData
            {
                id = "storage-main",
                type = BuildingType.Storage,
                level = 1,
                health = 100f,
                position = new SerializableVector3(new Vector3(-4.5f, 0f, 0f))
            });
            return data;
        }

        private static SurvivorSaveData CreateSurvivor(string name, SurvivorRole role, Vector3 position)
        {
            return new SurvivorSaveData
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = name,
                role = role,
                state = SurvivorState.Idle,
                position = new SerializableVector3(position)
            };
        }
    }

    /// <summary>
    /// نام‌ها و قالب‌های متنیِ داده‌محور. هیچ رشته‌ای اینجا نوشته نمی‌شود؛ همه از
    /// LocalizationManager خوانده می‌شوند تا افزودن زبان تازه فقط یک ویرایشِ جدول باشد.
    /// </summary>
    public static class GameText
    {
        /// <summary>تعداد نام‌های آماده در استخرِ نام (survivor.name.0 … survivor.name.N).</summary>
        public const int SurvivorNameCount = 12;
        public const int StartingSurvivorCount = 6;
        public const int RecruitNameOffset = 6;

        // ---------- کلیدها (برای تست و ممیزی) ----------

        public static string ResourceKey(ResourceType type) { return "resource." + type.ToString().ToLowerInvariant(); }
        public static string BuildingKey(BuildingType type) { return "building." + type.ToString().ToLowerInvariant(); }
        public static string RoleKey(SurvivorRole role) { return "role." + role.ToString().ToLowerInvariant(); }
        public static string WeatherKey(WeatherType type) { return "weather." + type.ToString().ToLowerInvariant(); }
        public static string TechnologyKey(TechnologyType type) { return "technology." + type.ToString().ToLowerInvariant(); }
        public static string AchievementKey(AchievementId id) { return "achievement." + id.ToString().ToLowerInvariant(); }
        public static string StateKey(SurvivorState state) { return "status." + state.ToString().ToLowerInvariant(); }
        public static string EquipmentKey(EquipmentType type) { return "equipment." + type.ToString().ToLowerInvariant(); }
        public static string QuestKey(string questId, string part) { return "quest." + questId + "." + part; }
        public static string StoryKey(string storyId, string part) { return "story." + storyId + "." + part; }

        // ---------- نام‌ها ----------

        public static string ResourceName(ResourceType type) { return Loc.Get(ResourceKey(type)); }
        public static string BuildingName(BuildingType type) { return Loc.Get(BuildingKey(type)); }
        public static string RoleName(SurvivorRole role) { return Loc.Get(RoleKey(role)); }
        public static string WeatherName(WeatherType type) { return Loc.Get(WeatherKey(type)); }
        public static string TechnologyName(TechnologyType type) { return Loc.Get(TechnologyKey(type)); }
        public static string AchievementName(AchievementId id) { return Loc.Get(AchievementKey(id)); }
        public static string EquipmentName(EquipmentType type) { return Loc.Get(EquipmentKey(type)); }
        public static string StateName(SurvivorState state) { return Loc.Get(StateKey(state)); }

        /// <summary>نامِ بازمانده از استخرِ نام‌های جدول (به‌جای رشته‌های سخت‌کدشده در کد).</summary>
        public static string SurvivorNameAt(int index)
        {
            int wrapped = index % SurvivorNameCount;
            if (wrapped < 0) wrapped += SurvivorNameCount;
            return Loc.Get("survivor.name." + wrapped);
        }

        // ---------- قالب‌های مشترک ----------

        /// <summary>هزینه‌ی ساخت/ارتقا به شکل «چوب ۲۰  •  سنگ ۱۰» (برچسب «رایگان» اگر خالی باشد).</summary>
        public static string CostLine(System.Collections.Generic.IEnumerable<ResourceCost> costs)
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            if (costs != null)
            {
                foreach (ResourceCost cost in costs) parts.Add(ResourceAmount(cost.type, cost.amount));
            }
            string joined = Join(parts);
            return string.IsNullOrEmpty(joined) ? Loc.Get("label.free") : joined;
        }

        /// <summary>همان قالب برای هزینه‌های ارتقای تجهیزات.</summary>
        public static string CostLine(System.Collections.Generic.IEnumerable<EquipmentUpgradeCost> costs)
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            if (costs != null)
            {
                foreach (EquipmentUpgradeCost cost in costs)
                {
                    if (cost == null) continue;
                    parts.Add(ResourceAmount(cost.type, cost.amount));
                }
            }
            string joined = Join(parts);
            return string.IsNullOrEmpty(joined) ? Loc.Get("label.free") : joined;
        }

        /// <summary>برچسب شناورِ ساختمان در جهان: «نام  «۲»».</summary>
        public static string BuildingLabel(BuildingType type, int level)
        {
            return Loc.Get("format.building_label", BuildingName(type), Loc.Num(level));
        }

        /// <summary>خط «مأموریت فعال» در نوار پایین.</summary>
        public static string QuestLine(string title)
        {
            return Loc.Get("format.quest_line", title);
        }

        /// <summary>یک سطر «منبع + مقدار» مثل «چوب ۲۰».</summary>
        public static string ResourceAmount(ResourceType type, int amount)
        {
            return Loc.Get("format.cost_entry", ResourceName(type), Loc.Num(amount));
        }

        /// <summary>پیوند چند تکه‌ی متنی با جداکننده‌ی محلی («  •  » در فارسی).</summary>
        public static string Join(params string[] parts)
        {
            System.Collections.Generic.List<string> kept = new System.Collections.Generic.List<string>();
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!string.IsNullOrEmpty(parts[i])) kept.Add(parts[i]);
                }
            }
            return Join(kept);
        }

        private static string Join(System.Collections.Generic.IEnumerable<string> parts)
        {
            System.Collections.Generic.List<string> kept = new System.Collections.Generic.List<string>();
            if (parts != null)
            {
                foreach (string part in parts)
                {
                    if (!string.IsNullOrEmpty(part)) kept.Add(part);
                }
            }
            if (kept.Count == 0) return string.Empty;
            return string.Join(Loc.Get("format.cost_join"), kept.ToArray());
        }
    }
}
