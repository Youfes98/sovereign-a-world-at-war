// CityDetailRenderer.cs
// Spawns building sprites at city/capital locations when zoomed in.
// Uses object pooling for performance — no runtime Instantiate/Destroy.

using System.Collections.Generic;
using UnityEngine;
using WarStrategy.Core;
using WarStrategy.Data;

namespace WarStrategy.Map
{
    public class CityDetailRenderer : MonoBehaviour
    {
        private const int POOL_SIZE = 60;
        private const float ZOOM_THRESHOLD = 5f;     // buildings appear at zoom > 5
        private const float ZOOM_FULL = 10f;          // fully visible at zoom > 10
        private const float MAP_WIDTH = 16384f;
        private const float MAP_HEIGHT = 8192f;

        private GameObject[] _pool;
        private SpriteRenderer[] _renderers;
        private int _activeCount;
        private MapCamera _mapCamera;
        private bool _cameraFound;

        // City data
        private struct CityInfo
        {
            public Vector2 WorldPos;   // map space position
            public long Population;
            public string Name;
        }
        private List<CityInfo> _cities = new List<CityInfo>();

        // Sprites by tier
        private Sprite _sprLarge;   // pop > 10M
        private Sprite _sprMedium;  // pop > 1M
        private Sprite _sprSmall;   // pop < 1M

        private void Start()
        {
            LoadSprites();
            BuildPool();
        }

        private void LoadSprites()
        {
            _sprLarge = LoadSprite("Map/Cities/city_large");
            _sprMedium = LoadSprite("Map/Cities/city_govt");
            _sprSmall = LoadSprite("Map/Cities/city_small");

            // Fallback: if any sprite fails, use whatever loaded
            if (_sprLarge == null) _sprLarge = _sprMedium ?? _sprSmall;
            if (_sprMedium == null) _sprMedium = _sprLarge ?? _sprSmall;
            if (_sprSmall == null) _sprSmall = _sprMedium ?? _sprLarge;
        }

        private Sprite LoadSprite(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private void BuildPool()
        {
            _pool = new GameObject[POOL_SIZE];
            _renderers = new SpriteRenderer[POOL_SIZE];

            for (int i = 0; i < POOL_SIZE; i++)
            {
                var go = new GameObject($"CitySprite_{i}");
                go.transform.SetParent(transform);
                go.SetActive(false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 5; // above map, below labels
                sr.sprite = _sprSmall;

                _pool[i] = go;
                _renderers[i] = sr;
            }
        }

        /// <summary>
        /// Feed city data from country data. Called by SceneSetup after data loads.
        /// </summary>
        public void SetCities(Dictionary<string, CountryData> countries)
        {
            _cities.Clear();
            foreach (var kvp in countries)
            {
                var c = kvp.Value;
                if (c.CapitalCentroid == default || string.IsNullOrEmpty(c.Capital))
                    continue;

                _cities.Add(new CityInfo
                {
                    WorldPos = new Vector2(c.CapitalCentroid.x, -c.CapitalCentroid.y), // Y negated for map space
                    Population = c.Population,
                    Name = c.Capital
                });
            }

            // Sort by population descending — large cities rendered first
            _cities.Sort((a, b) => b.Population.CompareTo(a.Population));

#if UNITY_EDITOR
            Debug.Log($"[CityDetail] {_cities.Count} cities loaded");
#endif
        }

        private void LateUpdate()
        {
            if (!_cameraFound)
            {
                _mapCamera = FindAnyObjectByType<MapCamera>();
                _cameraFound = true;
            }
            if (_mapCamera == null || _cities.Count == 0) return;

            float zoom = _mapCamera.CurrentZoom;

            // Hide all if zoomed out
            if (zoom < ZOOM_THRESHOLD)
            {
                HideAll();
                return;
            }

            // Calculate visibility and alpha
            float alpha = Mathf.Clamp01((zoom - ZOOM_THRESHOLD) / (ZOOM_FULL - ZOOM_THRESHOLD));

            // Get viewport bounds
            Camera cam = _mapCamera.Camera;
            if (cam == null) return;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float camX = cam.transform.position.x;
            float camY = cam.transform.position.y;

            float viewLeft = camX - halfW - 200; // padding
            float viewRight = camX + halfW + 200;
            float viewTop = camY + halfH + 200;
            float viewBottom = camY - halfH - 200;

            // Sprite scale based on zoom
            float spriteScale = Mathf.Lerp(8f, 3f, Mathf.Clamp01((zoom - ZOOM_THRESHOLD) / 15f));

            int active = 0;
            for (int i = 0; i < _cities.Count && active < POOL_SIZE; i++)
            {
                var city = _cities[i];
                float cx = city.WorldPos.x;
                float cy = city.WorldPos.y;

                // Viewport culling
                if (cx < viewLeft || cx > viewRight || cy < viewBottom || cy > viewTop)
                    continue;

                // Select sprite by population
                Sprite spr;
                float scale;
                if (city.Population > 10_000_000)
                {
                    spr = _sprLarge;
                    scale = spriteScale * 1.5f;
                }
                else if (city.Population > 1_000_000)
                {
                    spr = _sprMedium;
                    scale = spriteScale * 1.2f;
                }
                else
                {
                    spr = _sprSmall;
                    scale = spriteScale;
                }

                var go = _pool[active];
                var sr = _renderers[active];
                go.SetActive(true);
                go.transform.localPosition = new Vector3(cx, cy, -1f); // Z=-1 above map
                go.transform.localScale = new Vector3(scale, scale, 1f);
                sr.sprite = spr;
                sr.color = new Color(1f, 1f, 1f, alpha);
                active++;
            }

            // Hide remaining pool objects
            for (int i = active; i < _activeCount; i++)
                _pool[i].SetActive(false);

            _activeCount = active;
        }

        private void HideAll()
        {
            for (int i = 0; i < _activeCount; i++)
                _pool[i].SetActive(false);
            _activeCount = 0;
        }
    }
}
