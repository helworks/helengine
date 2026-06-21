using System.Collections.Generic;
using System.Reflection;
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated model picks in component property rows.
    /// </summary>
    public class ComponentPropertiesViewGeneratedAssetTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes an isolated content root and the core services required by component property rows.
        /// </summary>
        public ComponentPropertiesViewGeneratedAssetTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-model-picker-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Clears generated provider registrations and temporary test content.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures generated model picks assign the provider runtime model and keep the selected display label.
        /// </summary>
        [Fact]
        public void HandleModelPicked_WhenEntryIsGenerated_AssignsTheProviderRuntimeModelDisplayLabelAndGeneratedReference() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                },
                runtimeModel));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleModelPicked.Invoke(view, new object[] {
                modelRow,
                AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
            });

            Assert.Same(runtimeModel, meshComponent.Model);
            Assert.Equal("Cube", modelRow.ValueText.Text);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference reference));
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
            Assert.Equal("engine", reference.ProviderId);
            Assert.Equal(EngineGeneratedModelCache.CubeAssetId, reference.AssetId);
        }

        /// <summary>
        /// Ensures generated model picks mark the current scene as mutated.
        /// </summary>
        [Fact]
        public void HandleModelPicked_WhenEntryIsGenerated_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                },
                runtimeModel));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;

                handleModelPicked.Invoke(view, new object[] {
                    modelRow,
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                });

                Assert.True(raised);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
                EditorSceneMutationService.Reset();
            }
        }

        /// <summary>
        /// Ensures generated material picks assign the provider runtime material and persist the generated reference.
        /// </summary>
        [Fact]
        public void HandleMaterialPicked_WhenEntryIsGenerated_AssignsTheProviderRuntimeMaterialDisplayLabelAndGeneratedReference() {
            TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
                },
                new TestRuntimeModel(),
                runtimeMaterial));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            ComponentPropertyRow materialRow = FindMaterialRow(view);
            MethodInfo handleMaterialPicked = typeof(ComponentPropertiesView).GetMethod("HandleMaterialPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleMaterialPicked.Invoke(view, new object[] {
                materialRow,
                AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
            });

            Assert.Same(runtimeMaterial, Assert.Single(meshComponent.Materials));
            Assert.Equal("Standard", materialRow.ValueText.Text);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Materials[0]", out SceneAssetReference reference));
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
            Assert.Equal("engine", reference.ProviderId);
            Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, reference.AssetId);
        }

        /// <summary>
        /// Ensures generated material picks on platform-only added components store the generated reference in the detached component save-state.
        /// </summary>
        [Fact]
        public void HandleMaterialPicked_WhenPlatformOnlyMeshEntryIsGenerated_StoresTheReferenceInTheAddedComponentSaveState() {
            TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
                },
                new TestRuntimeModel(),
                runtimeMaterial));

            EditorEntity entity = new EditorEntity();
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            ComponentPlatformEditingService platformEditingService = new ComponentPlatformEditingService();
            EditorComponentAddDescriptor descriptor = new EditorComponentAddDescriptor(
                "Mesh",
                typeof(MeshComponent),
                true,
                target => target.AddComponent(new MeshComponent()));
            EntityPlatformAddedComponentState addedComponentState = platformEditingService.AddPlatformOnlyComponent(descriptor, saveComponent, "windows");

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity, "windows");

            ComponentPropertyRow materialRow = FindMaterialRow(view);
            MethodInfo handleMaterialPicked = typeof(ComponentPropertiesView).GetMethod("HandleMaterialPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleMaterialPicked.Invoke(view, new object[] {
                materialRow,
                AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
            });

            MeshComponent addedMesh = Assert.IsType<MeshComponent>(addedComponentState.Component);
            Assert.Same(runtimeMaterial, Assert.Single(addedMesh.Materials));
            Assert.Equal("Standard", materialRow.ValueText.Text);
            Assert.True(addedComponentState.SaveState.TryGetAssetReference("Materials[0]", out SceneAssetReference reference));
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
            Assert.Equal("engine", reference.ProviderId);
            Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, reference.AssetId);
        }

        /// <summary>
        /// Ensures file-backed material picks fall back to the first preview-capable platform when the active platform cannot supply one shader-backed runtime material.
        /// </summary>
        [Fact]
        public void HandleMaterialPicked_WhenActivePlatformLacksPreviewShader_UsesFirstPreviewCapablePlatform() {
            string materialRelativePath = "Materials/rendering/colored_cube_grid/Cube00.hasset";
            string materialFullPath = WriteMaterialSettingsDocument(materialRelativePath, CreatePreviewAndFixedPipelineMaterialSettings("#336699"));
            new EditorProjectPlatformsService(TempRootPath).Save(new EditorProjectPlatformsDocument {
                SupportedPlatforms = ["windows", "ps2"]
            });
            new EditorProjectLocalSettingsService(TempRootPath, ["windows", "ps2"]).SaveActivePlatform("ps2");
            EditorProjectPaths.Initialize(TempRootPath);
            ContentManager contentManager = new ContentManager(TempRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            using ShaderModuleManager shaderModuleManager = CreateShaderModuleManager();
            EditorShaderPackageService.Initialize(shaderModuleManager, ShaderCompileTarget.DirectX11, contentManager);

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), contentManager);
            view.ShowComponents(entity);

            ComponentPropertyRow materialRow = FindMaterialRow(view);
            MethodInfo handleMaterialPicked = typeof(ComponentPropertiesView).GetMethod("HandleMaterialPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleMaterialPicked.Invoke(view, new object[] {
                materialRow,
                AssetBrowserEntry.CreateFileSystemFile("Cube00", materialRelativePath, materialFullPath, ".hasset", AssetEntryKind.Material)
            });

            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            ShaderMaterialAsset builtMaterialAsset = Assert.Single(renderManager.BuiltMaterialAssets);

            Assert.NotNull(Assert.Single(meshComponent.Materials));
            Assert.Equal("Cube00", materialRow.ValueText.Text);
            Assert.Equal("ForwardStandardShader", builtMaterialAsset.ShaderAssetId);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Materials[0]", out SceneAssetReference reference));
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
            Assert.Equal(materialRelativePath, reference.RelativePath);
        }

        /// <summary>
        /// Creates one entity and attaches the supplied component so the properties view can inspect it.
        /// </summary>
        /// <param name="component">Component to add to the entity.</param>
        /// <returns>Entity containing the supplied component.</returns>
        EditorEntity CreateEntityWithComponent(Component component) {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(component);
            return entity;
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be read.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, value => value is EntitySaveComponent));
        }

        /// <summary>
        /// Finds the active model row produced for the mesh component.
        /// </summary>
        /// <param name="view">Properties view whose active rows should be inspected.</param>
        /// <returns>The single model row displayed by the view.</returns>
        ComponentPropertyRow FindModelRow(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
            return Assert.Single(rows, row => row.Kind == ComponentPropertyRowKind.Model);
        }

        /// <summary>
        /// Finds the active material row produced for the mesh component.
        /// </summary>
        /// <param name="view">Properties view whose active rows should be inspected.</param>
        /// <returns>The single material row displayed by the view.</returns>
        ComponentPropertyRow FindMaterialRow(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
            return Assert.Single(rows, row => row.Kind == ComponentPropertyRowKind.Material);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of property rows.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Creates a disposable shader module manager for file-backed material preview tests.
        /// </summary>
        /// <returns>Shader module manager rooted under the temporary test content tree.</returns>
        ShaderModuleManager CreateShaderModuleManager() {
            string shaderRootPath = Path.Combine(TempRootPath, "assets", "Shaders");
            string packageOutputPath = Path.Combine(TempRootPath, "cache", "shader-cache");
            Directory.CreateDirectory(shaderRootPath);
            Directory.CreateDirectory(packageOutputPath);

            ShaderTargetBuildOptions targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0));
            ShaderPackageBuildOptions buildOptions = new ShaderPackageBuildOptions(
                new[] { targetOptions },
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                Array.Empty<ShaderDefine>());
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
            ShaderModuleManagerOptions options = new ShaderModuleManagerOptions(
                shaderRootPath,
                packageOutputPath,
                buildOptions,
                ShaderCompileTarget.DirectX11,
                shaderBackendRegistry,
                100);
            return new ShaderModuleManager(options);
        }

        /// <summary>
        /// Writes one authored material settings document directly to the material `.hasset` path.
        /// </summary>
        /// <param name="relativePath">Project-relative material path whose authored document should be written.</param>
        /// <param name="settings">Material settings payload to persist.</param>
        /// <returns>Absolute path to the written material settings document.</returns>
        string WriteMaterialSettingsDocument(string relativePath, MaterialAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string materialFullPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(materialFullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Material directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(materialFullPath, settings);
            return materialFullPath;
        }

        /// <summary>
        /// Creates one multi-platform material settings payload with a Windows preview shader path and one fixed-pipeline PS2 fallback that lacks direct shader metadata.
        /// </summary>
        /// <param name="baseColor">Authored base color in editor HTML hex form.</param>
        /// <returns>Material settings payload that reproduces editor preview platform selection regressions.</returns>
        MaterialAssetImportSettings CreatePreviewAndFixedPipelineMaterialSettings(string baseColor) {
            if (string.IsNullOrWhiteSpace(baseColor)) {
                throw new ArgumentException("Base color must be provided.", nameof(baseColor));
            }

            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = "helengine.material";
            settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
                SchemaId = "standard-shader",
                FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    ["use-custom-shader"] = "false",
                    ["texture-id"] = string.Empty,
                    ["casts-shadow"] = "true",
                    ["receives-shadow"] = "true",
                    ["base-color"] = baseColor
                }
            };
            settings.Processor.Platforms["ps2"] = new MaterialAssetProcessorSettings {
                SchemaId = "ps2-simple-lit-textured",
                FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    ["texture-id"] = string.Empty,
                    ["casts-shadow"] = "true",
                    ["receives-shadow"] = "true",
                    ["base-color"] = baseColor
                }
            };
            return settings;
        }
    }
}
