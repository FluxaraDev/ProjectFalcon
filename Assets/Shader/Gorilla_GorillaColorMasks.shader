Shader "Gorilla/GorillaColorMasks" {
	Properties{
		[NoScaleOffset] _MainTex("Base (RGB)", 2D) = "white" {}
		_Color1("Color 1", Color) = (1,0,0,0.25)
		_Color2("Color 2", Color) = (0,1,0,0.25)
		_Color3("Color 3", Color) = (0,0,1,0.25)
		_Color4("Color 4", Color) = (0,0,0,0.25)
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
				GpuProgramID 36218
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
					float4 _Color1;
					float4 _Color2;
					float4 _Color3;
					float4 _Color4;
				CBUFFER_END
					// Texture params for Vertex Shader
					// Texture params for Fragment Shader
					sampler2D _MainTex;

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
					tmp1 = tmp0.yyyy * _Color2;
					tmp1 = tmp0.xxxx * _Color1 + tmp1;
					tmp1 = tmp0.zzzz * _Color3 + tmp1;
					o.sv_target = tmp0.wwww * _Color4 + tmp1;
					return o;
				}
				ENDCG
			}
		}
}