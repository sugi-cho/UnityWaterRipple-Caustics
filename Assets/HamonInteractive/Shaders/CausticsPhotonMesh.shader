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
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Blend One One
        ZWrite Off
        ZTest Always
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

            struct appdata
            {
                float3 pos : POSITION; // uv is stored here (0..1)
                uint vid  : SV_VertexID;
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
                uint x = v.vid % (uint)_Resolution.x;
                uint y = v.vid / (uint)_Resolution.x;
                float2 uv = (float2(x, y) + 0.5) / _Resolution;
                o.uv = uv;
                o.intensity = (float)_Density[v.vid] * _InvDensityScale;
                o.posCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                if (i.intensity <= 0) discard;
                float3 c = _ColorTint.rgb * i.intensity;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
