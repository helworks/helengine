namespace helengine.directx11 {
    /// <summary>
    /// Builds the ordered DirectX11 post-process chain for one camera render.
    /// </summary>
    public sealed class DirectX11PostProcessChain {
        /// <summary>
        /// Builds the post-process passes required by the supplied camera settings.
        /// </summary>
        /// <param name="renderSettings">Camera render settings that select post-processing behavior.</param>
        /// <param name="renderTarget">Rendered source target consumed by the chain.</param>
        /// <returns>Ordered post-process passes for the frame.</returns>
        public DirectX11PostProcessPass[] Build(CameraRenderSettings renderSettings, RenderTarget renderTarget) {
            if (renderSettings == null) {
                throw new ArgumentNullException(nameof(renderSettings));
            } else if (renderTarget == null) {
                throw new ArgumentNullException(nameof(renderTarget));
            } else if (renderSettings.PostProcessTier == PostProcessTier.Disabled) {
                return [];
            } else if (!renderTarget.CanSampleAsTexture) {
                throw new InvalidOperationException("Post-process chains require one render target that can be sampled as a texture.");
            }

            List<DirectX11PostProcessPass> passes = [
                new DirectX11PostProcessPass("tonemap")
            ];

            if (renderSettings.PostProcessTier != PostProcessTier.Low) {
                passes.Add(new DirectX11PostProcessPass("bloom"));
            }

            passes.Add(new DirectX11PostProcessPass("fxaa"));
            return passes.ToArray();
        }
    }
}
