Shader "Hidden/Yetman/PostProcess/EdgeDetection"
{
    HLSLINCLUDE
    #include "Packages/com.yetman.render-pipelines.universal.postprocess/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE2D_X(_MainTex);
    float4 _MainTex_TexelSize;

    // The blending factor for the edges with the original color
    float _Intensity;
    // The threshold ranges for edge detection (xy used for normals, zw used for depth)
    float4 _Threshold;
    // The thickness of the edge (determines how far the neighbour samples spread around the center sample)
    float _Thickness;
    // The color of the edge
    float3 _Color;

    // Curtom Scene normal and depth sampling function to read from a custom texture and sampler
    float3 SampleSceneNormals(float2 uv, TEXTURE2D_X_FLOAT(_Texture), SAMPLER(sampler_Texture))
    {
        return UnpackNormalOctRectEncode(SAMPLE_TEXTURE2D_X(_Texture, sampler_Texture, UnityStereoTransformScreenSpaceTex(uv)).xy) * float3(1.0, 1.0, -1.0);
    }

    float SampleSceneDepth(float2 uv, TEXTURE2D_X_FLOAT(_Texture), SAMPLER(sampler_Texture))
    {
        return SAMPLE_TEXTURE2D_X(_Texture, sampler_Texture, UnityStereoTransformScreenSpaceTex(uv)).r;
    }

    // A Bilinear sampler to allow for subpixel thickness for the edge 
    SAMPLER(sampler_linear_clamp);

    // Sample normal & depth and combine them to a single 4D vector (xyz for normal, w for depth)
    float4 SampleSceneDepthNormal(float2 uv){
        float depth = SampleSceneDepth(uv, _CameraDepthTexture, sampler_linear_clamp);
        float depthEye = LinearEyeDepth(depth, _ZBufferParams);
        float3 normal = SampleSceneNormals(uv, _CameraNormalsTexture, sampler_linear_clamp);
        return float4(normal, depthEye);
    }

    // Read the 8 surrounding samples and average them with perspective correction
    // The perspective correction helps when the surface normal is almost orthogonal to the view direction
    float4 SampleNeighborhood(float2 uv, float thickness){
        // The surrounding pixel offsets
        const float2 offsets[8] = {
            float2(-1, -1),
            float2(-1, 0),
            float2(-1, 1),
            float2(0, -1),
            float2(0, 1),
            float2(1, -1),
            float2(1, 0),
            float2(1, 1)
        };
        
        float2 delta = _MainTex_TexelSize.xy * thickness;
        float4 sum = 0;
        float weight = 0;
        for(int i=0; i<8; i++){
            float4 sample = SampleSceneDepthNormal(uv + delta * offsets[i]);
            // The sum is weight by 1/depth for perspecive correction
            sum += sample / sample.w;
            weight += 1/sample.w;
        }
        sum /= weight;
        // Doesn't make a visible difference but it feels more correct
        // May remove it for performance benefits
        sum.xyz = normalize(sum.xyz);
        return sum;
    }

    float4 EdgeDetectionFragmentProgram (PostProcessVaryings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
        float4 color = LOAD_TEXTURE2D_X(_MainTex, uv * _ScreenSize.xy);
        
        float4 center = SampleSceneDepthNormal(uv);
        float4 neighborhood = SampleNeighborhood(uv,  _Thickness);
        // Normal similarity is calculated using a dot product
        float normalSame = smoothstep(_Threshold.x, _Threshold.y, dot(center.xyz, neighborhood.xyz));
        // Depth similarity is calculated using absolute difference
        float depthSame = smoothstep(_Threshold.w * center.w, _Threshold.z * center.w, abs(center.w - neighborhood.w));
        // Combine normal and depth sameness to get edge factor
        float edge = 1 - normalSame * depthSame;

        color.rgb = lerp(color.rgb, _Color, edge * _Intensity);
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
            #pragma fragment EdgeDetectionFragmentProgram
            ENDHLSL
        }
    }
    Fallback Off
}
