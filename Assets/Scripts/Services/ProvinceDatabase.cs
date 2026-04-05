// ProvinceDatabase.cs
// Port of ProvinceDB.gd — O(1) province click detection via pixel bitmap.
// JSON files loaded from StreamingAssets via System.IO on background thread.
// Zero main-thread string allocations for the 57MB provinces.json.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    public class ProvinceDatabase : MonoBehaviour
    {
        public Dictionary<string, ProvinceData> Provinces { get; private set; } = new();
        public Dictionary<string, List<string>> CountryAdjacencies { get; private set; } = new();
        public Dictionary<string, List<string>> ProvinceAdjacencies { get; private set; } = new();
        public Dictionary<string, List<string>> SeaAdjacencies { get; private set; } = new();
        public Dictionary<string, List<string>> CountryProvinces { get; private set; } = new();

        private Texture2D _provinceBitmap;
        private Color32[] _cachedPixels;
        private int _bmpWidth, _bmpHeight;
        private Dictionary<Color32, string> _colorToProvince = new();

        public bool IsLoaded { get; private set; }
        public event System.Action OnDataLoaded;

        public void LoadProvinceData()
        {
            StartCoroutine(LoadAllAsync());
        }

        private IEnumerator LoadAllAsync()
        {
            // Load province bitmap async (GPU texture)
            var bitmapRequest = Resources.LoadAsync<Texture2D>("Map/provinces");
            yield return bitmapRequest;
            _provinceBitmap = bitmapRequest.asset as Texture2D;
            if (_provinceBitmap != null)
            {
                _cachedPixels = _provinceBitmap.GetPixels32();
                _bmpWidth = _provinceBitmap.width;
                _bmpHeight = _provinceBitmap.height;
                Debug.Log($"[ProvinceDB] Province bitmap: {_bmpWidth}x{_bmpHeight}");
            }

            // Read ALL JSON files from disk on a background thread
            // StreamingAssets = direct file access, no Unity API, no main-thread allocation
            string dataPath = Path.Combine(Application.streamingAssetsPath, "Data");

            List<ProvinceData> provinceList = null;
            Dictionary<string, List<string>> countryAdj = null;
            Dictionary<string, List<string>> seaAdj = null;
            Dictionary<string, List<string>> provAdj = null;
            long parseMs = 0;

            var task = Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Read + parse provinces (57MB) entirely off main thread
                string provPath = Path.Combine(dataPath, "provinces.json");
                if (File.Exists(provPath))
                {
                    string provJson = File.ReadAllText(provPath);
                    provinceList = JsonParser.ParseProvinces(provJson);
                }

                // Read + parse adjacencies (small files)
                string adjPath = Path.Combine(dataPath, "adjacencies.json");
                if (File.Exists(adjPath))
                    countryAdj = JsonParser.ParseAdjacencies(File.ReadAllText(adjPath));

                string seaPath = Path.Combine(dataPath, "sea_adjacencies.json");
                if (File.Exists(seaPath))
                    seaAdj = JsonParser.ParseAdjacencies(File.ReadAllText(seaPath));

                string provAdjPath = Path.Combine(dataPath, "province_adjacencies.json");
                if (File.Exists(provAdjPath))
                    provAdj = JsonParser.ParseAdjacencies(File.ReadAllText(provAdjPath));

                sw.Stop();
                parseMs = sw.ElapsedMilliseconds;
            });

            Debug.Log("[ProvinceDB] Reading + parsing province data on background thread...");

            // Yield every frame — Unity stays completely responsive
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                Debug.LogError($"[ProvinceDB] Parse failed: {task.Exception?.InnerException?.Message}");
                yield break;
            }

            // Apply results on main thread (fast — just dictionary inserts)
            if (provinceList != null)
            {
                Provinces.Clear();
                CountryProvinces.Clear();
                _colorToProvince.Clear();

                foreach (var p in provinceList)
                {
                    Provinces[p.Id] = p;
                    Color32 c32 = p.DetectColor;
                    _colorToProvince[c32] = p.Id;

                    if (!string.IsNullOrEmpty(p.ParentIso))
                    {
                        if (!CountryProvinces.ContainsKey(p.ParentIso))
                            CountryProvinces[p.ParentIso] = new List<string>();
                        CountryProvinces[p.ParentIso].Add(p.Id);
                    }
                }
            }

            if (countryAdj != null) CountryAdjacencies = countryAdj;
            if (seaAdj != null) SeaAdjacencies = seaAdj;
            if (provAdj != null) ProvinceAdjacencies = provAdj;

            IsLoaded = true;

            Debug.Log($"[ProvinceDB] Done! {Provinces.Count} provinces in {parseMs}ms. " +
                      $"Colors: {_colorToProvince.Count}, Countries: {CountryProvinces.Count}");

            OnDataLoaded?.Invoke();
        }

        // ── Click detection ──

        public string GetProvinceAtUV(Vector2 uv)
        {
            if (_cachedPixels == null) return null;
            int x = Mathf.Clamp((int)(uv.x * _bmpWidth), 0, _bmpWidth - 1);
            int y = Mathf.Clamp((int)(uv.y * _bmpHeight), 0, _bmpHeight - 1);
            Color32 pixel = _cachedPixels[y * _bmpWidth + x];
            return _colorToProvince.GetValueOrDefault(pixel);
        }

        public string GetCountryForProvince(string provinceId)
        {
            if (Provinces.TryGetValue(provinceId, out var prov))
                return prov.ParentIso;
            return null;
        }

        public Vector2 GetCentroid(string iso)
        {
            if (!CountryProvinces.TryGetValue(iso, out var provinces) || provinces.Count == 0)
                return Vector2.zero;
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (var pid in provinces)
            {
                if (Provinces.TryGetValue(pid, out var prov))
                {
                    sum += prov.Centroid;
                    count++;
                }
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        public List<string> GetNeighborsForDomain(string provinceId, string domain)
        {
            return domain switch
            {
                "sea" => SeaAdjacencies.GetValueOrDefault(provinceId, new List<string>()),
                _ => ProvinceAdjacencies.GetValueOrDefault(provinceId, new List<string>())
            };
        }

        /// <summary>
        /// Calculate the bounding box of a country from all its province polygon vertices.
        /// Returns (center, size) in map coordinates.
        /// </summary>
        public (Vector2 center, Vector2 size) GetCountryBounds(string iso)
        {
            if (!CountryProvinces.TryGetValue(iso, out var provinceIds))
                return (GetCentroid(iso), new Vector2(500, 500)); // fallback

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var pid in provinceIds)
            {
                if (!Provinces.TryGetValue(pid, out var prov)) continue;
                if (prov.Polygon == null) continue;

                foreach (var v in prov.Polygon)
                {
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.y < minY) minY = v.y;
                    if (v.y > maxY) maxY = v.y;
                }
            }

            if (minX == float.MaxValue) // no polygon data
                return (GetCentroid(iso), new Vector2(500, 500));

            Vector2 center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            Vector2 size = new Vector2(maxX - minX, maxY - minY);
            return (center, size);
        }
    }
}
