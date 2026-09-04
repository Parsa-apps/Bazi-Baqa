using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    public enum GamePhase
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
        public bool IsPlaying { get { return Phase == GamePhase.Playing; } }
        public GameClock Clock { get; private set; } = new GameClock();
        public SaveSystem Save { get; private set; } = new SaveSystem();
        public ResourceSystem Resources { get; private set; }
        public WorldGenerator World { get; private set; }
        public ConstructionSystem Construction { get; private set; }
        public WeatherSystem Weather { get; private set; }
        public EnemyDirector EnemyDirector { get; private set; }
        public TechnologySystem Technology { get; private set; }
        public SurvivalSystem Survival { get; private set; }
        public AudioManager Audio { get; private set; }
        public UIManager UI { get; private set; }
        public CameraController CameraController { get; private set; }
        public WorldSelectionSystem WorldSelection { get; private set; }
        public bool HasSave { get { return Save != null && Save.HasSave(); } }

        private readonly List<SurvivorAgent> _survivors = new List<SurvivorAgent>();
        private readonly List<EnemyAgent> _enemies = new List<EnemyAgent>();
        private float _autoSaveTimer;
        private int _currentSeed;
        private bool _tutorialCompleted;
        private bool _initialized;
        private bool _initializing;

        public IReadOnlyList<SurvivorAgent> Survivors { get { return _survivors; } }
        public IReadOnlyList<EnemyAgent> Enemies { get { return _enemies; } }

        public void Initialize(UIManager ui)
        {
            if (_initialized) return;
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            UI = ui;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Resources = gameObject.AddComponent<ResourceSystem>();
            World = gameObject.AddComponent<WorldGenerator>();
            Construction = gameObject.AddComponent<ConstructionSystem>();
            Weather = gameObject.AddComponent<WeatherSystem>();
            EnemyDirector = gameObject.AddComponent<EnemyDirector>();
            Technology = gameObject.AddComponent<TechnologySystem>();
            Survival = gameObject.AddComponent<SurvivalSystem>();
            Audio = gameObject.AddComponent<AudioManager>();
            World.Initialize();
            Clock.DayChanged += OnDayChanged;
            EnemyDirector.Initialize();
            EnsureCamera();
            _initialized = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Phase == GamePhase.Playing && !UI.HandleBack()) TogglePause();
                else if (Phase == GamePhase.Paused) Resume();
            }
            if (!IsPlaying) return;
            Clock.Advance(Time.deltaTime);
            _autoSaveTimer -= Time.deltaTime;
            if (_autoSaveTimer <= 0f)
            {
                _autoSaveTimer = 30f;
                SaveGame();
            }
            if (Input.GetKeyDown(KeyCode.Escape) && !UI.HandleBack()) TogglePause();
        }

        public void StartNewGame()
        {
            int seed = Random.Range(10000, 999999);
            StartFromSave(GameSaveData.CreateNew(seed), false);
            GameEvents.Notify("گروه شما به ساحل ناشناخته رسید؛ با هم بقا پیدا کنید.");
        }

        public void ContinueGame()
        {
            GameSaveData save = Save.Load();
            if (save == null)
            {
                GameEvents.Notify("ذخیره‌ای پیدا نشد؛ یک سفر تازه آغاز می‌شود.");
                StartNewGame();
                return;
            }
            StartFromSave(save, true);
        }

        public void StartFromSave(GameSaveData save, bool continuing)
        {
            if (save == null) save = GameSaveData.CreateNew(Random.Range(10000, 999999));
            if (save.resources == null) save.resources = new ResourceState();
            if (save.survivors == null) save.survivors = new List<SurvivorSaveData>();
            if (save.buildings == null) save.buildings = new List<BuildingSaveData>();
            _currentSeed = save.seed == 0 ? 14729 : save.seed;
            save.seed = _currentSeed;
            _tutorialCompleted = save.settings != null && save.settings.tutorialCompleted;
            _initializing = true;
            Phase = GamePhase.Playing;
            _survivors.Clear();
            _enemies.Clear();
            World.Generate(save.seed);
            Resources.Initialize(save.resources);
            Clock.Initialize(save.day, save.dayTime);
            Technology.Initialize(save);
            Survival.Initialize();
            Weather.Initialize();
            EnemyDirector.Clear();
            Construction.Initialize(save.buildings);
            for (int i = 0; i < save.survivors.Count; i++) World.CreateSurvivorVisual(save.survivors[i]);
            if (_survivors.Count == 0)
            {
                GameSaveData fallback = GameSaveData.CreateNew(save.seed);
                for (int i = 0; i < fallback.survivors.Count; i++) World.CreateSurvivorVisual(fallback.survivors[i]);
            }
            _autoSaveTimer = 30f;
            EnsureCamera();
            if (Audio == null) Audio = gameObject.AddComponent<AudioManager>();
            bool soundEnabled = save.settings == null || save.settings.soundEnabled;
            bool vibrationEnabled = save.settings == null || save.settings.vibrationEnabled;
            if (PlayerPrefs.HasKey("bazi_baqa_sound")) soundEnabled = PlayerPrefs.GetInt("bazi_baqa_sound", 1) == 1;
            if (PlayerPrefs.HasKey("bazi_baqa_vibration")) vibrationEnabled = PlayerPrefs.GetInt("bazi_baqa_vibration", 1) == 1;
            Audio.Initialize(soundEnabled, vibrationEnabled);
            _initializing = false;
            UI.ShowGame(continuing);
            GameEvents.StateChanged();
            if (!_tutorialCompleted) UI.ShowTutorial();
            if (AliveSurvivorCount() == 0) LoseGame();
        }

        public void TogglePause()
        {
            if (Phase == GamePhase.Playing)
            {
                Phase = GamePhase.Paused;
                UI.ShowPause(true);
            }
            else if (Phase == GamePhase.Paused)
            {
                Resume();
            }
        }

        public void Resume()
        {
            if (Phase != GamePhase.Paused) return;
            Phase = GamePhase.Playing;
            UI.ShowPause(false);
        }

        public void ReturnToMenu()
        {
            SaveGame();
            Phase = GamePhase.MainMenu;
            _survivors.Clear();
            _enemies.Clear();
            if (World != null) World.ClearGeneratedWorld();
            if (EnemyDirector != null) EnemyDirector.Clear();
            UI.ShowMainMenu();
        }

        public void SaveSoon()
        {
            _autoSaveTimer = Mathf.Min(_autoSaveTimer, 1f);
        }

        public void MarkTutorialCompleted()
        {
            _tutorialCompleted = true;
            SaveGame();
        }

        public void SaveSettings(bool soundEnabled)
        {
            PlayerPrefs.SetInt("bazi_baqa_sound", soundEnabled ? 1 : 0);
            PlayerPrefs.Save();
            if (Phase != GamePhase.MainMenu) SaveGame();
        }

        public void SaveGame()
        {
            if (Phase == GamePhase.MainMenu) return;
            GameSaveData save = new GameSaveData
            {
                saveVersion = 1,
                seed = WorldSeedFromCurrent(),
                day = Clock.Day,
                dayTime = Clock.NormalizedTime,
                resources = Resources.ToSave(),
                survivors = new List<SurvivorSaveData>(),
                buildings = Construction.GetSaveData()
            };
            for (int i = 0; i < _survivors.Count; i++)
            {
                if (_survivors[i] != null) save.survivors.Add(_survivors[i].ToSaveData());
            }
            Technology.CopyTo(save);
            save.settings.soundEnabled = Audio == null || Audio.SoundEnabled;
            save.settings.vibrationEnabled = Audio == null || Audio.VibrationEnabled;
            save.settings.tutorialCompleted = _tutorialCompleted;
            Save.Save(save);
        }

        public void OnSurvivorLost(SurvivorAgent survivor)
        {
            if (_initializing) return;
            int alive = AliveSurvivorCount();
            if (alive <= 0) LoseGame();
            else UI.RefreshHud();
        }

        public void OnEnemyLost(EnemyAgent enemy)
        {
            _enemies.Remove(enemy);
            if (EnemyDirector != null) EnemyDirector.Unregister(enemy);
        }

        public int AliveSurvivorCount()
        {
            int count = 0;
            for (int i = 0; i < _survivors.Count; i++) if (_survivors[i] != null && _survivors[i].IsAlive) count++;
            return count;
        }

        public SurvivorAgent FindMostInjuredSurvivor(SurvivorAgent except)
        {
            SurvivorAgent result = null;
            float health = 92f;
            for (int i = 0; i < _survivors.Count; i++)
            {
                SurvivorAgent candidate = _survivors[i];
                if (candidate == null || candidate == except || !candidate.IsAlive || candidate.Health >= health) continue;
                health = candidate.Health;
                result = candidate;
            }
            return result;
        }

        public SurvivorAgent FindNearestSurvivor(Vector3 position, float maxDistance)
        {
            SurvivorAgent result = null;
            float best = maxDistance * maxDistance;
            for (int i = 0; i < _survivors.Count; i++)
            {
                SurvivorAgent candidate = _survivors[i];
                if (candidate == null || !candidate.IsAlive) continue;
                float distance = (candidate.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    result = candidate;
                }
            }
            return result;
        }

        public EnemyAgent FindNearestEnemy(Vector3 position, float maxDistance)
        {
            EnemyAgent result = null;
            float best = maxDistance * maxDistance;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyAgent candidate = _enemies[i];
                if (candidate == null || !candidate.IsAlive) continue;
                float distance = (candidate.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    result = candidate;
                }
            }
            return result;
        }

        public void RegisterSurvivor(SurvivorAgent survivor)
        {
            if (survivor != null && !_survivors.Contains(survivor)) _survivors.Add(survivor);
        }

        public void RegisterEnemy(EnemyAgent enemy)
        {
            if (enemy != null && !_enemies.Contains(enemy)) _enemies.Add(enemy);
            if (EnemyDirector != null) EnemyDirector.Register(enemy);
        }

        private void OnDayChanged(int day)
        {
            Technology.AddPoints(1);
            GameEvents.Notify("روز " + GameClock.ToPersianDigits(day.ToString()) + " آغاز شد؛ یک امتیاز فناوری گرفتید.");
            SaveGame();
            if (day >= 7 && Phase == GamePhase.Playing) WinGame();
        }

        private void WinGame()
        {
            Phase = GamePhase.Victory;
            SaveGame();
            UI.ShowResult(true);
        }

        private void LoseGame()
        {
            Phase = GamePhase.GameOver;
            SaveGame();
            UI.ShowResult(false);
        }

        private void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("دوربین بازی");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            CameraController = camera.GetComponent<CameraController>();
            if (CameraController == null) CameraController = camera.gameObject.AddComponent<CameraController>();
            CameraController.Initialize();
            WorldSelection = camera.GetComponent<WorldSelectionSystem>();
            if (WorldSelection == null) WorldSelection = camera.gameObject.AddComponent<WorldSelectionSystem>();
            WorldSelection.Initialize(camera);
        }

        private int WorldSeedFromCurrent()
        {
            // seed هر سفر در فایل ذخیره می‌ماند تا جهان ادامه‌ی همان سفر را بازسازی کند.
            return _currentSeed == 0 ? 14729 : _currentSeed;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveGame();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveGame();
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Clock != null) Clock.DayChanged -= OnDayChanged;
        }
    }
}
