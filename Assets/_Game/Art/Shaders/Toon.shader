// StoryQuest toon shader (ARCHITECTURE §13.7). Four passes: banded lighting from the main light only, an inverted-hull
// outline, and URP's own ShadowCaster and DepthOnly bodies. Every scalar and vector property lives in UnityPerMaterial,
// declared once for all passes, so the SRP Batcher stays on. The presenter drives _FlashAmount through a
// MaterialPropertyBlock for two frames per hit and never touches renderer.material. ToonForward also multiplies the
// albedo inside up to two cells of the palette texture's grid (§15.14.3: skin tones, the gold bow) without a second
// texture sample; a material that sets no cell (cell.z == 0) renders as before. Hero materials enable _SKIN_TINT
// (§15.18.2, §15.18.8): ToonForward samples Synty's skin mask once more and multiplies the albedo inside its skin and
// stubble regions by the tone, so the male chin reads as skin.
Shader "StoryQuest/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.6, 0.6, 0.6, 1)
        _RampThreshold ("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmooth ("Ramp Smooth", Range(0.001, 0.5)) = 0.05
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.05, 1)
        _OutlineWidth ("Outline Width (object-space m)", Range(0, 0.05)) = 0.015
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _CellA ("Tint cell A (col, row, cols, rows)", Vector) = (0, 0, 0, 0)
        _CellAMul ("Tint cell A multiplier, linear", Vector) = (1, 1, 1, 1)
        _CellB ("Tint cell B (col, row, cols, rows)", Vector) = (0, 0, 0, 0)
        _CellBMul ("Tint cell B multiplier, linear", Vector) = (1, 1, 1, 1)
        // White means "no skin, no stubble" (the mask is black on white), so a material that sets no mask is unaffected.
        _SkinMask ("Skin mask (skin where red < 0.5, stubble where green < 0.5), linear", 2D) = "white" {}
        _SkinTint ("Skin tint multiplier, linear", Vector) = (1, 1, 1, 1)
        _StubbleTint ("Stubble tint multiplier, linear", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half _RampThreshold;
            half _RampSmooth;
            half4 _OutlineColor;
            half _OutlineWidth;
            half4 _FlashColor;
            half _FlashAmount;
            float4 _CellA;
            half4 _CellAMul;
            float4 _CellB;
            half4 _CellBMul;
            half4 _SkinTint;
            half4 _StubbleTint;
        CBUFFER_END
        ENDHLSL

        // Pass 1: main light with its shadow attenuation, two-band ramp, flat SH fill, flash lerp. No fog, no additional lights.
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            // URP sets _MAIN_LIGHT_SHADOWS for one cascade and _MAIN_LIGHT_SHADOWS_CASCADE for more; §13.5 uses one,
            // the cascade variant keeps the shader correct if that setting ever changes. No soft shadows, no screen-space.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma shader_feature_local_fragment _SKIN_TINT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SkinMask);
            SAMPLER(sampler_SkinMask);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD3;
            #endif
            };

            half3 CellTint(float2 uv, float4 cell, half4 mul)
            {
                float2 grid = max(cell.zw, 1);
                float2 c = min(floor(float2(uv.x, 1 - uv.y) * grid), grid - 1);   // row 0 is the image's top row
                return (cell.z > 0 && all(c == cell.xy)) ? mul.rgb : half3(1, 1, 1);
            }

            Varyings ToonVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(positionInputs);
            #endif
                return output;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                // The textures clamp, so saturate makes the cell test match the texel that was sampled.
                float2 uv = saturate(input.uv);
            #if defined(_SKIN_TINT)
                // The inversions live here, not in the import: 1 - x is one instruction, an inverted import is pixel work.
                half4 m = SAMPLE_TEXTURE2D(_SkinMask, sampler_SkinMask, uv);
                half skin = 1.0h - m.r;      // Mask_03.r, black on white
                half stubble = 1.0h - m.g;   // Mask_03.b swizzled to G at import; black on white as SyntyImportCheck measured
                // Stubble wins where both are set.
                albedo *= lerp(lerp(half3(1, 1, 1), _SkinTint.rgb, skin), _StubbleTint.rgb, stubble);
            #endif
                albedo = saturate(albedo * CellTint(uv, _CellA, _CellAMul) * CellTint(uv, _CellB, _CellBMul));
                half3 normalWS = normalize(input.normalWS);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                float4 shadowCoord = float4(0, 0, 0, 0);
            #endif
                Light mainLight = GetMainLight(shadowCoord);

                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half band = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth,
                                       ndotl * mainLight.shadowAttenuation);
                half3 col = albedo * lerp(_ShadeColor.rgb, half3(1, 1, 1), band) * mainLight.color
                          + albedo * SampleSH(normalWS);
                col = lerp(col, _FlashColor.rgb, _FlashAmount);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Pass 2: inverted hull. Back faces pushed out along the object-space normal, flat colour.
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        // Pass 3: URP's shadow caster body.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // Directional and punctual lights bias the caster differently; part of URP's standard pass.
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Pass 4: URP's depth-only body.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
