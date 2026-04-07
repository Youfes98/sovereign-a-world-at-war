Shader "WarStrategy/MapShader"
{
    Properties
    {
        _ProvinceTex ("Province Bitmap", 2D) = "black" {}
        _ColorLUT ("Color LUT", 2D) = "black" {}
        _CountryLUT ("Country LUT", 2D) = "black" {}
        _LUTWidth ("LUT Width", Float) = 256

        _TerrainTex ("Terrain", 2D) = "black" {}
        _HeightmapTex ("Heightmap", 2D) = "black" {}
        _NoiseTex ("Noise", 2D) = "black" {}
        _DetailTex ("Detail", 2D) = "gray" {}
        _TerrainTypeTex ("Terrain Types", 2D) = "black" {}
        _BiomeAtlas ("Biome Atlas", 2D) = "gray" {}
        _WaveNormalTex ("Wave Normal Map", 2D) = "bump" {}
        _WaveScale ("Wave Scale", Float) = 8.0

        [Toggle] _HasTerrain ("Has Terrain", Float) = 0
        [Toggle] _HasWaveNormal ("Has Wave Normal", Float) = 0
        [Toggle] _HasHeightmap ("Has Heightmap", Float) = 0
        [Toggle] _HasNoise ("Has Noise", Float) = 0
        [Toggle] _HasDetail ("Has Detail", Float) = 0
        [Toggle] _HasTerrainTypes ("Has Terrain Types", Float) = 0

        _CountryColorStr ("Country Color Strength", Range(0,1)) = 0.70
        _TerrainDesat ("Terrain Desat", Range(0,1)) = 0.03
        _ElevationStrength ("Elevation Relief", Float) = 8.0
        _NoiseStr ("Noise Strength", Float) = 0.012
        _DetailStrength ("Detail Strength", Float) = 0.12
        _BiomeDetailStrength ("Biome Detail", Float) = 0.30
        _CoastGlowStr ("Coast Glow", Float) = 0.22

        _OceanDeep ("Ocean Deep", Color) = (0.06, 0.18, 0.35, 1)
        _OceanMid ("Ocean Mid", Color) = (0.12, 0.32, 0.46, 1)
        _OceanShallow ("Ocean Shallow", Color) = (0.18, 0.52, 0.58, 1)
        _PaperTint ("Paper Tint", Color) = (1.0, 0.96, 0.88, 1)
        _PaperStrength ("Paper Strength", Float) = 0.18

        _CloudStr ("Cloud Strength", Range(0,1)) = 0.18
        _CloudScale ("Cloud Scale", Float) = 1.5
        _CloudSpeed ("Cloud Speed", Float) = 0.02
        _FogStr ("Fog Strength", Range(0,1)) = 0.06
        _FogColor ("Fog Color", Color) = (0.82, 0.85, 0.88, 1)

        _SnowHeight ("Snow Height Threshold", Range(0,1)) = 0.92
        _SnowStr ("Snow Strength", Range(0,1)) = 0.6

        _ZoomLevel ("Zoom Level", Float) = 1.0
        _ZoomTerrainStart ("Terrain Fade Start", Float) = 2.5
        _ZoomTerrainEnd ("Terrain Fade End", Float) = 6.0

        _HoverProvinceIndex ("Hover Index", Int) = -1
        _SelectedProvinceIndex ("Selected Index", Int) = -1
        _SelectionColor ("Selection Color", Color) = (1, 0.85, 0.3, 1)

        _OwnerLUT ("Owner LUT", 2D) = "black" {}
        _BorderZoomStart ("Border Zoom Start", Float) = 1.5
        _BorderZoomEnd ("Border Zoom End", Float) = 2.5
        _ProvBorderZoomStart ("Prov Border Zoom Start", Float) = 6.0
        _ProvBorderZoomEnd ("Prov Border Zoom End", Float) = 10.0
        _PlayerOwnerValue ("Player Owner Value", Float) = -1
        _SelectedOwnerValue ("Selected Owner", Float) = -1
        _SelectionDarken ("Selection Darken", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "MapPass"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_ProvinceTex);    SAMPLER(sampler_ProvinceTex);
            TEXTURE2D(_ColorLUT);       SAMPLER(sampler_ColorLUT);
            TEXTURE2D(_CountryLUT);     SAMPLER(sampler_CountryLUT);
            TEXTURE2D(_TerrainTex);     SAMPLER(sampler_TerrainTex);
            TEXTURE2D(_HeightmapTex);   SAMPLER(sampler_HeightmapTex);
            TEXTURE2D(_NoiseTex);       SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_DetailTex);      SAMPLER(sampler_DetailTex);
            TEXTURE2D(_TerrainTypeTex); SAMPLER(sampler_TerrainTypeTex);
            TEXTURE2D(_BiomeAtlas);     SAMPLER(sampler_BiomeAtlas);
            TEXTURE2D(_OwnerLUT);       SAMPLER(sampler_OwnerLUT);
            TEXTURE2D(_WaveNormalTex);  SAMPLER(sampler_WaveNormalTex);

            CBUFFER_START(UnityPerMaterial)
                float _LUTWidth;
                float4 _ProvinceTex_TexelSize;
                float4 _HeightmapTex_TexelSize;

                float _HasTerrain, _HasHeightmap, _HasNoise, _HasDetail, _HasTerrainTypes, _HasWaveNormal;
                float _WaveScale;
                float _CountryColorStr, _TerrainDesat, _ElevationStrength;
                float _NoiseStr, _DetailStrength, _BiomeDetailStrength, _CoastGlowStr;

                float4 _OceanDeep, _OceanMid, _OceanShallow, _PaperTint;
                float _PaperStrength;

                float _ZoomLevel, _ZoomTerrainStart, _ZoomTerrainEnd;

                int _HoverProvinceIndex, _SelectedProvinceIndex;
                float4 _SelectionColor;

                float _BorderZoomStart, _BorderZoomEnd;
                float _ProvBorderZoomStart, _ProvBorderZoomEnd;
                float _PlayerOwnerValue;
                float _SelectedOwnerValue;
                float _SelectionDarken;

                float _CloudStr, _CloudScale, _CloudSpeed;
                float _FogStr;
                float4 _FogColor;
                float _SnowHeight, _SnowStr;
            CBUFFER_END

            // ── Helpers ──

            int RGBToIndex(float3 c)
            {
                int idx = (int)(c.r * 255.0 + 0.5) * 65536
                        + (int)(c.g * 255.0 + 0.5) * 256
                        + (int)(c.b * 255.0 + 0.5);
                return min(idx, 65535); // clamp to LUT size
            }

            float3 LookupColor(int idx)
            {
                float u = (fmod((float)idx, _LUTWidth) + 0.5) / _LUTWidth;
                float v = (floor((float)idx / _LUTWidth) + 0.5) / _LUTWidth;
                return SAMPLE_TEXTURE2D_LOD(_ColorLUT, sampler_ColorLUT, float2(u, v), 0).rgb;
            }

            float Lum(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            // Owner LUT lookup: province index → owner ID (integer 0-254)
            // R8 linear texture stores exact byte values
            int LookupOwner(int i)
            {
                float u = (fmod((float)i, _LUTWidth) + 0.5) / _LUTWidth;
                float v = (floor((float)i / _LUTWidth) + 0.5) / _LUTWidth;
                float raw = SAMPLE_TEXTURE2D_LOD(_OwnerLUT, sampler_OwnerLUT, float2(u, v), 0).r;
                return (int)(raw * 255.0 + 0.5); // exact byte → integer
            }

            float3 Desat(float3 c, float a)
            {
                float l = Lum(c);
                return lerp(c, float3(l,l,l), a);
            }

            float3 SampleProv(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_ProvinceTex, sampler_ProvinceTex, uv, 0).rgb;
            }

            // Simple hash for ocean animation
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Smooth value noise (band-limited, no grid artifacts)
            float smoothNoise(float2 p)
            {
                float2 f = frac(p); float2 i = floor(p);
                float2 s = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i+float2(1,0)), s.x),
                            lerp(hash21(i+float2(0,1)), hash21(i+float2(1,1)), s.x), s.y);
            }

            // Atlas palette mapping — pushes colors toward curated warm tones
            float3 PaletteMap(float3 col)
            {
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float3 shadows = float3(0.48, 0.50, 0.53);
                float3 mid     = float3(0.92, 0.88, 0.78);
                float3 highs   = float3(1.00, 0.96, 0.88);
                float3 palette = lerp(shadows, mid, smoothstep(0.2, 0.6, lum));
                palette = lerp(palette, highs, smoothstep(0.6, 1.0, lum));
                return lerp(col, col * palette, 0.15); // minimal — preserve biome vibrancy
            }

            // Procedural biome coloring from terrain_types + heightmap
            float3 BiomeColor(float terrainTypeRaw, float height, float2 uv)
            {
                // Curated palette — bright, vivid, painterly (hi = LIGHTER at altitude)
                float3 desert_lo  = float3(0.80, 0.62, 0.34); float3 desert_hi  = float3(0.92, 0.78, 0.48);
                float3 plains_lo  = float3(0.58, 0.62, 0.32); float3 plains_hi  = float3(0.78, 0.78, 0.48);
                float3 forest_lo  = float3(0.18, 0.45, 0.15); float3 forest_hi  = float3(0.35, 0.60, 0.30);
                float3 mount_lo   = float3(0.50, 0.45, 0.35); float3 mount_hi   = float3(0.68, 0.62, 0.50);
                float3 tundra_lo  = float3(0.78, 0.80, 0.84); float3 tundra_hi  = float3(0.90, 0.92, 0.94);
                float3 jungle_lo  = float3(0.12, 0.42, 0.12); float3 jungle_hi  = float3(0.25, 0.60, 0.25);

                int biome = clamp((int)(terrainTypeRaw * 5.0 + 0.5), 0, 5);
                float h = saturate(height);

                float3 col;
                if      (biome == 0) col = lerp(plains_lo, plains_hi, h);
                else if (biome == 1) col = lerp(forest_lo, forest_hi, h);
                else if (biome == 2) col = lerp(mount_lo,  mount_hi,  h);
                else if (biome == 3) col = lerp(desert_lo, desert_hi, h);
                else if (biome == 4) col = lerp(tundra_lo, tundra_hi, h);
                else                 col = lerp(jungle_lo, jungle_hi, h);

                // Natural variation within biome — breaks uniformity
                float nv = smoothNoise(uv * 3.0);
                col *= 0.90 + 0.20 * nv;

                // Cross-biome edge softening: sample neighbors and blend
                float nv2 = smoothNoise(uv * 7.0 + float2(31.0, 17.0));
                col *= 0.97 + 0.06 * nv2;

                return col;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 px = _ProvinceTex_TexelSize.xy;
                float2 hpx = _HeightmapTex_TexelSize.xy;

                // ── Province index ──
                float3 provCol = SAMPLE_TEXTURE2D(_ProvinceTex, sampler_ProvinceTex, uv).rgb;
                int idx = RGBToIndex(provCol);

                // ── Sample 4 neighbors for AA and border detection ──
                int idxL = RGBToIndex(SampleProv(uv + float2(-px.x, 0)));
                int idxR = RGBToIndex(SampleProv(uv + float2( px.x, 0)));
                int idxU = RGBToIndex(SampleProv(uv + float2(0, -px.y)));
                int idxD = RGBToIndex(SampleProv(uv + float2(0,  px.y)));

                // Edge detection: how many neighbors are different provinces
                float edgeFactor = 0.0;
                float3 neighborBlend = float3(0,0,0);
                int nCount = 0;
                if (idxL != idx) { edgeFactor += 0.25; if (idxL > 0) { neighborBlend += LookupColor(idxL); nCount++; } }
                if (idxR != idx) { edgeFactor += 0.25; if (idxR > 0) { neighborBlend += LookupColor(idxR); nCount++; } }
                if (idxU != idx) { edgeFactor += 0.25; if (idxU > 0) { neighborBlend += LookupColor(idxU); nCount++; } }
                if (idxD != idx) { edgeFactor += 0.25; if (idxD > 0) { neighborBlend += LookupColor(idxD); nCount++; } }
                if (nCount > 0) neighborBlend /= (float)nCount;

                // ── Heightmap with 3×3 Sobel normals ──
                float height = 0.0;
                float relief = 0.0;
                float dx = 0.0;
                float dy = 0.0;
                float selfShadow = 1.0;
                // Parallax terrain UV (shifted for visual depth, separate from political UV)
                float2 terrainUV = uv;
                if (_HasHeightmap > 0.5)
                {
                    height = SAMPLE_TEXTURE2D(_HeightmapTex, sampler_HeightmapTex, uv).r;

                    // Parallax relief: LAND ONLY (skip for ocean to prevent seafloor sliding)
                    float pomFade = (idx > 0) ? saturate((_ZoomLevel - 4.0) / 6.0) : 0.0;
                    if (pomFade > 0.01)
                    {
                        // Multi-step parallax (4 iterations for smooth depth)
                        float2 pDir = float2(-0.4, 0.25) * hpx * 3.0 * pomFade;
                        float ph = height;
                        [unroll] for (int pi = 0; pi < 4; pi++)
                        {
                            terrainUV -= pDir * ph;
                            ph = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV, 0).r;
                        }
                        height = ph; // use parallax-corrected height
                    }

                    // 3×3 Sobel kernel for smooth normals (8 neighbor reads)
                    float hTL = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2(-hpx.x, -hpx.y), 0).r;
                    float hT  = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2(0, -hpx.y), 0).r;
                    float hTR = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2( hpx.x, -hpx.y), 0).r;
                    float hL  = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2(-hpx.x, 0), 0).r;
                    float hR  = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2( hpx.x, 0), 0).r;
                    float hBL = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2(-hpx.x,  hpx.y), 0).r;
                    float hB  = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2(0,  hpx.y), 0).r;
                    float hBR = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, terrainUV + float2( hpx.x,  hpx.y), 0).r;

                    // Sobel operators
                    dx = (hTR + 2.0*hR + hBR) - (hTL + 2.0*hL + hBL);
                    dy = (hBL + 2.0*hB + hBR) - (hTL + 2.0*hT + hTR);
                    relief = (-dx + dy) * _ElevationStrength;

                    // Self-shadowing: LAND ONLY, zoom-gated, 12 steps for smooth shadows
                    float shadowFade = (idx > 0) ? saturate((_ZoomLevel - 2.0) / 3.0) : 0.0;
                    if (shadowFade > 0.01)
                    {
                        float2 sunUV = normalize(float2(-1.5, 0.8)) * hpx * 2.0;
                        float currentH = height;
                        selfShadow = 1.0;
                        [unroll] for (int si = 1; si <= 12; si++)
                        {
                            float sH = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex,
                                terrainUV + sunUV * (float)si, 0).r;
                            float horizon = currentH + 0.006 * (float)si;
                            if (sH > horizon) { selfShadow = 0.45; break; }
                        }
                        selfShadow = lerp(1.0, selfShadow, 0.65 * shadowFade);
                    }

                    // Curvature-based valley AO (concave = darker)
                    if (_HasHeightmap > 0.5 && idx > 0)
                    {
                        float avgH = (hT + hB + hL + hR) * 0.25;
                        float curveAO = saturate(1.0 - (avgH - height) * 10.0);
                        selfShadow *= lerp(0.6, 1.0, curveAO);
                    }
                }

                // ── Terrain base (AFTER heightmap so terrainUV is parallax-shifted) ──
                float3 terrain = float3(0.5, 0.5, 0.5);
                if (_HasTerrain > 0.5)
                    terrain = SAMPLE_TEXTURE2D(_TerrainTex, sampler_TerrainTex, terrainUV).rgb;

                float3 col;

                if (idx == 0)
                {
                    // ════════════════════════════════════════
                    // OCEAN — depth gradient + wave normals + specular
                    // ════════════════════════════════════════

                    float waterDepth = saturate((0.15 - height) * 6.0); // 0=coast, 1=deep
                    float t = _Time.y * 0.06;

                    // Depth gradient: shallow teal → deep navy
                    float3 shallowCol = float3(0.12, 0.45, 0.52);
                    float3 deepCol = float3(0.04, 0.14, 0.28);
                    col = lerp(shallowCol, deepCol, waterDepth);

                    // Large-scale ocean patterns visible at world zoom
                    float oceanPat = smoothNoise(uv * 3.0 + t * 0.1) * 0.5 + smoothNoise(uv * 6.0 - t * 0.05) * 0.5;
                    col = lerp(col, col * 1.12, (oceanPat - 0.4) * 0.25);

                    // Seafloor visible in shallows
                    if (_HasTerrain > 0.5)
                    {
                        float3 seafloor = SAMPLE_TEXTURE2D(_TerrainTex, sampler_TerrainTex, uv).rgb * float3(0.5, 0.65, 0.62);
                        col = lerp(col, seafloor, (1.0 - smoothstep(0.0, 0.35, waterDepth)) * 0.40);
                    }

                    // Underwater relief
                    if (_HasHeightmap > 0.5)
                    {
                        float3 uwN = normalize(float3(-dx * 8.0, -dy * 8.0, 1.0));
                        float uwL = 0.85 + 0.15 * max(dot(uwN, normalize(float3(-0.4, 0.3, 0.8))), 0.0);
                        col *= lerp(1.0, uwL, (1.0 - waterDepth) * 0.5);
                    }

                    // Wave normal map (dual scroll for organic movement)
                    if (_HasWaveNormal > 0.5)
                    {
                        float2 wUV1 = uv * _WaveScale + float2(t * 0.35, t * 0.25);
                        float2 wUV2 = uv * (_WaveScale * 1.7) + float2(-t * 0.2, t * 0.4);
                        float3 wn1 = SAMPLE_TEXTURE2D(_WaveNormalTex, sampler_WaveNormalTex, wUV1).rgb * 2.0 - 1.0;
                        float3 wn2 = SAMPLE_TEXTURE2D(_WaveNormalTex, sampler_WaveNormalTex, wUV2).rgb * 2.0 - 1.0;
                        float3 waveN = normalize(float3((wn1.xy + wn2.xy) * 0.5, 1.0));

                        // Specular from wave normals (sun glint)
                        float2 sunDir2D = normalize(float2(-0.5, 0.3));
                        float waveSpec = pow(saturate(dot(waveN.xy, sunDir2D) * 0.5 + 0.5), 32.0) * 0.25;
                        waveSpec *= (1.0 - waterDepth * 0.5);
                        col += float3(waveSpec, waveSpec * 0.96, waveSpec * 0.88);

                        // Surface perturbation from normals
                        col += (waveN.x * 0.008 + waveN.y * 0.005) * (1.0 - waterDepth);
                    }
                    else
                    {
                        // Fallback: procedural waves when no normal map loaded
                        float w1 = smoothNoise(uv * 30.0 + float2(t, t * 0.7));
                        float w2 = smoothNoise(uv * 55.0 + float2(-t * 0.6, t * 0.4));
                        col += (w1 * 0.6 + w2 * 0.4 - 0.5) * lerp(0.04, 0.08, 1.0 - waterDepth);
                        // Procedural specular
                        float specN = smoothNoise(uv * 80.0 + float2(t * 0.5, t * 0.3));
                        col += pow(saturate(specN * 1.2 - 0.1), 4.0) * 0.18;
                    }

                    // Coast detection
                    float coastDist = 0.0;
                    if (RGBToIndex(SampleProv(uv + float2(-px.x, -px.y))) > 0) coastDist += 0.7;
                    if (RGBToIndex(SampleProv(uv + float2(    0, -px.y))) > 0) coastDist += 1.0;
                    if (RGBToIndex(SampleProv(uv + float2( px.x, -px.y))) > 0) coastDist += 0.7;
                    if (RGBToIndex(SampleProv(uv + float2(-px.x,     0))) > 0) coastDist += 1.0;
                    if (RGBToIndex(SampleProv(uv + float2( px.x,     0))) > 0) coastDist += 1.0;
                    if (RGBToIndex(SampleProv(uv + float2(-px.x,  px.y))) > 0) coastDist += 0.7;
                    if (RGBToIndex(SampleProv(uv + float2(    0,  px.y))) > 0) coastDist += 1.0;
                    if (RGBToIndex(SampleProv(uv + float2( px.x,  px.y))) > 0) coastDist += 0.7;
                    coastDist = saturate(coastDist * 0.12);

                    // Coastal brightening
                    float coastGrad = saturate(1.0 - coastDist * 2.5);
                    coastGrad = coastGrad * coastGrad;
                    col *= lerp(1.0, 1.15, coastGrad * 0.3);

                    // Coastal foam
                    float foamN = smoothNoise(uv * 45.0 + t * 0.12);
                    float foamBand = smoothstep(0.01, 0.08, coastDist) * (1.0 - smoothstep(0.08, 0.16, coastDist));
                    float foam = foamBand * smoothstep(0.42, 0.68, foamN) * 0.22 * saturate((_ZoomLevel - 1.5) / 3.0);
                    col = lerp(col, float3(0.82, 0.88, 0.92), foam);
                }
                else
                {
                    // ════════════════════════════════════════
                    // LAND — terrain-first, country color as overlay
                    // ════════════════════════════════════════

                    float3 countryCol = LookupColor(idx);

                    // Zoom-based political→terrain transition
                    float zoomFade = 1.0 - saturate((_ZoomLevel - _ZoomTerrainStart) / (_ZoomTerrainEnd - _ZoomTerrainStart));
                    float colorStr = _CountryColorStr * zoomFade;

                    // Step 1: Blend terrain texture with procedural biome coloring
                    float3 biomeBase = terrain;
                    float cachedTerrainType = -1.0; // cache for reuse in biome detail
                    if (_HasTerrainTypes > 0.5 && _HasTerrain > 0.5)
                    {
                        float tt = SAMPLE_TEXTURE2D(_TerrainTypeTex, sampler_TerrainTypeTex, terrainUV).r;
                        cachedTerrainType = tt;
                        float3 biomeCol = BiomeColor(tt, height, terrainUV);
                        // Boost terrain saturation before blending
                        float tLum = Lum(terrain);
                        // Strip satellite color, keep only luminance detail
                        float3 terrLumOnly = float3(tLum, tLum, tLum);
                        float3 terrSubtle = lerp(terrLumOnly, terrain, 0.3); // 30% color, 70% luminance
                        // Biome color dominates — painterly, not photographic
                        biomeBase = biomeCol * 0.75 + terrSubtle * 0.25;
                    }
                    else if (_HasTerrainTypes > 0.5)
                    {
                        float tt = SAMPLE_TEXTURE2D(_TerrainTypeTex, sampler_TerrainTypeTex, uv).r;
                        biomeBase = BiomeColor(tt, height, uv);
                    }
                    float terrLum = Lum(biomeBase);
                    float desatAmount = _TerrainDesat * zoomFade;
                    float3 terrBase = Desat(biomeBase, desatAmount);

                    // Step 2: Overlay blend — terrain luminance + country hue (V3 style)
                    float3 cBoost = countryCol * 1.5;
                    float3 overlaid = lerp(
                        2.0 * terrBase * cBoost,
                        1.0 - 2.0 * (1.0 - terrBase) * (1.0 - cBoost),
                        step(0.5, terrBase));
                    float3 tinted = lerp(terrBase, overlaid, colorStr);

                    // Flat area fill boost
                    float flatness = 1.0 - saturate(abs(relief) * 3.0);
                    tinted = lerp(tinted, lerp(terrBase, countryCol, 0.6) * (terrLum * 0.5 + 0.6), colorStr * 0.35 * flatness);

                    col = tinted;

                    // Step 3: Multi-scale biome detail (more detail as you zoom IN)
                    if (_HasTerrainTypes > 0.5)
                    {
                        // Reuse cached terrain type if available, otherwise sample
                        float ttRaw = cachedTerrainType >= 0.0 ? cachedTerrainType :
                            SAMPLE_TEXTURE2D(_TerrainTypeTex, sampler_TerrainTypeTex, terrainUV).r;
                        float tt = ttRaw * 5.0;
                        int bi = clamp((int)(tt + 0.5), 0, 5);
                        float ac = (float)(bi % 3);
                        float ar = (float)(bi / 3);

                        // Scale 1: broad biome pattern (always visible)
                        float2 buv = float2((ac + frac(terrainUV.x * 8.0)) / 3.0, (ar + frac(terrainUV.y * 8.0)) / 2.0);
                        float3 bc = SAMPLE_TEXTURE2D(_BiomeAtlas, sampler_BiomeAtlas, buv).rgb;
                        float2 buv2 = float2((ac + frac(terrainUV.x * 11.3 + smoothNoise(terrainUV * 3.0) * 0.1)) / 3.0,
                                              (ar + frac(terrainUV.y * 11.3)) / 2.0);
                        float3 bc2 = SAMPLE_TEXTURE2D(_BiomeAtlas, sampler_BiomeAtlas, buv2).rgb;
                        bc = bc * 0.6 + bc2 * 0.4;
                        col = lerp(col, col * bc, _BiomeDetailStrength * 0.6);

                        // Scale 2: medium detail (visible when zoomed in past 3x)
                        float midZoom = saturate((_ZoomLevel - 3.0) / 4.0);
                        if (midZoom > 0.01)
                        {
                            float2 buv3 = float2((ac + frac(uv.x * 24.0)) / 3.0, (ar + frac(uv.y * 24.0)) / 2.0);
                            float3 bc3 = SAMPLE_TEXTURE2D(_BiomeAtlas, sampler_BiomeAtlas, buv3).rgb;
                            col = lerp(col, col * bc3, _BiomeDetailStrength * 0.35 * midZoom);
                        }

                        // Scale 3: fine detail (visible when very zoomed in past 6x)
                        float closeZoom = saturate((_ZoomLevel - 6.0) / 6.0);
                        if (closeZoom > 0.01)
                        {
                            float2 buv4 = float2((ac + frac(uv.x * 48.0 + 0.37)) / 3.0, (ar + frac(uv.y * 48.0 + 0.71)) / 2.0);
                            float3 bc4 = SAMPLE_TEXTURE2D(_BiomeAtlas, sampler_BiomeAtlas, buv4).rgb;
                            col = lerp(col, col * bc4, _BiomeDetailStrength * 0.25 * closeZoom);
                        }
                    }

                    // Multi-scale brushstroke detail (stronger at close zoom to mask pixelation)
                    if (_HasDetail > 0.5)
                    {
                        // Medium zoom (3+): broad brushwork
                        float detZoom1 = saturate((_ZoomLevel - 3.0) / 4.0);
                        if (detZoom1 > 0.01)
                        {
                            float det1 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, terrainUV * 12.0).r;
                            col *= lerp(1.0, 0.88 + 0.24 * det1, _DetailStrength * 2.5 * detZoom1);
                        }
                        // Close zoom (6+): fine brushwork
                        float detZoom2 = saturate((_ZoomLevel - 6.0) / 4.0);
                        if (detZoom2 > 0.01)
                        {
                            float det2 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, terrainUV * 32.0 + 0.37).r;
                            col *= lerp(1.0, 0.85 + 0.30 * det2, _DetailStrength * 3.5 * detZoom2);
                        }
                        // Ultra-close zoom (10+): micro texture to completely mask base pixelation
                        float detZoom3 = saturate((_ZoomLevel - 10.0) / 5.0);
                        if (detZoom3 > 0.01)
                        {
                            float det3 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, terrainUV * 80.0 + 0.71).r;
                            float det4 = smoothNoise(terrainUV * 60.0);
                            float ultraMix = det3 * 0.5 + det4 * 0.5;
                            col *= lerp(1.0, 0.82 + 0.36 * ultraMix, _DetailStrength * 4.0 * detZoom3);
                        }
                    }

                    // ── V3-style biome contrast layers (zoom 6+) ──
                    float biomeZoom = saturate((_ZoomLevel - 6.0) / 3.0);
                    if (biomeZoom > 0.01 && _HasTerrainTypes > 0.5)
                    {
                        // Zoom-gated UV scaling — detail gets "higher-res" as you zoom in
                        float uvScale = lerp(1.0, 8.0, biomeZoom);
                        float2 detailUV = terrainUV * uvScale;

                        int detBiome = clamp((int)(cachedTerrainType * 5.0 + 0.5), 0, 5);

                        // Forest: visible "tree blob" shadows
                        if (detBiome == 1 || detBiome == 5)
                        {
                            float trees = step(0.55, smoothNoise(detailUV * 3.0) * 1.5);
                            col *= lerp(1.0, 0.72, trees * biomeZoom * 0.6);
                        }

                        // Plains: farmland grid pattern (HUGE for V3 look)
                        if (detBiome == 0)
                        {
                            float gridX = sin(detailUV.x * 0.8);
                            float gridY = sin(detailUV.y * 0.8);
                            float farms = step(0.3, gridX * gridY);
                            col = lerp(col, col * 1.15, farms * biomeZoom * 0.15);
                        }

                        // Desert: sand ripple lines
                        if (detBiome == 3)
                        {
                            float ripples = sin(detailUV.x * 1.5 + smoothNoise(detailUV * 0.5) * 2.0);
                            col *= 1.0 + ripples * 0.04 * biomeZoom;
                        }

                        // Mountain: rocky crag contrast
                        if (detBiome == 2)
                        {
                            float crags = smoothNoise(detailUV * 4.0);
                            col *= lerp(1.0, 0.78, step(0.6, crags) * biomeZoom * 0.4);
                        }
                    }

                    // Step 4: Directional hillshading + AO + elevation color + snow
                    if (_HasHeightmap > 0.5)
                    {
                        // Reconstruct normal from heightmap gradient (dx/dy computed earlier)
                        float3 normal = normalize(float3(-dx * _ElevationStrength, -dy * _ElevationStrength, 1.0));
                        float3 sunDir = normalize(float3(-1.5, 0.8, 2.0));
                        float diffuse = max(dot(normal, sunDir), 0.0);

                        // Height-masked relief: plains stay flat, mountains get dramatic shading
                        float reliefMask = smoothstep(0.12, 0.40, height);
                        float lighting = lerp(0.92, 0.50 + 0.50 * diffuse, reliefMask);

                        float grad = sqrt(dx*dx + dy*dy);
                        float ao = saturate(1.0 - grad * _ElevationStrength * 0.8);
                        lighting *= lerp(0.88, 1.0, ao);
                        col *= clamp(lighting, 0.42, 1.50);
                        // Apply self-shadowing (mountains cast shadows on valleys)
                        col *= selfShadow;
                        // Elevation color shift: warm lowlands, slightly cool highlands
                        float3 lowTint  = float3(1.02, 0.98, 0.92); // warm boost
                        float3 highTint = float3(0.92, 0.92, 0.94); // barely cool
                        col *= lerp(lowTint, highTint, saturate(height * 0.4));
                        // Snow: mountain peaks + polar latitude
                        float snowMask = smoothstep(_SnowHeight, _SnowHeight + 0.12, height);
                        float latitude = abs(uv.y - 0.5) * 2.0;
                        float polarSnow = smoothstep(0.78, 0.92, latitude);
                        snowMask = max(snowMask, polarSnow * 0.85);
                        // Snow is brighter on sun-facing slopes
                        float snowLight = 0.85 + 0.15 * diffuse;
                        float3 snowColor = float3(0.92, 0.93, 0.95) * snowLight;
                        col = lerp(col, snowColor, snowMask * _SnowStr);

                        // Rivers removed — heightmap gradient detection was unreliable
                        // TODO: Add dedicated river mask texture for accurate rivers
                    }

                    // Step 5: Unified noise system (smooth, zoom-aware)
                    if (_HasNoise > 0.5)
                    {
                        float n1 = smoothNoise(uv * 1.5);   // broad terrain variation
                        float n2 = smoothNoise(uv * 5.0);   // regional variation
                        float noiseMix = (n1 - 0.5) * _NoiseStr + (n2 - 0.5) * _NoiseStr * 0.4;
                        // Detail: stronger when zoomed in (two zoom tiers)
                        float detailZoom = saturate((_ZoomLevel - 3.0) / 5.0);
                        if (_HasDetail > 0.5 && detailZoom > 0.01)
                        {
                            float d = smoothNoise(uv * 8.0);
                            noiseMix += (d - 0.5) * _DetailStrength * detailZoom;
                            // Extra close-up terrain texture (zoom 6+)
                            float closeZoom = saturate((_ZoomLevel - 6.0) / 6.0);
                            if (closeZoom > 0.01)
                            {
                                float d2 = smoothNoise(uv * 20.0);
                                noiseMix += (d2 - 0.5) * _DetailStrength * 0.5 * closeZoom;
                            }
                        }
                        col += noiseMix;
                    }

                    // Step 6: Paper texture with directional fibers
                    float paperStr = _PaperStrength * zoomFade;
                    if (paperStr > 0.01)
                    {
                        float pn1 = smoothNoise(uv * 5.0 + float2(42.0, 17.0));
                        float pn2 = smoothNoise(uv * 15.0 + float2(13.0, 31.0));
                        float paperNoise = pn1 * 0.6 + pn2 * 0.4;
                        float fiber = sin((uv.x + pn1 * 0.1) * 120.0) * 0.03; // stronger fibers
                        paperNoise += fiber;
                        float3 paperCol = _PaperTint.rgb * lerp(0.94, 1.06, paperNoise); // wider variation
                        col = Desat(col, paperStr * 0.12);
                        col = lerp(col, col * paperCol, paperStr);
                    }

                    // Step 7: Coast darkening (softened)
                    bool nearCoast = (idxL == 0 || idxR == 0 || idxU == 0 || idxD == 0);
                    if (nearCoast)
                        col *= 0.93;

                    // Step 8: Anti-alias province boundaries
                    if (edgeFactor > 0.01 && nCount > 0)
                    {
                        float3 blendedNeighbor = neighborBlend;
                        blendedNeighbor = terrBase * lerp(float3(1,1,1), blendedNeighbor * 1.8, _CountryColorStr);
                        col = lerp(col, blendedNeighbor, edgeFactor * 0.35);
                    }

                    // ── Micro color variation + palette mapping + global harmony ──
                    col *= 0.98 + 0.04 * smoothNoise(uv * 2.0);
                    col = PaletteMap(col);
                    col = lerp(col, col * float3(1.02, 0.99, 0.96), 0.25); // subtle warm, not mud

                    // ── Step 9: GPU Border Detection (integer comparison, 8-neighbor AA) ──
                    // If center pixel itself is an interpolated artifact (owner=0, idx>0),
                    // inherit ownership from the nearest valid cardinal neighbor to avoid
                    // drawing false borders around stray pixels.
                    int ownerC = LookupOwner(idx);
                    int ownerL = LookupOwner(idxL);
                    int ownerR = LookupOwner(idxR);
                    int ownerU = LookupOwner(idxU);
                    int ownerD = LookupOwner(idxD);

                    // If center is unassigned (interpolated artifact), inherit from first valid neighbor
                    if (ownerC == 0)
                    {
                        if      (ownerL > 0) ownerC = ownerL;
                        else if (ownerR > 0) ownerC = ownerR;
                        else if (ownerU > 0) ownerC = ownerU;
                        else if (ownerD > 0) ownerC = ownerD;
                    }

                    // Diagonal neighbors for smoother borders
                    int idxUL = RGBToIndex(SampleProv(uv + float2(-px.x, -px.y)));
                    int idxUR = RGBToIndex(SampleProv(uv + float2( px.x, -px.y)));
                    int idxDL = RGBToIndex(SampleProv(uv + float2(-px.x,  px.y)));
                    int idxDR = RGBToIndex(SampleProv(uv + float2( px.x,  px.y)));
                    int ownerUL = LookupOwner(idxUL);
                    int ownerUR = LookupOwner(idxUR);
                    int ownerDL = LookupOwner(idxDL);
                    int ownerDR = LookupOwner(idxDR);

                    // Country border: require at least 2 neighbors with a DIFFERENT valid owner.
                    // Single-pixel artifacts at province boundaries produce isolated mismatches
                    // that shouldn't trigger country borders.
                    // FIX: Treat owner=0 (unassigned/interpolated pixels) as same-owner as center.
                    // When the province bitmap is downscaled, interpolated boundary pixels produce
                    // RGB values that map to unassigned indices (owner=0). These must NOT trigger borders.
                    int effOwnerL  = ownerL  > 0 ? ownerL  : ownerC;
                    int effOwnerR  = ownerR  > 0 ? ownerR  : ownerC;
                    int effOwnerU  = ownerU  > 0 ? ownerU  : ownerC;
                    int effOwnerD  = ownerD  > 0 ? ownerD  : ownerC;
                    int effOwnerUL = ownerUL > 0 ? ownerUL : ownerC;
                    int effOwnerUR = ownerUR > 0 ? ownerUR : ownerC;
                    int effOwnerDL = ownerDL > 0 ? ownerDL : ownerC;
                    int effOwnerDR = ownerDR > 0 ? ownerDR : ownerC;

                    // Inner ring (1px distance) — 8 neighbors
                    float countryEdgeW = 0.0;
                    if (effOwnerL  != ownerC) countryEdgeW += 1.0;
                    if (effOwnerR  != ownerC) countryEdgeW += 1.0;
                    if (effOwnerU  != ownerC) countryEdgeW += 1.0;
                    if (effOwnerD  != ownerC) countryEdgeW += 1.0;
                    if (effOwnerUL != ownerC) countryEdgeW += 0.7;
                    if (effOwnerUR != ownerC) countryEdgeW += 0.7;
                    if (effOwnerDL != ownerC) countryEdgeW += 0.7;
                    if (effOwnerDR != ownerC) countryEdgeW += 0.7;

                    // Outer ring (2px distance) — thickens the border line
                    float2 px2 = px * 2.0;
                    int oL2  = LookupOwner(RGBToIndex(SampleProv(uv + float2(-px2.x, 0))));
                    int oR2  = LookupOwner(RGBToIndex(SampleProv(uv + float2( px2.x, 0))));
                    int oU2  = LookupOwner(RGBToIndex(SampleProv(uv + float2(0, -px2.y))));
                    int oD2  = LookupOwner(RGBToIndex(SampleProv(uv + float2(0,  px2.y))));
                    int eL2  = oL2 > 0 ? oL2 : ownerC;
                    int eR2  = oR2 > 0 ? oR2 : ownerC;
                    int eU2  = oU2 > 0 ? oU2 : ownerC;
                    int eD2  = oD2 > 0 ? oD2 : ownerC;
                    if (eL2 != ownerC) countryEdgeW += 0.5;
                    if (eR2 != ownerC) countryEdgeW += 0.5;
                    if (eU2 != ownerC) countryEdgeW += 0.5;
                    if (eD2 != ownerC) countryEdgeW += 0.5;

                    countryEdgeW = saturate(countryEdgeW / 3.0);

                    // Province border: same owner, different province index
                    // Use effective owners so interpolated pixels (owner=0) don't break province borders
                    float provEdgeW = 0.0;
                    if (countryEdgeW < 0.1)
                    {
                        if (idxL != idx && effOwnerL == ownerC && ownerC > 0) provEdgeW += 1.0;
                        if (idxR != idx && effOwnerR == ownerC && ownerC > 0) provEdgeW += 1.0;
                        if (idxU != idx && effOwnerU == ownerC && ownerC > 0) provEdgeW += 1.0;
                        if (idxD != idx && effOwnerD == ownerC && ownerC > 0) provEdgeW += 1.0;
                        provEdgeW = saturate(provEdgeW / 2.0);
                    }

                    // Zoom-based fade IN (stay visible once they appear)
                    float countryBorderFade = saturate((_ZoomLevel - _BorderZoomStart) / (_BorderZoomEnd - _BorderZoomStart));
                    float provBorderFade = saturate((_ZoomLevel - _ProvBorderZoomStart) / (_ProvBorderZoomEnd - _ProvBorderZoomStart));

                    if (countryEdgeW > 0.01 && countryBorderFade > 0.01)
                    {
                        float3 inkColor = float3(0.15, 0.12, 0.08); // very dark ink — prominent borders
                        float inkNoise = 0.88 + 0.12 * smoothNoise(uv * 20.0);
                        float borderStr = 0.85 * countryEdgeW * countryBorderFade * inkNoise;
                        col = lerp(col, col * inkColor, borderStr);
                        // Inner glow — warm edge light
                        col += countryEdgeW * countryBorderFade * float3(0.06, 0.04, 0.02) * (1.0 - borderStr);
                    }

                    if (provEdgeW > 0.01 && provBorderFade > 0.01)
                        col = lerp(col, col * 0.80, 0.25 * provEdgeW * provBorderFade);

                    // ── Player territory highlight ──
                    int playerOwnerId = (int)(_PlayerOwnerValue * 255.0 + 0.5);
                    if (playerOwnerId > 0 && ownerC == playerOwnerId)
                    {
                        float3 pCountryCol = LookupColor(idx);
                        float playerTint = saturate((_ZoomLevel - 2.0) / 4.0) * 0.15;
                        col = lerp(col, col * pCountryCol * 2.0, playerTint);
                    }

                    // ── Step 10: Country-level selection highlight ──
                    // When a country is selected (during country selection phase),
                    // darken everything else and brighten the selected country
                    if (_SelectionDarken > 0.01)
                    {
                        int selectedOwnerId = (int)(_SelectedOwnerValue * 255.0 + 0.5);
                        if (selectedOwnerId > 0 && ownerC > 0)
                        {
                            if (ownerC == selectedOwnerId)
                            {
                                // Multiplicative highlight: keeps terrain contrast intact
                                float3 highlightCol = float3(1.3, 0.85, 0.35); // golden orange
                                col *= lerp(float3(1,1,1), highlightCol, 0.5 * _SelectionDarken);
                                col *= (1.0 + 0.15 * _SelectionDarken); // slight brightness boost
                            }
                            else
                            {
                                // Other countries: heavily darkened + desaturated
                                col *= lerp(1.0, 0.25, _SelectionDarken);
                                float lum = dot(col, float3(0.299, 0.587, 0.114));
                                col = lerp(col, float3(lum, lum, lum), 0.5 * _SelectionDarken);
                            }
                        }
                    }

                    // ── Interactive: Hover & Selection ──
                    if (_HoverProvinceIndex >= 0 && idx == _HoverProvinceIndex)
                    {
                        col *= 1.08;
                        col += float3(0.03, 0.02, 0.01); // warm shift
                    }

                    if (_SelectedProvinceIndex >= 0 && idx == _SelectedProvinceIndex)
                    {
                        bool selEdge = (idxL != _SelectedProvinceIndex) ||
                                       (idxR != _SelectedProvinceIndex) ||
                                       (idxU != _SelectedProvinceIndex) ||
                                       (idxD != _SelectedProvinceIndex);

                        if (selEdge)
                            col = lerp(col, _SelectionColor.rgb, 0.85);
                        else
                            col *= 1.06; // subtle interior highlight
                    }
                }

                // ════════════════════════════════════════
                // GLOBAL EFFECTS — applied to both ocean and land
                // ════════════════════════════════════════

                // Cloud overlay (fade out at high zoom)
                float cloudFade = 1.0 - saturate((_ZoomLevel - 4.0) / 4.0);
                if (cloudFade > 0.01 && _CloudStr > 0.01)
                {
                    float cloud1 = smoothNoise(uv * _CloudScale + _Time.y * float2(_CloudSpeed, _CloudSpeed * 0.7));
                    float cloud2 = smoothNoise(uv * _CloudScale * 2.3 + _Time.y * float2(-_CloudSpeed * 0.6, _CloudSpeed * 0.4) + float2(7,13));
                    float clouds = smoothstep(0.28, 0.58, cloud1 * 0.6 + cloud2 * 0.4);
                    float cloudMask = (idx > 0) ? 1.0 : 0.7;
                    // Dramatic clouds: whiter, more visible (like actual cloud banks)
                    float3 cloudCol = col * 0.70 + float3(0.18, 0.18, 0.20);
                    cloudCol = Desat(cloudCol, 0.08);
                    col = lerp(col, cloudCol, clouds * _CloudStr * cloudFade * cloudMask);
                }

                // Distance fog (atmospheric haze at viewport edges)
                if (_FogStr > 0.01)
                {
                    float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
                    float dist = length(screenUV - float2(0.5, 0.5)) * 2.0;
                    dist *= 0.7 + 0.3 * _ZoomLevel;
                    float fog = smoothstep(0.6, 1.3, dist) * _FogStr;
                    col = lerp(col, _FogColor.rgb, fog);
                }

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    // Fallback for built-in RP
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            ZWrite On
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _ProvinceTex;
            sampler2D _ColorLUT;
            sampler2D _TerrainTex;

            float _LUTWidth;
            float4 _ProvinceTex_TexelSize;

            float _HasTerrain;
            float _CountryColorStr;
            float4 _OceanDeep;

            int RGBToIndex(float3 c)
            {
                int idx = (int)(c.r * 255.0 + 0.5) * 65536
                        + (int)(c.g * 255.0 + 0.5) * 256
                        + (int)(c.b * 255.0 + 0.5);
                return min(idx, 65535); // clamp to LUT size
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 provCol = tex2D(_ProvinceTex, i.uv).rgb;
                int idx = RGBToIndex(provCol);

                if (idx == 0)
                    return fixed4(_OceanDeep.rgb, 1);

                float u = (fmod((float)idx, _LUTWidth) + 0.5) / _LUTWidth;
                float v = (floor((float)idx / _LUTWidth) + 0.5) / _LUTWidth;
                float3 countryCol = tex2Dlod(_ColorLUT, float4(u, v, 0, 0)).rgb;

                float3 terrain = float3(0.5, 0.5, 0.5);
                if (_HasTerrain > 0.5)
                    terrain = tex2D(_TerrainTex, i.uv).rgb;

                float3 col = terrain * lerp(float3(1,1,1), countryCol * 1.8, _CountryColorStr);
                return fixed4(saturate(col), 1);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
