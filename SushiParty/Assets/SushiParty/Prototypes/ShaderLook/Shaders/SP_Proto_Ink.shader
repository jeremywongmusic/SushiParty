// PROTOTYPE — throwaway. See ../README.md.
//
// Variant B, "Inkbrush". No bloom, no glow: a hard ink outline and three flat bands of tone.
//
// The outline is an inverted hull expanded from the object's origin rather than along the
// normal — the geometry here is all centred convex primitives, and origin-expansion is the
// one that gives a clean line on a cube.
Shader "SushiParty/Prototype/Ink"
{
    Properties
    {
        _BaseColor   ("Base Colour", Color) = (1, 1, 1, 1)
        _AccentColor ("Accent", Color) = (1, 1, 1, 1)
        _Emission    ("Emission", Range(0, 8)) = 0
        _Pulse       ("Pulse", Range(0, 1)) = 0
        _Surface     ("Surface Mode", Range(0, 3)) = 0
        _Warp        ("Warp", Range(0, 1)) = 0
        _Seed        ("Seed", Float) = 0
        _Outline     ("Outline", Range(0, 8)) = 2.2
        _Gloss       ("Gloss", Range(0, 1)) = 0.2
        _Progress    ("Progress", Range(0, 1)) = 0
        _Mode        ("Mode", Range(0, 3)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "InkOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma target 3.0

            #include "SP_Proto_Common.hlsl"

            SPVaryings OutlineVert(SPAttributes input)
            {
                SPVaryings output;

                float3 positionOS = SPWobble(input.positionOS.xyz, input.normalOS, _Warp, _Seed);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                float3 outward = positionWS - centreWS;
                float len = length(outward);
                outward = len > 1e-5 ? outward / len : normalize(TransformObjectToWorldNormal(input.normalOS));

                // Scaled by view distance to hold its screen thickness, then capped against the
                // piece's own size — uncapped, the line eats an octopus pupil at board range.
                float distance = length(GetCameraPositionWS() - positionWS);
                float width = min(_Outline * distance * 0.0018, len * 0.28);
                positionWS += outward * width;

                output.positionWS = positionWS;
                output.normalWS = outward;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 OutlineFrag(SPVaryings input) : SV_Target
            {
                // Not flat black: the line carries a trace of what it is drawn round.
                float3 ink = lerp(float3(0.035, 0.030, 0.055), _BaseColor.rgb * 0.22, 0.45);
                return half4(ink, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "InkForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SPLitVert
            #pragma fragment InkFrag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "SP_Proto_Common.hlsl"

            static const float3 InkGround = float3(0.13, 0.13, 0.17);
            static const float3 InkSky    = float3(0.42, 0.44, 0.50);

            half4 InkFrag(SPVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float3 albedo = _BaseColor.rgb;
                float3 emissive = 0.0;
                float gloss = _Gloss;

                // Liquid and lacquer still animate here; they just do it in flat tone.
                SPSurfaceDetail(input.positionWS, _Seed, albedo, emissive, gloss);

                Light light = SPMainLight(input.positionWS);

                float ndl = dot(normalWS, light.direction) * 0.5 + 0.5;
                float shade = ndl * lerp(0.62, 1.0, light.shadowAttenuation);

                // Three bands. Two would read as a silhouette, four stops being a print.
                float band = shade < 0.44 ? 0.58 : (shade < 0.70 ? 0.82 : 1.0);

                float3 ambient = SPAmbient(normalWS, InkGround, InkSky);
                float3 colour = albedo * band * (0.55 + ambient);

                // Paper. Screen-space so it sits on the picture rather than on the models.
                float grain = SPNoise(input.positionCS.xy * 0.42);
                colour *= 0.93 + grain * 0.14;

                // A thin bright edge, cut hard, so two overlapping bodies stay separable.
                float rim = SPFresnel(normalWS, viewDirWS, 4.0);
                colour += albedo * step(0.62, rim) * 0.38;

                // No bloom in this variant, so glow materials go to flat wash instead.
                colour = lerp(colour, saturate(albedo * 1.7 + 0.18), saturate(_Emission * 0.42));
                colour += emissive * 0.35;
                colour += _AccentColor.rgb * _Pulse * 0.9;

                return half4(colour, 1.0);
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
            #pragma vertex SPShadowVert
            #pragma fragment SPDepthFrag
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "SP_Proto_Common.hlsl"
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
            #pragma vertex SPDepthVert
            #pragma fragment SPDepthFrag
            #pragma target 3.0
            #include "SP_Proto_Common.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
