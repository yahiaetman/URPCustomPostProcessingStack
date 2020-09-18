using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

namespace Yetman.PostProcess {

    // Define the Volume Component for the custom post processing effect 
    [System.Serializable, VolumeComponentMenu("CustomPostProcess/Glitch")]
    public class GlitchEffect : VolumeComponent
    {
        [Tooltip("Controls the glitch amount relative to screen width.")]
        public MinFloatParameter power = new MinFloatParameter(0, 0);
        
        [Tooltip("Controls the noise horizontal scrolling speed.")]
        public MinFloatParameter speed = new MinFloatParameter(0, 0);

        [Tooltip("Controls the noise vertical scale.")]
        public MinFloatParameter scale = new MinFloatParameter(10, 0);
    }

    // Define the renderer for the custom post processing effect
    [CustomPostProcess("Glitch", CustomPostProcessInjectionPoint.AfterPostProcess)]
    public class GlitchEffectRenderer : CustomPostProcessRenderer
    {
        // A variable to hold a reference to the corresponding volume component (you can define as many as you like)
        private GlitchEffect m_VolumeComponent;
        
        // The postprocessing material (you can define as many as you like)
        private Material m_Material;
        
        // The ids of the shader variables
        static class ShaderIDs {
            internal readonly static int Power = Shader.PropertyToID("_GlitchPower");
            internal readonly static int Time = Shader.PropertyToID("_NoiseX");
            internal readonly static int Scale = Shader.PropertyToID("_GlitchScale");
        }

        // By default, the effect is visible in the scene view, but we can change that here.
        public override bool visibleInSceneView => true;

        // you can define local variables if you wish (Note that they will be shared between cameras)
        private float timeElapsed, previousFrameTime;

        // Initialized is called only once before the first render call
        // so we use it to create our material and initialize variables
        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial("Shader Graphs/Glitch");
            timeElapsed = 0;
            previousFrameTime = Time.time;
        }

        // Called for each camera/injection point pair on each frame. Return true if the effect should be rendered for this camera.
        public override bool Setup(ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Get the current volume stack
            var stack = VolumeManager.instance.stack;
            // Get the corresponding volume component
            m_VolumeComponent = stack.GetComponent<GlitchEffect>();
            // if power value > 0, then we need to render this effect. 
            return m_VolumeComponent.power.value > 0;
        }

        // The actual rendering execution is done here
        public override void Render(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Update local variables
            // Note: we calculate our delta time since this function can be called more than once in a single frame.
            timeElapsed += m_VolumeComponent.speed.value * (Time.time - previousFrameTime);
            previousFrameTime = Time.time;
            // set material properties
            if(m_Material != null){
                m_Material.SetFloat(ShaderIDs.Power, m_VolumeComponent.power.value);
                m_Material.SetFloat(ShaderIDs.Time, timeElapsed);
                m_Material.SetFloat(ShaderIDs.Scale, m_VolumeComponent.scale.value);
            }
            // Since we are using a shader graph, we cann't use CoreUtils.DrawFullScreen without modifying the vertex shader.
            // So we go with the easy route and use CommandBuffer.Blit instead. The same goes if you want to use legacy image effect shaders.
            // Note: don't forget to set pass to 0 (last argument in Blit) to make sure that extra passes are not drawn.
            cmd.Blit(source, destination, m_Material, 0);
        }
    }

}