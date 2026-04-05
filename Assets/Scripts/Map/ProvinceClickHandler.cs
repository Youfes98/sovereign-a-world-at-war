// ProvinceClickHandler.cs
// O(1) province click detection via CPU-side pixel sampling of provinces.png.
// Sets hover/selection on MapRenderer material (GPU uniforms).
// Also queries ProvinceDatabase for province/country ID on click.

using UnityEngine;
using WarStrategy.Core;

namespace WarStrategy.Map
{
    public class ProvinceClickHandler : MonoBehaviour
    {
        [SerializeField] private MapRenderer _mapRenderer;
        [SerializeField] private MapCamera _mapCamera;

        private Texture2D _provinceTex;
        private Color32[] _cachedPixels;
        private int _texWidth, _texHeight;
        private int _lastHoverIndex = -1;

        private ProvinceDatabase _provinceDB;
        private GameStateService _gameState;

        /// <summary>
        /// When false, all hover/click processing is skipped.
        /// Set by GameFlowController during non-gameplay phases.
        /// </summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>
        /// Runtime initialization — called by SceneSetup after components are created.
        /// Replaces fragile reflection-based field wiring.
        /// </summary>
        public void Initialize(MapRenderer renderer, MapCamera camera)
        {
            _mapRenderer = renderer;
            _mapCamera = camera;
        }

        private void Start()
        {
            if (_mapRenderer != null)
                _provinceTex = _mapRenderer.ProvinceTexture;
        }

        private void Update()
        {
            if (!InputEnabled) return;

            // Late-bind province texture (SceneSetup wires _mapRenderer after Start)
            if (_provinceTex == null && _mapRenderer != null)
            {
                _provinceTex = _mapRenderer.ProvinceTexture;
                if (_provinceTex != null)
                {
                    _cachedPixels = _provinceTex.GetPixels32();
                    _texWidth = _provinceTex.width;
                    _texHeight = _provinceTex.height;
                    _provinceDB = Services.ProvinceDB;
                    _gameState = Services.GameState;
                }
            }
            if (_provinceTex == null || _mapCamera == null) return;

            // Hover — sample province under mouse every frame
            Vector2 uv = _mapCamera.ScreenToMapUV(Input.mousePosition);
            int hoverIdx = SampleProvinceIndex(uv);

            if (hoverIdx != _lastHoverIndex)
            {
                _lastHoverIndex = hoverIdx;
                _mapRenderer.SetHoverIndex(hoverIdx);
            }

            // Click — left mouse button (skip if UI consumed the event)
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
#if UNITY_EDITOR
                // Debug: show world position and UV
                Vector3 worldPos = _mapCamera.Camera.ScreenToWorldPoint(Input.mousePosition);
                Debug.Log($"[Click Debug] WorldPos: ({worldPos.x:F0}, {worldPos.y:F0}) | UV: ({uv.x:F4}, {uv.y:F4}) | PixelIdx: {hoverIdx}");
#endif

                if (hoverIdx > 0)
                {
                    _mapRenderer.SetSelectedIndex(hoverIdx);

                    if (_provinceDB != null)
                    {
                        string provinceId = _provinceDB.GetProvinceAtUV(uv);
                        if (!string.IsNullOrEmpty(provinceId))
                        {
                            string countryIso = _provinceDB.GetCountryForProvince(provinceId);
                            string countryName = "";
                            if (!string.IsNullOrEmpty(countryIso) && _gameState != null)
                            {
                                if (_gameState.Countries.TryGetValue(countryIso, out var country))
                                    countryName = country.Name;

                                _gameState.SelectCountry(countryIso);
                            }

#if UNITY_EDITOR
                            Debug.Log($"[Click] Province: {provinceId} | Country: {countryIso} ({countryName}) | Index: {hoverIdx} | PlayerIso: '{_gameState?.PlayerIso}'");
#endif
                        }
                        else
                        {
#if UNITY_EDITOR
                            Debug.Log($"[Click] Index {hoverIdx} — not in province DB (UV: {uv})");
#endif
                        }
                    }
                }
                else
                {
                    _mapRenderer.SetSelectedIndex(-1);
                    _gameState?.Deselect();
#if UNITY_EDITOR
                    Debug.Log("[Click] Ocean (no province)");
#endif
                }
            }
        }

        /// <summary>
        /// Sample provinces.png at UV coordinate, return province index.
        /// O(1) — same approach as Godot's ProvinceDB.
        /// provinces.png MUST be imported with: Read/Write=true, Compression=None, FilterMode=Point
        /// </summary>
        private int SampleProvinceIndex(Vector2 uv)
        {
            if (_cachedPixels == null) return 0;
            int x = Mathf.Clamp((int)(uv.x * _texWidth), 0, _texWidth - 1);
            int y = Mathf.Clamp((int)(uv.y * _texHeight), 0, _texHeight - 1);
            Color32 pixel = _cachedPixels[y * _texWidth + x];
            return pixel.r * 65536 + pixel.g * 256 + pixel.b;
        }

        /// <summary>
        /// Check if the mouse is over any UI Toolkit element that consumes input.
        /// Prevents map clicks from firing when clicking UI buttons/panels.
        /// </summary>
        private bool IsPointerOverUI()
        {
            var docs = FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                if (doc.rootVisualElement == null) continue;
                var root = doc.rootVisualElement;
                var panel = root.panel;
                if (panel == null) continue;

                // Convert screen position to panel coordinates
                Vector2 screenPos = Input.mousePosition;
                Vector2 panelPos = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(
                    panel, new Vector2(screenPos.x, Screen.height - screenPos.y));

                // Pick the topmost element at that position
                var picked = panel.Pick(panelPos);

                // If we hit a real element (not null, not PickingMode.Ignore), UI is consuming input
                if (picked != null && picked.pickingMode == UnityEngine.UIElements.PickingMode.Position)
                    return true;
            }
            return false;
        }
    }
}
