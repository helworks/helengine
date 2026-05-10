namespace helengine.editor {
    /// <summary>
    /// Resolves the best preview source for the current editor selection snapshot.
    /// </summary>
    public class PreviewSourceResolver {
        /// <summary>
        /// Asset manager used to load texture previews.
        /// </summary>
        readonly AssetImportManager assetImportManager;
        /// <summary>
        /// 2D renderer used to build runtime textures for texture previews.
        /// </summary>
        readonly RenderManager2D renderManager2D;
        /// <summary>
        /// 3D renderer used for camera and model preview sources.
        /// </summary>
        readonly RenderManager3D renderManager3D;
        /// <summary>
        /// Scene-owned canvas profile used to size camera previews against the authored logical resolution.
        /// </summary>
        readonly EditorSceneCanvasProfileState sceneCanvasProfileState;

        /// <summary>
        /// Initializes a new preview source resolver.
        /// </summary>
        /// <param name="assetImportManager">Asset import manager used for texture preview loading.</param>
        /// <param name="renderManager2D">2D renderer used for texture preview creation.</param>
        /// <param name="renderManager3D">3D renderer used for camera and model preview creation.</param>
        /// <param name="sceneCanvasProfileState">Scene-owned canvas profile used to size camera previews.</param>
        public PreviewSourceResolver(AssetImportManager assetImportManager, RenderManager2D renderManager2D, RenderManager3D renderManager3D, EditorSceneCanvasProfileState sceneCanvasProfileState) {
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager2D == null) {
                throw new ArgumentNullException(nameof(renderManager2D));
            }
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }

            this.assetImportManager = assetImportManager;
            this.renderManager2D = renderManager2D;
            this.renderManager3D = renderManager3D;
            this.sceneCanvasProfileState = sceneCanvasProfileState;
        }

        /// <summary>
        /// Initializes a new preview source resolver without a shared scene canvas profile.
        /// </summary>
        /// <param name="assetImportManager">Asset import manager used for texture preview loading.</param>
        /// <param name="renderManager2D">2D renderer used for texture preview creation.</param>
        /// <param name="renderManager3D">3D renderer used for camera and model preview creation.</param>
        public PreviewSourceResolver(AssetImportManager assetImportManager, RenderManager2D renderManager2D, RenderManager3D renderManager3D) {
            this.assetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
            this.renderManager2D = renderManager2D ?? throw new ArgumentNullException(nameof(renderManager2D));
            this.renderManager3D = renderManager3D ?? throw new ArgumentNullException(nameof(renderManager3D));
            sceneCanvasProfileState = null;
        }

        /// <summary>
        /// Resolves one preview source for the provided selection snapshot.
        /// </summary>
        /// <param name="assetEntry">Currently selected asset browser entry.</param>
        /// <param name="selectedEntity">Currently selected scene entity.</param>
        /// <param name="source">Resolved preview source when one is available.</param>
        /// <returns>True when a preview source was resolved; otherwise false.</returns>
        public bool TryResolve(AssetBrowserEntry assetEntry, Entity selectedEntity, out IPreviewSource source) {
            if (selectedEntity != null) {
                CameraComponent cameraComponent = FindComponent<CameraComponent>(selectedEntity);
                if (cameraComponent != null) {
                    try {
                        source = new CameraPreviewSource(selectedEntity, cameraComponent, renderManager3D, sceneCanvasProfileState);
                        return true;
                    } catch (Exception ex) {
                        Logger.WriteError($"Camera preview failed for '{GetSelectionLabel(selectedEntity)}': {ex.Message}");
                    }
                }
            }

            if (assetEntry != null && !assetEntry.IsDirectory && assetEntry.EntryKind == AssetEntryKind.Model) {
                try {
                    ModelPreviewSource modelSource;
                    if (ModelPreviewSource.TryCreate(assetEntry, assetImportManager, renderManager3D, out modelSource)) {
                        source = modelSource;
                        return true;
                    }
                } catch (Exception ex) {
                    Logger.WriteError($"Model preview failed for '{assetEntry.RelativePath}': {ex.Message}");
                }
            }

            if (assetEntry != null && !assetEntry.IsDirectory && assetImportManager.IsTextureExtension(assetEntry.Extension)) {
                TexturePreviewSource textureSource;
                if (TexturePreviewSource.TryCreate(assetEntry, assetImportManager, renderManager2D, out textureSource)) {
                    source = textureSource;
                    return true;
                }
            }

            source = null;
            return false;
        }

        /// <summary>
        /// Resolves one preview source for the provided asset selection.
        /// </summary>
        /// <param name="assetEntry">Currently selected asset browser entry.</param>
        /// <param name="source">Resolved preview source when one is available.</param>
        /// <returns>True when a preview source was resolved; otherwise false.</returns>
        public bool TryResolveAssetPreview(AssetBrowserEntry assetEntry, out IPreviewSource source) {
            return TryResolve(assetEntry, null, out source);
        }

        /// <summary>
        /// Resolves one preview source for the provided camera selection.
        /// </summary>
        /// <param name="selectedEntity">Currently selected scene entity.</param>
        /// <param name="source">Resolved preview source when one is available.</param>
        /// <returns>True when a preview source was resolved; otherwise false.</returns>
        public bool TryResolveCameraPreview(Entity selectedEntity, out IPreviewSource source) {
            return TryResolve(null, selectedEntity, out source);
        }

        /// <summary>
        /// Finds the first component of the requested type on one entity.
        /// </summary>
        /// <typeparam name="T">Component type to locate.</typeparam>
        /// <param name="entity">Entity whose components should be searched.</param>
        /// <returns>Matching component instance when present; otherwise null.</returns>
        static T FindComponent<T>(Entity entity) where T : Component {
            if (entity == null || entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is T component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a human-readable label for preview logging.
        /// </summary>
        /// <param name="entity">Selected entity being previewed.</param>
        /// <returns>Readable selection label.</returns>
        static string GetSelectionLabel(Entity entity) {
            if (entity is EditorEntity editorEntity && !string.IsNullOrWhiteSpace(editorEntity.Name)) {
                return editorEntity.Name;
            }

            return entity?.GetType().Name ?? "selection";
        }
    }
}
