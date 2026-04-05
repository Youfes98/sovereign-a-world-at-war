// BorderRenderer.cs — Manages Owner LUT for GPU-based border detection.
// Borders are rendered entirely in the MapShader fragment shader by comparing
// neighbor province owners. This component only maintains the owner lookup texture.
// Zero meshes, zero background threads, instant territory change updates.

using UnityEngine;
using WarStrategy.Core;

namespace WarStrategy.Map
{
    public class BorderRenderer : MonoBehaviour
    {
        // Country ISO → unique owner ID byte (1-254)
        private System.Collections.Generic.Dictionary<string, int> _countryToOwnerId = new();
        private int _nextOwnerId = 1; // 0 = ocean/unowned
        private MapRenderer _mapRenderer;

        /// <summary>Get the owner ID for a country ISO code.</summary>
        public int GetOwnerId(string iso) => _countryToOwnerId.TryGetValue(iso, out int v) ? v : 0;

        private void Start()
        {
            var gs = Services.GameState;
            if (gs != null)
            {
                gs.TerritoryChanged += OnTerritoryChanged;
                gs.PlayerCountrySet += OnPlayerCountrySet;
            }

            _mapRenderer = FindAnyObjectByType<MapRenderer>();
        }

        private void OnDestroy()
        {
            var gs = Services.GameState;
            if (gs != null)
            {
                gs.TerritoryChanged -= OnTerritoryChanged;
                gs.PlayerCountrySet -= OnPlayerCountrySet;
            }
        }

        private void OnPlayerCountrySet(string iso)
        {
            if (_countryToOwnerId.TryGetValue(iso, out int id))
            {
                if (_mapRenderer != null)
                    _mapRenderer.SetPlayerOwnerValue(id / 255f);
#if UNITY_EDITOR
                Debug.Log($"[BorderRenderer] Player country set to {iso}, owner ID={id}");
#endif
            }
        }

        /// <summary>
        /// Populate the Owner LUT for all provinces. Called once by SceneSetup after data loads.
        /// </summary>
        public void PopulateOwnerLUT(MapRenderer mapRenderer,
                                      ProvinceDatabase provinceDB,
                                      GameStateService gameState)
        {
            if (mapRenderer == null || provinceDB == null || gameState == null) return;

            // Assign unique integer owner IDs to each country (1-254, stored as exact bytes)
            _countryToOwnerId.Clear();
            _nextOwnerId = 1;

            foreach (var kvp in gameState.Countries)
            {
                _countryToOwnerId[kvp.Key] = _nextOwnerId;
                _nextOwnerId++;
                if (_nextOwnerId > 254)
                {
                    Debug.LogWarning("[BorderRenderer] More than 254 countries — some may share owner IDs");
                    break;
                }
            }

            // Set owner for each province
            int set = 0;
            int skippedIdx = 0;
            int skippedNoOwner = 0;
            foreach (var kvp in provinceDB.Provinces)
            {
                var prov = kvp.Value;
                int r = Mathf.RoundToInt(prov.DetectColor.r * 255f);
                int g = Mathf.RoundToInt(prov.DetectColor.g * 255f);
                int b = Mathf.RoundToInt(prov.DetectColor.b * 255f);
                int idx = r * 65536 + g * 256 + b;

                if (idx <= 0 || idx >= MapRenderer.LUT_SIZE) { skippedIdx++; continue; }

                string owner;
                if (!gameState.TerritoryOwnership.TryGetValue(kvp.Key, out owner))
                    owner = prov.ParentIso;
                if (!string.IsNullOrEmpty(owner) && _countryToOwnerId.TryGetValue(owner, out int ownerId))
                {
                    mapRenderer.SetProvinceOwner(idx, ownerId);
                    set++;
                }
                else
                {
                    skippedNoOwner++;
                }
            }

            Debug.Log($"[BorderRenderer] Owner LUT: {set} set, {skippedIdx} skipped(idx), {skippedNoOwner} skipped(no owner), {_countryToOwnerId.Count} countries, nextId={_nextOwnerId}");

#if UNITY_EDITOR
            // Debug: check US provinces specifically
            int usaId = _countryToOwnerId.ContainsKey("USA") ? _countryToOwnerId["USA"] : -1;
            Debug.Log($"[BorderRenderer] USA owner ID = {usaId}");
            int usCount = 0;
            foreach (var kvp in provinceDB.Provinces)
            {
                if (kvp.Value.ParentIso == "USA")
                {
                    int dr = Mathf.RoundToInt(kvp.Value.DetectColor.r * 255f);
                    int dg = Mathf.RoundToInt(kvp.Value.DetectColor.g * 255f);
                    int db = Mathf.RoundToInt(kvp.Value.DetectColor.b * 255f);
                    int di = dr * 65536 + dg * 256 + db;
                    if (usCount < 5)
                        Debug.Log($"[BorderRenderer] US province {kvp.Key}: detectColor=({dr},{dg},{db}) idx={di} inRange={di > 0 && di < MapRenderer.LUT_SIZE}");
                    usCount++;
                }
            }
            Debug.Log($"[BorderRenderer] Total US provinces: {usCount}");
#endif
        }

        /// <summary>
        /// Called when a province changes owner. Updates single LUT pixel — instant border update.
        /// </summary>
        private void OnTerritoryChanged(string provinceId, string oldOwner, string newOwner)
        {
            var provinceDB = Services.ProvinceDB;
            if (_mapRenderer == null || provinceDB == null) return;

            if (!provinceDB.Provinces.TryGetValue(provinceId, out var prov)) return;

            int r = Mathf.RoundToInt(prov.DetectColor.r * 255f);
            int g = Mathf.RoundToInt(prov.DetectColor.g * 255f);
            int b = Mathf.RoundToInt(prov.DetectColor.b * 255f);
            int idx = r * 65536 + g * 256 + b;

            if (idx <= 0 || idx >= MapRenderer.LUT_SIZE) return;

            // Assign new owner ID (create if first time seeing this country)
            if (!_countryToOwnerId.TryGetValue(newOwner, out int ownerId))
            {
                if (_nextOwnerId > 254)
                {
                    Debug.LogWarning($"[BorderRenderer] Owner ID overflow for {newOwner}");
                    return;
                }
                ownerId = _nextOwnerId;
                _countryToOwnerId[newOwner] = ownerId;
                _nextOwnerId++;
            }

            _mapRenderer.SetProvinceOwner(idx, ownerId);

            // Also update the color LUT to show new owner's color
            var gameState = Services.GameState;
            if (gameState != null && gameState.Countries.TryGetValue(newOwner, out var country))
                _mapRenderer.SetProvinceColor(idx, country.MapColor);
        }
    }
}
