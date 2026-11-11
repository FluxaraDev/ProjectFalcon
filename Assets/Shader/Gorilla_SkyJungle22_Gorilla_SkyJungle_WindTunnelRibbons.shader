Shader "Gorilla/SkyJungle22/Gorilla_SkyJungle_WindTunnelRibbons" {
    Properties{
        [HideInInspector] __dirty("", Float) = 1
    }
        SubShader{
            Tags { "IsEmissive" = "true" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" }
            Pass {
                Name "FORWARD"
                Tags { "IsEmissive" = "true" "LIGHTMODE" = "FORWARDBASE" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" "SHADOWSUPPORT" = "true" }
                Cull Off
                GpuProgramID 9548
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                struct v2f
                {
                    float4 position : SV_POSITION0;
                    float3 texcoord : TEXCOORD0;
                    float texcoord2 : TEXCOORD2;
                    float3 texcoord1 : TEXCOORD1;
                    float3 texcoord3 : TEXCOORD3;
                    float4 texcoord5 : TEXCOORD5;
                    float4 texcoord6 : TEXCOORD6;
                };
                struct fout
                {
                    float4 sv_target : SV_Target0;
                };
                // $Globals ConstantBuffers for Vertex Shader
                // $Globals ConstantBuffers for Fragment Shader
                // Custom ConstantBuffers for Vertex Shader
                // Custom ConstantBuffers for Fragment Shader
                // Texture params for Vertex Shader
                // Texture params for Fragment Shader

                // Keywords: DIRECTIONAL
                v2f vert(appdata_full v)
                {
                    v2f o;
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    tmp0.x = v.texcoord.x + _Time.y;
                    tmp0.x = tmp0.x * 0.5;
                    tmp0.y = tmp0.x >= -tmp0.x;
                    tmp0.x = frac(abs(tmp0.x));
                    tmp0.x = tmp0.y ? tmp0.x : -tmp0.x;
                    tmp0.x = tmp0.x * 2.0 + -0.3;
                    tmp0.x = tmp0.x * 10.0;
                    tmp0.x = max(tmp0.x, 0.7);
                    tmp0.x = min(tmp0.x, 1.0);
                    tmp0.yzw = v.color.xyz - v.vertex.xyz;
                    tmp0.xyz = tmp0.xxx * tmp0.yzw + v.vertex.xyz;
                    tmp1 = tmp0.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
                    tmp2.x = sin(-_Time.y);
                    tmp3.x = cos(-_Time.y);
                    tmp4.z = tmp2.x;
                    tmp4.y = tmp3.x;
                    tmp4.x = -tmp2.x;
                    tmp0.y = dot(tmp0.xy, tmp4.xy);
                    tmp0.x = dot(tmp0.xy, tmp4.xy);
                    tmp1 = unity_ObjectToWorld._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * tmp0.yyyy + tmp1;
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
                    o.texcoord2.x = 0.0;
                    o.texcoord3.xyz = float3(0.0, 0.0, 0.0);
                    o.texcoord5 = float4(0.0, 0.0, 0.0, 0.0);
                    o.texcoord6 = float4(0.0, 0.0, 0.0, 0.0);
                    return o;
                }
                // Keywords: DIRECTIONAL
                fout frag(v2f inp)
                {
                    fout o;
                    o.sv_target = float4(0.2933302, 0.4601087, 0.6231937, 1.0);
                    return o;
                }
                ENDCG
            }
            Pass {
                Name "FORWARD"
                Tags { "IsEmissive" = "true" "LIGHTMODE" = "FORWARDADD" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" "SHADOWSUPPORT" = "true" }
                Blend One One, One One
                ZWrite Off
                Cull Off
                GpuProgramID 101417
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                struct v2f
                {
                    float4 position : SV_POSITION0;
                    float3 texcoord : TEXCOORD0;
                    float texcoord2 : TEXCOORD2;
                    float3 texcoord1 : TEXCOORD1;
                    float3 texcoord3 : TEXCOORD3;
                    float4 texcoord4 : TEXCOORD4;
                };
                struct fout
                {
                    float4 sv_target : SV_Target0;
                };
                // $Globals ConstantBuffers for Vertex Shader
                float4x4 unity_WorldToLight;
                // $Globals ConstantBuffers for Fragment Shader
                // Custom ConstantBuffers for Vertex Shader
                // Custom ConstantBuffers for Fragment Shader
                // Texture params for Vertex Shader
                // Texture params for Fragment Shader

                // Keywords: POINT
                v2f vert(appdata_full v)
                {
                    v2f o;
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    tmp0.x = v.texcoord.x + _Time.y;
                    tmp0.x = tmp0.x * 0.5;
                    tmp0.y = tmp0.x >= -tmp0.x;
                    tmp0.x = frac(abs(tmp0.x));
                    tmp0.x = tmp0.y ? tmp0.x : -tmp0.x;
                    tmp0.x = tmp0.x * 2.0 + -0.3;
                    tmp0.x = tmp0.x * 10.0;
                    tmp0.x = max(tmp0.x, 0.7);
                    tmp0.x = min(tmp0.x, 1.0);
                    tmp0.yzw = v.color.xyz - v.vertex.xyz;
                    tmp0.xyz = tmp0.xxx * tmp0.yzw + v.vertex.xyz;
                    tmp1 = tmp0.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
                    tmp2.x = sin(-_Time.y);
                    tmp3.x = cos(-_Time.y);
                    tmp4.z = tmp2.x;
                    tmp4.y = tmp3.x;
                    tmp4.x = -tmp2.x;
                    tmp0.y = dot(tmp0.xy, tmp4.xy);
                    tmp0.x = dot(tmp0.xy, tmp4.xy);
                    tmp1 = unity_ObjectToWorld._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * tmp0.yyyy + tmp1;
                    tmp0 = tmp0 + unity_ObjectToWorld._m03_m13_m23_m33;
                    tmp1 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
                    tmp1 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp1 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
                    o.position = unity_MatrixVP._m03_m13_m23_m33 * tmp0.wwww + tmp1;
                    tmp1.x = dot(v.normal.xyz, unity_WorldToObject._m00_m10_m20);
                    tmp1.y = dot(v.normal.xyz, unity_WorldToObject._m01_m11_m21);
                    tmp1.z = dot(v.normal.xyz, unity_WorldToObject._m02_m12_m22);
                    tmp1.w = dot(tmp1.xyz, tmp1.xyz);
                    tmp1.w = rsqrt(tmp1.w);
                    o.texcoord.xyz = tmp1.www * tmp1.xyz;
                    o.texcoord2.x = 0.0;
                    o.texcoord1.xyz = tmp0.xyz;
                    tmp1.xyz = tmp0.yyy * unity_WorldToLight._m01_m11_m21;
                    tmp1.xyz = unity_WorldToLight._m00_m10_m20 * tmp0.xxx + tmp1.xyz;
                    tmp0.xyz = unity_WorldToLight._m02_m12_m22 * tmp0.zzz + tmp1.xyz;
                    o.texcoord3.xyz = unity_WorldToLight._m03_m13_m23 * tmp0.www + tmp0.xyz;
                    o.texcoord4 = float4(0.0, 0.0, 0.0, 0.0);
                    return o;
                }
                // Keywords: POINT
                fout frag(v2f inp)
                {
                    fout o;
                    o.sv_target = float4(0.0, 0.0, 0.0, 1.0);
                    return o;
                }
                ENDCG
            }
            Pass {
                Name "ShadowCaster"
                Tags { "IsEmissive" = "true" "LIGHTMODE" = "SHADOWCASTER" "QUEUE" = "Geometry+0" "RenderType" = "Opaque" "SHADOWSUPPORT" = "true" }
                Cull Off
                GpuProgramID 179022
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                struct v2f
                {
                    float4 position : SV_POSITION0;
                    float texcoord1 : TEXCOORD1;
                };
                struct fout
                {
                    float4 sv_target : SV_Target0;
                };
                // $Globals ConstantBuffers for Vertex Shader
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
                    float4 tmp3;
                    float4 tmp4;
                    tmp0.x = v.texcoord.x + _Time.y;
                    tmp0.x = tmp0.x * 0.5;
                    tmp0.y = tmp0.x >= -tmp0.x;
                    tmp0.x = frac(abs(tmp0.x));
                    tmp0.x = tmp0.y ? tmp0.x : -tmp0.x;
                    tmp0.x = tmp0.x * 2.0 + -0.3;
                    tmp0.x = tmp0.x * 10.0;
                    tmp0.x = max(tmp0.x, 0.7);
                    tmp0.x = min(tmp0.x, 1.0);
                    tmp0.yzw = v.color.xyz - v.vertex.xyz;
                    tmp0.xyz = tmp0.xxx * tmp0.yzw + v.vertex.xyz;
                    tmp1 = tmp0.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
                    tmp2.x = sin(-_Time.y);
                    tmp3.x = cos(-_Time.y);
                    tmp4.z = tmp2.x;
                    tmp4.y = tmp3.x;
                    tmp4.x = -tmp2.x;
                    tmp0.y = dot(tmp0.xy, tmp4.xy);
                    tmp0.x = dot(tmp0.xy, tmp4.xy);
                    tmp1 = unity_ObjectToWorld._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * tmp0.yyyy + tmp1;
                    tmp0 = tmp0 + unity_ObjectToWorld._m03_m13_m23_m33;
                    tmp1.xyz = -tmp0.xyz * _WorldSpaceLightPos0.www + _WorldSpaceLightPos0.xyz;
                    tmp1.w = dot(tmp1.xyz, tmp1.xyz);
                    tmp1.w = rsqrt(tmp1.w);
                    tmp1.xyz = tmp1.www * tmp1.xyz;
                    tmp2.x = dot(v.normal.xyz, unity_WorldToObject._m00_m10_m20);
                    tmp2.y = dot(v.normal.xyz, unity_WorldToObject._m01_m11_m21);
                    tmp2.z = dot(v.normal.xyz, unity_WorldToObject._m02_m12_m22);
                    tmp1.w = dot(tmp2.xyz, tmp2.xyz);
                    tmp1.w = rsqrt(tmp1.w);
                    tmp2.xyz = tmp1.www * tmp2.xyz;
                    tmp1.x = dot(tmp2.xyz, tmp1.xyz);
                    tmp1.x = -tmp1.x * tmp1.x + 1.0;
                    tmp1.x = sqrt(tmp1.x);
                    tmp1.x = tmp1.x * unity_LightShadowBias.z;
                    tmp1.xyz = -tmp2.xyz * tmp1.xxx + tmp0.xyz;
                    tmp1.w = unity_LightShadowBias.z != 0.0;
                    tmp0.xyz = tmp1.www ? tmp1.xyz : tmp0.xyz;
                    tmp1 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
                    tmp1 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp1 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
                    tmp0 = unity_MatrixVP._m03_m13_m23_m33 * tmp0.wwww + tmp1;
                    tmp1.x = unity_LightShadowBias.x / tmp0.w;
                    tmp1.x = min(tmp1.x, 0.0);
                    tmp1.x = max(tmp1.x, -1.0);
                    tmp0.z = tmp0.z + tmp1.x;
                    tmp1.x = min(tmp0.w, tmp0.z);
                    o.position.xyw = tmp0.xyw;
                    tmp0.x = tmp1.x - tmp0.z;
                    o.position.z = unity_LightShadowBias.y * tmp0.x + tmp0.z;
                    o.texcoord1.x = 0.0;
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