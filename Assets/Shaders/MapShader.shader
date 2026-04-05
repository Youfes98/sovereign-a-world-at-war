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

        _CountryColorStr ("Country Color Strength", Range(0,1)) = 0.52
        _TerrainDesat ("Terrain Desat", Range(0,1)) = 0.35
        _ElevationStrength ("Elevation Relief", Float) = 12.0
        _NoiseStr ("Noise Strength", Float) = 0.025
        _DetailStrength ("Detail Strength", Float) = 0.08
        _BiomeDetailStrength ("Biome Detail", Float) = 0.3
        _CoastGlowStr ("Coast Glow", Float) = 0.18

        _OceanDeep ("Ocean Deep", Color) = (0.04, 0.08, 0.18, 1)
        _OceanMid ("Ocean Mid", Color) = (0.08, 0.16, 0.30, 1)
        _OceanShallow ("Ocean Shallow", Color) = (0.12, 0.24, 0.40, 1)
        _PaperTint ("Paper Tint", Color) = (0.97, 0.94, 0.88, 1)
        _PaperStrength ("Paper Strength", Float) = 0.12

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

                // ── Terrain base ──
                float3 terrain = float3(0.5, 0.5, 0.5);
                if (_HasTerrain > 0.5)
                    terrain = SAMPLE_TEXTURE2D(_TerrainTex, sampler_TerrainTex, uv).rgb;

                // ── Heightmap ──
                float height = 0.0;
                float relief = 0.0;
                if (_HasHeightmap > 0.5)
                {
                    height = SAMPLE_TEXTURE2D(_HeightmapTex, sampler_HeightmapTex, uv).r;
                    float hL = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, uv + float2(-hpx.x, 0), 0).r;
                    float hR = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, uv + float2( hpx.x, 0), 0).r;
                    float hU = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, uv + float2(0, -hpx.y), 0).r;
                    float hD = SAMPLE_TEXTURE2D_LOD(_HeightmapTex, sampler_HeightmapTex, uv + float2(0,  hpx.y), 0).r;
                    float dx = (hR - hL);
                    float dy = (hD - hU);
                    relief = (-dx + dy) * _ElevationStrength;
                }

                float3 col;

                if (idx == 0)
                {
                    // ════════════════════════════════════════
                    // OCEAN — animated depth rendering
                    // ════════════════════════════════════════

                    float depth = 1.0 - height;
                    col = lerp(_OceanShallow.rgb, _OceanDeep.rgb, saturate(depth * 1.5));

                    // Terrain-based continental shelf detail
                    if (_HasTerrain > 0.5)
                    {
                        float terrLum = Lum(terrain);
                        col = lerp(col, _OceanMid.rgb, terrLum * 0.25);
                    }

                    // Underwater relief
                    col *= clamp(1.0 + relief * 0.15, 0.9, 1.1);

                    // Animated ocean waves (smooth value noise, no grid artifacts)
                    float t = _Time.y * 0.08;
                    float2 waveUV1 = uv * 80.0 + float2(t, t * 0.7);
                    float2 waveUV2 = uv * 120.0 + float2(-t * 0.6, t * 0.4);
                    // Smooth interpolated noise instead of floor() hash
                    float2 f1 = frac(waveUV1); float2 i1 = floor(waveUV1);
                    float2 s1 = f1 * f1 * (3.0 - 2.0 * f1); // smoothstep
                    float wave1 = lerp(lerp(hash21(i1), hash21(i1 + float2(1,0)), s1.x),
                                       lerp(hash21(i1 + float2(0,1)), hash21(i1 + float2(1,1)), s1.x), s1.y);
                    float2 f2 = frac(waveUV2); float2 i2 = floor(waveUV2);
                    float2 s2 = f2 * f2 * (3.0 - 2.0 * f2);
                    float wave2 = lerp(lerp(hash21(i2), hash21(i2 + float2(1,0)), s2.x),
                                       lerp(hash21(i2 + float2(0,1)), hash21(i2 + float2(1,1)), s2.x), s2.y);
                    float wavePattern = (wave1 + wave2) * 0.5;
                    float waveStr = lerp(0.012, 0.025, saturate(1.0 - depth));
                    col += (wavePattern - 0.5) * waveStr;

                    // Coast glow — 3×3 neighbor check (8 samples instead of 24)
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

                    // Warm coast glow with slight color shift
                    float3 coastColor = lerp(_OceanShallow.rgb, float3(0.18, 0.30, 0.42), 0.3);
                    col = lerp(col, coastColor, coastDist * _CoastGlowStr * 2.0);

                    // Noise for ocean texture
                    if (_HasNoise > 0.5)
                        col += (SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 2.0).r - 0.5) * _NoiseStr * 0.3;
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

                    // Step 1: Start with terrain as base
                    float terrLum = Lum(terrain);
                    float desatAmount = _TerrainDesat * zoomFade;
                    float3 terrBase = Desat(terrain, desatAmount);

                    // Step 2: Tint terrain with country color (fades with zoom)
                    float3 tinted = terrBase * lerp(float3(1,1,1), countryCol * 1.8, colorStr);

                    // Boost saturation in flat areas
                    float flatness = 1.0 - saturate(abs(relief) * 3.0);
                    tinted = lerp(tinted, countryCol * (terrLum * 0.6 + 0.5), colorStr * 0.45 * flatness);

                    col = tinted;

                    // Step 3: Biome detail
                    if (_HasTerrainTypes > 0.5)
                    {
                        float zf = saturate(1.0 - px.x * 2048.0);
                        if (zf > 0.01)
                        {
                            float tt = SAMPLE_TEXTURE2D(_TerrainTypeTex, sampler_TerrainTypeTex, uv).r * 255.0;
                            int bi = clamp((int)(tt + 0.5), 0, 5);
                            float ac = (float)(bi % 3);
                            float ar = (float)(bi / 3);
                            float2 buv = float2((ac + frac(uv.x * 12.0)) / 3.0, (ar + frac(uv.y * 12.0)) / 2.0);
                            float3 bc = SAMPLE_TEXTURE2D(_BiomeAtlas, sampler_BiomeAtlas, buv).rgb;
                            col = lerp(col, col * bc, _BiomeDetailStrength * zf);
                        }
                    }

                    // Step 4: Elevation relief
                    if (_HasHeightmap > 0.5)
                    {
                        col *= clamp(1.0 + relief, 0.6, 1.4);
                        col *= lerp(1.0, 0.85, saturate((height - 0.5) * 2.0));
                    }

                    // Step 5: Micro-detail noise
                    if (_HasNoise > 0.5)
                    {
                        float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 3.0).r;
                        float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 12.0).r;
                        col += (n1 - 0.5) * _NoiseStr;
                        col += (n2 - 0.5) * _NoiseStr * 0.4;
                    }

                    if (_HasDetail > 0.5)
                        col += (SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, uv * 24.0).r - 0.5) * _DetailStrength;

                    // Step 6: Paper/parchment tint
                    float paperStr = _PaperStrength * zoomFade;
                    col = Desat(col, paperStr * 0.3);
                    col = lerp(col, col * _PaperTint.rgb, paperStr);

                    // Step 7: Coast darkening
                    bool nearCoast = (idxL == 0 || idxR == 0 || idxU == 0 || idxD == 0);
                    if (nearCoast)
                        col *= 0.82;

                    // Step 8: Anti-alias province boundaries
                    if (edgeFactor > 0.01 && nCount > 0)
                    {
                        float3 blendedNeighbor = neighborBlend;
                        blendedNeighbor = terrBase * lerp(float3(1,1,1), blendedNeighbor * 1.8, _CountryColorStr);
                        col = lerp(col, blendedNeighbor, edgeFactor * 0.35);
                    }

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
                        col = lerp(col, float3(0.02, 0.02, 0.03), 0.9 * countryEdgeW * countryBorderFade);

                    if (provEdgeW > 0.01 && provBorderFade > 0.01)
                        col = lerp(col, col * 0.75, 0.3 * provEdgeW * provBorderFade);

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
                                // Selected country: brighten slightly
                                col = col * (1.0 + 0.12 * _SelectionDarken);
                                // Add subtle warm tint
                                col = lerp(col, col * float3(1.05, 1.02, 0.95), 0.3 * _SelectionDarken);
                            }
                            else
                            {
                                // Other countries: darken
                                col *= lerp(1.0, 0.45, _SelectionDarken);
                                // Slight desaturation for darkened areas
                                float lum = dot(col, float3(0.299, 0.587, 0.114));
                                col = lerp(col, float3(lum, lum, lum), 0.3 * _SelectionDarken);
                            }
                        }
                    }

                    // ── Interactive: Hover & Selection ──
                    if (_HoverProvinceIndex >= 0 && idx == _HoverProvinceIndex)
                    {
                        // Subtle brightening with slight desaturation for hover
                        col = lerp(col, col * 1.25 + float3(0.03, 0.03, 0.04), 0.45);
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
                            col *= 1.1;
                    }
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
