using System;

namespace UnityEngine.Rendering.Universal.PostProcessing {
    
    /// <summary>
    /// Custom Post Processing injection points.
    /// </summary>
    public enum CustomPostProcessInjectPoint {
        /// <summary>After Opaque and Sky.</summary>
        AfterOpaqueAndSky,
        /// <summary>Before Post Processing.</summary>
        BeforePostProcess,
        /// <summary>After Post Processing.</summary>
        AfterPostProcess,
    }

    /// <summary>
    /// The Base Class for all the custom post process renderers
    /// </summary>
    public abstract class CustomPostProcessRenderer : IDisposable
    {
        
        /// <summary>
        /// True if you want your custom post process to be visible in the scene view. False otherwise.
        /// </summary>
        public virtual bool visibleInSceneView => true;

        
        /// <summary>
        /// Setup function, called once when the effect is constructed.
        /// </summary>
        public virtual void Setup(){}


        /// <summary>
        /// Setup function, called every frame once for each camera before render is called.
        /// </summary>
        /// <param name="renderingData">Current Rendering Data</param>
        /// <returns>
        /// True if render should be called for this camera. False Otherwise.
        /// </returns>
        public virtual bool SetupCamera(ref RenderingData renderingData){
            return true;
        }


        /// <summary>
        /// Called every frame for each camera when the post process needs to be rendered.
        /// </summary>
        /// <param name="cmd">Command Buffer used to issue your commands</param>
        /// <param name="renderingData">Current Rendering Data</param>
        /// <param name="source">Source Render Target, it contains the camera color buffer in it's current state</param>
        /// <param name="destination">Destination Render Target</param>
        public virtual void Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier destination){}

        /// <summary>
        /// Dispose function, called when the renderer is disposed.
        /// </summary>
        /// <param name="disposing"> If true, dispose of managed objects </param>
        public virtual void Dispose(bool disposing){}


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Create a descriptor for intermediate render targets based on the rendering data.
        /// Mainly used to create intermediate render targets.
        /// </summary>
        /// <returns>a descriptor similar to the camera target but with no depth buffer or multisampling</returns>
        public static RenderTextureDescriptor GetTempRTDescriptor(in RenderingData renderingData){
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            return descriptor;
        }
    }

    /// <summary>
    /// Use this attribute to mark classes that can be used as a custom post-processing renderer
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CustomPostProcessAttribute : System.Attribute {

        // Name of the effect in the custom post-processing render feature editor
        readonly string name;

        // In which render pass this effect should be injected
        readonly CustomPostProcessInjectPoint injectPoint;

        /// <value> Name of the effect in the custom post-processing render feature editor </value>
        public string Name => name;

        /// <value> In which render pass this effect should be injected </value>
        public CustomPostProcessInjectPoint InjectPoint => injectPoint;

        /// <summary>
        /// Marks this class as a custom post processing renderer
        /// </summary>
        /// <param name="name"> Name of the effect in the custom post-processing render feature editor </param>
        /// <param name="injectPoint"> In which render pass this effect should be injected </param>
        public CustomPostProcessAttribute(string name, CustomPostProcessInjectPoint injectPoint){
            this.name = name;
            this.injectPoint = injectPoint;
        }

        public static CustomPostProcessAttribute GetAttribute(Type type){
            var atttributes = type.GetCustomAttributes(typeof(CustomPostProcessAttribute), false);
            return (atttributes.Length != 0) ? (atttributes[0] as CustomPostProcessAttribute) : null;
        }

    }

}