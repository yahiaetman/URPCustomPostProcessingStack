﻿using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

// This Custom Post Process Renderer serves as an example for
// creating an Uber renderer for multiple effects to enhance performance.

// NOTE: We will use the same volume component used by the GrayScale and Invert Effects

// Define the renderer for the custom post processing effect
[CustomPostProcess("Grayscale & Invert", CustomPostProcessInjectPoint.AfterPostProcess),]
public class GrayAndInvertEffectRenderer : CustomPostProcessRenderer
{
    // Here, we will get 2 components so we create a reference for each of them
    private GrayscaleEffect m_GrayScaleComponent;
    private InvertEffect m_InvertComponent;

    // The postprocessing material (you can define as many as you like)
    private Material m_Material;
    
    // The ids of the shader variables
    static class ShaderIDs {
        internal readonly static int Input = Shader.PropertyToID("_MainTex");
        internal readonly static int GrayBlend = Shader.PropertyToID("_grayBlend");
        internal readonly static int InvertBlend = Shader.PropertyToID("_invertBlend");
    }
    
    // By default, the effect is visible in the scene view, but we can change that here.
    public override bool visibleInSceneView => true;
    
    // Setup is called once so we use it to create our material
    public override void Setup()
    {
        m_Material = CoreUtils.CreateEngineMaterial("Hidden/GrayAndInvert");
    }

    // Called once before rendering. Return true if the effect should be rendered for this camera.
    public override bool SetupCamera(ref RenderingData renderingData)
    {
        // Get the current volume stack
        var stack = VolumeManager.instance.stack;
        // Get the 2 volume components
        m_GrayScaleComponent = stack.GetComponent<GrayscaleEffect>();
        m_InvertComponent = stack.GetComponent<InvertEffect>();
        // if blend value > 0 for any of the 2 components, then we need to render this effect. 
        return m_GrayScaleComponent.blend.value > 0 || m_InvertComponent.blend.value > 0;
    }

    // The actual rendering execution is done here
    public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
    {
        // set material properties
        if(m_Material != null){
            float grayBlend = m_GrayScaleComponent.blend.value;
            if(grayBlend > 0) {
                m_Material.EnableKeyword("GRAYSCALE_ON"); 
                m_Material.SetFloat(ShaderIDs.GrayBlend, grayBlend);
            } else { 
                m_Material.DisableKeyword("GRAYSCALE_ON");
            }
            float invertBlend = m_InvertComponent.blend.value;
            if(invertBlend > 0) {
                m_Material.EnableKeyword("INVERT_ON"); 
                m_Material.SetFloat(ShaderIDs.InvertBlend, invertBlend);
            } else { 
                m_Material.DisableKeyword("INVERT_ON");
            }
        }
        // set source texture
        cmd.SetGlobalTexture(ShaderIDs.Input, source);
        // draw a fullscreen triangle to the destination
        CoreUtils.DrawFullScreen(cmd, m_Material, destination);
    }
}
