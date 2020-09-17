#ifndef YETMAN_POSTPROCESS_NORMALS_INPUT_INCLUDED
#define YETMAN_POSTPROCESS_NORMALS_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _Cutoff;
CBUFFER_END

#endif