namespace helengine.directx11 {
    /// <summary>
    /// Packs atlas-shadow data for the built-in DirectX11 forward shader.
    /// </summary>
    public sealed class DirectX11ShadowShaderDataBuilder {
        /// <summary>
        /// Default forward axis used to derive light-facing directions from entity orientation.
        /// </summary>
        static readonly float3 DefaultForward = new float3(0f, 0f, -1f);
        /// <summary>
        /// Default up axis used to build light view matrices.
        /// </summary>
        static readonly float3 DefaultUp = new float3(0f, 1f, 0f);

        /// <summary>
        /// Builds atlas-shadow shader data for the selected forward-light set and planned shadow resources of the current camera frame.
        /// </summary>
        /// <param name="camera">Camera whose frame is currently being rendered.</param>
        /// <param name="selectedLights">Forward-light slots selected for the current frame.</param>
        /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
        /// <returns>Packed atlas-shadow shader data.</returns>
        public DirectX11ShadowShaderData Build(
            CameraComponent camera,
            IReadOnlyList<RenderFrameLightSubmission> selectedLights,
            DirectX11ShadowResourceSet shadowResourceSet) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            } else if (selectedLights == null) {
                throw new ArgumentNullException(nameof(selectedLights));
            } else if (shadowResourceSet == null) {
                throw new ArgumentNullException(nameof(shadowResourceSet));
            }

            DirectX11ShadowShaderData data = new DirectX11ShadowShaderData {
                ShadowMetadata = new float4(
                    shadowResourceSet.AtlasWidth > 0 && shadowResourceSet.AtlasHeight > 0 ? 1f : 0f,
                    shadowResourceSet.AtlasWidth > 0 ? 1f / shadowResourceSet.AtlasWidth : 0f,
                    shadowResourceSet.AtlasHeight > 0 ? 1f / shadowResourceSet.AtlasHeight : 0f,
                    shadowResourceSet.AtlasAllocations.Count)
            };

            for (int lightIndex = 0; lightIndex < selectedLights.Count && lightIndex < 4; lightIndex++) {
                RenderFrameLightSubmission selectedLight = selectedLights[lightIndex];
                DirectX11ShadowAtlasAllocation allocation = FindAtlasAllocation(shadowResourceSet.AtlasAllocations, selectedLight);
                int pointShadowResourceIndex = FindPointShadowResourceIndex(shadowResourceSet.PointShadowResources, selectedLight);
                DirectX11ShadowLightSlotShaderData slot = BuildSlot(
                    camera,
                    selectedLight,
                    allocation,
                    pointShadowResourceIndex,
                    shadowResourceSet.AtlasWidth,
                    shadowResourceSet.AtlasHeight);
                AssignSlot(ref data, lightIndex, slot);
            }

