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
        private readonly Dictionary<ResourceType, Text> _resourceLabels = new Dictionary<ResourceType, Text>();

        private GameManager _game;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Font _font;
        private GameObject _view;
        private GameObject _hudRoot;
        private GameObject _buildPanel;
        private GameObject _technologyPanel;
        private GameObject _mapPanel;
        private GameObject _modalLayer;
        private Transform _survivorList;
        private Text _groupTitleLabel;
        private Text _clockLabel;
        private Text _weatherLabel;
        private Text _populationLabel;
        private Text _technologyLabel;
        private Text _notificationLabel;
        private Text _goalLabel;
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
            _font = Resources.Load<Font>("Fonts/DejaVuSans");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            CreateCanvas();
            GameEvents.Notification += OnNotification;
            GameEvents.StateChanged += RefreshHud;
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
            _hudActive = false;
            ClearView();
            _view = CreateRectObject("صفحه‌ی آغاز", _canvas.transform);
            CanvasGroup splashGroup = _view.AddComponent<CanvasGroup>();
            splashGroup.alpha = 0f;
            Image background = _view.AddComponent<Image>();
            background.color = DeepNavy;
            _view.AddComponent<SplashEffects>();

            GameObject glow = CreatePanel("هاله‌ی لوگو", _view.transform, new Color(0.06f, 0.38f, 0.42f, 0.42f), new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.76f));
            glow.AddComponent<GlowPulse>();

            GameObject goldHalo = CreatePanel("هاله‌ی طلایی", _view.transform, new Color(1f, 0.72f, 0.22f, 0.28f), new Vector2(0.32f, 0.66f), new Vector2(0.68f, 0.92f));
            goldHalo.AddComponent<GlowPulse>();
            GameObject crownObject = CreateRectObject("تاج طلایی", _view.transform);
            SetRect(crownObject.GetComponent<RectTransform>(), new Vector2(0.35f, 0.7f), new Vector2(0.65f, 0.9f), Vector2.zero, Vector2.zero);
            Text crown = crownObject.AddComponent<Text>();
            crown.font = _font;
            crown.fontSize = 58;
            crown.color = Gold;
            crown.alignment = TextAnchor.MiddleCenter;
            crown.text = "♛";
            crown.resizeTextForBestFit = true;
            crown.resizeTextMinSize = 40;
            crown.resizeTextMaxSize = 58;
            CrownPulse crownPulse = crownObject.AddComponent<CrownPulse>();
            crownPulse.Configure(goldHalo.transform);

            Text mark = CreateText(_view.transform, "Parsa Apps", 48, Color.white, TextAnchor.MiddleCenter);
            SetRect(mark.rectTransform, new Vector2(0.12f, 0.52f), new Vector2(0.88f, 0.68f), Vector2.zero, Vector2.zero);
            Text line = CreateText(_view.transform, "استودیوی بازی‌سازی پارسا", 22, Teal, TextAnchor.MiddleCenter);
            SetRect(line.rectTransform, new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.53f), Vector2.zero, Vector2.zero);
            Text loading = CreateText(_view.transform, "در حال آماده‌سازی سرزمین بقا", 18, Muted, TextAnchor.MiddleCenter);
            SetRect(loading.rectTransform, new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.22f), Vector2.zero, Vector2.zero);
            StartCoroutine(SplashRoutine(splashGroup));
        }

        public void ShowMainMenu()
        {
            _hudActive = false;
            ClearView();
            _view = CreateRectObject("منوی اصلی", _canvas.transform);
            Image background = _view.AddComponent<Image>();
            background.color = DeepNavy;
            CreatePanel("خط نور", _view.transform, new Color(0.05f, 0.3f, 0.34f, 0.28f), new Vector2(0f, 0.72f), new Vector2(1f, 1f));
            CreatePanel("نوار پایین", _view.transform, new Color(0.02f, 0.03f, 0.06f, 0.8f), new Vector2(0f, 0f), new Vector2(1f, 0.12f));

            Text studio = CreateText(_view.transform, "Parsa Apps", 22, Teal, TextAnchor.MiddleRight);
            SetRect(studio.rectTransform, new Vector2(0.58f, 0.9f), new Vector2(0.94f, 0.98f), Vector2.zero, Vector2.zero);
            Text title = CreateText(_view.transform, "سرزمین بقا", 52, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.61f), new Vector2(0.92f, 0.83f), Vector2.zero, Vector2.zero);
            Text subtitle = CreateText(_view.transform, "با همکاری، زنده می‌مانیم", 22, Teal, TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform, new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.64f), Vector2.zero, Vector2.zero);

            GameObject menuCard = CreatePanel("کارت منو", _view.transform, new Color(0.04f, 0.11f, 0.16f, 0.97f), new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.53f));
            CreateButton(menuCard.transform, "آغاز نجات", Teal, StartNewGame, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.92f), 22);
            Button continueButton = CreateButton(menuCard.transform, "ادامه‌ی سفر", new Color(0.26f, 0.53f, 0.72f), ContinueGame, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.65f), 19);
            continueButton.interactable = _game.HasSave;
            CreateButton(menuCard.transform, "تنظیمات", PanelBlueLight, ShowSettings, new Vector2(0.1f, 0.29f), new Vector2(0.9f, 0.46f), 19);
            CreateButton(menuCard.transform, "درباره‌ی سازنده", PanelBlueLight, ShowAbout, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.27f), 18);

            Text footer = CreateText(_view.transform, "ساخته شده توسط Parsa Apps  •  مدیریت: فرشاد پارسا", 15, Muted, TextAnchor.MiddleCenter);
            SetRect(footer.rectTransform, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.095f), Vector2.zero, Vector2.zero);
        }

        public void ShowGame(bool continuing)
        {
            _hudActive = true;
            ClearView();
            _view = CreateRectObject("رابط بازی", _canvas.transform);
            _view.AddComponent<CanvasGroup>();
            _hudRoot = _view;
            CreateGameHud();
            RefreshHud();
            if (continuing) OnNotification("سفر قبلی با موفقیت بازیابی شد.");
        }

        public void ShowTutorial()
        {
            CreateModal("راهنمای آغاز", (modal) =>
            {
                Text body = CreateText(modal.transform, "برای بقا، منابع را جمع کنید و نیازهای گروه را زیر نظر داشته باشید.\n\nاز نوار پایین ساخت‌وساز را باز کنید و یک سازه روی زمین انتخاب کنید. روی ساختمان ساخته‌شده بزنید تا ارتقا دهید.\n\nشب‌ها سایه‌ها حمله می‌کنند؛ برج دیده‌بانی و نگهبان‌ها از اردوگاه محافظت می‌کنند.", 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.8f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, "آماده‌ام؛ شروع کنیم", Teal, () => FinishTutorial(modal), new Vector2(0.16f, 0.14f), new Vector2(0.84f, 0.26f), 17);
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
            _modalLayer = CreatePanel("مکث", _canvas.transform, new Color(0.01f, 0.025f, 0.05f, 0.84f), Vector2.zero, Vector2.one);
            Text title = CreateText(_modalLayer.transform, "بازی متوقف شد", 36, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.18f, 0.65f), new Vector2(0.82f, 0.78f), Vector2.zero, Vector2.zero);
            CreateButton(_modalLayer.transform, "ادامه", Teal, _game.Resume, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.62f), 20);
            CreateButton(_modalLayer.transform, "ذخیره و منوی اصلی", PanelBlueLight, _game.ReturnToMenu, new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.47f), 18);
            CreateButton(_modalLayer.transform, "ثبت ذخیره", new Color(0.25f, 0.4f, 0.46f), _game.SaveGame, new Vector2(0.25f, 0.2f), new Vector2(0.75f, 0.32f), 18);
        }

        public void ShowResult(bool victory)
        {
            _hudActive = false;
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = CreatePanel("نتیجه", _canvas.transform, new Color(0.01f, 0.025f, 0.05f, 0.91f), Vector2.zero, Vector2.one);
            Text crown = CreateText(_modalLayer.transform, victory ? "♛" : "✦", 62, victory ? Gold : new Color(0.8f, 0.3f, 0.35f), TextAnchor.MiddleCenter);
            SetRect(crown.rectTransform, new Vector2(0.3f, 0.66f), new Vector2(0.7f, 0.86f), Vector2.zero, Vector2.zero);
            Text title = CreateText(_modalLayer.transform, victory ? "جزیره نجات یافت" : "چراغ اردوگاه خاموش شد", 31, Color.white, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            Text detail = CreateText(_modalLayer.transform, victory ? "گروه شما هفت روز را با همکاری پشت سر گذاشت." : "همه‌ی بازمانده‌ها از دست رفتند. از تجربه‌ی این سفر استفاده کنید.", 18, Muted, TextAnchor.MiddleCenter);
            SetRect(detail.rectTransform, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.54f), Vector2.zero, Vector2.zero);
            CreateButton(_modalLayer.transform, "سفر تازه", Teal, StartNewGame, new Vector2(0.2f, 0.24f), new Vector2(0.8f, 0.36f), 19);
            CreateButton(_modalLayer.transform, "بازگشت به منو", PanelBlueLight, _game.ReturnToMenu, new Vector2(0.2f, 0.09f), new Vector2(0.8f, 0.21f), 18);
        }

        public void RefreshHud()
        {
            if (!_hudActive || _game == null || _game.Resources == null) return;
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                if (_resourceLabels.TryGetValue(type, out Text label))
                {
                    string value = GameClock.ToPersianDigits(_game.Resources.Get(type).ToString());
                    PersianText.Set(label, GameText.ResourceName(type) + "  " + value);
                }
            }
            if (_clockLabel != null) PersianText.Set(_clockLabel, "روز " + GameClock.ToPersianDigits(_game.Clock.Day.ToString()) + "  •  " + _game.Clock.GetClockText());
            if (_weatherLabel != null) PersianText.Set(_weatherLabel, "☁  " + GameText.WeatherName(_game.Weather.Current));
            if (_populationLabel != null) PersianText.Set(_populationLabel, "گروه: " + GameClock.ToPersianDigits(_game.AliveSurvivorCount().ToString()) + " / " + GameClock.ToPersianDigits(_game.Survivors.Count.ToString()));
            if (_groupTitleLabel != null)
            {
                int level = _game.Progression == null ? 1 : _game.Progression.Level;
                PersianText.Set(_groupTitleLabel, "همراهان  •  مرحله‌ی گروه: " + GameClock.ToPersianDigits(level.ToString()));
            }
            if (_technologyLabel != null) PersianText.Set(_technologyLabel, "فناوری  " + GameClock.ToPersianDigits(_game.Technology.Points.ToString()));
            if (_goalLabel != null)
            {
                string quest = ActiveQuestText();
                string morale = _game.Survival == null ? "" : "  •  روحیه: " + GameClock.ToPersianDigits(Mathf.RoundToInt(_game.Survival.TeamMorale).ToString());
                PersianText.Set(_goalLabel, "هدف: زنده ماندن تا روز " + GameClock.ToPersianDigits("۷") + morale + quest);
            }
            if (_survivorRefreshTimer <= 0f)
            {
                _survivorRefreshTimer = 1f;
                RefreshSurvivors();
            }
        }

        private void CreateGameHud()
        {
            GameObject header = CreatePanel("نوار بالایی", _hudRoot.transform, new Color(0.02f, 0.08f, 0.12f, 0.96f), new Vector2(0f, 0.84f), Vector2.one);
            for (int i = 0; i < 6; i++)
            {
                ResourceType type = (ResourceType)i;
                GameObject card = CreatePanel("کارت منبع", header.transform, new Color(0.05f, 0.15f, 0.2f, 0.96f), new Vector2(i / 6f, 0.35f), new Vector2((i + 1) / 6f, 0.9f));
                Text label = CreateText(card.transform, GameText.ResourceName(type), 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 1f), new Vector2(-3f, -1f));
                _resourceLabels[type] = label;
            }
            _clockLabel = CreateText(header.transform, "", 17, Gold, TextAnchor.MiddleRight);
            SetRect(_clockLabel.rectTransform, new Vector2(0.54f, 0.03f), new Vector2(0.98f, 0.32f), Vector2.zero, Vector2.zero);
            _weatherLabel = CreateText(header.transform, "", 15, Muted, TextAnchor.MiddleLeft);
            SetRect(_weatherLabel.rectTransform, new Vector2(0.02f, 0.03f), new Vector2(0.4f, 0.32f), Vector2.zero, Vector2.zero);

            GameObject leftPanel = CreatePanel("گروه", _hudRoot.transform, new Color(0.025f, 0.09f, 0.13f, 0.92f), new Vector2(0.015f, 0.2f), new Vector2(0.275f, 0.82f));
            _groupTitleLabel = CreateText(leftPanel.transform, "همراهان", 19, Teal, TextAnchor.MiddleRight);
            SetRect(_groupTitleLabel.rectTransform, new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.99f), Vector2.zero, Vector2.zero);
            _survivorList = CreateRectObject("فهرست بازمانده‌ها", leftPanel.transform).transform;
            SetRect((RectTransform)_survivorList, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero);

            GameObject rightInfo = CreatePanel("اطلاعات", _hudRoot.transform, new Color(0.025f, 0.09f, 0.13f, 0.78f), new Vector2(0.73f, 0.71f), new Vector2(0.985f, 0.82f));
            _populationLabel = CreateText(rightInfo.transform, "", 15, Color.white, TextAnchor.MiddleRight);
            SetRect(_populationLabel.rectTransform, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero);
            _technologyLabel = CreateText(rightInfo.transform, "", 14, Gold, TextAnchor.MiddleRight);
            SetRect(_technologyLabel.rectTransform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject bottom = CreatePanel("نوار عملیات", _hudRoot.transform, new Color(0.02f, 0.07f, 0.11f, 0.97f), new Vector2(0f, 0f), new Vector2(1f, 0.2f));
            CreateButton(bottom.transform, "ساخت‌وساز", Teal, ToggleBuildPanel, new Vector2(0.02f, 0.55f), new Vector2(0.2f, 0.92f), 13);
            CreateButton(bottom.transform, "فناوری", new Color(0.38f, 0.55f, 0.85f), ToggleTechnologyPanel, new Vector2(0.21f, 0.55f), new Vector2(0.39f, 0.92f), 13);
            CreateButton(bottom.transform, "تربیت نیرو", new Color(0.55f, 0.38f, 0.68f), ShowTrainingPanel, new Vector2(0.4f, 0.55f), new Vector2(0.58f, 0.92f), 12);
            CreateButton(bottom.transform, "مأموریت‌ها", new Color(0.28f, 0.72f, 0.62f), ShowQuestsPanel, new Vector2(0.59f, 0.55f), new Vector2(0.77f, 0.92f), 13);
            CreateButton(bottom.transform, "دستاوردها", new Color(0.86f, 0.66f, 0.3f), ShowAchievementsPanel, new Vector2(0.78f, 0.55f), new Vector2(0.96f, 0.92f), 12);
            CreateButton(bottom.transform, "تجهیزات", new Color(0.3f, 0.52f, 0.55f), ShowEquipmentPanel, new Vector2(0.02f, 0.14f), new Vector2(0.17f, 0.48f), 12);
            CreateButton(bottom.transform, "یورش", new Color(0.7f, 0.36f, 0.3f), ShowRaidPanel, new Vector2(0.18f, 0.14f), new Vector2(0.33f, 0.48f), 12);
            CreateButton(bottom.transform, "نقشه", new Color(0.3f, 0.45f, 0.32f), ShowMap, new Vector2(0.34f, 0.14f), new Vector2(0.49f, 0.48f), 12);
            CreateButton(bottom.transform, "پاداش روز", new Color(0.24f, 0.55f, 0.42f), ShowDailyReward, new Vector2(0.5f, 0.14f), new Vector2(0.65f, 0.48f), 12);
            CreateButton(bottom.transform, "ذخیره", new Color(0.36f, 0.45f, 0.5f), _game.SaveGame, new Vector2(0.66f, 0.14f), new Vector2(0.81f, 0.48f), 12);
            CreateButton(bottom.transform, "مکث", new Color(0.28f, 0.34f, 0.4f), _game.TogglePause, new Vector2(0.82f, 0.14f), new Vector2(0.96f, 0.48f), 12);
            _goalLabel = CreateText(bottom.transform, "", 12, Muted, TextAnchor.MiddleLeft);
            SetRect(_goalLabel.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.12f), Vector2.zero, Vector2.zero);

            _notificationLabel = CreateText(_hudRoot.transform, "", 17, Color.white, TextAnchor.MiddleCenter);
            SetRect(_notificationLabel.rectTransform, new Vector2(0.29f, 0.18f), new Vector2(0.72f, 0.28f), Vector2.zero, Vector2.zero);
            CreatePanel("نشانه اعلان", _hudRoot.transform, new Color(0.03f, 0.15f, 0.17f, 0.4f), new Vector2(0.29f, 0.18f), new Vector2(0.72f, 0.28f)).transform.SetAsFirstSibling();
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
                GameObject row = CreatePanel("ردیف همراه", _survivorList, survivor.IsAlive ? new Color(0.06f, 0.16f, 0.2f, 0.92f) : new Color(0.18f, 0.08f, 0.1f, 0.8f), new Vector2(0.02f, top - 0.14f), new Vector2(0.98f, top - 0.01f));
                Text name = CreateText(row.transform, survivor.DisplayName, 15, survivor.IsAlive ? Color.white : new Color(0.75f, 0.5f, 0.5f), TextAnchor.MiddleRight);
                SetRect(name.rectTransform, new Vector2(0.39f, 0.48f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
                Text role = CreateText(row.transform, survivor.IsAlive ? GameText.RoleName(survivor.Role) : "از دست رفته", 11, Muted, TextAnchor.MiddleRight);
                SetRect(role.rectTransform, new Vector2(0.39f, 0.04f), new Vector2(0.96f, 0.48f), Vector2.zero, Vector2.zero);
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
                string detail = "سطح: " + GameClock.ToPersianDigits(building.Level.ToString()) + "\nسلامت: " + GameClock.ToPersianDigits(Mathf.RoundToInt(building.Health).ToString()) + " / ۱۰۰\n\nهزینه‌ی ارتقا:\n" + CostText(ConstructionSystem.GetUpgradeCosts(building.Type, building.Level));
                Text body = CreateText(modal.transform, detail, 17, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.8f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, "ارتقای ساختمان", Teal, () => UpgradeBuilding(building), new Vector2(0.18f, 0.2f), new Vector2(0.82f, 0.32f), 17);
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
            _buildPanel = CreatePanel("پنل ساخت", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.79f));
            Text title = CreateText(_buildPanel.transform, "انتخاب سازه", 20, Teal, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.89f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);
            BuildingType[] types = { BuildingType.House, BuildingType.Farm, BuildingType.WatchTower, BuildingType.Workshop, BuildingType.Wall, BuildingType.SolarStation };
            for (int i = 0; i < types.Length; i++)
            {
                int row = i / 2;
                int column = i % 2;
                BuildingType type = types[i];
                string label = GameText.BuildingName(type) + "\n" + CostText(ConstructionSystem.GetBuildCosts(type));
                CreateButton(_buildPanel.transform, label, i % 2 == 0 ? Teal : new Color(0.27f, 0.52f, 0.72f), () => SelectBuilding(type), new Vector2(0.07f + column * 0.47f, 0.65f - row * 0.17f), new Vector2(0.46f + column * 0.47f, 0.8f - row * 0.17f), 13);
            }
            CreateButton(_buildPanel.transform, "بستن", new Color(0.25f, 0.32f, 0.36f), ToggleBuildPanel, new Vector2(0.28f, 0.04f), new Vector2(0.72f, 0.15f), 16);
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
            CreateModal("مرکز تربیت نیرو", (modal) =>
            {
                Text body = CreateText(modal.transform, "کارگاه، محل آموزش نیروهای تازه است.\n\nهزینه‌ی هر نیروی نگهبان:\nغذا ۱۲  •  انرژی ۵  •  طلا ۳\n\nظرفیت گروه: " + GameClock.ToPersianDigits(_game.AliveSurvivorCount().ToString()) + " / ۱۰", 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.4f), new Vector2(0.92f, 0.79f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, "تربیت نگهبان", Teal, TrainGuard, new Vector2(0.16f, 0.23f), new Vector2(0.84f, 0.35f), 17);
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
            _technologyPanel = CreatePanel("پنل فناوری", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.78f));
            Text title = CreateText(_technologyPanel.transform, "درخت فناوری", 21, Gold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);
            Text points = CreateText(_technologyPanel.transform, "امتیاز پژوهش: " + GameClock.ToPersianDigits(_game.Technology.Points.ToString()), 14, Muted, TextAnchor.MiddleCenter);
            SetRect(points.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
            TechnologyType[] technologies = { TechnologyType.Cooperation, TechnologyType.WaterPurification, TechnologyType.ReinforcedWalls, TechnologyType.FieldRotation };
            for (int i = 0; i < technologies.Length; i++)
            {
                TechnologyType technology = technologies[i];
                string name = TechnologyName(technology) + (_game.Technology.IsUnlocked(technology) ? "  ✓" : "\nهزینه: ۲ امتیاز");
                CreateButton(_technologyPanel.transform, name, _game.Technology.IsUnlocked(technology) ? new Color(0.2f, 0.38f, 0.3f) : PanelBlueLight, () => Research(technology), new Vector2(0.08f, 0.64f - i * 0.13f), new Vector2(0.92f, 0.74f - i * 0.13f), 14);
            }
            CreateButton(_technologyPanel.transform, "بستن", new Color(0.25f, 0.32f, 0.36f), ToggleTechnologyPanel, new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.13f), 16);
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
            CreateModal("پاداش روزانه", (modal) =>
            {
                Text body = CreateText(modal.transform, "هر روز برای ماندن در کنار گروه پاداش بگیرید.\n\n" +
                    "پاداش امروز: ۱۲ غذا + ۲ طلا\n" +
                    "ردیف روزانه: " + GameClock.ToPersianDigits(streak.ToString()) + "\n\n" +
                    (claimable ? "پاداش امروز در انتظار شماست." : "پاداش امروز را دریافت کردید؛ فردا دوباره سر بزنید."),
                    17, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.8f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, "گرفتن پاداش", Teal, () => ClaimDaily(modal), new Vector2(0.2f, 0.26f), new Vector2(0.8f, 0.4f), 17);
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
            _buildPanel = CreatePanel("پنل مأموریت‌ها", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.82f));
            Text title = CreateText(_buildPanel.transform, "مأموریت‌های گروه", 22, Teal, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);

            IReadOnlyList<QuestRuntime> quests = _game.Quests == null ? (IReadOnlyList<QuestRuntime>)new List<QuestRuntime>() : _game.Quests.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime quest = quests[i];
                float top = 0.78f - i * 0.16f;
                string status = quest.Status == QuestStatus.Completed ? "✓" : (quest.Status == QuestStatus.Claimed ? "گرفته شد" : "در حال انجام");
                Color statusColor = quest.Status == QuestStatus.Completed ? Gold : (quest.Status == QuestStatus.Claimed ? Muted : Color.white);
                Text q = CreateText(_buildPanel.transform, quest.Definition.title + "\n" + quest.Definition.description + "\nپاداش: " + GameText.ResourceName(quest.Definition.rewardResource) + " " + GameClock.ToPersianDigits(quest.Definition.rewardAmount.ToString()) + "   •   " + status, 13, statusColor, TextAnchor.MiddleRight);
                SetRect(q.rectTransform, new Vector2(0.08f, top - 0.13f), new Vector2(0.92f, top), Vector2.zero, Vector2.zero);
            }
            CreateButton(_buildPanel.transform, "گرفتن پاداش‌های آماده", Teal, ClaimQuests, new Vector2(0.16f, 0.05f), new Vector2(0.84f, 0.16f), 15);
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
            _buildPanel = CreatePanel("پنل دستاوردها", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.82f));
            Text title = CreateText(_buildPanel.transform, "دستاوردها", 22, Gold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);

            AchievementId[] ids = { AchievementId.Builder, AchievementId.Defender, AchievementId.Scavenger, AchievementId.FirstNight, AchievementId.Survivor, AchievementId.Rich };
            string[] names = { "سازنده", "مدافع اردوگاه", "جمع‌آور", "دومین روز", "بازمانده‌ی ماهر", "ثروتمند" };
            for (int i = 0; i < ids.Length; i++)
            {
                float top = 0.78f - i * 0.1f;
                bool unlocked = _game.Achievements.IsUnlocked(ids[i]);
                Text row = CreateText(_buildPanel.transform, (unlocked ? "✓  " : "○  ") + names[i], 15, unlocked ? Gold : Muted, TextAnchor.MiddleRight);
                SetRect(row.rectTransform, new Vector2(0.08f, top - 0.08f), new Vector2(0.92f, top), Vector2.zero, Vector2.zero);
            }
        }

        public void ShowEquipmentPanel()
        {
            if (_buildPanel != null) Destroy(_buildPanel);
            if (_technologyPanel != null) Destroy(_technologyPanel);
            _buildPanel = CreatePanel("پنل تجهیزات", _hudRoot.transform, new Color(0.03f, 0.12f, 0.16f, 0.98f), new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.84f));
            Text title = CreateText(_buildPanel.transform, "تجهیزات گروه", 22, Teal, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);

            EquipmentType[] types = { EquipmentType.Tool, EquipmentType.Weapon, EquipmentType.Armor };
            for (int i = 0; i < types.Length; i++)
            {
                EquipmentType type = types[i];
                int level = _game.Equipment.Level(type);
                float top = 0.76f - i * 0.14f;
                bool can = _game.Equipment.CanUpgrade(type, out _);
                Color color = can ? Teal : PanelBlueLight;
                string label = _game.Equipment.Name(type) + "\n" +
                    "سطح: " + GameClock.ToPersianDigits(level.ToString()) + " / ۵\n" +
                    "هزینه: " + CostTextUtils(_game.Equipment.GetUpgradeCosts(type, level)) + (can ? "" : "\n(نیاز به مرحله‌ی بالاتر)");
                CreateButton(_buildPanel.transform, label, color, () => UpgradeEquipment(type), new Vector2(0.08f, top - 0.11f), new Vector2(0.92f, top), 13);
            }
            Text bonus = CreateText(_buildPanel.transform, "اثر فعلی\n" +
                "جمع‌آوری +" + Mathf.RoundToInt(_game.Equipment.ToolGatherBonus * 100f) + "٪\n" +
                "حمله +" + _game.Equipment.WeaponDamageBonus.ToString("0.0") + "\n" +
                "کاهش آسیب " + Mathf.RoundToInt(_game.Equipment.ArmorReduction * 100f) + "٪", 13, Muted, TextAnchor.MiddleCenter);
            SetRect(bonus.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.34f), Vector2.zero, Vector2.zero);
            CreateButton(_buildPanel.transform, "بستن", new Color(0.25f, 0.32f, 0.36f), () => { Destroy(_buildPanel); _buildPanel = null; }, new Vector2(0.3f, 0.0f), new Vector2(0.7f, 0.06f), 14);
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
            CreateModal("یورش به اردوگاه دشمن", (modal) =>
            {
                Text body = CreateText(modal.transform, "نگهبانان را برای غنیمت به سمت اردوگاه دشمن بفرستید.\n\n" +
                    "نگهبانان آماده: " + GameClock.ToPersianDigits(guards.ToString()) + "\n" +
                    "شانس پیروزی: " + GameClock.ToPersianDigits(chance.ToString()) + "٪\n" +
                    "هزینه: ۸ انرژی و ۳ طلا\n\n" +
                    "برنامه‌ی روزانه: " + GameClock.ToPersianDigits(_game.Raid.Wins.ToString()) + " برد / " + GameClock.ToPersianDigits(_game.Raid.Losses.ToString()) + " باخت", 16, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.4f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, "شروع یورش", new Color(0.7f, 0.36f, 0.3f), () => ExecuteRaid(modal), new Vector2(0.16f, 0.22f), new Vector2(0.84f, 0.36f), 17);
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
            _mapPanel = CreatePanel("نقشه‌ی جزیره", _canvas.transform, new Color(0.02f, 0.05f, 0.08f, 0.95f), new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.8f));
            Text title = CreateText(_mapPanel.transform, "نقشه‌ی جزیره", 22, Teal, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.92f), new Vector2(0.92f, 0.99f), Vector2.zero, Vector2.zero);
            GameObject mapArea = CreatePanel("قلمرو جزیره", _mapPanel.transform, new Color(0.12f, 0.2f, 0.16f, 0.9f), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));

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

            CreateButton(_mapPanel.transform, "بستن", new Color(0.25f, 0.32f, 0.36f), () => { Destroy(_mapPanel); _mapPanel = null; }, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.1f), 15);
        }

        private void AddMapMarker(Transform parent, Vector3 worldPosition, Color color, float size)
        {
            GameObject marker = CreateRectObject("نشانه", parent);
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
            CreateModal(story.title, (modal) =>
            {
                Text body = CreateText(modal.transform, story.body, 17, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
                for (int i = 0; i < story.choices.Count; i++)
                {
                    StoryChoice choice = story.choices[i];
                    float top = 0.46f - i * 0.14f;
                    CreateButton(modal.transform, choice.title, i % 2 == 0 ? Teal : PanelBlueLight, () => ResolveStory(modal, choice), new Vector2(0.1f, top - 0.11f), new Vector2(0.9f, top), 14);
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

        private string CostTextUtils(List<EquipmentUpgradeCost> costs)
        {
            if (costs == null || costs.Count == 0) return "رایگان";
            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++) parts.Add(GameText.ResourceName(costs[i].type) + " " + GameClock.ToPersianDigits(costs[i].amount.ToString()));
            return string.Join("  •  ", parts.ToArray());
        }

        private void ShowSettings()
        {
            CreateModal("تنظیمات", (modal) =>
            {
                Text hint = CreateText(modal.transform, "تنظیمات روی دستگاه ذخیره می‌شوند.", 15, Muted, TextAnchor.MiddleCenter);
                SetRect(hint.rectTransform, new Vector2(0.1f, 0.64f), new Vector2(0.9f, 0.75f), Vector2.zero, Vector2.zero);
                bool sound = _game.Audio == null || _game.Audio.SoundEnabled;
                bool vibration = _game.Audio == null || _game.Audio.VibrationEnabled;
                if (PlayerPrefs.HasKey("bazi_baqa_sound")) sound = PlayerPrefs.GetInt("bazi_baqa_sound", 1) == 1;
                if (PlayerPrefs.HasKey("bazi_baqa_vibration")) vibration = PlayerPrefs.GetInt("bazi_baqa_vibration", 1) == 1;
                CreateButton(modal.transform, sound ? "صدا: روشن" : "صدا: خاموش", sound ? Teal : PanelBlueLight, () => ToggleSound(modal), new Vector2(0.18f, 0.45f), new Vector2(0.82f, 0.58f), 18);
                CreateButton(modal.transform, vibration ? "لرزش لمسی: روشن" : "لرزش لمسی: خاموش", vibration ? Teal : PanelBlueLight, () => ToggleVibration(modal), new Vector2(0.18f, 0.29f), new Vector2(0.82f, 0.42f), 17);
            });
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
            CreateModal("درباره‌ی سازنده", (modal) =>
            {
                Text body = CreateText(modal.transform, "ساخته شده توسط\nParsa Apps\n\nمدیریت: فرشاد پارسا\n\nوب‌سایت رسمی:\nParsa-apps.github.io", 18, Color.white, TextAnchor.MiddleCenter);
                SetRect(body.rectTransform, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
                CreateButton(modal.transform, "باز کردن وب‌سایت", Teal, () => Application.OpenURL("https://Parsa-apps.github.io"), new Vector2(0.2f, 0.14f), new Vector2(0.8f, 0.25f), 16);
            });
        }

        private void CreateModal(string titleText, Action<GameObject> content)
        {
            if (_modalLayer != null) Destroy(_modalLayer);
            _modalLayer = CreatePanel("پنجره", _canvas.transform, new Color(0.01f, 0.025f, 0.05f, 0.86f), Vector2.zero, Vector2.one);
            GameObject card = CreatePanel("کارت پنجره", _modalLayer.transform, new Color(0.035f, 0.12f, 0.17f, 0.99f), new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.84f));
            Text title = CreateText(card.transform, titleText, 26, Teal, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            CreateButton(card.transform, "×", new Color(0.4f, 0.16f, 0.2f), () => { Destroy(_modalLayer); _modalLayer = null; }, new Vector2(0.82f, 0.88f), new Vector2(0.96f, 0.99f), 19);
            content(card);
        }

        private void OnNotification(string message)
        {
            if (_notificationLabel == null) return;
            PersianText.Set(_notificationLabel, message);
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
            if (_game.Quests == null || _game.Quests.Quests.Count == 0) return "";
            for (int i = 0; i < _game.Quests.Quests.Count; i++)
            {
                QuestRuntime quest = _game.Quests.Quests[i];
                if (quest.Status == QuestStatus.Active) return "  •  مأموریت: " + quest.Definition.title;
            }
            return "";
        }

        private string CostText(List<ResourceCost> costs)
        {
            if (costs == null || costs.Count == 0) return "رایگان";
            List<string> values = new List<string>();
            for (int i = 0; i < costs.Count; i++) values.Add(GameText.ResourceName(costs[i].type) + " " + GameClock.ToPersianDigits(costs[i].amount.ToString()));
            return string.Join("  •  ", values.ToArray());
        }

        private static string TechnologyName(TechnologyType type)
        {
            switch (type)
            {
                case TechnologyType.Cooperation: return "همکاری گروهی";
                case TechnologyType.WaterPurification: return "تصفیه‌ی آب";
                case TechnologyType.ReinforcedWalls: return "دیوار تقویت‌شده";
                case TechnologyType.FieldRotation: return "کشت چرخشی";
                default: return "فناوری";
            }
        }

        private void CreateStatusBar(Transform parent, Vector2 position, float ratio, Color color)
        {
            GameObject bar = CreatePanel("نوار وضعیت", parent, new Color(0f, 0f, 0f, 0.34f), position, position + new Vector2(0.3f, 0.12f));
            GameObject fill = CreatePanel("مقدار وضعیت", bar.transform, color, Vector2.zero, new Vector2(Mathf.Clamp01(ratio), 1f));
        }

        private Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("بوم رابط کاربری");
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
            return panel;
        }

        private Text CreateText(Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            GameObject textObject = CreateRectObject("متن", parent);
            Text label = textObject.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, size - 5);
            label.resizeTextMaxSize = size;
            PersianText.Set(label, value);
            return label;
        }

        private Button CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action, Vector2 anchorMin, Vector2 anchorMax, int size)
        {
            GameObject buttonObject = CreatePanel("دکمه", parent, color, anchorMin, anchorMax);
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
            Text buttonText = CreateText(buttonObject.transform, label, size, Color.white, TextAnchor.MiddleCenter);
            SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 2f), new Vector2(-5f, -2f));
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
        }
    }
}
