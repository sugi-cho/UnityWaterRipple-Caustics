// Caustics Mesh VFX helper functions
// - ParticleID とグリッドサイズ (W,H) から PositionIntensityTexture のピクセル座標を求める
// - Custom HLSL Block で PositionIntensityTexture へ位置と強度を書き込む

#ifndef HAMON_CausticsMeshVfxHelpers_INCLUDED
#define HAMON_CausticsMeshVfxHelpers_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

// グリッドサイズは頂点分割数 (W,H)。テクスチャ解像度は (W+1, H+1)。
uint2 Caustics_GetPixelCoord(uint particleId, uint2 gridSize)
{
    uint width = gridSize.x + 1;
    uint2 coord;
    coord.x = particleId % width;
    coord.y = particleId / width;
    return coord;
}

bool Caustics_IsCoordInside(uint2 coord, uint2 gridSize)
{
    uint2 texSize = gridSize + uint2(1, 1);
    return coord.x < texSize.x && coord.y < texSize.y;
}

// VFX の float2 プロパティからグリッドを得るラッパー（安全に int 化）
uint2 Caustics_GridFromFloat2(float2 gridSizeF)
{
    int gx = (int)max(1.0, floor(gridSizeF.x + 0.5));
    int gy = (int)max(1.0, floor(gridSizeF.y + 0.5));
    return uint2((uint)gx, (uint)gy);
}

// Custom HLSL Block 用エントリ:
// attributes    : VFXAttributes (必須先頭)
// positionRT    : PositionIntensityTexture (RWTexture2D<float4>)
// gridSizeF     : メッシュ分割数 (float2, VFX プロパティで受ける)
// intensity     : 書き込む強度
// positionWS    : 書き込むワールド座標
void Caustics_WritePositionIntensity(
    inout VFXAttributes attributes,
    RWTexture2D<float4> positionRT,
    float2 gridSizeF,
    float intensity,
    float3 positionWS)
{
    uint2 gridSize = Caustics_GridFromFloat2(gridSizeF);
    uint2 coord = Caustics_GetPixelCoord((uint)attributes.particleId, gridSize);
    if (!Caustics_IsCoordInside(coord, gridSize)) return;

    positionRT[coord] = float4(positionWS, intensity);
}

// Custom HLSL Operator 用: PositionIntensityTexture から隣接頂点の面積を計算し密度(=1/面積平均)を返す
// - particleId: 入力 Particle ID
// - positionRT: PositionIntensityTexture (RGBA: pos.xyz, intensity)
// 戻り値: intensity (面積が小さいほど大きな値)
float Caustics_ComputeIntensity(uint particleId, Texture2D<float4> positionRT)
{
    uint width, height;
    positionRT.GetDimensions(width, height);
    if (width < 2 || height < 2) return 0.0; // 最低2x2が必要

    uint2 coord = uint2(particleId % width, particleId / width);

    float areaSum = 0.0;
    float quadCount = 0.0;

    // 頂点を共有する最大4つのセルを走査（左下起点のセル）
    [unroll] for (int oy = -1; oy <= 0; oy++)
    {
        [unroll] for (int ox = -1; ox <= 0; ox++)
        {
            int2 base = int2(coord) + int2(ox, oy);
            if (base.x < 0 || base.y < 0) continue;
            if (base.x + 1 >= (int)width || base.y + 1 >= (int)height) continue;

            float3 p00 = positionRT.Load(int3(base, 0)).xyz;
            float3 p10 = positionRT.Load(int3(base + int2(1, 0), 0)).xyz;
            float3 p01 = positionRT.Load(int3(base + int2(0, 1), 0)).xyz;
            float3 p11 = positionRT.Load(int3(base + int2(1, 1), 0)).xyz;

            float3 e0 = p10 - p00;
            float3 e1 = p01 - p00;
            float3 e2 = p11 - p10;
            float3 e3 = p11 - p01;

            float area = 0.5 * (length(cross(e0, e1)) + length(cross(e2, e3)));
            areaSum += area;
            quadCount += 1.0;
        }
    }

    if (quadCount < 1.0) return 0.0;

    float areaAvg = areaSum / quadCount;
    return (areaAvg > 1e-6) ? rcp(areaAvg) : 0.0;
}

#endif // HAMON_CausticsMeshVfxHelpers_INCLUDED


