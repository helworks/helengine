namespace helengine.editor {
    /// <summary>
    /// Builds the scene entities produced by asset-browser "add to scene" commands. Turning one
    /// browser entry into a live entity means resolving the runtime model, resolving or generating
    /// its material slots, and producing stable scene asset references for each of them — none of
    /// which is presentation work. The editor session still decides when to add an entity, where to
    /// place it, and what to select afterwards; this service decides what the entity is made of.
    /// </summary>
    public sealed class EditorSceneEntityCreationService {
        /// <summary>
        /// Converts asset-browser entries into stable scene asset references.
        /// </summary>
        SceneAssetReferenceFactory SceneAssetReferenceFactory { get; }

        /// <summary>
        /// Rebuilds file-backed scene asset references into runtime assets.
        /// </summary>
        EditorSceneAssetReferenceResolver SceneAssetReferenceResolver { get; }

        /// <summary>
        /// Produces canonical authored references for files inside the project.
        /// </summary>
        EditorAssetReferenceResolver AuthoredAssetReferenceResolver { get; }

        /// <summary>
        /// Resolves runtime models for engine-generated asset entries.
        /// </summary>
        GeneratedAssetProviderRegistry GeneratedAssetProviderRegistry { get; }

        /// <summary>
        /// Session-owned cache of engine-generated runtime materials.
        /// </summary>
        EngineGeneratedMaterialCache GeneratedMaterialCache { get; }

        /// <summary>
        /// Supplies the import settings that describe how a model asset is loaded.
        /// </summary>
        AssetImportManager AssetImportManager { get; }

        /// <summary>
        /// Loads imported asset sets from the project content tree.
        /// </summary>
        ContentManager EditorContentManager { get; }

        /// <summary>
        /// Renderer that builds runtime models from imported model assets.
        /// </summary>
        RenderManager3D Render3D { get; }

        /// <summary>
        /// Creates the editor entities and components behind each scene primitive.
        /// </summary>
        EditorSceneCreationService SceneCreationService { get; }

        /// <summary>
        /// Expands blueprint instance roots into their inherited child hierarchy.
        /// </summary>
        SceneFileLoadService SceneFileLoadService { get; }

        /// <summary>
        /// Initializes one entity-creation service from the collaborators owned by the editor session.
        /// </summary>
        /// <param name="sceneAssetReferenceFactory">Factory that converts browser entries into scene references.</param>
        /// <param name="sceneAssetReferenceResolver">Resolver that rebuilds scene references into runtime assets.</param>
        /// <param name="authoredAssetReferenceResolver">Resolver that produces canonical authored file references.</param>
        /// <param name="generatedAssetProviderRegistry">Registry that resolves engine-generated runtime models.</param>
        /// <param name="generatedMaterialCache">Session-owned engine-generated material cache.</param>
        /// <param name="assetImportManager">Import manager that owns model import settings.</param>
        /// <param name="editorContentManager">Content manager used to load imported model asset sets.</param>
        /// <param name="render3D">Renderer that builds runtime models from imported assets.</param>
        /// <param name="sceneCreationService">Service that creates the editor entities themselves.</param>
        /// <param name="sceneFileLoadService">Service that expands blueprint instance roots.</param>
        public EditorSceneEntityCreationService(
            SceneAssetReferenceFactory sceneAssetReferenceFactory,
            EditorSceneAssetReferenceResolver sceneAssetReferenceResolver,
            EditorAssetReferenceResolver authoredAssetReferenceResolver,
            GeneratedAssetProviderRegistry generatedAssetProviderRegistry,
            EngineGeneratedMaterialCache generatedMaterialCache,
            AssetImportManager assetImportManager,
            ContentManager editorContentManager,
            RenderManager3D render3D,
            EditorSceneCreationService sceneCreationService,
            SceneFileLoadService sceneFileLoadService) {
            if (sceneAssetReferenceFactory == null) {
                throw new ArgumentNullException(nameof(sceneAssetReferenceFactory));
            }
            if (sceneAssetReferenceResolver == null) {
                throw new ArgumentNullException(nameof(sceneAssetReferenceResolver));
            }
            if (authoredAssetReferenceResolver == null) {
                throw new ArgumentNullException(nameof(authoredAssetReferenceResolver));
            }
            if (generatedAssetProviderRegistry == null) {
                throw new ArgumentNullException(nameof(generatedAssetProviderRegistry));
            }
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (editorContentManager == null) {
                throw new ArgumentNullException(nameof(editorContentManager));
            }
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (sceneCreationService == null) {
                throw new ArgumentNullException(nameof(sceneCreationService));
            }
            if (sceneFileLoadService == null) {
                throw new ArgumentNullException(nameof(sceneFileLoadService));
            }

            SceneAssetReferenceFactory = sceneAssetReferenceFactory;
            SceneAssetReferenceResolver = sceneAssetReferenceResolver;
            AuthoredAssetReferenceResolver = authoredAssetReferenceResolver;
            GeneratedAssetProviderRegistry = generatedAssetProviderRegistry;
            GeneratedMaterialCache = generatedMaterialCache;
            AssetImportManager = assetImportManager;
            EditorContentManager = editorContentManager;
            Render3D = render3D;
            SceneCreationService = sceneCreationService;
            SceneFileLoadService = sceneFileLoadService;
        }

        /// <summary>
        /// Builds the default generated material reference used when an imported model does not provide any authored materials.
        /// </summary>
        /// <returns>Stable scene reference for the generated standard material.</returns>
        public static SceneAssetReference BuildGeneratedStandardMaterialReference() {
            return global::helengine.EngineSceneAssetReferenceFactory.CreateStandardMaterial();
        }

        /// <summary>
        /// Resolves one display name for a spawned entity from its asset-browser entry.
        /// </summary>
        /// <param name="entry">Asset browser entry being added.</param>
        /// <returns>Entity name shown in the scene hierarchy.</returns>
        public static string BuildModelEntityName(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            string candidateName = Path.GetFileNameWithoutExtension(entry.Name);
            if (string.IsNullOrWhiteSpace(candidateName)) {
                candidateName = entry.Name;
            }

            return candidateName;
        }

        /// <summary>
        /// Creates one scene entity for a model asset entry using the resolved runtime model and imported materials.
        /// </summary>
        /// <param name="entry">Model entry selected in the asset browser.</param>
        /// <param name="placementPosition">World-space position the new entity is spawned at.</param>
        /// <returns>Configured model scene entity.</returns>
        public EditorEntity CreateModelSceneEntity(AssetBrowserEntry entry, float3 placementPosition) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (entry.IsDirectory || entry.EntryKind != AssetEntryKind.Model) {
                throw new InvalidOperationException("Only model assets can be added to the scene.");
            }

            SceneAssetReference modelReference = SceneAssetReferenceFactory.CreateFromEntry(entry);
            if (entry.IsGenerated) {
                RuntimeModel runtimeModel = GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
                RuntimeMaterial standardMaterial = GeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId);
                EditorEntity entity = SceneCreationService.CreateModel(
                    BuildModelEntityName(entry),
                    runtimeModel,
                    new RuntimeMaterial[] { standardMaterial },
                    modelReference,
                    new SceneAssetReference[] { BuildGeneratedStandardMaterialReference() });
                entity.Position = placementPosition;
                return entity;
            }

            ModelAssetImportSettings importSettings = AssetImportManager.LoadOrCreateModelImportSettings(entry.FullPath);
            if (importSettings == null || importSettings.Importer == null || string.IsNullOrWhiteSpace(importSettings.Importer.ImporterId)) {
                throw new InvalidOperationException("Model import settings could not be resolved.");
            }

            ImportedModelAssetSet importedModel = EditorContentManager.Load<ImportedModelAssetSet>(entry.FullPath, importSettings.Importer.ImporterId);
            if (importedModel == null || importedModel.ModelAsset == null) {
                throw new InvalidOperationException("Model import did not produce a runtime model asset.");
            }

            RuntimeModel runtimeImportedModel = Render3D.BuildModelFromRaw(importedModel.ModelAsset);
            RuntimeMaterial[] runtimeMaterials = ResolveImportedModelMaterials(entry, importedModel.GeneratedMaterials);
            SceneAssetReference[] materialSlots = BuildImportedModelMaterialSlots(entry, importedModel.GeneratedMaterials);
            if (runtimeMaterials.Length == 0) {
                runtimeMaterials = new RuntimeMaterial[] { GeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId) };
                materialSlots = new SceneAssetReference[] { BuildGeneratedStandardMaterialReference() };
            }

            EditorEntity importedEntity = SceneCreationService.CreateModel(
                BuildModelEntityName(entry),
                runtimeImportedModel,
                runtimeMaterials,
                modelReference,
                materialSlots);
            importedEntity.Position = placementPosition;
            return importedEntity;
        }

        /// <summary>
        /// Creates one blueprint instance root for an asset-browser blueprint entry and expands its
        /// inherited children so the instance appears in the hierarchy exactly as it will at runtime.
        /// </summary>
        /// <param name="entry">Blueprint asset entry selected in the asset browser.</param>
        /// <param name="placementPosition">World-space position the new instance root is spawned at.</param>
        /// <returns>Configured blueprint instance root entity.</returns>
        public EditorEntity CreateBlueprintInstanceSceneEntity(AssetBrowserEntry entry, float3 placementPosition) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (entry.IsDirectory || entry.EntryKind != AssetEntryKind.Blueprint) {
                throw new InvalidOperationException("Only blueprint assets can be instantiated into the scene.");
            }

            SceneAssetReference blueprintReference = SceneAssetReferenceFactory.CreateFromEntry(entry);
            EditorEntity entity = SceneCreationService.CreateBlueprintInstance(BuildModelEntityName(entry), blueprintReference);
            SceneFileLoadService.ExpandBlueprintInstanceRoot(entity);
            entity.Position = placementPosition;
            return entity;
        }

        /// <summary>
        /// Resolves imported runtime materials for one imported model entry.
        /// </summary>
        /// <param name="entry">Model browser entry that owns the imported materials.</param>
        /// <param name="generatedMaterials">Generated material records returned by the importer.</param>
        /// <returns>Runtime materials ordered by imported submesh slot.</returns>
        public RuntimeMaterial[] ResolveImportedModelMaterials(AssetBrowserEntry entry, ImportedModelMaterialAsset[] generatedMaterials) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }

            RuntimeMaterial[] runtimeMaterials = new RuntimeMaterial[generatedMaterials.Length];
            for (int index = 0; index < generatedMaterials.Length; index++) {
                ImportedModelMaterialAsset generatedMaterial = generatedMaterials[index];
                if (generatedMaterial == null) {
                    throw new InvalidOperationException("Imported model material entries cannot contain null values.");
                }

                runtimeMaterials[index] = SceneAssetReferenceResolver.ResolveMaterial(BuildImportedModelMaterialReference(entry, generatedMaterial));
            }

            return runtimeMaterials;
        }

        /// <summary>
        /// Builds the stable scene references used by imported model material slots.
        /// </summary>
        /// <param name="entry">Model browser entry that owns the imported materials.</param>
        /// <param name="generatedMaterials">Generated material records returned by the importer.</param>
        /// <returns>Scene asset references ordered by imported submesh slot.</returns>
        public SceneAssetReference[] BuildImportedModelMaterialSlots(AssetBrowserEntry entry, ImportedModelMaterialAsset[] generatedMaterials) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }

            SceneAssetReference[] references = new SceneAssetReference[generatedMaterials.Length];
            for (int index = 0; index < generatedMaterials.Length; index++) {
                ImportedModelMaterialAsset generatedMaterial = generatedMaterials[index];
                if (generatedMaterial == null) {
                    throw new InvalidOperationException("Imported model material entries cannot contain null values.");
                }

                references[index] = BuildImportedModelMaterialReference(entry, generatedMaterial);
            }

            return references;
        }

        /// <summary>
        /// Builds one stable scene reference for one imported model material asset.
        /// </summary>
        /// <param name="entry">Model browser entry that owns the imported material.</param>
        /// <param name="generatedMaterial">Generated material entry produced by the importer.</param>
        /// <returns>Scene asset reference for the generated material asset.</returns>
        public SceneAssetReference BuildImportedModelMaterialReference(AssetBrowserEntry entry, ImportedModelMaterialAsset generatedMaterial) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (generatedMaterial == null) {
                throw new ArgumentNullException(nameof(generatedMaterial));
            }

            string sourceDirectoryPath = Path.GetDirectoryName(entry.FullPath);
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath)) {
                throw new InvalidOperationException("Model source directory could not be resolved.");
            }

            string materialFullPath = Path.GetFullPath(Path.Combine(sourceDirectoryPath, generatedMaterial.RelativeMaterialPath));
            if (!File.Exists(materialFullPath)) {
                throw new InvalidOperationException($"Imported material source '{materialFullPath}' does not exist and cannot produce a canonical asset reference.");
            }

            return AuthoredAssetReferenceResolver.CreateFileReference(materialFullPath, AssetEntryKind.Material);
        }
    }
}
