// PROTOTYPE — throwaway. See ../README.md.
//
// The effects layer. One additive billboard shader with four shapes on it, all drawn on a
// Unity Quad and shaped in the fragment — one draw call, no simulation.
//
//   _Mode 0  ring    an expanding shockwave — impacts, GO, a switch pair being cleared
//   _Mode 1  mote    a soft dot — sparks, embers, the poof left where something died
//   _Mode 2  beam    a standing column — the shrine, a claim, a win
//   _Mode 3  star    a four-point flash — the single frame something is struck on
//
// _Progress runs 0 to 1 across the flourish's life and each shape reads it itself.
Shader "SushiParty/Prototype/Flourish"
{
    Properties
    {
        _BaseColor   ("Base Colour", Color) = (1, 1, 1, 1)
        _AccentColor ("Accent", Color) = (1, 1, 1, 1)
        _Emission    ("Emission", Range(0, 8)) = 1
        _Pulse       ("Pulse", Range(0, 1)) = 0
        _Surface     ("Surface Mode", Range(0, 3)) = 0
        _Warp        ("Warp", Range(0, 1)) = 0
        _Seed        ("Seed", Float) = 0
        _Outline     ("Outline", Range(0, 8)) = 0
        _Gloss       ("Gloss", Range(0, 1)) = 0
        _Progress    ("Progress", Range(0, 1)) = 0
        _Mode        ("Mode", Range(0, 3)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Flourish"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex FlourishVert
            #pragma fragment FlourishFrag
            #pragma target 3.0

            #include "SP_Proto_Common.hlsl"

            struct FlourishAttributes
            {
                float4 positionOS : POSITION;
            };

            struct FlourishVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            FlourishVaryings FlourishVert(FlourishAttributes input)
            {
                FlourishVaryings output;

                float3 centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                // Size comes off the object's matrix, so the spawner only scales the quad.
                float sizeX = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float sizeY = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));

                float3 right = float3(UNITY_MATRIX_V._m00, UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m02);
                float3 up = float3(UNITY_MATRIX_V._m10, UNITY_MATRIX_V._m11, UNITY_MATRIX_V._m12);

                // A beam stands up in the world and only spins to face you; everything
                // else is flat on to the camera.
                if (_Mode > 1.5 && _Mode < 2.5)
                {
                    float3 toCamera = GetCameraPositionWS() - centreWS;
                    up = float3(0.0, 1.0, 0.0);
                    right = normalize(cross(up, toCamera) + float3(1e-4, 0.0, 0.0));
                }

                float3 positionWS = centreWS
                    + right * (input.positionOS.x * sizeX)
                    + up * (input.positionOS.y * sizeY);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.positionOS.xy + 0.5;
                return output;
            }

            half4 FlourishFrag(FlourishVaryings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 centred = uv - 0.5;
                float radius = length(centred) * 2.0;
                float progress = saturate(_Progress);
                float alpha = 0.0;

                if (_Mode < 0.5)
                {
                    // Ring. Thins as it grows, with a flash in the middle on the first frames.
                    float width = 0.30 * (1.0 - progress * 0.65) + 0.03;
                    float band = saturate(1.0 - abs(radius - progress) / width);
                    alpha = band * band * (1.0 - progress);
                    alpha += pow(saturate(1.0 - radius), 4.0) * saturate(1.0 - progress * 5.0);
                }
                else if (_Mode < 1.5)
                {
                    // Mote.
                    alpha = pow(saturate(1.0 - radius), 3.0) * (1.0 - progress);
                }
                else if (_Mode < 2.5)
                {
                    // Beam, with light climbing it.
                    float across = 1.0 - saturate(abs(centred.x) * 2.0);
                    float climb = 0.72 + 0.38 * sin(uv.y * 16.0 - _Time.y * 6.0);
                    alpha = pow(across, 2.5) * (1.0 - uv.y * 0.80) * climb;
                    alpha *= sin(progress * 3.14159265);
                }
                else
                {
                    // Star: two crossed spikes plus a hot core.
                    float2 arm = abs(centred) * 2.0;
                    float spikes = saturate(1.0 - min(arm.x, arm.y) * 9.0) * saturate(1.0 - max(arm.x, arm.y));
                    float core = pow(saturate(1.0 - radius), 6.0);
                    alpha = (spikes + core) * (1.0 - progress);
                }

                alpha = saturate(alpha);

                // White-hot at the head, settling into its own colour.
                float3 colour = lerp(float3(1.0, 0.98, 0.94), _BaseColor.rgb, saturate(progress * 2.2));
                colour *= _Emission;

                return half4(colour * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
