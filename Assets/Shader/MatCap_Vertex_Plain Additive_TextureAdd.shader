Shader "MatCap/Vertex/Plain Additive_TextureAdd" {
	Properties {
		_Color ("Main Color", Vector) = (0.5,0.5,0.5,1)
		_MatCap ("MatCap (RGB)", 2D) = "white" {}
		_MainTex ("Additional Texture", 2D) = "white" {}
		_AddColor ("Texture Color", Vector) = (1,1,1,1)
		_PowVal ("MatCap Contrast", Range(0, 10)) = 1
		_BlurLevelMatCap ("Blur Level MatCap", Range(0, 10)) = 0
		[Toggle] _Is_Ortho ("Is Orthography", Float) = 1
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
	Fallback "VertexLit"
}