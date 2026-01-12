Shader "ERBS_FX/FX_Trail_AB" {
	Properties {
		_Texture0 ("Texture 0", 2D) = "white" {}
		[Toggle(_KEYWORD0_ON)] _Keyword0 ("Keyword 0", Float) = 1
		_emmision ("emmision", Float) = 4
		_Timescale ("Timescale", Float) = 1
		_ScrollSpeed ("Scroll Speed Detail", Vector) = (-1.3,-0.8,-2,1)
		_Colorstart ("Colorstart", Vector) = (0.4198113,0.5631521,1,0)
		_Colorend ("Colorend", Vector) = (1,0.4764151,0.7461407,0)
		[Space(30)] [Enum(LESS,0,GREATER,1,LEQUAL,2,GEQUAL,3,EQUAL,4,NOTEQUAL,5,ALWAYS,6)] _ZTestMode ("ZTest Mode", Float) = 2
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		struct Input
		{
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = 1;
		}
		ENDCG
	}
}