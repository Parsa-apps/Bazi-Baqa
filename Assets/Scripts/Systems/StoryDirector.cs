using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public enum StoryOutcome
    {
        Resources,
        Morale,
        Heal
    }

    [Serializable]
    public class StoryChoice
    {
        /// <summary>شناسه‌ی رویداد و انتخاب؛ برای ساختن کلیدهای متن در جدول بومی‌سازی.</summary>
        public string eventId;
        public string id;
        public StoryOutcome outcome;
        public ResourceType resource;
        public int amount;
        public float moraleDelta;

        public StoryChoice(string eventIdValue, string idValue, StoryOutcome outcomeValue, ResourceType resourceValue, int amountValue, float moraleValue)
        {
            eventId = eventIdValue;
            id = idValue;
            outcome = outcomeValue;
            resource = resourceValue;
            amount = amountValue;
            moraleDelta = moraleValue;
        }

        /// <summary>متن‌ها هنگام نمایش از جدول خوانده می‌شوند تا زبان زنده عوض شود.</summary>
        public string Title { get { return StoryText.Title(eventId, id); } }
        public string Hint { get { return StoryText.Hint(eventId, id); } }
    }

    [Serializable]
    public class StoryEvent
    {
        public int day;
        public string id;
        public List<StoryChoice> choices;

        public StoryEvent(int dayValue, string idValue, List<StoryChoice> choicesValue)
        {
            day = dayValue;
            id = idValue;
            choices = choicesValue;
        }

        /// <summary>متن‌ها از جدول بومی‌سازی خوانده می‌شوند (story.&lt;id&gt;.title / .body).</summary>
        public string Title { get { return Loc.Get(GameText.StoryKey(id, "title")); } }
        public string Body { get { return Loc.Get(GameText.StoryKey(id, "body")); } }
    }

    /// <summary>کلیدهای متن یک انتخاب داستانی: story.&lt;storyId&gt;.&lt;choiceId&gt;.title / .hint.</summary>
    public static class StoryText
    {
        public static string Title(string eventId, string choiceId)
        {
            return Loc.Get("story." + eventId + "." + choiceId + ".title");
        }

        public static string Hint(string eventId, string choiceId)
        {
            return Loc.Get("story." + eventId + "." + choiceId + ".hint");
        }
    }

    /// <summary>
    /// مدیر داستان: در روزهای کلیدیِ سفر، یک تصمیم‌گیری تأثیرگذار به بازیکن ارائه می‌دهد.
    /// هر انتخاب پیامد واقعی دارد (منبع، روحیه یا درمان) و فقط یک‌بار در آن روز ظاهر می‌شود.
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        private int _lastDecisionDay;

        public bool HasPendingDecision { get; private set; }
        public StoryEvent Pending { get; private set; }

        public void Initialize(GameSaveData save)
        {
            // برای سازگاری با ذخیره‌های قدیمی، اگر بخش «داستان» وجود نداشت ارزش پیش‌فرض می‌گیریم.
            _lastDecisionDay = save != null && save.story != null ? save.story.lastDecisionDay : 0;
            HasPendingDecision = false;
            Pending = null;
        }

        public void OnDay(int day)
        {
            if (HasPendingDecision) return;
            if (day == _lastDecisionDay) return;

            StoryEvent story = FindForDay(day);
            if (story == null) return;
            Pending = story;
            HasPendingDecision = true;
            if (GameManager.Instance.UI != null) GameManager.Instance.UI.ShowStoryDecision(story);
        }

        public bool Resolve(StoryChoice choice)
        {
            if (Pending == null) return false;
            if (Pending.day == _lastDecisionDay) return false;

            _lastDecisionDay = Pending.day;
            ApplyOutcome(choice);
            HasPendingDecision = false;
            Pending = null;
            GameManager.Instance.SaveSoon();
            return true;
        }

        public void CopyTo(GameSaveData save)
        {
            if (save == null) return;
            if (save.story == null) save.story = new StorySaveState();
            save.story.lastDecisionDay = _lastDecisionDay;
        }

        private void ApplyOutcome(StoryChoice choice)
        {
            switch (choice.outcome)
            {
                case StoryOutcome.Resources:
                    GameManager.Instance.Resources.Add(choice.resource, choice.amount, "story");
                    GameEvents.Notify(Loc.Get("story.decision.resources", choice.Title,
                        GameText.ResourceAmount(choice.resource, choice.amount)));
                    break;
                case StoryOutcome.Morale:
                    BoostAll(choice.moraleDelta);
                    GameEvents.Notify(Loc.Get("story.decision.morale", choice.Title,
                        Loc.Num(Mathf.Abs(choice.moraleDelta).ToString("0"))));
                    break;
                case StoryOutcome.Heal:
                    HealAll(choice.amount);
                    GameEvents.Notify(Loc.Get("story.decision.heal", choice.Title));
                    break;
            }
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayClick();
            if (GameManager.Instance.UI != null) GameManager.Instance.UI.RefreshHud();
        }

        private void BoostAll(float amount)
        {
            for (int i = 0; i < GameManager.Instance.Survivors.Count; i++)
            {
                SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                if (survivor != null && survivor.IsAlive) survivor.BoostMorale(amount);
            }
        }

        private void HealAll(float amount)
        {
            for (int i = 0; i < GameManager.Instance.Survivors.Count; i++)
            {
                SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                if (survivor != null && survivor.IsAlive) survivor.Heal(amount);
            }
        }

        private static StoryEvent FindForDay(int day)
        {
            for (int i = 0; i < StoryDefinitions.All.Count; i++)
            {
                if (StoryDefinitions.All[i].day == day) return StoryDefinitions.All[i];
            }
            return null;
        }
    }

    public static class StoryDefinitions
    {
        public static readonly List<StoryEvent> All = new List<StoryEvent>
        {
            new StoryEvent(2, "s_voice", new List<StoryChoice>
                {
                    new StoryChoice("s_voice", "scout", StoryOutcome.Resources, ResourceType.Wood, 22, -6f),
                    new StoryChoice("s_voice", "guard", StoryOutcome.Morale, ResourceType.Food, 0, 8f),
                    new StoryChoice("s_voice", "ignore", StoryOutcome.Morale, ResourceType.Food, 0, -4f)
                }),
            new StoryEvent(4, "s_stranger", new List<StoryChoice>
                {
                    new StoryChoice("s_stranger", "heal", StoryOutcome.Morale, ResourceType.Food, 0, 12f),
                    new StoryChoice("s_stranger", "ask", StoryOutcome.Resources, ResourceType.Water, 26, 0f),
                    new StoryChoice("s_stranger", "reject", StoryOutcome.Morale, ResourceType.Food, 0, -8f)
                }),
            new StoryEvent(6, "s_storm", new List<StoryChoice>
                {
                    new StoryChoice("s_storm", "reinforce", StoryOutcome.Resources, ResourceType.Stone, 20, -5f),
                    new StoryChoice("s_storm", "stock", StoryOutcome.Resources, ResourceType.Food, 28, 0f),
                    new StoryChoice("s_storm", "patrol", StoryOutcome.Resources, ResourceType.Energy, 10, 4f)
                })
        };
    }
}
