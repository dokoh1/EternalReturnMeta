Shader "ERBS_FX/FX_Dissolve_04" {
	Properties {
		_noise ("noise", 2D) = "white" {}
		_mask ("mask", 2D) = "white" {}
		_main ("main", 2D) = "white" {}
		_time_Spped_x ("time_Spped_x", Float) = -0.5
		_time_Spped_y ("time_Spped_y", Float) = 0
		_voronoi_scale ("voronoi_scale", Vector) = (0.5,0.5,0,0)
		_power ("power", Float) = 2
		_step ("step", Float) = 0.54
		[Toggle] _alphaDissolve_swich ("alphaDissolve_swich", Float) = 1
		[Toggle] _Dissolve_swich ("Dissolve_swich", Float) = 1
		_int ("int", Float) = 1
		_mask_UV ("mask_UV", Vector) = (1,1,0,0)
		[Space(20)] [Enum(LESS,0,GREATER,1,LEQUAL,2,GEQUAL,3,EQUAL,4,NOTEQUAL,5,ALWAYS,6)] _ZTestMode ("ZTest Mode", Float) = 2
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
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