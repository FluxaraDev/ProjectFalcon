Shader "Gorilla/Halloween22/HornsSlingshotProjectile" {
	Properties{
		[SingleLineTexture] [NoScaleOffset] _Masks("Masks", 2D) = "white" {}
		[Vec2] _SpeedRG("SpeedRG", Vector) = (-0.2,-0.2,0,0)
		[Vec2] _ScaleRG("ScaleRG", Vector) = (0.08,0.08,0,0)
		_Color("Color", Vector) = (1,1,1,1)
		[ASEEnd][Toggle(_VERTEX_DEFORMATION)] _VERTEX_DEFORMATION("_VERTEX_DEFORMATION", Float) = 1
	}
		SubShader{
			LOD 100
			Tags { "RenderType" = "Opaque" }
			Pass {
				Name "Unlit"
				LOD 100
				Tags { "LIGHTMODE" = "FORWARDBASE" "RenderType" = "Opaque" }
				GpuProgramID 19765
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"
				struct v2f
				{
					float4 position : SV_POSITION0;
					float4 texcoord1 : TEXCOORD1;
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
				CBUFFER_START(GorillaHalloween22HornsSlingshotProjectile)
					float4 _Color;
				CBUFFER_END
					// Texture params for Vertex Shader
					// Texture params for Fragment Shader
					sampler2D _Masks;

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
					tmp0.xy = v.texcoord.xy * _ScaleRG;
					o.texcoord1.xy = _SpeedRG * _Time.yy + tmp0.xy;
					o.texcoord1.zw = float2(0.0, 0.0);
					return o;
				}
				// Keywords: 
				fout frag(v2f inp)
				{
					fout o;
					float4 tmp0;
					float4 tmp1;
					float4 tmp2;
					tmp0 = _Color * _Color;
					tmp0 = min(tmp0, float4(1.0, 1.0, 1.0, 1.0));
					tmp0 = tmp0 - float4(0.0, 0.0, 0.0, 1.0);
					tmp1 = tex2D(_Masks, inp.texcoord1.xy);
					tmp1.x = saturate(tmp1.y + tmp1.x);
					tmp1.y = tmp1.x * 5.0;
					tmp1.xzw = tmp1.xxx - float3(0.2, 0.6, 0.99);
					tmp1.xzw = tmp1.xzw * float3(5.0, 2.564103, 100.0);
					tmp1.y = min(tmp1.y, 1.0);
					tmp0 = tmp1.yyyy * tmp0 + float4(0.0, 0.0, 0.0, 1.0);
					tmp2 = _Color - tmp0;
					tmp1.xz = saturate(tmp1.xz);
					tmp1.y = max(tmp1.w, 0.0);
					tmp0 = tmp1.xxxx * tmp2 + tmp0;
					tmp2 = saturate(_Color * float4(3.5, 3.5, 3.5, 3.5));
					tmp2 = tmp2 - tmp0;
					tmp0 = tmp1.zzzz * tmp2 + tmp0;
					tmp1.x = _Color.y + _Color.x;
					tmp1.x = tmp1.x + _Color.z;
					o.sv_target = tmp1.yyyy * tmp1.xxxx + tmp0;
					return o;
				}
				ENDCG
			}
		}
}