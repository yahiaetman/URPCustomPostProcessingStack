﻿using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

// Define the Volume Component for the custom post processing effect 
[System.Serializable, VolumeComponentMenu("CustomPostProcess/Grayscale")]
public class GrayscaleEffect : VolumeComponent
{
    [Tooltip("Controls the blending between the original and the grayscale color.")]
    public ClampedFloatParameter blend = new ClampedFloatParameter(0, 0, 1);
}

// Define the renderer for the custom post processing effect
[CustomPostProcess("Grayscale", CustomPostProcessInjectPoint.AfterPostProcess),]
public class GrayscaleEffectRenderer : CustomPostProcessRenderer
{
    // A variable to hold a reference to the corresponding volume component (you can define as many as you like)
    private GrayscaleEffect m_VolumeComponent;

    // The postprocessing material (you can define as many as you like)
    private Material m_Material;
    
    // The ids of the shader variables
    static class ShaderIDs {
        internal readonly static int Input = Shader.PropertyToID("_MainTex");
        internal readonly static int Blend = Shader.PropertyToID("_blend");
    }
    
    // By default, the effect is visible in the scene view, but we can change that here.
    public override bool visibleInSceneView => true;
    
    // Setup is called once so we use it to create our material
    public override void Setup()
    {
        m_Material = CoreUtils.CreateEngineMaterial("Hidden/Grayscale");
    }

    // Called once before rendering. Return true if the effect should be rendered for this camera.
    public override bool SetupCamera(ref RenderingData renderingData)
    {
        // Get the current volume stack
        var stack = VolumeManager.instance.stack;
        // Get the corresponding volume component
        m_VolumeComponent = stack.GetComponent<GrayscaleEffect>();
        // if blend value > 0, then we need to render this effect. 
        return m_VolumeComponent.blend.value > 0;
    }

    // The actual rendering execution is done here
    public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
    {
        // set material properties
        if(m_Material != null){
            m_Material.SetFloat(ShaderIDs.Blend, m_VolumeComponent.blend.value);
        }
        // set source texture
        cmd.SetGlobalTexture(ShaderIDs.Input, source);
        // draw a fullscreen triangle to the destination
        CoreUtils.DrawFullScreen(cmd, m_Material, destination);
    }
}
