// GameFlowController.cs
// Master UI controller — owns the UIDocument, all panel controllers, and the game phase state machine.
// Runs after Bootstrap (-1000) and SceneSetup (-999) but before normal scripts.
// Drives: Loading → MainMenu → CountrySelection → Gameplay transitions.

using System;
using UnityEngine;
using UnityEngine.UIElements;
using WarStrategy.Core;
using WarStrategy.Map;

namespace WarStrategy.UI
{
    public enum GamePhase
    {
        Loading,
        MainMenu,
        CountrySelection,
        Gameplay
    }

    [DefaultExecutionOrder(-900)]
    public class GameFlowController : MonoBehaviour
    {
        // ── State ──
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Loading;
        public event Action<GamePhase> PhaseChanged;

        // ── UI Toolkit ──
        private UIDocument _uiDocument;
        private VisualElement _uiRoot;

        // ── Sub-controllers ──
        private MainMenuController _mainMenu;
        private CountrySelectionController _countrySelect;
        private TransitionController _transition;

        // ── UXML templates (loaded from Resources/UI/) ──
        private VisualTreeAsset _mainMenuTemplate;
        private VisualTreeAsset _countrySelectTemplate;
        private VisualTreeAsset _loadingTemplate;
        private VisualTreeAsset _gameplayHudTemplate;

        // ── Panel roots (instantiated from templates) ──
        private VisualElement _loadingPanel;
        private VisualElement _mainMenuPanel;
        private VisualElement _countrySelectPanel;
        private VisualElement _gameplayHudPanel;

        // ── Cached scene references ──
        private MapCamera _mapCamera;
        private ProvinceClickHandler _clickHandler;
        private MenuCameraController _menuCamera;

        // ── Data load tracking ──
        private bool _gameStateLoaded;
        private bool _provinceDBLoaded;

        // ── Selected country for confirmation ──
        private string _pendingCountryIso;

        // ────────────────────────────────────────────
        // Lifecycle
        // ────────────────────────────────────────────

        private void Awake()
        {
            // Create UIDocument on a child GameObject
            var uiGO = new GameObject("UIRoot");
            uiGO.transform.SetParent(transform);
            _uiDocument = uiGO.AddComponent<UIDocument>();

            // Try to load the editor-created PanelSettings asset (has proper theme)
            // Create it via menu: WarStrategy > Create Panel Settings
            var ps = Resources.Load<PanelSettings>("UI/GamePanelSettings");

            if (ps == null)
            {
                // Fallback: create programmatically (will show theme warning but still works)
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ps.match = 0.5f;
                ps.name = "GameFlowPanelSettings";
                Debug.LogWarning("[GameFlow] GamePanelSettings.asset not found. Run: WarStrategy > Create Panel Settings");
            }

            _uiDocument.panelSettings = ps;
            _uiDocument.sortingOrder = 100;
        }

        private void Start()
        {
            // Cache scene references
            _mapCamera = FindAnyObjectByType<MapCamera>();
            _clickHandler = FindAnyObjectByType<ProvinceClickHandler>();

            // Subscribe to data load completion
            if (Services.GameState != null)
            {
                if (Services.GameState.IsLoaded)
                    _gameStateLoaded = true;
                else
                    Services.GameState.OnCountryDataLoaded += OnGameStateLoaded;
            }

            if (Services.ProvinceDB != null)
            {
                if (Services.ProvinceDB.IsLoaded)
                    _provinceDBLoaded = true;
                else
                    Services.ProvinceDB.OnDataLoaded += OnProvinceDBLoaded;
            }

            // Subscribe to country selection for CountrySelection phase
            if (Services.GameState != null)
                Services.GameState.CountrySelected += OnCountryClicked;

            // Load UXML templates from Resources/UI/
            _mainMenuTemplate = Resources.Load<VisualTreeAsset>("UI/MainMenu");
            _countrySelectTemplate = Resources.Load<VisualTreeAsset>("UI/CountrySelect");
            _loadingTemplate = Resources.Load<VisualTreeAsset>("UI/Loading");
            _gameplayHudTemplate = Resources.Load<VisualTreeAsset>("UI/GameplayHUD");

            // Get the UIDocument root
            _uiRoot = _uiDocument.rootVisualElement;
            _uiRoot.style.flexGrow = 1;

            // Load stylesheets and apply to root (UXML relative paths don't work with Resources.Load)
            var themeUSS = Resources.Load<StyleSheet>("UI/GrandStrategyTheme");
            var mainMenuUSS = Resources.Load<StyleSheet>("UI/MainMenu");
            var countrySelectUSS = Resources.Load<StyleSheet>("UI/CountrySelection");
            var countryInfoUSS = Resources.Load<StyleSheet>("UI/CountryInfoPanel");

            if (themeUSS != null) _uiRoot.styleSheets.Add(themeUSS);
            else Debug.LogWarning("[GameFlow] GrandStrategyTheme.uss not found in Resources/UI/");
            if (mainMenuUSS != null) _uiRoot.styleSheets.Add(mainMenuUSS);
            if (countrySelectUSS != null) _uiRoot.styleSheets.Add(countrySelectUSS);
            if (countryInfoUSS != null) _uiRoot.styleSheets.Add(countryInfoUSS);

            // Initialize transition overlay (must be first — sits on top)
            _transition = new TransitionController();
            _transition.Initialize(_uiRoot);

            // Build panels from templates (or create placeholder containers)
            _loadingPanel = InstantiatePanel(_loadingTemplate, "loading-panel");
            _mainMenuPanel = InstantiatePanel(_mainMenuTemplate, "main-menu-panel");
            _countrySelectPanel = InstantiatePanel(_countrySelectTemplate, "country-select-panel");
            _gameplayHudPanel = InstantiatePanel(_gameplayHudTemplate, "gameplay-hud-panel");

            // Initialize sub-controllers
            _mainMenu = new MainMenuController();
            _mainMenu.Initialize(
                _mainMenuPanel,
                onNewGame: () => EnterPhase(GamePhase.CountrySelection),
                onLoadGame: () => Debug.Log("[GameFlow] Load game not yet implemented"),
                onExit: () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            );

            // Initialize country selection controller
            _countrySelect = new CountrySelectionController();
            _countrySelect.Initialize(
                _countrySelectPanel,
                onPlayAs: iso =>
                {
                    _pendingCountryIso = iso;
                    OnPlayAsConfirmed();
                },
                _mapCamera
            );

            var backBtn = _countrySelectPanel.Q<Button>("back-btn");
            if (backBtn != null)
                backBtn.clicked += () => EnterPhase(GamePhase.MainMenu);

            // Start in Loading phase
            EnterPhase(GamePhase.Loading);

            Debug.Log("[GameFlow] Initialized. Waiting for data to load...");
        }

