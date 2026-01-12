Shader "ERBS_FX/FX_MeshTrail" {
	Properties {
		_Glow ("Glow", Float) = 0
		_Noise ("Noise Strength", Float) = 0
		_Noise01 ("Noise01", 2D) = "white" {}
		_Noise01UV_TilingSpeed ("Noise01 UV_Tiling & Speed", Vector) = (0,0,0,0)
		_Noise02 ("Noise02", 2D) = "white" {}
		_Noise02UV_TilingSpeed ("Noise02 UV_Tiling & Speed", Vector) = (0,0,0,0)
		_MainTex ("MainTex", 2D) = "white" {}
		_MainTexUV_TilingSpeed ("MainTex UV_Tiling & Speed", Vector) = (0,0,0,0)
		_MaskTex ("MaskTex", 2D) = "white" {}
		[Space(20)] [Enum(LESS,0,GREATER,1,LEQUAL,2,GEQUAL,3,EQUAL,4,NOTEQUAL,5,ALWAYS,6)] _ZTestMode ("ZTest Mode", Float) = 2
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
		struct Input
		{
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
}