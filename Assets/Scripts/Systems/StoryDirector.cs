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
        public string title;
        public string description;
        public StoryOutcome outcome;
        public ResourceType resource;
        public int amount;
        public float moraleDelta;

        public StoryChoice(string titleValue, string descriptionValue, StoryOutcome outcomeValue, ResourceType resourceValue, int amountValue, float moraleValue)
        {
            title = titleValue;
            description = descriptionValue;
            outcome = outcomeValue;
            resource = resourceValue;
            amount = amountValue;
            moraleDelta = moraleValue;
        }
    }

    [Serializable]
    public class StoryEvent
    {
        public int day;
        public string title;
        public string body;
        public List<StoryChoice> choices;

        public StoryEvent(int dayValue, string titleValue, string bodyValue, List<StoryChoice> choicesValue)
        {
            day = dayValue;
            title = titleValue;
            body = bodyValue;
            choices = choicesValue;
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
                    GameManager.Instance.Resources.Add(choice.resource, choice.amount, "تصمیم داستانی");
                    GameEvents.Notify("تصمیم شما: " + choice.title + " — " + choice.amount + " " + GameText.ResourceName(choice.resource) + " گرفتید.");
                    break;
                case StoryOutcome.Morale:
                    BoostAll(choice.moraleDelta);
                    GameEvents.Notify("تصمیم شما: " + choice.title + " روحیه‌ی گروه را تغییر داد.");
                    break;
                case StoryOutcome.Heal:
                    HealAll(choice.amount);
                    GameEvents.Notify("تصمیم شما: " + choice.title + " — زخمی‌ها درمان شدند.");
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
            new StoryEvent(2, "ندای ناشناخته",
                "در سحرگاه، صدایی از اعماق جنگل شنیده می‌شود. کسی باید ببیند چه خبر است.",
                new List<StoryChoice>
                {
                    new StoryChoice("پیشاهنگ را بفرست", "گروه ذخیره‌ی چوب می‌یابد ولی پیشاهنگ خسته می‌شود.", StoryOutcome.Resources, ResourceType.Wood, 22, -6f),
                    new StoryChoice("با یک نگهبان برو", "با احتیاط پیش می‌روید؛ روحیه تقویت می‌شود.", StoryOutcome.Morale, ResourceType.Food, 0, 8f),
                    new StoryChoice("بی‌تفاوت بمان", "چیزی رخ نمی‌دهد، ولی تردید در گروه می‌ماند.", StoryOutcome.Morale, ResourceType.Food, 0, -4f)
                }),
            new StoryEvent(4, "رهگذری زخمی",
                "یک بازمانده‌ی ناآشنا زخمی به اردوگاه می‌رسد و کمک می‌خواهد.",
                new List<StoryChoice>
                {
                    new StoryChoice("مداوا کنید", "با مصرف غذا گروه کمک می‌کند و روحیه بالا می‌رود.", StoryOutcome.Morale, ResourceType.Food, 0, 12f),
                    new StoryChoice("راهنمایی بگیرید", "رهگذر جای چشمه‌ی آب را نشان می‌دهد.", StoryOutcome.Resources, ResourceType.Water, 26, 0f),
                    new StoryChoice("دور کنید", "گروه کنار کشیده و حس امنیت کمی می‌کاهد.", StoryOutcome.Morale, ResourceType.Food, 0, -8f)
                }),
            new StoryEvent(6, "توفان پیش رو",
                "آسمان تیره می‌شود و باد به نشانه‌ی توفان وزیدن می‌گیرد. آماده‌سازی لازم است.",
                new List<StoryChoice>
                {
                    new StoryChoice("پناهگاه را تقویت کن", "سنگ اضافه و امنیت بیش‌تر؛ اما زمان می‌برد.", StoryOutcome.Resources, ResourceType.Stone, 20, -5f),
                    new StoryChoice("ذخیره‌ی غذا", "خودتان را برای روزهای سخت آماده می‌کنید.", StoryOutcome.Resources, ResourceType.Food, 28, 0f),
                    new StoryChoice("قبل از توفان برو", "برای گشت کامل‌تر فضول، انرژی مصرف می‌کنید.", StoryOutcome.Resources, ResourceType.Energy, 10, 4f)
                })
        };
    }
}
