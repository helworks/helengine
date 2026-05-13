namespace helengine.editor {
    /// <summary>
    /// Creates user-authored scene entities for the editor Add menu.
    /// </summary>
    public class EditorSceneCreationService {
        /// <summary>
        /// Factory used to create authored scene entities for the active editor host.
        /// </summary>
        readonly IEntityFactory EntityFactory;

        /// <summary>
        /// Stable save-state slot name used by mesh persistence for model references.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Stable save-state slot name used by mesh persistence for material references.
        /// </summary>
        const string MeshMaterialReferenceName = "Material";

        /// <summary>
        /// Initializes one editor scene creation service.
        /// </summary>
        public EditorSceneCreationService()
            : this(new EditorEntityFactory()) {
        }

        /// <summary>
        /// Initializes one editor scene creation service.
        /// </summary>
        /// <param name="entityFactory">Factory used to create authored scene entities for the active editor host.</param>
        public EditorSceneCreationService(IEntityFactory entityFactory) {
            EntityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        }

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
                EngineGeneratedMaterialCache.StandardAssetId);
        }

        /// <summary>
        /// Creates a root plane primitive for the scene.
        /// </summary>
        /// <returns>Configured plane scene entity.</returns>
        public EditorEntity CreatePlane() {
            return CreatePrimitive(
                "Plane",
                EngineGeneratedModelCache.PlaneAssetId,
                EngineGeneratedMaterialCache.StandardAssetId);
        }

        /// <summary>
        /// Creates one root model entity for the scene and stores the supplied model and material references for persistence.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <param name="model">Runtime model assigned to the mesh component.</param>
        /// <param name="materials">Runtime materials assigned to the mesh component submesh slots.</param>
        /// <param name="modelReference">Stable scene reference for the mesh model.</param>
        /// <param name="materialReferences">Stable scene references for each material slot.</param>
        /// <returns>Configured model scene entity.</returns>
        public EditorEntity CreateModel(
            string name,
            RuntimeModel model,
            RuntimeMaterial[] materials,
            SceneAssetReference modelReference,
            SceneAssetReference[] materialReferences) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Model name must be provided.", nameof(name));
            }
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }
            if (materials == null) {
                throw new ArgumentNullException(nameof(materials));
            }
            if (modelReference == null) {
                throw new ArgumentNullException(nameof(modelReference));
            }
            if (materialReferences == null) {
                throw new ArgumentNullException(nameof(materialReferences));
            }
            if (materialReferences.Length != materials.Length) {
                throw new InvalidOperationException("Model material references must match the runtime material slot count.");
            }

            EditorEntity entity = CreateBaseEntity(name);

            try {
                EntitySaveComponent saveComponent = FindSaveComponent(entity);
                MeshComponent meshComponent = new MeshComponent {
                    Model = model
                };
                meshComponent.SetMaterials(materials);
                entity.AddComponent(meshComponent);
                saveComponent.SetAssetReference(meshComponent, MeshModelReferenceName, modelReference);
                for (int materialIndex = 0; materialIndex < materialReferences.Length; materialIndex++) {
                    SceneAssetReference materialReference = materialReferences[materialIndex];
                    if (materialReference == null) {
                        continue;
                    }

                    saveComponent.SetAssetReference(meshComponent, BuildMaterialReferenceName(materialIndex), materialReference);
                }

                return entity;
            } catch {
                entity.Enabled = false;
                Core.Instance.ObjectManager.RemoveEntity(entity);
                throw;
            }
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
        /// Creates a root ambient light entity for the scene.
        /// </summary>
        /// <returns>Configured ambient-light scene entity.</returns>
        public EditorEntity CreateAmbientLight() {
            EditorEntity entity = CreateBaseEntity("Ambient Light");
            entity.AddComponent(new AmbientLightComponent());
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
        /// <param name="materialAssetId">Stable generated material identifier.</param>
        /// <returns>Configured primitive scene entity.</returns>
        EditorEntity CreatePrimitive(
            string name,
            string modelAssetId,
            string materialAssetId) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Primitive name must be provided.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(modelAssetId)) {
                throw new ArgumentException("Generated model asset id must be provided.", nameof(modelAssetId));
            }
            if (string.IsNullOrWhiteSpace(materialAssetId)) {
                throw new ArgumentException("Generated material asset id must be provided.", nameof(materialAssetId));
            }

            RuntimeModel runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(modelAssetId);
            RuntimeMaterial runtimeMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(materialAssetId);
            EditorEntity entity = CreateBaseEntity(name);

            try {
                MeshComponent meshComponent = new MeshComponent {
                    Model = runtimeModel,
                    Material = runtimeMaterial
                };
                entity.AddComponent(meshComponent);
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

            return ResolveEditorEntity(EntityFactory.Create(name));
        }

        /// <summary>
        /// Resolves the editor entity returned by the host-owned authored entity factory.
        /// </summary>
        /// <param name="entity">Entity returned by the factory.</param>
        /// <returns>Resolved editor entity.</returns>
        EditorEntity ResolveEditorEntity(Entity entity) {
            if (entity is EditorEntity editorEntity) {
                return editorEntity;
            }

            throw new InvalidOperationException("Editor-authored scene creation requires the entity factory to return EditorEntity instances.");
        }

        /// <summary>
        /// Resolves one stable save-state material-reference name for the supplied slot index.
        /// </summary>
        /// <param name="slotIndex">Zero-based material slot index.</param>
        /// <returns>Stable save-state reference name.</returns>
        static string BuildMaterialReferenceName(int slotIndex) {
            if (slotIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Material slot index must be non-negative.");
            }

            return slotIndex == 0
                ? MeshMaterialReferenceName
                : string.Concat(MeshMaterialReferenceName, "[", slotIndex.ToString(), "]");
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


