Shader "ERBS_FX/FX_MaskFlow_AB_Time" {
	Properties {
		_Color ("Color", Vector) = (0.5,0.5,0.5,1)
		_MainTex ("MainTex", 2D) = "white" {}
		_MaskTex ("MaskTex", 2D) = "white" {}
		_MulVal_rgb ("RGB channel multiplier", Float) = 5
		_MulVal_alpha ("Alpha channel multiplier", Float) = 3
		[Space(10)] _ScrollSpeed ("Texture Scrolling Speed", Vector) = (1,1,-1,-1)
		[Space(20] [Enum(LESS,0,GREATER,1,LEQUAL,2,GEQUAL,3,EQUAL,4,NOTEQUAL,5,ALWAYS,6)] _ZTestMode ("ZTest Mode", Float) = 2
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
}