Shader "Hidden/Yetman/PostProcess/GrayAndInvert"
{
    HLSLINCLUDE
    #include "Packages/com.yetman.render-pipelines.universal.postprocess/ShaderLibrary/Core.hlsl"
    // This file contains the "Luminance" which we use to get the grayscale value
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

    TEXTURE2D_X(_MainTex);

    float _GrayBlend;
    float _InvertBlend;

    float4 GrayAndInverFragmentProgram(PostProcessVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
        float4 color = LOAD_TEXTURE2D_X(_MainTex, uv * _ScreenSize.xy);
        
        #if GRAYSCALE_ON
        // Blend between the original and the grayscale color
        color.rgb = lerp(color.rgb, Luminance(color.rgb).xxx, _GrayBlend);
        #endif

        #if INVERT_ON
        // just invert the colors and blend with the original color
        color.rgb = lerp(color.rgb, 1.0 - color.rgb, _InvertBlend);
        #endif
        
        return color;
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            // We compile the shader to multiple versions where Grayscale and Invert can be on or off separately.
            #pragma multi_compile_local_fragment __ GRAYSCALE_ON
            #pragma multi_compile_local_fragment __ INVERT_ON

            #pragma vertex FullScreenTrianglePostProcessVertexProgram
            #pragma fragment GrayAndInverFragmentProgram
            ENDHLSL
        }
    }
    Fallback Off
}
