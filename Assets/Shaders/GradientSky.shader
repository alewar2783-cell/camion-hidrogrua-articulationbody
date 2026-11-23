// Flat two-tone sky for URP: a vertical gradient from the horizon to the zenith and a plain
// colour below the horizon. One cheap pass, no textures, works in WebGL 2.
Shader "Truck/GradientSky"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.45, 0.62, 0.78, 1)
        _HorizonColor ("Horizon Color", Color) = (0.86, 0.89, 0.9, 1)
        _GroundColor ("Below Horizon Color", Color) = (0.62, 0.62, 0.6, 1)
        _Exponent ("Gradient Exponent", Float) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _Exponent;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings SkyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                half height = normalize(input.direction).y;
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb,
                    pow(saturate(height), _Exponent));
                return half4(height < 0.0h ? _GroundColor.rgb : sky, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
