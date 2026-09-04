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
            data.survivors.Add(CreateSurvivor("سارا", SurvivorRole.Gatherer, new Vector3(-2f, 0f, -1f)));
            data.survivors.Add(CreateSurvivor("یونس", SurvivorRole.Builder, new Vector3(2f, 0f, -1f)));
            data.survivors.Add(CreateSurvivor("آوا", SurvivorRole.Medic, new Vector3(-1f, 0f, 2f)));
            data.survivors.Add(CreateSurvivor("کاوه", SurvivorRole.Guard, new Vector3(2f, 0f, 2f)));
            data.survivors.Add(CreateSurvivor("نورا", SurvivorRole.Scout, new Vector3(-3f, 0f, 2f)));
            data.survivors.Add(CreateSurvivor("سام", SurvivorRole.Farmer, new Vector3(3f, 0f, 1f)));
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

    public static class GameText
    {
        public static string ResourceName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return "چوب";
                case ResourceType.Stone: return "سنگ";
                case ResourceType.Food: return "غذا";
                case ResourceType.Gold: return "طلا";
                case ResourceType.Energy: return "انرژی";
                case ResourceType.Water: return "آب";
                default: return "منبع";
            }
        }

        public static string RoleName(SurvivorRole role)
        {
            switch (role)
            {
                case SurvivorRole.Gatherer: return "جمع‌آور";
                case SurvivorRole.Builder: return "سازنده";
                case SurvivorRole.Medic: return "پزشک";
                case SurvivorRole.Guard: return "نگهبان";
                case SurvivorRole.Scout: return "پیشاهنگ";
                case SurvivorRole.Farmer: return "کشاورز";
                default: return "بازمانده";
            }
        }

        public static string BuildingName(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Camp: return "اردوگاه";
                case BuildingType.House: return "خانه";
                case BuildingType.Storage: return "انبار";
                case BuildingType.Farm: return "مزرعه";
                case BuildingType.WatchTower: return "برج دیده‌بانی";
                case BuildingType.Workshop: return "کارگاه";
                case BuildingType.Wall: return "دیوار دفاعی";
                case BuildingType.SolarStation: return "نیروگاه خورشیدی";
                default: return "ساختمان";
            }
        }

        public static string WeatherName(WeatherType type)
        {
            switch (type)
            {
                case WeatherType.Clear: return "آسمان صاف";
                case WeatherType.Rain: return "بارانی";
                case WeatherType.Fog: return "مه‌آلود";
                case WeatherType.Storm: return "توفانی";
                default: return "آرام";
            }
        }
    }
}
