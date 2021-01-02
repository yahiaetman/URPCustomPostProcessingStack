using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

namespace Yetman.PostProcess {

    // Define the Volume Component for the custom post processing effect 
    [System.Serializable, VolumeComponentMenu("CustomPostProcess/Edge Detection")]
    public class EdgeDetectionEffect : VolumeComponent
    {
        [Tooltip("Controls the blending between the original and the edge color.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);
        
        [Tooltip("Defines the edge thickness.")]
        public MinFloatParameter thickness = new MinFloatParameter(1, 0);
        
        [Tooltip("Define the threshold of the normal difference in degrees.")]
        public FloatRangeParameter normalThreshold = new FloatRangeParameter(new Vector2(1, 2), 0, 360);

        [Tooltip("Define the threshold of the depth difference in world units.")]
        public FloatRangeParameter depthThreshold = new FloatRangeParameter(new Vector2(0.1f, 0.11f), 0, 1);

        [Tooltip("Define the edge color.")]
        public ColorParameter color = new ColorParameter(Color.black, true, false, true);
    }

    // Define the renderer for the custom post processing effect
    [CustomPostProcess("Edge Detection", CustomPostProcessInjectionPoint.AfterOpaqueAndSky)]
    public class EdgeDetectionEffectRenderer : CustomPostProcessRenderer
    {
        // A variable to hold a reference to the corresponding volume component (you can define as many as you like)
        private EdgeDetectionEffect m_VolumeComponent;
        
        // The postprocessing material (you can define as many as you like)
        private Material m_Material;
        
        // The ids of the shader variables
        static class ShaderIDs {
            internal readonly static int Input = Shader.PropertyToID("_MainTex");
            internal readonly static int Intensity = Shader.PropertyToID("_Intensity");
            internal readonly static int Threshold = Shader.PropertyToID("_Threshold");
            internal readonly static int Thickness = Shader.PropertyToID("_Thickness");
            internal readonly static int Color = Shader.PropertyToID("_Color");
        }
        
        // By default, the effect is visible in the scene view, but we can change that here.
        public override bool visibleInSceneView => true;
        
        // We need Color, Depth and Normal textures to apply edge detection.
        public override ScriptableRenderPassInput input => ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;

        // Initialized is called only once before the first render call
        // so we use it to create our material
        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/Yetman/PostProcess/EdgeDetection");
        }

        // Called for each camera/injection point pair on each frame. Return true if the effect should be rendered for this camera.
        public override bool Setup(ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Get the current volume stack
            var stack = VolumeManager.instance.stack;
            // Get the corresponding volume component
            m_VolumeComponent = stack.GetComponent<EdgeDetectionEffect>();
            // if intensity value > 0, then we need to render this effect. 
            return m_VolumeComponent.intensity.value > 0;
        }

        // The actual rendering execution is done here
        public override void Render(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // set material properties
            if(m_Material != null){
                m_Material.SetFloat(ShaderIDs.Intensity, m_VolumeComponent.intensity.value);
                m_Material.SetFloat(ShaderIDs.Thickness, m_VolumeComponent.thickness.value);
                Vector2 normalThreshold = m_VolumeComponent.normalThreshold.value;
                Vector2 depthThreshold = m_VolumeComponent.depthThreshold.value;
                Vector4 threshold = new Vector4(Mathf.Cos(normalThreshold.y * Mathf.Deg2Rad), Mathf.Cos(normalThreshold.x * Mathf.Deg2Rad), depthThreshold.x, depthThreshold.y);
                m_Material.SetVector(ShaderIDs.Threshold, threshold);
                m_Material.SetColor(ShaderIDs.Color, m_VolumeComponent.color.value);
            }
            // set source texture
            cmd.SetGlobalTexture(ShaderIDs.Input, source);
            // draw a fullscreen triangle to the destination
            CoreUtils.DrawFullScreen(cmd, m_Material, destination);
        }
    }

}