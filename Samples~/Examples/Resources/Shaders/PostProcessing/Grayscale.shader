Shader "Hidden/Yetman/PostProcess/Grayscale"
{
    HLSLINCLUDE
    #include "Packages/com.yetman.render-pipelines.universal.postprocess/ShaderLibrary/Core.hlsl"
    // This file contains the "Luminance" which we use to get the grayscale value
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

    TEXTURE2D_X(_MainTex);

    float _Blend;

    float4 GrayscaleFragmentProgram (PostProcessVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
        float4 color = LOAD_TEXTURE2D_X(_MainTex, uv * _ScreenSize.xy);
        
        // Blend between the original and the grayscale color
        color.rgb = lerp(color.rgb, Luminance(color.rgb).xxx, _Blend);
        
        return color;
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex FullScreenTrianglePostProcessVertexProgram
            #pragma fragment GrayscaleFragmentProgram
            ENDHLSL
        }
    }
    Fallback Off
}
