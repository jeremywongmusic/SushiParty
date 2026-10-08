// PROTOTYPE — throwaway. See ../README.md.
//
// Variant C, "Lacquer". Wet specular, light bleeding through the thin parts, and a jelly
// wobble on anything soft — a counter with things on it rather than a lit sign.
Shader "SushiParty/Prototype/Lacquer"
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
        _Gloss       ("Gloss", Range(0, 1)) = 0.62
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
            Name "LacquerForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex SPLitVert
            #pragma fragment LacquerFrag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "SP_Proto_Common.hlsl"

            // Warm room, cool bounce off the counter. Brighter overall than Neon.
            static const float3 LacquerGround = float3(0.075, 0.058, 0.062);
            static const float3 LacquerSky    = float3(0.230, 0.215, 0.255);

            half4 LacquerFrag(SPVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float3 albedo = _BaseColor.rgb;
                float3 emissive = albedo * _Emission * 0.55;
                float gloss = _Gloss;

                SPSurfaceDetail(input.positionWS, _Seed, albedo, emissive, gloss);

                Light light = SPMainLight(input.positionWS);
                float shadow = lerp(0.42, 1.0, light.shadowAttenuation);

                float ndl = saturate(dot(normalWS, light.direction));
                float3 ambient = SPAmbient(normalWS, LacquerGround, LacquerSky);
                float3 diffuse = albedo * (ambient + light.color * ndl * shadow);

                // Wet coat: a tight highlight plus a broad one, so roundness still reads at the
                // silhouette.
                float3 halfWS = normalize(light.direction + viewDirWS);
                float nh = saturate(dot(normalWS, halfWS));
                float tight = pow(nh, lerp(40.0, 300.0, gloss)) * lerp(0.6, 2.2, gloss);

                // Scaled by gloss, or the sheen sits on every floor whatever its material says.
                float broad = pow(nh, 12.0) * 0.18 * gloss;
                float3 specular = light.color * (tight + broad) * shadow;

                // Not real subsurface: light aimed through the piece toward the camera, warmed.
                float through = pow(saturate(dot(viewDirWS, -light.direction)), 3.5);
                float3 sss = albedo * float3(1.15, 0.92, 0.86) * through * 0.55;

                // A soft edge instead of Neon's hard one: this is a sheen, not a tube.
                float rim = SPFresnel(normalWS, viewDirWS, 3.2);
                float3 sheen = lerp(albedo, float3(1.0, 0.97, 0.92), 0.45) * rim * 0.42;

                float3 colour = diffuse + specular + sss + sheen + emissive;
                colour += _AccentColor.rgb * _Pulse * 2.2;

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
