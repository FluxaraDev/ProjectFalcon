Shader "Gorilla/GorillaUnlitWithEmissiveTex" {
	Properties{
		[NoScaleOffset] _MainTex("Base (RGB)", 2D) = "white" {}
		[NoScaleOffset] _EmissiveTex("Emissive (RGB)", 2D) = "white" {}
		_EmissiveAmount("Emissive Amount", Range(0, 1)) = 1
		[KeywordEnum(OFF, ON)] _VERTEX_COLOR_("Vertex Color", Float) = 0
		[Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
	}
		SubShader{
			LOD 100
			Tags { "RenderType" = "Opaque" }
			Pass {
				Name "Unlit"
				LOD 100
				Tags { "LIGHTMODE" = "FORWARDBASE" "RenderType" = "Opaque" }
				Cull Off
				GpuProgramID 20894
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"
				struct v2f
				{
					float4 position : SV_POSITION0;
					float4 texcoord : TEXCOORD0;
				};
				struct fout
				{
					float4 sv_target : SV_Target0;
				};
				// $Globals ConstantBuffers for Vertex Shader
				// $Globals ConstantBuffers for Fragment Shader
				// Custom ConstantBuffers for Vertex Shader
				// Custom ConstantBuffers for Fragment Shader
				CBUFFER_START(UnityPerMaterial)
					float _EmissiveAmount;
				CBUFFER_END
					// Texture params for Vertex Shader
					// Texture params for Fragment Shader
					sampler2D _MainTex;
					sampler2D _EmissiveTex;

					// Keywords: 
					v2f vert(appdata_full v)
					{
						v2f o;
						float4 tmp0;
						float4 tmp1;
						tmp0 = v.vertex.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
						tmp0 = unity_ObjectToWorld._m00_m10_m20_m30 * v.vertex.xxxx + tmp0;
						tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * v.vertex.zzzz + tmp0;
						tmp0 = tmp0 + unity_ObjectToWorld._m03_m13_m23_m33;
						tmp1 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
						tmp1 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
						tmp1 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
						o.position = unity_MatrixVP._m03_m13_m23_m33 * tmp0.wwww + tmp1;
						o.texcoord = v.texcoord;
						return o;
					}
					// Keywords: 
					fout frag(v2f inp)
					{
						fout o;
						float4 tmp0;
						float4 tmp1;
						tmp0 = tex2D(_MainTex, inp.texcoord.xy);
						tmp1 = tex2D(_EmissiveTex, inp.texcoord.xy);
						o.sv_target.xyz = tmp1.xyz * _EmissiveAmount.xxx + tmp0.xyz;
						o.sv_target.w = 1.0;
						return o;
					}
					ENDCG
				}
		}
}