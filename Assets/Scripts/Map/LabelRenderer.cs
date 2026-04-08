// LabelRenderer.cs
// Pooled TextMeshPro labels with overlap rejection and territory-spanning sizing.
// Labels stretch to fill country bounding box width (HOI4/EU4 style).
// Pre-allocates ~60 TMP objects. Each frame: sort, assign, reject overlaps, hide unused.
// NO create/destroy per frame.

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using WarStrategy.Core;

namespace WarStrategy.Map
{
    public class LabelRenderer : MonoBehaviour
    {
        public const float MAP_WIDTH = 16384f;
        public const float MAP_HEIGHT = 8192f;
        public const int POOL_SIZE = 60;
        public const int CITY_POOL_SIZE = 80;
        public const float CITY_ZOOM_THRESHOLD = 3f;

        [Header("Colors")]
        public Color TextColor = new(0.06f, 0.05f, 0.03f, 0.82f);     // bold dark, clearly readable
        public Color ShadowColor = new(1f, 1f, 1f, 0.18f);             // white underlay for legibility
        public Color PlayerColor = new(0.04f, 0.02f, 0.0f, 0.90f);     // bolder for player
        public Color CityTextColor = new(0.10f, 0.08f, 0.06f, 0.80f);  // dark, readable

        private MapCamera _mapCamera;
        private TMP_FontAsset _font;
        private TextMeshPro[] _labelPool;
        private TextMeshPro[] _shadowPool;
        private TextMeshPro[] _cityLabelPool;
        private TextMeshPro[] _cityShadowPool;
        private int _activeCount;
        private int _activeCityCount;

        // Dirty tracking — skip layout when camera hasn't moved
        private Vector3 _lastCamPos;
        private float _lastCamZoom;

        // Country label data (sorted by area descending)
        private List<LabelEntry> _entries = new();
        private List<Rect> _usedRects = new(64); // reused each frame, no GC

        // City label data (sorted by population descending)
        private List<CityEntry> _cityEntries = new();

        private struct LabelEntry
        {
            public string Iso;
            public string Name;
            public Vector2 Position;   // map-space centroid
            public float Area;         // for priority sorting
            public bool IsPlayer;
            public float BoundsWidth;  // world-unit width of country territory
        }

        private struct CityEntry
        {
            public string CountryIso;
            public string DisplayText;
            public Vector2 Position;
            public long Population;
        }

        private void Start()
        {
            _mapCamera = FindFirstObjectByType<MapCamera>();

            _font = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_font == null)
                _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
                Debug.LogError("[LabelRenderer] No TMP font found!");

            BuildPool();
        }

        private void BuildPool()
        {
            _labelPool = new TextMeshPro[POOL_SIZE];
            _shadowPool = new TextMeshPro[POOL_SIZE];

            for (int i = 0; i < POOL_SIZE; i++)
            {
                var shadowGO = new GameObject($"LabelShadow_{i}");
                shadowGO.transform.SetParent(transform);
                var shadow = shadowGO.AddComponent<TextMeshPro>();
                shadow.alignment = TextAlignmentOptions.Center;
                shadow.color = ShadowColor;
                shadow.sortingOrder = 9;
                shadow.textWrappingMode = TextWrappingModes.NoWrap;
                if (_font != null) shadow.font = _font;
                shadowGO.SetActive(false);
                _shadowPool[i] = shadow;

                var textGO = new GameObject($"Label_{i}");
                textGO.transform.SetParent(transform);
                var label = textGO.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.color = TextColor;
                label.sortingOrder = 10;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                if (_font != null) label.font = _font;
                textGO.SetActive(false);
                _labelPool[i] = label;
            }

            _cityLabelPool = new TextMeshPro[CITY_POOL_SIZE];
            _cityShadowPool = new TextMeshPro[CITY_POOL_SIZE];

            for (int i = 0; i < CITY_POOL_SIZE; i++)
            {
                var shadowGO = new GameObject($"CityShadow_{i}");
                shadowGO.transform.SetParent(transform);
                var shadow = shadowGO.AddComponent<TextMeshPro>();
                shadow.alignment = TextAlignmentOptions.Center;
                shadow.color = ShadowColor;
                shadow.sortingOrder = 11;
                shadow.textWrappingMode = TextWrappingModes.NoWrap;
                if (_font != null) shadow.font = _font;
                shadowGO.SetActive(false);
                _cityShadowPool[i] = shadow;

                var textGO = new GameObject($"CityLabel_{i}");
                textGO.transform.SetParent(transform);
                var label = textGO.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.color = CityTextColor;
                label.sortingOrder = 12;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                if (_font != null) label.font = _font;
                textGO.SetActive(false);
                _cityLabelPool[i] = label;
            }
        }

