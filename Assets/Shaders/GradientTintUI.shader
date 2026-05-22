Shader "Custom/GradientTintUI"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Gradient ("Gradient", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 guv : TEXCOORD1; };

            sampler2D _MainTex;
            sampler2D _Gradient;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // use x UV for the gradient
                o.guv = float2(v.uv.x, 0.5);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 sprite = tex2D(_MainTex, i.uv);
                float4 grad = tex2D(_Gradient, i.guv);
                return sprite * grad;
            }
            ENDCG
        }
    }
}
