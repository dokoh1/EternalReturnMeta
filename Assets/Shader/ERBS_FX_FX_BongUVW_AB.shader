Shader "ERBS_FX/FX_BongUVW_AB" {
	Properties {
		_G_tex ("G_tex", 2D) = "white" {}
		_R_tex ("R_tex", 2D) = "white" {}
		_value ("value", Float) = 2
		_A_Speed ("A_Speed", Float) = 0.3
		_B_Speed ("B_Speed", Float) = 0
		_C_Speed ("C_Speed", Float) = 0
		_D_Speed ("D_Speed", Float) = 0.3
		_Color ("Color", Vector) = (0.5,0.5,0.5,1)
		_B_tex ("B_tex", 2D) = "white" {}
		_stvalue ("st value", Range(0, 5)) = 0
		_RotateSpeed ("Rotate Speed", Range(-5, 5)) = 0
		[Space(30)] [Enum(LESS,0,GREATER,1,LEQUAL,2,GEQUAL,3,EQUAL,4,NOTEQUAL,5,ALWAYS,6)] _ZTestMode ("ZTest Mode", Float) = 2
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = _Color.rgb;
			o.Alpha = _Color.a;
		}
		ENDCG
	}
}