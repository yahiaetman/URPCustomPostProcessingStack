using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal.PostProcessing {

    /// <summary>
    /// This render feature is responsible for:
    /// <list type="bullet">
    /// <item>
    /// <description> Injecting the render passes for custom post processing </description>
    /// </item>
    /// <item>
    /// <description> Ordering and categorizing the custom post processing renderers </description>
    /// </item>
    /// </list>
    /// </summary>
    [Serializable]
    public class CustomPostProcess : ScriptableRendererFeature
    {
        /// <summary>
        /// The settings for the custom post processing render feature.
        /// </summary>
        [Serializable]
        public class CustomPostProcessSettings {

            /// <summary>
            /// Three list (one for each injection point) that holds the custom post processing renderers in order of execution during the render pass
            /// </summary>
            [SerializeField]
            public List<string> renderersAfterOpaqueAndSky, renderersBeforePostProcess, renderersAfterPostProcess;

            public CustomPostProcessSettings(){
                renderersAfterOpaqueAndSky = new List<string>();
                renderersBeforePostProcess = new List<string>();
                renderersAfterPostProcess = new List<string>();
            }
        }

        /// <summary>
        /// The settings of the render feature
        /// </summary>
        [SerializeField] public CustomPostProcessSettings settings = new CustomPostProcessSettings();

        /// <summary>
        /// The render passes at each inject point
        /// </summary>
        private CustomPostProcessRenderPass m_AfterOpaqueAndSky, m_BeforePostProcess, m_AfterPostProcess;

        /// <summary>
        /// A handle to the "_AfterPostProcessTexture" used as the target for the builtin post processing pass in the last camera in the camera stack 
        /// </summary>
        private RenderTargetHandle m_AfterPostProcessColor;

        /// <summary>
        /// Injects the custom post-processing render passes.
        /// </summary>
        /// <param name="renderer">The renderer</param>
        /// <param name="renderingData">Rendering state. Use this to setup render passes.</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Only inject passes if post processing is enabled
            if(renderingData.cameraData.postProcessEnabled) {
                // For each pass, only inject if there is at least one custom post-processing renderer class in it.
                if(m_AfterOpaqueAndSky.HasPostProcessRenderers && m_AfterOpaqueAndSky.PrepareRenderers(ref renderingData)){
                    m_AfterOpaqueAndSky.Setup(renderer.cameraColorTarget, renderer.cameraColorTarget);
                    renderer.EnqueuePass(m_AfterOpaqueAndSky);
                }
                if(m_BeforePostProcess.HasPostProcessRenderers && m_BeforePostProcess.PrepareRenderers(ref renderingData)){
                    m_BeforePostProcess.Setup(renderer.cameraColorTarget, renderer.cameraColorTarget);
                    renderer.EnqueuePass(m_BeforePostProcess);
                }
                if(m_AfterPostProcess.HasPostProcessRenderers && m_AfterPostProcess.PrepareRenderers(ref renderingData)){
                    // If this camera resolve to the final target, then both the source & destination will be "_AfterPostProcessTexture" (Note: a final blit/post pass is added by the renderer).
                    var source = renderingData.cameraData.resolveFinalTarget ? m_AfterPostProcessColor.Identifier() : renderer.cameraColorTarget;
                    m_AfterPostProcess.Setup(source, source);
                    renderer.EnqueuePass(m_AfterPostProcess);
                }
            }
        }

        /// <summary>
        /// Initializes the custom post-processing render passes.
        /// </summary>
        public override void Create()
        {
            // This is copied from the forward renderer
            m_AfterPostProcessColor.Init("_AfterPostProcessTexture");
            // Create the three render passes and send the custom post-processing renderer classes to each
            Dictionary<string, CustomPostProcessRenderer> shared = new Dictionary<string, CustomPostProcessRenderer>();
            m_AfterOpaqueAndSky = new CustomPostProcessRenderPass(CustomPostProcessInjectionPoint.AfterOpaqueAndSky, InstantiateRenderers(settings.renderersAfterOpaqueAndSky, shared));
            m_BeforePostProcess = new CustomPostProcessRenderPass(CustomPostProcessInjectionPoint.BeforePostProcess, InstantiateRenderers(settings.renderersBeforePostProcess, shared));
            m_AfterPostProcess = new CustomPostProcessRenderPass(CustomPostProcessInjectionPoint.AfterPostProcess, InstantiateRenderers(settings.renderersAfterPostProcess, shared));
        }
        
        /// <summary>
        /// Converts the class name (AssemblyQualifiedName) to an instance. Filters out types that don't exist or don't match the requirements.
        /// </summary>
        /// <param name="names">The list of assembly-qualified class names</param>
        /// <param name="shared">Dictionary of shared instances keyed by class name</param>
        /// <returns>List of renderers</returns>
        private List<CustomPostProcessRenderer> InstantiateRenderers(List<String> names, Dictionary<string, CustomPostProcessRenderer> shared){
            var renderers = new List<CustomPostProcessRenderer>(names.Count);
            foreach(var name in names){
                if(shared.TryGetValue(name, out var renderer)){
                    renderers.Add(renderer);
                } else {
                    var type = Type.GetType(name);
                    if(type == null || !type.IsSubclassOf(typeof(CustomPostProcessRenderer))) continue;
                    var attribute = CustomPostProcessAttribute.GetAttribute(type);
                    if(attribute == null) continue;

                    renderer = Activator.CreateInstance(type) as CustomPostProcessRenderer;
                    renderers.Add(renderer);
                    
                    if(attribute.ShareInstance)
                        shared.Add(name, renderer);
                }
            }
            return renderers;
        }
    }

    /// <summary>
    /// A render pass for executing custom post processing renderers.
    /// </summary>
    public class CustomPostProcessRenderPass : ScriptableRenderPass
    {
        /// <summary>
        /// The injection point of the pass
        /// </summary>
        private CustomPostProcessInjectionPoint injectionPoint;

        /// <summary>
        /// The pass name which will be displayed on the command buffer in the frame debugger.
        /// </summary>
        private string m_PassName;

        /// <summary>
        /// List of all post process renderer instances.
        /// </summary>
        private List<CustomPostProcessRenderer> m_PostProcessRenderers;

        /// <summary>
        /// List of all post process renderer instances that are active for the current camera.
        /// </summary>
        private List<int> m_ActivePostProcessRenderers;

        /// <summary>
        /// Array of 2 intermediate render targets used to hold intermediate results.
        /// </summary>
        private RenderTargetHandle[] m_Intermediate;

        /// <summary>
        /// Indentifies whether the intermediate render targets are allocated or not.
        /// </summary>
        private bool[] m_IntermediateAllocated;

        /// <summary>
        /// The texture descriptor for the intermediate render targets.
        /// </summary>
        private RenderTextureDescriptor m_IntermediateDesc;

        /// <summary>
        /// The source of the color data for the render pass
        /// </summary>
        private RenderTargetIdentifier m_Source;

        /// <summary>
        /// The destination of the color data for the render pass
        /// </summary>
        private RenderTargetIdentifier m_Destination;

        /// <summary>
        /// A list of profiling samplers, one for each post process renderer
        /// </summary>
        private List<ProfilingSampler> m_ProfilingSamplers;

        /// <summary>
        /// Gets whether this render pass has any post process renderers to execute
        /// </summary>
        public bool HasPostProcessRenderers => m_PostProcessRenderers.Count != 0;

        /// <summary>
        /// Construct the custom post-processing render pass
        /// </summary>
        /// <param name="injectionPoint">The post processing injection point</param>
        /// <param name="classes">The list of classes for the renderers to be executed by this render pass</param>
        public CustomPostProcessRenderPass(CustomPostProcessInjectionPoint injectionPoint, List<CustomPostProcessRenderer> renderers){
            this.injectionPoint = injectionPoint;
            this.m_ProfilingSamplers = new List<ProfilingSampler>(renderers.Count);
            this.m_PostProcessRenderers = renderers;
            foreach(var renderer in renderers){
                // Get renderer name and add it to the names list
                var attribute = CustomPostProcessAttribute.GetAttribute(renderer.GetType());
                m_ProfilingSamplers.Add(new ProfilingSampler(attribute?.Name));
            }
            // Pre-allocate a list for active renderers
            this.m_ActivePostProcessRenderers = new List<int>(renderers.Count);
            // Set render pass event and name based on the injection point.
            switch(injectionPoint){
                case CustomPostProcessInjectionPoint.AfterOpaqueAndSky: 
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox; 
                    m_PassName = "Custom PostProcess after Opaque & Sky";
                    break;
                case CustomPostProcessInjectionPoint.BeforePostProcess: 
                    renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                    m_PassName = "Custom PostProcess before PostProcess";
                    break;
                case CustomPostProcessInjectionPoint.AfterPostProcess:
                    // NOTE: This was initially "AfterRenderingPostProcessing" but it made the builtin post-processing to blit directly to the camera target.
                    renderPassEvent = RenderPassEvent.AfterRendering;
                    m_PassName = "Custom PostProcess after PostProcess";
                    break;
            }
            // Initialize the IDs and allocation state of the intermediate render targets
            m_Intermediate = new RenderTargetHandle[2];
            m_Intermediate[0].Init("_IntermediateRT0");
            m_Intermediate[1].Init("_IntermediateRT1");
            m_IntermediateAllocated = new bool[2];
            m_IntermediateAllocated[0] = false;
            m_IntermediateAllocated[1] = false;
        }

        /// <summary>
        /// Gets the corresponding intermediate RT and allocates it if not already allocated
        /// </summary>
        /// <param name="cmd">The command buffer to use for allocation</param>
        /// <param name="index">The intermediate RT index</param>
        /// <returns></returns>
        private RenderTargetIdentifier GetIntermediate(CommandBuffer cmd, int index){
            if(!m_IntermediateAllocated[index]){
                cmd.GetTemporaryRT(m_Intermediate[index].id, m_IntermediateDesc);
                m_IntermediateAllocated[index] = true;
            }
            return m_Intermediate[index].Identifier();
        }

        /// <summary>
        /// Release allocated intermediate RTs
        /// </summary>
        /// <param name="cmd">The command buffer to use for deallocation</param>
        private void CleanupIntermediate(CommandBuffer cmd){
            for(int index = 0; index < 2; ++index){
                if(m_IntermediateAllocated[index]){
                    cmd.ReleaseTemporaryRT(m_Intermediate[index].id);
                    m_IntermediateAllocated[index] = false;
                }
            }
        }

        /// <summary>
        /// Setup the source and destination render targets
        /// </summary>
        /// <param name="source">Source render target</param>
        /// <param name="destination">Destination render target</param>
        public void Setup(RenderTargetIdentifier source, RenderTargetIdentifier destination){
            this.m_Source = source;
            this.m_Destination = destination;
        }

        /// <summary>
        /// Prepares the renderer for executing on this frame and checks if any of them actually requires rendering
        /// </summary>
        /// <param name="renderingData">Current rendering data</param>
        /// <returns>True if any renderer will be executed for the given camera. False Otherwise.</returns>
        public bool PrepareRenderers(ref RenderingData renderingData){
            // See if current camera is a scene view camera to skip renderers with "visibleInSceneView" = false.
            bool isSceneView = renderingData.cameraData.cameraType == CameraType.SceneView;

            // Here, we will collect the inputs needed by all the custom post processing effects
            ScriptableRenderPassInput passInput = ScriptableRenderPassInput.None;

            // Collect the active renderers
            m_ActivePostProcessRenderers.Clear();
            for(int index = 0; index < m_PostProcessRenderers.Count; index++){
                var ppRenderer = m_PostProcessRenderers[index];
                // Skips current renderer if "visibleInSceneView" = false and the current camera is a scene view camera. 
                if(isSceneView && !ppRenderer.visibleInSceneView) continue;
                // Setup the camera for the renderer and if it will render anything, add to active renderers and get its required inputs
                if(ppRenderer.Setup(ref renderingData, injectionPoint)){
                    m_ActivePostProcessRenderers.Add(index);
                    passInput |= ppRenderer.input;
                }
            }

            // Configure the pass to tell the renderer what inputs we need
            ConfigureInput(passInput);

            // return if no renderers are active
            return m_ActivePostProcessRenderers.Count != 0;
        } 

        /// <summary>
        /// Execute the custom post processing renderers
        /// </summary>
        /// <param name="context">The scriptable render context</param>
        /// <param name="renderingData">Current rendering data</param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Copy camera target description for intermediate RTs. Disable multisampling and depth buffer for the intermediate targets.
            m_IntermediateDesc = renderingData.cameraData.cameraTargetDescriptor;
            m_IntermediateDesc.msaaSamples = 1;
            m_IntermediateDesc.depthBufferBits = 0;

            CommandBuffer cmd = CommandBufferPool.Get(m_PassName);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            int width = m_IntermediateDesc.width;
            int height = m_IntermediateDesc.height;
            cmd.SetGlobalVector("_ScreenSize", new Vector4(width, height, 1.0f/width, 1.0f/height));
            
            // The variable will be true if the last renderer couldn't blit to destination.
            // This happens if there is only 1 renderer and the source is the same as the destination.
            bool requireBlitBack = false;
            // The current intermediate RT to use as a source.
            int intermediateIndex = 0;

            for(int index = 0; index < m_ActivePostProcessRenderers.Count; ++index){
                var rendererIndex = m_ActivePostProcessRenderers[index];
                var renderer = m_PostProcessRenderers[rendererIndex];
                
                RenderTargetIdentifier source, destination;
                if(index == 0){
                    // If this is the first renderers then the source will be the external source (not intermediate).
                    source = m_Source;
                    if(m_ActivePostProcessRenderers.Count == 1){
                        // There is only one renderer, check if the source is the same as the destination
                        if(m_Source == m_Destination){
                            // Since we can't bind the same RT as a texture and a render target at the same time, we will blit to an intermediate RT.
                            destination = GetIntermediate(cmd, 0);
                            // Then we will blit back to the destination.
                            requireBlitBack = true;
                        } else {
                            // Otherwise, we can directly blit from source to destination.
                            destination = m_Destination;
                        }
                    } else {
                        // If there is more than one renderer, we will need to the intermediate RT anyway.
                        destination = GetIntermediate(cmd, intermediateIndex);
                    }
                } else {
                    // If this is not the first renderer, we will want to the read from the intermediate RT.
                    source = GetIntermediate(cmd, intermediateIndex);
                    if(index == m_ActivePostProcessRenderers.Count - 1){
                        // If this is the last renderer, blit to the destination directly.
                        destination = m_Destination;
                    } else {
                        // Otherwise, flip the intermediate RT index and set as destination.
                        // This will act as a ping pong process between the 2 RT where color data keeps moving back and forth while being processed on each pass.
                        intermediateIndex = 1 - intermediateIndex;
                        destination = GetIntermediate(cmd, intermediateIndex);
                    }
                }

                using(new ProfilingScope(cmd, m_ProfilingSamplers[rendererIndex]))
                {
                    // If the renderer was not already initialized, initialize it.
                    if(!renderer.Initialized)
                        renderer.InitializeInternal();
                    // Execute the renderer.
                    renderer.Render(cmd, source, destination, ref renderingData, injectionPoint);
                }
                
            }

            // If blit back is needed, blit from the intermediate RT to the destination (see above for explanation)
            if(requireBlitBack)
                Blit(cmd, m_Intermediate[0].Identifier(), m_Destination);
            
            // Release allocated Intermediate RTs.
            CleanupIntermediate(cmd);

            // Send command buffer for execution, then release it.
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

}