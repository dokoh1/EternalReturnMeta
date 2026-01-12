//Shader "UI/Additive" {
//	Properties {
//		_MainTex ("Sprite Texture", 2D) = "white" {}
//		_Color ("Tint", Vector) = (1,1,1,1)
//		_StencilComp ("Stencil Comparison", Float) = 8
//		_Stencil ("Stencil ID", Float) = 0
//		_StencilOp ("Stencil Operation", Float) = 0
//		_StencilWriteMask ("Stencil Write Mask", Float) = 255
//		_StencilReadMask ("Stencil Read Mask", Float) = 255
//		_ColorMask ("Color Mask", Float) = 15
//		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
//	}
//	//DummyShaderTextExporter
//	SubShader{
//		Tags { "RenderType"="Opaque" }
//		LOD 200
//
//		Pass
//		{
//			HLSLPROGRAM
//			#pragma vertex vert
//			#pragma fragment frag
//
//			float4x4 unity_MatrixMVP;
//
//			struct Vertex_Stage_Input
//			{
//				float3 pos : POSITION;
//			};
//
//			struct Vertex_Stage_Output
//			{
//				float4 pos : SV_POSITION;
//			};
//
//			Vertex_Stage_Output vert(Vertex_Stage_Input input)
//			{
//				Vertex_Stage_Output output;
//				output.pos = mul(unity_MatrixMVP, float4(input.pos, 1.0));
//				return output;
//			}
//
//			Texture2D<float4> _MainTex;
//			SamplerState _MainTex_sampler;
//			fixed4 _Color;
//
//			struct Fragment_Stage_Input
//			{
//				float2 uv : TEXCOORD0;
//			};
//
//			float4 frag(Fragment_Stage_Input input) : SV_TARGET
//			{
//				return _MainTex.Sample(_MainTex_sampler, float2(input.uv.x, input.uv.y)) * _Color;
//			}
//
//			ENDHLSL
//		}
//	}
//}
Shader "UI/CustomAdditiveGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, IN.texcoord) * _Color;
                return col;
            }
            ENDCG
        }
    }
}
