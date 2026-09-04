using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>رابط کاربری؛ بعد از GameManager آماده می‌شود ولی پیش از سامانه‌های زمان‌بر.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed class UIManager : MonoBehaviour
    {
        private readonly Dictionary<ResourceType, UIText> _resourceLabels = new Dictionary<ResourceType, UIText>();

        private GameManager _game;
        private bool _splashActive;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _view;
        private GameObject _hudRoot;
        private GameObject _buildPanel;
        private GameObject _technologyPanel;
        private GameObject _mapPanel;
        private GameObject _modalLayer;
        private Transform _survivorList;
        private UIText _groupTitleLabel;
        private UIText _clockLabel;
        private UIText _weatherLabel;
        private UIText _populationLabel;
        private UIText _technologyLabel;
        private UIText _notificationLabel;
        private UIText _goalLabel;
        private float _refreshTimer;
        private float _survivorRefreshTimer;
        private float _notificationTimer;
        private bool _hudActive;

        private static readonly Color DeepNavy = new Color(0.025f, 0.06f, 0.1f, 0.97f);
        private static readonly Color PanelBlue = new Color(0.055f, 0.13f, 0.19f, 0.94f);
        private static readonly Color PanelBlueLight = new Color(0.1f, 0.23f, 0.29f, 0.96f);
        private static readonly Color Teal = new Color(0.18f, 0.78f, 0.7f, 1f);
        private static readonly Color Gold = new Color(1f, 0.68f, 0.25f, 1f);
        private static readonly Color Muted = new Color(0.67f, 0.78f, 0.82f, 1f);

        public void Initialize(GameManager game)
        {
            _game = game;
            CreateCanvas();
            GameEvents.Notification += OnNotification;
            GameEvents.StateChanged += RefreshHud;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        public void BeginPresentation()
        {
            ShowSplash();
        }

        private void Update()
        {
            if (!_hudActive || _game == null) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            _survivorRefreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.25f;
                RefreshHud();
            }
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.unscaledDeltaTime;
                if (_notificationTimer <= 0f && _notificationLabel != null) _notificationLabel.text = string.Empty;
            }
        }

        public void ShowSplash()
        {
            _splashActive = true;
            _hudActive = false;
            ClearView();
            _view = CreateRectObject("SplashView", _canvas.transform);
            CanvasGroup splashGroup = _view.AddComponent<CanvasGroup>();
            splashGroup.alpha = 0f;
            Image background = _view.AddComponent<Image>();
            background.color = DeepNavy;
            _view.AddComponent<SplashEffects>();
            _view.AddComponent<IntroFx>();          // اینتروی سینماییِ استودیو (گام ۶)
            UIGlassPanel.ApplyMood("menu");

            GameObject glow = CreatePanel("LogoGlow", _view.transform, new Color(0.06f, 0.38f, 0.42f, 0.42f), new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.76f));
            glow.AddComponent<GlowPulse>();

            GameObject goldHalo = CreatePanel("GoldGlow", _view.transform, new Color(1f, 0.72f, 0.22f, 0.28f), new Vector2(0.32f, 0.66f), new Vector2(0.68f, 0.92f));
            goldHalo.AddComponent<GlowPulse>();
            GameObject crownObject = CreateRectObject("CrownMark", _view.transform);
            SetRect(crownObject.GetComponent<RectTransform>(), new Vector2(0.35f, 0.7f), new Vector2(0.65f, 0.9f), Vector2.zero, Vector2.zero);
            UIText crown = UIText.Attach(crownObject);
            crown.fontSize = 58;
            crown.color = Gold;
            crown.alignment = TextAnchor.MiddleCenter;
            crown.autoFit = true;
            crown.Set("♛");
            CrownPulse crownPulse = crownObject.AddComponent<CrownPulse>();
            crownPulse.Configure(goldHalo.transform);

            UIText mark = CreateText(_view.transform, Loc.Get("game.studio"), 48, Color.white, TextAnchor.MiddleCenter);
            SetRect(mark.Rect, new Vector2(0.12f, 0.52f), new Vector2(0.88f, 0.68f), Vector2.zero, Vector2.zero);
            UIText line = CreateText(_view.transform, Loc.Get("ui.splash.studio_line"), 22, Teal, TextAnchor.MiddleCenter);
            SetRect(line.Rect, new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.53f), Vector2.zero, Vector2.zero);
            UIText loading = CreateText(_view.transform, Loc.Get("game.loading"), 18, Muted, TextAnchor.MiddleCenter);
            SetRect(loading.Rect, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.22f), Vector2.zero, Vector2.zero);
            StartCoroutine(SplashRoutine(splashGroup));
        }

        public void ShowMainMenu()
        {
            _hudActive = false;
            ClearView();
            _view = CreateRectObject("MainMenuView", _canvas.transform);
            Image background = _view.AddComponent<Image>();
            background.color = DeepNavy;
            CreatePanel("LightSweep", _view.transform, new Color(0.05f, 0.3f, 0.34f, 0.28f), new Vector2(0f, 0.72f), new Vector2(1f, 1f));
            CreatePanel("MenuFooterStrip", _view.transform, new Color(0.02f, 0.03f, 0.06f, 0.8f), new Vector2(0f, 0f), new Vector2(1f, 0.12f));

            UIText studio = CreateText(_view.transform, Loc.Get("game.studio"), 22, Teal, TextAnchor.MiddleRight);
            SetRect(studio.Rect, new Vector2(0.58f, 0.9f), new Vector2(0.94f, 0.98f), Vector2.zero, Vector2.zero);
            UIText title = CreateText(_view.transform, Loc.Get("game.title"), 52, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.61f), new Vector2(0.92f, 0.83f), Vector2.zero, Vector2.zero);
            UIText subtitle = CreateText(_view.transform, Loc.Get("game.tagline"), 22, Teal, TextAnchor.MiddleCenter);
            SetRect(subtitle.Rect, new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.64f), Vector2.zero, Vector2.zero);

            GameObject menuCard = CreatePanel("MenuCard", _view.transform, new Color(0.04f, 0.11f, 0.16f, 0.97f), new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.53f));
            CreateButton(menuCard.transform, Loc.Get("ui.menu.new_game"), Teal, StartNewGame, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.92f), 22);
            Button continueButton = CreateButton(menuCard.transform, Loc.Get("menu.continue_game"), new Color(0.26f, 0.53f, 0.72f), ContinueGame, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.65f), 19);
            continueButton.interactable = _game.HasSave;
            CreateButton(menuCard.transform, Loc.Get("menu.settings"), PanelBlueLight, ShowSettings, new Vector2(0.1f, 0.29f), new Vector2(0.9f, 0.46f), 19);
            CreateButton(menuCard.transform, Loc.Get("ui.menu.about"), PanelBlueLight, ShowAbout, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.27f), 18);

            UIText footer = CreateText(_view.transform, Loc.Get("game.footer"), 15, Muted, TextAnchor.MiddleCenter);
            SetRect(footer.Rect, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.095f), Vector2.zero, Vector2.zero);
        }

        public void ShowGame(bool continuing)
        {
            _hudActive = true;
            ClearView();
            _view = CreateRectObject("GameView", _canvas.transform);
            _view.AddComponent<CanvasGroup>();
            _hudRoot = _view;
            CreateGameHud();
            RefreshHud();
            if (continuing) OnNotification(Loc.Get("toast.save_restored"));
        }

        public void ShowTutorial()
        {
            CreateModal(Loc.Get("ui.tutorial.title"), (modal) =>
            {
                UIText body = CreateText(modal.transform, Loc.Get("ui.tutorial.body"), 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.8f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, Loc.Get("ui.tutorial.ready"), Teal, () => FinishTutorial(modal), new Vector2(0.16f, 0.14f), new Vector2(0.84f, 0.26f), 17);
            });
        }

        private void FinishTutorial(GameObject modal)
        {
            _game.MarkTutorialCompleted();
            Destroy(modal);
            _modalLayer = null;
        }

        public bool HandleBack()
        {
            if (_modalLayer != null)
            {
                Destroy(_modalLayer);
                _modalLayer = null;
                return true;
            }
            if (_buildPanel != null)
            {
                Destroy(_buildPanel);
                _buildPanel = null;
                return true;
            }
            if (_technologyPanel != null)
            {
                Destroy(_technologyPanel);
                _technologyPanel = null;
                return true;
            }
            if (_mapPanel != null)
            {
                Destroy(_mapPanel);
                _mapPanel = null;
                return true;
            }
            return false;
        }

        public void ShowPause(bool visible)
        {
            if (!visible)
            {
                if (_modalLayer != null) Destroy(_modalLayer);
                _modalLayer = null;
                return;
            }
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = CreatePanel("PauseOverlay", _canvas.transform, new Color(0.01f, 0.025f, 0.05f, 0.84f), Vector2.zero, Vector2.one);
            UIText title = CreateText(_modalLayer.transform, Loc.Get("modal.pause"), 36, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.18f, 0.65f), new Vector2(0.82f, 0.78f), Vector2.zero, Vector2.zero);
            CreateButton(_modalLayer.transform, Loc.Get("modal.continue"), Teal, _game.Resume, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.62f), 20);
            CreateButton(_modalLayer.transform, Loc.Get("ui.modal.save_and_menu"), PanelBlueLight, _game.ReturnToMenu, new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.47f), 18);
            CreateButton(_modalLayer.transform, Loc.Get("ui.modal.save_now"), new Color(0.25f, 0.4f, 0.46f), _game.SaveGame, new Vector2(0.25f, 0.2f), new Vector2(0.75f, 0.32f), 18);
        }

        public void ShowResult(bool victory)
        {
            _hudActive = false;
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = CreatePanel("ResultOverlay", _canvas.transform, new Color(0.01f, 0.025f, 0.05f, 0.91f), Vector2.zero, Vector2.one);
            UIText crown = CreateText(_modalLayer.transform, victory ? "♛" : "✦", 62, victory ? Gold : new Color(0.8f, 0.3f, 0.35f), TextAnchor.MiddleCenter);
            SetRect(crown.Rect, new Vector2(0.3f, 0.66f), new Vector2(0.7f, 0.86f), Vector2.zero, Vector2.zero);
            UIText title = CreateText(_modalLayer.transform, victory ? Loc.Get("ui.result.victory") : Loc.Get("ui.result.defeat"), 31, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            UIText detail = CreateText(_modalLayer.transform, victory ? Loc.Get("ui.result.victory_detail") : Loc.Get("ui.result.defeat_detail"), 18, Muted, TextAnchor.MiddleCenter);
            SetRect(detail.Rect, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.54f), Vector2.zero, Vector2.zero);
            CreateButton(_modalLayer.transform, Loc.Get("menu.new_game"), Teal, StartNewGame, new Vector2(0.2f, 0.24f), new Vector2(0.8f, 0.36f), 19);
            CreateButton(_modalLayer.transform, Loc.Get("modal.main_menu"), PanelBlueLight, _game.ReturnToMenu, new Vector2(0.2f, 0.09f), new Vector2(0.8f, 0.21f), 18);
        }

        public void RefreshHud()
        {
            if (!_hudActive || _game == null || _game.Resources == null) return;
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (_resourceLabels.TryGetValue(type, out UIText label))
                {
label.Set(Loc.Get("format.resource_line", GameText.ResourceName(type), Loc.Num(_game.Resources.Get(type))));
                }
            }
