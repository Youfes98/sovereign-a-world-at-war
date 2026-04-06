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

        [Toggle] _HasTerrain ("Has Terrain", Float) = 0
        [Toggle] _HasHeightmap ("Has Heightmap", Float) = 0
        [Toggle] _HasNoise ("Has Noise", Float) = 0
        [Toggle] _HasDetail ("Has Detail", Float) = 0
        [Toggle] _HasTerrainTypes ("Has Terrain Types", Float) = 0

        _CountryColorStr ("Country Color Strength", Range(0,1)) = 0.55
        _TerrainDesat ("Terrain Desat", Range(0,1)) = 0.10
        _ElevationStrength ("Elevation Relief", Float) = 12.0
        _NoiseStr ("Noise Strength", Float) = 0.012
        _DetailStrength ("Detail Strength", Float) = 0.08
        _BiomeDetailStrength ("Biome Detail", Float) = 0.30
        _CoastGlowStr ("Coast Glow", Float) = 0.22

        _OceanDeep ("Ocean Deep", Color) = (0.08, 0.16, 0.28, 1)
        _OceanMid ("Ocean Mid", Color) = (0.14, 0.28, 0.40, 1)
        _OceanShallow ("Ocean Shallow", Color) = (0.28, 0.40, 0.46, 1)
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

            CBUFFER_START(UnityPerMaterial)
                float _LUTWidth;
                float4 _ProvinceTex_TexelSize;
                float4 _HeightmapTex_TexelSize;

                float _HasTerrain, _HasHeightmap, _HasNoise, _HasDetail, _HasTerrainTypes;
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
                return lerp(col, col * palette, 0.30); // light touch — preserve terrain richness
            }

            // Procedural biome coloring from terrain_types + heightmap
            float3 BiomeColor(float terrainTypeRaw, float height, float2 uv)
            {
                // Curated palette per biome — rich, saturated, painterly
                float3 desert_lo  = float3(0.88, 0.72, 0.40); float3 desert_hi  = float3(0.80, 0.60, 0.32);
                float3 plains_lo  = float3(0.72, 0.72, 0.40); float3 plains_hi  = float3(0.62, 0.58, 0.35);
                float3 forest_lo  = float3(0.25, 0.52, 0.22); float3 forest_hi  = float3(0.18, 0.42, 0.15);
                float3 mount_lo   = float3(0.60, 0.52, 0.38); float3 mount_hi   = float3(0.50, 0.45, 0.35);
                float3 tundra_lo  = float3(0.72, 0.74, 0.72); float3 tundra_hi  = float3(0.65, 0.67, 0.66);
                float3 jungle_lo  = float3(0.18, 0.55, 0.18); float3 jungle_hi  = float3(0.14, 0.46, 0.12);

                int biome = clamp((int)(terrainTypeRaw * 255.0 + 0.5), 0, 5);
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

                // ── Terrain base (use terrainUV for parallax-shifted sampling) ──
                float3 terrain = float3(0.5, 0.5, 0.5);
                if (_HasTerrain > 0.5)
                    terrain = SAMPLE_TEXTURE2D(_TerrainTex, sampler_TerrainTex, terrainUV).rgb;

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

                    // Parallax relief: shift terrain UV based on height (zoom-gated)
                    float pomFade = saturate((_ZoomLevel - 4.0) / 6.0);
                    if (pomFade > 0.01)
                    {
                        // Multi-step parallax (4 iterations for smooth depth)
                        float2 pDir = float2(-0.4, 0.25) * hpx * 3.0 * pomFade;
                        float ph = height;
                        for (int pi = 0; pi < 4; pi++)
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

                    // Self-shadowing: trace toward sun in UV space (zoom-gated)
                    float shadowFade = saturate((_ZoomLevel - 2.0) / 3.0);
                    if (shadowFade > 0.01)
                    {
                        float2 sunUV = normalize(float2(-0.5, 0.3)) * hpx * 2.5;
                        float currentH = height;
                        selfShadow = 1.0;
                        for (int si = 1; si <= 8; si++)
                        {
                            float sH = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex,
                                terrainUV + sunUV * (float)si, 0).r;
                            float hDiff = sH - currentH - 0.005 * (float)si;
                            selfShadow = min(selfShadow, 1.0 - saturate(hDiff * 12.0));
                        }
                        selfShadow = lerp(1.0, selfShadow, 0.55 * shadowFade);
                    }
                }

                float3 col;

                if (idx == 0)
                {
                    // ════════════════════════════════════════
                    // OCEAN — realistic depth with visible seafloor
                    // ════════════════════════════════════════

                    float depth = 1.0 - height;

                    // Base water color gradient (shallow teal → deep navy)
                    float3 deepCol = _OceanDeep.rgb;
                    float3 midCol  = _OceanMid.rgb;
                    float3 shallowCol = _OceanShallow.rgb;
                    float depthT = saturate(depth * 1.5);
                    col = lerp(shallowCol, lerp(midCol, deepCol, saturate((depthT - 0.4) / 0.6)), depthT);

                    // Visible seafloor: blend terrain texture through water based on depth
                    // Shallow = see seafloor clearly, Deep = only water color
                    if (_HasTerrain > 0.5)
                    {
                        float3 seafloor = terrain * float3(0.6, 0.75, 0.72); // tinted by water color
                        float seafloorVisibility = (1.0 - smoothstep(0.0, 0.5, depthT)) * 0.45;
                        col = lerp(col, seafloor, seafloorVisibility);
                    }

                    // Underwater relief from heightmap
                    if (_HasHeightmap > 0.5)
                    {
                        float3 uwNormal = normalize(float3(-dx * 8.0, -dy * 8.0, 1.0));
                        float3 uwSun = normalize(float3(-0.4, 0.3, 0.8));
                        float uwDiffuse = max(dot(uwNormal, uwSun), 0.0);
                        float uwLighting = 0.85 + 0.15 * uwDiffuse;
                        col *= lerp(1.0, uwLighting, (1.0 - depthT) * 0.6);
                    }

                    // Multi-scale wave animation
                    float t = _Time.y * 0.08;
                    float wave1 = smoothNoise(uv * 25.0 + float2(t, t * 0.7));
                    float wave2 = smoothNoise(uv * 60.0 + float2(-t * 0.6, t * 0.4));
                    float wave3 = smoothNoise(uv * 120.0 + float2(t * 0.3, -t * 0.5));
                    float wavePattern = wave1 * 0.5 + wave2 * 0.3 + wave3 * 0.2;
                    float waveStr = lerp(0.012, 0.025, saturate(1.0 - depthT));
                    col += (wavePattern - 0.5) * waveStr;

                    // Specular highlight (sun reflection on water surface)
                    float specWave = smoothNoise(uv * 80.0 + float2(t * 0.5, t * 0.3));
                    float specular = pow(saturate(specWave * 1.2 - 0.1), 8.0) * 0.08;
                    specular *= (1.0 - depthT * 0.5); // stronger in shallow water
                    col += float3(specular, specular, specular * 0.9);

                    // Coast proximity detection (8 neighbors)
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

                    // Coastal shallow gradient with realistic teal tint
                    float coastGradient = saturate(1.0 - coastDist * 3.0);
                    coastGradient = coastGradient * coastGradient;
                    float3 nearShore = float3(0.18, 0.48, 0.52); // realistic teal near shore
                    col = lerp(col, nearShore, coastGradient * 0.35);

                    // Subtle coast brightening (not glow)
                    col = lerp(col, col * 1.15, coastDist * _CoastGlowStr);

                    // Coastal foam — natural white-water
                    float foamNoise = smoothNoise(uv * 40.0 + _Time.y * 0.1);
                    float foamLine = smoothstep(0.02, 0.10, coastDist) * (1.0 - smoothstep(0.10, 0.20, coastDist));
                    float foamFade = saturate((_ZoomLevel - 1.5) / 3.0);
                    float foam = foamLine * smoothstep(0.45, 0.7, foamNoise) * 0.25 * foamFade;
                    col = lerp(col, float3(0.75, 0.82, 0.85), foam);
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
                        float3 terrSat = lerp(float3(tLum,tLum,tLum), terrain, 1.8);
                        // Extra warmth push for sandy/desert terrain (where R > G > B)
                        float warmness = saturate((terrSat.r - terrSat.b) * 2.0);
                        terrSat *= lerp(float3(1,1,1), float3(1.08, 1.0, 0.88), warmness);
                        biomeBase = terrSat * 0.6 + biomeCol * 0.4;
                    }
                    else if (_HasTerrainTypes > 0.5)
                    {
                        float tt = SAMPLE_TEXTURE2D(_TerrainTypeTex, sampler_TerrainTypeTex, uv).r;
                        biomeBase = BiomeColor(tt, height, uv);
                    }
                    float terrLum = Lum(biomeBase);
                    float desatAmount = _TerrainDesat * zoomFade;
                    float3 terrBase = Desat(biomeBase, desatAmount);

                    // Step 2: Tint terrain with country color (fades with zoom)
                    float3 tinted = terrBase * lerp(float3(1,1,1), countryCol * 1.8, colorStr);

                    // Boost saturation in flat areas
                    float flatness = 1.0 - saturate(abs(relief) * 3.0);
                    tinted = lerp(tinted, countryCol * (terrLum * 0.6 + 0.5), colorStr * 0.45 * flatness);

                    col = tinted;

                    // Step 3: Multi-scale biome detail (more detail as you zoom IN)
                    if (_HasTerrainTypes > 0.5)
                    {
                        // Reuse cached terrain type if available, otherwise sample
                        float ttRaw = cachedTerrainType >= 0.0 ? cachedTerrainType :
                            SAMPLE_TEXTURE2D(_TerrainTypeTex, sampler_TerrainTypeTex, terrainUV).r;
                        float tt = ttRaw * 255.0;
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

                    // Brushstroke detail overlay (adds painterly texture at close zoom)
                    if (_HasDetail > 0.5)
                    {
                        float detZoom = saturate((_ZoomLevel - 2.0) / 6.0);
                        if (detZoom > 0.01)
                        {
                            float det1 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, terrainUV * 16.0).r;
                            float det2 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, terrainUV * 40.0 + 0.5).r;
                            float detMix = det1 * 0.6 + det2 * 0.4;
                            col *= lerp(1.0, 0.85 + 0.30 * detMix, _DetailStrength * 3.0 * detZoom);
                        }
                    }

                    // Step 4: Directional hillshading + AO + elevation color + snow
                    if (_HasHeightmap > 0.5)
                    {
                        // Reconstruct normal from heightmap gradient (dx/dy computed earlier)
                        float3 normal = normalize(float3(-dx * _ElevationStrength, -dy * _ElevationStrength, 1.0));
                        float3 sunDir = normalize(float3(-0.5, 0.3, 0.7)); // NW sun, dramatic angle
                        float diffuse = max(dot(normal, sunDir), 0.0);
                        float lighting = 0.55 + 0.45 * diffuse; // deeper shadows, brighter peaks
                        // Valley AO
                        float grad = sqrt(dx*dx + dy*dy);
                        float ao = saturate(1.0 - grad * _ElevationStrength * 0.8);
                        lighting *= lerp(0.85, 1.0, ao);
                        col *= clamp(lighting, 0.50, 1.40); // dramatic range for 3D depth
                        // Apply self-shadowing (mountains cast shadows on valleys)
                        col *= selfShadow;
                        // Elevation color shift: warm lowlands, slightly cool highlands
                        float3 lowTint  = float3(1.02, 0.98, 0.92); // warm boost
                        float3 highTint = float3(0.92, 0.92, 0.94); // barely cool
                        col *= lerp(lowTint, highTint, saturate(height * 0.4));
                        // Snow on mountain peaks
                        float snowMask = smoothstep(_SnowHeight, _SnowHeight + 0.12, height);
                        // Snow is brighter on sun-facing slopes
                        float snowLight = 0.85 + 0.15 * diffuse;
                        float3 snowColor = float3(0.92, 0.93, 0.95) * snowLight;
                        col = lerp(col, snowColor, snowMask * _SnowStr);

                        // Rivers: detect valley channels from heightmap gradient
                        float valleyDepth = saturate(0.3 - height) * 3.0;
                        float riverWidth = saturate(grad * _ElevationStrength - 0.15) * valleyDepth;
                        riverWidth *= smoothstep(0.0, 0.02, riverWidth); // sharpen edges
                        float3 riverColor = float3(0.15, 0.28, 0.40);
                        col = lerp(col, riverColor, saturate(riverWidth * 0.6));
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
                    col = lerp(col, col * float3(1.0, 0.97, 0.93), 0.6);

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
                                // Selected country: DRAMATICALLY brighter with golden warmth
                                col = col * (1.0 + 0.45 * _SelectionDarken);
                                col = lerp(col, col * float3(1.20, 1.10, 0.85), 0.6 * _SelectionDarken);
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
