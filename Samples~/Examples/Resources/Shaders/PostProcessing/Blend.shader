Shader "Hidden/Yetman/PostProcess/Blend"
{
    HLSLINCLUDE
    #include "Packages/com.yetman.render-pipelines.universal.postprocess/ShaderLibrary/Core.hlsl"

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_SecondaryTex);

    float3 _Blend;

    float4 BlendFragmentProgram(PostProcessVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
        uint2 positionSS = uv * _ScreenSize.xy;
        float3 mainColor = LOAD_TEXTURE2D_X(_MainTex, positionSS).rgb;
        float3 secondaryColor = LOAD_TEXTURE2D_X(_SecondaryTex, positionSS).rgb;
        // blend the main and secondary color
        float3 color = lerp(mainColor, secondaryColor, _Blend);
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