        /// <summary>
        /// Set the country data for label rendering.
        /// boundsWidth = world-unit width of country bounding box from ProvinceDatabase.GetCountryBounds().
        /// </summary>
        public void SetCountryData(List<(string iso, string name, Vector2 centroid, float area, bool isPlayer, float boundsWidth)> countries)
        {
            _entries.Clear();
            foreach (var (iso, name, centroid, area, isPlayer, boundsWidth) in countries)
            {
                _entries.Add(new LabelEntry
                {
                    Iso = iso,
                    Name = name,
                    Position = centroid,
                    Area = area,
                    IsPlayer = isPlayer,
                    BoundsWidth = boundsWidth
                });
            }
            _entries.Sort((a, b) => b.Area.CompareTo(a.Area));
        }

        public void SetCityData(string countryIso, string cityName, Vector2 centroid, long population)
        {
            if (string.IsNullOrEmpty(cityName) || centroid == Vector2.zero) return;

            _cityEntries.Add(new CityEntry
            {
                CountryIso = countryIso,
                DisplayText = $"{cityName}, {FormatPopulation(population)}",
                Position = centroid,
                Population = population
            });
            _cityEntries.Sort((a, b) => b.Population.CompareTo(a.Population));
        }

        public void ClearCityData()
        {
            _cityEntries.Clear();
        }

        /// <summary>
        /// Curve text along a gentle arc by modifying TMP vertex positions.
        /// curveAmount = max Y displacement at the center of the text.
        /// Positive = curve upward (smile shape). Call after ForceMeshUpdate().
        /// </summary>
        private static void CurveText(TextMeshPro tmp, float curveAmount)
        {
            if (curveAmount == 0f) return;

            var textInfo = tmp.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) return;

