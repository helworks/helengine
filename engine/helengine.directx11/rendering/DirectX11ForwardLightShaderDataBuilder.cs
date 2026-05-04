namespace helengine.directx11 {
    /// <summary>
    /// Builds the packed forward-light constant-buffer payload consumed by the built-in DirectX11 forward shader.
    /// </summary>
    public sealed class DirectX11ForwardLightShaderDataBuilder {
        /// <summary>
        /// Maximum number of selected lights packed into the built-in DirectX11 forward shader buffer.
        /// </summary>
        public const int MaximumPackedLightCount = 4;

        /// <summary>
        /// Builds one packed forward-light buffer payload from the selected lights for the current frame.
        /// </summary>
        /// <param name="selectedLights">Selected frame lights that survived backend budgeting.</param>
        /// <returns>Packed forward-light constant-buffer payload.</returns>
        public DirectX11ForwardLightShaderData Build(IReadOnlyList<RenderFrameLightSubmission> selectedLights) {
            if (selectedLights == null) {
                throw new ArgumentNullException(nameof(selectedLights));
            }

            DirectX11ForwardLightShaderData data = new DirectX11ForwardLightShaderData();
            int activeLightCount = selectedLights.Count;
            if (activeLightCount > MaximumPackedLightCount) {
                activeLightCount = MaximumPackedLightCount;
            }

            data.LightMetadata = new float4(activeLightCount, 0f, 0f, 0f);
            for (int lightIndex = 0; lightIndex < activeLightCount; lightIndex++) {
                DirectX11ForwardLightSlotShaderData slot = BuildSlot(selectedLights[lightIndex]);
                SetSlot(ref data, lightIndex, slot);
            }

            return data;
        }

        /// <summary>
        /// Builds one packed light slot from one selected frame light.
        /// </summary>
        /// <param name="submission">Selected frame light to pack.</param>
        /// <returns>Packed light slot consumed by the shader.</returns>
        DirectX11ForwardLightSlotShaderData BuildSlot(RenderFrameLightSubmission submission) {
            if (submission == null) {
                throw new ArgumentNullException(nameof(submission));
            }

            LightComponent light = submission.Light;
            Entity entity = light.Parent;
            if (entity == null) {
                throw new InvalidOperationException("Selected lights must be attached to an entity before they can be packed for DirectX11 forward shading.");
            }

            return new DirectX11ForwardLightSlotShaderData {
                ColorAndType = BuildColorAndType(light),
                DirectionAndShadow = BuildDirectionAndShadow(light, entity),
                PositionAndRange = BuildPositionAndRange(light, entity),
                SpotAngles = BuildSpotAngles(light)
            };
        }

        /// <summary>
        /// Builds the packed light color and type payload for one authored light.
        /// </summary>
        /// <param name="light">Authored light to pack.</param>
        /// <returns>Packed color and type payload.</returns>
        float4 BuildColorAndType(LightComponent light) {
            float4 color = light.Color;
            return new float4(
                color.X * light.Intensity,
                color.Y * light.Intensity,
                color.Z * light.Intensity,
                (float)light.LightType);
        }

        /// <summary>
        /// Builds the packed light direction and shadow payload for one authored light.
        /// </summary>
        /// <param name="light">Authored light to pack.</param>
        /// <param name="entity">Entity that owns the authored light.</param>
        /// <returns>Packed direction and shadow payload.</returns>
        float4 BuildDirectionAndShadow(LightComponent light, Entity entity) {
            float3 direction = LightDirectionUtility.GetEntityForwardDirection(entity);
            return new float4(direction.X, direction.Y, direction.Z, light.ShadowStrength);
        }

        /// <summary>
        /// Builds the packed light position and range payload for one authored light.
        /// </summary>
        /// <param name="light">Authored light to pack.</param>
        /// <param name="entity">Entity that owns the authored light.</param>
        /// <returns>Packed position and range payload.</returns>
        float4 BuildPositionAndRange(LightComponent light, Entity entity) {
            float3 position = entity.Position;
            if (light is PointLightComponent pointLight) {
                return new float4(position.X, position.Y, position.Z, pointLight.Range);
            } else if (light is SpotLightComponent spotLight) {
                return new float4(position.X, position.Y, position.Z, spotLight.Range);
            }

            return new float4(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// Builds the packed spot-light cone payload for one authored light.
        /// </summary>
        /// <param name="light">Authored light to pack.</param>
        /// <returns>Packed spot-light cone payload.</returns>
        float4 BuildSpotAngles(LightComponent light) {
            if (light is not SpotLightComponent spotLight) {
                return new float4(0f, 0f, 0f, 0f);
            }

            double innerRadians = spotLight.InnerConeAngleDegrees * (Math.PI / 180.0);
            double outerRadians = spotLight.OuterConeAngleDegrees * (Math.PI / 180.0);
            float innerCosine = (float)Math.Cos(innerRadians);
            float outerCosine = (float)Math.Cos(outerRadians);
            return new float4(innerCosine, outerCosine, 0f, 0f);
        }

        /// <summary>
        /// Writes one packed light slot into the requested slot index.
        /// </summary>
        /// <param name="data">Packed forward-light buffer payload receiving the slot.</param>
        /// <param name="slotIndex">Zero-based slot index to write.</param>
        /// <param name="slot">Packed light slot to write.</param>
        void SetSlot(ref DirectX11ForwardLightShaderData data, int slotIndex, DirectX11ForwardLightSlotShaderData slot) {
            switch (slotIndex) {
                case 0:
                    data.Light0 = slot;
                    return;
                case 1:
                    data.Light1 = slot;
                    return;
                case 2:
                    data.Light2 = slot;
                    return;
                case 3:
                    data.Light3 = slot;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slotIndex), "Forward light slot index exceeds the packed shader-light budget.");
            }
        }
    }
}
