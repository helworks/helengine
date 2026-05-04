namespace helengine.directx11 {
    /// <summary>
    /// Executes one concrete DirectX11 render pass selected by the shared render plan.
    /// </summary>
    public interface IDirectX11RenderPassExecutor {
        /// <summary>
        /// Executes the depth-only prepass for the current frame.
        /// </summary>
        /// <param name="context">Execution context that describes the current frame and output surface.</param>
        void ExecuteDepthPrepass(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the shadow-map pass for the current frame.
        /// </summary>
        /// <param name="context">Execution context that describes the current frame and output surface.</param>
        void ExecuteShadowPass(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the opaque forward pass for the current frame.
        /// </summary>
        /// <param name="context">Execution context that describes the current frame and output surface.</param>
        void ExecuteOpaqueForwardPass(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the transparent forward pass for the current frame.
        /// </summary>
        /// <param name="context">Execution context that describes the current frame and output surface.</param>
        void ExecuteTransparentForwardPass(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the post-process chain for the current frame.
        /// </summary>
        /// <param name="context">Execution context that describes the current frame and output surface.</param>
        void ExecutePostProcessPass(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes final presentation for the current frame.
        /// </summary>
        /// <param name="context">Execution context that describes the current frame and output surface.</param>
        void ExecutePresentPass(DirectX11RenderPassExecutionContext context);
    }
}
