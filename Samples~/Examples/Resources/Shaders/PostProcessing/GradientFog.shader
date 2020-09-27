Shader "Hidden/Yetman/PostProcess/GradientFog"
{
    HLSLINCLUDE
    #include "Packages/com.yetman.render-pipelines.universal.postprocess/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    
    #if AFTER_TRANSPARENT_ON
    #include "Packages/com.yetman.render-pipelines.universal.postprocess/ShaderLibrary/DeclareTransparentDepthTexture.hlsl"
    #endif

    TEXTURE2D_X(_MainTex);

    float _Intensity;
    float _Exponent;

    // The near and far color distance
    float2 _ColorRange;
    float3 _NearFogColor;
    float3 _FarFogColor;

    // The value from the depth buffer must be remapped from [0,1]
    // to [-1,1] before computing view space position 
    float ConvertZBufferToDeviceDepth(float z){
        #if UNITY_REVERSED_Z
            return 1 - 2 * z;
        #else
            return 2 * z - 1;
        #endif 
    }

    float4 GradientFogFragmentProgram(PostProcessVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

        uint2 positionSS = uv * _ScreenSize.xy;

        float depth = LoadSceneDepth(positionSS);

        #if AFTER_TRANSPARENT_ON
        // If the fog is applied after the transparent pass,
        // the depth is sampled from both the opaque and transparent depth textures
        // and the nearest depth is selected.
        // TODO: This can be optimized to read from one texture if the scene transparent depth includes the opaque depth 
        float transparentDepth = LoadSceneTransparentDepth(positionSS);
            #if UNITY_REVERSED_Z
                depth = max(depth, transparentDepth);
            #else
                depth = min(depth, transparentDepth);
            #endif
        #endif

        float deviceDepth = ConvertZBufferToDeviceDepth(depth);
        float3 viewPos = ComputeViewSpacePosition(uv, deviceDepth, unity_CameraInvProjection);
        float distance = length(viewPos);

        float fogFactor = 1.0 - exp(- _Exponent * distance); // exponential fog
        float3 fogColor = lerp(_NearFogColor, _FarFogColor, smoothstep(_ColorRange.x, _ColorRange.y, distance));

        float4 color = LOAD_TEXTURE2D_X(_MainTex, positionSS);
        
        color.rgb = lerp(color.rgb, fogColor, _Intensity * fogFactor);
        return color;
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_local_fragment __ AFTER_TRANSPARENT_ON

            #pragma vertex FullScreenTrianglePostProcessVertexProgram
            #pragma fragment GradientFogFragmentProgram
            ENDHLSL
        }
    }
    Fallback Off
}
