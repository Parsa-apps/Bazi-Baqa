using System;
using System.IO;
using UnityEngine;

namespace BaziBaqa
{
    public sealed class SaveSystem
    {
        private const string FileName = "survival_save.json";
        private const string BackupName = "survival_save.backup.json";
        private const string TemporaryName = "survival_save.tmp";

        /// <summary>نسخه‌ی فعلی فرمت ذخیره؛ برای مهاجرت از نسخه‌های قدیمی استفاده می‌شود.</summary>
        public const int CurrentSaveVersion = 3;

        public string SavePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }
        public string BackupPath { get { return Path.Combine(Application.persistentDataPath, BackupName); } }
        private string TemporaryPath { get { return Path.Combine(Application.persistentDataPath, TemporaryName); } }

        public bool HasSave()
        {
            return File.Exists(SavePath) || File.Exists(BackupPath);
        }

        public bool Save(GameSaveData data)
        {
            if (data == null) return false;
            try
            {
                data.saveVersion = CurrentSaveVersion;
                Directory.CreateDirectory(Application.persistentDataPath);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(TemporaryPath, json);

                if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, BackupPath, true);
                    File.Delete(SavePath);
                }
                File.Move(TemporaryPath, SavePath);
                GameLogger.Info(Loc.Get("log.save_done", Loc.Num(CurrentSaveVersion)));
                return true;
            }
            catch (Exception exception)
            {
                GameLogger.Error(Loc.Get("log.save_failed"), exception);
                TryDelete(TemporaryPath);
                return false;
            }
        }

        public GameSaveData Load()
        {
            GameSaveData data = TryLoad(SavePath);
            if (data != null) return data;

            data = TryLoad(BackupPath);
            if (data != null)
            {
                GameLogger.Warn(Loc.Get("log.backup_loaded"));
            }
            return data;
        }

        public void Delete()
        {
            TryDelete(SavePath);
            TryDelete(BackupPath);
            TryDelete(TemporaryPath);
        }

        private static GameSaveData TryLoad(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null || data.resources == null) return null;
                Migrate(data);
                return data;
            }
            catch (Exception exception)
            {
                GameLogger.Error(Loc.Get("log.load_failed"), exception);
                return null;
            }
        }

        /// <summary>
        /// سازگاری با ذخیره‌های قدیمی: هیچ داده‌ای حذف نمی‌شود، فقط فیلدهای جدیدِ نبوده
        /// مقدار پیش‌فرض می‌گیرند تا بازیکن با آپدیت، پیشرفت خود را از دست ندهد.
        /// </summary>
        private static void Migrate(GameSaveData data)
        {
            if (data.settings == null) data.settings = new SettingsSaveData();
            if (data.survivors == null) data.survivors = new System.Collections.Generic.List<SurvivorSaveData>();
            if (data.buildings == null) data.buildings = new System.Collections.Generic.List<BuildingSaveData>();
            if (data.achievements == null) data.achievements = new AchievementSaveState();
            if (data.dailyReward == null) data.dailyReward = new DailyRewardSaveState();
            if (data.equipment == null) data.equipment = new EquipmentSaveState();
            if (data.story == null) data.story = new StorySaveState();
            if (data.raid == null) data.raid = new RaidSaveState();
            if (data.questIndex < 0) data.questIndex = 0;
            data.saveVersion = CurrentSaveVersion;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                GameLogger.Warn(Loc.Get("log.delete_failed", exception.Message));
            }
        }
    }
}
