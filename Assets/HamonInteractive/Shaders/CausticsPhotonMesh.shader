Shader "Hidden/Hamon/CausticsPhotonMesh"
{
    Properties
    {
        _ColorTint("Color Tint", Color) = (1,1,1,1)
        _InvDensityScale("Inv Density Scale", Float) = 0.00024414
        _Resolution("Resolution", Vector) = (256,256,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            StructuredBuffer<uint> _Density;
            float _InvDensityScale;
            float4 _ColorTint;
            float2 _Resolution;

            float3 _TargetPos;
            float3 _TargetRight;
            float3 _TargetUp;
            float3 _TargetNormal;
            float2 _TargetScale;

            struct appdata
            {
                float3 pos : POSITION; // uv is stored here (0..1)
                float2 uv  : TEXCOORD0;
            };

            struct v2f
            {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float intensity : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float2 uv = v.uv; // stored as position
                float2 local = (uv - 0.5) * _TargetScale;
                float3 worldPos = _TargetPos + _TargetRight * local.x + _TargetUp * local.y + _TargetNormal * 0.0001; // slight offset to avoid z-fight
                o.posCS = UnityWorldToClipPos(worldPos);
                o.uv = uv;

                // 頂点に接する最大4セルの密度を平均し、セル面積でスケーリング
                float2 coord = uv * _Resolution;                // 0..Res
                int2 baseCell = int2(floor(coord));             // 下側のセル
                int2 resInt = int2(_Resolution);

                float sum = 0.0;
                float count = 0.0;
                [unroll] for (int oy = -1; oy <= 0; oy++)
                {
                    [unroll] for (int ox = -1; ox <= 0; ox++)
                    {
                        int2 c = baseCell + int2(ox, oy);
                        if (c.x >= 0 && c.y >= 0 && c.x < resInt.x && c.y < resInt.y)
                        {
                            uint idx = (uint)(c.y * resInt.x + c.x);
                            sum += (float)_Density[idx] * _InvDensityScale;
                            count += 1.0;
                        }
                    }
                }

                float3 stepR = _TargetRight * (_TargetScale.x / _Resolution.x);
                float3 stepU = _TargetUp * (_TargetScale.y / _Resolution.y);
                float cellArea = length(cross(stepR, stepU));

                o.intensity = (count > 0.0) ? (sum / count) * cellArea : 0.0;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float intensity = max(i.intensity, 0.0);
                if (intensity <= 0.0) discard;
                float3 c = _ColorTint.rgb * 0.1;
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
