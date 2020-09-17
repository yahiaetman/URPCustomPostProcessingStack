﻿Shader "Hidden/Blend"
{
    HLSLINCLUDE
    #include "Packages/com.yetman.render-pipelines.universal.postprocessing/ShaderLibrary/Core.hlsl"

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_SecondaryTex);

    float3 _blend;

    float4 BlendFragmentProgram(PostProcessVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
        uint2 positionSS = uv * _ScreenParams.xy;
        float3 mainColor = LOAD_TEXTURE2D_X(_MainTex, positionSS).rgb;
        float3 secondaryColor = LOAD_TEXTURE2D_X(_SecondaryTex, positionSS).rgb;
        // blend the main and secondary color
        float3 color = lerp(mainColor, secondaryColor, _blend);
        return float4(color, 1);
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex FullScreenTrianglePostProcessVertexProgram
            #pragma fragment BlendFragmentProgram
            ENDHLSL
        }
    }
    Fallback Off
}
