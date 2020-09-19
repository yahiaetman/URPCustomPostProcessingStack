using System;

namespace UnityEngine.Rendering.Universal.PostProcessing {

    /// <summary>
    /// This is a workaround to get scene normals till URP 10.0 releases
    /// Credits for the original script go to Christopher Sims for thier repo: https://github.com/chrisloop/outlinestudy
    /// Unfortunately, SRP doesn't fetch properties from the original material properties while overriding the material.
    /// You can set a MaterialPropertyBlock on the renderer to override some properties (e.g. Normal Maps)
    /// But you can't set keyword or renderstate in the MaterialPropertyBlock so no alpha clipping :( 
    /// </summary>
    [Serializable]
    public class SceneNormals : ScriptableRendererFeature
    {
        /// <summary>
        /// The shader used to build the override material
        /// </summary>
        [SerializeField] private Shader normalsShader = null;
        
        /// <summary>
        /// The render pass
        /// </summary>
        private SceneNormalsPass normalsPass = null;

        /// <summary>
        /// The render target for the scene normals
        /// </summary>
        private RenderTargetHandle sceneNormalsTexture = default;

        /// <summary>
        /// The override material to render the scene normals
        /// </summary>
        private Material normalsMaterial = null;

        /// <summary>
        /// Intializes the renderer feature resources
        /// </summary>
        public override void Create()
        {
            // If the shader is not set by the user, find the default shader by name
            if(normalsShader == null)
                normalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Yetman/Postprocess/Internal-NormalsOutput");
            else 
                normalsMaterial = CoreUtils.CreateEngineMaterial(normalsShader);
            normalsMaterial.enableInstancing = true;
            normalsPass = new SceneNormalsPass(RenderQueueRange.opaque, -1, normalsMaterial);
            normalsPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            sceneNormalsTexture.Init("_CameraNormalsTexture");
        }

        /// <summary>
        /// Here you can inject one or multiple render passes in the renderer.
        /// This method is called when setting up the renderer once per-camera.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            normalsPass.Setup(sceneNormalsTexture);
            renderer.EnqueuePass(normalsPass);
        }
    }

    /// <summary>
    /// The render pass to draw the scene normals
    /// </summary>
    public class SceneNormalsPass : ScriptableRenderPass
    {

        public RenderTargetHandle destination { get; set; }
        private Material normalsMaterial = null;
        private FilteringSettings m_FilteringSettings;
        private ProfilingSampler m_ProfilingSampler;
        
        // Draw the pass named "DepthOnly" 
        private static ShaderTagId m_ShaderTagId = new ShaderTagId("DepthOnly");
        
        // Since the sky should always look at the camera, its view space normal will point towards the camera
        // After encoding with "PackNormalOctRectEncode", it should be #800000
        private static Color skyNormalColor = new Color(0.5f, 0.0f, 0.0f, 1.0f);


        public SceneNormalsPass(RenderQueueRange renderQueueRange, LayerMask layerMask, Material material)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            this.normalsMaterial = material;
            m_ProfilingSampler = new ProfilingSampler("Scene Normals Prepass");
        }

        public void Setup(RenderTargetHandle destination)
        {
            this.destination = destination;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in an performance manner.
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 32;
            descriptor.colorFormat = RenderTextureFormat.RGHalf;
            descriptor.msaaSamples = 1;

            cmd.GetTemporaryRT(destination.id, descriptor, FilterMode.Point);
            ConfigureTarget(destination.Identifier());
            ConfigureClear(ClearFlag.All, skyNormalColor);
            
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Scene Normals Prepass");

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;

                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;
                if (cameraData.isStereoEnabled)
                    context.StartMultiEye(camera);

                drawSettings.overrideMaterial = normalsMaterial;

                m_FilteringSettings.layerMask = camera.cullingMask;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                    ref m_FilteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (destination != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(destination.id);
                destination = RenderTargetHandle.CameraTarget;
            }
        }
    }

}