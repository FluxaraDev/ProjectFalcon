Shader "Gorilla/DayNightLerpUnlit" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
		_Lightmap1("Lightmap 1 (RGBM)", 2D) = "white" {}
		_Lightmap2("Lightmap 2 (RGBM)", 2D) = "white" {}
		_LerpValue("Lerp Value", Float) = 0.5
	}
		SubShader{
			LOD 100
			Tags { "RenderType" = "Opaque" }
			Pass {
				LOD 100
				Tags { "LIGHTMODE" = "Vertex" "RenderType" = "Opaque" }
				Fog {

				}
				GpuProgramID 125833
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"
				struct v2f
				{
					float4 color : COLOR0;
					float2 texcoord : TEXCOORD0;
					float4 position : SV_POSITION0;
				};
				struct fout
				{
					float4 sv_target : SV_Target0;
				};
				// $Globals ConstantBuffers for Vertex Shader
				float4 _MainTex_ST;
				// $Globals ConstantBuffers for Fragment Shader
				// Custom ConstantBuffers for Vertex Shader
				// Custom ConstantBuffers for Fragment Shader
				// Texture params for Vertex Shader
				// Texture params for Fragment Shader
				sampler2D _MainTex;

				// Keywords: 
				v2f vert(appdata_full v)
				{
					v2f o;
					float4 tmp0;
					float4 tmp1;
					o.color = float4(0.0, 0.0, 0.0, 1.0);
					o.texcoord.xy = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
					tmp0 = v.vertex.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
					tmp0 = unity_ObjectToWorld._m00_m10_m20_m30 * v.vertex.xxxx + tmp0;
					tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * v.vertex.zzzz + tmp0;
					tmp0 = tmp0 + unity_ObjectToWorld._m03_m13_m23_m33;
					tmp1 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
					tmp1 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
					tmp1 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
					o.position = unity_MatrixVP._m03_m13_m23_m33 * tmp0.wwww + tmp1;
					return o;
				}
				// Keywords: 
				fout frag(v2f inp)
				{
					fout o;
					float4 tmp0;
					tmp0 = tex2D(_MainTex, inp.texcoord.xy);
					o.sv_target.xyz = tmp0.xyz;
					o.sv_target.w = 1.0;
					return o;
				}
				ENDCG
			}
			Pass {
				LOD 100
				Tags { "LIGHTMODE" = "VertexLM" "RenderType" = "Opaque" }
				GpuProgramID 264
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"
				struct v2f
				{
					float2 texcoord : TEXCOORD0;
					float2 texcoord1 : TEXCOORD1;
					float4 position : SV_POSITION0;
				};
				struct fout
				{
					float4 sv_target : SV_Target0;
				};
				// $Globals ConstantBuffers for Vertex Shader
				float4 _MainTex_ST;
				// $Globals ConstantBuffers for Fragment Shader
				float _LerpValue;
				// Custom ConstantBuffers for Vertex Shader
				// Custom ConstantBuffers for Fragment Shader
				// Texture params for Vertex Shader
				// Texture params for Fragment Shader
				sampler2D _Lightmap1;
				sampler2D _Lightmap2;
				sampler2D _MainTex;

				// Keywords: 
				v2f vert(appdata_full v)
				{
					v2f o;
					float4 tmp0;
					float4 tmp1;
					o.texcoord.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
					o.texcoord1.xy = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
					tmp0 = v.vertex.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
					tmp0 = unity_ObjectToWorld._m00_m10_m20_m30 * v.vertex.xxxx + tmp0;
					tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * v.vertex.zzzz + tmp0;
					tmp0 = tmp0 + unity_ObjectToWorld._m03_m13_m23_m33;
					tmp1 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
					tmp1 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
					tmp1 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
					o.position = unity_MatrixVP._m03_m13_m23_m33 * tmp0.wwww + tmp1;
					return o;
				}
				// Keywords: 
				fout frag(v2f inp)
				{
					fout o;
					float4 tmp0;
					float4 tmp1;
					tmp0 = tex2D(_Lightmap1, inp.texcoord.xy);
					tmp0.w = log(tmp0.w);
					tmp0.w = tmp0.w * unity_Lightmap_HDR.y;
					tmp0.w = exp(tmp0.w);
					tmp0.w = tmp0.w * unity_Lightmap_HDR.x;
					tmp0.xyz = tmp0.xyz * tmp0.www;
					tmp1 = tex2D(_Lightmap2, inp.texcoord.xy);
					tmp0.w = log(tmp1.w);
					tmp0.w = tmp0.w * unity_Lightmap_HDR.y;
					tmp0.w = exp(tmp0.w);
					tmp0.w = tmp0.w * unity_Lightmap_HDR.x;
					tmp1.xyz = tmp0.www * tmp1.xyz + -tmp0.xyz;
					tmp0.xyz = _LerpValue.xxx * tmp1.xyz + tmp0.xyz;
					tmp1 = tex2D(_MainTex, inp.texcoord1.xy);
					o.sv_target.xyz = tmp0.xyz * tmp1.xyz;
					o.sv_target.w = 1.0;
					return o;
				}
				ENDCG
			}
		}
}