// MapRenderer.cs
// Creates 3 fullscreen quads for seamless horizontal wrapping.
// Builds and manages Color LUT + Country LUT textures at runtime.
// ALL texture loading is async to avoid freezing the editor.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarStrategy.Core;
using WarStrategy.Data;

namespace WarStrategy.Map
{
    public class MapRenderer : MonoBehaviour
    {
        public const float MAP_WIDTH = 16384f;
        public const float MAP_HEIGHT = 8192f;
        public const int LUT_SIZE = 65536; // Must cover R*65536+G*256+B for all province detect_colors (R=0 → max 65535)
        public const int LUT_WIDTH = 256;  // 256×256 = 65536 pixels (Unity max tex width is 16384)
        public const int LUT_HEIGHT = 256;

        [Header("Shader")]
        [SerializeField] private Shader _mapShader;

        private Material _mapMaterial;
        private Texture2D _colorLUT;
        private Texture2D _countryLUT;
        private Texture2D _ownerLUT;
        private Texture2D _provinceTex;
        private bool _lutDirty;
        private bool _ownerLutDirty;
        private MapCamera _mapCameraRef;
        private bool _cameraSearchDone;

        // Province index → color mapping (CPU-side mirror of LUT)
        private Color[] _lutColors;
        // Province index → owner ID byte (CPU-side mirror, exact integer 0-254)
        private Color32[] _ownerPixels;

        // Map quad GameObjects (3 tiles for wrapping)
        private GameObject[] _tiles = new GameObject[3];

        private void Start()
        {
            if (_mapShader == null)
                _mapShader = Shader.Find("WarStrategy/MapShader");

            if (_mapShader == null)
            {
                Debug.LogError("[MapRenderer] MapShader not found!");
                return;
            }

            _mapMaterial = new Material(_mapShader);
            BuildMapQuads();
            BuildLUTs();

            // Load all textures async — no freeze
            StartCoroutine(LoadTexturesAsync());
        }

        // ── Quad Setup (3× horizontal wrap) ──

        private void BuildMapQuads()
        {
            for (int i = 0; i < 3; i++)
            {
                float xOffset = (i - 1) * MAP_WIDTH;

                var go = new GameObject($"MapTile_{i}");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(xOffset, 0, i * 0.5f);

                var mf = go.AddComponent<MeshFilter>();
                mf.mesh = CreateQuadMesh(MAP_WIDTH, MAP_HEIGHT);

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mapMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _tiles[i] = go;
            }
        }

