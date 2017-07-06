// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/EdgeDetector" {
	SubShader{
		Tags{"RenderType" = "Opaque"}
		Pass{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#include "UnityCG.cginc"

		sampler2D _CameraDepthNormalsTexture;
		float4 _CameraDepthNormalsTexture_TexelSize;

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

			float3 normalValue;
			float depthVal;

			DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.scrPos.xy), depthVal, normalValue);

			float4 size = _CameraDepthNormalsTexture_TexelSize;

			// surrounding pixels
			float topD;
			float bottomD;
			float leftD;
			float rightD;
			float3 topN;
			float3 bottomN;
			float3 leftN;
			float3 rightN;

			DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(i.scrPos.x, i.scrPos.y + size.y)), topD, topN);
			DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(i.scrPos.x, i.scrPos.y - size.y)), bottomD, bottomN);
			DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(i.scrPos.x - size.x, i.scrPos.y)), leftD, leftN);
			DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, float2(i.scrPos.x + size.x, i.scrPos.y)), rightD, rightN);

			topN = normalize(topN);
			bottomN = normalize(bottomN);
			leftN = normalize(leftN);
			rightN = normalize(rightN);

			topD = Linear01Depth(topD);
			bottomD = Linear01Depth(bottomD);
			leftD = Linear01Depth(leftD);
			rightD = Linear01Depth(rightD);

			if (((abs(topD - bottomD) > 0.1) || (abs(rightD - leftD) > 0.1)) || ((abs(dot(topN, bottomN)) < 0.9) || (abs(dot(rightN, leftN)) < 0.9))) {
				return float4(0,0,0,1);
				//return float4(normalValue, 1);
			}
			else {
				return float4(1, 1, 1, 1);
				
			}

		}

		ENDCG
	}
	}
	FallBack "Diffuse"
}
