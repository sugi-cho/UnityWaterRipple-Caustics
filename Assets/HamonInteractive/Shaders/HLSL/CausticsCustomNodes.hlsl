// UNITY_SHADER_NO_UPGRADE
#ifndef CAUSTICS_CUSTOM_NODES_INCLUDED
#define CAUSTICS_CUSTOM_NODES_INCLUDED

#include "../../VFX/HLSL/RayPlaneIntersection.hlsl"

// ---------------------------------------------------------------------------
// 共通ヘルパ
// ---------------------------------------------------------------------------
inline void BuildPlaneFrame_float(float3 planeNormal, out float3 tangent, out float3 bitangent)
{
    float3 n = normalize(planeNormal);
    float3 helper = (abs(n.z) < 0.999) ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    tangent = normalize(cross(helper, n));
    bitangent = cross(n, tangent);
}

// ---------------------------------------------------------------------------
// 任意の Ray を受光平面に投げ交点と平面ローカルUVを返す（頂点ステージ想定）
// ---------------------------------------------------------------------------
void CausticsRayToPlane_float(
    float3 rayOriginWS,
    float3 rayDirWS,
    float3 planePositionWS,
    float3 planeNormalWS,
    out float3 hitPosWS,
    out float2 hitUV,
    out float hitMask)
{
    const float kRayStartOffset = 1e-3;
    float3 dir = normalize(rayDirWS);

    float t = RayPlaneIntersection(rayOriginWS + dir * kRayStartOffset, dir, planePositionWS, planeNormalWS);
    if (t > 0.0)
    {
        hitPosWS = rayOriginWS + dir * (t + kRayStartOffset);

        float3 tangent, bitangent;
        BuildPlaneFrame_float(planeNormalWS, tangent, bitangent);
        float3 delta = hitPosWS - planePositionWS;
        hitUV = float2(dot(delta, tangent), dot(delta, bitangent));
        hitMask = 1.0;
    }
    else
    {
        hitPosWS = 0.0;
        hitUV = 0.0;
        hitMask = 0.0;
    }
}

// ---------------------------------------------------------------------------
// UV 微分→密度（フラグメントステージ想定）
// intensity: 任意スケール、minDeterminant: 足跡面積の下限
// ---------------------------------------------------------------------------
void CausticsDensityFromRUV_float(
    float2 rUV,
    float hitMask,
    float intensity,
    float minDeterminant,
    out float density)
{
    const float kEpsilonDet = 1e-4;
    float2 du = ddx(rUV);
    float2 dv = ddy(rUV);
    float det = du.x * dv.y - du.y * dv.x;
    float area = max(abs(det), max(minDeterminant, kEpsilonDet));
    density = hitMask * intensity / area;
}

#endif // CAUSTICS_CUSTOM_NODES_INCLUDED
