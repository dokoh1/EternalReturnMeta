Shader "ERBS_FX/FX_Shockwave01_03" {
	Properties {
		_GradientControl ("GradientControl", Vector) = (1,1,0,0)
		_GradientTex ("GradientTex", 2D) = "white" {}
		_GradientMask ("GradientMask", 2D) = "white" {}
		_NoiseTex ("NoiseTex", 2D) = "white" {}
		_Noise2Tex ("Noise2Tex", 2D) = "white" {}
		_NoiseIntensity ("Noise Intensity", Float) = 0
		_Noise2Intensity ("Noise2 Intensity", Float) = 0
		_NoiseUV ("Noise UV", Vector) = (1,1,0,0)
		_Noise2UV ("Noise2 UV", Vector) = (1,1,0,0)
		_LenMutiply ("LenMutiply", Vector) = (1,1,1,0.5)
		_UseCustomOffet ("Use Custom Offet", Float) = 0
		_TileTex ("TileTex", 2D) = "white" {}
		_TileControl ("Tile Control", Vector) = (1,0,0,0)
		_TileUV ("Tile UV", Vector) = (1,1,0,0)
		[Space(20] [Enum(LESS,0,GREATER,1,LEQUAL,2,GEQUAL,3,EQUAL,4,NOTEQUAL,5,ALWAYS,6)] _ZTestMode ("ZTest Mode", Float) = 2
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
		[Toggle] _ZWrite ("ZWrite Mode", Float) = 0
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