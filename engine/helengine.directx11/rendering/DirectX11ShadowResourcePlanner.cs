namespace helengine.directx11 {
    /// <summary>
    /// Selects shadow-enabled lights and plans the DirectX11 shadow resources required for one frame.
    /// </summary>
    public sealed class DirectX11ShadowResourcePlanner {
        /// <summary>
        /// Default atlas width used by the current DirectX11 shadow planning slice.
        /// </summary>
        const int DefaultShadowAtlasWidth = 2048;
        /// <summary>
        /// Default atlas height used by the current DirectX11 shadow planning slice.
        /// </summary>
        const int DefaultShadowAtlasHeight = 2048;
        /// <summary>
        /// Default cube-face resolution used by the current DirectX11 point-shadow planning slice.
        /// </summary>
        const int DefaultPointShadowResolution = 512;

        /// <summary>
        /// Plans shadow resources for the supplied visible-light set under the backend shadow budget.
        /// </summary>
        /// <param name="lights">Visible lights already selected for forward execution.</param>
        /// <param name="maximumShadowedLights">Maximum number of shadow-enabled lights the backend wants active at once.</param>
        /// <returns>Grouped selected-shadow-light data and planned DirectX11 shadow resources.</returns>
        public DirectX11ShadowResourceSet PlanResources(IReadOnlyList<RenderFrameLightSubmission> lights, int maximumShadowedLights) {
            if (lights == null) {
                throw new ArgumentNullException(nameof(lights));
            } else if (maximumShadowedLights < 0) {
                throw new ArgumentOutOfRangeException(nameof(maximumShadowedLights), "Maximum shadowed-light count cannot be negative.");
            }

            if (maximumShadowedLights == 0 || lights.Count == 0) {
                return new DirectX11ShadowResourceSet(
                    Array.Empty<RenderFrameLightSubmission>(),
                    Array.Empty<DirectX11ShadowAtlasAllocation>(),
                    Array.Empty<DirectX11PointShadowResource>(),
                    0,
                    0);
            }

            List<RenderFrameLightSubmission> selectedShadowLights = new List<RenderFrameLightSubmission>(maximumShadowedLights);
            for (int lightIndex = 0; lightIndex < lights.Count; lightIndex++) {
                RenderFrameLightSubmission submission = lights[lightIndex];
                if (submission == null || !submission.Light.ShadowsEnabled) {
                    continue;
                }

                selectedShadowLights.Add(submission);
                if (selectedShadowLights.Count == maximumShadowedLights) {
                    break;
                }
            }

            if (selectedShadowLights.Count == 0) {
                return new DirectX11ShadowResourceSet(
                    Array.Empty<RenderFrameLightSubmission>(),
                    Array.Empty<DirectX11ShadowAtlasAllocation>(),
                    Array.Empty<DirectX11PointShadowResource>(),
                    0,
                    0);
            }

            DirectX11ShadowMapAtlas atlas = new DirectX11ShadowMapAtlas(DefaultShadowAtlasWidth, DefaultShadowAtlasHeight);
            DirectX11ShadowAtlasAllocation[] atlasAllocations = atlas.PlanAllocations(selectedShadowLights);
            List<DirectX11PointShadowResource> pointShadowResources = new List<DirectX11PointShadowResource>();
            for (int lightIndex = 0; lightIndex < selectedShadowLights.Count; lightIndex++) {
                RenderFrameLightSubmission submission = selectedShadowLights[lightIndex];
                if (submission.LightType != LightType.Point) {
                    continue;
                }

                pointShadowResources.Add(new DirectX11PointShadowResource(submission, DefaultPointShadowResolution));
            }

            return new DirectX11ShadowResourceSet(
                selectedShadowLights.ToArray(),
                atlasAllocations,
                pointShadowResources.ToArray(),
                DefaultShadowAtlasWidth,
                DefaultShadowAtlasHeight);
        }
    }
}
