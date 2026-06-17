using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene save and load services for user-authored editor entities.
    /// </summary>
    public class SceneSaveServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used for scene save outputs.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for scene serialization.
        /// </summary>
        public SceneSaveServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-save-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            EditorCore core = new EditorCore(new Project {
                Name = "Scene Save Service",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorCameraVisualResources.ResetForTests();
            EditorPointLightVisualResources.ResetForTests();
            EditorDirectionalLightVisualResources.ResetForTests();
            EditorSpotLightVisualResources.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene save writes one `.helen` file, excludes internal editor entities, and round-trips mesh persistence through the load service.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsUserEntities_RoundTripsUserHierarchyAndExcludesInternalEntities() {
            SceneAssetReference modelReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Cube",
                ProviderId = "engine",
                AssetId = EngineGeneratedModelCache.CubeAssetId
            };
            SceneAssetReference materialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Materials/Default.hasset"
            };

            EditorEntity root = CreateUserEntity("Root", new float3(1f, 2f, 3f), new float3(2f, 2f, 2f), new float4(0f, 0.70710677f, 0f, 0.70710677f));
            MeshComponent rootMesh = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial(),
                RenderOrder3D = 5
            };
            root.AddComponent(rootMesh);
            GetSaveComponent(root).SetAssetReference(rootMesh, "Model", modelReference);
            GetSaveComponent(root).SetAssetReference(rootMesh, "Material", materialReference);

            EditorEntity child = CreateUserEntity("Child", new float3(5f, 6f, 7f), float3.One, float4.Identity);
            root.AddChild(child);

            EditorEntity internalEntity = CreateUserEntity("Internal", new float3(9f, 9f, 9f), float3.One, float4.Identity);
            internalEntity.InternalEntity = true;

            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "RoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal("Scenes/RoundTrip.helen", asset.Id);
            Assert.Equal(2, asset.AssetReferences.Length);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == modelReference.SourceKind &&
                reference.RelativePath == modelReference.RelativePath &&
                reference.ProviderId == modelReference.ProviderId &&
                reference.AssetId == modelReference.AssetId);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == materialReference.SourceKind &&
                reference.RelativePath == materialReference.RelativePath);
            Assert.Single(asset.RootEntities);
            Assert.NotEqual(0u, asset.RootEntities[0].Id);
            Assert.Equal("Root", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Children);
            Assert.Equal("Child", asset.RootEntities[0].Children[0].Name);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            TestRuntimeModel loadedModel = new TestRuntimeModel();
            TestRuntimeMaterial loadedMaterial = new TestRuntimeMaterial();
            resolver.RegisterModel(modelReference, loadedModel);
            resolver.RegisterMaterial(materialReference, loadedMaterial);

            SceneLoadService loadService = new SceneLoadService(registry, resolver);
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedRoot = Assert.Single(loadedRoots);
            Assert.Equal("Root", loadedRoot.Name);
            Assert.Equal(new float3(1f, 2f, 3f), loadedRoot.LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), loadedRoot.LocalScale);
            Assert.Equal(new float4(0f, 0.70710677f, 0f, 0.70710677f), loadedRoot.LocalOrientation);
            Assert.Equal(asset.RootEntities[0].Id, GetSaveComponent(loadedRoot).EntityId);
            Assert.Single(loadedRoot.Children);
            Assert.Equal("Child", ((EditorEntity)loadedRoot.Children[0]).Name);

            MeshComponent loadedMesh = FindMeshComponent(loadedRoot);
            Assert.Same(loadedModel, loadedMesh.Model);
            Assert.Same(loadedMaterial, loadedMesh.Material);
            Assert.Equal((byte)5, loadedMesh.RenderOrder3D);
            Assert.True(GetSaveComponent(loadedRoot).TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference("Model", out SceneAssetReference loadedModelReference));
            Assert.Equal(modelReference.AssetId, loadedModelReference.AssetId);
        }

        /// <summary>
        /// Ensures scene save and load preserve the authored static flag for root and child entities.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenEntityStaticFlagsDiffer_RoundTripsStaticFlags() {
            EditorEntity root = CreateUserEntity("StaticRoot", new float3(1f, 2f, 3f), float3.One, float4.Identity);
            root.Static = true;

            EditorEntity child = CreateUserEntity("DynamicChild", new float3(4f, 5f, 6f), float3.One, float4.Identity);
            child.Static = false;
            root.AddChild(child);

            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "StaticFlags.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset rootAsset = Assert.Single(asset.RootEntities);
            Assert.True(rootAsset.IsStatic);
            SceneEntityAsset childAsset = Assert.Single(rootAsset.Children);
            Assert.False(childAsset.IsStatic);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            EditorEntity loadedRoot = Assert.Single(loadService.Load(asset));
            Assert.True(loadedRoot.Static);

            EditorEntity loadedChild = Assert.IsType<EditorEntity>(Assert.Single(loadedRoot.Children));
            Assert.False(loadedChild.Static);
        }

        /// <summary>
        /// Ensures scene save can infer generated mesh asset references from the live runtime assignments without requiring user-authored save metadata.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenMeshUsesGeneratedAssetsWithoutStoredReferences_InfersReferencesDuringSave() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "GeneratedMeshInference.helen");

            EditorEntity root = CreateUserEntity("GeneratedCube", float3.Zero, float3.One, float4.Identity);
            MeshComponent meshComponent = new MeshComponent {
                Model = EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId),
                Material = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId),
                RenderOrder3D = 9
            };
            root.AddComponent(meshComponent);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal(2, asset.AssetReferences.Length);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == SceneAssetReferenceSourceKind.Generated &&
                reference.ProviderId == EngineGeneratedAssetProvider.ProviderIdValue &&
                reference.RelativePath == EngineGeneratedAssetProvider.CubeRelativePath &&
                reference.AssetId == EngineGeneratedModelCache.CubeAssetId);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == SceneAssetReferenceSourceKind.Generated &&
                reference.ProviderId == EngineGeneratedAssetProvider.ProviderIdValue &&
                reference.RelativePath == EngineGeneratedAssetProvider.StandardMaterialRelativePath &&
                reference.AssetId == EngineGeneratedMaterialCache.StandardAssetId);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            RuntimeModel generatedModel = EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId);
            RuntimeMaterial generatedMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId);
            resolver.RegisterModel(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EngineGeneratedAssetProvider.CubeRelativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = EngineGeneratedModelCache.CubeAssetId
            }, generatedModel);
            resolver.RegisterMaterial(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            }, generatedMaterial);
            SceneLoadService loadService = new SceneLoadService(registry, resolver);
            EditorEntity loadedRoot = Assert.Single(loadService.Load(asset));
            MeshComponent loadedMesh = FindMeshComponent(loadedRoot);

            Assert.Same(generatedModel, loadedMesh.Model);
            Assert.Same(generatedMaterial, loadedMesh.Material);
            Assert.True(GetSaveComponent(loadedRoot).TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference("Model", out SceneAssetReference loadedModelReference));
            Assert.True(loadedSaveState.TryGetAssetReference("Material", out SceneAssetReference loadedMaterialReference));
            Assert.Equal(EngineGeneratedAssetProvider.CubeRelativePath, loadedModelReference.RelativePath);
            Assert.Equal(EngineGeneratedAssetProvider.StandardMaterialRelativePath, loadedMaterialReference.RelativePath);
        }

        /// <summary>
        /// Ensures scene save can infer one file-backed model reference from the live runtime model id without requiring user-authored save metadata.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenMeshUsesFileSystemModelWithoutStoredReference_InfersReferenceDuringSave() {
            string modelRelativePath = Path.Combine("Models", "Arrow.obj");
            string modelSourcePath = Path.Combine(TempProjectRootPath, "assets", modelRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(modelSourcePath));
            File.WriteAllText(modelSourcePath, "# test");

            ModelAssetImportSettings importSettings = new ModelAssetImportSettings();
            importSettings.Importer.ImporterId = "obj";
            importSettings.Importer.SourceChecksum = "checksum";
            importSettings.Importer.AssetId = "imported-arrow-model";
            using (FileStream stream = File.Create(modelSourcePath + ".hasset")) {
                ModelAssetImportSettingsBinarySerializer.Serialize(stream, importSettings);
            }

            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "FileSystemModelInference.helen");

            EditorEntity root = CreateUserEntity("Arrow", float3.Zero, float3.One, float4.Identity);
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            runtimeModel.SetId(importSettings.Importer.AssetId);
            MeshComponent meshComponent = new MeshComponent {
                Model = runtimeModel,
                Material = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId),
                RenderOrder3D = 3
            };
            root.AddComponent(meshComponent);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem &&
                reference.RelativePath == "Models/Arrow.obj");
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == SceneAssetReferenceSourceKind.Generated &&
                reference.RelativePath == EngineGeneratedAssetProvider.StandardMaterialRelativePath &&
                reference.AssetId == EngineGeneratedMaterialCache.StandardAssetId);
        }

        /// <summary>
        /// Ensures scene save can infer the editor default-font reference for FPS overlays without requiring user-authored save metadata.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenFpsUsesEditorCoreFont_InfersReferenceDuringSave() {
            EditorCore editorCore = Assert.IsType<EditorCore>(Core.Instance);
            editorCore.SetDefaultFontAssetForEditor(CreateFont("EditorUi"));
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "FpsFontInference.helen");

            EditorEntity root = CreateUserEntity("Fps", float3.Zero, float3.One, float4.Identity);
            FPSComponent fpsComponent = new FPSComponent {
                Font = editorCore.DefaultFontAssetForEditor,
                RefreshIntervalSeconds = 1.75d,
                Padding = new int2(4, 6),
                RenderOrder2D = 211
            };
            root.AddComponent(fpsComponent);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneAssetReference fontReference = Assert.Single(asset.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, fontReference.SourceKind);
            Assert.Equal("editor", fontReference.ProviderId);
            Assert.Equal("ui-font", fontReference.AssetId);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            resolver.RegisterFont(fontReference, editorCore.DefaultFontAssetForEditor);
            SceneLoadService loadService = new SceneLoadService(registry, resolver);
            EditorEntity loadedRoot = Assert.Single(loadService.Load(asset));
            FPSComponent loadedComponent = Assert.IsType<FPSComponent>(Assert.Single(loadedRoot.Components, component => component is FPSComponent));

            Assert.Same(editorCore.DefaultFontAssetForEditor, loadedComponent.Font);
            Assert.True(GetSaveComponent(loadedRoot).TryGetComponentState(loadedComponent, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference("Font", out SceneAssetReference loadedFontReference));
            Assert.Equal(fontReference.AssetId, loadedFontReference.AssetId);
        }

        /// <summary>
        /// Ensures scene save collects multiple text font references into the generic scene dependency manifest.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsMultipleTextComponents_CollectsAllFontReferences() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "TextRoundTrip.helen");

            SceneAssetReference titleFontReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Fonts/Title",
                ProviderId = "fonts",
                AssetId = "title"
            };
            SceneAssetReference bodyFontReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Fonts/Body",
                ProviderId = "fonts",
                AssetId = "body"
            };

            EditorEntity root = CreateUserEntity("Root", float3.Zero, float3.One, float4.Identity);
            TextComponent titleText = new TextComponent {
                Font = CreateFont("Title"),
                Text = "Title",
                WrapText = false,
                Size = new int2(240, 48),
                Color = new byte4(255, 255, 255, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0f,
                RenderOrder2D = 11,
                LayerMask = 3
            };
            root.AddComponent(titleText);
            GetSaveComponent(root).SetAssetReference(titleText, "Font", titleFontReference);

            EditorEntity child = CreateUserEntity("Child", new float3(0f, 32f, 0f), float3.One, float4.Identity);
            TextComponent bodyText = new TextComponent {
                Font = CreateFont("Body"),
                Text = "Body",
                WrapText = true,
                Size = new int2(320, 96),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0f,
                RenderOrder2D = 12,
                LayerMask = 5
            };
            child.AddComponent(bodyText);
            root.AddChild(child);
            GetSaveComponent(child).SetAssetReference(bodyText, "Font", bodyFontReference);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal(2, asset.AssetReferences.Length);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.RelativePath == titleFontReference.RelativePath &&
                reference.ProviderId == titleFontReference.ProviderId &&
                reference.AssetId == titleFontReference.AssetId);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.RelativePath == bodyFontReference.RelativePath &&
                reference.ProviderId == bodyFontReference.ProviderId &&
                reference.AssetId == bodyFontReference.AssetId);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            FontAsset loadedTitleFont = CreateFont("LoadedTitle");
            FontAsset loadedBodyFont = CreateFont("LoadedBody");
            resolver.RegisterFont(titleFontReference, loadedTitleFont);
            resolver.RegisterFont(bodyFontReference, loadedBodyFont);

            SceneLoadService loadService = new SceneLoadService(registry, resolver);
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedRoot = Assert.Single(loadedRoots);
            TextComponent loadedTitleText = Assert.IsType<TextComponent>(Assert.Single(loadedRoot.Components, component => component is TextComponent));
            Assert.Same(loadedTitleFont, loadedTitleText.Font);
            Assert.Equal("Title", loadedTitleText.Text);

            EditorEntity loadedChild = Assert.IsType<EditorEntity>(Assert.Single(loadedRoot.Children));
            TextComponent loadedBodyText = Assert.IsType<TextComponent>(Assert.Single(loadedChild.Components, component => component is TextComponent));
            Assert.Same(loadedBodyFont, loadedBodyText.Font);
            Assert.Equal("Body", loadedBodyText.Text);
        }

        /// <summary>
        /// Ensures built-in text components persist their font asset through the reflected `Font` member name instead of the removed legacy `FontReference` field.
        /// </summary>
        [Fact]
        public void Save_WhenSceneContainsTextComponent_WritesTaggedPayloadUsingFontMemberName() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "TextFontFieldName.helen");
            SceneAssetReference fontReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Fonts/Body",
                ProviderId = "fonts",
                AssetId = "body"
            };

            EditorEntity root = CreateUserEntity("Root", float3.Zero, float3.One, float4.Identity);
            TextComponent textComponent = new TextComponent {
                Font = CreateFont("Body"),
                Text = "Body",
                WrapText = true,
                Size = new int2(320, 96),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0f,
                RenderOrder2D = 12,
                LayerMask = 5
            };
            root.AddComponent(textComponent);
            GetSaveComponent(root).SetAssetReference(textComponent, nameof(TextComponent.Font), fontReference);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset rootAsset = Assert.Single(asset.RootEntities);
            SceneComponentAssetRecord textRecord = Assert.Single(rootAsset.Components);
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(textRecord.Payload ?? Array.Empty<byte>());

            Assert.True(reader.TryGetFieldReader(nameof(TextComponent.Font), out EngineBinaryReader fontReader));
            fontReader.Dispose();
            Assert.False(reader.TryGetFieldReader("FontReference", out EngineBinaryReader legacyFontReader));
            Assert.Null(legacyFontReader);
        }

        /// <summary>
        /// Ensures built-in text components persist authored font scale through the reflected tagged payload instead of silently dropping it.
        /// </summary>
        [Fact]
        public void Save_WhenSceneContainsTextComponent_WritesTaggedPayloadUsingFontScaleMember() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "TextFontScale.helen");
            SceneAssetReference fontReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Fonts/Body",
                ProviderId = "fonts",
                AssetId = "body"
            };

            EditorEntity root = CreateUserEntity("Root", float3.Zero, float3.One, float4.Identity);
            TextComponent textComponent = new TextComponent {
                Font = CreateFont("Body"),
                Text = "Scaled Body",
                WrapText = true,
                Size = new int2(320, 96),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0f,
                FontScale = 2f,
                RenderOrder2D = 12,
                LayerMask = 5
            };
            root.AddComponent(textComponent);
            GetSaveComponent(root).SetAssetReference(textComponent, nameof(TextComponent.Font), fontReference);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset rootAsset = Assert.Single(asset.RootEntities);
            SceneComponentAssetRecord textRecord = Assert.Single(rootAsset.Components);
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(textRecord.Payload ?? Array.Empty<byte>());

            Assert.True(reader.TryGetFieldReader(nameof(TextComponent.FontScale), out EngineBinaryReader fontScaleReader));
            using (fontScaleReader) {
                Assert.Equal(2f, fontScaleReader.ReadSingle());
            }
        }

        /// <summary>
        /// Ensures scene save persists the supplied scene-level canvas profile into the serialized scene asset.
        /// </summary>
        [Fact]
        public void Save_WhenSceneSettingsAreProvided_PersistsCanvasProfile() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1920,
                    Height = 1080
                }
            };
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CanvasProfile.helen");

            saveService.Save(scenePath, sceneSettings);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal(1920, asset.SceneSettings.CanvasProfile.Width);
            Assert.Equal(1080, asset.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Ensures scene save persists the dont-unload scene setting into the serialized scene asset.
        /// </summary>
        [Fact]
        public void Save_WhenSceneSettingsDontUnloadIsTrue_PersistsTheFlag() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1920,
                    Height = 1080
                },
                DontUnload = true
            };
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Persistent.helen");

            saveService.Save(scenePath, sceneSettings);

            using FileStream stream = File.OpenRead(scenePath);
            SceneAsset asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            Assert.True(asset.SceneSettings.DontUnload);
        }

        /// <summary>
        /// Ensures scene save persists common entity transforms separately from projected platform overrides and load restores those overrides for later editing.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenEntityHasProjectedPs2TransformOverride_PersistsCommonTransformAndPlatformOverride() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PlatformTransformOverride.helen");
            EditorEntity entity = CreateUserEntity("PlatformEntity", new float3(1f, 2f, 3f), new float3(4f, 5f, 6f), float4.Identity);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            EntityPlatformTransformEditingService transformEditingService = new EntityPlatformTransformEditingService();

            transformEditingService.ActivatePlatform(entity, saveComponent, "ps2");
            entity.LocalPosition = new float3(10f, 20f, 30f);
            transformEditingService.PersistActivePlatform(entity, saveComponent);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset rootEntity = Assert.Single(asset.RootEntities);
            SceneEntityPlatformTransformOverrideAsset ps2Override = Assert.Single(rootEntity.PlatformTransformOverrides);
            Assert.Equal(new float3(1f, 2f, 3f), rootEntity.LocalPosition);
            Assert.Equal("ps2", ps2Override.PlatformId);
            Assert.True(ps2Override.HasLocalPositionOverride);
            Assert.Equal(new float3(10f, 20f, 30f), ps2Override.LocalPosition);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);
            EditorEntity loadedEntity = Assert.Single(loadedRoots);
            EntitySaveComponent loadedSaveComponent = GetSaveComponent(loadedEntity);

            Assert.Equal(new float3(1f, 2f, 3f), loadedEntity.LocalPosition);
            Assert.True(loadedSaveComponent.TryGetTransformPlatformOverride("ps2", out SceneEntityPlatformTransformOverrideAsset loadedOverride));
            Assert.True(loadedOverride.HasLocalPositionOverride);
            Assert.Equal(new float3(10f, 20f, 30f), loadedOverride.LocalPosition);

            transformEditingService.ActivatePlatform(loadedEntity, loadedSaveComponent, "ps2");
            Assert.Equal(new float3(10f, 20f, 30f), loadedEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures sparse component platform overrides persist their explicit property paths and rebuild platform edits from the current common component after reload.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenCameraFarPlaneUsesSparseWindowsOverride_RoundTripsTheOverridePathAndRebuildsFromCommon() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "SparseComponentOverride.helen");
            EditorEntity entity = CreateUserEntity("PlatformCamera", float3.Zero, float3.One, float4.Identity);
            CameraComponent camera = new CameraComponent {
                FarPlaneDistance = 100f
            };
            entity.AddComponent(camera);

            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            ComponentPlatformEditingService platformEditingService = new ComponentPlatformEditingService();
            CameraComponent editableWindowsCamera = Assert.IsType<CameraComponent>(platformEditingService.EnsurePlatformOverrideComponent(camera, saveComponent, "windows"));
            editableWindowsCamera.FarPlaneDistance = 200f;
            platformEditingService.MarkPropertyOverride(camera, saveComponent, "windows", nameof(CameraComponent.FarPlaneDistance));
            platformEditingService.PersistPlatformOverride(camera, editableWindowsCamera, saveComponent, "windows");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            EditorEntity loadedEntity = Assert.Single(loadService.Load(asset));
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedEntity.Components, component => component is CameraComponent));
            EntitySaveComponent loadedSaveComponent = GetSaveComponent(loadedEntity);

            Assert.True(loadedSaveComponent.TryGetComponentState(loadedCamera, out EntityComponentSaveState loadedComponentSaveState));
            Assert.True(loadedComponentSaveState.TryGetPlatformOverride("windows", out EntityComponentPlatformOverrideState loadedOverrideState));
            Assert.True(loadedOverrideState.HasPropertyOverride(nameof(CameraComponent.FarPlaneDistance)));

            platformEditingService = new ComponentPlatformEditingService();
            CameraComponent loadedEditableWindowsCamera = Assert.IsType<CameraComponent>(platformEditingService.ResolveEditableComponent(loadedCamera, loadedSaveComponent, "windows"));
            Assert.Equal(200f, loadedEditableWindowsCamera.FarPlaneDistance);

            loadedCamera.FarPlaneDistance = 150f;
            platformEditingService.ClearPropertyOverride(loadedCamera, loadedSaveComponent, "windows", nameof(CameraComponent.FarPlaneDistance));
            loadedEditableWindowsCamera = Assert.IsType<CameraComponent>(platformEditingService.ResolveEditableComponent(loadedCamera, loadedSaveComponent, "windows"));
            Assert.Equal(150f, loadedEditableWindowsCamera.FarPlaneDistance);
        }

        /// <summary>
        /// Ensures platform-only components round-trip through scene save and load as editor metadata without being materialized into the live common entity.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenWindowsAddsPlatformOnlyCamera_RoundTripsTheAddedComponentWithoutAddingItToCommon() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PlatformAddedCamera.helen");
            EditorEntity entity = CreateUserEntity("PlatformEntity", float3.Zero, float3.One, float4.Identity);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            ComponentPlatformEditingService platformEditingService = new ComponentPlatformEditingService();
            EditorComponentAddDescriptor descriptor = new EditorComponentAddDescriptor(
                "Camera",
                typeof(CameraComponent),
                true,
                target => target.AddComponent(new CameraComponent()));

            EntityPlatformAddedComponentState addedComponentState = platformEditingService.AddPlatformOnlyComponent(descriptor, saveComponent, "windows");
            CameraComponent addedCamera = Assert.IsType<CameraComponent>(addedComponentState.Component);
            addedCamera.FarPlaneDistance = 250f;

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset rootEntity = Assert.Single(asset.RootEntities);
            SceneEntityPlatformComponentOverrideAsset windowsOverride = Assert.Single(rootEntity.PlatformComponentOverrides);
            SceneEntityPlatformAddedComponentAsset addedCameraAsset = Assert.Single(windowsOverride.AddedComponents);
            Assert.Equal("windows", windowsOverride.PlatformId);
            Assert.NotNull(addedCameraAsset.Component);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            EditorEntity loadedEntity = Assert.Single(loadService.Load(asset));
            EntitySaveComponent loadedSaveComponent = GetSaveComponent(loadedEntity);

            Assert.DoesNotContain(loadedEntity.Components, component => component is CameraComponent);

            IReadOnlyList<EntityPlatformAddedComponentState> loadedAddedComponents = platformEditingService.GetAddedComponents(loadedSaveComponent, "windows");
            EntityPlatformAddedComponentState loadedAddedComponentState = Assert.Single(loadedAddedComponents);
            CameraComponent loadedAddedCamera = Assert.IsType<CameraComponent>(loadedAddedComponentState.Component);
            Assert.Equal(250f, loadedAddedCamera.FarPlaneDistance);
        }

        /// <summary>
        /// Ensures save fails clearly when one user component falls into automatic persistence but exposes an unsupported reflected member type.
        /// </summary>
        [Fact]
        public void Save_WhenEntityContainsUnsupportedComponent_ThrowsClearError() {
            EditorEntity entity = CreateUserEntity("Unsupported", float3.Zero, float3.One, float4.Identity);
            entity.AddComponent(new UnsupportedScriptComponent());
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Unsupported.helen");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => saveService.Save(scenePath));

            Assert.Contains(nameof(Entity), exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures eligible scripted components round-trip through the automatic reflected editor persistence fallback.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsEligibleScriptComponent_RoundTripsThroughAutomaticFallback() {
            EditorEntity entity = CreateUserEntity("Scripted", float3.Zero, float3.One, float4.Identity);
            TestScriptSerializableComponent component = new TestScriptSerializableComponent {
                DisplayName = "Runtime Widget",
                Visible = true,
                SortOrder = 9
            };
            entity.AddComponent(component);
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "ScriptedRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord record = Assert.Single(Assert.Single(asset.RootEntities).Components);
            Assert.Equal(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestScriptSerializableComponent)),
                record.ComponentTypeId);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedEntity = Assert.Single(loadedRoots);
            TestScriptSerializableComponent loadedComponent = Assert.IsType<TestScriptSerializableComponent>(
                Assert.Single(loadedEntity.Components, loadedComponent => loadedComponent is TestScriptSerializableComponent));
            Assert.Equal("Runtime Widget", loadedComponent.DisplayName);
            Assert.True(loadedComponent.Visible);
            Assert.Equal(9, loadedComponent.SortOrder);
        }

        /// <summary>
        /// Ensures camera entities persist only the camera component and rebuild the hidden editor visual when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsCameraEntity_RoundTripsCameraAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity cameraEntity = creationService.CreateCamera();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CameraRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.CameraComponent", asset.RootEntities[0].Components[0].ComponentTypeId);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);
            EditorEntity loadedCameraEntity = Assert.Single(loadedRoots);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedCameraEntity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionComponent suppressionComponent = Assert.IsType<EditorSceneCameraSuppressionComponent>(Assert.Single(loadedCameraEntity.Components, component => component is EditorSceneCameraSuppressionComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedCameraEntity.Children));
            EditorCameraVisualComponent loadedVisual = Assert.IsType<EditorCameraVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorCameraVisualComponent));

            Assert.Equal((ushort)0, loadedCamera.LayerMask);
            Assert.False(loadedCamera.ClearSettings.ClearColorEnabled);
            Assert.Equal(EditorLayerMasks.SceneObjects, suppressionComponent.LayerMask);
            Assert.True(suppressionComponent.ClearSettings.ClearColorEnabled);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures point light entities persist only the point-light component and rebuild the hidden editor visual when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsPointLightEntity_RoundTripsPointLightAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity pointLightEntity = creationService.CreatePointLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PointLightRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Equal("Point Light", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.PointLightComponent", asset.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Empty(asset.RootEntities[0].Children);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedPointLightEntity = Assert.Single(loadedRoots);
            PointLightComponent loadedPointLight = Assert.IsType<PointLightComponent>(Assert.Single(loadedPointLightEntity.Components, component => component is PointLightComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedPointLightEntity.Children));
            EditorPointLightVisualComponent loadedVisual = Assert.IsType<EditorPointLightVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorPointLightVisualComponent));

            Assert.Equal(10f, loadedPointLight.Range);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures directional light entities persist only the directional-light component and rebuild the hidden editor arrow when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsDirectionalLightEntity_RoundTripsDirectionalLightAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity directionalLightEntity = creationService.CreateDirectionalLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "DirectionalLightRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Equal("Directional Light", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.DirectionalLightComponent", asset.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Empty(asset.RootEntities[0].Children);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedDirectionalLightEntity = Assert.Single(loadedRoots);
            DirectionalLightComponent loadedDirectionalLight = Assert.IsType<DirectionalLightComponent>(Assert.Single(loadedDirectionalLightEntity.Components, component => component is DirectionalLightComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedDirectionalLightEntity.Children));
            EditorDirectionalLightVisualComponent loadedVisual = Assert.IsType<EditorDirectionalLightVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorDirectionalLightVisualComponent));

            Assert.True(loadedDirectionalLight.ShadowsEnabled);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures spot light entities persist only the spot-light component and rebuild the hidden editor cone when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsSpotLightEntity_RoundTripsSpotLightAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity spotLightEntity = creationService.CreateSpotLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "SpotLightRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Equal("Spot Light", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.SpotLightComponent", asset.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Empty(asset.RootEntities[0].Children);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedSpotLightEntity = Assert.Single(loadedRoots);
            SpotLightComponent loadedSpotLight = Assert.IsType<SpotLightComponent>(Assert.Single(loadedSpotLightEntity.Components, component => component is SpotLightComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedSpotLightEntity.Children));
            EditorSpotLightVisualComponent loadedVisual = Assert.IsType<EditorSpotLightVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorSpotLightVisualComponent));

            Assert.Equal(10f, loadedSpotLight.Range);
            Assert.Equal(25f, loadedSpotLight.InnerConeAngleDegrees);
            Assert.Equal(45f, loadedSpotLight.OuterConeAngleDegrees);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures a point light loaded from scene data registers its hidden editor visual drawable immediately so viewport picking can target it without requiring a later transform edit.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsPointLightEntity_RegistersHiddenEditorVisualDrawableImmediately() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity pointLightEntity = creationService.CreatePointLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PointLightPicker.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            EditorEntity loadedPointLightEntity = Assert.Single(loadService.Load(asset));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedPointLightEntity.Children));
            IDrawable3D visualDrawable = Assert.IsAssignableFrom<IDrawable3D>(Assert.Single(loadedVisualEntity.Components, component => component is EditorPointLightVisualComponent));

            Assert.Contains(visualDrawable, Core.Instance.ObjectManager.Drawables3D);
            Assert.Same(loadedPointLightEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(visualDrawable.Parent));
        }

        /// <summary>
        /// Ensures editor-only component platform overrides round-trip through scene save and load without adding duplicate live components.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenComponentHasWindowsOverride_RoundTripsOverrideMetadataWithoutAddingExtraLiveComponents() {
            EditorEntity root = CreateUserEntity("Scripted", float3.Zero, float3.One, float4.Identity);
            TestScriptSerializableComponent component = new TestScriptSerializableComponent {
                DisplayName = "Common",
                Visible = true,
                SortOrder = 100
            };
            root.AddComponent(component);

            TestScriptSerializableComponent windowsOverrideComponent = new TestScriptSerializableComponent {
                DisplayName = "Windows",
                Visible = false,
                SortOrder = 200
            };

            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(component);

            SceneComponentAssetRecord overrideRecord = descriptor.SerializeComponent(windowsOverrideComponent, 0, null);
            AttachPlatformOverridePayload(root, component, "windows", overrideRecord.Payload);

            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CameraPlatformOverride.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedRoot = Assert.Single(loadedRoots);
            TestScriptSerializableComponent loadedComponent = Assert.IsType<TestScriptSerializableComponent>(Assert.Single(loadedRoot.Components, loadedComponent => loadedComponent is TestScriptSerializableComponent));
            Assert.Equal("Common", loadedComponent.DisplayName);
            Assert.True(loadedComponent.Visible);
            Assert.Equal(100, loadedComponent.SortOrder);

            EntitySaveComponent saveComponent = GetSaveComponent(loadedRoot);
            Assert.True(saveComponent.TryGetComponentState(loadedComponent, out EntityComponentSaveState loadedSaveState));
            Assert.True(TryGetPlatformOverride(loadedSaveState, "windows", out object loadedOverrideState));

            Type overrideStateType = ResolveRequiredType("helengine.EntityComponentPlatformOverrideState");
            byte[] loadedPayload = Assert.IsType<byte[]>(overrideStateType.GetProperty("Payload").GetValue(loadedOverrideState));

            Assert.Equal(overrideRecord.Payload, loadedPayload);
            Assert.Single(loadedRoot.Components, loadedComponent => loadedComponent is TestScriptSerializableComponent);
        }

        /// <summary>
        /// Creates one user-authored editor entity configured for scene serialization.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <param name="position">Local position assigned to the entity.</param>
        /// <param name="scale">Local scale assigned to the entity.</param>
        /// <param name="orientation">Local orientation assigned to the entity.</param>
        /// <returns>Configured editor entity.</returns>
        EditorEntity CreateUserEntity(string name, float3 position, float3 scale, float4 orientation) {
            EditorEntity entity = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create(name));
            entity.LocalPosition = position;
            entity.LocalScale = scale;
            entity.LocalOrientation = orientation;
            return entity;
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
        }

        /// <summary>
        /// Attaches one editor-only platform override payload to one persisted component.
        /// </summary>
        /// <param name="entity">Entity that owns the component.</param>
        /// <param name="component">Component whose save-state should receive the override.</param>
        /// <param name="platformId">Platform identifier that owns the override.</param>
        /// <param name="payload">Serialized override payload.</param>
        void AttachPlatformOverridePayload(EditorEntity entity, Component component, string platformId, byte[] payload) {
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(component);
            Type overrideStateType = ResolveRequiredType("helengine.EntityComponentPlatformOverrideState");
            object overrideState = Activator.CreateInstance(overrideStateType);

            overrideStateType.GetProperty("PlatformId").SetValue(overrideState, platformId);
            overrideStateType.GetProperty("Payload").SetValue(overrideState, payload);

            MethodInfo setMethod = typeof(EntityComponentSaveState).GetMethod("SetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(setMethod);
            setMethod.Invoke(saveState, new[] { platformId, overrideState });
        }

        /// <summary>
        /// Attempts to read one platform override entry from one component save-state.
        /// </summary>
        /// <param name="saveState">Component save-state that owns the override entries.</param>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <param name="overrideState">Resolved override entry when one exists.</param>
        /// <returns>True when the requested override exists.</returns>
        bool TryGetPlatformOverride(EntityComponentSaveState saveState, string platformId, out object overrideState) {
            MethodInfo tryGetMethod = typeof(EntityComponentSaveState).GetMethod("TryGetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(tryGetMethod);

            object[] arguments = new object[] { platformId, null };
            bool found = Assert.IsType<bool>(tryGetMethod.Invoke(saveState, arguments));
            overrideState = arguments[1];
            return found;
        }

        /// <summary>
        /// Finds the mesh component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose mesh component should be returned.</param>
        /// <returns>Attached mesh component.</returns>
        MeshComponent FindMeshComponent(EditorEntity entity) {
            return Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
        }

        /// <summary>
        /// Creates a stable runtime font asset used by scene persistence tests.
        /// </summary>
        /// <param name="name">Friendly font name.</param>
        /// <returns>Runtime font asset with deterministic metrics.</returns>
        FontAsset CreateFont(string name) {
            return new FontAsset(
                new FontInfo(name, 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1);
        }

        /// <summary>
        /// Resolves one required editor runtime type by full name.
        /// </summary>
        /// <param name="typeName">Full type name to resolve from the editor assembly.</param>
        /// <returns>Resolved runtime type.</returns>
        Type ResolveRequiredType(string typeName) {
            Type type = typeof(EntityComponentSaveState).Assembly.GetType(typeName, false);
            Assert.NotNull(type);
            return type;
        }
    }
}
