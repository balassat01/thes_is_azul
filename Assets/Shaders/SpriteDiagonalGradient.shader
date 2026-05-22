Shader "Custom/SpriteDiagonalGradient"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Gradient ("Gradient Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType"="Transparent"
        }

        Cull Off
        ZWrite Off
        Lighting Off

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _Gradient;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float2 luv : TEXCOORD1; // local UV for gradient
            };

            v2f vert (appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Gradient uses 0–1 UVs directly
                o.luv = v.uv;

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 sprite = tex2D(_MainTex, i.uv);

                // Diagonal projection: UL → LR
                //
                // UV.y decreases upward, so UL corner is (0,1).
                // To fix this, invert Y so UL becomes (0,0).
                float2 uv = float2(i.luv.x, 1.0 - i.luv.y);

                // Project onto diagonal direction vector (1,1)
                float t = dot(uv, float2(1, 1)) / 2.0;

                t = saturate(t);

                float4 grad = tex2D(_Gradient, float2(t, 0.5));

                return sprite * grad;
            }
            ENDCG
        }
    }
}
