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
        /// Stable save-state slot name used by MeshComponent persistence for material references.
        /// </summary>
        const string MeshMaterialReferenceName = "Material";

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
            return CreatePrimitive(
                "Cube",
                EngineGeneratedModelCache.CubeAssetId,
                EngineGeneratedAssetProvider.CubeRelativePath,
                EngineGeneratedMaterialCache.StandardAssetId,
                EngineGeneratedAssetProvider.StandardMaterialRelativePath);
        }

        /// <summary>
        /// Creates a root plane primitive for the scene.
        /// </summary>
        /// <returns>Configured plane scene entity.</returns>
        public EditorEntity CreatePlane() {
            return CreatePrimitive(
                "Plane",
                EngineGeneratedModelCache.PlaneAssetId,
                EngineGeneratedAssetProvider.PlaneRelativePath,
                EngineGeneratedMaterialCache.StandardAssetId,
                EngineGeneratedAssetProvider.StandardMaterialRelativePath);
        }

        /// <summary>
        /// Creates a root camera entity for the scene and attaches the editor-only camera visual.
        /// </summary>
        /// <returns>Configured camera scene entity.</returns>
        public EditorEntity CreateCamera() {
            EditorEntity entity = CreateBaseEntity("Camera");
            CameraComponent cameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.SceneObjects,
                CameraDrawOrder = 0,
                Viewport = new float4(0f, 0f, 1f, 1f),
                ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0)
            };
            entity.AddComponent(cameraComponent);
            EditorSceneCameraSuppressionService.AttachAndSuppress(entity);
            EditorCameraVisualAttachmentService.Attach(entity);
            return entity;
        }

        /// <summary>
        /// Creates a root point light entity for the scene and attaches the editor-only point-light visual.
        /// </summary>
        /// <returns>Configured point-light scene entity.</returns>
        public EditorEntity CreatePointLight() {
            EditorEntity entity = CreateBaseEntity("Point Light");
            entity.AddComponent(new PointLightComponent());
            EditorPointLightVisualAttachmentService.Attach(entity);
            return entity;
        }

        /// <summary>
        /// Creates a root directional light entity for the scene and attaches the editor-only directional-light arrow.
        /// </summary>
        /// <returns>Configured directional-light scene entity.</returns>
        public EditorEntity CreateDirectionalLight() {
            EditorEntity entity = CreateBaseEntity("Directional Light");
            entity.AddComponent(new DirectionalLightComponent());
            EditorDirectionalLightVisualAttachmentService.Attach(entity);
            return entity;
        }

        /// <summary>
        /// Creates a root spot light entity for the scene and attaches the editor-only spot-light cone.
        /// </summary>
        /// <returns>Configured spot-light scene entity.</returns>
        public EditorEntity CreateSpotLight() {
            EditorEntity entity = CreateBaseEntity("Spot Light");
            entity.AddComponent(new SpotLightComponent());
            EditorSpotLightVisualAttachmentService.Attach(entity);
            return entity;
        }

        /// <summary>
        /// Creates one primitive entity backed by a generated runtime model.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <param name="modelAssetId">Stable generated model identifier.</param>
        /// <param name="modelRelativePath">Virtual generated model path used for persistence.</param>
        /// <param name="materialAssetId">Stable generated material identifier.</param>
        /// <param name="materialRelativePath">Virtual generated material path used for persistence.</param>
        /// <returns>Configured primitive scene entity.</returns>
        EditorEntity CreatePrimitive(
            string name,
            string modelAssetId,
            string modelRelativePath,
            string materialAssetId,
            string materialRelativePath) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Primitive name must be provided.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(modelAssetId)) {
                throw new ArgumentException("Generated model asset id must be provided.", nameof(modelAssetId));
            }
            if (string.IsNullOrWhiteSpace(modelRelativePath)) {
                throw new ArgumentException("Generated model asset path must be provided.", nameof(modelRelativePath));
            }
            if (string.IsNullOrWhiteSpace(materialAssetId)) {
                throw new ArgumentException("Generated material asset id must be provided.", nameof(materialAssetId));
            }
            if (string.IsNullOrWhiteSpace(materialRelativePath)) {
                throw new ArgumentException("Generated material asset path must be provided.", nameof(materialRelativePath));
            }

            RuntimeModel runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(modelAssetId);
            RuntimeMaterial runtimeMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(materialAssetId);
            EditorEntity entity = CreateBaseEntity(name);

            try {
                EntitySaveComponent saveComponent = FindSaveComponent(entity);
                MeshComponent meshComponent = new MeshComponent {
                    Model = runtimeModel,
                    Material = runtimeMaterial
                };
                entity.AddComponent(meshComponent);
                saveComponent.SetAssetReference(meshComponent, MeshModelReferenceName, BuildGeneratedReference(modelRelativePath, modelAssetId));
                saveComponent.SetAssetReference(meshComponent, MeshMaterialReferenceName, BuildGeneratedReference(materialRelativePath, materialAssetId));
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
        /// Builds the stable generated-asset reference stored for created primitives.
        /// </summary>
        /// <param name="relativePath">Virtual generated-asset path used for persistence.</param>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Stable scene asset reference for the generated asset.</returns>
        SceneAssetReference BuildGeneratedReference(string relativePath, string assetId) {
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
