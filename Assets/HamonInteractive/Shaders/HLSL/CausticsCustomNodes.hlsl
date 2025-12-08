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

// ---------------------------------------------------------------------------
// 解析ヤコビアンでレイの足跡面積を算出（フラグメントステージ想定）
// rayOriginWS, rayDirWS はフラグメントで計算されたものを渡すと精度が高い
// ---------------------------------------------------------------------------
void CausticsRayFootprint_float(
    float3 rayOriginWS,
    float3 rayDirWS,
    float3 planePositionWS,
    float3 planeNormalWS,
    float minArea,
    out float3 hitPosWS,
    out float2 hitUV,
    out float hitMask,
    out float area)
{
    const float kRayStartOffset = 1e-3;
    const float kEpsilonDet = 1e-5;

    float3 dir = normalize(rayDirWS);
    float3 origin = rayOriginWS + dir * kRayStartOffset;

    // 交点計算
    float t = RayPlaneIntersection(origin, dir, planePositionWS, planeNormalWS);
    if (t <= 0.0)
    {
        hitPosWS = 0.0;
        hitUV = 0.0;
        hitMask = 0.0;
        area = max(minArea, kEpsilonDet);
        return;
    }

    hitPosWS = origin + dir * (t + kRayStartOffset);

    float3 tangent, bitangent;
    BuildPlaneFrame_float(planeNormalWS, tangent, bitangent);
    float3 delta = hitPosWS - planePositionWS;
    hitUV = float2(dot(delta, tangent), dot(delta, bitangent));
    hitMask = 1.0;

    // ヤコビアンを解析的に計算
    float3 dOdx = ddx(rayOriginWS);
    float3 dOdy = ddy(rayOriginWS);
    float3 dDdx = ddx(dir);
    float3 dDdy = ddy(dir);

    // origin にオフセットを足しているので微分も補正
    dOdx += dDdx * kRayStartOffset;
    dOdy += dDdy * kRayStartOffset;

    float denom = dot(dir, planeNormalWS);
    float numer = dot(planePositionWS - origin, planeNormalWS);
    float denomSq = max(denom * denom, 1e-8);

    float dn_dx = -dot(dOdx, planeNormalWS);
    float dn_dy = -dot(dOdy, planeNormalWS);
    float dd_dx = dot(dDdx, planeNormalWS);
    float dd_dy = dot(dDdy, planeNormalWS);

    float dt_dx = (dn_dx * denom - numer * dd_dx) / denomSq;
    float dt_dy = (dn_dy * denom - numer * dd_dy) / denomSq;

    float3 dDeltaDx = dOdx + dir * dt_dx + t * dDdx;
    float3 dDeltaDy = dOdy + dir * dt_dy + t * dDdy;

    float du_dx = dot(dDeltaDx, tangent);
    float dv_dx = dot(dDeltaDx, bitangent);
    float du_dy = dot(dDeltaDy, tangent);
    float dv_dy = dot(dDeltaDy, bitangent);

    float det = du_dx * dv_dy - du_dy * dv_dx;
    area = max(abs(det), max(minArea, kEpsilonDet));
}

#endif // CAUSTICS_CUSTOM_NODES_INCLUDED
