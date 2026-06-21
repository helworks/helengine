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
        /// Screen-space pan multiplier applied once per pixel of middle-button drag.
        /// </summary>
        const double PanSensitivity = 0.004d;
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
        /// Extra multiplier applied to the preview clip planes so fitted models are not clipped by near or far plane rounding.
        /// </summary>
        const double ClipPlanePadding = 1.25d;

        /// <summary>
        /// Owning renderer used to allocate the preview render target.
        /// </summary>
        readonly RenderManager3D renderManager3D;
        /// <summary>
        /// Shared neutral diffuse texture used by preview materials that do not have an authored diffuse map.
        /// </summary>
        static RuntimeTexture neutralPreviewTexture;
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
        /// Screen-space pan offset accumulated from middle-button drag input.
        /// </summary>
        float2 panOffset;
        /// <summary>
        /// Tracks whether the source has been disposed.
        /// </summary>
        bool isDisposed;

        /// <summary>
        /// Initializes a new model preview source for one runtime model.
        /// </summary>
        /// <param name="runtimeModel">Runtime model to preview.</param>
        /// <param name="renderManager3D">Renderer used to allocate the offscreen render target.</param>
        public ModelPreviewSource(RuntimeModel runtimeModel, RenderManager3D renderManager3D)
            : this(runtimeModel, Array.Empty<RuntimeMaterial>(), renderManager3D) {
        }

        /// <summary>
        /// Initializes a new model preview source for one runtime model and one ordered material array.
        /// </summary>
        /// <param name="runtimeModel">Runtime model to preview.</param>
        /// <param name="previewMaterials">Runtime materials ordered by submesh slot.</param>
        /// <param name="renderManager3D">Renderer used to allocate the offscreen render target.</param>
        public ModelPreviewSource(RuntimeModel runtimeModel, RuntimeMaterial[] previewMaterials, RenderManager3D renderManager3D) {
            if (runtimeModel == null) {
                throw new ArgumentNullException(nameof(runtimeModel));
            }
            if (previewMaterials == null) {
                throw new ArgumentNullException(nameof(previewMaterials));
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
                Model = runtimeModel
            };
            if (previewMaterials.Length == 0) {
                previewMeshComponent.Materials = new[] { CreateNeutralPreviewMaterial() };
            } else {
                previewMeshComponent.SetMaterials(previewMaterials);
            }
            modelEntity.AddComponent(previewMeshComponent);

            previewCameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneModelPreview,
                CameraDrawOrder = 0,
                ClearSettings = new CameraClearSettings(true, ResolvePreviewBackgroundColor(), true, 1f, false, 0)
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
                ModelAssetImportSettings importSettings = assetImportManager.LoadOrCreateModelImportSettings(entry.FullPath);
                if (importSettings == null || importSettings.Importer == null || string.IsNullOrWhiteSpace(importSettings.Importer.ImporterId)) {
                    return false;
                }

                ImportedModelAssetSet importedModel = assetImportManager.ContentManager.Load<ImportedModelAssetSet>(entry.FullPath, importSettings.Importer.ImporterId);
                if (importedModel == null || importedModel.ModelAsset == null) {
                    return false;
                }

                runtimeModel = renderManager3D.BuildModelFromRaw(importedModel.ModelAsset);
                RuntimeMaterial[] previewMaterials = BuildPreviewMaterials(
                    runtimeModel.Submeshes,
                    importedModel.GeneratedMaterials,
                    assetImportManager,
                    renderManager3D,
                    entry.FullPath);
                source = new ModelPreviewSource(runtimeModel, previewMaterials, renderManager3D);
                return true;
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
        /// Handles one middle-button drag delta by panning the preview camera across the model plane.
        /// </summary>
        /// <param name="delta">Mouse delta accumulated since the previous frame.</param>
        public void HandleMouseMiddleDrag(int2 delta) {
            if (isDisposed || (delta.X == 0 && delta.Y == 0)) {
                return;
            }

            panOffset = new float2(
                panOffset.X - (float)(delta.X * PanSensitivity),
                panOffset.Y + (float)(delta.Y * PanSensitivity));
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
            double boundsRadius = ResolveBoundsRadius();
            double fitDistance = ResolveFitDistance();
            double cameraDistance = fitDistance / zoomScale;

            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll((float)orbitYawRadians, (float)orbitPitchRadians, 0f, out cameraOrientation);
            cameraOrientation.Normalize();

            float3 forward = float4.RotateVector(new float3(0f, 0f, -1f), cameraOrientation);
            float3 right = float4.RotateVector(new float3(1f, 0f, 0f), cameraOrientation);
            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), cameraOrientation);
            float3 cameraPosition =
                forward * (float)-cameraDistance +
                right * (float)(panOffset.X * fitDistance) +
                up * (float)(panOffset.Y * fitDistance);
            cameraEntity.LocalOrientation = cameraOrientation;
            cameraEntity.LocalPosition = cameraPosition;
            UpdateCameraClipPlanes(cameraPosition, boundsRadius);
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
            double radius = ResolveBoundsRadius();
            double aspectRatio = contentSize.X / (double)contentSize.Y;
            double halfVerticalFov = Math.PI / 8.0d;
            double halfHorizontalFov = Math.Atan(Math.Tan(halfVerticalFov) * aspectRatio);
            double effectiveHalfFov = Math.Min(halfVerticalFov, halfHorizontalFov);
            double fitDistance = radius / Math.Sin(effectiveHalfFov);
            return Math.Max(0.1d, fitDistance * FramingPadding);
        }

        /// <summary>
        /// Resolves the bounding-sphere radius used for preview framing and clip-plane expansion.
        /// </summary>
        /// <returns>Radius derived from the cached model bounds.</returns>
        double ResolveBoundsRadius() {
            float3 halfExtents = new float3(
                Math.Abs(boundsMax.X - boundsMin.X) * 0.5f,
                Math.Abs(boundsMax.Y - boundsMin.Y) * 0.5f,
                Math.Abs(boundsMax.Z - boundsMin.Z) * 0.5f);
            return Math.Max(0.5d, Math.Sqrt(
                (double)halfExtents.X * halfExtents.X +
                (double)halfExtents.Y * halfExtents.Y +
                (double)halfExtents.Z * halfExtents.Z));
        }

        /// <summary>
        /// Expands the preview camera near and far clip planes around the current orbit target so tall or wide models remain visible.
        /// </summary>
        /// <param name="cameraPosition">Current camera position relative to the centered model.</param>
        /// <param name="boundsRadius">Bounding-sphere radius of the previewed model.</param>
        void UpdateCameraClipPlanes(float3 cameraPosition, double boundsRadius) {
            double distanceToCenter = Math.Sqrt(
                (double)cameraPosition.X * cameraPosition.X +
                (double)cameraPosition.Y * cameraPosition.Y +
                (double)cameraPosition.Z * cameraPosition.Z);
            double paddedRadius = boundsRadius * ClipPlanePadding;
            double nearPlaneDistance = CameraProjectionUtils.MinimumNearPlaneDistance;
            double farPlaneDistance = Math.Max(
                nearPlaneDistance + CameraProjectionUtils.MinimumPlaneSeparation,
                distanceToCenter + paddedRadius);
            previewCameraComponent.NearPlaneDistance = (float)nearPlaneDistance;
            previewCameraComponent.FarPlaneDistance = (float)farPlaneDistance;
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
        /// Builds the runtime material array used by imported model previews.
        /// </summary>
        /// <param name="submeshes">Runtime submeshes exposed by the imported model.</param>
        /// <param name="generatedMaterials">Generated material descriptions returned by the importer.</param>
        /// <param name="assetImportManager">Asset import manager used to resolve imported texture cache files.</param>
        /// <param name="renderManager3D">Renderer used to materialize runtime textures.</param>
        /// <returns>Runtime materials ordered by submesh slot.</returns>
        static RuntimeMaterial[] BuildPreviewMaterials(
            RuntimeSubmesh[] submeshes,
            ImportedModelMaterialAsset[] generatedMaterials,
            AssetImportManager assetImportManager,
            RenderManager3D renderManager3D,
            string modelSourcePath) {
            if (submeshes == null) {
                throw new ArgumentNullException(nameof(submeshes));
            }
            if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (string.IsNullOrWhiteSpace(modelSourcePath)) {
                throw new ArgumentException("Model source path must be provided.", nameof(modelSourcePath));
            }

            if (submeshes.Length == 0) {
                return Array.Empty<RuntimeMaterial>();
            }

            RuntimeMaterial fallbackMaterial = CreateNeutralPreviewMaterial();
            RuntimeMaterial[] previewMaterials = new RuntimeMaterial[submeshes.Length];
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                previewMaterials[submeshIndex] = ResolvePreviewMaterial(
                    submeshes[submeshIndex],
                    generatedMaterials,
                    assetImportManager,
                    renderManager3D,
                    fallbackMaterial,
                    modelSourcePath);
            }

            return previewMaterials;
        }

        /// <summary>
        /// Resolves one preview material for one runtime submesh.
        /// </summary>
        /// <param name="submesh">Runtime submesh whose material slot should be resolved.</param>
        /// <param name="generatedMaterials">Generated material descriptions returned by the importer.</param>
        /// <param name="assetImportManager">Asset import manager used to resolve imported texture cache files.</param>
        /// <param name="renderManager3D">Renderer used to materialize runtime textures.</param>
        /// <param name="fallbackMaterial">Neutral fallback material used when the importer did not generate a matching slot.</param>
        /// <returns>Runtime material assigned to the submesh slot.</returns>
        static RuntimeMaterial ResolvePreviewMaterial(
            RuntimeSubmesh submesh,
            ImportedModelMaterialAsset[] generatedMaterials,
            AssetImportManager assetImportManager,
            RenderManager3D renderManager3D,
            RuntimeMaterial fallbackMaterial,
            string modelSourcePath) {
            if (submesh == null) {
                throw new ArgumentNullException(nameof(submesh));
            }
            if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (fallbackMaterial == null) {
                throw new ArgumentNullException(nameof(fallbackMaterial));
            }
            if (string.IsNullOrWhiteSpace(modelSourcePath)) {
                throw new ArgumentException("Model source path must be provided.", nameof(modelSourcePath));
            }

            if (!string.IsNullOrWhiteSpace(submesh.MaterialSlotName)) {
                for (int materialIndex = 0; materialIndex < generatedMaterials.Length; materialIndex++) {
                    ImportedModelMaterialAsset generatedMaterial = generatedMaterials[materialIndex];
                    if (generatedMaterial == null) {
                        throw new InvalidOperationException("Imported model material collections cannot contain null entries.");
                    }

                    if (!string.Equals(generatedMaterial.MaterialName, submesh.MaterialSlotName, StringComparison.Ordinal)) {
                        continue;
                    }

                    return CreateImportedPreviewMaterial(generatedMaterial, assetImportManager, renderManager3D, modelSourcePath);
                }
            }

            return fallbackMaterial;
        }

        /// <summary>
        /// Creates one runtime material for one generated imported material description.
        /// </summary>
        /// <param name="generatedMaterial">Generated material description supplied by the importer.</param>
        /// <param name="assetImportManager">Asset import manager used to resolve imported texture cache files.</param>
        /// <param name="renderManager3D">Renderer used to materialize runtime textures.</param>
        /// <returns>Runtime material configured with the imported diffuse texture when one was authored.</returns>
        static RuntimeMaterial CreateImportedPreviewMaterial(
            ImportedModelMaterialAsset generatedMaterial,
            AssetImportManager assetImportManager,
            RenderManager3D renderManager3D,
            string modelSourcePath) {
            if (generatedMaterial == null) {
                throw new ArgumentNullException(nameof(generatedMaterial));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (string.IsNullOrWhiteSpace(modelSourcePath)) {
                throw new ArgumentException("Model source path must be provided.", nameof(modelSourcePath));
            }

            RuntimeMaterial previewMaterial = CreateNeutralPreviewMaterial();
            ShaderMaterialAsset materialAsset = generatedMaterial.MaterialAsset;
            if (materialAsset == null) {
                throw new InvalidOperationException("Imported model material entries must include a material asset.");
            }

            if (!string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId)) {
                TextureAsset textureAsset = LoadImportedTextureAsset(assetImportManager, modelSourcePath, materialAsset.DiffuseTextureAssetId);
                RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
                ShaderRuntimeMaterialAccess.Require(previewMaterial).Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, runtimeTexture);
            }

            StandardMaterialTextureBindingDefaults.Apply(ShaderRuntimeMaterialAccess.Require(previewMaterial));
            return previewMaterial;
        }

        /// <summary>
        /// Creates one neutral preview material that stays readable even when the imported source has no diffuse texture.
        /// </summary>
        /// <returns>Runtime material configured with the shared neutral preview swatch.</returns>
        static RuntimeMaterial CreateNeutralPreviewMaterial() {
            RuntimeMaterial previewMaterial = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();
            ShaderRuntimeMaterialAccess.Require(previewMaterial).Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, ResolveNeutralPreviewTexture());
            StandardMaterialTextureBindingDefaults.Apply(ShaderRuntimeMaterialAccess.Require(previewMaterial));
            return previewMaterial;
        }

        /// <summary>
        /// Resolves the shared neutral preview swatch used for untextured imported model previews.
        /// </summary>
        /// <returns>Shared runtime texture used as the untextured preview diffuse map.</returns>
        static RuntimeTexture ResolveNeutralPreviewTexture() {
            if (neutralPreviewTexture != null) {
                return neutralPreviewTexture;
            }

            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("A render manager must be initialized before preview materials can create neutral diffuse textures.");
            }

            neutralPreviewTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 192, 198, 208, 255 }
            });
            return neutralPreviewTexture;
        }

        /// <summary>
        /// Resolves the preview background clear color from the active editor theme.
        /// </summary>
        /// <returns>Normalized RGBA clear color based on the current theme background.</returns>
        static float4 ResolvePreviewBackgroundColor() {
            byte4 color = ThemeManager.Colors.BackgroundPrimary;
            return new float4(
                color.X / 255f,
                color.Y / 255f,
                color.Z / 255f,
                color.W / 255f);
        }

        /// <summary>
        /// Loads one imported texture asset from the model source tree, the project assets tree, or the import cache.
        /// </summary>
        /// <param name="assetImportManager">Asset import manager used to resolve source and cache roots.</param>
        /// <param name="modelSourcePath">Absolute path to the imported model source file.</param>
        /// <param name="assetId">Imported texture asset identifier.</param>
        /// <returns>Serialized texture asset loaded from the importer inputs or cache.</returns>
        static TextureAsset LoadImportedTextureAsset(AssetImportManager assetImportManager, string modelSourcePath, string assetId) {
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (string.IsNullOrWhiteSpace(modelSourcePath)) {
                throw new ArgumentException("Model source path must be provided.", nameof(modelSourcePath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string modelSourceDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(modelSourcePath));
            if (!string.IsNullOrWhiteSpace(modelSourceDirectoryPath)) {
                string sourceTexturePath = Path.IsPathRooted(assetId) ? Path.GetFullPath(assetId) : Path.GetFullPath(Path.Combine(modelSourceDirectoryPath, assetId));
                if (File.Exists(sourceTexturePath)) {
                    return LoadTextureAssetFromSource(assetImportManager, sourceTexturePath);
                }
            }

            string assetsRootPath = assetImportManager.AssetsRootPath;
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new InvalidOperationException("Asset source root path has not been initialized.");
            }

            string projectTexturePath = Path.IsPathRooted(assetId) ? Path.GetFullPath(assetId) : Path.GetFullPath(Path.Combine(assetsRootPath, assetId));
            if (File.Exists(projectTexturePath)) {
                return LoadTextureAssetFromSource(assetImportManager, projectTexturePath);
            }

            string importRootPath = assetImportManager.ImportRootPath;
            if (string.IsNullOrWhiteSpace(importRootPath)) {
                throw new InvalidOperationException("Asset import root path has not been initialized.");
            }

            string texturePath = Path.GetFullPath(Path.Combine(importRootPath, assetId));
            string normalizedImportRootPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(importRootPath));
            if (!texturePath.StartsWith(normalizedImportRootPath, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Imported texture asset references must stay inside the project cache folder.");
            }

            return assetImportManager.ContentManager.Load<TextureAsset>(texturePath, EditorContentProcessorIds.TextureAsset);
        }

        /// <summary>
        /// Loads one texture asset directly from its source file so preview materials reflect the current importer output instead of stale cached texture artifacts.
        /// </summary>
        /// <param name="assetImportManager">Asset import manager that owns the texture importer registrations.</param>
        /// <param name="sourceTexturePath">Absolute path to the texture source file.</param>
        /// <returns>Texture asset freshly imported from the source file.</returns>
        static TextureAsset LoadTextureAssetFromSource(AssetImportManager assetImportManager, string sourceTexturePath) {
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (string.IsNullOrWhiteSpace(sourceTexturePath)) {
                throw new ArgumentException("Texture source path must be provided.", nameof(sourceTexturePath));
            }

            TextureAssetImportSettings settings = assetImportManager.LoadOrCreateTextureImportSettings(sourceTexturePath);
            if (settings == null || settings.Importer == null || string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                throw new InvalidOperationException("Texture import settings must resolve a valid importer id.");
            }

            TextureAsset textureAsset = assetImportManager.ContentManager.Load<TextureAsset>(sourceTexturePath, settings.Importer.ImporterId);
            if (textureAsset == null) {
                throw new InvalidOperationException($"Texture importer '{settings.Importer.ImporterId}' did not return an asset for '{sourceTexturePath}'.");
            }

            textureAsset.Id = settings.Importer.AssetId;
            return textureAsset;
        }

        /// <summary>
        /// Ensures one directory path ends with a trailing separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path to normalize.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        static string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                return path;
            }

            return string.Concat(path, Path.DirectorySeparatorChar);
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
                LayerMask = EditorLayerMasks.SceneModelPreview
            };
            return entity;
        }
    }
}