_clockLabel.Set(Loc.Get("hud.day_line", Loc.Num(_game.Clock.Day), _game.Clock.GetClockText()));
_weatherLabel.Set(Loc.Get("hud.weather_line", GameText.WeatherName(_game.Weather.Current)));
_populationLabel.Set(Loc.Get("hud.population_line", Loc.Num(_game.AliveSurvivorCount()), Loc.Num(_game.Survivors.Count)));
            if (_groupTitleLabel != null)
            {
                int level = _game.Progression == null ? 1 : _game.Progression.Level;
_groupTitleLabel.Set(Loc.Get("hud.companions_line", Loc.Num(level)));
            }
_technologyLabel.Set(Loc.Get("hud.technology_line", Loc.Num(_game.Technology.Points)));
            if (_goalLabel != null)
            {
                string quest = ActiveQuestText();
                string morale = _game.Survival == null ? string.Empty : Loc.Get("hud.morale_suffix", Loc.Num(Mathf.RoundToInt(_game.Survival.TeamMorale)));
_goalLabel.Set(Loc.Get("hud.objective_line", Loc.Num(GameManager.VictoryDay)) + morale + quest);
            }
            if (_survivorRefreshTimer <= 0f)
            {
                _survivorRefreshTimer = 1f;
                RefreshSurvivors();
            }
        }

        private void CreateGameHud()
        {
            UIGlassPanel.ApplyMood("hud");
            GameObject header = CreatePanel("HudHeader", _hudRoot.transform, new Color(0.02f, 0.08f, 0.12f, 0.96f), new Vector2(0f, 0.84f), Vector2.one);
            for (int i = 0; i < 6; i++)
            {
                ResourceType type = (ResourceType)i;
                GameObject card = CreatePanel("ResourceCard", header.transform, new Color(0.05f, 0.15f, 0.2f, 0.96f), new Vector2(i / 6f, 0.35f), new Vector2((i + 1) / 6f, 0.9f));
                UIText label = CreateText(card.transform, GameText.ResourceName(type), 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(label.Rect, Vector2.zero, Vector2.one, new Vector2(3f, 1f), new Vector2(-23f, -1f));
                // آیکنِ رویه‌ای در ابتدایِ راست (چیدمانِ RTL) ⇒ عدد و نام بدونِ Asset خوانا می‌شوند
                UIIconLibrary.AttachBadge(card.transform, type, 17f, 5f);
                _resourceLabels[type] = label;
            }
            _clockLabel = CreateText(header.transform, "", 17, Gold, TextAnchor.MiddleRight);
            SetRect(_clockLabel.Rect, new Vector2(0.54f, 0.03f), new Vector2(0.98f, 0.32f), Vector2.zero, Vector2.zero);
            _weatherLabel = CreateText(header.transform, "", 15, Muted, TextAnchor.MiddleLeft);
            SetRect(_weatherLabel.Rect, new Vector2(0.02f, 0.03f), new Vector2(0.4f, 0.32f), Vector2.zero, Vector2.zero);

            GameObject leftPanel = CreatePanel("GroupPanel", _hudRoot.transform, new Color(0.025f, 0.09f, 0.13f, 0.92f), new Vector2(0.015f, 0.2f), new Vector2(0.275f, 0.82f));
            _groupTitleLabel = CreateText(leftPanel.transform, Loc.Get("hud.companions"), 19, Teal, TextAnchor.MiddleRight);
            SetRect(_groupTitleLabel.Rect, new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.99f), Vector2.zero, Vector2.zero);
            _survivorList = CreateRectObject("SurvivorList", leftPanel.transform).transform;
            SetRect((RectTransform)_survivorList, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero);

            GameObject rightInfo = CreatePanel("InfoPanel", _hudRoot.transform, new Color(0.025f, 0.09f, 0.13f, 0.78f), new Vector2(0.73f, 0.71f), new Vector2(0.985f, 0.82f));
            _populationLabel = CreateText(rightInfo.transform, "", 15, Color.white, TextAnchor.MiddleRight);
            SetRect(_populationLabel.Rect, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero);
            _technologyLabel = CreateText(rightInfo.transform, "", 14, Gold, TextAnchor.MiddleRight);
            SetRect(_technologyLabel.Rect, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject bottom = CreatePanel("ActionBar", _hudRoot.transform, new Color(0.02f, 0.07f, 0.11f, 0.97f), new Vector2(0f, 0f), new Vector2(1f, 0.2f));
            CreateButton(bottom.transform, Loc.Get("ui.action.build"), Teal, ToggleBuildPanel, new Vector2(0.02f, 0.55f), new Vector2(0.2f, 0.92f), 13);
            CreateButton(bottom.transform, Loc.Get("hud.technology"), new Color(0.38f, 0.55f, 0.85f), ToggleTechnologyPanel, new Vector2(0.21f, 0.55f), new Vector2(0.39f, 0.92f), 13);
            CreateButton(bottom.transform, Loc.Get("ui.action.train"), new Color(0.55f, 0.38f, 0.68f), ShowTrainingPanel, new Vector2(0.4f, 0.55f), new Vector2(0.58f, 0.92f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.quests"), new Color(0.28f, 0.72f, 0.62f), ShowQuestsPanel, new Vector2(0.59f, 0.55f), new Vector2(0.77f, 0.92f), 13);
            CreateButton(bottom.transform, Loc.Get("ui.action.achievements"), new Color(0.86f, 0.66f, 0.3f), ShowAchievementsPanel, new Vector2(0.78f, 0.55f), new Vector2(0.96f, 0.92f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.equipment"), new Color(0.3f, 0.52f, 0.55f), ShowEquipmentPanel, new Vector2(0.02f, 0.14f), new Vector2(0.17f, 0.48f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.raid"), new Color(0.7f, 0.36f, 0.3f), ShowRaidPanel, new Vector2(0.18f, 0.14f), new Vector2(0.33f, 0.48f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.map"), new Color(0.3f, 0.45f, 0.32f), ShowMap, new Vector2(0.34f, 0.14f), new Vector2(0.49f, 0.48f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.daily"), new Color(0.24f, 0.55f, 0.42f), ShowDailyReward, new Vector2(0.5f, 0.14f), new Vector2(0.65f, 0.48f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.save"), new Color(0.36f, 0.45f, 0.5f), _game.SaveGame, new Vector2(0.66f, 0.14f), new Vector2(0.81f, 0.48f), 12);
            CreateButton(bottom.transform, Loc.Get("ui.action.pause"), new Color(0.28f, 0.34f, 0.4f), _game.TogglePause, new Vector2(0.82f, 0.14f), new Vector2(0.96f, 0.48f), 12);
            _goalLabel = CreateText(bottom.transform, "", 12, Muted, TextAnchor.MiddleLeft);
            SetRect(_goalLabel.Rect, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.12f), Vector2.zero, Vector2.zero);

            _notificationLabel = CreateText(_hudRoot.transform, "", 17, Color.white, TextAnchor.MiddleCenter);
            SetRect(_notificationLabel.Rect, new Vector2(0.29f, 0.18f), new Vector2(0.72f, 0.28f), Vector2.zero, Vector2.zero);
            CreatePanel("NotificationBackdrop", _hudRoot.transform, new Color(0.03f, 0.15f, 0.17f, 0.4f), new Vector2(0.29f, 0.18f), new Vector2(0.72f, 0.28f)).transform.SetAsFirstSibling();
        }

        private void RefreshSurvivors()
        {
            if (_survivorList == null) return;
            for (int i = _survivorList.childCount - 1; i >= 0; i--) Destroy(_survivorList.GetChild(i).gameObject);
            IReadOnlyList<SurvivorAgent> survivors = _game.Survivors;
            for (int i = 0; i < survivors.Count; i++)
            {
                SurvivorAgent survivor = survivors[i];
                if (survivor == null) continue;
                float top = 1f - i * 0.155f;
                GameObject row = CreatePanel("SurvivorRow", _survivorList, survivor.IsAlive ? new Color(0.06f, 0.16f, 0.2f, 0.92f) : new Color(0.18f, 0.08f, 0.1f, 0.8f), new Vector2(0.02f, top - 0.14f), new Vector2(0.98f, top - 0.01f));
                UIText name = CreateText(row.transform, survivor.DisplayName, 15, survivor.IsAlive ? Color.white : new Color(0.75f, 0.5f, 0.5f), TextAnchor.MiddleRight);
                SetRect(name.Rect, new Vector2(0.39f, 0.48f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
                string roleLine = survivor.IsAlive
                    ? GameText.RoleName(survivor.Role) + "\n" + survivor.TaskDescription
                    : Loc.Get("ui.survivor.lost");
                UIText role = CreateText(row.transform, roleLine, 11, Muted, TextAnchor.MiddleRight);
                SetRect(role.Rect, new Vector2(0.39f, 0.04f), new Vector2(0.96f, 0.48f), Vector2.zero, Vector2.zero);
                CreateStatusBar(row.transform, new Vector2(0.04f, 0.58f), survivor.Health / 100f, new Color(0.92f, 0.28f, 0.3f));
                CreateStatusBar(row.transform, new Vector2(0.04f, 0.35f), survivor.Hunger / 100f, new Color(0.9f, 0.64f, 0.2f));
                CreateStatusBar(row.transform, new Vector2(0.04f, 0.12f), survivor.Thirst / 100f, new Color(0.2f, 0.6f, 0.9f));
            }
        }

        public void ShowBuildingDetails(BuildingController building)
        {
            if (building == null || !building.IsOperational) return;
            CreateModal(GameText.BuildingName(building.Type), (modal) =>
            {
                string detail = Loc.Get("ui.building.detail", Loc.Num(building.Level), Loc.Num(Mathf.RoundToInt(building.Health)), Loc.Num(Mathf.RoundToInt(building.MaxHealth)),
                    GameText.CostLine(ConstructionSystem.GetUpgradeCosts(building.Type, building.Level)));
                UIText body = CreateText(modal.transform, detail, 17, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.8f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, Loc.Get("ui.building.upgrade"), Teal, () => UpgradeBuilding(building), new Vector2(0.18f, 0.2f), new Vector2(0.82f, 0.32f), 17);
            });
        }

        private void UpgradeBuilding(BuildingController building)
        {
            _game.Construction.Upgrade(building);
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = null;
            RefreshHud();
        }

        private void ToggleBuildPanel()
        {
            if (_buildPanel != null)
            {
                Destroy(_buildPanel);
                _buildPanel = null;
                return;
            }
            if (_technologyPanel != null) Destroy(_technologyPanel);
            _buildPanel = CreatePanel("BuildPanel", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.79f));
            UIText title = CreateText(_buildPanel.transform, Loc.Get("ui.build.title"), 20, Teal, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.89f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);
            BuildingType[] types = { BuildingType.House, BuildingType.Farm, BuildingType.WatchTower, BuildingType.Workshop, BuildingType.Wall, BuildingType.SolarStation };
            for (int i = 0; i < types.Length; i++)
            {
                int row = i / 2;
                int column = i % 2;
                BuildingType type = types[i];
                string label = GameText.BuildingName(type) + "\n" + GameText.CostLine(ConstructionSystem.GetBuildCosts(type));
                CreateButton(_buildPanel.transform, label, i % 2 == 0 ? Teal : new Color(0.27f, 0.52f, 0.72f), () => SelectBuilding(type), new Vector2(0.07f + column * 0.47f, 0.65f - row * 0.17f), new Vector2(0.46f + column * 0.47f, 0.8f - row * 0.17f), 13);
            }
            CreateButton(_buildPanel.transform, Loc.Get("ui.common.close"), new Color(0.25f, 0.32f, 0.36f), ToggleBuildPanel, new Vector2(0.28f, 0.04f), new Vector2(0.72f, 0.15f), 16);
        }

        private void SelectBuilding(BuildingType type)
        {
            _game.Construction.SelectForPlacement(type);
            if (_buildPanel != null) Destroy(_buildPanel);
            _buildPanel = null;
        }

        private void ShowTrainingPanel()
        {
            if (_buildPanel != null) Destroy(_buildPanel);
            if (_technologyPanel != null) Destroy(_technologyPanel);
            CreateModal(Loc.Get("ui.train.title"), (modal) =>
            {
                UIText body = CreateText(modal.transform, Loc.Get("ui.train.body", TrainingSystem.CostLine(),
                    Loc.Num(_game.AliveSurvivorCount()), Loc.Num(TrainingSystem.MaximumGroupSize)), 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.4f), new Vector2(0.92f, 0.79f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, Loc.Get("ui.train.button"), Teal, TrainGuard, new Vector2(0.16f, 0.23f), new Vector2(0.84f, 0.35f), 17);
            });
        }

        private void TrainGuard()
        {
            _game.Training.TrainGuard();
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = null;
            RefreshHud();
        }

        private void ToggleTechnologyPanel()
        {
            if (_technologyPanel != null)
            {
                Destroy(_technologyPanel);
                _technologyPanel = null;
                return;
            }
            if (_buildPanel != null) Destroy(_buildPanel);
            _technologyPanel = CreatePanel("TechnologyPanel", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.78f));
            UIText title = CreateText(_technologyPanel.transform, Loc.Get("ui.tech.title"), 21, Gold, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);
            UIText points = CreateText(_technologyPanel.transform, Loc.Get("ui.tech.points", Loc.Num(_game.Technology.Points)), 14, Muted, TextAnchor.MiddleCenter);
            SetRect(points.Rect, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
            TechnologyType[] technologies = { TechnologyType.Cooperation, TechnologyType.WaterPurification, TechnologyType.ReinforcedWalls, TechnologyType.FieldRotation };
            for (int i = 0; i < technologies.Length; i++)
            {
                TechnologyType technology = technologies[i];
                string name = GameText.TechnologyName(technology) + (_game.Technology.IsUnlocked(technology)
                    ? "  " + Loc.Get("ui.common.done")
                    : "\n" + Loc.Get("ui.tech.cost", Loc.Num(TechnologySystem.ResearchCost)));
                CreateButton(_technologyPanel.transform, name, _game.Technology.IsUnlocked(technology) ? new Color(0.2f, 0.38f, 0.3f) : PanelBlueLight, () => Research(technology), new Vector2(0.08f, 0.64f - i * 0.13f), new Vector2(0.92f, 0.74f - i * 0.13f), 14);
            }
            CreateButton(_technologyPanel.transform, Loc.Get("ui.common.close"), new Color(0.25f, 0.32f, 0.36f), ToggleTechnologyPanel, new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.13f), 16);
        }

        private void Research(TechnologyType technology)
        {
            _game.Technology.Research(technology);
            if (_technologyPanel != null) Destroy(_technologyPanel);
            _technologyPanel = null;
            RefreshHud();
        }

        public void ShowDailyReward()
        {
            int streak = _game.DailyRewards == null ? 0 : _game.DailyRewards.Streak;
            bool claimable = _game.DailyRewards != null && _game.DailyRewards.Claimable;
            CreateModal(Loc.Get("ui.daily.title"), (modal) =>
            {
                int bonus = Mathf.Min(streak, 3);
                UIText body = CreateText(modal.transform, Loc.Get("ui.daily.body",
                        Loc.Get("ui.daily.reward_line",
                            GameText.ResourceAmount(ResourceType.Food, DailyRewardSystem.FoodReward(bonus)),
                            GameText.ResourceAmount(ResourceType.Gold, DailyRewardSystem.GoldReward(bonus))),
                        Loc.Num(streak),
                        Loc.Get(claimable ? "ui.daily.available" : "ui.daily.claimed")),
                    17, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.8f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, Loc.Get("ui.daily.claim"), Teal, () => ClaimDaily(modal), new Vector2(0.2f, 0.26f), new Vector2(0.8f, 0.4f), 17);
            });
        }

        private void ClaimDaily(GameObject modal)
        {
            if (_game.DailyRewards != null) _game.DailyRewards.TryClaim();
            Destroy(modal);
            _modalLayer = null;
            RefreshHud();
        }

        public void ShowQuestsPanel()
        {
            if (_buildPanel != null) Destroy(_buildPanel);
            if (_technologyPanel != null) Destroy(_technologyPanel);
            _buildPanel = CreatePanel("QuestPanel", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.82f));
            UIText title = CreateText(_buildPanel.transform, Loc.Get("ui.quest.title"), 22, Teal, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);

            IReadOnlyList<QuestRuntime> quests = _game.Quests == null ? (IReadOnlyList<QuestRuntime>)new List<QuestRuntime>() : _game.Quests.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime quest = quests[i];
                float top = 0.78f - i * 0.16f;
                string status = quest.Status == QuestStatus.Completed ? Loc.Get("ui.common.done")
                    : Loc.Get(quest.Status == QuestStatus.Claimed ? "ui.quest.claimed" : "ui.quest.active");
                Color statusColor = quest.Status == QuestStatus.Completed ? Gold : (quest.Status == QuestStatus.Claimed ? Muted : Color.white);
                UIText q = CreateText(_buildPanel.transform, Loc.Get("ui.quest.entry_line", quest.Definition.Title, quest.Definition.Description,
                        GameText.ResourceAmount(quest.Definition.rewardResource, quest.Definition.rewardAmount), status),
                    13, statusColor, TextAnchor.MiddleRight);
                SetRect(q.Rect, new Vector2(0.08f, top - 0.13f), new Vector2(0.92f, top), Vector2.zero, Vector2.zero);
            }
            CreateButton(_buildPanel.transform, Loc.Get("ui.quest.claim_all"), Teal, ClaimQuests, new Vector2(0.16f, 0.05f), new Vector2(0.84f, 0.16f), 15);
        }

        private void ClaimQuests()
        {
            _game.Quests.ClaimAll();
            if (_buildPanel != null) Destroy(_buildPanel);
            _buildPanel = null;
            RefreshHud();
        }

        public void ShowAchievementsPanel()
        {
            if (_buildPanel != null) Destroy(_buildPanel);
            if (_technologyPanel != null) Destroy(_technologyPanel);
            _buildPanel = CreatePanel("AchievementPanel", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.82f));
            UIText title = CreateText(_buildPanel.transform, Loc.Get("ui.action.achievements"), 22, Gold, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);

            AchievementId[] ids = { AchievementId.Builder, AchievementId.Defender, AchievementId.Scavenger, AchievementId.FirstNight, AchievementId.Survivor, AchievementId.Rich };
            AchievementId[] ids = { AchievementId.Builder, AchievementId.Defender, AchievementId.Scavenger, AchievementId.FirstNight, AchievementId.Survivor, AchievementId.Rich };
            for (int i = 0; i < ids.Length; i++)
            {
                float top = 0.78f - i * 0.1f;
                bool unlocked = _game.Achievements.IsUnlocked(ids[i]);
                UIText row = CreateText(_buildPanel.transform,
                    (unlocked ? Loc.Get("ui.common.done") + "  " : "○  ") + GameText.AchievementName(ids[i]),
                    15, unlocked ? Gold : Muted, TextAnchor.MiddleRight);
                SetRect(row.Rect, new Vector2(0.08f, top - 0.08f), new Vector2(0.92f, top), Vector2.zero, Vector2.zero);
            }
        }

        public void ShowEquipmentPanel()
        {
            if (_buildPanel != null) Destroy(_buildPanel);
            if (_technologyPanel != null) Destroy(_technologyPanel);
            _buildPanel = CreatePanel("EquipmentPanel", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.84f));
            UIText title = CreateText(_buildPanel.transform, Loc.Get("ui.equip.title"), 22, Teal, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);

            EquipmentType[] types = { EquipmentType.Tool, EquipmentType.Weapon, EquipmentType.Armor };
            for (int i = 0; i < types.Length; i++)
            {
                EquipmentType type = types[i];
                int level = _game.Equipment.Level(type);
                float top = 0.76f - i * 0.14f;
                bool can = _game.Equipment.CanUpgrade(type, out _);
                Color color = can ? Teal : PanelBlueLight;
                string label = _game.Equipment.Name(type) + "\n" +
                    Loc.Get("ui.equip.level_line", Loc.Num(level), Loc.Num(EquipmentSystem.MaxLevel)) + "\n" +
                    Loc.Get("ui.equip.cost_line", GameText.CostLine(_game.Equipment.GetUpgradeCosts(type, level))) +
                    (can ? string.Empty : "\n" + Loc.Get("ui.equip.level_required"));
                CreateButton(_buildPanel.transform, label, color, () => UpgradeEquipment(type), new Vector2(0.08f, top - 0.11f), new Vector2(0.92f, top), 13);
            }
            UIText bonus = CreateText(_buildPanel.transform, Loc.Get("ui.equip.effect_block",
                Loc.Get("ui.equip.gather_bonus", Loc.Num(Mathf.RoundToInt(_game.Equipment.ToolGatherBonus * 100f))),
                Loc.Get("ui.equip.attack_bonus", Loc.Num(_game.Equipment.WeaponDamageBonus)),
                Loc.Get("ui.equip.armor_bonus", Loc.Num(Mathf.RoundToInt(_game.Equipment.ArmorReduction * 100f)))),
                13, Muted, TextAnchor.MiddleCenter);
            SetRect(bonus.Rect, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.34f), Vector2.zero, Vector2.zero);
            CreateButton(_buildPanel.transform, Loc.Get("ui.common.close"), new Color(0.25f, 0.32f, 0.36f), () => { Destroy(_buildPanel); _buildPanel = null; }, new Vector2(0.3f, 0.0f), new Vector2(0.7f, 0.06f), 14);
        }

        private void UpgradeEquipment(EquipmentType type)
        {
            _game.Equipment.Upgrade(type);
            RefreshHud();
        }

        public void ShowRaidPanel()
        {
            int guards = 0;
            for (int i = 0; i < _game.Survivors.Count; i++)
            {
                SurvivorAgent s = _game.Survivors[i];
                if (s != null && s.IsAlive && (s.Role == SurvivorRole.Guard || s.Role == SurvivorRole.Scout)) guards++;
            }
            float chance = Mathf.RoundToInt(_game.Raid.SuccessChance() * 100f);
            CreateModal(Loc.Get("ui.raid.title"), (modal) =>
            {
                UIText body = CreateText(modal.transform, Loc.Get("ui.raid.body",
                    Loc.Get("ui.raid.guards_line", Loc.Num(guards)),
                    Loc.Get("ui.raid.chance_line", Loc.Num(chance)),
                    Loc.Get("ui.raid.cost_line",
                        GameText.ResourceAmount(ResourceType.Energy, RaidSystem.EnergyCost),
                        GameText.ResourceAmount(ResourceType.Gold, RaidSystem.GoldCost)),
                    Loc.Get("ui.raid.record_line", Loc.Num(_game.Raid.Wins), Loc.Num(_game.Raid.Losses))),
                    16, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.4f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, Loc.Get("ui.raid.start"), new Color(0.7f, 0.36f, 0.3f), () => ExecuteRaid(modal), new Vector2(0.16f, 0.22f), new Vector2(0.84f, 0.36f), 17);
            });
        }

        private void ExecuteRaid(GameObject modal)
        {
            _game.Raid.ExecuteRaid();
            Destroy(modal);
            _modalLayer = null;
            RefreshHud();
        }

        public void ShowMap()
        {
            if (_mapPanel != null) { Destroy(_mapPanel); _mapPanel = null; return; }
            _mapPanel = CreatePanel("MapPanel", _canvas.transform, new Color(0.02f, 0.05f, 0.08f, 0.95f), new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.8f));
            UIText title = CreateText(_mapPanel.transform, Loc.Get("ui.map.title"), 22, Teal, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.92f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);
            GameObject mapArea = CreatePanel("MapArea", _mapPanel.transform, new Color(0.12f, 0.2f, 0.16f, 0.9f), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));

            // ساختمان‌ها (سبز)
            for (int i = 0; i < _game.Construction.Buildings.Count; i++)
            {
                BuildingController b = _game.Construction.Buildings[i];
                if (b != null && b.IsOperational) AddMapMarker(mapArea.transform, b.transform.position, new Color(0.3f, 0.7f, 0.35f), 0.05f);
            }
            // منابع (قهوه‌ای/آبی)
            for (int i = 0; i < _game.World.ResourceNodes.Count; i++)
            {
                ResourceNode node = _game.World.ResourceNodes[i];
                if (node == null || node.IsDepleted) continue;
                Color c = node.type == ResourceType.Water ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.55f, 0.4f, 0.25f);
                AddMapMarker(mapArea.transform, node.transform.position, c, 0.03f);
            }
            // بازمانده‌ها (فیروزه‌ای)
            for (int i = 0; i < _game.Survivors.Count; i++)
            {
                SurvivorAgent s = _game.Survivors[i];
                if (s != null && s.IsAlive) AddMapMarker(mapArea.transform, s.transform.position, new Color(0.18f, 0.85f, 0.8f), 0.045f);
            }
            // دشمنان (قرمز)
            for (int i = 0; i < _game.Enemies.Count; i++)
            {
                EnemyAgent e = _game.Enemies[i];
                if (e != null && e.IsAlive) AddMapMarker(mapArea.transform, e.transform.position, new Color(0.9f, 0.2f, 0.25f), 0.05f);
            }

            CreateButton(_mapPanel.transform, Loc.Get("ui.common.close"), new Color(0.25f, 0.32f, 0.36f), () => { Destroy(_mapPanel); _mapPanel = null; }, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.1f), 15);
        }

        private void AddMapMarker(Transform parent, Vector3 worldPosition, Color color, float size)
        {
            GameObject marker = CreateRectObject("MapMarker", parent);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size * 100f, size * 100f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float nx = Mathf.Clamp01((worldPosition.x + WorldGenerator.WorldWidth * 0.5f) / WorldGenerator.WorldWidth);
            float ny = Mathf.Clamp01((worldPosition.z + WorldGenerator.WorldDepth * 0.5f) / WorldGenerator.WorldDepth);
            rect.anchorMin = rect.anchorMax = new Vector2(nx, ny);
            rect.anchoredPosition = Vector2.zero;
            Image image = marker.AddComponent<Image>();
            image.color = color;
        }

        public void ShowStoryDecision(StoryEvent story)
        {
            if (story == null) return;
            CreateModal(story.Title, (modal) =>
            {
                UIText body = CreateText(modal.transform, story.Body, 17, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
                for (int i = 0; i < story.choices.Count; i++)
                {
                    StoryChoice choice = story.choices[i];
                    float top = 0.46f - i * 0.14f;
                    CreateButton(modal.transform, choice.Title + "\n" + choice.Hint, i % 2 == 0 ? Teal : PanelBlueLight,
                        () => ResolveStory(modal, choice), new Vector2(0.1f, top - 0.11f), new Vector2(0.9f, top), 14);
                }
            });
        }

        private void ResolveStory(GameObject modal, StoryChoice choice)
        {
            _game.Story.Resolve(choice);
            Destroy(modal);
            _modalLayer = null;
            RefreshHud();
        }

        private void ShowSettings()
        {
            CreateModal(Loc.Get("menu.settings"), (modal) =>
            {
                UIText hint = CreateText(modal.transform, Loc.Get("ui.settings.hint"), 15, Muted, TextAnchor.MiddleCenter);
                SetRect(hint.Rect, new Vector2(0.1f, 0.64f), new Vector2(0.9f, 0.75f), Vector2.zero, Vector2.zero);
                bool sound = _game.Audio == null || _game.Audio.SoundEnabled;
                bool vibration = _game.Audio == null || _game.Audio.VibrationEnabled;
                if (PlayerPrefs.HasKey("bazi_baqa_sound")) sound = PlayerPrefs.GetInt("bazi_baqa_sound", 1) == 1;
                if (PlayerPrefs.HasKey("bazi_baqa_vibration")) vibration = PlayerPrefs.GetInt("bazi_baqa_vibration", 1) == 1;
                CreateButton(modal.transform, sound ? Loc.Get("ui.settings.sound_on") : Loc.Get("ui.settings.sound_off"), sound ? Teal : PanelBlueLight, () => ToggleSound(modal), new Vector2(0.18f, 0.45f), new Vector2(0.82f, 0.58f), 18);
                CreateButton(modal.transform, vibration ? Loc.Get("ui.settings.haptics_on") : Loc.Get("ui.settings.haptics_off"), vibration ? Teal : PanelBlueLight, () => ToggleVibration(modal), new Vector2(0.18f, 0.29f), new Vector2(0.82f, 0.42f), 17);

                CreateButton(modal.transform, Loc.Get("ui.settings.language", LocalizationManager.DisplayName(LocalizationManager.Language)),
                    LocalizationManager.IsRtl ? Teal : PanelBlueLight, () => SwitchLanguage(modal), new Vector2(0.18f, 0.13f), new Vector2(0.82f, 0.26f), 17);
            });
        }

        /// <summary>چرخش بین زبان‌های جدول؛ انتخاب در PlayerPrefs می‌ماند و رابط بازسازی می‌شود.</summary>
        private void SwitchLanguage(GameObject modal)
        {
            string next = LocalizationManager.NextLanguage(LocalizationManager.Language);
            if (string.IsNullOrEmpty(next))
            {
                GameEvents.Notify(Loc.Get("toast.language_single"));
                return;
            }
            LocalizationManager.SetLanguage(next);
            if (_modalLayer == null) ShowSettings();
        }

        private void ToggleSound(GameObject modal)
        {
            if (_game.Audio == null) return;
            _game.Audio.SetEnabled(!_game.Audio.SoundEnabled);
            _game.SaveSettings(_game.Audio.SoundEnabled);
            Destroy(modal);
            ShowSettings();
        }

        private void ToggleVibration(GameObject modal)
        {
            if (_game.Audio == null) return;
            _game.Audio.SetVibrationEnabled(!_game.Audio.VibrationEnabled);
            PlayerPrefs.SetInt("bazi_baqa_vibration", _game.Audio.VibrationEnabled ? 1 : 0);
            PlayerPrefs.Save();
            Destroy(modal);
            ShowSettings();
        }

        private void ShowAbout()
        {
            CreateModal(Loc.Get("ui.menu.about"), (modal) =>
            {
                UIText body = CreateText(modal.transform, Loc.Get("game.credits"), 18, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.Rect, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
                UIText version = CreateText(modal.transform, Loc.Get("ui.about.version", GameVersion.Display), 14, Teal, TextAnchor.MiddleCenter);
                SetRect(version.Rect, new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.31f), Vector2.zero, Vector2.zero);
                UIText release = CreateText(modal.transform, Loc.Get("ui.about.release_summary", GameVersion.BundleId,
                    Loc.Num(GameVersion.MinSdkVersion), Loc.Num(GameVersion.TargetSdkVersion)), 11, Muted, TextAnchor.MiddleCenter);
                SetRect(release.Rect, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.12f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, Loc.Get("ui.about.website"), Teal, () => Application.OpenURL("https://Parsa-apps.github.io"), new Vector2(0.2f, 0.13f), new Vector2(0.8f, 0.24f), 16);
            });
        }

        private void CreateModal(string titleText, Action<GameObject> content)
        {
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = CreatePanel("ModalOverlay", _canvas.transform, new Color(0.01f, 0.025f, 0.05f, 0.86f), Vector2.zero, Vector2.one);
            GameObject card = CreatePanel("ModalCard", _modalLayer.transform, new Color(0.035f, 0.12f, 0.17f, 0.99f), new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.84f));
            UIText title = CreateText(card.transform, titleText, 26, Teal, TextAnchor.MiddleCenter);
            SetRect(title.Rect, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            CreateButton(card.transform, "×", new Color(0.4f, 0.16f, 0.2f), () => { Destroy(_modalLayer); _modalLayer = null; }, new Vector2(0.82f, 0.88f), new Vector2(0.96f, 0.99f), 19);
            content(card);
        }

        private void OnNotification(string message)
        {
            if (_notificationLabel == null) return;
_notificationLabel.Set(message);
            _notificationTimer = 4.5f;
        }

        private IEnumerator SplashRoutine(CanvasGroup splashGroup)
        {
            float elapsed = 0f;
            while (elapsed < 0.55f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (splashGroup != null) splashGroup.alpha = Mathf.Clamp01(elapsed / 0.55f);
                yield return null;
            }
            _splashActive = false;
            yield return new WaitForSecondsRealtime(1.15f);
            elapsed = 0f;
            while (elapsed < 0.35f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (splashGroup != null) splashGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.35f);
                yield return null;
            }
            ShowMainMenu();
        }

        private void StartNewGame()
        {
            _game.Audio?.PlayClick();
            _game.StartNewGame();
        }

        private void ContinueGame()
        {
            _game.Audio?.PlayClick();
            _game.ContinueGame();
        }

        private string ActiveQuestText()
        {
            if (_game.Quests == null || _game.Quests.Quests.Count == 0) return string.Empty;
            for (int i = 0; i < _game.Quests.Quests.Count; i++)
            {
                QuestRuntime quest = _game.Quests.Quests[i];
                if (quest.Status == QuestStatus.Active) return GameText.QuestLine(quest.Definition.Title);
            }
            return string.Empty;
        }

        private void CreateStatusBar(Transform parent, Vector2 position, float ratio, Color color)
        {
            GameObject bar = CreatePanel("StatusBar", parent, new Color(0f, 0f, 0f, 0.34f), position, position + new Vector2(0.3f, 0.12f));
            GameObject fill = CreatePanel("StatusFill", bar.transform, color, Vector2.zero, new Vector2(Mathf.Clamp01(ratio), 1f));
        }

        private Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("UICanvas");
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<SafeAreaFitter>();
            _canvasRect = canvasObject.GetComponent<RectTransform>();
            return _canvas;
        }

        private void ClearView()
        {
            if (_modalLayer != null) Destroy(_modalLayer);
            if (_view != null) Destroy(_view);
            _view = null;
            _hudRoot = null;
            _buildPanel = null;
            _technologyPanel = null;
            _mapPanel = null;
            _modalLayer = null;
            _resourceLabels.Clear();
            _survivorList = null;
            _groupTitleLabel = null;
            _clockLabel = null;
            _weatherLabel = null;
            _populationLabel = null;
            _technologyLabel = null;
            _notificationLabel = null;
            _goalLabel = null;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject panel = CreateRectObject(name, parent);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.65f, 0.68f, 0.15f);
            outline.effectDistance = new Vector2(1f, -1f);

            // لایه‌ی AAA (گام ۶): شیشه‌ایِ رویه‌ای برای همه‌ی پنل‌ها؛ انیمیشنِ باز شدن فقط
            // برای پنجره‌ها (دکمه‌ها مقیاس‌شان را به ButtonFx می‌دهند و دو انیماتور
            // روی یک localScale نمی‌جنگند).
            UIGlassPanel.Apply(panel);
            if (name != "Button")
            {
                WindowFx windowFx = panel.AddComponent<WindowFx>();
                windowFx.Report();
            }
            return panel;
        }

        /// <summary>
        /// تنها نقطه‌ی ساختِ برچسب در رابط؛ خودِ بک‌اند (TMP یا Text قدیمی) را GameTextBackend انتخاب
        /// می‌کند، پس این‌جا نه فونتِ raw هست و نه نوعِ کامپوننتِ Unity UI.
        /// </summary>
        private UIText CreateText(Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            UIText label = UIText.Create(parent, "Text", value, size, color, anchor);
            label.autoFit = true;
            return label;
        }

        private Button CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action, Vector2 anchorMin, Vector2 anchorMax, int size)
        {
            GameObject buttonObject = CreatePanel("Button", parent, color, anchorMin, anchorMax);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 0.65f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
            buttonObject.AddComponent<ButtonFx>();
            UIText buttonText = CreateText(buttonObject.transform, label, size, Color.white, TextAnchor.MiddleCenter);
            SetRect(buttonText.Rect, Vector2.zero, Vector2.one, new Vector2(5f, 2f), new Vector2(-5f, -2f));
            return button;
        }

        private GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject objectInstance = new GameObject(name, typeof(RectTransform));
            objectInstance.transform.SetParent(parent, false);
            return objectInstance;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private void OnDestroy()
        {
            GameEvents.Notification -= OnNotification;
            GameEvents.StateChanged -= RefreshHud;
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        /// <summary>
        /// تغییر زبان: چون همه‌ی متن‌ها از جدول خوانده می‌شوند، کافی است صفحه‌ی فعلی دوباره
        /// ساخته شود. متن‌های صحنه‌های دستی هم با LocalizedText خودکار تازه می‌شوند.
        /// </summary>
        private void OnLanguageChanged(string language)
        {
            if (_splashActive) return;
            // برچسب‌هایِ کلید‌محور (UIText.SetKey) همین‌جا از جدول تازه می‌شوند…
            GameTextBackend.RebuildAll();
            // …و پنل‌هایی که داده‌ی زنده نشان می‌دهند دوباره ساخته می‌شوند.
            if (_hudActive) ShowGame(false);
            else ShowMainMenu();
        }
    }
}
