// CountrySelectionController.cs
// Map-first country selection: click a country on the map → floating card appears.
// No sidebar list — the map IS the selection interface.

using UnityEngine;
using UnityEngine.UIElements;
using WarStrategy.Core;
using WarStrategy.Data;
using WarStrategy.Map;

namespace WarStrategy.UI
{
    public class CountrySelectionController
    {
        private VisualElement _root;
        private VisualElement _card;
        private System.Action<string> _onPlayAs;
        private MapCamera _mapCamera;
        private string _selectedIso = "";

        // Cached references for country highlight
        private MapRenderer _mapRenderer;
        private BorderRenderer _borderRenderer;

        // Card elements
        private VisualElement _flagImage;
        private Label _nameLabel, _govtLabel, _regionLabel;
        private Label _gdpValue, _popValue, _milValue, _stabilityValue;
        private VisualElement _gdpBar, _popBar, _milBar, _stabilityBar;
        private Label _tierBadge, _difficultyLabel;
        private Button _playBtn;

        public void Initialize(VisualElement root, System.Action<string> onPlayAs, MapCamera mapCamera)
        {
            _root = root;
            _onPlayAs = onPlayAs;
            _mapCamera = mapCamera;

            // Query card elements
            _card = root.Q("country-card");
            _flagImage = root.Q("flag-image");
            _nameLabel = root.Q<Label>("country-name");
            _govtLabel = root.Q<Label>("govt-type");
            _regionLabel = root.Q<Label>("region-label");
            _gdpValue = root.Q<Label>("gdp-value");
            _popValue = root.Q<Label>("pop-value");
            _milValue = root.Q<Label>("mil-value");
            _stabilityValue = root.Q<Label>("stability-value");
            _gdpBar = root.Q("gdp-bar");
            _popBar = root.Q("pop-bar");
            _milBar = root.Q("mil-bar");
            _stabilityBar = root.Q("stability-bar");
            _tierBadge = root.Q<Label>("tier-badge");
            _difficultyLabel = root.Q<Label>("difficulty-label");
            _playBtn = root.Q<Button>("play-btn");

            if (_playBtn != null)
                _playBtn.clicked += () => { if (!string.IsNullOrEmpty(_selectedIso)) _onPlayAs?.Invoke(_selectedIso); };

            // Cache renderer references for country highlight
            _mapRenderer = Object.FindAnyObjectByType<MapRenderer>();
            _borderRenderer = Object.FindAnyObjectByType<BorderRenderer>();

            // Subscribe to country selection (from map clicks)
            if (Services.GameState != null)
                Services.GameState.CountrySelected += OnCountrySelected;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            // Clear any country highlight from previous selection
            if (_mapRenderer != null)
                _mapRenderer.SetCountryHighlight(-1f, 0f);
            // Hide card until a country is clicked
            if (_card != null)
            {
                _card.style.display = DisplayStyle.None;
                _card.style.translate = new Translate(320, 0);
                _card.style.opacity = 0f;
            }
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            // Clear country highlight
            if (_mapRenderer != null)
                _mapRenderer.SetCountryHighlight(-1f, 0f);
            if (_card != null)
            {
                // Reset card to hidden state
                _card.style.translate = new Translate(320, 0);
                _card.style.opacity = 0f;
                _card.style.display = DisplayStyle.None;
            }
            _selectedIso = "";
        }

        public void Cleanup()
        {
            if (Services.GameState != null)
                Services.GameState.CountrySelected -= OnCountrySelected;
        }

        private void OnCountrySelected(string iso)
        {
            // Skip zoom if same country already selected (user is clicking provinces within it)
            bool sameCountry = iso == _selectedIso;
            _selectedIso = iso;

            if (Services.GameState == null) return;
            if (!Services.GameState.Countries.TryGetValue(iso, out var country)) return;

            if (!sameCountry)
                ShowCountryCard(country);

            // Highlight the selected country on the map
            if (_mapRenderer != null && _borderRenderer != null)
            {
                int ownerId = _borderRenderer.GetOwnerId(iso);
                _mapRenderer.SetCountryHighlight(ownerId / 255f, 0.85f);
            }

            // Smart zoom: only when selecting a NEW country
            if (!sameCountry && _mapCamera != null && Services.ProvinceDB != null)
            {
                var (center, size) = Services.ProvinceDB.GetCountryBounds(iso);

                // Calculate zoom to fit the country with 30% padding
                float padding = 1.4f;
                Camera cam = _mapCamera.Camera;
                float aspect = cam.aspect;

                // The zoom that fits the country horizontally vs vertically
                float zoomToFitWidth = (MapCamera.MAP_HEIGHT / 2f) / ((size.x * padding) / (2f * aspect));
                float zoomToFitHeight = (MapCamera.MAP_HEIGHT / 2f) / ((size.y * padding) / 2f);
                float targetZoom = Mathf.Min(zoomToFitWidth, zoomToFitHeight);

                // Clamp to reasonable range (don't zoom too far in for tiny countries or out for huge ones)
                targetZoom = Mathf.Clamp(targetZoom, 1.5f, 12f);

                // Pan to center (Y is negated in map space)
                _mapCamera.PanTo(new Vector2(center.x, -center.y), 0.8f, targetZoom);
            }
        }

