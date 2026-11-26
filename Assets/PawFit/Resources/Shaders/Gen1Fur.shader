// Shader thân thú Gen 1 (URP): màu lấy từ vertex color của FBX, không cần texture.
//   R = vùng sáng (bụng / mõm / má / ngực; tai: mặt trong)   G = bàn chân / chóp tai / chóp đuôi / chóp sừng
//   B = sọc      A = đốm      UV1.x = vùng lưng (tối hơn)     UV1.y = vết bẩn cố định theo vị trí
// Ánh sáng "bọc" mềm + viền sáng giả lông tơ. Lông thật (shell / strand) làm ở Phase 6.
Shader "PawFit/Gen1Fur"
{
    Properties
    {
        _FurColor ("Fur", Color) = (0.9, 0.6, 0.3, 1)
        _LightColor ("Light area (R)", Color) = (1, 0.95, 0.88, 1)
        _PointsColor ("Paws / tips (G)", Color) = (0.35, 0.24, 0.18, 1)
        _PatternColor ("Pattern", Color) = (0.35, 0.18, 0.1, 1)
        _StripeAmount ("Stripes (B)", Range(0, 1)) = 0
        _SpotAmount ("Spots (A)", Range(0, 1)) = 0
        _DorsalAmount ("Darker back (UV1.x)", Range(0, 1)) = 0.25
        _Dirt ("Dirt", Range(0, 1)) = 0
        _Pale ("Sick pale", Range(0, 1)) = 0
        _DirtColor ("Dirt color", Color) = (0.36, 0.28, 0.2, 1)
        _Fuzz ("Fuzz rim", Range(0, 1)) = 1
        _Wrap ("Wrap lighting", Range(0, 1)) = 0.5
        _Gloss ("Gloss", Range(0, 1)) = 0.06
        _Emission ("Emission", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _FurColor;
            half4 _LightColor;
            half4 _PointsColor;
            half4 _PatternColor;
            half4 _DirtColor;
            half4 _Emission;
            half _StripeAmount;
            half _SpotAmount;
            half _DorsalAmount;
            half _Dirt;
            half _Pale;
            half _Fuzz;
            half _Wrap;
            half _Gloss;
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv1 : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv1 = v.uv1;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // màu lông theo vùng: lông -> hoa văn -> chân / chóp -> vùng sáng
                half3 albedo = _FurColor.rgb;
                half pattern = saturate(i.color.b * _StripeAmount + i.color.a * _SpotAmount);
                albedo = lerp(albedo, _PatternColor.rgb, pattern);
                albedo = lerp(albedo, _PointsColor.rgb, i.color.g);
                albedo = lerp(albedo, _LightColor.rgb, i.color.r);
                albedo *= 1.0 - 0.3 * _DorsalAmount * (half)i.uv1.x;

                // bẩn: lông xỉn đi + mảng lấm lem (vết cố định theo đỉnh nên không trôi khi thú cử động)
                half dirtEdge = 1.0 - 0.55 * _Dirt;
                half blotch = smoothstep(dirtEdge, dirtEdge + 0.1, (half)i.uv1.y) * step(0.001, _Dirt);
                albedo = lerp(albedo, albedo * half3(0.8, 0.76, 0.7), _Dirt * 0.6);
                albedo = lerp(albedo, _DirtColor.rgb, blotch * 0.85);

                // ốm: lông nhợt, ngả xám xanh
                half lum = dot(albedo, half3(0.3, 0.59, 0.11));
                albedo = lerp(albedo, lum * half3(0.9, 1.0, 0.92) + 0.06, _Pale * 0.6);

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half atten = light.shadowAttenuation * light.distanceAttenuation;

                half ndl = dot(N, light.direction);
                half wrap = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half3 diffuse = albedo * light.color * (wrap * atten);
                half3 ambient = albedo * SampleSH(N);

                // viền sáng giả lông tơ (càng nhìn xiên càng sáng)
                half fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                half3 fuzz = fres * _Fuzz * lerp(albedo, half3(1.0, 1.0, 1.0), 0.45)
                             * (SampleSH(N) * 0.8 + light.color * (0.35 * atten));

                half3 H = SafeNormalize(light.direction + V);
                half spec = pow(saturate(dot(N, H)), 24.0) * _Gloss * atten;

                half3 color = diffuse + ambient + fuzz + spec * light.color + _Emission.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return 0;
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

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct NormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormalsVaryings NormalsVert(NormalsAttributes v)
            {
                NormalsVaryings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 NormalsFrag(NormalsVaryings i) : SV_Target
            {
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 n = normalize(i.normalWS);
                float2 oct = PackNormalOctQuadEncode(n);
                half3 packed = PackFloat2To888(saturate(oct * 0.5 + 0.5));
                return half4(packed, 0.0);
            #else
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
