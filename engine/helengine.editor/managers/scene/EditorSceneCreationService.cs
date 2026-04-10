namespace helengine.editor {
    /// <summary>
    /// Creates user-authored scene entities for the editor Add menu.
    /// </summary>
    public class EditorSceneCreationService {
        /// <summary>
        /// Stable save-state slot name used by MeshComponent persistence for model references.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Creates a root empty entity for the scene.
        /// </summary>
        /// <returns>Configured empty scene entity.</returns>
        public EditorEntity CreateEmpty() {
            return CreateBaseEntity("Empty");
        }

        /// <summary>
        /// Creates a root cube primitive for the scene.
        /// </summary>
        /// <returns>Configured cube scene entity.</returns>
        public EditorEntity CreateCube() {
            return CreatePrimitive("Cube", EngineGeneratedModelCache.CubeAssetId, EngineGeneratedAssetProvider.CubeRelativePath);
        }

        /// <summary>
        /// Creates a root plane primitive for the scene.
        /// </summary>
        /// <returns>Configured plane scene entity.</returns>
        public EditorEntity CreatePlane() {
            return CreatePrimitive("Plane", EngineGeneratedModelCache.PlaneAssetId, EngineGeneratedAssetProvider.PlaneRelativePath);
        }

        /// <summary>
        /// Creates one primitive entity backed by a generated runtime model.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <param name="assetId">Stable generated model identifier.</param>
        /// <param name="relativePath">Virtual generated-asset path used for persistence.</param>
        /// <returns>Configured primitive scene entity.</returns>
        EditorEntity CreatePrimitive(string name, string assetId, string relativePath) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Primitive name must be provided.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Generated asset path must be provided.", nameof(relativePath));
            }

            RuntimeModel runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(assetId);
            EditorEntity entity = CreateBaseEntity(name);

            try {
                EntitySaveComponent saveComponent = FindSaveComponent(entity);
                MeshComponent meshComponent = new MeshComponent {
                    Model = runtimeModel
                };
                entity.AddComponent(meshComponent);
                saveComponent.SetAssetReference(meshComponent, MeshModelReferenceName, BuildGeneratedModelReference(relativePath, assetId));
                return entity;
            } catch {
                entity.Enabled = false;
                Core.Instance.ObjectManager.RemoveEntity(entity);
                throw;
            }
        }

        /// <summary>
        /// Creates a root scene entity with the standard defaults used by add commands.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <returns>Configured root scene entity.</returns>
        EditorEntity CreateBaseEntity(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Entity name must be provided.", nameof(name));
            }

            return new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
        }

        /// <summary>
        /// Builds the stable generated-model reference stored for created primitives.
        /// </summary>
        /// <param name="relativePath">Virtual generated-asset path used for persistence.</param>
        /// <param name="assetId">Stable generated model identifier.</param>
        /// <returns>Stable scene asset reference for the generated model.</returns>
        SceneAssetReference BuildGeneratedModelReference(string relativePath, string assetId) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Resolves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached save component.</returns>
        EntitySaveComponent FindSaveComponent(EditorEntity entity) {
            if (entity == null || entity.Components == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            throw new InvalidOperationException("Editor entities created by the add flow must include EntitySaveComponent.");
        }
    }
}