            // Find the total width of visible text for normalization
            int firstVisible = -1, lastVisible = -1;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;
                if (firstVisible < 0) firstVisible = i;
                lastVisible = i;
            }
            if (firstVisible < 0) return;

            float leftX = textInfo.characterInfo[firstVisible].bottomLeft.x;
            float rightX = textInfo.characterInfo[lastVisible].topRight.x;
            float textWidth = rightX - leftX;
            if (textWidth < 0.01f) return;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int matIdx = textInfo.characterInfo[i].materialReferenceIndex;
                int vertIdx = textInfo.characterInfo[i].vertexIndex;

                var verts = textInfo.meshInfo[matIdx].vertices;

                // Character center X, normalized to 0..1 across text width
                float charCenterX = (verts[vertIdx].x + verts[vertIdx + 2].x) * 0.5f;
                float t = (charCenterX - leftX) / textWidth; // 0 at left, 1 at right

                // Parabolic arc: max at center (t=0.5), zero at edges
                float yOffset = curveAmount * (1f - 4f * (t - 0.5f) * (t - 0.5f));

                for (int j = 0; j < 4; j++)
                    verts[vertIdx + j].y += yOffset;
            }

            // Push modified vertices back to TMP mesh
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                tmp.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }

        private static string FormatPopulation(long pop)
        {
            if (pop >= 1_000_000)
            {
                float millions = pop / 1_000_000f;
                return millions >= 10f ? $"{millions:F0}M" : $"{millions:F1}M";
            }
            if (pop >= 1_000)
            {
                float thousands = pop / 1_000f;
                return thousands >= 10f ? $"{thousands:F0}k" : $"{thousands:F1}k";
            }
            return pop.ToString();
        }

        private void LateUpdate()
        {
            if (_mapCamera == null || _entries.Count == 0) return;

            Camera cam = _mapCamera.Camera;
            Vector3 camPos = cam.transform.position;
            float zoom = _mapCamera.CurrentZoom;
            if (camPos == _lastCamPos && Mathf.Approximately(zoom, _lastCamZoom))
                return;
            _lastCamPos = camPos;
            _lastCamZoom = zoom;

            float orthoSize = cam.orthographicSize;
            float viewportHeight = orthoSize * 2f;
            float halfH = orthoSize;
            float halfW = halfH * cam.aspect;

            int poolIdx = 0;

            // Viewport bounds for frustum culling (generous margin)
            Rect viewRect = new(camPos.x - halfW - 500f, camPos.y - halfH - 500f,
                               halfW * 2f + 1000f, halfH * 2f + 1000f);

            _usedRects.Clear();

            float[] xOffsets = { -MAP_WIDTH, 0f, MAP_WIDTH };

            // Screen-size clamps: labels stay between 4% and 14% of viewport height
            float minFontSize = viewportHeight * 0.04f;
            float maxFontSize = viewportHeight * 0.14f;

            foreach (var entry in _entries)
            {
                if (poolIdx >= POOL_SIZE) break;

                // Area threshold — skip tiny countries that would be unreadable
                float screenArea = entry.Area * zoom * zoom;
                float minArea = 40f / Mathf.Max(zoom, 0.5f);
                if (screenArea < minArea) continue;

                string text = entry.Name.ToUpper();

                // Territory-spanning fontSize:
                // Text should fill ~70% of country width
                // Cap BoundsWidth to prevent absurd sizes (Russia wraps the globe)
                float bw = Mathf.Min(entry.BoundsWidth, MAP_WIDTH * 0.35f);
                float targetWidth = bw * 0.85f;
                float charFactor = Mathf.Max(text.Length * 0.65f, 1f);
                float fontSizeFromTerritory = targetWidth / charFactor;

                // Clamp to screen-readable range
                float fontSize = Mathf.Clamp(fontSizeFromTerritory, minFontSize, maxFontSize);

                // Small country override: use ISO code if text won't fit
                if (fontSizeFromTerritory < minFontSize * 0.5f && text.Length > 3)
                    text = entry.Iso;

                // Player gets slight boost
                if (entry.IsPlayer) fontSize *= 1.15f;

                foreach (float xOff in xOffsets)
                {
                    if (poolIdx >= POOL_SIZE) break;

                    Vector3 worldPos = new(entry.Position.x + xOff, -entry.Position.y, -2f);

                    if (!viewRect.Contains(new Vector2(worldPos.x, worldPos.y)))
                        continue;

                    // Overlap check — localScale is 1, so world units = fontSize directly
                    float estWidth = text.Length * fontSize * 0.65f;
                    float estHeight = fontSize * 1.4f;
                    Rect labelRect = new(worldPos.x - estWidth * 0.5f, worldPos.y - estHeight * 0.5f,
                                        estWidth, estHeight);

                    bool blocked = false;
                    foreach (var used in _usedRects)
                    {
                        if (labelRect.Overlaps(used))
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (blocked) continue;

                    _usedRects.Add(labelRect);

                    var label = _labelPool[poolIdx];
                    var shadow = _shadowPool[poolIdx];

                    label.gameObject.SetActive(true);
                    shadow.gameObject.SetActive(true);

                    label.text = text;
                    label.fontSize = fontSize;
                    label.characterSpacing = 18f; // very wide — spans territory
                    label.fontStyle = FontStyles.Bold;
                    label.color = entry.IsPlayer ? PlayerColor : TextColor;
                    label.outlineWidth = 0f; // no outline — text is embedded in map
                    label.transform.position = worldPos;
                    label.transform.localScale = Vector3.one;

                    // Force mesh update then curve the text
                    label.ForceMeshUpdate();
                    CurveText(label, fontSize * 0.15f); // gentle arc

                    // Faint white underlay instead of dark shadow
                    shadow.text = text;
                    shadow.fontSize = fontSize * 1.02f; // slightly larger for halo effect
                    shadow.characterSpacing = 18f;
                    shadow.fontStyle = FontStyles.Bold;
                    shadow.outlineWidth = 0.2f;
                    shadow.outlineColor = new Color32(255, 255, 255, 30);
                    shadow.transform.position = worldPos + new Vector3(0, 0, 0.1f); // same position, behind
                    shadow.transform.localScale = Vector3.one;

                    shadow.ForceMeshUpdate();
                    CurveText(shadow, fontSize * 0.15f);

                    poolIdx++;
                }
            }

            _activeCount = poolIdx;
            for (int i = poolIdx; i < POOL_SIZE; i++)
            {
                _labelPool[i].gameObject.SetActive(false);
                _shadowPool[i].gameObject.SetActive(false);
            }

            // ── City / capital labels ──
            int cityPoolIdx = 0;

            if (zoom >= CITY_ZOOM_THRESHOLD && _cityEntries.Count > 0)
            {
                // City labels: 1.2% of viewport height, no territory spanning
                float cityFontSize = viewportHeight * 0.012f;

                foreach (var city in _cityEntries)
                {
                    if (cityPoolIdx >= CITY_POOL_SIZE) break;

                    foreach (float xOff in xOffsets)
                    {
                        if (cityPoolIdx >= CITY_POOL_SIZE) break;

                        Vector3 worldPos = new(city.Position.x + xOff, -city.Position.y, -2f);

                        if (!viewRect.Contains(new Vector2(worldPos.x, worldPos.y)))
                            continue;

                        float estWidth = city.DisplayText.Length * cityFontSize * 0.5f;
                        float estHeight = cityFontSize * 1.2f;
                        Rect labelRect = new(worldPos.x - estWidth * 0.5f, worldPos.y - estHeight * 0.5f,
                                            estWidth, estHeight);

                        bool blocked = false;
                        foreach (var used in _usedRects)
                        {
                            if (labelRect.Overlaps(used))
                            {
                                blocked = true;
                                break;
                            }
                        }
                        if (blocked) continue;

                        _usedRects.Add(labelRect);

                        var label = _cityLabelPool[cityPoolIdx];
                        var shadow = _cityShadowPool[cityPoolIdx];

                        label.gameObject.SetActive(true);
                        shadow.gameObject.SetActive(true);

                        label.text = city.DisplayText;
                        label.fontSize = cityFontSize;
                        label.color = CityTextColor;
                        label.transform.position = worldPos;
                        label.transform.localScale = Vector3.one;

                        float shadowOff = Mathf.Max(cityFontSize * 0.04f, viewportHeight * 0.0005f);
                        shadow.text = city.DisplayText;
                        shadow.fontSize = cityFontSize;
                        shadow.transform.position = worldPos + new Vector3(shadowOff, -shadowOff, 0.1f);
                        shadow.transform.localScale = Vector3.one;

                        cityPoolIdx++;
                    }
                }
            }

            _activeCityCount = cityPoolIdx;
            for (int i = cityPoolIdx; i < CITY_POOL_SIZE; i++)
            {
                _cityLabelPool[i].gameObject.SetActive(false);
                _cityShadowPool[i].gameObject.SetActive(false);
            }
        }
    }
}
