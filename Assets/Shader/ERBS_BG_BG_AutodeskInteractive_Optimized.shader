Shader "ERBS_BG/BG_AutodeskInteractive_Optimized" {
	Properties {
		_Color ("Color", Vector) = (1,1,1,1)
		_AddColor ("Color on lightmap", Vector) = (1,1,1,1)
		_MaskColor ("Alpha Channel Mask Color", Vector) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		[NoScaleOffset] _MaskTex ("Merged Values Texture", 2D) = "white" {}
		_Metallic ("Metallic", Range(0, 1)) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 1
		_BumpScale ("Normal Scale", Float) = 1
		[NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
		_OcclusionStrength ("Occlusion Strength", Float) = 0
		[Toggle(_EMISSION)] _IsEmission ("Is Emission", Float) = 0
		[Enum(UnityEngine.Material.GlobalIlluminationFlags)] _GIFlag ("Global Illumination", Float) = 0
		_EmissiveValue ("EmissiveValue", Float) = 0
		[HDR] _EmissionColor ("EmissionColor", Vector) = (0,0,0,0)
		_EmissiveMask ("EmissiveMask", 2D) = "white" {}
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
	Fallback "Diffuse"
}