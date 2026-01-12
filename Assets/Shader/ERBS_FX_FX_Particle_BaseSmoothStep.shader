Shader "ERBS_FX/FX_Particle_BaseSmoothStep" {
	Properties {
		_Color ("Color", Vector) = (1,1,1,1)
		_MainTex ("Main Texture", 2D) = "white" {}
		_Rotate ("Rotate", Float) = 0
		_MaskTex ("MaskTex", 2D) = "white" {}
		_MainColor_To_Alpha_Amount ("MainColor_To_Alpha_Amount", Range(0, 1)) = 0
		_MainAlpha_To_R ("MainAlpha_To_R", Range(0, 1)) = 1
		_MulVal_Color ("MulVal_Color", Float) = 1
		[Toggle(_WITHPARTICLECUSTOM_ON)] _WithParticleCustom ("WithParticleCustom", Float) = 0
		_ScrollSpeed ("ScrollSpeed", Vector) = (0,0,0,0)
		_MaskScrollSpeed ("MaskScrollSpeed", Vector) = (0,0,0,0)
		[Toggle(_ISNOISE_ON)] _IsNoise ("IsNoise", Float) = 0
		[KeywordEnum(Simple,Complex,NormalComplex)] _IsNoiseComplex ("IsNoiseComplex", Float) = 1
		_NoiseTex ("NoiseTex", 2D) = "black" {}
		[KeywordEnum(NoiseUV_Used,BaseUV_Follow)] _AssignNoiseUV ("AssignNoiseUV", Float) = 0
		_MulVal_Noise ("MulVal_Noise", Float) = 0.05
		_NoiseScale_1 ("NoiseScale_1", Float) = 1
		_NoiseScrollSpeed_1 ("NoiseScrollSpeed_1", Vector) = (-1,-1,0,0)
		_NoiseScale_2 ("NoiseScale_2", Float) = 2
		_NoiseScrollSpeed_2 ("NoiseScrollSpeed_2", Vector) = (1,1,0,0)
		[Toggle(_ISDISSOLVE_ON)] _IsDissolve ("IsDissolve", Float) = 0
		[Toggle(_ISSMOOTHDISSOLVE_ON)] _IsSmoothDissolve ("IsSmoothDissolve", Float) = 0
		_DissolveMask ("DissolveMask", 2D) = "white" {}
		_MulAlpha ("MulAlpha", Float) = 1
		[KeywordEnum(DissolveUV_Used,BaseWarpUV_Follow)] _AssignDissolveUV ("AssignDissolveUV", Float) = 0
		[KeywordEnum(OnlyDissolve,BaseRedXDissolve)] _DissolveAlpha ("DissolveAlpha", Float) = 1
		_DissolveStep ("DissolveStep", Range(-1, 1)) = 1
		_DissolveSmoothRange ("DissolveSmoothRange", Range(0, 1)) = 0.2
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
		[Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("SrcBlend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DestBlend ("DestBlend", Float) = 10
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
	//CustomEditor "ASEMaterialInspector"
}