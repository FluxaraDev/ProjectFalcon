Shader "Gorilla/CutoffLerpLightmap" {
    Properties{
        _Color("Main Color", Color) = (1,1,1,1)
        _SpecColor("Spec Color", Vector) = (1,1,1,0)
        _Emission("Emissive Color", Vector) = (0,0,0,0)
        [PowerSlider(5.0)] _Shininess("Shininess", Range(0.1, 1)) = 0.7
        _MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
        _Cutoff("Alpha cutoff", Range(0, 1)) = 0.5
    }
        SubShader{
            LOD 100
            Tags { "IGNOREPROJECTOR" = "true" "QUEUE" = "AlphaTest" "RenderType" = "TransparentCutout" }
            Pass {
                LOD 100
                Tags { "IGNOREPROJECTOR" = "true" "LIGHTMODE" = "Vertex" "QUEUE" = "AlphaTest" "RenderType" = "TransparentCutout" }
                ColorMask RGB
                AlphaToMask On
                Fog
                {
                }
                GpuProgramID 170603
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                struct v2f
                {
                    float4 color : COLOR0;
                    float3 color1 : COLOR1;
                    float2 texcoord : TEXCOORD0;
                    float4 position : SV_POSITION0;
                };
                struct fout
                {
                    float4 sv_target : SV_Target0;
                };
                // $Globals ConstantBuffers for Vertex Shader
                float4 _Color;
                float4 _SpecColor;
                float4 _Emission;
                float _Shininess;
                int4 unity_VertexLightParams;
                float4 _MainTex_ST;
                // $Globals ConstantBuffers for Fragment Shader
                float _Cutoff;
                // Custom ConstantBuffers for Vertex Shader
                // Custom ConstantBuffers for Fragment Shader
                // Texture params for Vertex Shader
                // Texture params for Fragment Shader
                sampler2D _MainTex;

                // Keywords: POINT
                v2f vert(appdata_full v)
                {
                    v2f o;
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    float4 tmp5;
                    float4 tmp6;
                    tmp0.xyz = unity_ObjectToWorld._m10_m10_m10 * unity_MatrixV._m01_m11_m21;
                    tmp0.xyz = unity_MatrixV._m00_m10_m20 * unity_ObjectToWorld._m00_m00_m00 + tmp0.xyz;
                    tmp0.xyz = unity_MatrixV._m02_m12_m22 * unity_ObjectToWorld._m20_m20_m20 + tmp0.xyz;
                    tmp0.xyz = unity_MatrixV._m03_m13_m23 * unity_ObjectToWorld._m30_m30_m30 + tmp0.xyz;
                    tmp1.xyz = unity_ObjectToWorld._m11_m11_m11 * unity_MatrixV._m01_m11_m21;
                    tmp1.xyz = unity_MatrixV._m00_m10_m20 * unity_ObjectToWorld._m01_m01_m01 + tmp1.xyz;
                    tmp1.xyz = unity_MatrixV._m02_m12_m22 * unity_ObjectToWorld._m21_m21_m21 + tmp1.xyz;
                    tmp1.xyz = unity_MatrixV._m03_m13_m23 * unity_ObjectToWorld._m31_m31_m31 + tmp1.xyz;
                    tmp2.xyz = unity_ObjectToWorld._m12_m12_m12 * unity_MatrixV._m01_m11_m21;
                    tmp2.xyz = unity_MatrixV._m00_m10_m20 * unity_ObjectToWorld._m02_m02_m02 + tmp2.xyz;
                    tmp2.xyz = unity_MatrixV._m02_m12_m22 * unity_ObjectToWorld._m22_m22_m22 + tmp2.xyz;
                    tmp2.xyz = unity_MatrixV._m03_m13_m23 * unity_ObjectToWorld._m32_m32_m32 + tmp2.xyz;
                    tmp3.xyz = unity_ObjectToWorld._m13_m13_m13 * unity_MatrixV._m01_m11_m21;
                    tmp3.xyz = unity_MatrixV._m00_m10_m20 * unity_ObjectToWorld._m03_m03_m03 + tmp3.xyz;
                    tmp3.xyz = unity_MatrixV._m02_m12_m22 * unity_ObjectToWorld._m23_m23_m23 + tmp3.xyz;
                    tmp3.xyz = unity_MatrixV._m03_m13_m23 * unity_ObjectToWorld._m33_m33_m33 + tmp3.xyz;
                    tmp4.xyz = unity_WorldToObject._m01_m11_m21 * unity_MatrixInvV._m10_m10_m10;
                    tmp4.xyz = unity_WorldToObject._m00_m10_m20 * unity_MatrixInvV._m00_m00_m00 + tmp4.xyz;
                    tmp4.xyz = unity_WorldToObject._m02_m12_m22 * unity_MatrixInvV._m20_m20_m20 + tmp4.xyz;
                    tmp4.xyz = unity_WorldToObject._m03_m13_m23 * unity_MatrixInvV._m30_m30_m30 + tmp4.xyz;
                    tmp5.xyz = unity_WorldToObject._m01_m11_m21 * unity_MatrixInvV._m11_m11_m11;
                    tmp5.xyz = unity_WorldToObject._m00_m10_m20 * unity_MatrixInvV._m01_m01_m01 + tmp5.xyz;
                    tmp5.xyz = unity_WorldToObject._m02_m12_m22 * unity_MatrixInvV._m21_m21_m21 + tmp5.xyz;
                    tmp5.xyz = unity_WorldToObject._m03_m13_m23 * unity_MatrixInvV._m31_m31_m31 + tmp5.xyz;
                    tmp6.xyz = unity_WorldToObject._m01_m11_m21 * unity_MatrixInvV._m12_m12_m12;
                    tmp6.xyz = unity_WorldToObject._m00_m10_m20 * unity_MatrixInvV._m02_m02_m02 + tmp6.xyz;
                    tmp6.xyz = unity_WorldToObject._m02_m12_m22 * unity_MatrixInvV._m22_m22_m22 + tmp6.xyz;
                    tmp6.xyz = unity_WorldToObject._m03_m13_m23 * unity_MatrixInvV._m32_m32_m32 + tmp6.xyz;
                    tmp1.xyz = tmp1.xyz * v.vertex.yyy;
                    tmp0.xyz = tmp0.xyz * v.vertex.xxx + tmp1.xyz;
                    tmp0.xyz = tmp2.xyz * v.vertex.zzz + tmp0.xyz;
                    tmp0.xyz = tmp3.xyz + tmp0.xyz;
                    tmp1.x = dot(tmp4.xyz, v.normal.xyz);
                    tmp1.y = dot(tmp5.xyz, v.normal.xyz);
                    tmp1.z = dot(tmp6.xyz, v.normal.xyz);
                    tmp0.w = dot(tmp1.xyz, tmp1.xyz);
                    tmp0.w = rsqrt(tmp0.w);
                    tmp1.xyz = tmp0.www * tmp1.xyz;
                    tmp0.w = dot(tmp0.xyz, tmp0.xyz);
                    tmp0.w = rsqrt(tmp0.w);
                    tmp2.xyz = _Color.xyz * glstate_lightmodel_ambient.xyz + _Emission.xyz;
                    tmp1.w = _Shininess * 128.0;
                    tmp3.xyz = tmp2.xyz;
                    tmp4.xyz = float3(0.0, 0.0, 0.0);
                    tmp2.w = 0.0;
                    for (int i = tmp2.w; i < unity_VertexLightParams.x; i += 1) {
                        tmp5.xyz = -tmp0.xyz * unity_LightPosition[i].www + unity_LightPosition[i].xyz;
                        tmp3.w = dot(tmp5.xyz, tmp5.xyz);
                        tmp4.w = unity_LightAtten[i].z * tmp3.w + 1.0;
                        tmp4.w = 1.0 / tmp4.w;
                        tmp5.w = unity_LightPosition[i].w != 0.0;
                        tmp6.x = unity_LightAtten[i].w < tmp3.w;
                        tmp5.w = tmp5.w ? tmp6.x : 0.0;
                        tmp3.w = max(tmp3.w, 0.000001);
                        tmp3.w = rsqrt(tmp3.w);
                        tmp5.xyz = tmp3.www * tmp5.xyz;
                        tmp3.w = tmp4.w * 0.5;
                        tmp3.w = tmp5.w ? 0.0 : tmp3.w;
                        tmp4.w = dot(tmp1.xyz, tmp5.xyz);
                        tmp4.w = max(tmp4.w, 0.0);
                        tmp6.xyz = tmp4.www * _Color.xyz;
                        tmp6.xyz = tmp6.xyz * unity_LightColor[i].xyz;
                        tmp4.w = tmp4.w > 0.0;
                        if (tmp4.w) {
                            tmp5.xyz = -tmp0.xyz * tmp0.www + tmp5.xyz;
                            tmp4.w = dot(tmp5.xyz, tmp5.xyz);
                            tmp4.w = rsqrt(tmp4.w);
                            tmp5.xyz = tmp4.www * tmp5.xyz;
                            tmp4.w = dot(tmp1.xyz, tmp5.xyz);
                            tmp4.w = max(tmp4.w, 0.0);
                            tmp4.w = log(tmp4.w);
                            tmp4.w = tmp1.w * tmp4.w;
                            tmp4.w = exp(tmp4.w);
                            tmp4.w = min(tmp4.w, 1.0);
                            tmp4.w = tmp3.w * tmp4.w;
                            tmp4.xyz = tmp4.www * unity_LightColor[i].xyz + tmp4.xyz;
                        }
                        tmp5.xyz = tmp3.www * tmp6.xyz;
                        tmp5.xyz = min(tmp5.xyz, float3(1.0, 1.0, 1.0));
                        tmp3.xyz = tmp3.xyz + tmp5.xyz;
                    }
                    o.color1.xyz = saturate(tmp4.xyz * _SpecColor.xyz);
                    o.color.xyz = saturate(tmp3.xyz);
                    o.color.w = saturate(_Color.w);
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
                // Keywords: POINT
                fout frag(v2f inp)
                {
                    fout o;
                    float4 tmp0;
                    float4 tmp1;
                    tmp0 = tex2D(_MainTex, inp.texcoord.xy);
                    tmp0 = tmp0 * inp.color;
                    tmp1.x = _Cutoff >= tmp0.w;
                    if (tmp1.x) {
                        discard;
                    }
                    o.sv_target.xyz = tmp0.xyz * float3(2.0, 2.0, 2.0) + inp.color1.xyz;
                    o.sv_target.w = tmp0.w;
                    return o;
                }
                ENDCG
            }
            Pass {
                LOD 100
                Tags { "IGNOREPROJECTOR" = "true" "LIGHTMODE" = "VertexLM" "QUEUE" = "AlphaTest" "RenderType" = "TransparentCutout" }
                ColorMask RGB
                AlphaToMask On
                GpuProgramID 62069
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                struct v2f
                {
                    float4 color : COLOR0;
                    float2 texcoord : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                    float4 position : SV_POSITION0;
                };
                struct fout
                {
                    float4 sv_target : SV_Target0;
                };
                // $Globals ConstantBuffers for Vertex Shader
                float4 unity_Lightmap_ST;
                float4 _MainTex_ST;
                // $Globals ConstantBuffers for Fragment Shader
                float _GlobalDayNightLerpValue;
                float4 _Color;
                float _Cutoff;
                // Custom ConstantBuffers for Vertex Shader
                // Custom ConstantBuffers for Fragment Shader
                // Texture params for Vertex Shader
                // Texture params for Fragment Shader
                sampler2D _GlobalDayNightLightmap1;
                sampler2D _GlobalDayNightLightmap2;
                sampler2D _MainTex;

                // Keywords: 
                v2f vert(appdata_full v)
                {
                    v2f o;
                    float4 tmp0;
                    float4 tmp1;
                    o.color = saturate(v.color);
                    o.texcoord.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.texcoord1.xy = v.texcoord1.xy * unity_Lightmap_ST.xy + unity_Lightmap_ST.zw;
                    o.texcoord2.xy = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
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
                    float4 tmp2;
                    tmp0 = tex2D(_MainTex, inp.texcoord2.xy);
                    tmp1.x = tmp0.w * inp.color.w + -_Cutoff;
                    tmp1.x = tmp1.x < 0.0;
                    if (tmp1.x) {
                        discard;
                    }
                    tmp1 = tex2D(_GlobalDayNightLightmap2, inp.texcoord.xy);
                    tmp2 = tex2D(_GlobalDayNightLightmap1, inp.texcoord.xy);
                    tmp1 = tmp1 - tmp2;
                    tmp1 = _GlobalDayNightLerpValue.xxxx * tmp1 + tmp2;
                    tmp1.w = log(tmp1.w);
                    tmp1.w = tmp1.w * unity_Lightmap_HDR.y;
                    tmp1.w = exp(tmp1.w);
                    tmp1.w = tmp1.w * unity_Lightmap_HDR.x;
                    tmp1.xyz = tmp1.xyz * tmp1.www;
                    tmp1.xyz = tmp1.xyz * _Color.xyz;
                    o.sv_target.xyz = tmp0.xyz * tmp1.xyz;
                    tmp0.x = tmp0.w * inp.color.w;
                    o.sv_target.w = tmp0.x;
                    return o;
                }
                ENDCG
            }
            Pass {
                Name "Caster"
                LOD 100
                Tags { "IGNOREPROJECTOR" = "true" "LIGHTMODE" = "SHADOWCASTER" "QUEUE" = "AlphaTest" "RenderType" = "TransparentCutout" "SHADOWSUPPORT" = "true" }
                GpuProgramID 111136
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
                float4 _MainTex_ST;
                // $Globals ConstantBuffers for Fragment Shader
                float _Cutoff;
                float4 _Color;
                // Custom ConstantBuffers for Vertex Shader
                // Custom ConstantBuffers for Fragment Shader
                // Texture params for Vertex Shader
                // Texture params for Fragment Shader
                sampler2D _MainTex;

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
                    tmp1 = unity_ObjectToWorld._m03_m13_m23_m33 * v.vertex.wwww + tmp1;
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
                    o.texcoord1.xy = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                    return o;
                }
                // Keywords: SHADOWS_DEPTH
                fout frag(v2f inp)
                {
                    fout o;
                    float4 tmp0;
                    tmp0 = tex2D(_MainTex, inp.texcoord1.xy);
                    tmp0.x = tmp0.w * _Color.w + -_Cutoff;
                    tmp0.x = tmp0.x < 0.0;
                    if (tmp0.x) {
                        discard;
                    }
                    o.sv_target = float4(0.0, 0.0, 0.0, 0.0);
                    return o;
                }
                ENDCG
            }
        }
}