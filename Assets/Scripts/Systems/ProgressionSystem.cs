using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// منطق خالص تبدیل تجربه به مرحله‌ی گروه. بدون وابستگی به صحنه یا GameManager تا بتوان
    /// آن را به‌صورت واحد (Unit) در EditMode تست کرد.
    /// </summary>
    public static class ProgressionMath
    {
        public static int XpRequiredForLevel(int level)
        {
            // هر مرحله کمی بیش‌تر از قبلی نیاز دارد تا پیشرفت در بلندمدت متعادل بماند.
            return 18 + (Mathf.Max(1, level) - 1) * 8;
        }

        // تجمع تجربه را به مرحله‌ها تبدیل می‌کند و تجربه‌ی باقی‌مانده را برمی‌گرداند.
        public static int Advance(int xp, ref int level, int maxLevel)
        {
            while (xp >= XpRequiredForLevel(level) && level < maxLevel)
            {
                xp -= XpRequiredForLevel(level);
                level++;
            }
            if (level >= maxLevel) xp = Mathf.Min(xp, XpRequiredForLevel(level) - 1);
            return xp;
        }
    }

    /// <summary>
    /// سیستم پیشرفت گروه: بازیکن با هر اقدام معنادار (جمع‌آوری، ساخت، ارتقا، تربیت، فناوری، دفاع و
    /// زنده ماندن در شب) تجربه کسب می‌کند و «مرحله‌ی گروه» بالا می‌رود. این سیستم مستقل است تا
    /// در آینده بتوان باز کردن تجهیزات و امتیازهای بیشتری را به آن اضافه کرد.
    /// </summary>
    public sealed class ProgressionSystem : MonoBehaviour
    {
        public const int MaxLevel = 10;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }

        public int XpToNext
        {
            get { return ProgressionMath.XpRequiredForLevel(Level); }
        }

        public bool IsMaxLevel
        {
            get { return Level >= MaxLevel; }
        }

        public void Initialize(GameSaveData save)
        {
            Level = Mathf.Clamp(save == null ? 1 : Mathf.Max(1, save.playerLevel), 1, MaxLevel);
            Xp = save == null ? 0 : Mathf.Max(0, save.playerXp);
            Xp = ProgressionMath.Advance(Xp, ref Level, MaxLevel);
        }

        public void AddXp(int amount, string source)
        {
            if (amount <= 0 || IsMaxLevel) return;
            int level = Level;
            Xp = ProgressionMath.Advance(Xp + amount, ref level, MaxLevel);
            bool leveled = level > Level;
            Level = level;
            if (leveled) OnLevelUp();
        }

        public void CopyTo(GameSaveData save)
        {
            if (save == null) return;
            save.playerLevel = Level;
            save.playerXp = Xp;
        }

        private void OnLevelUp()
        {
            GameEvents.Notify(Loc.Get("toast.level_up", Loc.Num(Level)));
            GameManager.Instance.Resources.Add(ResourceType.Gold, 2 * Level, "level-reward");
            for (int i = 0; i < GameManager.Instance.Survivors.Count; i++)
            {
                SurvivorAgent survivor = GameManager.Instance.Survivors[i];
                if (survivor != null && survivor.IsAlive) survivor.BoostMorale(5f);
            }
            if (GameManager.Instance.Audio != null) GameManager.Instance.Audio.PlayClick();
            if (GameManager.Instance.UI != null) GameManager.Instance.UI.RefreshHud();
        }
    }
}
