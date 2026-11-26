// Shader mắt thú Gen 1 (URP), vẽ bằng toán theo UV của thấu kính mắt (UV = tọa độ mắt, đơn vị nửa bề ngang):
// mống mắt sáng ở giữa -> tối dần ra viền, vân tỏa tròn, đồng tử, 2 đốm sáng long lanh.
// Giống vật liệu mắt của bản render Blender (Tools/Blender/pawfit_kit.py, hàm eye_material).
Shader "PawFit/Gen1Eye"
{
    Properties
    {
        _IrisColor ("Iris", Color) = (0.18, 0.7, 0.76, 1)
        _Side ("Side (+1 phải / -1 trái)", Float) = 1
        _PupilSize ("Pupil size (x, y)", Vector) = (0.2, 0.27, 0, 0)
        _Highlight ("Highlight", Range(0, 4)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _IrisColor;
            float4 _PupilSize;
            half _Side;
            half _Highlight;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float2 d = uv - float2(-0.07 * _Side, -0.04);      // mống mắt lệch vào trong 1 chút
                float rn = length(d) / 0.64;

                // độ sáng theo bán kính (5 mốc, nội suy mượt như ColorRamp EASE của Blender)
                half k = lerp(2.2, 1.45, smoothstep(0.0, 0.42, rn));
                k = lerp(k, 1.0, smoothstep(0.42, 0.78, rn));
                k = lerp(k, 0.28, smoothstep(0.78, 0.93, rn));
                k = lerp(k, 0.035, smoothstep(0.93, 1.0, rn));

                // vân mống mắt tỏa ra (số nguyên lần theo góc => không có đường nối)
                float ang = atan2(d.y, d.x);
                half streak = 0.88 + 0.16 * sin(ang * 14.0 + sin(ang * 5.0) * 2.0) * smoothstep(0.15, 0.55, rn);
                half3 color = _IrisColor.rgb * k * streak;

                // đồng tử
                float pr = length(d / max(_PupilSize.xy, float2(0.01, 0.01)));
                color = lerp(half3(0.006, 0.005, 0.007), color, smoothstep(0.86, 1.0, pr));

                // 2 đốm sáng
                half h1 = 1.0 - smoothstep(0.11, 0.15, distance(uv, float2(-0.30, 0.34)));
                half h2 = 1.0 - smoothstep(0.05, 0.075, distance(uv, float2(0.24, -0.22)));
                half hl = saturate(h1 + h2 * 0.75);

                float3 N = normalize(i.normalWS);
                Light light = GetMainLight();
                half shade = lerp(0.7, 1.08, saturate(dot(N, light.direction) * 0.5 + 0.5));
                color = color * shade * (light.color * 0.5 + SampleSH(N) * 0.5 + 0.2) + hl * _Highlight;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
