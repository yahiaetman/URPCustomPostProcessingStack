using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

// Credits goes to Keijiro Takahashi for their repo: https://github.com/keijiro/Kino

namespace Yetman.PostProcess {

    // Define the Volume Component for the custom post processing effect 
    [System.Serializable, VolumeComponentMenu("CustomPostProcess/Streak")]
    public class StreakEffect : VolumeComponent
    {
        [Tooltip("Controls the blending between the original and the edge color.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);
        
        [Tooltip("Defines the minimum brightness for the streak to appear.")]
        public MinFloatParameter threshold = new MinFloatParameter(1, 0);
        
        [Tooltip("Define the stretching of the streak.")]
        public ClampedFloatParameter stretch  = new ClampedFloatParameter(0.75f, 0, 1);

        [Tooltip("Define the streak tint.")]
        public ColorParameter tint = new ColorParameter(new Color(0.55f, 0.55f, 1), true, false, true);
    }

    // Define the renderer for the custom post processing effect
    [CustomPostProcess("Streak", CustomPostProcessInjectionPoint.BeforePostProcess)]
    public class StreakEffectRenderer : CustomPostProcessRenderer
    {
        // A variable to hold a reference to the corresponding volume component (you can define as many as you like)
        private StreakEffect m_VolumeComponent;
        
        // The postprocessing material (you can define as many as you like)
        private Material m_Material;
        
        // The ids of the shader variables
        static class ShaderIDs {
            internal readonly static int Source = Shader.PropertyToID("_SourceTexture");
            internal readonly static int Input = Shader.PropertyToID("_InputTexture");
            internal readonly static int High = Shader.PropertyToID("_HighTexture");
            internal readonly static int Intensity = Shader.PropertyToID("_Intensity");
            internal readonly static int Threshold = Shader.PropertyToID("_Threshold");
            internal readonly static int Stretch = Shader.PropertyToID("_Stretch");
            internal readonly static int Color = Shader.PropertyToID("_Color");
        }
        
        // The maximum number of mip levels in the streak pyramid.
        private const int MAX_MIP_LEVEL = 16;

        // The streak pyramid for downscaling and upscaling (used for bluring the prefiltering result)
        private (RenderTargetHandle down, RenderTargetHandle up)[] pyramid;

        // By default, the effect is visible in the scene view, but we can change that here.
        public override bool visibleInSceneView => true;

        // Initialized is called only once before the first render call
        // so we use it to create our material
        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/Yetman/PostProcess/Streak");
            // Define names for each RT in the streak pyramid
            pyramid = new (RenderTargetHandle up, RenderTargetHandle down)[MAX_MIP_LEVEL];
            for(int index = 0; index < MAX_MIP_LEVEL; ++index){
                RenderTargetHandle mipup = default, mipdown = default;
                mipup.Init($"_Level{index}_Up");
                mipdown.Init($"_Level{index}_Down");
                pyramid[index] = (mipdown, mipup);
            }
        }

        // Called for each camera/injection point pair on each frame. Return true if the effect should be rendered for this camera.
        public override bool Setup(ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Get the current volume stack
            var stack = VolumeManager.instance.stack;
            // Get the corresponding volume component
            m_VolumeComponent = stack.GetComponent<StreakEffect>();
            // if intensity value > 0, then we need to render this effect. 
            return m_VolumeComponent.intensity.value > 0;
        }

        /// <summary>
        /// Allocate the streak pyramid and returns the number of allocated mip levels.
        /// </summary>
        /// <param name="cmd">The post processing command buffer</param>
        /// <param name="renderingData">Current rendering data</param>
        /// <returns>The number mip levels in the pyramid</returns>
        private int AllocatePyramid(CommandBuffer cmd, ref RenderingData renderingData){
            RenderTextureDescriptor descriptor = GetTempRTDescriptor(renderingData);

            descriptor.height /= 2;

            cmd.GetTemporaryRT(pyramid[0].down.id, descriptor, FilterMode.Bilinear);

            // Allocate temporary RTs for each mip level till the width is less than 4
            int index = 1; 
            for (; index < MAX_MIP_LEVEL; ++index)
            {
                descriptor.width /= 2;
                if(descriptor.width >= 4){
                    cmd.GetTemporaryRT(pyramid[index].down.id, descriptor, FilterMode.Bilinear);
                    cmd.GetTemporaryRT(pyramid[index].up.id, descriptor, FilterMode.Bilinear);
                } else {
                    break;
                }
            }
            return index;
        }

        /// <summary>
        /// Release the streak pyramid
        /// </summary>
        /// <param name="cmd">The post processing command buffer</param>
        /// <param name="mips">Number of allocated mip levels</param>
        private void ReleasePyramid(CommandBuffer cmd, int mips){
            for (int index = 0; index < mips; ++index)
            {
                cmd.ReleaseTemporaryRT(pyramid[index].down.id);
                if(index > 0) cmd.ReleaseTemporaryRT(pyramid[index].up.id);
            }
        }

        // The actual rendering execution is done here
        public override void Render(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            int mips = AllocatePyramid(cmd, ref renderingData);
            // set material properties
            if(m_Material != null){
                m_Material.SetFloat(ShaderIDs.Intensity, m_VolumeComponent.intensity.value);
                m_Material.SetFloat(ShaderIDs.Threshold, m_VolumeComponent.threshold.value);
                m_Material.SetFloat(ShaderIDs.Stretch, m_VolumeComponent.stretch.value);
                m_Material.SetColor(ShaderIDs.Color, m_VolumeComponent.tint.value);
            }
            // set source texture
            cmd.SetGlobalTexture(ShaderIDs.Source, source);
            // prefilter and downscale the source to the first level of the pyramid
            CoreUtils.DrawFullScreen(cmd, m_Material, pyramid[0].down.Identifier(), null, 0);

            // downscale the prefiltering result all the way down the pyramid
            var level = 1;
            for (; level < mips - 1; level++)
            {
                cmd.SetGlobalTexture(ShaderIDs.Input, pyramid[level-1].down.Identifier());
                CoreUtils.DrawFullScreen(cmd, m_Material, pyramid[level].down.Identifier(), null, 1);
            }

            // Upsample & combine
            var lastRT = pyramid[--level].down;
            for (level--; level >= 1; level--)
            {
                var mip = pyramid[level];
                cmd.SetGlobalTexture(ShaderIDs.Input, lastRT.Identifier());
                cmd.SetGlobalTexture(ShaderIDs.High, mip.down.Identifier());
                CoreUtils.DrawFullScreen(cmd, m_Material, mip.up.Identifier(), null, 2);
                lastRT = mip.up;
            }

            // Final composition
            cmd.SetGlobalTexture(ShaderIDs.Input, lastRT.Identifier());
            CoreUtils.DrawFullScreen(cmd, m_Material, destination, null, 3);

            ReleasePyramid(cmd, mips);
        }
    }

}