            return data;
        }

        /// <summary>
        /// Finds the atlas allocation corresponding to one selected forward light.
        /// </summary>
        /// <param name="atlasAllocations">Atlas allocations planned for the frame.</param>
        /// <param name="selectedLight">Selected forward light whose atlas allocation should be resolved.</param>
        /// <returns>Matching atlas allocation, or null when the light does not use atlas shadows.</returns>
        DirectX11ShadowAtlasAllocation FindAtlasAllocation(
            IReadOnlyList<DirectX11ShadowAtlasAllocation> atlasAllocations,
            RenderFrameLightSubmission selectedLight) {
            if (atlasAllocations == null) {
                throw new ArgumentNullException(nameof(atlasAllocations));
            } else if (selectedLight == null) {
                throw new ArgumentNullException(nameof(selectedLight));
            }

            for (int allocationIndex = 0; allocationIndex < atlasAllocations.Count; allocationIndex++) {
                DirectX11ShadowAtlasAllocation allocation = atlasAllocations[allocationIndex];
                if (allocation != null && ReferenceEquals(allocation.Light.Light, selectedLight.Light)) {
                    return allocation;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the point-shadow cube resource index corresponding to one selected forward light.
        /// </summary>
        /// <param name="pointShadowResources">Point-shadow resources planned for the frame.</param>
        /// <param name="selectedLight">Selected forward light whose cube resource should be resolved.</param>
        /// <returns>Matching cube resource index, or <c>-1</c> when the light does not use a point-shadow resource.</returns>
        int FindPointShadowResourceIndex(
            IReadOnlyList<DirectX11PointShadowResource> pointShadowResources,
            RenderFrameLightSubmission selectedLight) {
            if (pointShadowResources == null) {
                throw new ArgumentNullException(nameof(pointShadowResources));
            } else if (selectedLight == null) {
                throw new ArgumentNullException(nameof(selectedLight));
            }

            for (int resourceIndex = 0; resourceIndex < pointShadowResources.Count; resourceIndex++) {
                DirectX11PointShadowResource resource = pointShadowResources[resourceIndex];
                if (resource != null && ReferenceEquals(resource.Light.Light, selectedLight.Light)) {
                    return resourceIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Builds one packed atlas-shadow slot for the supplied allocation.
        /// </summary>
        /// <param name="camera">Camera whose frame is currently being rendered.</param>
        /// <param name="allocation">Atlas allocation assigned to one light, or null when the slot is unshadowed.</param>
        /// <returns>Packed atlas-shadow slot.</returns>
        DirectX11ShadowLightSlotShaderData BuildSlot(
            CameraComponent camera,
            RenderFrameLightSubmission selectedLight,
            DirectX11ShadowAtlasAllocation allocation,
            int pointShadowResourceIndex,
            int atlasWidth,
            int atlasHeight) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            } else if (selectedLight == null) {
                throw new ArgumentNullException(nameof(selectedLight));
            }

            if (allocation == null && pointShadowResourceIndex < 0) {
                return new DirectX11ShadowLightSlotShaderData();
            }

            if (allocation == null) {
                return new DirectX11ShadowLightSlotShaderData {
                    Metadata = new float4(1f, selectedLight.Light.ShadowStrength, 2f, pointShadowResourceIndex)
                };
            }

            LightComponent light = allocation.Light.Light;
            float4x4 worldToShadowClip = BuildWorldToShadowClipMatrix(camera, allocation);
            return new DirectX11ShadowLightSlotShaderData {
                AtlasRect = new float4(
                    atlasWidth > 0 ? allocation.X / (float)atlasWidth : 0f,
                    atlasHeight > 0 ? allocation.Y / (float)atlasHeight : 0f,
                    atlasWidth > 0 ? allocation.Width / (float)atlasWidth : 0f,
                    atlasHeight > 0 ? allocation.Height / (float)atlasHeight : 0f),
                Metadata = new float4(1f, light.ShadowStrength, 1f, 0f),
                WorldToShadowClip = worldToShadowClip
            };
        }

        /// <summary>
        /// Assigns one packed slot into the requested light index of the shadow shader data.
        /// </summary>
        /// <param name="data">Shadow shader data being assembled.</param>
        /// <param name="lightIndex">Selected forward-light slot index.</param>
        /// <param name="slot">Packed atlas-shadow slot to assign.</param>
        void AssignSlot(ref DirectX11ShadowShaderData data, int lightIndex, DirectX11ShadowLightSlotShaderData slot) {
            if (lightIndex == 0) {
                data.Light0AtlasRect = slot.AtlasRect;
                data.Light0Metadata = slot.Metadata;
                data.Light0WorldToShadowClip = slot.WorldToShadowClip;
            } else if (lightIndex == 1) {
                data.Light1AtlasRect = slot.AtlasRect;
                data.Light1Metadata = slot.Metadata;
                data.Light1WorldToShadowClip = slot.WorldToShadowClip;
            } else if (lightIndex == 2) {
                data.Light2AtlasRect = slot.AtlasRect;
                data.Light2Metadata = slot.Metadata;
                data.Light2WorldToShadowClip = slot.WorldToShadowClip;
            } else if (lightIndex == 3) {
                data.Light3AtlasRect = slot.AtlasRect;
                data.Light3Metadata = slot.Metadata;
                data.Light3WorldToShadowClip = slot.WorldToShadowClip;
            }
        }

        /// <summary>
        /// Builds a transposed world-to-shadow-clip matrix for one atlas allocation.
        /// </summary>
        /// <param name="camera">Camera whose frame is currently being rendered.</param>
        /// <param name="allocation">Atlas allocation whose light projection should be built.</param>
        /// <returns>Transposed world-to-shadow-clip matrix.</returns>
        public float4x4 BuildShadowViewProjectionMatrix(CameraComponent camera, DirectX11ShadowAtlasAllocation allocation) {
            LightComponent light = allocation.Light.Light;
            Entity entity = light.Parent;
            if (entity == null) {
                throw new InvalidOperationException("Shadowed lights must be attached to entities.");
            } else if (camera.Parent == null) {
                throw new InvalidOperationException("Shadow camera calculations require the render camera to be attached to an entity.");
            }

            float4x4 view;
            float4x4 projection;
            if (light.LightType == LightType.Directional) {
                float3 rotatedForward = float4.RotateVector(DefaultForward, entity.Orientation);
                float3 lightDirection = Normalize(new float3(-rotatedForward.X, -rotatedForward.Y, -rotatedForward.Z));
                float shadowDistance = (float)Math.Max(1.0, camera.RenderSettings.ShadowDistance);
                float3 target = camera.Parent.Position;
                float3 lightPosition = target + (lightDirection * (float)(shadowDistance * 0.5));
                float3 up = Math.Abs(float3.Dot(lightDirection, DefaultUp)) > 0.99f ? new float3(0f, 0f, 1f) : DefaultUp;
                float4x4.CreateLookAt(ref lightPosition, ref target, ref up, out view);
                float halfDistance = (float)(shadowDistance * 0.5);
                float4x4.CreateOrthographicOffCenter(-halfDistance, halfDistance, -halfDistance, halfDistance, 0.1f, shadowDistance, out projection);
            } else if (light.LightType == LightType.Spot) {
                SpotLightComponent spotLight = (SpotLightComponent)light;
                float3 lightDirection = Normalize(float4.RotateVector(DefaultForward, entity.Orientation));
                float3 lightPosition = entity.Position;
                float3 target = lightPosition + lightDirection;
                float3 up = Math.Abs(float3.Dot(lightDirection, DefaultUp)) > 0.99f ? new float3(0f, 0f, 1f) : DefaultUp;
                float4x4.CreateLookAt(ref lightPosition, ref target, ref up, out view);
                float fieldOfViewRadians = (float)(spotLight.OuterConeAngleDegrees * (Math.PI / 180.0));
                float4x4.CreatePerspectiveFieldOfView(fieldOfViewRadians, 1f, 0.1f, spotLight.Range, out projection);
            } else {
                return new float4x4();
            }

            float4x4 worldToShadowClip;
            float4x4.Multiply(ref view, ref projection, out worldToShadowClip);
            return worldToShadowClip;
        }

        /// <summary>
        /// Builds a transposed world-to-shadow-clip matrix for one atlas allocation.
        /// </summary>
        /// <param name="camera">Camera whose frame is currently being rendered.</param>
        /// <param name="allocation">Atlas allocation whose light projection should be built.</param>
        /// <returns>Transposed world-to-shadow-clip matrix.</returns>
        public float4x4 BuildWorldToShadowClipMatrix(CameraComponent camera, DirectX11ShadowAtlasAllocation allocation) {
            float4x4 worldToShadowClip = BuildShadowViewProjectionMatrix(camera, allocation);
            float4x4 transposed;
            float4x4.Transpose(ref worldToShadowClip, out transposed);
            return transposed;
        }

        /// <summary>
        /// Builds an untransposed view-projection matrix for one point-shadow cube face.
        /// </summary>
        /// <param name="pointLight">Point light whose cube-face projection should be built.</param>
        /// <param name="faceIndex">Cube-face index in the range <c>0-5</c>.</param>
        /// <returns>Untransposed view-projection matrix for the requested point-shadow cube face.</returns>
        public float4x4 BuildPointShadowViewProjectionMatrix(PointLightComponent pointLight, int faceIndex) {
            if (pointLight == null) {
                throw new ArgumentNullException(nameof(pointLight));
            } else if (pointLight.Parent == null) {
                throw new InvalidOperationException("Point-shadow lights must be attached to entities.");
            } else if (faceIndex < 0 || faceIndex > 5) {
                throw new ArgumentOutOfRangeException(nameof(faceIndex), "Point-shadow face index must be in the range 0-5.");
            }

            float3 lightPosition = pointLight.Parent.Position;
            float3 faceDirection = GetPointShadowFaceDirection(faceIndex);
            float3 faceUp = GetPointShadowFaceUp(faceIndex);
            float3 target = lightPosition + faceDirection;
            float4x4 view;
            float4x4.CreateLookAt(ref lightPosition, ref target, ref faceUp, out view);
            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)(Math.PI * 0.5), 1f, 0.1f, pointLight.Range, out projection);
            float4x4 viewProjection;
            float4x4.Multiply(ref view, ref projection, out viewProjection);
            return viewProjection;
        }

        /// <summary>
        /// Gets the cube-face forward direction used while rendering point-light shadows.
        /// </summary>
        /// <param name="faceIndex">Cube-face index in the range <c>0-5</c>.</param>
        /// <returns>Forward direction for the requested cube face.</returns>
        float3 GetPointShadowFaceDirection(int faceIndex) {
            if (faceIndex == 0) {
                return new float3(1f, 0f, 0f);
            } else if (faceIndex == 1) {
                return new float3(-1f, 0f, 0f);
            } else if (faceIndex == 2) {
                return new float3(0f, 1f, 0f);
            } else if (faceIndex == 3) {
                return new float3(0f, -1f, 0f);
            } else if (faceIndex == 4) {
                return new float3(0f, 0f, 1f);
            }

            return new float3(0f, 0f, -1f);
        }

        /// <summary>
        /// Gets the cube-face up vector used while rendering point-light shadows.
        /// </summary>
        /// <param name="faceIndex">Cube-face index in the range <c>0-5</c>.</param>
        /// <returns>Up vector for the requested cube face.</returns>
        float3 GetPointShadowFaceUp(int faceIndex) {
            if (faceIndex == 2) {
                return new float3(0f, 0f, -1f);
            } else if (faceIndex == 3) {
                return new float3(0f, 0f, 1f);
            }

            return DefaultUp;
        }

        /// <summary>
        /// Normalizes one vector using double-precision length calculations.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized vector, or the default forward axis when the length is too small.</returns>
        float3 Normalize(float3 value) {
            double lengthSquared = (value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z);
            if (lengthSquared <= 0.0000001) {
                return DefaultForward;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }
    }
}
