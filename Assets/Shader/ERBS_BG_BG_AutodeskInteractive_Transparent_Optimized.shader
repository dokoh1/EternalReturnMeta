Shader "ERBS_BG/BG_AutodeskInteractive_Transparent_Optimized" {
	Properties {
		_Color ("Color", Vector) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Cutout ("Cutout Value", Range(0, 1)) = 0.5
		[NoScaleOffset] _MaskTex ("Merged Values Texture", 2D) = "white" {}
		_Metallic ("Metallic", Range(0, 1)) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 1
		_BumpScale ("Normal Scale", Float) = 1
		[NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
		_OcclusionStrength ("Occlusion Strength", Float) = 1
		_EmissiveValue ("EmissiveValue", Float) = 0
		[HDR] _EmissionColor ("EmissionColor", Vector) = (0,0,0,0)
		[Toggle(_ISSANDDECAL)] _IsSandDecal ("Is Sand Decal?", Float) = 0
		[Header(Forward Rendering Options)] [ToggleOff] _SpecularHighlights ("Specular Highlights", Float) = 1
		[ToggleOff] _GlossyReflections ("Reflections", Float) = 1
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
	Fallback "Standard"
}