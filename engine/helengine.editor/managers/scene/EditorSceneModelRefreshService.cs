namespace helengine.editor {
    /// <summary>
    /// Refreshes live scene mesh components that reference one file-system model source after its processed cache changes.
    /// </summary>
    public class EditorSceneModelRefreshService {
        /// <summary>
        /// Stable save-state reference name used for mesh model bindings.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Resolver that rebuilds runtime models from processed file-system model assets.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;

        /// <summary>
        /// Initializes a new refresh service that can rebuild runtime models from source files.
        /// </summary>
        /// <param name="fileSystemModelResolver">Resolver used to load processed file-system models.</param>
        public EditorSceneModelRefreshService(EditorFileSystemModelResolver fileSystemModelResolver) {
            if (fileSystemModelResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemModelResolver));
            }

            FileSystemModelResolver = fileSystemModelResolver;
        }

        /// <summary>
        /// Rebuilds all live mesh components whose saved model reference matches the supplied file-system asset.
        /// </summary>
        /// <param name="sourcePath">Absolute source path used to rebuild the processed runtime model.</param>
        /// <param name="relativePath">Project-relative source path used to find matching scene references.</param>
        public void RefreshFileSystemModel(string sourcePath, string relativePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            RuntimeModel refreshedModel = null;
            IReadOnlyList<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
                Entity entity = entities[entityIndex];
                if (entity.Components == null || entity.Components.Count == 0) {
                    continue;
                }

                EntitySaveComponent saveComponent = FindSaveComponent(entity);
                if (saveComponent == null) {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is not MeshComponent meshComponent) {
                        continue;
                    }

                    if (!ReferencesFileSystemModel(saveComponent, meshComponent, relativePath)) {
                        continue;
                    }

                    if (refreshedModel == null) {
                        refreshedModel = FileSystemModelResolver.ResolveRuntimeModel(sourcePath);
                    }

                    meshComponent.Model = refreshedModel;
                }
            }
        }

        /// <summary>
        /// Finds the hidden entity save component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be located.</param>
        /// <returns>Attached save component, or null when the entity does not have one.</returns>
        EntitySaveComponent FindSaveComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether one mesh component stores a file-system model reference that matches the supplied relative path.
        /// </summary>
        /// <param name="saveComponent">Entity save component that stores mesh persistence metadata.</param>
        /// <param name="meshComponent">Mesh component whose saved model reference should be checked.</param>
        /// <param name="relativePath">Project-relative model path to match.</param>
        /// <returns>True when the mesh component references the supplied file-system model path.</returns>
        bool ReferencesFileSystemModel(EntitySaveComponent saveComponent, MeshComponent meshComponent, string relativePath) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            if (!saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState)) {
                return false;
            }
            if (!saveState.TryGetAssetReference(MeshModelReferenceName, out SceneAssetReference reference)) {
                return false;
            }

            return reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem &&
                string.Equals(reference.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
