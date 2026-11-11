Shader "Gorilla/Halloween22/CandleFlameWobble" {
    Properties{
        [NoScaleOffset] _MainTex("MainTex", 2D) = "white" {}
        [NoScaleOffset] _FlameWobbleNoise("FlameWobbleNoise", 2D) = "white" {}
        [ToggleOff(_USE_WORLD_POS_AS_OFFSET_OFF)] _USE_WORLD_POS_AS_OFFSET("USE_WORLD_POS_AS_OFFSET", Float) = 1
        [ASEEnd] _ScrollUOffset("Scroll U Offset", Float) = 0
        [HideInInspector] _texcoord("", 2D) = "white" {}
    }
        SubShader{
            LOD 100
            Tags { "RenderType" = "Opaque" }
            Pass {
                Name "Unlit"
                LOD 100
                Tags { "LIGHTMODE" = "FORWARDBASE" "RenderType" = "Opaque" }
                GpuProgramID 42951
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
                // $Globals ConstantBuffers for Fragment Shader
                // Custom ConstantBuffers for Vertex Shader
                CBUFFER_START(GorillaHalloween22CandleFlameWobble)
                    float _ScrollUOffset;
                CBUFFER_END
                    // Custom ConstantBuffers for Fragment Shader
                    // Texture params for Vertex Shader
                    sampler2D _FlameWobbleNoise;
                // Texture params for Fragment Shader
                sampler2D _MainTex;

                // Keywords: 
                v2f vert(appdata_full v)
                {
                    v2f o;
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    tmp0.xy = unity_ObjectToWorld._m10_m10 * unity_MatrixV._m01_m21;
                    tmp0.xy = unity_MatrixV._m00_m20 * unity_ObjectToWorld._m00_m00 + tmp0.xy;
                    tmp0.xy = unity_MatrixV._m02_m22 * unity_ObjectToWorld._m20_m20 + tmp0.xy;
                    tmp0.xy = unity_MatrixV._m03_m23 * unity_ObjectToWorld._m30_m30 + tmp0.xy;
                    tmp0.z = max(abs(tmp0.y), abs(tmp0.x));
                    tmp0.z = 1.0 / tmp0.z;
                    tmp0.w = min(abs(tmp0.y), abs(tmp0.x));
                    tmp0.z = tmp0.z * tmp0.w;
                    tmp0.w = tmp0.z * tmp0.z;
                    tmp1.x = tmp0.w * 0.0208351 + -0.085133;
                    tmp1.x = tmp0.w * tmp1.x + 0.180141;
                    tmp1.x = tmp0.w * tmp1.x + -0.3302995;
                    tmp0.w = tmp0.w * tmp1.x + 0.999866;
                    tmp1.x = tmp0.w * tmp0.z;
                    tmp1.x = tmp1.x * -2.0 + 1.570796;
                    tmp1.y = abs(tmp0.y) < abs(tmp0.x);
                    tmp1.x = tmp1.y ? tmp1.x : 0.0;
                    tmp0.z = tmp0.z * tmp0.w + tmp1.x;
                    tmp0.w = tmp0.y < -tmp0.y;
                    tmp0.w = tmp0.w ? -3.141593 : 0.0;
                    tmp0.z = tmp0.w + tmp0.z;
                    tmp0.w = min(tmp0.y, tmp0.x);
                    tmp0.x = max(tmp0.y, tmp0.x);
                    tmp0.x = tmp0.x >= -tmp0.x;
                    tmp0.y = tmp0.w < -tmp0.w;
                    tmp0.x = tmp0.x ? tmp0.y : 0.0;
                    tmp0.x = tmp0.x ? -tmp0.z : tmp0.z;
                    tmp0.y = _SinTime.y + _SinTime.x;
                    tmp0.y = tmp0.y + _SinTime.z;
                    tmp0.y = tmp0.y + _SinTime.w;
                    tmp0.x = tmp0.y + tmp0.x;
                    tmp0.x = sin(tmp0.x);
                    tmp1.x = cos(tmp0.x);
                    tmp2.z = tmp0.x;
                    tmp2.y = tmp1.x;
                    tmp2.x = -tmp0.x;
                    tmp0.x = _Time.y * 0.5;
                    tmp0.y = -tmp0.x;
                    tmp1.y = v.vertex.z + v.vertex.y;
                    tmp1.x = v.vertex.x;
                    tmp0.x = -_ScrollUOffset;
                    tmp0.xy = tmp0.xy + abs(tmp1.xy);
                    tmp0 = tex2Dlod(_FlameWobbleNoise, float4(tmp0.xy, 0, 0.0));
                    tmp0.xyz = tmp0.xyz * v.color.xxx;
                    tmp0.xyz = tmp0.xyz * float3(0.05, 0.05, 0.05);
                    tmp1.x = dot(tmp2.xy, tmp0.xy);
                    tmp1.z = dot(tmp2.xy, tmp0.xy);
                    tmp1.y = tmp0.y;
                    tmp0.xyz = tmp1.xyz + v.vertex.xyz;
                    tmp1 = tmp0.yyyy * unity_ObjectToWorld._m01_m11_m21_m31;
                    tmp1 = unity_ObjectToWorld._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp0 = unity_ObjectToWorld._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
                    tmp0 = tmp0 + unity_ObjectToWorld._m03_m13_m23_m33;
                    tmp1 = tmp0.yyyy * unity_MatrixVP._m01_m11_m21_m31;
                    tmp1 = unity_MatrixVP._m00_m10_m20_m30 * tmp0.xxxx + tmp1;
                    tmp1 = unity_MatrixVP._m02_m12_m22_m32 * tmp0.zzzz + tmp1;
                    o.position = unity_MatrixVP._m03_m13_m23_m33 * tmp0.wwww + tmp1;
                    o.texcoord1.xy = v.texcoord.xy;
                    o.texcoord1.zw = float2(0.0, 0.0);
                    return o;
                }
                // Keywords: 
                fout frag(v2f inp)
                {
                    fout o;
                    float4 tmp0;
                    tmp0 = tex2D(_MainTex, inp.texcoord1.xy);
                    o.sv_target.xyz = tmp0.xyz;
                    o.sv_target.w = 0.0;
                    return o;
                }
                ENDCG
            }
    }
}