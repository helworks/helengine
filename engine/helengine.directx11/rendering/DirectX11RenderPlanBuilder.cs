namespace helengine.directx11 {
    /// <summary>
    /// Builds the ordered DirectX11 forward-render pass list for one extracted render frame.
    /// </summary>
    public sealed class DirectX11RenderPlanBuilder {
        /// <summary>
        /// Builds the DirectX11 forward-render pass list for one extracted frame.
        /// </summary>
        /// <param name="frame">Extracted render frame to analyze.</param>
        /// <param name="capabilities">Backend capability profile that constrains the selected passes.</param>
        /// <returns>Ordered pass list for the DirectX11 backend.</returns>
        public RenderPlan Build(RenderFrame frame, RendererBackendCapabilityProfile capabilities) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }

            List<RenderPassKind> passes = new List<RenderPassKind>();
            if (frame.Camera.RenderSettings.DepthPrepassMode == DepthPrepassMode.Always) {
                passes.Add(RenderPassKind.DepthPrepass);
            }

            DirectX11ShadowPlan shadowPlan = new DirectX11ShadowPlan(
                frame.ShadowCasterSubmissions.Count > 0
                && capabilities.MaximumShadowedLights > 0
                && HasShadowEnabledLight(frame.LightSubmissions));
            if (shadowPlan.HasShadowPass) {
                passes.Add(RenderPassKind.Shadow);
            }

            passes.Add(RenderPassKind.OpaqueForward);
            if (frame.HasTransparentDrawables) {
                passes.Add(RenderPassKind.TransparentForward);
            }

            DirectX11PostProcessPlan postProcessPlan = new DirectX11PostProcessPlan(frame.Camera.RenderSettings.PostProcessTier);
            if (postProcessPlan.PostProcessTier != PostProcessTier.Disabled) {
                passes.Add(RenderPassKind.PostProcess);
            }

            passes.Add(RenderPassKind.Present);
            return new RenderPlan(passes);
        }

        /// <summary>
        /// Determines whether the extracted frame contains at least one light that requests shadow rendering.
        /// </summary>
        /// <param name="lights">Visible lights extracted for the frame.</param>
        /// <returns><c>true</c> when at least one light requests shadows; otherwise <c>false</c>.</returns>
        bool HasShadowEnabledLight(IReadOnlyList<RenderFrameLightSubmission> lights) {
            if (lights == null) {
                throw new ArgumentNullException(nameof(lights));
            }

            for (int lightIndex = 0; lightIndex < lights.Count; lightIndex++) {
                RenderFrameLightSubmission light = lights[lightIndex];
                if (light != null && light.Light.ShadowsEnabled) {
                    return true;
                }
            }

            return false;
        }
    }
}
