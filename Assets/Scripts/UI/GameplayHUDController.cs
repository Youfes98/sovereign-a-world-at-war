// GameplayHUDController.cs
// Manages the in-game HUD: top bar resources, left nav, bottom tabs.
// Subscribes to game state events for live data updates.

using UnityEngine;
using UnityEngine.UIElements;
using WarStrategy.Core;
using WarStrategy.Data;

namespace WarStrategy.UI
{
    public class GameplayHUDController
    {
        private VisualElement _root;

        // Top bar elements
        private VisualElement _playerFlag;
        private Label _playerName;
        private Label _gameDate;
        private Label _gdpValue, _manpowerValue, _balanceValue;
        private Label _popValue, _militaryValue, _stabilityValue;
        private Label _speedLabel;
        private Button _pauseBtn, _speedUpBtn, _speedDownBtn, _menuBtn;

        // Left nav buttons
        private Button _btnDiplomacy, _btnResearch, _btnProduction, _btnEspionage;

        // Bottom tabs
        private Button _tabEconomy, _tabMilitary, _tabTechnology, _tabDiplomacy;
        private Button _activeTab;

        public void Initialize(VisualElement root)
        {
            _root = root;

            // Top bar
            _playerFlag = root.Q("player-flag");
            _playerName = root.Q<Label>("player-name");
            _gameDate = root.Q<Label>("game-date");
            _gdpValue = root.Q<Label>("gdp-value");
            _manpowerValue = root.Q<Label>("manpower-value");
            _balanceValue = root.Q<Label>("balance-value");
            _popValue = root.Q<Label>("pop-value");
            _militaryValue = root.Q<Label>("military-value");
            _stabilityValue = root.Q<Label>("stability-value");
            _speedLabel = root.Q<Label>("speed-label");

            // Speed controls
            _pauseBtn = root.Q<Button>("pause-btn");
            _speedUpBtn = root.Q<Button>("speed-up-btn");
            _speedDownBtn = root.Q<Button>("speed-down-btn");
            _menuBtn = root.Q<Button>("menu-btn");

            if (_pauseBtn != null) _pauseBtn.clicked += OnPauseClicked;
            if (_speedUpBtn != null) _speedUpBtn.clicked += OnSpeedUpClicked;
            if (_speedDownBtn != null) _speedDownBtn.clicked += OnSpeedDownClicked;

            // Left nav
            _btnDiplomacy = root.Q<Button>("btn-diplomacy");
            _btnResearch = root.Q<Button>("btn-research");
            _btnProduction = root.Q<Button>("btn-production");
            _btnEspionage = root.Q<Button>("btn-espionage");

            // Bottom tabs
            _tabEconomy = root.Q<Button>("tab-economy");
            _tabMilitary = root.Q<Button>("tab-military");
            _tabTechnology = root.Q<Button>("tab-technology");
            _tabDiplomacy = root.Q<Button>("tab-diplomacy");

            if (_tabEconomy != null) _tabEconomy.clicked += () => SetActiveTab(_tabEconomy);
            if (_tabMilitary != null) _tabMilitary.clicked += () => SetActiveTab(_tabMilitary);
            if (_tabTechnology != null) _tabTechnology.clicked += () => SetActiveTab(_tabTechnology);
            if (_tabDiplomacy != null) _tabDiplomacy.clicked += () => SetActiveTab(_tabDiplomacy);

            _activeTab = _tabEconomy;

            // Subscribe to game events
            if (Services.GameState != null)
                Services.GameState.CountryDataChanged += OnCountryDataChanged;
        }

        /// <summary>
        /// Populate the HUD with the player's country data.
        /// Called once when entering gameplay phase.
        /// </summary>
        public void SetPlayerCountry(string iso)
        {
            if (Services.GameState == null) return;
            if (!Services.GameState.Countries.TryGetValue(iso, out var country)) return;

            // Flag
            if (_playerFlag != null)
            {
                var flagTex = Resources.Load<Texture2D>($"Flags/{country.Iso2}");
                if (flagTex != null)
                    _playerFlag.style.backgroundImage = new StyleBackground(flagTex);
            }

            // Name
            if (_playerName != null)
                _playerName.text = country.Name;

            UpdateResourceDisplay(country);
        }

        /// <summary>
        /// Called every frame or on tick to update date display.
        /// </summary>
        public void UpdateDate()
        {
            if (_gameDate != null && Services.Clock != null)
                _gameDate.text = Services.Clock.GetDateString();
        }

