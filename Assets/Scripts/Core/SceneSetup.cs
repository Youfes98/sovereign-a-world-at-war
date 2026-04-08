// SceneSetup.cs
// Runtime scene builder — creates all Phase 2 GameObjects if they don't exist.
// After creation, wires data from services into renderers (colors, borders, labels).
// Province data and country data load asynchronously — wiring happens when BOTH are ready.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarStrategy.Map;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    [DefaultExecutionOrder(-999)] // After Bootstrap (-1000), before everything else
    public class SceneSetup : MonoBehaviour
    {
        void Awake()
        {
            SetupCamera();
            SetupMap();
        }

        void Start()
        {
            // Both province data and country data load async on background threads.
            // Subscribe to both events so we wire renderers only when BOTH are ready.
            var provinceDB = Services.ProvinceDB;
            var gameState = Services.GameState;

            if (provinceDB != null)
            {
                if (!provinceDB.IsLoaded)
                    provinceDB.OnDataLoaded += OnAnyDataLoaded;
            }

            if (gameState != null)
            {
                if (!gameState.IsLoaded)
                    gameState.OnCountryDataLoaded += OnAnyDataLoaded;
            }

            // If both happen to be loaded already (unlikely but safe), wire now
            TryWireData();

            // Wire click handler immediately (it only needs MapRenderer + MapCamera refs)
            StartCoroutine(WireClickHandler());
        }

        /// <summary>
        /// Callback for either data source finishing. Checks if both are ready.
        /// </summary>
        private void OnAnyDataLoaded()
        {
            TryWireData();
        }

        /// <summary>
        /// Wires renderers only when both GameState and ProvinceDB have finished loading.
        /// </summary>
        private bool _wired = false;
        private void TryWireData()
        {
            if (_wired) return;

            var provinceDB = Services.ProvinceDB;
            var gameState = Services.GameState;

            if (provinceDB == null || !provinceDB.IsLoaded) return;
            if (gameState == null || !gameState.IsLoaded) return;

            _wired = true;
            WireDataToRenderers();
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }

            // Add MapCamera if not present
            if (cam.GetComponent<MapCamera>() == null)
                cam.gameObject.AddComponent<MapCamera>();

            // Add ProvinceClickHandler if not present
            if (cam.GetComponent<ProvinceClickHandler>() == null)
                cam.gameObject.AddComponent<ProvinceClickHandler>();

            // Camera clear color = ocean blue
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.16f, 0.28f, 1f);
        }

        void SetupMap()
        {
            // MapRenderer (creates the 3 quads internally)
            if (FindAnyObjectByType<MapRenderer>() == null)
            {
                var mapGo = new GameObject("MapRoot");
                mapGo.AddComponent<MapRenderer>();
            }

            // BorderRenderer
            if (FindAnyObjectByType<BorderRenderer>() == null)
            {
                var borderGo = new GameObject("BorderRenderer");
                borderGo.AddComponent<BorderRenderer>();
            }

            // LabelRenderer
            if (FindAnyObjectByType<LabelRenderer>() == null)
            {
                var labelGo = new GameObject("LabelRenderer");
                labelGo.AddComponent<LabelRenderer>();
            }

            // CityDetailRenderer (building sprites at close zoom)
            // Disabled: medieval sprites don't fit modern aesthetic. Re-enable when modern markers are ready.
            // if (FindAnyObjectByType<CityDetailRenderer>() == null)
            // {
            //     var cityGo = new GameObject("CityDetailRenderer");
            //     cityGo.AddComponent<CityDetailRenderer>();
            // }
        }

        IEnumerator WireClickHandler()
        {
            // Wait for MapRenderer to finish Start()
            yield return null;
            yield return null;

            var clickHandler = FindAnyObjectByType<ProvinceClickHandler>();
            if (clickHandler == null) yield break;

            var mapRenderer = FindAnyObjectByType<MapRenderer>();
            var mapCamera = FindAnyObjectByType<MapCamera>();

            clickHandler.Initialize(mapRenderer, mapCamera);
        }

        /// <summary>
        /// Called when both province and country data have finished loading.
        /// Wires real country colors, borders, and labels into renderers.
        /// </summary>
        void WireDataToRenderers()
        {
            var gameState = Services.GameState;
            var provinceDB = Services.ProvinceDB;
            var mapRenderer = FindAnyObjectByType<MapRenderer>();
            var borderRenderer = FindAnyObjectByType<BorderRenderer>();
            var labelRenderer = FindAnyObjectByType<LabelRenderer>();

            // ── Feed real country colors into MapRenderer LUT ──
            if (mapRenderer != null && gameState != null && provinceDB != null)
            {
                int colored = 0;
                int skippedBounds = 0;
                int skippedNoCountry = 0;
                int maxIdx = 0;
                foreach (var kvp in provinceDB.Provinces)
                {
                    var prov = kvp.Value;
                    int r = Mathf.RoundToInt(prov.DetectColor.r * 255f);
                    int g = Mathf.RoundToInt(prov.DetectColor.g * 255f);
                    int b = Mathf.RoundToInt(prov.DetectColor.b * 255f);
                    int idx = r * 65536 + g * 256 + b;

                    if (idx > maxIdx) maxIdx = idx;

                    if (idx <= 0 || idx >= MapRenderer.LUT_SIZE)
                    {
                        skippedBounds++;
                        if (skippedBounds <= 5)
                            Debug.LogWarning($"[SceneSetup] Province {prov.Id} detect_color [{r},{g},{b}] → idx {idx} out of LUT range (0..{MapRenderer.LUT_SIZE - 1})");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(prov.ParentIso) && gameState.Countries.TryGetValue(prov.ParentIso, out var country))
                    {
                        mapRenderer.SetProvinceColor(idx, country.MapColor);
                        colored++;
                    }
                    else
                    {
                        skippedNoCountry++;
                    }
                }

#if UNITY_EDITOR
                Debug.Log($"[SceneSetup] Colored {colored} provinces. Max index={maxIdx}, LUT_SIZE={MapRenderer.LUT_SIZE}. " +
                          $"Skipped: {skippedBounds} out-of-bounds, {skippedNoCountry} no country match.");
#endif

                // Set initial territory ownership
                foreach (var kvp in provinceDB.Provinces)
                {
                    if (!string.IsNullOrEmpty(kvp.Value.ParentIso))
                        gameState.TerritoryOwnership[kvp.Key] = kvp.Value.ParentIso;
                }
            }

            // ── Populate Owner LUT for GPU border detection (instant, no background thread) ──
            if (borderRenderer != null && provinceDB != null && gameState != null)
            {
                borderRenderer.PopulateOwnerLUT(mapRenderer, provinceDB, gameState);
            }

            // ── Feed label data ──
            if (labelRenderer != null && gameState != null && provinceDB != null)
            {
                var labelData = new List<(string iso, string name, Vector2 centroid, float area, bool isPlayer)>();

                foreach (var kvp in gameState.Countries)
                {
                    var c = kvp.Value;
                    Vector2 centroid = c.Centroid;
                    if (centroid == Vector2.zero)
                        centroid = provinceDB.GetCentroid(c.Iso);

                    float totalArea = 0f;
                    if (provinceDB.CountryProvinces.TryGetValue(c.Iso, out var provIds))
                    {
                        foreach (var pid in provIds)
                        {
                            if (provinceDB.Provinces.TryGetValue(pid, out var prov))
                                totalArea += prov.AreaKm2;
                        }
                    }

                    if (centroid != Vector2.zero)
                    {
                        labelData.Add((c.Iso, c.Name, centroid, totalArea, c.Iso == gameState.PlayerIso));
                    }
                }

                labelRenderer.SetCountryData(labelData);

                // Feed city/capital label data
                labelRenderer.ClearCityData();
                int cityCount = 0;
                foreach (var kvp in gameState.Countries)
                {
                    var c = kvp.Value;
                    if (!string.IsNullOrEmpty(c.Capital) && c.CapitalCentroid != Vector2.zero)
                    {
                        labelRenderer.SetCityData(c.Iso, c.Capital, c.CapitalCentroid, c.Population);
                        cityCount++;
                    }
                }

                // Generate GPU city mask for shader-driven markers (replaces CityDetailRenderer)
                if (mapRenderer != null)
                    mapRenderer.GenerateCityMask(gameState.Countries);

#if UNITY_EDITOR
                Debug.Log($"[SceneSetup] Fed {labelData.Count} country labels, {cityCount} city labels.");
#endif
            }

#if UNITY_EDITOR
            Debug.Log("[SceneSetup] Data wiring complete — colors, borders, labels ready.");
#endif
        }

        void OnDestroy()
        {
            // Unsubscribe
            var provinceDB = Services.ProvinceDB;
            if (provinceDB != null)
                provinceDB.OnDataLoaded -= OnAnyDataLoaded;

            var gameState = Services.GameState;
            if (gameState != null)
                gameState.OnCountryDataLoaded -= OnAnyDataLoaded;
        }
    }
}
