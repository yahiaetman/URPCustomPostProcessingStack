#ifndef YETMAN_POSTPROCESS_TRANSPARENT_DECLARE_DEPTH_TEXTURE_INCLUDED
#define YETMAN_POSTPROCESS_TRANSPARENT_DECLARE_DEPTH_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_FLOAT(_CameraTransparentDepthTexture);
SAMPLER(sampler_CameraTransparentDepthTexture);

float SampleSceneTransparentDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_CameraTransparentDepthTexture, sampler_CameraTransparentDepthTexture, UnityStereoTransformScreenSpaceTex(uv)).r;
}

float LoadSceneTransparentDepth(uint2 uv)
{
    return LOAD_TEXTURE2D_X(_CameraTransparentDepthTexture, uv).r;
}
#endif
