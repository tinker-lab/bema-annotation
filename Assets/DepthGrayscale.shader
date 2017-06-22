// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/DepthGrayscale" {
	//Properties {
	//	_Color ("Color", Color) = (1,1,1,1)
	//	_MainTex ("Albedo (RGB)", 2D) = "white" {}
	//	_Glossiness ("Smoothness", Range(0,1)) = 0.5
	//	_Metallic ("Metallic", Range(0,1)) = 0.0
	//}
	SubShader{
		Tags{"RenderType" = "Opaque"}
		Pass{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#include "UnityCG.cginc"

		sampler2D _CameraDepthTexture;

		struct v2f {
			float4 pos : SV_POSITION;
			float4 scrPos: TEXCOORD1;
		};

	v2f vert(appdata_base v) {
		v2f o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.scrPos = ComputeScreenPos(o.pos);
		o.scrPos.y = 1 - o.scrPos.y;
		return o;
	}

		half4 frag(v2f i) : COLOR{
		float depthValue = Linear01Depth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.scrPos)).r);
		half4 depth;

		depth.r = depthValue;
		depth.g = depthValue;
		depth.b = depthValue;

		depth.a = 1;
		return depth;
	}

	ENDCG
	}
	}
	FallBack "Diffuse"
}
