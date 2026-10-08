// PROTOTYPE — throwaway. See ../README.md.
//
// Variant A, "Neon Izakaya". Emissive-first: every piece lit from inside, a hard rim on the
// edges, and Bloom on so Palette.Glow means something.
Shader "SushiParty/Prototype/Neon"
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
        _Outline     ("Outline", Range(0, 8)) = 0
        _Gloss       ("Gloss", Range(0, 1)) = 0.4
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
            Name "NeonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SPLitVert
            #pragma fragment NeonFrag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "SP_Proto_Common.hlsl"

            // Cold room, warm sign. Near-black ground so the lit pieces float.
            static const float3 NeonGround = float3(0.020, 0.024, 0.055);
            static const float3 NeonSky    = float3(0.090, 0.075, 0.190);

            half4 NeonFrag(SPVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float3 albedo = _BaseColor.rgb;
                float3 emissive = albedo * _Emission;
                float gloss = _Gloss;

                SPSurfaceDetail(input.positionWS, _Seed, albedo, emissive, gloss);

                Light light = SPMainLight(input.positionWS);

                // Wrapped lambert: a fully black facet on a neon sign reads as a hole.
                float ndl = dot(normalWS, light.direction) * 0.5 + 0.5;
                float shadow = lerp(0.55, 1.0, light.shadowAttenuation);

                float3 ambient = SPAmbient(normalWS, NeonGround, NeonSky);
                float3 diffuse = albedo * (ambient + light.color * ndl * shadow * 1.15);

                // The rim is what turns a grey primitive into a tube of light.
                float rim = SPFresnel(normalWS, viewDirWS, 2.6);
                emissive += albedo * rim * 2.4;

                // A tight specular chip, so the glossy surfaces still catch the key light.
                float3 halfWS = normalize(light.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfWS)), lerp(20.0, 180.0, gloss));
                emissive += light.color * spec * gloss * 1.6;

                // What the skinner drives when something in the game has just lit up.
                emissive += _AccentColor.rgb * _Pulse * 5.0;

                return half4(diffuse + emissive, 1.0);
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
