using System;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// سیستم حمله‌ی روزانه: بازیکن با ارسال نگهبانان به یک اردوگاه دشمن در جزیره، خطر وارد شدن
    /// خسارت را می‌پذیرد اما در صورت پیروزی، منابع ارزنده (طلا و سنگ) به دست می‌آورد. این حمله
    /// هر روز فقط یک‌بار و با شرایط مشخص قابل انجام است.
    /// </summary>
    public sealed class RaidSystem : MonoBehaviour
    {
        private RaidSaveState _state = new RaidSaveState();

        public bool HasRaidedToday
        {
            get { return _state.lastRaidDay == GameManager.Instance.Clock.Day; }
        }

        public int Wins { get { return _state.wins; } }
        public int Losses { get { return _state.losses; } }

        public void Initialize(GameSaveData save)
        {
            _state = save != null && save.raid != null ? save.raid : new RaidSaveState();
        }

        /// <summary>هزینه‌ها و شرایط یورش؛ یک منبع برای منطق و رابط کاربری.</summary>
        public const int EnergyCost = 8;
        public const int GoldCost = 3;
        public const int MinGuards = 2;

        public bool CanRaid(out string reason)
        {
            if (GameManager.Instance.Clock.IsNight)
            {
                reason = Loc.Get("toast.raid_night");
                return false;
            }
            if (HasRaidedToday)
            {
                reason = Loc.Get("toast.raid_today");
                return false;
            }
            int guards = AliveGuards();
            if (guards < MinGuards)
            {
                reason = Loc.Get("toast.raid_guards", Loc.Num(MinGuards));
                return false;
            }
            if (GameManager.Instance.Resources.Get(ResourceType.Energy) < EnergyCost)
            {
                reason = Loc.Get("toast.raid_energy", Loc.Num(EnergyCost));
                return false;
            }
            if (GameManager.Instance.Resources.Get(ResourceType.Gold) < GoldCost)
            {
                reason = Loc.Get("toast.raid_gold", Loc.Num(GoldCost));
                return false;
            }
            reason = null;
            return true;
        }

        public void ExecuteRaid()
        {
            if (!CanRaid(out string reason))
            {
                GameEvents.Notify(Loc.Get("toast.raid_blocked", reason));
                return;
            }

            GameManager.Instance.Resources.TrySpend(ResourceType.Energy, EnergyCost, "raid-prepare");
            GameManager.Instance.Resources.TrySpend(ResourceType.Gold, GoldCost, "raid-prepare");

            float weaponBonus = GameManager.Instance.Equipment != null ? GameManager.Instance.Equipment.WeaponDamageBonus : 0f;
            float guardPower = AliveGuards() * 0.06f;
            float success = Mathf.Clamp(0.45f + weaponBonus * 0.08f + guardPower, 0.2f, 0.9f);

            _state.lastRaidDay = GameManager.Instance.Clock.Day;
            bool won = UnityEngine.Random.value <= success;

            if (won)
            {
                int wood = 16 + UnityEngine.Random.Range(0, 9);
                int stone = 14 + UnityEngine.Random.Range(0, 7);
                int gold = 8 + UnityEngine.Random.Range(0, 6);
                GameManager.Instance.Resources.Add(ResourceType.Wood, wood, "raid-loot");
                GameManager.Instance.Resources.Add(ResourceType.Stone, stone, "raid-loot");
                GameManager.Instance.Resources.Add(ResourceType.Gold, gold, "raid-loot");
                _state.wins++;
                GameEvents.Notify(Loc.Get("toast.raid_won", GameText.Join(
                    GameText.ResourceAmount(ResourceType.Wood, wood),
                    GameText.ResourceAmount(ResourceType.Stone, stone),
                    GameText.ResourceAmount(ResourceType.Gold, gold))));
                if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(10, "raid");
            }
            else
            {
                _state.losses++;
                PlayerDamage(18f);
                GameEvents.Notify(Loc.Get("toast.raid_lost"));
                for (int i = 0; i < GameManager.Instance.Survivors.Count; i++)
                {
                    SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                    if (survivor != null && survivor.IsAlive) survivor.BoostMorale(-12f);
                }
            }

            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayAlert();
            if (GameManager.Instance.UI != null) GameManager.Instance.UI.RefreshHud();
            GameManager.Instance.SaveSoon();
        }

        public float SuccessChance()
        {
            float weaponBonus = GameManager.Instance.Equipment != null ? GameManager.Instance.Equipment.WeaponDamageBonus : 0f;
            float guardPower = AliveGuards() * 0.06f;
            return Mathf.Clamp(0.45f + weaponBonus * 0.08f + guardPower, 0.2f, 0.9f);
        }

        public void CopyTo(GameSaveData save)
        {
            if (save == null) return;
            if (save.raid == null) save.raid = new RaidSaveState();
            save.raid.lastRaidDay = _state.lastRaidDay;
            save.raid.wins = _state.wins;
            save.raid.losses = _state.losses;
        }

        private int AliveGuards()
        {
            int count = 0;
            for (int i = 0; i < GameManager.Instance.Survivors.Count; i++)
            {
                SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                if (survivor != null && survivor.IsAlive && (survivor.Role == SurvivorRole.Guard || survivor.Role == SurvivorRole.Scout)) count++;
            }
            return count;
        }

        private void PlayerDamage(float amount)
        {
            float remaining = amount;
            for (int i = 0; i < GameManager.Instance.Survivors.Count && remaining > 0f; i++)
            {
                SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                if (survivor == null || !survivor.IsAlive) continue;
                survivor.NotifyDamage(remaining);
                remaining = 0f;
            }
        }
    }
}
