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

        public bool CanRaid(out string reason)
        {
            if (GameManager.Instance.Clock.IsNight)
            {
                reason = "در شب نمی‌توان یورش برد؛ شب متعلق به سایه‌هاست.";
                return false;
            }
            if (HasRaidedToday)
            {
                reason = "امروز یک‌بار یورش رفتید؛ فردا دوباره تلاش کنید.";
                return false;
            }
            int guards = AliveGuards();
            if (guards < 2)
            {
                reason = "برای یورش به دست‌کم دو نگهبان نیاز دارید.";
                return false;
            }
            if (GameManager.Instance.Resources.Get(ResourceType.Energy) < 8)
            {
                reason = "برای یورش ۸ انرژی لازم است.";
                return false;
            }
            if (GameManager.Instance.Resources.Get(ResourceType.Gold) < 3)
            {
                reason = "برای آماده‌سازی یورش ۳ طلا لازم است.";
                return false;
            }
            reason = null;
            return true;
        }

        public void ExecuteRaid()
        {
            if (!CanRaid(out string reason))
            {
                GameEvents.Notify("یورش ممکن نیست: " + reason);
                return;
            }

            GameManager.Instance.Resources.TrySpend(ResourceType.Energy, 8, "آماده‌سازی یورش");
            GameManager.Instance.Resources.TrySpend(ResourceType.Gold, 3, "آماده‌سازی یورش");

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
                GameManager.Instance.Resources.Add(ResourceType.Wood, wood, "غنیمت یورش");
                GameManager.Instance.Resources.Add(ResourceType.Stone, stone, "غنیمت یورش");
                GameManager.Instance.Resources.Add(ResourceType.Gold, gold, "غنیمت یورش");
                _state.wins++;
                GameEvents.Notify("یورش پیروز شد! " + wood + " چوب، " + stone + " سنگ و " + gold + " طلا غنیمت گرفتید.");
                if (GameManager.Instance.Progression != null) GameManager.Instance.Progression.AddXp(10, "یورش پیروز");
            }
            else
            {
                _state.losses++;
                PlayerDamage(18f);
                GameEvents.Notify("یورش شکست خورد و خسارت دیدید؛ روحیه‌ی گروه پایین آمد.");
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
            save.raid = _state;
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