        private void OnCountryDataChanged(string iso)
        {
            if (Services.GameState == null) return;
            if (iso != Services.GameState.PlayerIso) return;
            if (!Services.GameState.Countries.TryGetValue(iso, out var country)) return;
            UpdateResourceDisplay(country);
        }

        private void UpdateResourceDisplay(CountryData country)
        {
            if (_gdpValue != null)
                _gdpValue.text = FormatCurrency(country.GdpRawBillions);

            if (_manpowerValue != null)
                _manpowerValue.text = FormatPopulation(country.Population / 10); // military-age approx

            if (_balanceValue != null)
            {
                float balance = country.MonthlyBalance;
                _balanceValue.text = (balance >= 0 ? "+" : "") + FormatCurrency(balance);
                _balanceValue.style.color = balance >= 0
                    ? new Color(0.30f, 0.69f, 0.31f) // green
                    : new Color(0.75f, 0.22f, 0.17f); // red
            }

            if (_popValue != null)
                _popValue.text = FormatPopulation(country.Population);

            if (_militaryValue != null)
                _militaryValue.text = country.MilitaryNormalized.ToString();

            if (_stabilityValue != null)
                _stabilityValue.text = country.Stability.ToString();
        }

        private void OnPauseClicked()
        {
            if (Services.Clock == null) return;
            Services.Clock.TogglePause();
            UpdateSpeedDisplay();
        }

        private void OnSpeedUpClicked()
        {
            if (Services.Clock == null) return;
            Services.Clock.SetSpeed(Mathf.Min(Services.Clock.Speed + 1, 5));
            UpdateSpeedDisplay();
        }

        private void OnSpeedDownClicked()
        {
            if (Services.Clock == null) return;
            Services.Clock.SetSpeed(Mathf.Max(Services.Clock.Speed - 1, 1));
            UpdateSpeedDisplay();
        }

        private void UpdateSpeedDisplay()
        {
            if (_speedLabel == null || Services.Clock == null) return;
            if (Services.Clock.Paused)
            {
                _speedLabel.text = "II";
                _speedLabel.style.color = new Color(0.75f, 0.22f, 0.17f);
            }
            else
            {
                _speedLabel.text = $"{Services.Clock.Speed}x";
                _speedLabel.style.color = new Color(0.84f, 0.70f, 0.42f); // gold
            }
        }

        private void SetActiveTab(Button tab)
        {
            if (_activeTab == tab) return;

            // Deactivate old tab
            if (_activeTab != null)
            {
                _activeTab.style.backgroundColor = new Color(1, 1, 1, 0.03f);
                _activeTab.style.borderTopColor = new Color(1, 1, 1, 0.06f);
                _activeTab.style.borderBottomColor = new Color(1, 1, 1, 0.06f);
                _activeTab.style.borderLeftColor = new Color(1, 1, 1, 0.06f);
                _activeTab.style.borderRightColor = new Color(1, 1, 1, 0.06f);
                _activeTab.style.color = new Color(0.53f, 0.60f, 0.67f);
            }

            // Activate new tab
            _activeTab = tab;
            if (_activeTab != null)
            {
                _activeTab.style.backgroundColor = new Color(0.84f, 0.70f, 0.42f, 0.15f);
                _activeTab.style.borderTopColor = new Color(0.84f, 0.70f, 0.42f, 0.3f);
                _activeTab.style.borderBottomColor = new Color(0.84f, 0.70f, 0.42f, 0.3f);
                _activeTab.style.borderLeftColor = new Color(0.84f, 0.70f, 0.42f, 0.3f);
                _activeTab.style.borderRightColor = new Color(0.84f, 0.70f, 0.42f, 0.3f);
                _activeTab.style.color = new Color(0.84f, 0.70f, 0.42f);
            }
        }

        public void Cleanup()
        {
            if (Services.GameState != null)
                Services.GameState.CountryDataChanged -= OnCountryDataChanged;
        }

        // ── Formatting Helpers ──

        private static string FormatCurrency(float billions)
        {
            if (billions >= 1000f) return $"${billions / 1000f:F1}T";
            if (billions >= 1f) return $"${billions:F1}B";
            if (billions >= 0.001f) return $"${billions * 1000f:F0}M";
            if (billions <= -1000f) return $"-${-billions / 1000f:F1}T";
            if (billions <= -1f) return $"-${-billions:F1}B";
            return "$0";
        }

        private static string FormatPopulation(long pop)
        {
            if (pop >= 1_000_000_000) return $"{pop / 1_000_000_000f:F1}B";
            if (pop >= 1_000_000) return $"{pop / 1_000_000f:F1}M";
            if (pop >= 1_000) return $"{pop / 1_000f:F0}K";
            return pop.ToString();
        }
    }
}
