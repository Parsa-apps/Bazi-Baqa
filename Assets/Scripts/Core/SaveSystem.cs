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
                Directory.CreateDirectory(Application.persistentDataPath);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(TemporaryPath, json);

                if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, BackupPath, true);
                    File.Delete(SavePath);
                }
                File.Move(TemporaryPath, SavePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("خطا در ذخیره‌سازی: " + exception.Message);
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
                Debug.LogWarning("ذخیره‌ی اصلی آسیب دیده بود؛ نسخه‌ی پشتیبان بارگذاری شد.");
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
                if (data.settings == null) data.settings = new SettingsSaveData();
                if (data.survivors == null) data.survivors = new System.Collections.Generic.List<SurvivorSaveData>();
                if (data.buildings == null) data.buildings = new System.Collections.Generic.List<BuildingSaveData>();
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("خواندن ذخیره ناموفق بود: " + exception.Message);
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("پاک‌کردن فایل ذخیره ناموفق بود: " + exception.Message);
            }
        }
    }
}
