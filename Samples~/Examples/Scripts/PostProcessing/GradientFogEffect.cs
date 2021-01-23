using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

namespace Yetman.PostProcess {

    // Define the Volume Component for the custom post processing effect 
    [System.Serializable, VolumeComponentMenu("CustomPostProcess/Gradient Fog")]
    public class GradientFogEffect : VolumeComponent
    {
        [Tooltip("Controls the blending between the original and the fog color.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);
        
        [Tooltip("Controls the distance at which the fog strength is 63.2%.")]
        public MinFloatParameter fogDistance = new MinFloatParameter(20, 0);

        [Tooltip("Define the near fog color.")]
        public ColorParameter nearFogColor = new ColorParameter(Color.red, true, false, true);

        [Tooltip("Define the far fog color.")]
        public ColorParameter farFogColor = new ColorParameter(Color.blue, true, false, true);
        
        [Tooltip("Define the distance at which the fog color gradient starts.")]
        public MinFloatParameter nearColorDistance = new MinFloatParameter(5, 0);
        
        [Tooltip("Define the distance at which the fog color gradient ends.")]
        public MinFloatParameter farColorDistance = new MinFloatParameter(20, 0);
    }

    // Define the renderer for the custom post processing effect
    // This effect can be used after the opaque/sky pass or after the transparent pass
    [CustomPostProcess("Gradient Fog", CustomPostProcessInjectionPoint.AfterOpaqueAndSky | CustomPostProcessInjectionPoint.BeforePostProcess)]
    public class GradientFogEffectRenderer : CustomPostProcessRenderer
    {
        // A variable to hold a reference to the corresponding volume component (you can define as many as you like)
        private GradientFogEffect m_VolumeComponent;

        // The postprocessing material (you can define as many as you like)
        private Material m_Material;
        
        // The ids of the shader variables
        static class ShaderIDs {
            internal readonly static int Input = Shader.PropertyToID("_MainTex");
            internal readonly static int Intensity = Shader.PropertyToID("_Intensity");
            internal readonly static int Exponent = Shader.PropertyToID("_Exponent");
            internal readonly static int ColorRange = Shader.PropertyToID("_ColorRange");
            internal readonly static int NearFogColor = Shader.PropertyToID("_NearFogColor");
            internal readonly static int FarFogColor = Shader.PropertyToID("_FarFogColor");
        }

        // By default, the effect is visible in the scene view, but we can change that here.
        public override bool visibleInSceneView => true;

        // We need depth to compute the pixel distance from the camera which is required to calculate the fog
        public override ScriptableRenderPassInput input => ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;

        // Initialized is called only once before the first render call
        // so we use it to create our material
        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/Yetman/PostProcess/GradientFog");
        }

        // Called for each camera/injection point pair on each frame. Return true if the effect should be rendered for this camera.
        public override bool Setup(ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Get the current volume stack
            var stack = VolumeManager.instance.stack;
            // Get the corresponding volume component
            m_VolumeComponent = stack.GetComponent<GradientFogEffect>();
            // if intensity value > 0, then we need to render this effect. 
            return m_VolumeComponent.intensity.value > 0;
        }

        // The actual rendering execution is done here
        public override void Render(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // set material properties
            if(m_Material != null){
                m_Material.SetFloat(ShaderIDs.Intensity, m_VolumeComponent.intensity.value);
                m_Material.SetFloat(ShaderIDs.Exponent, 1/m_VolumeComponent.fogDistance.value);
                m_Material.SetVector(ShaderIDs.ColorRange, new Vector2(m_VolumeComponent.nearColorDistance.value, m_VolumeComponent.farColorDistance.value));
                m_Material.SetColor(ShaderIDs.NearFogColor, m_VolumeComponent.nearFogColor.value);
                m_Material.SetColor(ShaderIDs.FarFogColor, m_VolumeComponent.farFogColor.value);
                // Checks whether the renderer is called before transparent or not to pick the proper shader features
                if(injectionPoint == CustomPostProcessInjectionPoint.AfterOpaqueAndSky){
                    m_Material.DisableKeyword("AFTER_TRANSPARENT_ON");
                } else {
                    m_Material.EnableKeyword("AFTER_TRANSPARENT_ON");
                }
            }
            // set source texture
            cmd.SetGlobalTexture(ShaderIDs.Input, source);
            // draw a fullscreen triangle to the destination
            CoreUtils.DrawFullScreen(cmd, m_Material, destination);
        }
    }

}