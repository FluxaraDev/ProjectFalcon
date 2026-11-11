Shader "Gorilla/Halloween22/HornsSlingshotImpactFX" {
	Properties{
		[SingleLineTexture] [NoScaleOffset] _Masks("Masks", 2D) = "white" {}
		[Vec2] _SpeedRG("SpeedRG", Vector) = (-0.2,-0.2,0,0)
		[Vec2] _ScaleRG("ScaleRG", Vector) = (0.08,0.08,0,0)
		[Toggle(_VERTEX_DEFORMATION)] _VERTEX_DEFORMATION("VERTEX_DEFORMATION", Float) = 1
		[HideInInspector] __dirty("", Float) = 1
	}
		SubShader{
			Tags { "FORCENOSHADOWCASTING" = "true" "IGNOREPROJECTOR" = "true" "IsEmissive" = "true" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" }
			Pass {
				Name "FORWARD"
				Tags { "FORCENOSHADOWCASTING" = "true" "IGNOREPROJECTOR" = "true" "IsEmissive" = "true" "LIGHTMODE" = "FORWARDBASE" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" "SHADOWSUPPORT" = "true" }
				GpuProgramID 12975
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"
				struct v2f
				{
					float4 position : SV_POSITION0;
					float3 texcoord : TEXCOORD0;
					float3 texcoord1 : TEXCOORD1;
					float4 color : COLOR0;
					float2 texcoord2 : TEXCOORD2;
					float4 texcoord4 : TEXCOORD4;
				};
				struct fout
				{
					float4 sv_target : SV_Target0;
				};
				// $Globals ConstantBuffers for Vertex Shader
				float2 _SpeedRG;
				float2 _ScaleRG;
				// $Globals ConstantBuffers for Fragment Shader
				// Custom ConstantBuffers for Vertex Shader
				// Custom ConstantBuffers for Fragment Shader
				// Texture params for Vertex Shader
				// Texture params for Fragment Shader
				sampler2D _Masks;

				// Keywords: DIRECTIONAL
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
					o.texcoord1.xyz = tmp0.xyz;
					tmp0.x = dot(v.normal.xyz, unity_WorldToObject._m00_m10_m20);
					tmp0.y = dot(v.normal.xyz, unity_WorldToObject._m01_m11_m21);
					tmp0.z = dot(v.normal.xyz, unity_WorldToObject._m02_m12_m22);
					tmp0.w = dot(tmp0.xyz, tmp0.xyz);
					tmp0.w = rsqrt(tmp0.w);
					o.texcoord.xyz = tmp0.www * tmp0.xyz;
					o.color = v.color;
					tmp0.xy = v.texcoord.xy * _ScaleRG;
					o.texcoord2.xy = _SpeedRG * _Time.yy + tmp0.xy;
					o.texcoord4 = float4(0.0, 0.0, 0.0, 0.0);
					return o;
				}
				// Keywords: DIRECTIONAL
				fout frag(v2f inp)
				{
					fout o;
					float4 tmp0;
					tmp0 = tex2D(_Masks, inp.texcoord2.xy);
					tmp0.x = tmp0.y + tmp0.x;
					tmp0.x = saturate(tmp0.z + tmp0.x);
					o.sv_target.xyz = tmp0.xxx * inp.color.xyz;
					o.sv_target.w = 1.0;
					return o;
				}
				ENDCG
			}
			Pass {
				Name "ShadowCaster"
				Tags { "FORCENOSHADOWCASTING" = "true" "IGNOREPROJECTOR" = "true" "IsEmissive" = "true" "LIGHTMODE" = "SHADOWCASTER" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" "SHADOWSUPPORT" = "true" }
				GpuProgramID 86464
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"
				struct v2f
				{
					float4 position : SV_POSITION0;
					float2 texcoord1 : TEXCOORD1;
				};
				struct fout
				{
					float4 sv_target : SV_Target0;
				};
				// $Globals ConstantBuffers for Vertex Shader
				float2 _SpeedRG;
				float2 _ScaleRG;
				// $Globals ConstantBuffers for Fragment Shader
				// Custom ConstantBuffers for Vertex Shader
				// Custom ConstantBuffers for Fragment Shader
				// Texture params for Vertex Shader
				// Texture params for Fragment Shader

				// Keywords: SHADOWS_DEPTH
				v2f vert(appdata_full v)
				{
					v2f o;
					float4 tmp0;
					float4 tmp1;
					float4 tmp2;
					tmp0.x = dot(v.normal.xyz, unity_WorldToObject._m00_m10_m20);
					tmp0.y = dot(v.normal.xyz, unity_WorldToObject._m01_m11_m21);
					tmp0.z = dot(v.normal.xyz, unity_WorldToObject._m02_m12_m22);
					tmp0.w = dot(tmp0.xyz, tmp0.xyz);
					tmp0.w = rsqrt(tmp0.w);
					tmp0.xyz = tmp0.www * tmp0.xyz;
					tmp1 = v.vertex.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
					tmp1 = unity_ObjectToWorld._m00_m10_m20_m30 * v.vertex.xxxx + tmp1;
					tmp1 = unity_ObjectToWorld._m02_m12_m22_m32 * v.vertex.zzzz + tmp1;
					tmp1 = tmp1 + unity_ObjectToWorld._m03_m13_m23_m33;
					tmp2.xyz = -tmp1.xyz * _WorldSpaceLightPos0.www + _WorldSpaceLightPos0.xyz;
					tmp0.w = dot(tmp2.xyz, tmp2.xyz);
					tmp0.w = rsqrt(tmp0.w);
					tmp2.xyz = tmp0.www * tmp2.xyz;
					tmp0.w = dot(tmp0.xyz, tmp2.xyz);
					tmp0.w = -tmp0.w * tmp0.w + 1.0;
					tmp0.w = sqrt(tmp0.w);
					tmp0.w = tmp0.w * unity_LightShadowBias.z;
					tmp0.xyz = -tmp0.xyz * tmp0.www + tmp1.xyz;
					tmp0.w = unity_LightShadowBias.z != 0.0;
					tmp0.xyz = tmp0.www ? tmp0.xyz : tmp1.xyz;
					tmp2 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
					tmp2 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp2;
					tmp0 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp2;
					tmp0 = unity_MatrixVP._m03_m13_m23_m33 * tmp1.wwww + tmp0;
					tmp1.x = unity_LightShadowBias.x / tmp0.w;
					tmp1.x = min(tmp1.x, 0.0);
					tmp1.x = max(tmp1.x, -1.0);
					tmp0.z = tmp0.z + tmp1.x;
					tmp1.x = min(tmp0.w, tmp0.z);
					o.position.xyw = tmp0.xyw;
					tmp0.x = tmp1.x - tmp0.z;
					o.position.z = unity_LightShadowBias.y * tmp0.x + tmp0.z;
					tmp0.xy = v.texcoord.xy * _ScaleRG;
					o.texcoord1.xy = _SpeedRG * _Time.yy + tmp0.xy;
					return o;
				}
				// Keywords: SHADOWS_DEPTH
				fout frag(v2f inp)
				{
					fout o;
					o.sv_target = float4(0.0, 0.0, 0.0, 0.0);
					return o;
				}
				ENDCG
			}
		}
			Fallback "Diffuse"
}