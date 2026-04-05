// LabelRenderer.cs
// Pooled TextMeshPro labels with overlap rejection. Port of LabelLayer.gd.
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
        public const int POOL_SIZE = 60;

        [Header("Colors")]
        public Color TextColor = new(1f, 1f, 1f, 0.88f);
        public Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        public Color PlayerColor = new(0.95f, 0.95f, 0.98f, 1f);

        private MapCamera _mapCamera;
        private TMP_FontAsset _font;
        private TextMeshPro[] _labelPool;
        private TextMeshPro[] _shadowPool;
        private int _activeCount;

        // Dirty tracking — skip layout when camera hasn't moved
        private Vector3 _lastCamPos;
        private float _lastCamZoom;

        // Country label data (sorted by area descending)
        private List<LabelEntry> _entries = new();
        private List<Rect> _usedRects = new(64); // reused each frame, no GC

        private struct LabelEntry
        {
            public string Iso;
            public string Name;
            public Vector2 Position; // map-space centroid
            public float Area;       // for priority sorting
            public bool IsPlayer;
        }

        private void Start()
        {
            _mapCamera = FindFirstObjectByType<MapCamera>();

            // Load TMP font — try Resources first, then TMP default
            _font = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_font == null)
                _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
                Debug.LogError("[LabelRenderer] No TMP font found! Labels will be invisible. " +
                               "Import TMP Essentials via Window > TextMeshPro > Import TMP Essential Resources.");

            BuildPool();
        }

        private void BuildPool()
        {
            _labelPool = new TextMeshPro[POOL_SIZE];
            _shadowPool = new TextMeshPro[POOL_SIZE];

            for (int i = 0; i < POOL_SIZE; i++)
            {
                // Shadow (behind text)
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

                // Text (on top)
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
        }

        /// <summary>
        /// Set the country data for label rendering.
        /// Call once after data is loaded, and again on territory changes.
        /// </summary>
        public void SetCountryData(List<(string iso, string name, Vector2 centroid, float area, bool isPlayer)> countries)
        {
            _entries.Clear();
            foreach (var (iso, name, centroid, area, isPlayer) in countries)
            {
                _entries.Add(new LabelEntry
                {
                    Iso = iso,
                    Name = name,
                    Position = centroid,
                    Area = area,
                    IsPlayer = isPlayer
                });
            }

            // Sort by area descending — largest countries get priority
            _entries.Sort((a, b) => b.Area.CompareTo(a.Area));
        }

        private void LateUpdate()
        {
            if (_mapCamera == null || _entries.Count == 0) return;

            // Skip full layout if camera hasn't moved
            Camera cam = _mapCamera.Camera;
            Vector3 camPos = cam.transform.position;
            float zoom = _mapCamera.CurrentZoom;
            if (camPos == _lastCamPos && Mathf.Approximately(zoom, _lastCamZoom))
                return;
            _lastCamPos = camPos;
            _lastCamZoom = zoom;

            int poolIdx = 0;

            // Calculate zoom-responsive font size
            float baseSize = Mathf.Clamp(8f + 6f * Mathf.Log(zoom + 0.5f, 2f), 6f, 20f);

            // Get viewport bounds in world space for culling
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Rect viewRect = new(camPos.x - halfW - 200f, camPos.y - halfH - 200f,
                               halfW * 2f + 400f, halfH * 2f + 400f);

            // Overlap rejection (reuse list to avoid GC)
            _usedRects.Clear();

            // 3× tile rendering
            float[] xOffsets = { -MAP_WIDTH, 0f, MAP_WIDTH };

            foreach (var entry in _entries)
            {
                if (poolIdx >= POOL_SIZE) break;

                // Screen area threshold
                float screenArea = entry.Area * zoom * zoom;
                float minArea = 80f / Mathf.Max(zoom, 0.5f);
                if (screenArea < minArea) continue;

                // Font adjustments for small countries
                float fontSize = baseSize;
                string text = entry.Name;

                if (screenArea < 600f)
                {
                    fontSize -= 3f;
                    if (text.Length > 5) text = entry.Iso;
                }
                else if (screenArea < 2000f)
                {
                    fontSize -= 2f;
                    if (text.Length > 8) text = entry.Iso;
                }

                // Player gets bigger font
                if (entry.IsPlayer) fontSize += 3f;

                foreach (float xOff in xOffsets)
                {
                    if (poolIdx >= POOL_SIZE) break;

                    Vector3 worldPos = new(entry.Position.x + xOff, -entry.Position.y, 0);

                    // Frustum cull
                    if (!viewRect.Contains(new Vector2(worldPos.x, worldPos.y)))
                        continue;

                    // Estimate label rect for overlap check (account for 1/zoom scale)
                    float labelScale = 1f / zoom;
                    float estWidth = text.Length * fontSize * 0.5f * labelScale;
                    float estHeight = fontSize * 1.2f * labelScale;
                    Rect labelRect = new(worldPos.x - estWidth * 0.5f, worldPos.y - estHeight * 0.5f,
                                        estWidth, estHeight);

                    // Check overlap
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

                    // Assign from pool
                    var label = _labelPool[poolIdx];
                    var shadow = _shadowPool[poolIdx];

                    label.gameObject.SetActive(true);
                    shadow.gameObject.SetActive(true);

                    label.text = text;
                    label.fontSize = fontSize;
                    label.color = entry.IsPlayer ? PlayerColor : TextColor;
                    label.transform.position = worldPos;

                    float shadowOff = Mathf.Max(1f, fontSize * 0.08f);
                    shadow.text = text;
                    shadow.fontSize = fontSize;
                    shadow.transform.position = worldPos + new Vector3(shadowOff, -shadowOff, 0.01f);

                    // Scale labels inversely with zoom so they stay readable
                    label.transform.localScale = Vector3.one * labelScale;
                    shadow.transform.localScale = Vector3.one * labelScale;

                    poolIdx++;
                }
            }

            // Hide unused pool entries
            _activeCount = poolIdx;
            for (int i = poolIdx; i < POOL_SIZE; i++)
            {
                _labelPool[i].gameObject.SetActive(false);
                _shadowPool[i].gameObject.SetActive(false);
            }
        }
    }
}
