// PROTOTYPE — throwaway. See ../README.md.
//
// Shared by the three look variants: the property block, a hemisphere ambient term, the
// surface treatments, and the shadow/depth passes. No SampleSH and no BRDF — one
// directional light over a near-black background does not need either.

#ifndef SP_PROTO_COMMON_INCLUDED
#define SP_PROTO_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Keep the order identical in every Properties block or the SRP Batcher drops the lot.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _AccentColor;
    float  _Emission;
    float  _Pulse;
    float  _Surface;
    float  _Warp;
    float  _Seed;
    float  _Outline;
    float  _Gloss;
    float  _Progress;
    float  _Mode;
CBUFFER_END

float3 _LightDirection;

// ---- little maths --------------------------------------------------------------------

float SPHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float SPNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = SPHash(i);
    float b = SPHash(i + float2(1.0, 0.0));
    float c = SPHash(i + float2(0.0, 1.0));
    float d = SPHash(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float SPFresnel(float3 normalWS, float3 viewDirWS, float power)
{
    return pow(saturate(1.0 - saturate(dot(normalWS, viewDirWS))), power);
}

// Hemisphere ambient. Each variant passes its own pair.
float3 SPAmbient(float3 normalWS, float3 ground, float3 sky)
{
    return lerp(ground, sky, saturate(normalWS.y * 0.5 + 0.5));
}

// The main light, with shadows when the keywords are on.
Light SPMainLight(float3 positionWS)
{
#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    return GetMainLight(TransformWorldToShadowCoord(positionWS));
#else
    return GetMainLight();
#endif
}

// Jelly. Two out-of-phase sine trains along the normal, so a sphere breathes rather than
// pulsing uniformly. Zero cost when _Warp is 0.
float3 SPWobble(float3 positionOS, float3 normalOS, float amount, float seed)
{
    if (amount < 0.0001)
    {
        return positionOS;
    }

    float t = _Time.y * 2.4 + seed * 6.283;
    float wave = sin(t + positionOS.y * 7.0) * 0.6 + sin(t * 1.37 + positionOS.x * 5.5) * 0.4;
    return positionOS + normalOS * (wave * amount * 0.05);
}

// _Surface comes from the skinner, off the object's name:
// 0 solid, 1 liquid, 2 lacquered plate, 3 energy.

void SPSurfaceDetail(
    float3 positionWS,
    float seed,
    inout float3 albedo,
    inout float3 emissive,
    inout float gloss)
{
    if (_Surface < 0.5)
    {
        return;
    }

    float t = _Time.y;

    if (_Surface < 1.5)
    {
        // Liquid — two crossing wave trains, and a sparkle that only sits on the crests.
        float2 p = positionWS.xz * 0.35;
        float wave = sin(p.x * 1.7 + t * 1.1) * 0.5 + sin(p.y * 2.3 - t * 0.8) * 0.5;
        float crest = saturate(wave * 0.5 + 0.5);
        float ripple = SPNoise(p * 1.6 + float2(t * 0.15, -t * 0.11));

        albedo = lerp(albedo * 0.70, albedo * 1.40, crest);
        emissive += albedo * pow(saturate(ripple * crest), 6.0) * 2.5;
        gloss = max(gloss, 0.88);
    }
    else if (_Surface < 2.5)
    {
        // Plate — the counter, the floors, the platforms. Matte on purpose: anything that
        // moves down here competes with the game for the whole round. The grain is static and
        // stays, or a thirty-unit counter is one flat colour with no sense of scale.
        float grain = SPNoise(positionWS.xz * 3.1) * 0.08;
        albedo *= 0.94 + grain;

        // Forced down, so no variant puts a highlight on the floor either.
        gloss = min(gloss, 0.10);
    }
    else
    {
        // Energy — current chasing round the piece. Bumper Sparks' ring, mostly.
        float around = atan2(positionWS.z, positionWS.x) * 0.1591549;
        float flow = frac(around * 3.0 + positionWS.y * 0.8 - t * 1.6 + seed);
        float arc = pow(saturate(1.0 - abs(flow - 0.5) * 4.0), 4.0);

        emissive += albedo * (1.2 + arc * 5.0);
        gloss = max(gloss, 0.55);
    }
}

// ---- shared passes -------------------------------------------------------------------

struct SPDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
};

struct SPDepthVaryings
{
    float4 positionCS : SV_POSITION;
};

SPDepthVaryings SPShadowVert(SPDepthAttributes input)
{
    SPDepthVaryings output;

    float3 positionOS = SPWobble(input.positionOS.xyz, input.normalOS, _Warp, _Seed);
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    output.positionCS = positionCS;
    return output;
}

SPDepthVaryings SPDepthVert(SPDepthAttributes input)
{
    SPDepthVaryings output;
    float3 positionOS = SPWobble(input.positionOS.xyz, input.normalOS, _Warp, _Seed);
    output.positionCS = TransformObjectToHClip(positionOS);
    return output;
}

half4 SPDepthFrag(SPDepthVaryings input) : SV_Target
{
    return 0;
}

// ---- the one lit varyings struct all three skins use ---------------------------------

struct SPAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
};

struct SPVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
};

SPVaryings SPLitVert(SPAttributes input)
{
    SPVaryings output;

    float3 positionOS = SPWobble(input.positionOS.xyz, input.normalOS, _Warp, _Seed);
    output.positionWS = TransformObjectToWorld(positionOS);
    output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
    output.positionCS = TransformWorldToHClip(output.positionWS);

    return output;
}

#endif
