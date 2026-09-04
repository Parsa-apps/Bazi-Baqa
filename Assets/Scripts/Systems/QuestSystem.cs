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

        /// <summary>تعداد مأموریت‌هایی که تا امروز صادر شده‌اند (شاخصِ تعریف بعدی برای صدور).</summary>
        private int _issued;

        public void Initialize(GameSaveData save)
        {
            // «تعداد صادرشده» را نگه می‌داریم تا بعد از گرفتن پاداش، مأموریتِ تکراری صادر نشود.
            _issued = save == null ? ActiveQuests : Mathf.Max(ActiveQuests, save.questIndex);
            _quests.Clear();
            // پنجره‌ی فعال = چند مأموریتِ آخرِ صادرشده؛ همه‌ی آن‌ها تعاریف متمایز هستند.
            for (int i = 0; i < ActiveQuests; i++)
            {
                int questId = PositiveMod(_issued - ActiveQuests + i, QuestsDefinition.Count);
                QuestRuntime quest = new QuestRuntime(QuestsDefinition.Data(questId), questId);
                _quests.Add(quest);
            }
        }

        public void Refresh(GameSaveData save)
        {
            save.questIndex = _issued;
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
                // همیشه از «نشانگرِ صدور» یک تعریفِ تازه و متمایز صادر می‌کنیم تا پاداش تکراری
                // (farm) ممکن نباشد و مأموریتِ ادعاشده دوباره ظاهر نشود.
                int questId = PositiveMod(_issued, QuestsDefinition.Count);
                _issued++;
                _quests.Add(new QuestRuntime(QuestsDefinition.Data(questId), questId));
            }
            if (GameManager.Instance.UI != null) GameManager.Instance.UI.RefreshHud();
            GameManager.Instance.SaveSoon();
        }

        private static int PositiveMod(int value, int modulus)
        {
            if (modulus <= 0) return 0;
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
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
            GameManager.Instance.Resources.Add(definition.rewardResource, definition.rewardAmount, "quest-reward");
            if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(definition.rewardXp, "quest");
            GameEvents.Notify(Loc.Get("toast.quest_done", definition.Title,
                GameText.ResourceAmount(definition.rewardResource, definition.rewardAmount)));
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

    /// <summary>
    /// تعریف یک مأموریت. نکته‌ی مهم برای بومی‌سازی: عنوان و توضیح در کد نوشته نمی‌شوند،
    /// بلکه از جدول با کلیدهای quest.&lt;id&gt;.title / quest.&lt;id&gt;.description خوانده می‌شوند.
    /// </summary>
    [Serializable]
    public class QuestDefinition
    {
        public string id;
        public QuestKind kind;
        public BuildingType targetBuilding;
        public ResourceType targetResource;
        public int targetCount;
        public ResourceType rewardResource;
        public int rewardAmount;
        public int rewardXp;

        /// <summary>کلیدهای متن در جدول بومی‌سازی.</summary>
        public string TitleKey { get { return GameText.QuestKey(id, "title"); } }
        public string DescriptionKey { get { return GameText.QuestKey(id, "description"); } }

        /// <summary>متن‌ها هنگام نمایش از جدول خوانده می‌شوند تا تغییر زبان زنده اعمال شود.</summary>
        public string Title { get { return Loc.Get(TitleKey); } }
        public string Description { get { return Loc.Get(DescriptionKey); } }

        public QuestDefinition(string idValue, QuestKind kindValue,
            BuildingType building, ResourceType resource, int count, ResourceType reward, int amount, int xp)
        {
            id = idValue;
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
    /// فهرست مأموریت‌های بازی. داده‌ی عددی این‌جاست و متن‌ها از جدول بومی‌سازی خوانده می‌شوند
    /// (کلید: quest.&lt;id&gt;.title و quest.&lt;id&gt;.description). در محصول نهایی می‌توان کلِ این فهرست را
    /// از فایل JSON یا Addressables بارگذاری کرد.
    /// </summary>
    public static class QuestsDefinition
    {
        private static readonly List<QuestDefinition> _data = new List<QuestDefinition>
        {
            new QuestDefinition("q_house", QuestKind.Build, BuildingType.House, ResourceType.Wood, 1, ResourceType.Food, 15, 6),
            new QuestDefinition("q_wood", QuestKind.Collect, BuildingType.Camp, ResourceType.Wood, 120, ResourceType.Gold, 4, 5),
            new QuestDefinition("q_storage", QuestKind.Build, BuildingType.Storage, ResourceType.Wood, 1, ResourceType.Wood, 20, 6),
            new QuestDefinition("q_guard", QuestKind.Train, BuildingType.Camp, ResourceType.Food, 7, ResourceType.Water, 20, 8),
            new QuestDefinition("q_tower", QuestKind.Build, BuildingType.WatchTower, ResourceType.Wood, 1, ResourceType.Gold, 8, 8),
            new QuestDefinition("q_day3", QuestKind.Survive, BuildingType.Camp, ResourceType.Food, 3, ResourceType.Food, 30, 10),
            new QuestDefinition("q_farm", QuestKind.Build, BuildingType.Farm, ResourceType.Wood, 1, ResourceType.Energy, 10, 6),
            new QuestDefinition("q_workshop", QuestKind.Build, BuildingType.Workshop, ResourceType.Wood, 1, ResourceType.Gold, 6, 7)
        };

        public static int Count { get { return _data.Count; } }

        public static QuestDefinition Data(int index)
        {
            if (index < 0 || index >= _data.Count) return _data[0];
            return _data[index];
        }
    }
}
