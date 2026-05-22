Shader "Custom/SpriteGradient360"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _ColorA ("Start Color", Color) = (1,1,1,1)
        _ColorB ("End Color", Color) = (0,0,0,1)
        _Angle ("Angle (Degrees)", Range(0,360)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _ColorA;
            float4 _ColorB;
            float _Angle;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Convert object-space position to a centered coordinate (−0.5 to +0.5)
                o.localPos = v.vertex.xy;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 sprite = tex2D(_MainTex, i.uv);

                // Angle in radians
                float rad = radians(_Angle);

                // Gradient direction vector
                float2 dir = float2(cos(rad), sin(rad));

                // Project UV onto direction vector for gradient interpolation
                float t = dot(i.localPos, dir);

                // Normalize t to 0–1
                t = (t + 0.5);   // Because coordinates are roughly -0.5..0.5
                t = saturate(t);

                float4 grad = lerp(_ColorA, _ColorB, t);

                return sprite * grad;
            }
            ENDCG
        }
    }
}