        private Mesh CreateQuadMesh(float width, float height)
        {
            var mesh = new Mesh();
            mesh.name = "MapQuad";

            mesh.vertices = new Vector3[]
            {
                new(0, 0, 0),
                new(width, 0, 0),
                new(width, -height, 0),
                new(0, -height, 0)
            };

            mesh.uv = new Vector2[]
            {
                new(0, 1),
                new(1, 1),
                new(1, 0),
                new(0, 0)
            };

            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // ── Async Texture Loading ──

        private IEnumerator LoadTexturesAsync()
        {
            // Province bitmap — most critical, load first
            yield return LoadTextureAsync("Map/provinces", tex =>
            {
                _provinceTex = tex;
                _mapMaterial.SetTexture("_ProvinceTex", tex);
#if UNITY_EDITOR
                Debug.Log($"[MapRenderer] Province texture: {tex.width}x{tex.height}");
#endif
            });

            // Optional terrain layers — load one per frame to spread the cost
            yield return LoadTextureAsync("Map/terrain", tex =>
            {
                _mapMaterial.SetTexture("_TerrainTex", tex);
                _mapMaterial.SetFloat("_HasTerrain", 1f);
            });

            yield return LoadTextureAsync("Map/heightmap", tex =>
            {
                _mapMaterial.SetTexture("_HeightmapTex", tex);
                _mapMaterial.SetFloat("_HasHeightmap", 1f);
            });

            yield return LoadTextureAsync("Map/noise", tex =>
            {
                _mapMaterial.SetTexture("_NoiseTex", tex);
                _mapMaterial.SetFloat("_HasNoise", 1f);
            });

            yield return LoadTextureAsync("Map/detail", tex =>
            {
                _mapMaterial.SetTexture("_DetailTex", tex);
                _mapMaterial.SetFloat("_HasDetail", 1f);
            });

            yield return LoadTextureAsync("Map/terrain_types", tex =>
            {
                _mapMaterial.SetTexture("_TerrainTypeTex", tex);
                _mapMaterial.SetFloat("_HasTerrainTypes", 1f);
            });

            yield return LoadTextureAsync("Map/biome_atlas", tex =>
            {
                _mapMaterial.SetTexture("_BiomeAtlas", tex);
            });

#if UNITY_EDITOR
            Debug.Log("[MapRenderer] All textures loaded.");
#endif
        }

        private IEnumerator LoadTextureAsync(string path, System.Action<Texture2D> onLoaded)
        {
            var request = Resources.LoadAsync<Texture2D>(path);
            yield return request;

            var tex = request.asset as Texture2D;
            if (tex != null)
            {
                onLoaded(tex);
            }
            else
            {
                Debug.LogWarning($"[MapRenderer] Failed to load texture: Resources/{path}");
            }
        }

        // ── LUT Construction ──

        private void BuildLUTs()
        {
            // 256×256 = 65536 pixels. Index → (x=idx%256, y=idx/256)
            _colorLUT = new Texture2D(LUT_WIDTH, LUT_HEIGHT, TextureFormat.RGBA32, false);
            _colorLUT.filterMode = FilterMode.Point;
            _colorLUT.wrapMode = TextureWrapMode.Clamp;

            _lutColors = new Color[LUT_SIZE];

            // Index 0 = ocean
            _lutColors[0] = new Color(0.06f, 0.12f, 0.22f, 1f);

            // Placeholder colors — real country colors applied by SceneSetup when data loads
            for (int i = 1; i < LUT_SIZE; i++)
            {
                float h = (i * 0.618034f) % 1f;
                _lutColors[i] = Color.HSVToRGB(h, 0.3f, 0.7f);
            }

            _colorLUT.SetPixels(_lutColors);
            _colorLUT.Apply();
            _mapMaterial.SetTexture("_ColorLUT", _colorLUT);

            _countryLUT = new Texture2D(LUT_WIDTH, LUT_HEIGHT, TextureFormat.RGBA32, false);
            _countryLUT.filterMode = FilterMode.Point;
            _countryLUT.wrapMode = TextureWrapMode.Clamp;

            var countryColors = new Color[LUT_SIZE];
            for (int i = 0; i < LUT_SIZE; i++)
                countryColors[i] = Color.black;
            _countryLUT.SetPixels(countryColors);
            _countryLUT.Apply();
            _mapMaterial.SetTexture("_CountryLUT", _countryLUT);

            // Owner LUT: province index → owner ID (for GPU border detection)
            // RGBA32 linear — store owner ID in R channel as exact byte, no sRGB gamma
            _ownerLUT = new Texture2D(LUT_WIDTH, LUT_HEIGHT, TextureFormat.RGBA32, false, true); // true = LINEAR
            _ownerLUT.filterMode = FilterMode.Point;
            _ownerLUT.wrapMode = TextureWrapMode.Clamp;

            _ownerPixels = new Color32[LUT_SIZE];
            for (int i = 0; i < LUT_SIZE; i++)
                _ownerPixels[i] = new Color32(0, 0, 0, 255); // R=0 = ocean/unowned
            _ownerLUT.SetPixels32(_ownerPixels);
            _ownerLUT.Apply();
            _mapMaterial.SetTexture("_OwnerLUT", _ownerLUT);

            _mapMaterial.SetFloat("_LUTWidth", LUT_WIDTH);
        }

        // ── Dynamic LUT Updates (dirty flag pattern) ──

        public void SetProvinceColor(int provinceIndex, Color color)
        {
            if (provinceIndex < 0 || provinceIndex >= LUT_SIZE) return;
            if (_lutColors == null) return;
            _lutColors[provinceIndex] = color;
            _lutDirty = true;
        }

        /// <summary>
        /// Set the owner of a province in the Owner LUT (for GPU border detection).
        /// ownerId: unique byte per country (1-254), 0 = unowned/ocean.
        /// </summary>
        public void SetProvinceOwner(int provinceIndex, int ownerId)
        {
            if (provinceIndex < 0 || provinceIndex >= LUT_SIZE) return;
            if (_ownerPixels == null) return;
            byte b = (byte)Mathf.Clamp(ownerId, 0, 254);
            _ownerPixels[provinceIndex] = new Color32(b, b, b, 255); // same value in all channels for safety
            _ownerLutDirty = true;
        }

        private void LateUpdate()
        {
            if (_lutDirty && _colorLUT != null)
            {
                _colorLUT.SetPixels(_lutColors);
                _colorLUT.Apply();
                _lutDirty = false;
            }

            if (_ownerLutDirty && _ownerLUT != null)
            {
                _ownerLUT.SetPixels32(_ownerPixels);
                _ownerLUT.Apply();
                _ownerLutDirty = false;
            }

            // Pass zoom level to shader for terrain/political blend
            if (_mapMaterial != null)
            {
                if (_mapCameraRef == null && !_cameraSearchDone)
                {
                    _mapCameraRef = FindAnyObjectByType<MapCamera>();
                    _cameraSearchDone = true;
                }
                if (_mapCameraRef != null)
                    _mapMaterial.SetFloat("_ZoomLevel", _mapCameraRef.CurrentZoom);
            }

            UpdateTileVisibility();
        }

        private void UpdateTileVisibility()
        {
            var cam = Camera.main;
            if (cam == null || _tiles[0] == null) return;

            float camX = cam.transform.position.x;
            float halfViewW = cam.orthographicSize * cam.aspect;

            float viewLeft = camX - halfViewW;
            float viewRight = camX + halfViewW;

            _tiles[0].SetActive(viewLeft < 0);
            _tiles[2].SetActive(viewRight > MAP_WIDTH);
        }

        // ── Hover / Selection (GPU uniforms) ──

        public void SetHoverIndex(int provinceIndex)
        {
            if (_mapMaterial != null)
                _mapMaterial.SetInt("_HoverProvinceIndex", provinceIndex);
        }

        public void SetSelectedIndex(int provinceIndex)
        {
            if (_mapMaterial != null)
                _mapMaterial.SetInt("_SelectedProvinceIndex", provinceIndex);
        }

        /// <summary>
        /// Set the country-level highlight for selection screen.
        /// ownerValue: the owner LUT value for the selected country (or -1 to clear)
        /// darken: 0 = no effect, 1 = full darken/brighten effect
        /// </summary>
        public void SetCountryHighlight(float ownerValue, float darken)
        {
            if (_mapMaterial == null) return;
            _mapMaterial.SetFloat("_SelectedOwnerValue", ownerValue);
            _mapMaterial.SetFloat("_SelectionDarken", darken);
        }

        /// <summary>
        /// Set the player's owner value so the shader can highlight their territory.
        /// </summary>
        public void SetPlayerOwnerValue(float ownerValue)
        {
            if (_mapMaterial != null)
                _mapMaterial.SetFloat("_PlayerOwnerValue", ownerValue);
        }

        // ── Public access ──

        private void OnDestroy()
        {
            if (_mapMaterial != null) Destroy(_mapMaterial);
            if (_colorLUT != null) Destroy(_colorLUT);
            if (_countryLUT != null) Destroy(_countryLUT);
            if (_ownerLUT != null) Destroy(_ownerLUT);
        }

        public Texture2D ProvinceTexture => _provinceTex;
        public Material MapMaterial => _mapMaterial;
    }
}
