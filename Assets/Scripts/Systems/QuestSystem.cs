using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public enum QuestStatus
    {
        Active,
        Completed,
        Claimed
    }

    [Serializable]
    public class QuestSaveState
    {
        public string id;
        public QuestStatus status;
    }

    /// <summary>
    /// سیستم مأموریت‌ها و مراحل داستانی. هدف‌های کوچک و مرحله‌ای که به بازیکن دلیل ادامه می‌دهند و
    /// پاداش (منبع/طلا/تجربه) می‌دهند. پیشرفت آن‌ها به‌صورت چند مأموریت هم‌زمان پیگیری می‌شود.
    /// </summary>
    public sealed class QuestSystem : MonoBehaviour
    {
        private const int ActiveQuests = 3;

        private readonly List<QuestRuntime> _quests = new List<QuestRuntime>();
        public IReadOnlyList<QuestRuntime> Quests { get { return _quests; } }

        private int _nextIndex;

        public void Initialize(GameSaveData save)
        {
            _nextIndex = save == null ? 0 : Mathf.Max(0, save.questIndex);
            _quests.Clear();
            for (int i = 0; i < ActiveQuests; i++)
            {
                int questId = (_nextIndex + i) % QuestsDefinition.Count;
                QuestRuntime quest = new QuestRuntime(QuestsDefinition.Data(questId), questId);
                _quests.Add(quest);
            }
        }

        public void Refresh(GameSaveData save)
        {
            save.questIndex = _nextIndex;
        }

        public bool TryComplete()
        {
            bool claimed = false;
            for (int i = 0; i < _quests.Count; i++)
            {
                QuestRuntime quest = _quests[i];
                if (quest.Status != QuestStatus.Active) continue;
                if (!IsSatisfied(quest)) continue;
                quest.Status = QuestStatus.Completed;
                claimed = true;
            }
            if (claimed) GameManager.Instance.SaveSoon();
            return claimed;
        }

        public void ClaimAll()
        {
            for (int i = _quests.Count - 1; i >= 0; i--)
            {
                QuestRuntime quest = _quests[i];
                if (quest.Status == QuestStatus.Active || quest.Status == QuestStatus.Claimed) continue;
                GrantReward(quest);
                quest.Status = QuestStatus.Claimed;
                _quests.RemoveAt(i);
                _nextIndex++;
                int questId = (_nextIndex % QuestsDefinition.Count + QuestsDefinition.Count - 1) % QuestsDefinition.Count;
                _quests.Add(new QuestRuntime(QuestsDefinition.Data(questId), questId));
            }
            if (GameManager.Instance.UI != null) GameManager.Instance.UI.RefreshHud();
            GameManager.Instance.SaveSoon();
        }

        private bool IsSatisfied(QuestRuntime quest)
        {
            switch (quest.Definition.kind)
            {
                case QuestKind.Build:
                    return CountBuildings(quest.Definition.targetBuilding) >= quest.Definition.targetCount;
                case QuestKind.Collect:
                    return GameManager.Instance.Resources.Get(quest.Definition.targetResource) >= quest.Definition.targetCount;
                case QuestKind.Train:
                    return GameManager.Instance.Survivors.Count >= quest.Definition.targetCount;
                case QuestKind.Survive:
                    return GameManager.Instance.Clock.Day >= quest.Definition.targetCount;
                default:
                    return false;
            }
        }

        private static int CountBuildings(BuildingType type)
        {
            int count = 0;
            for (int i = 0; i < GameManager.Instance.Construction.Buildings.Count; i++)
            {
                BuildingController building = GameManager.Instance.Construction.Buildings[i];
                if (building != null && building.IsOperational && building.Type == type) count++;
            }
            return count;
        }

        private static void GrantReward(QuestRuntime quest)
        {
            QuestDefinition definition = quest.Definition;
            GameManager.Instance.Resources.Add(definition.rewardResource, definition.rewardAmount, "پاداش مأموریت");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(definition.rewardXp, "مأموریت");
            GameEvents.Notify("مأموریت «" + definition.title + "» انجام شد؛ " + definition.rewardAmount + " " + GameText.ResourceName(definition.rewardResource) + " گرفتید.");
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayQuest();
        }
    }

    public enum QuestKind
    {
        Build,
        Collect,
        Train,
        Survive
    }

    [Serializable]
    public class QuestDefinition
    {
        public string id;
        public string title;
        public string description;
        public QuestKind kind;
        public BuildingType targetBuilding;
        public ResourceType targetResource;
        public int targetCount;
        public ResourceType rewardResource;
        public int rewardAmount;
        public int rewardXp;

        public QuestDefinition(string idValue, string titleValue, string descriptionValue, QuestKind kindValue,
            BuildingType building, ResourceType resource, int count, ResourceType reward, int amount, int xp)
        {
            id = idValue;
            title = titleValue;
            description = descriptionValue;
            kind = kindValue;
            targetBuilding = building;
            targetResource = resource;
            targetCount = count;
            rewardResource = reward;
            rewardAmount = amount;
            rewardXp = xp;
        }
    }

    public sealed class QuestRuntime
    {
        public QuestDefinition Definition { get; private set; }
        public QuestStatus Status { get; set; }
        public int DefinitionIndex { get; private set; }

        public QuestRuntime(QuestDefinition definition, int index)
        {
            Definition = definition;
            Status = QuestStatus.Active;
            DefinitionIndex = index;
        }
    }

    /// <summary>
    /// تعریف مأموریت‌های بازی. در محصول نهایی این فهرست می‌تواند از فایل JSON یا Addressables خوانده شود.
    /// </summary>
    public static class QuestsDefinition
    {
        private static readonly List<QuestDefinition> _data = new List<QuestDefinition>
        {
            new QuestDefinition("q_house", "اولین پناهگاه", "یک خانه برای گروه بسازید.", QuestKind.Build, BuildingType.House, ResourceType.Wood, 1, ResourceType.Food, 15, 6),
            new QuestDefinition("q_wood", "تأمین چوب", "ذخیره‌ی چوب را به ۱۲۰ برسانید.", QuestKind.Collect, BuildingType.Camp, ResourceType.Wood, 120, ResourceType.Gold, 4, 5),
            new QuestDefinition("q_storage", "انبار امن", "یک انبار بسازید تا منابع در امان بمانند.", QuestKind.Build, BuildingType.Storage, ResourceType.Wood, 1, ResourceType.Wood, 20, 6),
            new QuestDefinition("q_guard", "نگهبان تازه", "گروه را به ۷ نفر برسانید.", QuestKind.Train, BuildingType.Camp, ResourceType.Food, 7, ResourceType.Water, 20, 8),
            new QuestDefinition("q_tower", "برج دیده‌بانی", "برای دفاع شبانه یک برج بسازید.", QuestKind.Build, BuildingType.WatchTower, ResourceType.Wood, 1, ResourceType.Gold, 8, 8),
            new QuestDefinition("q_day3", "سه روز مقاومت", "تا روز سوم زنده بمانید.", QuestKind.Survive, BuildingType.Camp, ResourceType.Food, 3, ResourceType.Food, 30, 10),
            new QuestDefinition("q_farm", "مزرعه‌ی نو", "یک مزرعه راه‌اندازی کنید.", QuestKind.Build, BuildingType.Farm, ResourceType.Wood, 1, ResourceType.Energy, 10, 6),
            new QuestDefinition("q_workshop", "کارگاه تولید", "کارگاه را فعال کنید.", QuestKind.Build, BuildingType.Workshop, ResourceType.Wood, 1, ResourceType.Gold, 6, 7)
        };

        public static int Count { get { return _data.Count; } }

        public static QuestDefinition Data(int index)
        {
            if (index < 0 || index >= _data.Count) return _data[0];
            return _data[index];
        }
    }
}
