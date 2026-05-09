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
                previewMeshComponent.Material = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();
            } else {
                previewMeshComponent.SetMaterials(previewMaterials);
            }
            modelEntity.AddComponent(previewMeshComponent);

            previewCameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneModelPreview,
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
                AssetImportSettings importSettings = assetImportManager.LoadOrCreateImportSettings(entry.FullPath);
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
                    renderManager3D);
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
            double fitDistance = ResolveFitDistance();
            double cameraDistance = fitDistance / zoomScale;

            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll((float)orbitYawRadians, (float)orbitPitchRadians, 0f, out cameraOrientation);
            cameraOrientation.Normalize();

            float3 forward = float4.RotateVector(new float3(0f, 0f, -1f), cameraOrientation);
            float3 right = float4.RotateVector(new float3(1f, 0f, 0f), cameraOrientation);
            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), cameraOrientation);
            cameraEntity.LocalOrientation = cameraOrientation;
            cameraEntity.LocalPosition =
                forward * (float)-cameraDistance +
                right * (float)(panOffset.X * fitDistance) +
                up * (float)(panOffset.Y * fitDistance);
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
            RenderManager3D renderManager3D) {
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

            if (submeshes.Length == 0) {
                return Array.Empty<RuntimeMaterial>();
            }

            RuntimeMaterial fallbackMaterial = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();
            RuntimeMaterial[] previewMaterials = new RuntimeMaterial[submeshes.Length];
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                previewMaterials[submeshIndex] = ResolvePreviewMaterial(
                    submeshes[submeshIndex],
                    generatedMaterials,
                    assetImportManager,
                    renderManager3D,
                    fallbackMaterial);
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
            RuntimeMaterial fallbackMaterial) {
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

            if (!string.IsNullOrWhiteSpace(submesh.MaterialSlotName)) {
                for (int materialIndex = 0; materialIndex < generatedMaterials.Length; materialIndex++) {
                    ImportedModelMaterialAsset generatedMaterial = generatedMaterials[materialIndex];
                    if (generatedMaterial == null) {
                        throw new InvalidOperationException("Imported model material collections cannot contain null entries.");
                    }

                    if (!string.Equals(generatedMaterial.MaterialName, submesh.MaterialSlotName, StringComparison.Ordinal)) {
                        continue;
                    }

                    return CreateImportedPreviewMaterial(generatedMaterial, assetImportManager, renderManager3D);
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
            RenderManager3D renderManager3D) {
            if (generatedMaterial == null) {
                throw new ArgumentNullException(nameof(generatedMaterial));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }

            RuntimeMaterial previewMaterial = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();
            MaterialAsset materialAsset = generatedMaterial.MaterialAsset;
            if (materialAsset == null) {
                throw new InvalidOperationException("Imported model material entries must include a material asset.");
            }

            if (!string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId)) {
                TextureAsset textureAsset = LoadImportedTextureAsset(assetImportManager, materialAsset.DiffuseTextureAssetId);
                RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
                previewMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, runtimeTexture);
            }

            StandardMaterialTextureBindingDefaults.Apply(previewMaterial);
            return previewMaterial;
        }

        /// <summary>
        /// Loads one imported texture asset from the cache folder used by the editor importer pipeline.
        /// </summary>
        /// <param name="assetImportManager">Asset import manager used to resolve the import cache root.</param>
        /// <param name="assetId">Imported texture asset identifier.</param>
        /// <returns>Serialized texture asset loaded from the import cache.</returns>
        static TextureAsset LoadImportedTextureAsset(AssetImportManager assetImportManager, string assetId) {
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string assetsRootPath = assetImportManager.AssetsRootPath;
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new InvalidOperationException("Asset source root path has not been initialized.");
            }

            string sourceTexturePath = Path.GetFullPath(Path.Combine(assetsRootPath, assetId));
            string normalizedAssetsRootPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(assetsRootPath));
            if (sourceTexturePath.StartsWith(normalizedAssetsRootPath, StringComparison.OrdinalIgnoreCase) && File.Exists(sourceTexturePath)) {
                TextureAsset textureAsset;
                if (assetImportManager.TryLoadTextureAsset(sourceTexturePath, out textureAsset)) {
                    return textureAsset;
                }
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
