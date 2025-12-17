// Ray と Plane の交点距離 t を返すユーティリティ。
// Custom HLSL Operator では out が使えないため、戻り値のみで距離を返し、
// ヒットしない場合は -1.0 を返す。位置は呼び出し側で rayOrigin + rayDirection * t で算出可。
// rayDirection/planeNormal は非正規化でも使用可（planeNormal は内部で正規化）。
#ifndef RAY_PLANE_INTERSECTION_INCLUDED
#define RAY_PLANE_INTERSECTION_INCLUDED

float RayPlaneIntersection(
    float3 rayOrigin,
    float3 rayDirection,
    float3 planePosition,
    float3 planeNormal)
{
    const float kEpsilon = 1e-5;
    float3 n = normalize(planeNormal);
    float denom = dot(rayDirection, n);

    // ほぼ平行ならヒットなし
    if (abs(denom) < kEpsilon)
    {
        return -1.0;
    }

    float t = dot(planePosition - rayOrigin, n) / denom;

    // 平面の裏側（t < 0）はヒットなしとする
    if (t < 0.0)
    {
        return -1.0;
    }

    return t;
}

// direction を normal で鏡面反射させたベクトルを返す。
// direction の長さは維持し、normal は正規化して使用。
float3 ReflectDirection(float3 direction, float3 normal)
{
    float3 n = normalize(normal);
    return direction - 2.0 * dot(direction, n) * n;
}

#endif // RAY_PLANE_INTERSECTION_INCLUDED
