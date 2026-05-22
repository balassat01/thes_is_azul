Shader "Custom/SpriteGradientBetweenPoints"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Gradient ("Gradient Texture", 2D) = "white" {}

        _PointA ("Point A (Local XY)", Vector) = ( -0.5, -0.5, 0, 0 )
        _PointB ("Point B (Local XY)", Vector) = ( 0.5, 0.5, 0, 0 )
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _Gradient;

            float4 _PointA;
            float4 _PointB;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float2 local : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // local sprite position (XY only)
                o.local = v.vertex.xy;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 sprite = tex2D(_MainTex, i.uv);

                float2 A = _PointA.xy;
                float2 B = _PointB.xy;
                float2 P = i.local;

                // Direction vector from A to B
                float2 AB = B - A;
                float2 AP = P - A;

                // Project AP onto AB → gives param t
                float t = dot(AP, AB) / dot(AB, AB);

                // Clamp to valid range
                t = saturate(t);

                // Sample gradient texture along U axis
                float4 grad = tex2D(_Gradient, float2(t, 0.5));

                return sprite * grad;
            }
            ENDCG
        }
    }
}
