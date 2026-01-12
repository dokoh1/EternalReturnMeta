Shader "ERBS_FX/Effect_Base_Always" {
	Properties {
		[HideInInspector] _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
		[Enum(UnityEngine.Rendering.BlendMode)] SrcBlend ("SrcBlend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] DstBlend ("DstBlend", Float) = 10
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
		[Enum(off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
		[Space(10)] [Toggle(IsCoustom)] _IsCoustom ("IsCoustom", Float) = 0
		[Space(10)] [HDR] _BaseColor ("Base Color", Vector) = (1,1,1,1)
		[Space(10)] _BaseMap ("Base Map", 2D) = "white" {}
		[Toggle(DeBlackBG)] _DeBlackBG ("DeBlackBG", Float) = 0
		_BaseMapBrightness ("BaseMap Brightness", Float) = 1
		_BaseMapPower ("BaseMap Power", Float) = 1
		_BaseMapPannerX ("BaseMapPannerX", Float) = 0
		_BaseMapPannerY ("BaseMapPannerY", Float) = 0
		[Space(10)] _TurbulenceTex ("Turbulence Tex", 2D) = "white" {}
		_TurbulenceTexPannerX ("Turbulence Tex Panner X", Float) = 0
		_TurbulenceTexPannerY ("Turbulence Tex Panner Y", Float) = 0
		_TurbulenceStrength ("TurbulenceStrength", Float) = 0
		[Space(10)] _MaskTex ("Mask Tex", 2D) = "white" {}
		_MaskTexPannerX ("Mask Tex Panner X", Float) = 0
		_MaskTexPannerY ("Mask Tex Panner Y", Float) = 0
		[Space(10)] _DissolveTex ("Dissolve Tex", 2D) = "white" {}
		_DissolvePannerX ("Dissolve Tex Panner X", Float) = 0
		_DissolvePannerY ("Dissolve Tex Panner Y", Float) = 0
		_DisSoftness ("DisSoftness", Range(0.1, 2)) = 0.1
		_AnDissolve ("AnDissolve", Range(-2, 2)) = -2
		[Space(10)] [Toggle(IsFresnel)] _IsFresnel ("IsFresnel", Float) = 0
		[HDR] _FColor ("FColor", Vector) = (1,1,1,1)
		_FScale ("FScale", Float) = 1
		[Space(10)] [Toggle(IsDoubleFace)] _IsDoubleFace ("IsDoubleFace", Float) = 0
		[HDR] _FaceInColor ("FaceInColor", Vector) = (1,1,1,1)
		[HDR] _FaceOurColor ("FaceOurColor", Vector) = (1,1,1,1)
		[Space(10)] _PointMove ("PointMove", Vector) = (0,0,0,0)
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