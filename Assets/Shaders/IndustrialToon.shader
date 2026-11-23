// Industrial toon shading for URP (WebGL 2 friendly).
// - UniversalForward: flat colour quantized into a few bands from the main light and its shadow.
//   Optional procedural hazard stripes (no texture) in object space scaled to metres.
// - Outline (SRPDefaultUnlit): inverted hull. The hull inflates Unity's unit primitives (cube and
//   cylinder) uniformly in world space, so long thin boxes keep an even line and box corners
//   do not split. The width is given in screen pixels and clamped in metres.
// - ShadowCaster, DepthOnly and DepthNormals so the object casts shadows and works with SSAO.
// The outline pass is turned off per material with Material.SetShaderPassEnabled.
Shader "Truck/IndustrialToon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.35, 0.38, 0.45, 1)
        _Bands ("Light Bands", Range(2, 6)) = 4
        _StripeColor ("Stripe Color", Color) = (0.08, 0.08, 0.08, 1)
        _StripeMix ("Stripe Mix", Range(0, 1)) = 0
        _StripeFrequency ("Stripes Per Metre", Float) = 3
        _OutlineColor ("Outline Color", Color) = (0.06, 0.06, 0.07, 1)
        _OutlinePixels ("Outline Width (px)", Float) = 2
        _OutlineMaxWidth ("Outline Max Width (m)", Float) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half _Bands;
            half4 _StripeColor;
            half _StripeMix;
            float _StripeFrequency;
            half4 _OutlineColor;
            float _OutlinePixels;
            float _OutlineMaxWidth;
        CBUFFER_END

        float3 ObjectScale()
        {
            return float3(length(UNITY_MATRIX_M._m00_m10_m20),
                length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float stripeCoord : TEXCOORD2;
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 metres = input.positionOS.xyz * ObjectScale();
                output.stripeCoord = (metres.x + metres.y + metres.z) * _StripeFrequency;
                return output;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 normalWS = normalize(input.normalWS);
                half light = saturate(dot(normalWS, mainLight.direction))
                    * mainLight.shadowAttenuation;
                half bands = max(_Bands, 2.0h);
                half band = min(floor(light * bands), bands - 1.0h) / (bands - 1.0h);

                float edge = fwidth(input.stripeCoord);
                float distanceToCenter = abs(frac(input.stripeCoord) - 0.5);
                half stripe = smoothstep(0.25 - edge, 0.25 + edge, distanceToCenter) * _StripeMix;
                half3 albedo = lerp(_BaseColor.rgb, _StripeColor.rgb, stripe);

                half3 shade = lerp(_ShadeColor.rgb, mainLight.color, band);
                return half4(albedo * shade, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                // Unit cube corners sit at +-0.5 and the unit cylinder rim at radius 0.5 with
                // caps at y +-1: doubling and clamping gives the direction that inflates both
                // shapes by the same amount on every face.
                float3 directionOS = clamp(input.positionOS.xyz * 2.0, -1.0, 1.0);
                float3 directionWS = mul((float3x3)UNITY_MATRIX_M, directionOS / ObjectScale());
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float viewDistance = distance(positionWS, GetCameraPositionWS());
                // The projection is Y-flipped when rendering into a texture, hence abs().
                float metresPerPixel = 2.0 * viewDistance
                    / (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y);
                float width = min(_OutlinePixels * metresPerPixel, _OutlineMaxWidth);
                output.positionCS = TransformWorldToHClip(positionWS + directionWS * width);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                return _OutlineColor;
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
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
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
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFragment(Varyings input) : SV_Target
            {
                return input.positionCS.z;
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
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
