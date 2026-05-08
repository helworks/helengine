namespace helengine.editor {
    /// <summary>
    /// Preview source that renders one selected model asset into an offscreen 3D viewport.
    /// </summary>
    public class ModelPreviewSource : IPreviewSource, IPreviewInteractionSource {
        /// <summary>
        /// Minimum zoom factor allowed relative to the model's fitted camera distance.
        /// </summary>
        const double MinimumZoomScale = 0.25d;
        /// <summary>
        /// Maximum zoom factor allowed relative to the model's fitted camera distance.
        /// </summary>
        const double MaximumZoomScale = 16.0d;
        /// <summary>
        /// Wheel multiplier applied once per input notch.
        /// </summary>
        const double ZoomStepFactor = 1.1d;
        /// <summary>
        /// Orbit sensitivity applied to mouse drag deltas.
        /// </summary>
        const double OrbitSensitivity = 0.01d;
        /// <summary>
        /// Pitch clamp used to keep the preview camera above the model horizon.
        /// </summary>
        const double MaxPitchRadians = (Math.PI * 0.5d) - 0.001d;
        /// <summary>
        /// Default yaw angle used when one preview is first created.
        /// </summary>
        const double DefaultYawRadians = 0.75d;
        /// <summary>
        /// Default pitch angle used when one preview is first created.
        /// </summary>
        const double DefaultPitchRadians = -0.35d;
        /// <summary>
        /// Padding multiplier applied to the fitted camera distance so the model is not framed too tightly.
        /// </summary>
        const double FramingPadding = 1.2d;

        /// <summary>
        /// Owning renderer used to allocate the preview render target.
        /// </summary>
        readonly RenderManager3D renderManager3D;
        /// <summary>
        /// Root entity that owns the preview model, light, and camera hierarchy.
        /// </summary>
        readonly EditorEntity previewEntity;
        /// <summary>
        /// Entity that holds the preview mesh.
        /// </summary>
        readonly EditorEntity modelEntity;
        /// <summary>
        /// Entity that holds the preview camera.
        /// </summary>
        readonly EditorEntity cameraEntity;
        /// <summary>
        /// Entity that holds the preview light.
        /// </summary>
        readonly EditorEntity lightEntity;
        /// <summary>
        /// Mesh component used to display the selected model.
        /// </summary>
        readonly MeshComponent previewMeshComponent;
        /// <summary>
        /// Camera component used to render the preview into an offscreen target.
        /// </summary>
        readonly CameraComponent previewCameraComponent;
        /// <summary>
        /// Directional light used to keep the preview model readable.
        /// </summary>
        readonly DirectionalLightComponent previewLightComponent;
        /// <summary>
        /// Runtime model currently displayed by the preview source.
        /// </summary>
        readonly RuntimeModel runtimeModel;
        /// <summary>
        /// Cached bounds minimum used to frame the model.
        /// </summary>
        readonly float3 boundsMin;
        /// <summary>
        /// Cached bounds maximum used to frame the model.
        /// </summary>
        readonly float3 boundsMax;
        /// <summary>
        /// Current offscreen render target.
        /// </summary>
        RenderTarget renderTarget;
        /// <summary>
        /// Current content size requested by the preview panel.
        /// </summary>
        int2 contentSize;
        /// <summary>
        /// Current yaw angle used by the orbit camera.
        /// </summary>
        double orbitYawRadians;
        /// <summary>
        /// Current pitch angle used by the orbit camera.
        /// </summary>
        double orbitPitchRadians;
        /// <summary>
        /// Current zoom scale relative to the model fit distance.
        /// </summary>
        double zoomScale;
        /// <summary>
        /// Tracks whether the source has been disposed.
        /// </summary>
        bool isDisposed;

        /// <summary>
        /// Initializes a new model preview source for one runtime model.
        /// </summary>
        /// <param name="runtimeModel">Runtime model to preview.</param>
        /// <param name="renderManager3D">Renderer used to allocate the offscreen render target.</param>
        public ModelPreviewSource(RuntimeModel runtimeModel, RenderManager3D renderManager3D) {
            if (runtimeModel == null) {
                throw new ArgumentNullException(nameof(runtimeModel));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }

            this.runtimeModel = runtimeModel;
            this.renderManager3D = renderManager3D;
            boundsMin = runtimeModel.BoundsMin;
            boundsMax = runtimeModel.BoundsMax;

            previewEntity = CreateHiddenEntity("Model Preview Root");
            modelEntity = CreateHiddenEntity("Model Preview Model");
            cameraEntity = CreateHiddenEntity("Model Preview Camera");
            lightEntity = CreateHiddenEntity("Model Preview Light");

            previewMeshComponent = new MeshComponent {
                Model = runtimeModel,
                Material = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial()
            };
            modelEntity.AddComponent(previewMeshComponent);

            previewCameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 0,
                ClearSettings = new CameraClearSettings(true, new float4(0.12f, 0.13f, 0.15f, 1f), true, 1f, false, 0)
            };
            cameraEntity.AddComponent(previewCameraComponent);

            previewLightComponent = new DirectionalLightComponent {
                ShadowsEnabled = false,
                Intensity = 1.6f,
                Color = new float4(1f, 1f, 1f, 1f)
            };
            lightEntity.AddComponent(previewLightComponent);

            previewEntity.AddChild(modelEntity);
            previewEntity.AddChild(cameraEntity);
            previewEntity.AddChild(lightEntity);

            float4 lightOrientation;
            float4.CreateFromYawPitchRoll((float)-0.9d, (float)-0.75d, 0f, out lightOrientation);
            lightEntity.Orientation = lightOrientation;

            contentSize = new int2(1, 1);
            orbitYawRadians = DefaultYawRadians;
            orbitPitchRadians = DefaultPitchRadians;
            zoomScale = 1d;
            Resize(contentSize);
        }

        /// <summary>
        /// Gets the preview camera component used by the source.
        /// </summary>
        public CameraComponent PreviewCamera => previewCameraComponent;

        /// <summary>
        /// Gets the current render target used by the preview camera.
        /// </summary>
        public RenderTarget RenderTarget => renderTarget;

        /// <summary>
        /// Gets the current preview texture exposed by the source.
        /// </summary>
        public RuntimeTexture Texture => renderTarget;

        /// <summary>
        /// Creates one preview source from a selected model asset entry.
        /// </summary>
        /// <param name="entry">Selected asset browser entry.</param>
        /// <param name="assetImportManager">Asset import manager used for file-system model loading.</param>
        /// <param name="renderManager3D">Renderer used to build runtime models and preview targets.</param>
        /// <param name="source">Created preview source when the model could be resolved.</param>
        /// <returns>True when the entry resolved to a model preview source; otherwise false.</returns>
        public static bool TryCreate(AssetBrowserEntry entry, AssetImportManager assetImportManager, RenderManager3D renderManager3D, out ModelPreviewSource source) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }

            source = null;
            if (entry.IsDirectory || entry.EntryKind != AssetEntryKind.Model) {
                return false;
            }

            RuntimeModel runtimeModel;
            if (entry.IsGenerated) {
                runtimeModel = GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
            } else {
                ModelAsset modelAsset;
                if (!assetImportManager.TryLoadModelAsset(entry.FullPath, out modelAsset) || modelAsset == null) {
                    return false;
                }

                runtimeModel = renderManager3D.BuildModelFromRaw(modelAsset);
            }

            source = new ModelPreviewSource(runtimeModel, renderManager3D);
            return true;
        }

        /// <summary>
        /// Updates the preview camera and keeps the orbit transform synchronized with the current input state.
        /// </summary>
        public void Update() {
            if (isDisposed) {
                return;
            }

            UpdateCameraTransform();
        }

        /// <summary>
        /// Resizes the preview render target to match the active panel content size.
        /// </summary>
        /// <param name="contentSize">Usable panel content size in pixels.</param>
        public void Resize(int2 contentSize) {
            if (isDisposed) {
                return;
            }

            this.contentSize = new int2(Math.Max(1, contentSize.X), Math.Max(1, contentSize.Y));
            int targetWidth = this.contentSize.X;
            int targetHeight = this.contentSize.Y;
            if (renderTarget != null && renderTarget.Width == targetWidth && renderTarget.Height == targetHeight) {
                previewCameraComponent.Viewport = BuildViewport();
                UpdateCameraTransform();
                return;
            }

            DisposeRenderTarget();
            renderTarget = renderManager3D.CreateRenderTarget(targetWidth, targetHeight);
            previewCameraComponent.RenderTarget = renderTarget;
            previewCameraComponent.Viewport = BuildViewport();
            UpdateCameraTransform();
        }

        /// <summary>
        /// Handles one mouse-wheel delta by adjusting the camera distance from the model.
        /// </summary>
        /// <param name="wheelDelta">Raw mouse-wheel delta from the input backend.</param>
        public void HandleMouseWheel(int wheelDelta) {
            if (isDisposed || wheelDelta == 0) {
                return;
            }

            double notchDelta = wheelDelta / 120.0d;
            double nextZoomScale = zoomScale * Math.Pow(ZoomStepFactor, notchDelta);
            zoomScale = Math.Max(MinimumZoomScale, Math.Min(MaximumZoomScale, nextZoomScale));
            UpdateCameraTransform();
        }

        /// <summary>
        /// Handles one left-button drag delta by orbiting the camera around the model center.
        /// </summary>
        /// <param name="delta">Mouse delta accumulated since the previous frame.</param>
        public void HandleMouseDrag(int2 delta) {
            if (isDisposed || (delta.X == 0 && delta.Y == 0)) {
                return;
            }

            orbitYawRadians -= delta.X * OrbitSensitivity;
            orbitPitchRadians -= delta.Y * OrbitSensitivity;
            orbitPitchRadians = Math.Max(-MaxPitchRadians, Math.Min(MaxPitchRadians, orbitPitchRadians));
            UpdateCameraTransform();
        }

        /// <summary>
        /// Releases the preview root and its render target.
        /// </summary>
        public void Dispose() {
            if (isDisposed) {
                return;
            }

            isDisposed = true;
            DisposeRenderTarget();
            Core.Instance.ObjectManager.RemoveCamera(previewCameraComponent);
            Core.Instance.ObjectManager.RemoveEntity(previewEntity);
            previewEntity.Dispose();
        }

        /// <summary>
        /// Applies the current orbit and zoom state to the preview camera transform.
        /// </summary>
        void UpdateCameraTransform() {
            float3 boundsCenter = GetBoundsCenter();
            double fitDistance = ResolveFitDistance();
            double cameraDistance = fitDistance / zoomScale;

            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll((float)orbitYawRadians, (float)orbitPitchRadians, 0f, out cameraOrientation);
            cameraOrientation.Normalize();

            float3 forward = float4.RotateVector(new float3(0f, 0f, -1f), cameraOrientation);
            cameraEntity.LocalOrientation = cameraOrientation;
            cameraEntity.LocalPosition = forward * (float)-cameraDistance;
            modelEntity.LocalPosition = new float3(-boundsCenter.X, -boundsCenter.Y, -boundsCenter.Z);
        }

        /// <summary>
        /// Resolves the world-space center of the cached model bounds.
        /// </summary>
        /// <returns>Bounding-box center used as the preview orbit target.</returns>
        float3 GetBoundsCenter() {
            return new float3(
                (boundsMin.X + boundsMax.X) * 0.5f,
                (boundsMin.Y + boundsMax.Y) * 0.5f,
                (boundsMin.Z + boundsMax.Z) * 0.5f);
        }

        /// <summary>
        /// Resolves the distance used to frame the model at the current panel aspect ratio.
        /// </summary>
        /// <returns>Camera distance that fits the full bounding sphere inside the viewport.</returns>
        double ResolveFitDistance() {
            float3 halfExtents = new float3(
                Math.Abs(boundsMax.X - boundsMin.X) * 0.5f,
                Math.Abs(boundsMax.Y - boundsMin.Y) * 0.5f,
                Math.Abs(boundsMax.Z - boundsMin.Z) * 0.5f);
            double radius = Math.Max(0.5d, Math.Sqrt(
                (double)halfExtents.X * halfExtents.X +
                (double)halfExtents.Y * halfExtents.Y +
                (double)halfExtents.Z * halfExtents.Z));
            double aspectRatio = contentSize.X / (double)contentSize.Y;
            double halfVerticalFov = Math.PI / 8.0d;
            double halfHorizontalFov = Math.Atan(Math.Tan(halfVerticalFov) * aspectRatio);
            double effectiveHalfFov = Math.Min(halfVerticalFov, halfHorizontalFov);
            double fitDistance = radius / Math.Sin(effectiveHalfFov);
            return Math.Max(0.1d, fitDistance * FramingPadding);
        }

        /// <summary>
        /// Builds the preview camera viewport from the current content size.
        /// </summary>
        /// <returns>Viewport rectangle sized to the offscreen preview target.</returns>
        float4 BuildViewport() {
            return new float4(0f, 0f, contentSize.X, contentSize.Y);
        }

        /// <summary>
        /// Releases the current render target when one is owned.
        /// </summary>
        void DisposeRenderTarget() {
            if (renderTarget is IDisposable disposableTarget) {
                disposableTarget.Dispose();
            }

            previewCameraComponent.RenderTarget = null;
            renderTarget = null;
        }

        /// <summary>
        /// Creates one hidden editor entity used only by the preview source.
        /// </summary>
        /// <param name="name">Display name assigned to the hidden entity.</param>
        /// <returns>Hidden editor entity instance.</returns>
        static EditorEntity CreateHiddenEntity(string name) {
            EditorEntity entity = new EditorEntity {
                Name = name,
                Hidden = true,
                InternalEntity = true,
                LayerMask = EditorLayerMasks.SceneObjects
            };
            return entity;
        }
    }
}
