Shader "Gorilla/Halloween22/HornsSlingshotElastic" {
    Properties{
        _TintColor("Tint Color", Vector) = (0.5,0.5,0.5,0.5)
        [SingleLineTexture][NoScaleOffset] _Masks("Masks", 2D) = "white" {}
        _GradientStop1("GradientStop1", Float) = 0.98
        _GradientStop2("GradientStop2", Float) = 0.9
        _GradientStop3("GradientStop3", Float) = 0.6
        _Influences("Influences", Vector) = (1,1,1,1)
        [Vec2] _SpeedR("SpeedR", Vector) = (-0.1,-0.1,0,0)
        [Vec2] _SpeedG("SpeedG", Vector) = (-0.5,-0.2,0,0)
        [Vec2] _SpeedB("SpeedB", Vector) = (-0.2,-0.1,0,0)
        [Vec2] _SpeedA("SpeedA", Vector) = (-2,-0.1,0,0)
        _TimeScale("TimeScale", Float) = 1
        [Vec2] _ScaleOffsetB("ScaleOffsetB", Vector) = (0,0,0,0)
        [Vec2] _ScaleOffsetG("ScaleOffsetG", Vector) = (3,1,0,0)
        [Vec2] _ScaleOffsetR("ScaleOffsetR", Vector) = (4,1,0,0)
        [Toggle(_FADE_START_EDGE)] _FADE_START_EDGE("Fade Start Edge", Float) = 1
        [ASEEnd][Toggle(_FADE_END_EDGE)] _FADE_END_EDGE("Fade End Edge", Float) = 0
    }
        SubShader{
            Tags { "IGNOREPROJECTOR" = "true" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderType" = "Transparent" }
            Pass {
                Tags { "IGNOREPROJECTOR" = "true" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderType" = "Transparent" }
                Blend SrcAlpha One, SrcAlpha One
                ZWrite Off
                Cull Off
                GpuProgramID 753
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                struct v2f
                {
                    float4 position : SV_POSITION0;
                    float4 color : COLOR0;
                    float2 texcoord : TEXCOORD0;
                    float4 texcoord3 : TEXCOORD3;
                };
                struct fout
                {
                    float4 sv_target : SV_Target0;
                };
                // $Globals ConstantBuffers for Vertex Shader
                float4 _TintColor;
                float _TimeScale;
                float2 _SpeedA;
                // $Globals ConstantBuffers for Fragment Shader
                float4 _Influences;
                float2 _SpeedR;
                float2 _ScaleOffsetR;
                float2 _SpeedG;
                float2 _ScaleOffsetG;
                float2 _SpeedB;
                float2 _ScaleOffsetB;
                float _GradientStop1;
                float _GradientStop2;
                float _GradientStop3;
                // Custom ConstantBuffers for Vertex Shader
                // Custom ConstantBuffers for Fragment Shader
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
                    o.color = v.color * _TintColor;
                    o.texcoord.xy = v.texcoord.xy;
                    tmp0.x = _TimeScale * _Time.y;
                    tmp0.yz = v.texcoord.xy * float2(0.5, 1.0);
                    o.texcoord3.xy = _SpeedA * tmp0.xx + tmp0.yz;
                    o.texcoord3.zw = float2(0.0, 0.0);
                    return o;
                }
                // Keywords: 
                fout frag(v2f inp)
                {
                    fout o;
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    tmp0.x = inp.texcoord.y * _ScaleOffsetB.y + -0.5;
                    tmp0.y = tmp0.x > 0.0;
                    tmp0.x = tmp0.x < 0.0;
                    tmp0.x = tmp0.x - tmp0.y;
                    tmp0.y = floor(tmp0.x);
                    tmp0.zw = inp.texcoord.xy * _ScaleOffsetB;
                    tmp0.x = 1.0;
                    tmp1.x = _TimeScale * _Time.y;
                    tmp1.yz = tmp1.xx * _SpeedB;
                    tmp0.xy = tmp1.yz * tmp0.xy + tmp0.zw;
                    tmp0 = tex2D(_Masks, tmp0.xy);
                    tmp1.y = inp.texcoord.y * _ScaleOffsetR.y + -0.5;
                    tmp1.z = tmp1.y > 0.0;
                    tmp1.y = tmp1.y < 0.0;
                    tmp1.y = tmp1.y - tmp1.z;
                    tmp2.y = floor(tmp1.y);
                    tmp1.yz = tmp1.xx * _SpeedR;
                    tmp1.xw = tmp1.xx * _SpeedG;
                    tmp3.xy = inp.texcoord.xy * _ScaleOffsetR;
                    tmp2.xz = float2(1.0, 1.0);
                    tmp1.yz = tmp1.yz * tmp2.xy + tmp3.xy;
                    tmp3 = tex2D(_Masks, tmp1.yz);
                    tmp0.x = tmp3.x;
                    tmp1.y = inp.texcoord.y * _ScaleOffsetG.y + -0.5;
                    tmp1.z = tmp1.y > 0.0;
                    tmp1.y = tmp1.y < 0.0;
                    tmp1.y = tmp1.y - tmp1.z;
                    tmp2.w = floor(tmp1.y);
                    tmp1.yz = inp.texcoord.xy * _ScaleOffsetG;
                    tmp1.xy = tmp1.xw * tmp2.zw + tmp1.yz;
                    tmp1 = tex2D(_Masks, tmp1.xy);
                    tmp0.y = tmp1.y;
                    tmp1 = tex2D(_Masks, inp.texcoord3.xy);
                    tmp0.w = tmp1.w;
                    tmp0 = tmp0 * _Influences;
                    tmp0.xy = tmp0.yw + tmp0.xz;
                    tmp0.z = inp.texcoord.y * 2.0 + -1.0;
                    tmp0.z = 1.0 - abs(tmp0.z);
                    tmp0.z = tmp0.z * tmp0.z;
                    tmp0.z = tmp0.z * tmp0.z;
                    tmp0.x = saturate(tmp0.x * tmp0.y + tmp0.z);
                    tmp0.yzw = tmp0.xxx + -float3(_GradientStop1.x, _GradientStop2.x, _GradientStop3.x);
                    tmp0.x = saturate(tmp0.x / _GradientStop3);
                    tmp1.x = 1.0 - _GradientStop1;
                    tmp0.y = saturate(tmp0.y / tmp1.x);
                    tmp1 = saturate(inp.color * float4(16.0, 16.0, 16.0, 16.0));
                    tmp2 = float4(1.0, 1.0, 1.0, 1.0) - tmp1;
                    tmp1 = tmp0.yyyy * tmp2 + tmp1;
                    tmp1 = tmp1 - inp.color;
                    tmp2.xy = -float2(_GradientStop2.x, _GradientStop3.x) + float2(_GradientStop1.x, _GradientStop2.x);
                    tmp0.yz = saturate(tmp0.zw / tmp2.xy);
                    tmp1 = tmp0.yyyy * tmp1 + inp.color;
                    tmp2 = inp.color * inp.color;
                    tmp1 = -tmp2 * inp.color + tmp1;
                    tmp2 = tmp2 * inp.color;
                    tmp1 = tmp0.zzzz * tmp1 + tmp2;
                    tmp0.y = tmp0.x * tmp0.x;
                    tmp0.x = tmp0.x * tmp0.y;
                    o.sv_target = tmp0.xxxx * tmp1;
                    return o;
                }
                ENDCG
            }
        }
}