        private void ShowCountryCard(CountryData c)
        {
            if (_card == null) return;

            // Ensure card starts off-screen before making visible
            _card.style.translate = new Translate(320, 0);
            _card.style.opacity = 0f;
            _card.style.display = DisplayStyle.Flex;

            // Schedule the slide-in after a frame so the transition triggers
            _card.schedule.Execute(() =>
            {
                _card.style.translate = new Translate(0, 0);
                _card.style.opacity = 1f;
            }).StartingIn(10);

            // Flag
            if (_flagImage != null)
            {
                var flagTex = Resources.Load<Texture2D>($"Flags/{c.Iso2}");
                if (flagTex != null)
                    _flagImage.style.backgroundImage = new StyleBackground(flagTex);
            }

            // Text fields
            SetLabel(_nameLabel, c.Name);
            SetLabel(_govtLabel, c.GovernmentType);
            SetLabel(_regionLabel, $"{c.Region} — {c.Subregion}");

            // Stats: reset bars to 0, then animate to target (CSS transition handles the animation)
            ResetBar(_gdpBar);
            ResetBar(_popBar);
            ResetBar(_milBar);
            ResetBar(_stabilityBar);

            // Schedule bar fill after a brief delay so CSS transition triggers
            _card.schedule.Execute(() =>
            {
                SetLabel(_gdpValue, FormatGdp(c.GdpRawBillions));
                SetBarWidth(_gdpBar, c.GdpNormalized / 1000f);

                SetLabel(_popValue, FormatPopulation(c.Population));
                SetBarWidth(_popBar, c.PopulationNormalized / 1000f);

                SetLabel(_milValue, $"{c.MilitaryNormalized}");
                SetBarWidth(_milBar, c.MilitaryNormalized / 1000f);

                SetLabel(_stabilityValue, $"{c.Stability}%");
                SetBarWidth(_stabilityBar, c.Stability / 100f);
            }).StartingIn(50); // 50ms delay so the 0% state renders first

            // Tier badge
            if (_tierBadge != null)
            {
                _tierBadge.text = c.PowerTier;
                // Color by tier
                Color tierColor = c.PowerTier switch
                {
                    "S" => new Color(0.83f, 0.66f, 0.26f), // gold
                    "A" => new Color(0.29f, 0.62f, 0.92f), // blue
                    "B" => new Color(0.15f, 0.68f, 0.38f), // green
                    "C" => new Color(0.95f, 0.61f, 0.07f), // orange
                    _ => new Color(0.75f, 0.22f, 0.17f),   // red
                };
                _tierBadge.style.backgroundColor = tierColor;
            }

            // Difficulty
            if (_difficultyLabel != null)
            {
                string diff = c.PowerTier switch
                {
                    "S" => "Easy",
                    "A" => "Normal",
                    "B" => "Moderate",
                    "C" => "Hard",
                    _ => "Very Hard"
                };
                _difficultyLabel.text = diff;
                _difficultyLabel.style.color = c.PowerTier switch
                {
                    "S" => new Color(0.15f, 0.68f, 0.38f),
                    "A" => new Color(0.29f, 0.62f, 0.92f),
                    "B" => new Color(0.95f, 0.61f, 0.07f),
                    "C" => new Color(0.75f, 0.22f, 0.17f),
                    _ => new Color(0.75f, 0.22f, 0.17f),
                };
            }

            // Play button
            if (_playBtn != null)
                _playBtn.text = $"PLAY AS {c.Name.ToUpper()}";
        }

        private static void SetLabel(Label label, string text)
        {
            if (label != null) label.text = text;
        }

        private static void ResetBar(VisualElement bar)
        {
            if (bar == null) return;
            bar.style.width = new Length(0f, LengthUnit.Percent);
        }

        private static void SetBarWidth(VisualElement bar, float normalized)
        {
            if (bar == null) return;
            bar.style.width = new Length(Mathf.Clamp01(normalized) * 100f, LengthUnit.Percent);
        }

        private static string FormatGdp(float billions)
        {
            if (billions >= 1000f) return $"${billions / 1000f:F1}T";
            if (billions >= 1f) return $"${billions:F1}B";
            if (billions >= 0.001f) return $"${billions * 1000f:F0}M";
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