        private void Update()
        {
            // ── Loading phase: poll for completion ──
            if (CurrentPhase == GamePhase.Loading)
            {
                if (_gameStateLoaded && _provinceDBLoaded)
                {
                    Debug.Log("[GameFlow] All data loaded. Transitioning to MainMenu.");
                    EnterPhase(GamePhase.MainMenu);
                }
                return;
            }

            // ── ESC key handling ──
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                switch (CurrentPhase)
                {
                    case GamePhase.Gameplay:
                        EnterPhase(GamePhase.MainMenu);
                        break;
                    case GamePhase.CountrySelection:
                        EnterPhase(GamePhase.MainMenu);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            if (Services.GameState != null)
            {
                Services.GameState.OnCountryDataLoaded -= OnGameStateLoaded;
                Services.GameState.CountrySelected -= OnCountryClicked;
            }

            if (Services.ProvinceDB != null)
                Services.ProvinceDB.OnDataLoaded -= OnProvinceDBLoaded;

            _countrySelect?.Cleanup();
        }

        // ────────────────────────────────────────────
        // Phase state machine
        // ────────────────────────────────────────────

        public void EnterPhase(GamePhase phase)
        {
            var previousPhase = CurrentPhase;
            CurrentPhase = phase;

            // Hide all panels first
            HideAllPanels();

            switch (phase)
            {
                case GamePhase.Loading:
                    ShowPanel(_loadingPanel);
                    SetMapInputEnabled(false);
                    SetMenuCamera(false);
                    break;

                case GamePhase.MainMenu:
                    ShowPanel(_mainMenuPanel);
                    SetMapInputEnabled(false);
                    SetMenuCamera(true);

                    // Pause the game clock when returning to menu
                    if (Services.Clock != null)
                        Services.Clock.SetPaused(true);

                    // Clear any pending selection
                    _pendingCountryIso = null;
                    break;

                case GamePhase.CountrySelection:
                    ShowPanel(_countrySelectPanel);
                    SetMapInputEnabled(true);   // Allow clicking countries
                    SetMenuCamera(false);       // Stop autopan, let player navigate

                    // Unlock camera for manual navigation
                    if (_mapCamera != null)
                        _mapCamera.InputLocked = false;

                    _countrySelect?.Show();
                    _pendingCountryIso = null;
                    break;

                case GamePhase.Gameplay:
                    ShowPanel(_gameplayHudPanel);
                    SetMapInputEnabled(true);
                    SetMenuCamera(false);

                    // Commit the selected country and start the clock
                    if (!string.IsNullOrEmpty(_pendingCountryIso) && Services.GameState != null)
                    {
                        Services.GameState.SetPlayerCountry(_pendingCountryIso);

                        // Populate gameplay HUD with player info
                        PopulateGameplayHUD(_pendingCountryIso);
                    }

                    // Clear country highlight from selection phase
                    var mr = FindAnyObjectByType<MapRenderer>();
                    if (mr != null) mr.SetCountryHighlight(-1f, 0f);

                    if (Services.Clock != null)
                        Services.Clock.SetPaused(false);

                    break;
            }

            PhaseChanged?.Invoke(phase);
            Debug.Log($"[GameFlow] Phase: {previousPhase} → {phase}");
        }

        // ────────────────────────────────────────────
        // Data load callbacks
        // ────────────────────────────────────────────

        private void OnGameStateLoaded()
        {
            _gameStateLoaded = true;
            Debug.Log("[GameFlow] GameState data loaded.");
        }

        private void OnProvinceDBLoaded()
        {
            _provinceDBLoaded = true;
            Debug.Log("[GameFlow] ProvinceDB data loaded.");
        }

        // ────────────────────────────────────────────
        // Country selection callbacks
        // ────────────────────────────────────────────

        private void OnCountryClicked(string iso)
        {
            // CountrySelectionController handles this via its own event subscription
            if (CurrentPhase != GamePhase.CountrySelection) return;
            _pendingCountryIso = iso;
        }

        private void OnPlayAsConfirmed()
        {
            if (string.IsNullOrEmpty(_pendingCountryIso)) return;

            _transition.FadeOut(0.4f, () =>
            {
                EnterPhase(GamePhase.Gameplay);
                _transition.FadeIn(0.6f, null);
            });
        }

        // ────────────────────────────────────────────
        // Panel helpers
        // ────────────────────────────────────────────

        private VisualElement InstantiatePanel(VisualTreeAsset template, string fallbackName)
        {
            VisualElement panel;

            if (template != null)
            {
                panel = template.Instantiate();
                panel.name = fallbackName;
            }
            else
            {
                // Create empty container if template not found — avoids null errors
                panel = new VisualElement { name = fallbackName };
                Debug.LogWarning($"[GameFlow] UXML template not found for '{fallbackName}'. Using empty container.");
            }

            panel.style.position = Position.Absolute;
            panel.style.left = 0;
            panel.style.top = 0;
            panel.style.right = 0;
            panel.style.bottom = 0;
            panel.style.display = DisplayStyle.None;
            panel.pickingMode = PickingMode.Ignore;

            _uiRoot.Add(panel);
            return panel;
        }

        private void HideAllPanels()
        {
            _loadingPanel.style.display = DisplayStyle.None;
            _mainMenuPanel.style.display = DisplayStyle.None;
            _countrySelectPanel.style.display = DisplayStyle.None;
            _gameplayHudPanel.style.display = DisplayStyle.None;
        }

        private void ShowPanel(VisualElement panel)
        {
            panel.style.display = DisplayStyle.Flex;
        }

        private void PopulateGameplayHUD(string iso)
        {
            if (_gameplayHudPanel == null || Services.GameState == null) return;
            if (!Services.GameState.Countries.TryGetValue(iso, out var country)) return;

            // Player flag (circular)
            var flagEl = _gameplayHudPanel.Q("player-flag");
            if (flagEl != null)
            {
                var flagTex = Resources.Load<Texture2D>($"Flags/{country.Iso2}");
                if (flagTex != null)
                    flagEl.style.backgroundImage = new StyleBackground(flagTex);
            }

            // Player name
            var nameLabel = _gameplayHudPanel.Q<Label>("player-name");
            if (nameLabel != null) nameLabel.text = country.Name;

            // Date
            var dateLabel = _gameplayHudPanel.Q<Label>("game-date");
            if (dateLabel != null && Services.Clock != null)
                dateLabel.text = Services.Clock.GetDateString();
        }

        // ────────────────────────────────────────────
        // Map / camera control helpers
        // ────────────────────────────────────────────

        private void SetMapInputEnabled(bool enabled)
        {
            if (_clickHandler != null)
                _clickHandler.InputEnabled = enabled;

            if (_mapCamera != null)
                _mapCamera.InputLocked = !enabled;
        }

        private void SetMenuCamera(bool enabled)
        {
            if (_menuCamera == null)
            {
                // Lazily find or create the MenuCameraController
                _menuCamera = FindAnyObjectByType<MenuCameraController>();

                if (_menuCamera == null && _mapCamera != null)
                {
                    _menuCamera = _mapCamera.gameObject.AddComponent<MenuCameraController>();
                }
            }

            if (_menuCamera != null)
                _menuCamera.enabled = enabled;

            // When menu camera is active, lock manual input
            if (_mapCamera != null && enabled)
                _mapCamera.InputLocked = true;
        }

        // ────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────

        /// <summary>
        /// Access the transition controller for external fade effects.
        /// </summary>
        public TransitionController Transition => _transition;

        /// <summary>
        /// The root VisualElement of the UI overlay.
        /// </summary>
        public VisualElement UIRoot => _uiRoot;
    }
}
