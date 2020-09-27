using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.PostProcessing;

// NOTE: Guess who wanted to have this work for XR but gave up... yup that's me.
// Anyway, I don't think anyone would want to see this nausea-inducing effect on a VR headset.

namespace Yetman.PostProcess {

    // Define the Volume Component for the custom post processing effect 
    [System.Serializable, VolumeComponentMenu("CustomPostProcess/After Image")]
    public class AfterImageEffect : VolumeComponent
    {
        [Tooltip("Controls the blending between the new and old color.")]
        public ColorParameter blend = new ColorParameter(Color.black, false, false, true);

        [Tooltip("A scale for the time to convergence.")]
        public MinFloatParameter timeScale = new MinFloatParameter(0, 0);
    }

    // Define the renderer for the custom post processing effect
    [CustomPostProcess("After Image", CustomPostProcessInjectionPoint.AfterPostProcess)]
    public class AfterImageEffectRenderer : CustomPostProcessRenderer
    {
        // A variable to hold a reference to the corresponding volume component (you can define as many as you like)
        private AfterImageEffect m_VolumeComponent;

        // The postprocessing material (you can define as many as you like)
        private Material m_Material;

        // The ids of the shader variables
        static class ShaderIDs {
            internal readonly static int Input = Shader.PropertyToID("_MainTex");
            internal readonly static int Other = Shader.PropertyToID("_SecondaryTex");
            internal readonly static int Blend = Shader.PropertyToID("_Blend");
        }

        /// <summary>
        /// Used to store the history of a camera (its previous frame and whether it is valid or not)
        /// </summary>
        private class CameraHistory {
            public RenderTexture Frame { get; set; }
            public bool Invalidated { get; set; }

            public CameraHistory(RenderTextureDescriptor descriptor){
                Frame = new RenderTexture(descriptor);
                Invalidated = false;
            }
        }

        // We store the history for each camera separately (key is the camera instance id).
        private Dictionary<int, CameraHistory> _histories = null;

        // A temporary render target in case we need it (if destination is the camera render target and can't be used as source).
        private RenderTargetHandle _intermediate = default;

        // By default, the effect is visible in the scene view, but we can change that here.
        // I chose false here because who would want an after-image effect while editing.
        public override bool visibleInSceneView => false;

        // Use the constructor for initialization work that needs to be done before anything else
        public AfterImageEffectRenderer(){
            _histories = new Dictionary<int, CameraHistory>();
        }

        // Initialized is called only once before the first render call
        // so we use it to create our material and define intermediate RT name
        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/Yetman/PostProcess/Blend");
            _intermediate.Init("_BlendDestination");
        }

        // Called for each camera/injection point pair on each frame. Return true if the effect should be rendered for this camera.
        public override bool Setup(ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Get the current volume stack
            var stack = VolumeManager.instance.stack;
            // Get the corresponding volume component
            m_VolumeComponent = stack.GetComponent<AfterImageEffect>();
            // if blend value and time scale > 0, then we need to render this effect. 
            bool requireRendering = m_VolumeComponent.blend.value != Color.black && m_VolumeComponent.timeScale.value > 0;
            // if we don't need to execute this frame, we need to make sure that the history is invalidated
            // this solves an artifact where a very old history is used due to the effect being disabled for many frames   
            if(!requireRendering){
                // If the camera already had a history, invalidate it.
                if(_histories.TryGetValue(renderingData.cameraData.camera.GetInstanceID(), out CameraHistory history)){
                    history.Invalidated = true;
                }
            }
            return requireRendering;
        }

        // The actual rendering execution is done here
        public override void Render(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, ref RenderingData renderingData, CustomPostProcessInjectionPoint injectionPoint)
        {
            // Get camera instance id
            int id = renderingData.cameraData.camera.GetInstanceID();
            // Get an RT descriptor for temporary or history RTs.
            RenderTextureDescriptor descriptor = GetTempRTDescriptor(renderingData);

            CameraHistory history;
            // See if we already have a history for this camera
            if(_histories.TryGetValue(id, out history)){
                var frame = history.Frame;
                // If the camera target is resized, we need to resize the history too.
                if(frame.width != descriptor.width || frame.height != descriptor.height){
                    RenderTexture newframe = new RenderTexture(descriptor);
                    newframe.name = "_CameraHistoryTexture";
                    if(history.Invalidated) // if invalidated, blit from source to history
                        cmd.Blit(source, newframe);
                    else // if not invalidated, copy & resize the old history to the new size
                        Graphics.Blit(frame,  newframe); 
                    frame.Release();
                    history.Frame = newframe;
                } else if(history.Invalidated) {
                    cmd.Blit(source, frame); // if invalidated, blit from source to history
                }
                history.Invalidated = false; // No longer invalid :D
            } else {
                // If we had no history for this camera, create one for it.
                history = new CameraHistory(descriptor);
                history.Frame.name = "_CameraHistoryTexture";
                _histories.Add(id, history);
                cmd.Blit(source, history.Frame); // Copy frame from source to history
            }

            
            // set material properties
            if(m_Material != null){
                Color blend = m_VolumeComponent.blend.value;
                float power = Time.deltaTime / Mathf.Max(Mathf.Epsilon, m_VolumeComponent.timeScale.value);
                // The amound of blending should depend on the delta time to make fading time frame-rate independent. 
                blend.r = Mathf.Pow(blend.r, power);
                blend.g = Mathf.Pow(blend.g, power);
                blend.b = Mathf.Pow(blend.b, power);
                m_Material.SetColor(ShaderIDs.Blend, blend);
            }
            // set source texture
            cmd.SetGlobalTexture(ShaderIDs.Input, source);
            // set source texture
            cmd.SetGlobalTexture(ShaderIDs.Other, history.Frame);
            
            // See if the destination is the camera target. If yes, then we need to use an intermediate texture to avoid reading from destination
            bool isCameraTarget = destination == RenderTargetHandle.CameraTarget.Identifier();

            if(isCameraTarget){
                // Create a temporary target
                cmd.GetTemporaryRT(_intermediate.id, descriptor, FilterMode.Point);
                // blend current frame with history frame into the temporary target
                CoreUtils.DrawFullScreen(cmd, m_Material, _intermediate.Identifier());
                // Copy the temporary target to the destination and history
                cmd.Blit(_intermediate.Identifier(), destination);
                cmd.Blit(_intermediate.Identifier(), history.Frame);
                // Release the temporary target
                cmd.ReleaseTemporaryRT(_intermediate.id);
            } else {
                // the destination isn't the camera target, blend onto the destination directly.
                CoreUtils.DrawFullScreen(cmd, m_Material, destination);
                // Then copy the destination to the history
                cmd.Blit(destination,  history.Frame);
            }
        }

        // Dispose of the allocated resources
        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // Release every history RT
            foreach(var entry in _histories){
                entry.Value.Frame.Release();
            }
            // Clear the histories dictionary
            _histories.Clear();
        }
    }

}