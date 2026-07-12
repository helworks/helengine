using System.Reflection;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Paths;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Results;
using helengine.directx11;
using helengine.editor.tests.serialization.scene;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies Windows scene packaging collects referenced shader ids from packaged material assets.
    /// </summary>
    public class EditorPlatformBuildScenePackagerTests : IDisposable {
        /// <summary>
        /// Temporary project root used for scene-packager tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build root used for scene-packager tests.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes one isolated project workspace for scene packaging verification.
        /// </summary>
        public EditorPlatformBuildScenePackagerTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-packager-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            BuildRootPath = Path.Combine(workspaceRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache", "shader-cache"));
            Directory.CreateDirectory(BuildRootPath);

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        }

        /// <summary>
        /// Deletes the temporary project workspace after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the real Windows builder metadata and gameplay script-type resolver package project-owned scripted scene components through the generic automatic runtime payload contract.
        /// </summary>
        [Fact]
        public void Package_WhenWindowsBuilderCompatibilityMetadataAndScriptResolverAreSupplied_PackagesCityStyleScriptComponents() {
            string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string sourceProjectRootPath = Path.Combine(repositoryRootPath, "test-project");
            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(sourceProjectRootPath, "project.heproj"));
            AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("windows");
            EditorPlatformAssetBuilderLoader builderLoader = new();
            IPlatformAssetBuilder builder = builderLoader.Load(platformDescriptor.BuilderAssemblyPath);
            AutomaticScriptComponentPersistenceDescriptor automaticDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            DictionaryScriptTypeResolver scriptTypeResolver = new DictionaryScriptTypeResolver();
            scriptTypeResolver.Register("project.menu.SceneReturnComponent, gameplay", typeof(TestSceneReturnComponent));
            scriptTypeResolver.Register("project.rendering.TowerSpinComponent, gameplay", typeof(TestDirectionalShadowTowerSpinComponent));

            SceneComponentAssetRecord returnToMenuRecord = new SceneComponentAssetRecord {
                ComponentTypeId = "project.menu.SceneReturnComponent, gameplay",
                ComponentIndex = 0,
                Payload = Array.Empty<byte>()
            };
            SceneComponentAssetRecord towerSpinRecord = automaticDescriptor.SerializeComponent(
                new TestDirectionalShadowTowerSpinComponent {
                    BaseYawRadians = 0.75f,
                    AngularSpeedRadians = 1.5f
                },
                1,
                new EntityComponentSaveState());
            towerSpinRecord.ComponentTypeId = "project.rendering.TowerSpinComponent, gameplay";

            string sceneId = "Scenes/CityStyleCompatibilityScene.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "CityRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            returnToMenuRecord,
                            towerSpinRecord
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                builder.Definition,
                null,
                builder,
                "debug",
                "directx11",
                scriptTypeResolver);

            packager.Package(new[] { sceneId }, BuildRootPath);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);
            SceneComponentAssetRecord packagedReturnToMenuRecord = Assert.Single(
                packagedRoot.Components,
                componentRecord => string.Equals(componentRecord.ComponentTypeId, "project.menu.SceneReturnComponent, gameplay", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedTowerSpinRecord = Assert.Single(
                packagedRoot.Components,
                componentRecord => string.Equals(componentRecord.ComponentTypeId, "project.rendering.TowerSpinComponent, gameplay", StringComparison.Ordinal));

            using (MemoryStream payloadStream = new MemoryStream(packagedReturnToMenuRecord.Payload ?? Array.Empty<byte>(), false))
            using (EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian)) {
                Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(1, reader.ReadInt32());
                Assert.Equal((byte)0, reader.ReadByte());
            }

            using (MemoryStream payloadStream = new MemoryStream(packagedTowerSpinRecord.Payload ?? Array.Empty<byte>(), false))
            using (EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian)) {
                Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(3, reader.ReadInt32());
            }
        }

        /// <summary>
        /// Ensures packaged boot scenes rewrite scene-map helper payloads into the strict runtime format consumed by player builds.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsSceneMapComponent_WritesRuntimeSceneMapPayload() {
            string sceneId = "Scenes/StartupScene.helen";
            SceneMapComponent sceneMapComponent = new SceneMapComponent {
                InitialSceneId = "MainMenuScene"
            };
            sceneMapComponent.Mappings.Add("MainMenuScene", "AlternateMainMenuScene");
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "StartupSceneRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            descriptor.SerializeComponent(sceneMapComponent, 0, null)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);
            SceneComponentAssetRecord packagedRecord = Assert.Single(packagedRoot.Components);
            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMapComponent)), packagedRecord.ComponentTypeId);

            AutomaticScriptComponentRuntimeDeserializer deserializer = new AutomaticScriptComponentRuntimeDeserializer(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMapComponent)),
                typeof(SceneMapComponent));
            SceneMapComponent packagedComponent = Assert.IsType<SceneMapComponent>(deserializer.Deserialize(packagedRecord, null));
            Assert.Equal("MainMenuScene", packagedComponent.InitialSceneId);
            Assert.Equal("AlternateMainMenuScene", packagedComponent.Mappings["MainMenuScene"]);
        }

        /// <summary>
        /// Ensures Windows scene packaging copies authored animation clips referenced by automatic scripted components and rewrites the packaged payload so runtime deserialization resolves the clip.
        /// </summary>
        [Fact]
        public void Package_WhenAutomaticScriptComponentUsesAnimationClipAsset_CopiesClipAndResolvesRuntimeReference() {
            string sceneId = "Scenes/AnimationClipScene.helen";
            string clipRelativePath = "Animations/TestLogoIdle.hanim";
            WriteAnimationClipAsset(clipRelativePath);

            SceneComponentAssetRecord componentRecord = CreateAnimationClipScriptComponentRecord(clipRelativePath);
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = [
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "AnimationRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [componentRecord],
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                ],
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestAnimationClipAssetScriptComponent)));

            packager.Package([sceneId], BuildRootPath);

            string packagedClipPath = Path.Combine(BuildRootPath, "cooked", "Animations", "TestLogoIdle.hanim");
            Assert.True(File.Exists(packagedClipPath));

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);
            AutomaticScriptComponentRuntimeDeserializer deserializer = new AutomaticScriptComponentRuntimeDeserializer(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestAnimationClipAssetScriptComponent)),
                typeof(TestAnimationClipAssetScriptComponent));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            TestAnimationClipAssetScriptComponent packagedComponent = Assert.IsType<TestAnimationClipAssetScriptComponent>(
                deserializer.Deserialize(packagedRecord, resolver));

            Assert.NotNull(packagedComponent.IdleClip);
            Assert.Equal(0.75f, packagedComponent.IdleClip.Duration);
            Assert.Equal("logo-idle", packagedComponent.Label);
        }

        /// <summary>
        /// Loads the editor host's default importer registrations so the repro test matches the real Windows build path.
        /// </summary>
        /// <returns>Importer registrations used by the editor host.</returns>
        static IReadOnlyList<IAssetImporterRegistration> LoadEditorHostImporters() {
            string appAssemblyPath = @"C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll";
            Assembly appAssembly = Assembly.LoadFrom(appAssemblyPath);
            Type importerFactoryType = appAssembly.GetType("helengine.editor.app.EditorHostImporterFactory", throwOnError: true);
            MethodInfo createDefaultMethod = importerFactoryType.GetMethod(
                "CreateDefault",
                BindingFlags.Public | BindingFlags.Static);
            if (createDefaultMethod == null) {
                throw new InvalidOperationException("Editor host importer factory did not expose its default importer set.");
            }

            object result = createDefaultMethod.Invoke(null, null);
            return Assert.IsAssignableFrom<IReadOnlyList<IAssetImporterRegistration>>(result);
        }

        /// <summary>
        /// Resolves the packaged scene file path for one authored scene inside the supplied build output root.
        /// </summary>
        /// <param name="buildRootPath">Build output root that contains packaged scene assets.</param>
        /// <param name="sceneId">Authored scene id whose packaged output should be resolved.</param>
        /// <returns>Absolute packaged scene file path for the authored scene.</returns>
        static string GetPackagedScenePath(string buildRootPath, string sceneId) {
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return Path.Combine(buildRootPath, GetPackagedSceneRelativePath(sceneId).Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Resolves the packaged scene relative path for one authored scene.
        /// </summary>
        /// <param name="sceneId">Authored scene id whose packaged relative path should be resolved.</param>
        /// <returns>Packaged scene relative path that matches the canonical authored name.</returns>
        static string GetPackagedSceneRelativePath(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return PackagedScenePathResolver.BuildRelativePath(sceneId, 0);
        }

        /// <summary>
        /// Ensures a packaged scene referencing one material reports one deduplicated shader id.
        /// </summary>
        [Fact]
        public void Package_WhenTwoMeshesReferenceTheSameShader_ReportsOneReferencedShaderId() {
            string sceneId = "Scenes/TestScene.helen";
            string materialRelativePath = "Materials/TestMaterial.hasset";
            string shaderAssetId = "ForwardStandardShader";

            WriteShaderCachePackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            WriteMaterialAsset(materialRelativePath, shaderAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.Equal(new[] { shaderAssetId }, result.ReferencedShaderAssetIds);
        }

        /// <summary>
        /// Ensures packaging rejects source-oriented texture ids that are not backed by imported cache assets.
        /// </summary>
        [Fact]
        public void Package_WhenImportedModelCompanionMaterialUsesSourceTextureIdWithoutCachedAsset_PackagesUsingSourceTexture() {
            string sceneId = "Scenes/ImportedModelScene.helen";
            string materialRelativePath = "Models/Riemers/racer/x3ds_mat_ruedas.hasset";
            string sourceModelRelativePath = "Models/Riemers/racer.x";
            string sourceTextureRelativePath = "Models/Riemers/ruedas.jpg";

            WriteMaterialAsset(materialRelativePath, "ForwardStandardShader", "RUEDAS.JPG");
            WriteSourceTextureAsset(sourceModelRelativePath);
            WriteSourceTextureAsset(sourceTextureRelativePath);
            WriteSceneAsset(sceneId, materialRelativePath);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                [
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".jpg" }),
                    new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".x" })
                ]);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedTexturePath = Path.Combine(BuildRootPath, "cooked", "imported", "RUEDAS.JPG");
            Assert.True(File.Exists(cookedTexturePath));
        }

        /// <summary>
        /// Ensures mesh components that reference multiple materials are rewritten into packaged runtime payloads that preserve every material slot.
        /// </summary>
        [Fact]
        public void Package_WhenMeshUsesMultipleMaterials_RewritesEveryMaterialReference() {
            string sceneId = "Scenes/MultiMaterialScene.helen";
            string firstMaterialRelativePath = "Materials/SponzaWalls.hasset";
            string secondMaterialRelativePath = "Materials/SponzaTrim.hasset";
            string shaderAssetId = "ForwardStandardShader";

            WriteShaderCachePackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            WriteMaterialAsset(firstMaterialRelativePath, shaderAssetId);
            WriteMaterialAsset(secondMaterialRelativePath, shaderAssetId);
            WriteSceneAssetWithTaggedMultiMaterialMesh(sceneId, firstMaterialRelativePath, secondMaterialRelativePath);

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(
                [
                    new PlatformComponentSupportRule(
                        "helengine.MeshComponent",
                        PlatformComponentSupportKind.Transform,
                        "Mesh components must be rewritten into packaged runtime payloads.",
                        string.Empty)
                ]);
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestDirectionalShadowTowerSpinComponent)));
            packager.Package(new[] { sceneId }, BuildRootPath);

            SceneAsset packagedSceneAsset;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset packagedRoot = Assert.Single(packagedSceneAsset.RootEntities);
            SceneComponentAssetRecord meshRecord = Assert.Single(
                packagedRoot.Components,
                component => string.Equals(
                    component.ComponentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(MeshComponent)),
                    StringComparison.Ordinal));

            AssertUsesAutomaticRuntimePayload(meshRecord, typeof(MeshComponent));
            AssertAutomaticRuntimeAssetReference(meshRecord, "Materials[0]", "cooked/materials/sponzawalls.hasset");
            AssertAutomaticRuntimeAssetReference(meshRecord, "Materials[1]", "cooked/materials/sponzatrim.hasset");
        }

        /// <summary>
        /// Ensures packaging preserves effective material settings when one platform override omits schema and field values.
        /// </summary>
        [Fact]
        public void Package_WhenMaterialSidecarHasNoSchema_PreservesTopLevelMaterialShaderFields() {
            string materialRelativePath = "Materials/TestMaterial.hasset";
            string shaderAssetId = "ForwardStandardShader";
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));

            WriteCityStyleStandardMaterialAsset(materialRelativePath);
            WriteInvalidMaterialSettings(materialRelativePath);

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            Assert.True(settingsService.TryLoadPlatformSettings(materialPath, "windows", out MaterialAssetProcessorSettings settings));
            ShaderMaterialAsset materialAsset = settingsService.LoadMaterialAsset(materialPath, "windows");

            MethodInfo validationMethod = typeof(EditorPlatformBuildScenePackager).GetMethod(
                "HasValidPlatformMaterialSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(validationMethod);
            bool isValid = Assert.IsType<bool>(validationMethod.Invoke(null, [settings]));

            if (isValid) {
                settingsService.ApplyPlatformMaterialFields(materialAsset, settings);
            }

            Assert.True(isValid);
            Assert.Equal(shaderAssetId, materialAsset.ShaderAssetId);
            Assert.Equal(shaderAssetId + ".vs", materialAsset.VertexProgram);
            Assert.Equal(shaderAssetId + ".ps", materialAsset.PixelProgram);
            Assert.Equal("default", materialAsset.Variant);
        }

        /// <summary>
        /// Ensures packaging rebuilds a corrupt material settings sidecar instead of failing on deserialize.
        /// </summary>
        [Fact]
        public void Package_WhenMaterialSettingsSidecarIsCorrupt_RebuildsSettingsAndPackagesScene() {
            string sceneId = "Scenes/MaterialScene.helen";
            string materialRelativePath = "Materials/rendering/colored_cube_grid/Cube00.hasset";

            WriteCityStyleStandardMaterialAsset(materialRelativePath);
            WriteCorruptMaterialSettingsOverride(materialRelativePath, "windows");
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(CreateWindowsMaterialBuilderDefinition());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                CreateWindowsMaterialBuilderDefinition(),
                defaultFont,
                materialBuilder,
                "debug",
                "directx11");

            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.NotNull(materialBuilder.LastMaterialCookRequest);
            string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "materials", "rendering", "colored_cube_grid", "Cube00.hasset");
            Assert.True(File.Exists(cookedMaterialPath));

            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            Assert.True(settingsService.TryLoadPlatformSettings(materialPath, "windows", out MaterialAssetProcessorSettings settings));
            Assert.Equal("standard-shader", settings.SchemaId);
        }

        /// <summary>
        /// Ensures custom shader mode stays opt-in while the packager still supplies the standard shader defaults and packaged standard-shader variant.
        /// </summary>
        [Fact]
        public void Package_WhenCustomShaderIsDisabled_UsesMeshVariantAndStandardShaderDefaults() {
            string sceneId = "Scenes/TestScene.helen";
            string materialRelativePath = "Materials/TestMaterial.hasset";
            string shaderAssetId = "ForwardStandardShader";

            WriteShaderCachePackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            WriteBlankMaterialAsset(materialRelativePath);
            WriteSceneAsset(sceneId, materialRelativePath);

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(CreateWindowsMaterialBuilderDefinition());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "windows",
                materialBuilder,
                "debug",
                "directx11");

            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.NotNull(materialBuilder.LastMaterialCookRequest);
            Assert.Equal("false", materialBuilder.LastMaterialCookRequest.FieldValues["use-custom-shader"]);
            Assert.Equal("default", materialBuilder.LastMaterialCookRequest.FieldValues["variant"]);
            Assert.Equal(shaderAssetId, materialBuilder.LastMaterialCookRequest.FieldValues["shader-asset-id"]);
            Assert.Equal(shaderAssetId + ".vs", materialBuilder.LastMaterialCookRequest.FieldValues["vertex-program"]);
            Assert.Equal(shaderAssetId + ".ps", materialBuilder.LastMaterialCookRequest.FieldValues["pixel-program"]);
            Assert.Equal(string.Empty, materialBuilder.LastMaterialCookRequest.FieldValues["texture-id"]);
            Assert.Equal("true", materialBuilder.LastMaterialCookRequest.FieldValues["casts-shadow"]);
            Assert.Equal("true", materialBuilder.LastMaterialCookRequest.FieldValues["receives-shadow"]);
        }

        /// <summary>
        /// Ensures generated standard materials are cooked through the active material builder instead of being written as raw desktop material assets.
        /// </summary>
        [Fact]
        public void Package_WhenSceneReferencesGeneratedStandardMaterial_CooksOpaquePlatformMaterialBytes() {
            string sceneId = "Scenes/GeneratedStandardMaterialScene.helen";
            WriteSceneAsset(sceneId, CreateGeneratedStandardMaterialReference());
            byte[] cookedBytes = [0x50, 0x53, 0x32, 0x4D, 0x41, 0x54];

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreatePs2MaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    cookedBytes,
                    Array.Empty<string>()));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "ps2",
                materialBuilder,
                "debug",
                "ps2-standard-forward");

            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.NotNull(materialBuilder.LastMaterialCookRequest);
            Assert.Equal("ps2-unlit-textured", materialBuilder.LastMaterialCookRequest.SchemaId);
            Assert.Equal("opaque", materialBuilder.LastMaterialCookRequest.FieldValues["alpha-mode"]);
            Assert.Equal("false", materialBuilder.LastMaterialCookRequest.FieldValues["double-sided"]);
            Assert.Equal("multiply", materialBuilder.LastMaterialCookRequest.FieldValues["vertex-color-mode"]);

            string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "engine", "materials", "standard.hasset");
            Assert.Equal(cookedBytes, File.ReadAllBytes(cookedMaterialPath));
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "cooked", "shaders", "ForwardStandardShader.dx11.hasset")));
        }

        /// <summary>
        /// Ensures PS2 builder-backed material cook requests translate imported `texture-id` values into cooked runtime `texture-relative-path` values.
        /// </summary>
        [Fact]
        public void Package_WhenPs2BuilderCooksMaterialWithImportedDiffuseTexture_PopulatesTextureRelativePath() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";

            WriteCachedTextureAsset(textureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            MaterialAssetSettingsService materialSettingsService = new MaterialAssetSettingsService();
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ShaderMaterialAsset loadedMaterialAsset = materialSettingsService.LoadMaterialAsset(materialPath, "ps2");
            Assert.Equal(textureAssetId, loadedMaterialAsset.DiffuseTextureAssetId);

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreatePs2MaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    [0x50, 0x53, 0x32, 0x54, 0x45, 0x58],
                    Array.Empty<string>()));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "ps2",
                materialBuilder,
                "debug",
                "ps2-standard-forward");

            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.True(materialSettingsService.TryLoadPlatformSettings(materialPath, "ps2", out MaterialAssetProcessorSettings ps2Settings));
            Assert.Equal(
                "cooked/imported/" + textureAssetId,
                ps2Settings.FieldValues["texture-relative-path"]);
            PlatformMaterialCookRequest ps2CookRequest = materialBuilder.MaterialCookRequests.First(request => string.Equals(request.MaterialRelativePath, materialRelativePath, StringComparison.Ordinal));
            Assert.Equal(
                "cooked/imported/" + textureAssetId,
                ps2CookRequest.FieldValues["texture-relative-path"]);
        }

        /// <summary>
        /// Ensures generated standard materials are cooked through the active DS material builder instead of writing a DX11 shader-backed material payload.
        /// </summary>
        [Fact]
        public void Package_WhenSceneReferencesGeneratedStandardMaterial_CooksDsPlatformMaterialAsset() {
            string sceneId = "Scenes/GeneratedStandardMaterialScene.helen";
            WriteSceneAsset(sceneId, CreateGeneratedStandardMaterialReference());

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreateDsMaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    AssetSerializer.SerializeToBytes(new PlatformMaterialAsset {
                        RendererFamilyId = "ds-main-2d",
                        TextureRelativePath = string.Empty,
                        DoubleSided = false,
                        UseVertexColor = true,
                        Lit = true,
                        BaseColorR = 255,
                        BaseColorG = 255,
                        BaseColorB = 255,
                        BaseColorA = 255
                    }),
                    Array.Empty<string>()));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "ds",
                materialBuilder,
                "ds-default",
                "ds-main-2d");

            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.NotNull(materialBuilder.LastMaterialCookRequest);
            Assert.Equal("ds-standard-textured", materialBuilder.LastMaterialCookRequest.SchemaId);

            string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "engine", "materials", "standard.hasset");
            using FileStream stream = File.OpenRead(cookedMaterialPath);
            PlatformMaterialAsset cookedMaterial = Assert.IsType<PlatformMaterialAsset>(AssetSerializer.Deserialize(stream));

            Assert.Equal("ds-main-2d", cookedMaterial.RendererFamilyId);
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "cooked", "shaders", "ForwardStandardShader.dx11.hasset")));
        }

        /// <summary>
        /// Ensures DS builder-backed material cook requests translate imported `texture-id` values into cooked runtime `texture-relative-path` values.
        /// </summary>
        [Fact]
        public void Package_WhenDsBuilderCooksMaterialWithImportedDiffuseTexture_PopulatesTextureRelativePath() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";
            string expectedTextureRelativePath = $"cooked/imported/{RuntimeAssetIdGenerator.Generate(textureAssetId):x16}.hetex";

            WriteCachedTextureAsset(textureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            MaterialAssetSettingsService materialSettingsService = new MaterialAssetSettingsService();
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ShaderMaterialAsset loadedMaterialAsset = materialSettingsService.LoadMaterialAsset(materialPath, "ds");
            Assert.Equal(textureAssetId, loadedMaterialAsset.DiffuseTextureAssetId);

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreateDsMaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    AssetSerializer.SerializeToBytes(new PlatformMaterialAsset {
                        RendererFamilyId = "ds-main-2d",
                        TextureRelativePath = request.FieldValues["texture-relative-path"],
                        DoubleSided = false,
                        UseVertexColor = true,
                        Lit = true,
                        BaseColorR = 255,
                        BaseColorG = 255,
                        BaseColorB = 255,
                        BaseColorA = 255
                    }),
                    Array.Empty<string>()));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "ds",
                materialBuilder,
                "ds-default",
                "ds-main-2d");

            packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformMaterialCookRequest dsCookRequest = materialBuilder.MaterialCookRequests.First(request => string.Equals(request.MaterialRelativePath, materialRelativePath, StringComparison.Ordinal));
            Assert.Equal("ds-standard-textured", dsCookRequest.SchemaId);
            Assert.Equal(expectedTextureRelativePath, dsCookRequest.FieldValues["texture-relative-path"]);

            string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "materials", "rendering", "textured_cube_grid", "Cube00.hasset");
            using FileStream cookedMaterialStream = File.OpenRead(cookedMaterialPath);
            PlatformMaterialAsset cookedMaterial = Assert.IsType<PlatformMaterialAsset>(AssetSerializer.Deserialize(cookedMaterialStream));
            Assert.Equal(expectedTextureRelativePath, cookedMaterial.TextureRelativePath);
        }

        /// <summary>
        /// Ensures DS builder-backed material cook requests rewrite stale legacy imported texture paths to the current `.hetex` runtime contract before cooking.
        /// </summary>
        [Fact]
        public void Package_WhenDsBuilderMaterialSettingsContainLegacyImportedTexturePath_NormalizesTextureRelativePath() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";
            string expectedTextureRelativePath = $"cooked/imported/{RuntimeAssetIdGenerator.Generate(textureAssetId):x16}.hetex";

            WriteCachedTextureAsset(textureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            MaterialAssetSettingsService materialSettingsService = new MaterialAssetSettingsService();
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(materialSettingsService.TryLoadPlatformSettings(materialPath, "windows", out MaterialAssetProcessorSettings windowsSettings));
            Assert.True(materialSettingsService.TryLoad(materialPath, out MaterialAssetImportSettings savedSettings));
            savedSettings.Processor.Platforms["ds"] = new MaterialAssetProcessorSettings {
                SchemaId = "ds-standard-textured",
                FieldValues = new Dictionary<string, string>(windowsSettings.FieldValues, StringComparer.OrdinalIgnoreCase) {
                    ["texture-relative-path"] = "cooked/imported/" + textureAssetId
                }
            };
            materialSettingsService.Save(materialPath, savedSettings);

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreateDsMaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    AssetSerializer.SerializeToBytes(new PlatformMaterialAsset {
                        RendererFamilyId = "ds-main-2d",
                        TextureRelativePath = request.FieldValues["texture-relative-path"],
                        DoubleSided = false,
                        UseVertexColor = true,
                        Lit = true,
                        BaseColorR = 255,
                        BaseColorG = 255,
                        BaseColorB = 255,
                        BaseColorA = 255
                    }),
                    Array.Empty<string>()));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "ds",
                materialBuilder,
                "ds-default",
                "ds-main-2d");

            packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformMaterialCookRequest dsCookRequest = materialBuilder.MaterialCookRequests.First(request => string.Equals(request.MaterialRelativePath, materialRelativePath, StringComparison.Ordinal));
            Assert.Equal(expectedTextureRelativePath, dsCookRequest.FieldValues["texture-relative-path"]);
        }

        /// <summary>
        /// Ensures DS builder-owned imported textures use one DS-safe cooked runtime path while preserving the full source asset id in work-item metadata.
        /// </summary>
        [Fact]
        public void Package_WhenDsPlatformOwnsImportedTextureCooking_UsesDsSafeCookedRuntimeTexturePath() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string textureRelativePath = "Images/Menu/helengine-logo.png";
            string textureAssetId = WriteSourceTextureAssetAndReturnAssetId(textureRelativePath, ".png", "ds");
            string expectedOutputRelativePath = $"cooked/imported/{RuntimeAssetIdGenerator.Generate(textureAssetId):x16}.hetex";

            WriteSceneAsset(sceneId, "helengine.SpriteComponent", WriteSpriteComponentPayload(SceneAssetReferenceTestFactory.CreateFileSystemTexture(textureRelativePath)));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                [
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"])
                ],
                CreateDsBuilderOwnedTexturePlatformDefinition(),
                CreatePackagedFontAsset());
            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformCookWorkItem workItem = Assert.Single(result.PlatformCookWorkItems);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Equal("runtime-texture", workItem.TargetArtifactKind);
            Assert.Equal(expectedOutputRelativePath, workItem.OutputRelativePath);
            Assert.Contains(
                workItem.Metadata,
                entry => string.Equals(entry.Key, "source-asset-id", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.Value, textureAssetId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures packaged scenes preserve FPS overlay components for the player runtime loader.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsFpsOverlay_LeavesPackagedComponentLoadable() {
            string sceneId = "Scenes/FpsScene.helen";

            WriteSceneAsset(sceneId, "helengine.FPSComponent", WriteFpsComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedRecord = packagedScene.RootEntities[0].Components[0];
            AssertUsesAutomaticRuntimePayload(packagedRecord, typeof(FPSComponent));
            AssertAutomaticRuntimeAssetReference(packagedRecord, "Font", "cooked/fonts/default.hefont");
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/fonts/default.hefont", packagedScene.AssetReferences[0].RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont")));
        }

        /// <summary>
        /// Ensures older FPS component payloads are rejected during Windows scene packaging.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsOlderVersionFpsOverlay_ThrowsUnsupportedPayloadVersion() {
            string sceneId = "Scenes/OlderVersionFpsScene.helen";

            WriteSceneAsset(sceneId, "helengine.FPSComponent", WriteOlderVersionFpsComponentPayload(), Array.Empty<SceneAssetReference>());

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));
            Assert.Contains("Unsupported automatic scripted component payload version", exception.Message);
        }

        /// <summary>
        /// Ensures packaged scenes preserve debug overlay components for the player runtime loader.
        /// </summary>
        [Fact]
        public void PackageBuild_WhenSceneContainsDebugComponent_RewritesRuntimePayloadAndFontReference() {
            string sceneId = "Scenes/DebugScene.helen";

            WriteSceneAsset(sceneId, "helengine.DebugComponent", WriteDebugComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord componentRecord = packagedScene.RootEntities[0].Components[0];
            AssertUsesAutomaticRuntimePayload(componentRecord, typeof(DebugComponent));
            AssertAutomaticRuntimeAssetReference(componentRecord, "Font", "cooked/fonts/default.hefont");
        }

        /// <summary>
        /// Ensures packaged scenes reject the removed generated Nintendo DS debug-font reference.
        /// </summary>
        [Fact]
        public void PackageBuild_WhenSceneContainsRemovedNintendoDsGeneratedFont_ThrowsUnsupportedGeneratedReference() {
            string sceneId = "Scenes/DebugScene.helen";

            WriteSceneAsset(sceneId, "helengine.DebugComponent", WriteDebugComponentPayload(CreateNintendoDsDebugFontReference()), new[] { CreateNintendoDsDebugFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));
            Assert.Contains("Unsupported generated", exception.Message);
        }

        /// <summary>
        /// Ensures packaged scenes rewrite text component font references into file-backed assets.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsTextComponent_LeavesPackagedComponentLoadable() {
            string sceneId = "Scenes/TextScene.helen";

            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedTextRecord = packagedScene.RootEntities[0].Components[0];
            AssertUsesAutomaticRuntimePayload(packagedTextRecord, typeof(TextComponent));
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/fonts/default.hefont", packagedScene.AssetReferences[0].RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont")));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            TextComponent loadedTextComponent = Assert.IsType<TextComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is TextComponent));
            System.Reflection.PropertyInfo alignmentProperty = typeof(TextComponent).GetProperty("Alignment");
            Assert.NotNull(alignmentProperty);

            Assert.Equal("Hello world", loadedTextComponent.Text);
            Assert.NotNull(loadedTextComponent.Font);
            Assert.Equal(defaultFont.FontInfo.Name, loadedTextComponent.Font.FontInfo.Name);
            Assert.Equal(2f, loadedTextComponent.FontScale);
            Assert.Equal("Center", alignmentProperty.GetValue(loadedTextComponent)?.ToString());
        }

        /// <summary>
        /// Ensures flagged text components remain runtime text components in the packaged runtime scene.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsFlaggedTextComponent_WritesTextComponentIntoPackagedScene() {
            string sceneId = "Scenes/BakedTextScene.helen";
            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(CreateEditorFontReference(), true), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont,
                new StubTextComponentSpriteBakeService());
            packager.Package(new[] { sceneId }, BuildRootPath);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal("helengine.TextComponent", packagedScene.RootEntities[0].Components[0].ComponentTypeId);
        }

        /// <summary>
        /// Ensures flagged text components keep the packaged font reference and do not produce one packaged generated texture.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsFlaggedTextComponent_KeepsPackagedFontReferenceWithoutWritingGeneratedTexture() {
            string sceneId = "Scenes/BakedTextScene.helen";
            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(CreateEditorFontReference(), true), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont,
                new StubTextComponentSpriteBakeService());
            packager.Package(new[] { sceneId }, BuildRootPath);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord textRecord = packagedScene.RootEntities[0].Components[0];
            AssertUsesAutomaticRuntimePayload(textRecord, typeof(TextComponent));
            AssertAutomaticRuntimeAssetReference(textRecord, "Font", "cooked/fonts/default.hefont");
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "generated", "text-sprites")));
        }

        /// <summary>
        /// Ensures shared scene packaging emits canonical runtime font paths directly inside both packaged scene references and the automatic text payload.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformUsesCanonicalPackagedPaths_TextComponentFontReferenceUsesCanonicalRuntimePath() {
            string sceneId = "Scenes/TextScene.helen";
            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            PlatformDefinition platformDefinition = CreateCanonicalPathPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                defaultFont);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string expectedFontRuntimePath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
                platformDefinition.PlatformId,
                platformDefinition.RuntimeGenerationContract,
                "cooked/fonts/default.hefont");
            using FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

            Assert.Equal(expectedFontRuntimePath, Assert.Single(packagedScene.AssetReferences).RelativePath);

            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);
            AssertUsesAutomaticRuntimePayload(packagedRecord, typeof(TextComponent));
            AssertAutomaticRuntimeAssetReference(packagedRecord, "Font", expectedFontRuntimePath);
        }

        /// <summary>
        /// Ensures shared packaging preserves the generated model artifact path instead of rewriting it to a platform-specific extension.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformPackagesGeneratedCube_ModelReferenceUsesSharedRuntimeModelPath() {
            string sceneId = "Scenes/GeneratedPrimitiveScene.helen";

            WriteSceneAsset(
                sceneId,
                CreateGeneratedCubeReference(),
                CreateGeneratedStandardMaterialReference());

            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
            FontAsset defaultFont = CreatePackagedFontAsset();
            PlatformDefinition platformDefinition = CreateCanonicalPathPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                defaultFont);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string expectedModelRuntimePath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
                platformDefinition.PlatformId,
                platformDefinition.RuntimeGenerationContract,
                "cooked/engine/models/cube.hasset");
            using FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            for (int entityIndex = 0; entityIndex < packagedScene.RootEntities.Length; entityIndex++) {
                SceneComponentAssetRecord packagedMeshRecord = Assert.Single(
                    packagedScene.RootEntities[entityIndex].Components,
                    component => string.Equals(
                        component.ComponentTypeId,
                        AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(MeshComponent)),
                        StringComparison.Ordinal));

                AssertUsesAutomaticRuntimePayload(packagedMeshRecord, typeof(MeshComponent));
                AssertAutomaticRuntimeAssetReference(packagedMeshRecord, "Model", expectedModelRuntimePath);
            }
        }

        /// <summary>
        /// Ensures file-backed material references in packaged mesh payloads are rewritten to the cooked player-material location.
        /// </summary>
        [Fact]
        public void Package_WhenSceneReferencesFileSystemMaterial_LeavesPackagedRuntimeLoadable() {
            string sceneId = "Scenes/MaterialScene.helen";
            string materialRelativePath = "Materials/rendering/colored_cube_grid/Cube00.hasset";
            string shaderAssetId = "ForwardStandardShader";

            WriteShaderCachePackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            WriteCityStyleStandardMaterialAsset(materialRelativePath);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "materials", "rendering", "colored_cube_grid", "Cube00.hasset");
            Assert.True(File.Exists(cookedMaterialPath));
            using (FileStream cookedMaterialStream = File.OpenRead(cookedMaterialPath)) {
                ShaderMaterialAsset cookedMaterial = Assert.IsType<ShaderMaterialAsset>(AssetSerializer.Deserialize(cookedMaterialStream));
                Assert.Equal("default", cookedMaterial.Variant);
            }

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            Assert.NotNull(Assert.Single(firstMeshComponent.Materials));
        }

        /// <summary>
        /// Ensures packaged generated cube and standard-material references keep their generated identity so the player runtime can share them across scene entities.
        /// </summary>
        [Fact]
        public void Package_WhenSceneReferencesGeneratedCubeAndStandardMaterial_PreservesGeneratedIdentityForRuntimeSharedCaching() {
            string sceneId = "Scenes/GeneratedPrimitiveScene.helen";

            WriteSceneAsset(
                sceneId,
                CreateGeneratedCubeReference(),
                CreateGeneratedStandardMaterialReference());

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            MeshComponent secondMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is MeshComponent));

            Assert.Same(firstMeshComponent.Model, secondMeshComponent.Model);
            Assert.Same(Assert.Single(firstMeshComponent.Materials), Assert.Single(secondMeshComponent.Materials));
        }

        /// <summary>
        /// Ensures packaged runtime scene loading registers generic mesh payloads into the loaded camera's 3D render queue.
        /// </summary>
        [Fact]
        public void Package_WhenPackagedSceneContainsCameraAndGenericMesh_RegistersTheMeshInRenderQueue3D() {
            string sceneId = "Scenes/CameraMeshScene.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "CameraRoot",
                        LocalPosition = new float3(0f, 0f, 5f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateCameraComponentRecord()
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "MeshRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(
                                    CreateGeneratedCubeReference(),
                                    CreateGeneratedStandardMaterialReference())
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is CameraComponent));
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is MeshComponent));

            Assert.Contains(meshComponent, Core.Instance.ObjectManager.Drawables3D);
            Assert.Equal(1, cameraComponent.RenderQueue3D.Count);
        }

        /// <summary>
        /// Ensures packaged scene-object entity masks are normalized into the same runtime layer as packaged cameras so loaded player scenes populate 3D camera queues.
        /// </summary>
        [Fact]
        public void Package_WhenPackagedSceneEntityUsesSceneObjectLayerMask_RegistersTheMeshInRenderQueue3D() {
            string sceneId = "Scenes/SceneObjectLayerCameraMeshScene.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            InitializeRuntimeCore(BuildRootPath);

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "CameraRoot",
                        LayerMask = EditorLayerMasks.SceneObjects,
                        LocalPosition = new float3(0f, 0f, 5f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateCameraComponentRecord()
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "MeshRoot",
                        LayerMask = EditorLayerMasks.SceneObjects,
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(
                                    CreateGeneratedCubeReference(),
                                    CreateGeneratedStandardMaterialReference())
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.All(packagedScene.RootEntities, entityAsset => Assert.Equal((ushort)1, entityAsset.LayerMask));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is CameraComponent));
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is MeshComponent));

            Assert.Equal((ushort)1, loadedRoots[0].LayerMask);
            Assert.Equal((ushort)1, loadedRoots[1].LayerMask);
            Assert.Contains(meshComponent, Core.Instance.ObjectManager.Drawables3D);
            Assert.Equal(1, cameraComponent.RenderQueue3D.Count);
        }

        /// <summary>
        /// Ensures a project-style standard-shader material packages with a shader contract the player can resolve.
        /// </summary>
        [Fact]
        public void Package_WhenStandardShaderMaterialUsesMirroredFieldSidecar_WritesPlayerResolvableShaderContract() {
            string sceneId = "Scenes/MaterialScene.helen";
            string materialRelativePath = "Materials/rendering/colored_cube_grid/Cube00.hasset";

            WriteCityStyleStandardMaterialAsset(materialRelativePath);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "materials", "rendering", "colored_cube_grid", "Cube00.hasset");
            using FileStream cookedMaterialStream = File.OpenRead(cookedMaterialPath);
            ShaderMaterialAsset cookedMaterial = Assert.IsType<ShaderMaterialAsset>(AssetSerializer.Deserialize(cookedMaterialStream));

            Assert.Equal("ForwardStandardShader", cookedMaterial.ShaderAssetId);
            Assert.Equal("ForwardStandardShader.vs", cookedMaterial.VertexProgram);
            Assert.Equal("ForwardStandardShader.ps", cookedMaterial.PixelProgram);
            Assert.Equal("default", cookedMaterial.Variant);
        }

        /// <summary>
        /// Ensures packaged standard-shader materials copy imported diffuse textures into the player-visible cooked texture location.
        /// </summary>
        [Fact]
        public void Package_WhenMaterialUsesImportedDiffuseTexture_WritesCookedImportedTextureAndLoadsRuntimeMaterial() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";

            WriteCachedTextureAsset(textureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedTexturePath = Path.Combine(BuildRootPath, "cooked", "imported", textureAssetId);
            Assert.True(File.Exists(cookedTexturePath));

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            ShaderRuntimeMaterial runtimeMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(firstMeshComponent.Materials));
            RuntimeTexture resolvedTexture = runtimeMaterial.ResolveTexture();
            Assert.NotNull(resolvedTexture);
        }

        /// <summary>
        /// Ensures builder-backed Windows material cooking still copies imported diffuse textures into the player-visible cooked texture location and preserves the imported runtime texture binding.
        /// </summary>
        [Fact]
        public void Package_WhenBuilderCooksMaterialWithImportedDiffuseTexture_WritesCookedImportedTextureAndLoadsRuntimeMaterial() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";

            WriteCachedTextureAsset(textureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            TestPlatformMaterialAssetBuilder materialBuilder = new TestPlatformMaterialAssetBuilder();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                materialBuilder.Definition,
                defaultFont,
                materialBuilder,
                "debug",
                "directx11");
            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedTexturePath = Path.Combine(BuildRootPath, "cooked", "imported", textureAssetId);
            Assert.True(File.Exists(cookedTexturePath));

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            ShaderRuntimeMaterial runtimeMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(firstMeshComponent.Materials));
            RuntimeTexture resolvedTexture = runtimeMaterial.ResolveTexture();
            Assert.NotNull(resolvedTexture);
        }

        /// <summary>
        /// Ensures builder-backed Windows material cooking copies both imported diffuse and roughness textures into the player-visible cooked texture location and preserves both runtime texture bindings.
        /// </summary>
        [Fact]
        public void Package_WhenBuilderCooksMaterialWithImportedDiffuseAndRoughnessTextures_WritesBothCookedImportedTexturesAndLoadsRuntimeMaterial() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string diffuseTextureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";
            string roughnessTextureAssetId = "fc80c802f5ddae81b5d19fb2e540eea910d5dd2f879bf672f557f5ad4f75bdb1";

            WriteCachedTextureAsset(diffuseTextureAssetId);
            WriteCachedTextureAsset(roughnessTextureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, diffuseTextureAssetId, roughnessTextureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            TestPlatformMaterialAssetBuilder materialBuilder = new TestPlatformMaterialAssetBuilder();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                materialBuilder.Definition,
                defaultFont,
                materialBuilder,
                "debug",
                "directx11");
            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "imported", diffuseTextureAssetId)));
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "imported", roughnessTextureAssetId)));

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            ShaderRuntimeMaterial runtimeMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(firstMeshComponent.Materials));
            int diffuseBindingIndex = runtimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            int roughnessBindingIndex = runtimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.RoughnessTextureBindingName);

            Assert.NotNull(runtimeMaterial.Properties.GetTexture(diffuseBindingIndex));
            Assert.NotNull(runtimeMaterial.Properties.GetTexture(roughnessBindingIndex));
        }

        /// <summary>
        /// Ensures builder-backed Windows material cooking copies imported emissive textures into the player-visible cooked texture location and preserves the runtime texture binding.
        /// </summary>
        [Fact]
        public void Package_WhenBuilderCooksMaterialWithImportedEmissiveTexture_WritesCookedImportedTextureAndLoadsRuntimeMaterial() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string emissiveTextureAssetId = "65d54b79f6a769f7f067e4c1f8db1bcd52aef6e6d8d95cb40f6f8440ab1b3f60";

            WriteCachedTextureAsset(emissiveTextureAssetId);
            WriteCityStyleStandardMaterialAsset(materialRelativePath, string.Empty, string.Empty, emissiveTextureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            TestPlatformMaterialAssetBuilder materialBuilder = new TestPlatformMaterialAssetBuilder();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                materialBuilder.Definition,
                defaultFont,
                materialBuilder,
                "debug",
                "directx11");
            packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "imported", emissiveTextureAssetId)));

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            ShaderRuntimeMaterial runtimeMaterial = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(firstMeshComponent.Materials));
            int emissiveBindingIndex = runtimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.EmissiveTextureBindingName);

            Assert.NotNull(runtimeMaterial.Properties.GetTexture(emissiveBindingIndex));
        }

        /// <summary>
        /// Ensures GameCube-style builder-owned texture capabilities emit explicit platform cook work items for imported diffuse textures.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformOwnsImportedDiffuseTextureCooking_EmitsPlatformCookWorkItem() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureRelativePath = "Textures/Cube00.png";
            string textureAssetId = WriteSourceTextureAssetAndReturnAssetId(textureRelativePath, ".png", "gamecube");

            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                [
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"])
                ],
                CreateGameCubeBuilderOwnedTexturePlatformDefinition(),
                defaultFont);
            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformCookWorkItem workItem = Assert.Single(result.PlatformCookWorkItems);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Equal("runtime-texture", workItem.TargetArtifactKind);
            Assert.True(
                string.Equals(
                    Path.GetFullPath(Path.Combine(ProjectRootPath, "assets", textureRelativePath.Replace('/', Path.DirectorySeparatorChar))),
                    workItem.SourceAssetPath,
                    StringComparison.OrdinalIgnoreCase));
            Assert.Equal($"cooked/imported/{textureAssetId}", workItem.OutputRelativePath);
        }

        /// <summary>
        /// Ensures builder-owned font-atlas texture capabilities externalize imported source-font atlases and emit one work item for the atlas texture path.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformOwnsFontAtlasTextureCooking_ExternalizesImportedSourceFontAtlasAndEmitsAtlasWorkItem() {
            string sceneId = "Scenes/TextScene.helen";
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            const string defaultSerializedTextureSettings = "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}";

            WriteSourceFont(fontRelativePath);
            SceneAssetReference fontReference = CreateFileFontReference(fontRelativePath);
            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(fontReference), new[] { fontReference });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                [
                    new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"])
                ],
                CreateGameCubeBuilderOwnedFontAtlasTexturePlatformDefinition(defaultSerializedTextureSettings),
                CreatePackagedFontAsset());
            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformCookWorkItem workItem = Assert.Single(result.PlatformCookWorkItems);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Equal("runtime-texture", workItem.TargetArtifactKind);
            Assert.Equal(".hetex", Path.GetExtension(workItem.SourceAssetPath));
            Assert.Contains(Path.Combine(ProjectRootPath, "cache", "generated", "platform-fonts"), workItem.SourceAssetPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", workItem.OutputRelativePath);
            Assert.Equal(defaultSerializedTextureSettings, workItem.SerializedPlatformSettings);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures packaged fonts store their external cooked atlas path in canonical runtime form while platform cook work items stay logical.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformUsesBuilderOwnedFontAtlasTexture_FontAssetStoresCanonicalAtlasRuntimePath() {
            string sceneId = "Scenes/TextScene.helen";
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            const string defaultSerializedTextureSettings = "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}";

            WriteSourceFont(fontRelativePath);
            SceneAssetReference fontReference = CreateFileFontReference(fontRelativePath);
            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(fontReference), new[] { fontReference });

            PlatformDefinition platformDefinition = CreateBuilderOwnedFontAtlasTexturePlatformDefinition(defaultSerializedTextureSettings);
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                [
                    new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"])
                ],
                platformDefinition,
                CreatePackagedFontAsset());
            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformCookWorkItem workItem = Assert.Single(result.PlatformCookWorkItems);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", workItem.OutputRelativePath);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Equal(".hetex", Path.GetExtension(workItem.SourceAssetPath));

            string expectedFontRuntimePath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
                platformDefinition.PlatformId,
                platformDefinition.RuntimeGenerationContract,
                "cooked/fonts/demodisctitle.hefont");
            using FileStream sceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(sceneStream));
            Assert.Equal(expectedFontRuntimePath, Assert.Single(packagedScene.AssetReferences).RelativePath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            string expectedAtlasRuntimePath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
                platformDefinition.PlatformId,
                platformDefinition.RuntimeGenerationContract,
                "cooked/fonts/demodisctitle.hetex");
            Assert.Equal(expectedAtlasRuntimePath, cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures the generated editor default font follows the same builder-owned atlas externalization path as authored fonts instead of embedding raw atlas bytes into `default.hefont`.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformUsesBuilderOwnedFontAtlasTexture_ExternalizesGeneratedDefaultFontAtlas() {
            string sceneId = "Scenes/TextScene.helen";
            const string defaultSerializedTextureSettings = "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}";

            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(), new[] { CreateEditorFontReference() });
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);

            PlatformDefinition platformDefinition = CreateBuilderOwnedFontAtlasTexturePlatformDefinition(defaultSerializedTextureSettings);
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                CreatePackagedFontAsset());

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            PlatformCookWorkItem workItem = Assert.Single(result.PlatformCookWorkItems);
            Assert.Equal("texture", workItem.SourceAssetKind);
            Assert.Equal("cooked/fonts/default.hetex", workItem.OutputRelativePath);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(BuildRootPath, "generated", "editor", "fonts", "default-font-atlas.hasset")),
                workItem.SourceAssetPath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            string expectedAtlasRuntimePath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
                platformDefinition.PlatformId,
                platformDefinition.RuntimeGenerationContract,
                "cooked/fonts/default.hetex");
            Assert.Equal(expectedAtlasRuntimePath, cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures source font references are imported into cooked `.hefont` outputs and rewritten in packaged payloads.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsSourceFontReference_WritesCookedHefontAndRewritesPayload() {
            string sceneId = "Scenes/TextScene.helen";
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            WriteSourceFont(fontRelativePath);
            SceneAssetReference fontReference = CreateFileFontReference(fontRelativePath);
            WriteSceneAsset(sceneId, "helengine.TextComponent", WriteTextComponentPayload(fontReference), new[] { fontReference });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreatePackagedFontAsset());
            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            Assert.True(File.Exists(cookedFontPath));

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Contains(packagedScene.AssetReferences, reference =>
                string.Equals(reference.RelativePath, "cooked/fonts/demodisctitle.hefont", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures reflectable built-in engine components package and load through the automatic fallback when the platform omits explicit support metadata.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformOmitsSupportRulesForReflectableEngineComponent_UsesAutomaticFallback() {
            string sceneId = "Scenes/LineRendererScene.helen";
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            LineRendererComponent component = new LineRendererComponent();
            SceneComponentAssetRecord componentRecord = persistenceRegistry.GetDescriptor(component)
                .SerializeComponent(component, 0, new EntityComponentSaveState());

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "LineRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            componentRecord
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestDirectionalShadowTowerSpinComponent)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            Entity loadedRoot = Assert.Single(loadedRoots);
            Assert.IsType<LineRendererComponent>(Assert.Single(loadedRoot.Components, loadedComponent => loadedComponent is LineRendererComponent));
        }

        /// <summary>
        /// Ensures empty automatic-script payloads with no persisted members package into valid runtime ordinal payloads.
        /// </summary>
        [Fact]
        public void Package_WhenAutomaticScriptComponentHasNoPersistedMembersAndPayloadIsEmpty_WritesValidRuntimePayload() {
            string sceneId = "Scenes/LegacyEmptyAutomaticScriptScene.helen";
            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestScriptComponentWithoutPersistedMembers));

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "ScriptRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = componentTypeId,
                                ComponentIndex = 0,
                                Payload = Array.Empty<byte>()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestScriptComponentWithoutPersistedMembers)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);

            using MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(0, reader.ReadInt32());
        }

        /// <summary>
        /// Ensures empty automatic-script payloads with reflected inherited members package using default component values.
        /// </summary>
        [Fact]
        public void Package_WhenAutomaticScriptComponentUsesLegacyEmptyPayload_WritesDefaultReflectedMemberValues() {
            string sceneId = "Scenes/LegacyEmptyUpdateScriptScene.helen";
            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestUpdateOnlyScriptComponent));

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "UpdateRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = componentTypeId,
                                ComponentIndex = 0,
                                Payload = Array.Empty<byte>()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestUpdateOnlyScriptComponent)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);

            using MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal((byte)0, reader.ReadByte());
        }

        /// <summary>
        /// Ensures packaged scene-memory probe components rewrite their authored step-array payload into one runtime payload that loads back through the default runtime registry.
        /// </summary>
        [Fact]
        public void PackageBuild_WhenSceneContainsSceneMemoryProbeComponent_RewritesRuntimePayloadForStepArray() {
            string sceneId = "Scenes/SceneMemoryProbeScene.helen";
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            SceneMemoryProbeComponent component = new SceneMemoryProbeComponent {
                ProbeName = "menu-memory-probe",
                Loop = true,
                StartAutomatically = true,
                InitialDelaySeconds = 2.0d,
                Steps = new[] {
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.Wait,
                        SceneId = string.Empty,
                        DurationSeconds = 5.0d,
                        Label = "idle-menu"
                    },
                    new SceneMemoryProbeStep {
                        ActionKind = SceneMemoryProbeActionKind.LoadSceneSingle,
                        SceneId = "Scenes/MainMenuScene.helen",
                        DurationSeconds = 0d,
                        Label = "load-menu"
                    }
                }
            };
            SceneComponentAssetRecord componentRecord = persistenceRegistry.GetDescriptor(component)
                .SerializeComponent(component, 0, new EntityComponentSaveState());

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "ProbeRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            componentRecord
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestDirectionalShadowTowerSpinComponent)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);

            using (MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false))
            using (EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian)) {
                Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(6, reader.ReadInt32());
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            Entity loadedRoot = Assert.Single(loadedRoots);
            SceneMemoryProbeComponent loadedComponent = Assert.IsType<SceneMemoryProbeComponent>(
                Assert.Single(loadedRoot.Components, loadedComponent => loadedComponent is SceneMemoryProbeComponent));

            Assert.Equal("menu-memory-probe", loadedComponent.ProbeName);
            Assert.True(loadedComponent.Loop);
            Assert.True(loadedComponent.StartAutomatically);
            Assert.Equal(2.0d, loadedComponent.InitialDelaySeconds);
            Assert.Equal(2, loadedComponent.Steps.Length);
            Assert.Equal(SceneMemoryProbeActionKind.Wait, loadedComponent.Steps[0].ActionKind);
            Assert.Equal(5.0d, loadedComponent.Steps[0].DurationSeconds);
            Assert.Equal(SceneMemoryProbeActionKind.LoadSceneSingle, loadedComponent.Steps[1].ActionKind);
            Assert.Equal("Scenes/MainMenuScene.helen", loadedComponent.Steps[1].SceneId);
            Assert.Equal("load-menu", loadedComponent.Steps[1].Label);
        }

        /// <summary>
        /// Ensures the Windows packager preserves directional-shadow motion script components as project-owned automatic script records.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsDirectionalShadowMotionScriptComponents_PreservesCityOwnedScriptRecords() {
            string sceneId = "Scenes/ScriptComponentScene.helen";
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            TestDirectionalShadowMotionScriptComponent component = new TestDirectionalShadowMotionScriptComponent {
                OrbitCenter = new float3(1f, 2f, 3f),
                OrbitRadius = 7f,
                OrbitHeight = 5f,
                BaseAngleRadians = 0.5f,
                AngularSpeedRadians = 0.25f,
                LookDownPitchRadians = -0.4f,
                MinYawRadians = -1.1f,
                MaxYawRadians = 0.9f,
                PitchRadians = -0.6f,
                SweepSpeedRadians = 0.12f,
                BaseYawRadians = 0.33f
            };
            SceneComponentAssetRecord serializedRecord = persistenceRegistry.GetDescriptor(component)
                .SerializeComponent(component, 0, new EntityComponentSaveState());
            DictionaryScriptTypeResolver runtimeScriptTypeResolver = new DictionaryScriptTypeResolver();
            runtimeScriptTypeResolver.Register("project.rendering.CameraOrbitComponent, gameplay", typeof(TestDirectionalShadowMotionScriptComponent));
            runtimeScriptTypeResolver.Register("project.rendering.OrbitComponent, gameplay", typeof(TestDirectionalShadowMotionScriptComponent));
            runtimeScriptTypeResolver.Register("project.rendering.SunSweepComponent, gameplay", typeof(TestDirectionalShadowMotionScriptComponent));
            runtimeScriptTypeResolver.Register("project.rendering.TowerSpinComponent, gameplay", typeof(TestDirectionalShadowMotionScriptComponent));

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "CameraRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("project.rendering.CameraOrbitComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "OrbitRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("project.rendering.OrbitComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "SunRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("project.rendering.SunSweepComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 4u,
                        Name = "TowerRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("project.rendering.TowerSpinComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                runtimeScriptTypeResolver);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Collection(
                packagedScene.RootEntities,
                cameraRoot => Assert.Equal("project.rendering.CameraOrbitComponent, gameplay", Assert.Single(cameraRoot.Components).ComponentTypeId),
                orbitRoot => Assert.Equal("project.rendering.OrbitComponent, gameplay", Assert.Single(orbitRoot.Components).ComponentTypeId),
                sunRoot => Assert.Equal("project.rendering.SunSweepComponent, gameplay", Assert.Single(sunRoot.Components).ComponentTypeId),
                towerRoot => Assert.Equal("project.rendering.TowerSpinComponent, gameplay", Assert.Single(towerRoot.Components).ComponentTypeId));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(
                resolver,
                CreateRuntimeComponentRegistry(
                    runtimeScriptTypeResolver,
                    "project.rendering.CameraOrbitComponent, gameplay",
                    "project.rendering.OrbitComponent, gameplay",
                    "project.rendering.SunSweepComponent, gameplay",
                    "project.rendering.TowerSpinComponent, gameplay"));
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            Assert.Collection(
                loadedRoots,
                cameraRoot => {
                    TestDirectionalShadowMotionScriptComponent loadedComponent = Assert.IsType<TestDirectionalShadowMotionScriptComponent>(Assert.Single(cameraRoot.Components));
                    Assert.Equal(component.OrbitCenter, loadedComponent.OrbitCenter);
                    Assert.Equal(component.LookDownPitchRadians, loadedComponent.LookDownPitchRadians);
                },
                orbitRoot => {
                    TestDirectionalShadowMotionScriptComponent loadedComponent = Assert.IsType<TestDirectionalShadowMotionScriptComponent>(Assert.Single(orbitRoot.Components));
                    Assert.Equal(component.OrbitRadius, loadedComponent.OrbitRadius);
                    Assert.Equal(component.AngularSpeedRadians, loadedComponent.AngularSpeedRadians);
                },
                sunRoot => {
                    TestDirectionalShadowMotionScriptComponent loadedComponent = Assert.IsType<TestDirectionalShadowMotionScriptComponent>(Assert.Single(sunRoot.Components));
                    Assert.Equal(component.MinYawRadians, loadedComponent.MinYawRadians);
                    Assert.Equal(component.PitchRadians, loadedComponent.PitchRadians);
                },
                towerRoot => {
                    TestDirectionalShadowMotionScriptComponent loadedComponent = Assert.IsType<TestDirectionalShadowMotionScriptComponent>(Assert.Single(towerRoot.Components));
                    Assert.Equal(component.BaseYawRadians, loadedComponent.BaseYawRadians);
                    Assert.Equal(component.AngularSpeedRadians, loadedComponent.AngularSpeedRadians);
                });
        }

        /// <summary>
        /// Ensures the Windows packager preserves the gameplay axis-rotation component as an automatic script record instead of rewriting it to the old engine tower-spin type.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsAxisRotationGameplayComponent_PreservesGameplayRuntimeComponentRecord() {
            string sceneId = "Scenes/AxisRotationScriptScene.helen";
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            TestAxisRotationScriptComponent component = new TestAxisRotationScriptComponent {
                Axis = new float3(0f, 1f, 0f),
                AngularSpeedRadiansPerSecond = (float)(Math.PI / 2.0)
            };
            SceneComponentAssetRecord serializedRecord = persistenceRegistry.GetDescriptor(component)
                .SerializeComponent(component, 0, new EntityComponentSaveState());

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "AxisRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("gameplay.rendering.AxisRotationComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestAxisRotationScriptComponent)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);
            SceneComponentAssetRecord packagedRecord = Assert.Single(packagedRoot.Components);
            Assert.Equal("gameplay.rendering.AxisRotationComponent, gameplay", packagedRecord.ComponentTypeId);
            Assert.DoesNotContain(packagedRoot.Components, record => string.Equals(record.ComponentTypeId, "helengine.DirectionalShadowTowerSpinComponent", StringComparison.Ordinal));

            using MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(3, reader.ReadInt32());
            Assert.Equal((float)(Math.PI / 2.0), reader.ReadSingle());
            Assert.Equal(new float3(0f, 1f, 0f), reader.ReadFloat3());
            Assert.Equal((byte)0, reader.ReadByte());
        }

        /// <summary>
        /// Ensures builder-supplied support metadata does not remove the default physics component packaging rules required to load and simulate packaged runtime scenes.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformOmitsPhysicsSupportRules_PreservesDefaultPhysicsPackagingSupport() {
            string sceneId = "Scenes/PhysicsScene.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "PhysicsRoot",
                        LocalPosition = new float3(0f, 3f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Dynamic, true)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(1f, 2f, 3f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(8f, 1f, 8f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestDirectionalShadowTowerSpinComponent)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(loadedRoots);
            Entity fallingRoot = loadedRoots[0];
            float initialY = fallingRoot.LocalPosition.Y;
            for (int index = 0; index < 60; index++) {
                world.Step(1.0 / 60.0);
            }

            Assert.True(fallingRoot.LocalPosition.Y < initialY - 0.25f, $"Expected the packaged dynamic physics root to fall, but its Y position only moved from {initialY} to {fallingRoot.LocalPosition.Y}.");
        }

        /// <summary>
        /// Ensures packaged lights flow through the shared automatic runtime payload path while rounded rectangles remain loadable.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsLightsAndRoundedRect_LeavesPackagedComponentsLoadable() {
            string sceneId = "Scenes/LightingUiScene.helen";
            WriteSceneAsset(sceneId, BuildLightingAndUiSceneAsset(sceneId));

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedDirectionalLightRecord = Assert.Single(packagedScene.RootEntities[0].Components);
            SceneComponentAssetRecord packagedAmbientLightRecord = Assert.Single(packagedScene.RootEntities[1].Components);
            SceneComponentAssetRecord packagedPointLightRecord = Assert.Single(packagedScene.RootEntities[2].Components);
            SceneComponentAssetRecord packagedSpotLightRecord = Assert.Single(packagedScene.RootEntities[3].Components);
            SceneComponentAssetRecord packagedRoundedRectRecord = Assert.Single(packagedScene.RootEntities[4].Components);

            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(DirectionalLightComponent)), packagedDirectionalLightRecord.ComponentTypeId);
            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(AmbientLightComponent)), packagedAmbientLightRecord.ComponentTypeId);
            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(PointLightComponent)), packagedPointLightRecord.ComponentTypeId);
            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SpotLightComponent)), packagedSpotLightRecord.ComponentTypeId);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, packagedDirectionalLightRecord.Payload[0]);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, packagedAmbientLightRecord.Payload[0]);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, packagedPointLightRecord.Payload[0]);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, packagedSpotLightRecord.Payload[0]);
            AssertUsesAutomaticRuntimePayload(packagedRoundedRectRecord, typeof(RoundedRectComponent));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            DirectionalLightComponent directionalLightComponent = Assert.IsType<DirectionalLightComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is DirectionalLightComponent));
            AmbientLightComponent ambientLightComponent = Assert.IsType<AmbientLightComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is AmbientLightComponent));
            PointLightComponent pointLightComponent = Assert.IsType<PointLightComponent>(
                Assert.Single(loadedRoots[2].Components, component => component is PointLightComponent));
            SpotLightComponent spotLightComponent = Assert.IsType<SpotLightComponent>(
                Assert.Single(loadedRoots[3].Components, component => component is SpotLightComponent));
            RoundedRectComponent roundedRectComponent = Assert.IsType<RoundedRectComponent>(
                Assert.Single(loadedRoots[4].Components, component => component is RoundedRectComponent));

            Assert.Equal(72f, directionalLightComponent.ShadowDistance);
            Assert.Equal(1.4f, ambientLightComponent.Intensity);
            Assert.Equal(18f, pointLightComponent.Range);
            Assert.Equal(22f, spotLightComponent.Range);
            Assert.Equal(18f, spotLightComponent.InnerConeAngleDegrees);
            Assert.Equal(31f, spotLightComponent.OuterConeAngleDegrees);
            Assert.Equal(14f, roundedRectComponent.Radius);
            Assert.Equal(new byte4(4, 8, 12, 255), roundedRectComponent.FillColor);
            Assert.Equal(new byte4(80, 120, 160, 255), roundedRectComponent.BorderColor);
        }

        /// <summary>
        /// Ensures packaged scenes rewrite sprite component texture references through the shared automatic runtime payload format.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsSpriteComponent_LeavesPackagedSpriteLoadable() {
            string sceneId = "Scenes/SpriteScene.helen";
            string textureRelativePath = "Textures/UiSprite.png";
            WriteSourceTextureAsset(textureRelativePath);
            SceneAssetReference textureReference = CreateFileTextureReference(textureRelativePath);
            WriteSceneAsset(sceneId, "helengine.SpriteComponent", WriteSpriteComponentPayload(textureReference), new[] { textureReference });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                [
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"])
                ]);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedSpriteRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);
            AssertUsesAutomaticRuntimePayload(packagedSpriteRecord, typeof(SpriteComponent));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            SpriteComponent loadedSpriteComponent = Assert.IsType<SpriteComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is SpriteComponent));

            Assert.NotNull(loadedSpriteComponent.Texture);
            Assert.Equal(new float4(0f, 0f, 1f, 1f), loadedSpriteComponent.SourceRect);
            Assert.Equal(new int2(32, 14), loadedSpriteComponent.Size);
            Assert.Equal(new byte4(249, 243, 255, 255), loadedSpriteComponent.Color);
            Assert.Equal(34, loadedSpriteComponent.RenderOrder2D);
            Assert.Equal(1, loadedSpriteComponent.LayerMask);
        }

        /// <summary>
        /// Ensures builder-provided pass-through light metadata cannot weaken the built-in runtime light transform contract.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformDowngradesBuiltInLightSupportRulesToPassThrough_PreservesRuntimeLightTransforms() {
            string sceneId = "Scenes/LightingUiScene.helen";
            WriteSceneAsset(sceneId, BuildLightingAndUiSceneAsset(sceneId));

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(
                [
                    new PlatformComponentSupportRule(
                        "helengine.DirectionalLightComponent",
                        PlatformComponentSupportKind.PassThrough,
                        "Legacy builder metadata still expects authored directional light payloads.",
                        string.Empty),
                    new PlatformComponentSupportRule(
                        "helengine.AmbientLightComponent",
                        PlatformComponentSupportKind.PassThrough,
                        "Legacy builder metadata still expects authored ambient light payloads.",
                        string.Empty),
                    new PlatformComponentSupportRule(
                        "helengine.PointLightComponent",
                        PlatformComponentSupportKind.PassThrough,
                        "Legacy builder metadata still expects authored point light payloads.",
                        string.Empty),
                    new PlatformComponentSupportRule(
                        "helengine.SpotLightComponent",
                        PlatformComponentSupportKind.PassThrough,
                        "Legacy builder metadata still expects authored spot light payloads.",
                        string.Empty)
                ]);
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            DirectionalLightComponent directionalLightComponent = Assert.IsType<DirectionalLightComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is DirectionalLightComponent));
            AmbientLightComponent ambientLightComponent = Assert.IsType<AmbientLightComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is AmbientLightComponent));
            PointLightComponent pointLightComponent = Assert.IsType<PointLightComponent>(
                Assert.Single(loadedRoots[2].Components, component => component is PointLightComponent));
            SpotLightComponent spotLightComponent = Assert.IsType<SpotLightComponent>(
                Assert.Single(loadedRoots[3].Components, component => component is SpotLightComponent));

            Assert.Equal(72f, directionalLightComponent.ShadowDistance);
            Assert.Equal(1.4f, ambientLightComponent.Intensity);
            Assert.Equal(18f, pointLightComponent.Range);
            Assert.Equal(22f, spotLightComponent.Range);
            Assert.Equal(18f, spotLightComponent.InnerConeAngleDegrees);
            Assert.Equal(31f, spotLightComponent.OuterConeAngleDegrees);
        }

        /// <summary>
        /// Ensures file-system model references in the scene manifest are imported instead of copied raw.
        /// </summary>
        [Fact]
        public void Package_WhenSceneReferencesFileSystemModel_ImportsModelAssetInsteadOfCopyingObj() {
            string sceneId = "Scenes/SponzaScene.helen";
            string sourceModelRelativePath = "Models/Sponza.obj";
            string sourceModelPath = Path.Combine(ProjectRootPath, "assets", sourceModelRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath));
            File.WriteAllText(sourceModelPath, "o Sponza\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

            IAssetImporterRegistration importerRegistration = new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" });

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                AssetReferences = new[] {
                    global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemModel(sourceModelRelativePath)
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                new[] { importerRegistration },
                defaultFont);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string importedModelPath = Path.Combine(BuildRootPath, "cooked", "imported", "Models", "Sponza.hasset");
            Assert.True(File.Exists(importedModelPath));
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));

            using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/imported/models/sponza.hasset", packagedScene.AssetReferences[0].RelativePath);
        }

        /// <summary>
        /// Ensures packaging for one target platform applies the authored entity transform override and strips the editor-only override metadata from the packaged runtime scene.
        /// </summary>
        [Fact]
        public void Package_WhenSceneEntityDefinesWindowsTransformOverride_AppliesOverrideToPackagedSceneEntity() {
            string sceneId = "Scenes/PlatformTransform.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = new float3(1f, 2f, 3f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        PlatformTransformOverrides = new[] {
                            new SceneEntityPlatformTransformOverrideAsset {
                                PlatformId = "windows",
                                HasLocalPositionOverride = true,
                                LocalPosition = new float3(10f, 20f, 30f)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "windows");
            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);

            Assert.Equal(new float3(10f, 20f, 30f), packagedRoot.LocalPosition);
            Assert.Empty(packagedRoot.PlatformTransformOverrides);
        }

        /// <summary>
        /// Ensures packaging for one target platform removes child entity subtrees authored as absent on that platform and strips the editor-only existence metadata from surviving runtime entities.
        /// </summary>
        [Fact]
        public void Package_WhenSceneChildDefinesWindowsExistenceOverrideFalse_PrunesTheChildSubtree() {
            string sceneId = "Scenes/PlatformChildExistence.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        PlatformExistenceOverrides = new[] {
                            new SceneEntityPlatformExistenceOverrideAsset {
                                PlatformId = "windows",
                                Exists = true
                            }
                        },
                        Children = new[] {
                            new SceneEntityAsset {
                                Id = 2u,
                                Name = "Child",
                                LocalPosition = float3.Zero,
                                LocalScale = float3.One,
                                LocalOrientation = float4.Identity,
                                Components = Array.Empty<SceneComponentAssetRecord>(),
                                PlatformExistenceOverrides = new[] {
                                    new SceneEntityPlatformExistenceOverrideAsset {
                                        PlatformId = "windows",
                                        Exists = false
                                    }
                                },
                                Children = new[] {
                                    new SceneEntityAsset {
                                        Id = 3u,
                                        Name = "GrandChild",
                                        LocalPosition = float3.Zero,
                                        LocalScale = float3.One,
                                        LocalOrientation = float4.Identity,
                                        Components = Array.Empty<SceneComponentAssetRecord>(),
                                        Children = Array.Empty<SceneEntityAsset>()
                                    }
                                }
                            }
                        }
                    }
                }
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "windows");
            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);

            Assert.Empty(packagedRoot.PlatformExistenceOverrides);
            Assert.Empty(packagedRoot.Children);
        }

        /// <summary>
        /// Ensures packaging for one target platform removes root entity subtrees authored as absent on that platform.
        /// </summary>
        [Fact]
        public void Package_WhenSceneRootDefinesWindowsExistenceOverrideFalse_PrunesTheRootSubtree() {
            string sceneId = "Scenes/PlatformRootExistence.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        PlatformExistenceOverrides = new[] {
                            new SceneEntityPlatformExistenceOverrideAsset {
                                PlatformId = "windows",
                                Exists = false
                            }
                        },
                        Children = new[] {
                            new SceneEntityAsset {
                                Id = 2u,
                                Name = "Child",
                                LocalPosition = float3.Zero,
                                LocalScale = float3.One,
                                LocalOrientation = float4.Identity,
                                Components = Array.Empty<SceneComponentAssetRecord>(),
                                Children = Array.Empty<SceneEntityAsset>()
                            }
                        }
                    }
                }
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "windows");
            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));

            Assert.Empty(packagedScene.RootEntities);
        }

        /// <summary>
        /// Ensures packaging for one target platform materializes platform-only components into the packaged runtime scene and strips the editor-only existence override metadata.
        /// </summary>
        [Fact]
        public void Package_WhenSceneEntityDefinesWindowsPlatformOnlyCamera_AppendsTheAddedComponentToThePackagedScene() {
            string sceneId = "Scenes/PlatformAddedCamera.helen";
            InitializeRuntimeCore(BuildRootPath);
            SceneComponentAssetRecord cameraRecord = CreateCameraComponentRecord();
            cameraRecord.ComponentKey = "windows-camera";

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        PlatformComponentOverrides = new[] {
                            new SceneEntityPlatformComponentOverrideAsset {
                                PlatformId = "windows",
                                AddedComponents = new[] {
                                    new SceneEntityPlatformAddedComponentAsset {
                                        Component = cameraRecord
                                    }
                                }
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "windows");
            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);
            SceneComponentAssetRecord packagedCameraRecord = Assert.Single(
                packagedRoot.Components,
                component => string.Equals(
                    component.ComponentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(CameraComponent)),
                    StringComparison.Ordinal));

            Assert.NotNull(packagedCameraRecord);
            AssertUsesAutomaticRuntimePayload(packagedCameraRecord, typeof(CameraComponent));
            Assert.Empty(packagedRoot.PlatformComponentOverrides);
        }

        /// <summary>
        /// Ensures authored camera payloads on ordinary scene components package into runtime scenes without failing the shared transform path.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsCameraComponent_WritesPackagedScene() {
            string sceneId = "Scenes/TaggedCameraScene.helen";
            InitializeRuntimeCore(BuildRootPath);
            SceneComponentAssetRecord cameraRecord = CreateCameraComponentRecord();

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "CameraRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            cameraRecord
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                "windows");
            packager.Package(new[] { sceneId }, BuildRootPath);

            using FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);
            SceneComponentAssetRecord packagedCameraRecord = Assert.Single(
                packagedRoot.Components,
                component => string.Equals(
                    component.ComponentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(CameraComponent)),
                    StringComparison.Ordinal));

            Assert.NotNull(packagedCameraRecord);
            AssertUsesAutomaticRuntimePayload(packagedCameraRecord, typeof(CameraComponent));
        }

        /// <summary>
        /// Ensures the committed point-shadow smoke scene packages without failing the component rewrite pipeline.
        /// </summary>
        [Fact]
        public void Package_WhenUsingCommittedPointShadowScene_PackagesSuccessfully() {
            string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string sourceScenePath = Path.Combine(repositoryRootPath, "test-project", "assets", "Scenes", "rendering", "point-shadow.helen");
            string sceneId = "Scenes/rendering/point-shadow.helen";
            string targetScenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "rendering", "point-shadow.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(targetScenePath));
            File.Copy(sourceScenePath, targetScenePath, true);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.NotNull(result);
            Assert.True(File.Exists(GetPackagedScenePath(BuildRootPath, sceneId)));
        }

        /// <summary>
        /// Ensures automatic script components authored in runtime payload form package without being re-read as tagged editor payloads.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsRuntimeEncodedAutomaticScriptComponent_PackagesSuccessfully() {
            string sceneId = "Scenes/ReturnToMenuScene.helen";
            byte[] payload;
            using (MemoryStream stream = new MemoryStream()) {
                using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
                writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
                writer.WriteInt32(1);
                writer.WriteByte(0);
                payload = stream.ToArray();
            }

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "ReturnRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "project.menu.SceneReturnComponent, gameplay",
                                ComponentIndex = 0,
                                Payload = payload
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestSceneReturnComponent)));

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);
            Assert.NotNull(result);
            Assert.True(File.Exists(GetPackagedScenePath(BuildRootPath, sceneId)));
        }

        /// <summary>
        /// Ensures project directional-shadow tower-spin records remain automatic script records when packaged.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsCityDirectionalShadowTowerSpin_PreservesAutomaticScriptRecord() {
            string sceneId = "Scenes/CityDirectionalShadowTowerSpin.helen";
            AutomaticScriptComponentPersistenceDescriptor automaticDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            byte[] payload = automaticDescriptor.SerializeComponent(
                new TestDirectionalShadowTowerSpinComponent {
                    BaseYawRadians = 0.5f,
                    AngularSpeedRadians = 1.25f
                },
                0,
                new EntityComponentSaveState()).Payload;

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "TowerRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "project.rendering.TowerSpinComponent, gameplay",
                                ComponentIndex = 0,
                                Payload = payload
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                null,
                new FakeScriptTypeResolver(typeof(TestDirectionalShadowTowerSpinComponent)));

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);
            Assert.NotNull(result);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);
            Assert.Equal("project.rendering.TowerSpinComponent, gameplay", packagedRecord.ComponentTypeId);

            using MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(3, reader.ReadInt32());
        }


        /// <summary>
        /// Ensures the build packager exports a default packaged font asset that the runtime content pipeline can reload.
        /// </summary>
        [Fact]
        public void Package_WhenDefaultFontAssetIsAvailable_WritesPackagedDefaultFontThatCanBeReloaded() {
            string sceneId = "Scenes/FontScene.helen";
            WriteSceneAsset(sceneId, "helengine.FPSComponent", WriteFpsComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont");
            Assert.True(File.Exists(packagedFontPath));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            FontAsset loadedFont = runtimeContentManager.Load<FontAsset>(packagedFontPath, RuntimeContentProcessorIds.FontAsset);

            Assert.Equal(defaultFont.FontInfo.Name, loadedFont.FontInfo.Name);
            Assert.Equal(defaultFont.FontInfo.LineSpacing, loadedFont.FontInfo.LineSpacing);
            Assert.Equal(defaultFont.FontInfo.SpaceWidth, loadedFont.FontInfo.SpaceWidth);
            Assert.Equal(defaultFont.LineHeight, loadedFont.LineHeight);
            Assert.Equal(defaultFont.AtlasWidth, loadedFont.AtlasWidth);
            Assert.Equal(defaultFont.AtlasHeight, loadedFont.AtlasHeight);
            Assert.NotNull(loadedFont.Texture);
        }

        /// <summary>
        /// Ensures platform-provided support metadata can reject unsupported components with a clear reason.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformMarksComponentUnsupported_FailsWithTheBuilderReason() {
            PlatformDefinition definition = new(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [])
                ],
                [
                    new PlatformAssetRequirementDefinition(
                        "texture",
                        "Texture",
                        true,
                        ["png", "tga"])
                ],
                [
                    new PlatformComponentSupportRule(
                        "helengine.BadComponent",
                        PlatformComponentSupportKind.Unsupported,
                        "This platform does not support the component.",
                        "Remove the component before building.")
                ]);

            string sceneId = "Scenes/Bad.helen";
            WriteSceneAsset(sceneId, "helengine.BadComponent", Array.Empty<byte>());

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                definition);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                packager.Package(new[] { sceneId }, BuildRootPath));

            Assert.Contains("This platform does not support the component.", ex.Message);
            Assert.Contains("Remove the component before building.", ex.Message);
        }

        /// <summary>
        /// Ensures the default scene packager rewrites 3D box-body physics records into automatic runtime payloads.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DBoxBody_RewritesPhysicsComponentRecordsToAutomaticRuntimePayloads() {
            string sceneId = "Scenes/PhysicsBoxes.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = new float3(8f, 1f, 8f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(8f, 1f, 8f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "Box",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Dynamic, true)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(1f, 1f, 1f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            using FileStream packagedSceneStream = File.OpenRead(packagedScenePath);
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));

            SceneEntityAsset packagedGround = packagedScene.RootEntities[0];
            SceneEntityAsset packagedBox = packagedScene.RootEntities[1];
            SceneComponentAssetRecord packagedGroundRigidBody = Assert.Single(packagedGround.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedGroundBoxCollider = Assert.Single(packagedGround.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedBoxRigidBody = Assert.Single(packagedBox.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedBoxBoxCollider = Assert.Single(packagedBox.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));

            AssertUsesAutomaticRuntimePayload(packagedGroundRigidBody, typeof(RigidBody3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedGroundBoxCollider, typeof(BoxCollider3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedBoxRigidBody, typeof(RigidBody3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedBoxBoxCollider, typeof(BoxCollider3DComponent));
            Assert.Equal((uint)PhysicsSceneFeatureFlags3D.BoxBoxContact, packagedScene.Physics3DSceneFeatureFlags);
        }

        /// <summary>
        /// Ensures the default scene packager rewrites kinematic-motion physics records into automatic runtime payloads.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DKinematicMotion_RewritesPhysicsComponentRecordsToAutomaticRuntimePayloads() {
            string sceneId = "Scenes/PhysicsKinematic.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "KinematicPusher",
                        LocalPosition = new float3(-2f, 0.5f, 0f),
                        LocalScale = new float3(1.5f, 1f, 1.5f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Kinematic, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(1.5f, 1f, 1.5f))
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.KinematicMotion3DComponent",
                                ComponentIndex = 2,
                                Payload = WriteAutomaticKinematicMotion3DComponentPayload(new float3(-2f, 0.5f, 0f), new float3(0.5f, 0.5f, 0f), 1d, true)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            using FileStream packagedSceneStream = File.OpenRead(packagedScenePath);
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
            SceneEntityAsset packagedRoot = Assert.Single(packagedScene.RootEntities);

            SceneComponentAssetRecord packagedRigidBody = Assert.Single(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedBoxCollider = Assert.Single(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedKinematicMotion = Assert.Single(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.KinematicMotion3DComponent", StringComparison.Ordinal));

            AssertUsesAutomaticRuntimePayload(packagedRigidBody, typeof(RigidBody3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedBoxCollider, typeof(BoxCollider3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedKinematicMotion, typeof(KinematicMotion3DComponent));
            Assert.Equal((uint)PhysicsSceneFeatureFlags3D.KinematicMotion, packagedScene.Physics3DSceneFeatureFlags);
        }

        /// <summary>
        /// Ensures the default scene packager rewrites character-controller physics records into automatic runtime payloads.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DCharacterController_RewritesPhysicsComponentRecordsToAutomaticRuntimePayloads() {
            string sceneId = "Scenes/PhysicsCharacterController.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = new float3(8f, 1f, 8f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(8f, 1f, 8f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "Controller",
                        LocalPosition = new float3(0f, 1f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(1f, 2f, 1f))
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.CharacterController3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticCharacterController3DComponentPayload(new float3(1f, 0f, 0f), 4.5d, 1d, 0.4d, 0.25d)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            using FileStream packagedSceneStream = File.OpenRead(packagedScenePath);
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(new HostFileSystemContentStreamSource(BuildRootPath));
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            RigidBody3DComponent loadedGroundRigidBody = Assert.IsType<RigidBody3DComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is RigidBody3DComponent));
            BoxCollider3DComponent loadedGroundBoxCollider = Assert.IsType<BoxCollider3DComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is BoxCollider3DComponent));
            BoxCollider3DComponent loadedControllerBoxCollider = Assert.IsType<BoxCollider3DComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is BoxCollider3DComponent));
            Assert.IsType<CharacterController3DComponent>(
                Assert.Single(loadedRoots[1].Components, component => component is CharacterController3DComponent));
            Assert.Equal(BodyKind3D.Static, loadedGroundRigidBody.BodyKind);
            Assert.False(loadedGroundBoxCollider.IsTrigger);
            Assert.False(loadedControllerBoxCollider.IsTrigger);

            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(loadedRoots);
            Assert.Single(world.ControllerStates);

            SceneComponentAssetRecord packagedGroundRigidBody = Assert.Single(packagedScene.RootEntities[0].Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedGroundBoxCollider = Assert.Single(packagedScene.RootEntities[0].Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedControllerBoxCollider = Assert.Single(packagedScene.RootEntities[1].Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedController = Assert.Single(packagedScene.RootEntities[1].Components, component => string.Equals(component.ComponentTypeId, "helengine.CharacterController3DComponent", StringComparison.Ordinal));

            AssertUsesAutomaticRuntimePayload(packagedGroundRigidBody, typeof(RigidBody3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedGroundBoxCollider, typeof(BoxCollider3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedControllerBoxCollider, typeof(BoxCollider3DComponent));
            AssertUsesAutomaticRuntimePayload(packagedController, typeof(CharacterController3DComponent));
            Assert.Equal(
                (uint)(PhysicsSceneFeatureFlags3D.CharacterController | PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport),
                packagedScene.Physics3DSceneFeatureFlags);
        }

        /// <summary>
        /// Ensures trigger colliders propagate the compact scene-feature flag used by runtime trigger dispatch.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DTriggerCollider_WritesPhysics3DFeatureFlags() {
            string sceneId = "Scenes/PhysicsTrigger.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Trigger",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteAutomaticRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteAutomaticBoxCollider3DComponentPayload(new float3(3f, 2f, 3f), true)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, sceneAsset);
            }

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            using FileStream packagedSceneStream = File.OpenRead(packagedScenePath);
            SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));

            Assert.Equal((uint)PhysicsSceneFeatureFlags3D.TriggerEvents, packagedScene.Physics3DSceneFeatureFlags);
        }

        /// <summary>
        /// Writes one serialized scene asset that references the supplied material path from a mesh component payload.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="materialRelativePath">Project-relative material path to reference.</param>
        void WriteSceneAsset(string sceneId, string materialRelativePath) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "SecondRoot",
                        LocalPosition = new float3(1f, 0f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one serialized scene asset that references the supplied generated material from a mesh component payload.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="materialReference">Generated material reference to encode.</param>
        void WriteSceneAsset(string sceneId, SceneAssetReference materialReference) {
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialReference)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one serialized scene asset that references the supplied generated model and material from duplicated mesh component payloads.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="modelReference">Generated model reference to encode.</param>
        /// <param name="materialReference">Generated material reference to encode.</param>
        void WriteSceneAsset(string sceneId, SceneAssetReference modelReference, SceneAssetReference materialReference) {
            if (modelReference == null) {
                throw new ArgumentNullException(nameof(modelReference));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(modelReference, materialReference)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "SecondRoot",
                        LocalPosition = new float3(1f, 0f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(modelReference, materialReference)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one serialized scene asset that contains a single component record with the supplied type id and payload.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="componentTypeId">Serialized component type id to encode.</param>
        /// <param name="payload">Serialized component payload to attach.</param>
        void WriteSceneAsset(string sceneId, string componentTypeId, byte[] payload, SceneAssetReference[] assetReferences = null) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                AssetReferences = assetReferences ?? Array.Empty<SceneAssetReference>(),
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = componentTypeId,
                                ComponentIndex = 0,
                                Payload = payload
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one complete serialized scene asset to the test project.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="sceneAsset">Complete scene asset to serialize.</param>
        void WriteSceneAsset(string sceneId, SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Creates one automatic scripted component record that persists an authored animation-clip scene reference.
        /// </summary>
        /// <param name="clipRelativePath">Project-relative animation-clip path referenced by the component.</param>
        /// <returns>Serialized automatic component record.</returns>
        static SceneComponentAssetRecord CreateAnimationClipScriptComponentRecord(string clipRelativePath) {
            if (string.IsNullOrWhiteSpace(clipRelativePath)) {
                throw new ArgumentException("Clip relative path must be provided.", nameof(clipRelativePath));
            }

            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestAnimationClipAssetScriptComponent component = new TestAnimationClipAssetScriptComponent {
                Label = "logo-idle",
                IdleClip = new AnimationClipAsset {
                    Id = clipRelativePath,
                    Duration = 0.75f
                }
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                nameof(TestAnimationClipAssetScriptComponent.IdleClip),
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemAnimationClip(clipRelativePath));

            return descriptor.SerializeComponent(component, 0, saveState);
        }

        /// <summary>
        /// Writes one minimal authored animation clip asset to the test project.
        /// </summary>
        /// <param name="relativePath">Project-relative animation clip path to write.</param>
        void WriteAnimationClipAsset(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Animation clip path must be provided.", nameof(relativePath));
            }

            string clipPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(clipPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Animation clip directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            AnimationClipAsset clipAsset = new AnimationClipAsset {
                Id = relativePath,
                Duration = 0.75f,
                PositionTracks = Array.Empty<PositionKeyframeTrackAsset>(),
                PositionOffsetTracks = Array.Empty<PositionOffsetKeyframeTrackAsset>(),
                ScaleTracks = Array.Empty<ScaleKeyframeTrackAsset>(),
                RotationTracks = Array.Empty<RotationKeyframeTrackAsset>()
            };

            using FileStream stream = new FileStream(clipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, clipAsset);
        }

        /// <summary>
        /// Creates the generated scene reference used for the editor's built-in font asset.
        /// </summary>
        /// <returns>Generated editor font scene reference.</returns>
        static SceneAssetReference CreateEditorFontReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEditorUiFont();
        }

        /// <summary>
        /// Creates the generated scene reference used for the Nintendo DS debug font asset.
        /// </summary>
        /// <returns>Generated Nintendo DS debug-font scene reference.</returns>
        static SceneAssetReference CreateNintendoDsDebugFontReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "generated/editor/fonts/ds-debug.hefont",
                "editor",
                "ds-debug-font");
        }

        /// <summary>
        /// Creates the generated scene reference used for the engine's built-in standard material.
        /// </summary>
        /// <returns>Generated engine standard-material scene reference.</returns>
        static SceneAssetReference CreateGeneratedStandardMaterialReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineStandardMaterial();
        }

        /// <summary>
        /// Creates the generated scene reference used for the engine's built-in cube primitive.
        /// </summary>
        /// <returns>Generated engine cube scene reference.</returns>
        static SceneAssetReference CreateGeneratedCubeReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel();
        }

        /// <summary>
        /// Clones one serialized script component payload under a supplied authored directional-shadow type id.
        /// </summary>
        /// <param name="componentTypeId">Authored project-script component type id to stamp onto the cloned record.</param>
        /// <param name="serializedRecord">Serialized source record whose payload should be reused.</param>
        /// <returns>Scene component record using the supplied authored type id.</returns>
        static SceneComponentAssetRecord CreateDirectionalShadowComponentRecord(string componentTypeId, SceneComponentAssetRecord serializedRecord) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }
            if (serializedRecord == null) {
                throw new ArgumentNullException(nameof(serializedRecord));
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = componentTypeId,
                ComponentIndex = serializedRecord.ComponentIndex,
                Payload = serializedRecord.Payload
            };
        }

        /// <summary>
        /// Initializes one runtime core instance for packaged-content loading against the current build root.
        /// </summary>
        /// <param name="contentRootPath">Packaged content root that should back the runtime content manager.</param>
        /// <summary>
        /// Creates one runtime component registry that extends the built-in registry with automatic script deserializers for explicit persisted script ids.
        /// </summary>
        /// <param name="scriptTypeResolver">Resolver that maps persisted script ids to runtime component types.</param>
        /// <param name="componentTypeIds">Persisted script ids that should deserialize through the automatic runtime component path.</param>
        /// <returns>Runtime component registry that can load the supplied script components.</returns>
        static RuntimeComponentRegistry CreateRuntimeComponentRegistry(IScriptTypeResolver scriptTypeResolver, params string[] componentTypeIds) {
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }

            RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault();
            if (componentTypeIds == null) {
                return registry;
            }

            for (int index = 0; index < componentTypeIds.Length; index++) {
                string componentTypeId = componentTypeIds[index];
                if (string.IsNullOrWhiteSpace(componentTypeId)) {
                    throw new ArgumentException("Component type id must be provided.", nameof(componentTypeIds));
                }

                registry.Register(new AutomaticScriptComponentRuntimeDeserializer(componentTypeId, scriptTypeResolver.Resolve(componentTypeId)));
            }

            return registry;
        }

        /// <summary>
        /// Initializes one runtime core instance backed by the supplied packaged content root.
        /// </summary>
        /// <param name="contentRootPath">Absolute content root path used by the runtime core.</param>
        void InitializeRuntimeCore(string contentRootPath) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(contentRootPath)
            });
            core.Initialize(new TestRenderManager3D(ShaderCompileTarget.DirectX11), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates the file-backed font reference used by packaged demo-disc menu scenes.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <returns>File-backed scene reference.</returns>
        static SceneAssetReference CreateFileFontReference(string relativePath) {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemFont(relativePath);
        }

        /// <summary>
        /// Creates the file-backed texture reference used by packaged sprite scenes.
        /// </summary>
        /// <param name="relativePath">Project-relative texture asset path.</param>
        /// <returns>File-backed scene reference.</returns>
        static SceneAssetReference CreateFileTextureReference(string relativePath) {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemTexture(relativePath);
        }

        /// <summary>
        /// Writes one serialized scene asset containing a single empty root entity.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        void WriteEmptySceneAsset(string sceneId) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one packaged font asset into the source project assets folder.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <param name="fontAsset">Font asset to serialize.</param>
        void WriteFontAsset(string relativePath, FontAsset fontAsset) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        /// <summary>
        /// Writes one raw source font file into the source project assets folder.
        /// </summary>
        /// <param name="relativePath">Project-relative source font path.</param>
        void WriteSourceFont(string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, new byte[] { 1, 2, 3, 4 });
        }

        /// <summary>
        /// Creates a packaged font asset with exportable raw atlas data.
        /// </summary>
        /// <returns>Font asset with export data attached for packaging.</returns>
        FontAsset CreatePackagedFontAsset() {
            byte[] colors = new byte[64];
            for (int index = 0; index < colors.Length; index += 4) {
                byte pixelIndex = (byte)(index / 4);
                colors[index] = (byte)(pixelIndex * 16);
                colors[index + 1] = (byte)(255 - (pixelIndex * 16));
                colors[index + 2] = (byte)(pixelIndex * 8);
                colors[index + 3] = 255;
            }

            TextureAsset textureAsset = new TextureAsset {
                Width = 4,
                Height = 4,
                Colors = colors
            };

            FontAsset font = new FontAsset(
                new FontInfo("PackagedTest", 16, 4f),
                new TestRuntimeTexture {
                    Width = textureAsset.Width,
                    Height = textureAsset.Height
                },
                new Dictionary<char, FontChar> {
                    ['A'] = new FontChar(new float4(0f, 0f, 1f, 1f), 0f, 1f, 0f, 0f)
                },
                16f,
                textureAsset.Width,
                textureAsset.Height);

            font.SourceTextureAsset = textureAsset;
            return font;
        }

        /// <summary>
        /// Writes one authored material document that references the supplied shader asset id.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to write.</param>
        /// <param name="shaderAssetId">Shader asset id referenced by the material.</param>
        /// <param name="diffuseTextureAssetId">Optional diffuse texture asset id authored on the material.</param>
        void WriteMaterialAsset(string materialRelativePath, string shaderAssetId, string diffuseTextureAssetId = "") {
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

            MaterialAssetImportSettings settings = CreateMaterialSettings(
                materialRelativePath,
                shaderAssetId,
                diffuseTextureAssetId,
                string.Empty,
                string.Empty,
                "standard-shader",
                useCustomShader: true,
                "#FFFFFFFF");

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(materialPath, settings);
        }

        /// <summary>
        /// Writes one minimal source texture or model file into the project assets tree.
        /// </summary>
        /// <param name="relativePath">Project-relative source path to create.</param>
        void WriteSourceTextureAsset(string relativePath) {
            string sourcePath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
        }

        /// <summary>
        /// Writes one source texture file and returns the asset id that the editor importer settings resolve for it.
        /// </summary>
        /// <param name="textureRelativePath">Project-relative source texture path to create.</param>
        /// <param name="extension">Texture extension registered for the test importer.</param>
        /// <returns>Importer-resolved texture asset id for the written source texture.</returns>
        string WriteSourceTextureAssetAndReturnAssetId(string textureRelativePath, string extension, string platformId) {
            WriteSourceTextureAsset(textureRelativePath);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new(ProjectRootPath, contentManager);
            assetImportManager.CurrentPlatformId = platformId;
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [extension]));

            string textureSourcePath = Path.Combine(ProjectRootPath, "assets", textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
            TextureAssetImportSettings settings;
            Assert.True(assetImportManager.TryLoadOrCreateTextureImportSettings(textureSourcePath, out settings));
            Assert.NotNull(settings);
            Assert.NotNull(settings.Importer);
            Assert.False(string.IsNullOrWhiteSpace(settings.Importer.AssetId));
            return settings.Importer.AssetId;
        }

        /// <summary>
        /// Writes one authored material document without explicit shader overrides so the packager must supply the standard defaults.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to write.</param>
        void WriteBlankMaterialAsset(string materialRelativePath) {
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

            MaterialAssetImportSettings settings = CreateMaterialSettings(
                materialRelativePath,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "standard-shader",
                useCustomShader: false,
                "#FFFFFFFF");

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(materialPath, settings);
        }

        /// <summary>
        /// Writes one project-style standard material document that mirrors the colored cube-grid authored content.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to write.</param>
        void WriteCityStyleStandardMaterialAsset(
            string materialRelativePath,
            string diffuseTextureAssetId = "",
            string roughnessTextureAssetId = "",
            string emissiveTextureAssetId = "") {
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

            MaterialAssetImportSettings settings = CreateMaterialSettings(
                materialRelativePath,
                "ForwardStandardShader",
                diffuseTextureAssetId,
                roughnessTextureAssetId,
                emissiveTextureAssetId,
                "standard-shader",
                useCustomShader: false,
                "#FF4040FF");

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(materialPath, settings);
        }

        /// <summary>
        /// Writes one cached imported texture asset using the project cache location expected by scene packaging.
        /// </summary>
        /// <param name="textureAssetId">Imported texture asset identifier to write.</param>
        void WriteCachedTextureAsset(string textureAssetId) {
            string texturePath = Path.Combine(ProjectRootPath, "cache", textureAssetId);
            Directory.CreateDirectory(Path.GetDirectoryName(texturePath));

            TextureAsset textureAsset = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            };

            using FileStream stream = new FileStream(texturePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, textureAsset);
        }

        /// <summary>
        /// Creates one minimal GameCube platform definition that publishes builder-owned runtime texture cooking.
        /// </summary>
        /// <returns>GameCube platform definition used by platform cook work-item packaging tests.</returns>
        static PlatformDefinition CreateGameCubeBuilderOwnedTexturePlatformDefinition() {
            return new PlatformDefinition(
                "gamecube",
                "GameCube",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug GameCube build",
                        "gx",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "gx",
                        "GX",
                        "GameCube GX renderer",
                        [])
                ],
                [],
                [],
                [],
                [],
                [],
                [],
                null,
                null,
                [
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "gamecube-texture")
                ]);
        }

        /// <summary>
        /// Creates one platform definition that publishes builder-owned font-atlas texture cooking with one default serialized texture-settings payload.
        /// </summary>
        /// <param name="defaultSerializedTextureSettings">Default serialized texture settings emitted when the source font has no platform override.</param>
        /// <returns>Platform definition used by builder-owned font-atlas texture packaging tests.</returns>
        static PlatformDefinition CreateGameCubeBuilderOwnedFontAtlasTexturePlatformDefinition(string defaultSerializedTextureSettings) {
            return new PlatformDefinition(
                "gamecube",
                "GameCube",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug GameCube build",
                        "gx",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "gx",
                        "GX",
                        "GameCube GX renderer",
                        [])
                ],
                [],
                [],
                [],
                [],
                [],
                [],
                null,
                null,
                [
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "gamecube-texture",
                        defaultSerializedTextureSettings)
                ]);
        }

        /// <summary>
        /// Writes one malformed material settings override that names the platform but omits schema and field values.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path whose override should be written.</param>
        void WriteInvalidMaterialSettings(string materialRelativePath) {
            string overridePath = GetMaterialPlatformOverridePath(materialRelativePath, "windows");
            string directoryPath = Path.GetDirectoryName(overridePath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Material override directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            MaterialAssetPlatformOverrideDocument document = new MaterialAssetPlatformOverrideDocument {
                PlatformId = "windows",
                Processor = new MaterialAssetProcessorOverrideSettings()
            };

            using FileStream stream = new FileStream(overridePath, FileMode.Create, FileAccess.Write, FileShare.None);
            MaterialAssetPlatformOverrideDocumentBinarySerializer.Serialize(stream, document);
        }

        /// <summary>
        /// Writes one corrupt material settings override file for the supplied platform.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path whose override should be corrupted.</param>
        /// <param name="platformId">Platform identifier encoded in the override filename.</param>
        void WriteCorruptMaterialSettingsOverride(string materialRelativePath, string platformId) {
            string overridePath = GetMaterialPlatformOverridePath(materialRelativePath, platformId);
            string directoryPath = Path.GetDirectoryName(overridePath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Material override directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            File.WriteAllBytes(overridePath, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        }

        /// <summary>
        /// Resolves one project-relative material override path for the supplied platform.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative base material path.</param>
        /// <param name="platformId">Platform identifier appended before the `.hasset` suffix.</param>
        /// <returns>Absolute override file path under the project assets tree.</returns>
        string GetMaterialPlatformOverridePath(string materialRelativePath, string platformId) {
            if (string.IsNullOrWhiteSpace(materialRelativePath)) {
                throw new ArgumentException("Material relative path must be provided.", nameof(materialRelativePath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            string relativePath = materialRelativePath.Replace('/', Path.DirectorySeparatorChar);
            string extension = Path.GetExtension(relativePath);
            string basePathWithoutExtension = relativePath.Substring(0, relativePath.Length - extension.Length);
            return Path.Combine(ProjectRootPath, "assets", $"{basePathWithoutExtension}.{platformId}{extension}");
        }

        /// <summary>
        /// Creates one authored material settings payload for the supplied material path and shader behavior.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path stored as the material asset id.</param>
        /// <param name="shaderAssetId">Shader asset id to author when custom shader mode is enabled.</param>
        /// <param name="diffuseTextureAssetId">Optional authored diffuse texture asset id.</param>
        /// <param name="schemaId">Schema id assigned to the authored material.</param>
        /// <param name="useCustomShader">True when custom shader fields should be authored explicitly.</param>
        /// <param name="baseColor">Authored standard-material base color.</param>
        /// <returns>Material settings payload ready to save.</returns>
        MaterialAssetImportSettings CreateMaterialSettings(
            string materialRelativePath,
            string shaderAssetId,
            string diffuseTextureAssetId,
            string roughnessTextureAssetId,
            string emissiveTextureAssetId,
            string schemaId,
            bool useCustomShader,
            string baseColor) {
            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = "helengine.material";
            settings.Importer.SourceChecksum = string.Empty;
            settings.Importer.AssetId = materialRelativePath;
            settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
                SchemaId = schemaId,
                FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    ["use-custom-shader"] = useCustomShader ? "true" : "false",
                    ["texture-id"] = diffuseTextureAssetId ?? string.Empty,
                    ["roughness"] = "1.0",
                    ["roughness-texture-id"] = roughnessTextureAssetId ?? string.Empty,
                    ["emissive-texture-id"] = emissiveTextureAssetId ?? string.Empty,
                    ["casts-shadow"] = "true",
                    ["receives-shadow"] = "true",
                    ["base-color"] = baseColor ?? "#FFFFFFFF"
                }
            };

            if (useCustomShader) {
                settings.Processor.Platforms["windows"].FieldValues["shader-asset-id"] = shaderAssetId ?? string.Empty;
                settings.Processor.Platforms["windows"].FieldValues["vertex-program"] = string.IsNullOrWhiteSpace(shaderAssetId) ? string.Empty : string.Concat(shaderAssetId, ".vs");
                settings.Processor.Platforms["windows"].FieldValues["pixel-program"] = string.IsNullOrWhiteSpace(shaderAssetId) ? string.Empty : string.Concat(shaderAssetId, ".ps");
            }

            return settings;
        }

        /// <summary>
        /// Writes one compiled shader package into the local editor shader cache.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to encode.</param>
        /// <param name="target">Shader compile target to encode.</param>
        void WriteShaderCachePackage(string shaderAssetId, ShaderCompileTarget target) {
            string packagePath = ShaderPackagePaths.GetPackagePath(Path.Combine(ProjectRootPath, "cache", "shader-cache"), shaderAssetId, target);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath));

            ShaderAsset shaderAsset = new ShaderAsset {
                Id = shaderAssetId,
                Name = shaderAssetId,
                TargetName = ShaderTargetNames.GetTargetName(target),
                Programs = new[] {
                    new ShaderProgramAsset {
                        Name = string.Concat(shaderAssetId, ".vs"),
                        Stage = ShaderStage.Vertex,
                        EntryPoint = "VS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = new[] {
                            new ShaderVariantAsset {
                                Name = "default",
                                Defines = Array.Empty<string>()
                            }
                        }
                    }
                },
                Binaries = new[] {
                    new ShaderBinaryAsset {
                        ProgramName = string.Concat(shaderAssetId, ".vs"),
                        Stage = ShaderStage.Vertex,
                        TargetName = ShaderTargetNames.GetTargetName(target),
                        Variant = "default",
                        Bytecode = new byte[] { 1, 2, 3, 4 }
                    }
                }
            };

            using FileStream stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, shaderAsset);
        }

        /// <summary>
        /// Writes one mesh-component payload that points at the supplied material path.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(string materialRelativePath) {
            MeshComponent meshComponent = new MeshComponent {
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() }
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                "Materials[0]",
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial(materialRelativePath));
            return CreateAuthoredMeshComponentRecord(meshComponent, saveState).Payload;
        }

        /// <summary>
        /// Creates one DS platform definition whose texture cooking is builder-owned.
        /// </summary>
        /// <returns>Platform definition with builder-owned DS texture cooking.</returns>
        static PlatformDefinition CreateDsBuilderOwnedTexturePlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                [
                    new PlatformBuildProfileDefinition(
                        "ds-default",
                        "DS Default",
                        "Debug player build",
                        "ds-main-2d",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "ds-main-2d",
                        "DS Main 2D",
                        "Nintendo DS fixed-pipeline renderer",
                        [])
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.ContentRelativeOnly),
                null,
                [
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "ds-texture-settings",
                        string.Empty,
                        null,
                        ".hetex")
                ]);
        }

        /// <summary>
        /// Writes one mesh-component payload that points at one generated material reference.
        /// </summary>
        /// <param name="materialReference">Generated material reference to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(SceneAssetReference materialReference) {
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            MeshComponent meshComponent = new MeshComponent {
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() }
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Materials[0]", materialReference);
            return CreateAuthoredMeshComponentRecord(meshComponent, saveState).Payload;
        }

        /// <summary>
        /// Writes one mesh-component payload that points at one generated model reference and one generated material reference.
        /// </summary>
        /// <param name="modelReference">Generated model reference to encode.</param>
        /// <param name="materialReference">Generated material reference to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            if (modelReference == null) {
                throw new ArgumentNullException(nameof(modelReference));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() }
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Model", modelReference);
            saveState.SetAssetReference("Materials[0]", materialReference);
            return CreateAuthoredMeshComponentRecord(meshComponent, saveState).Payload;
        }

        /// <summary>
        /// Writes one mesh-component payload that points at two tagged material references.
        /// </summary>
        /// <param name="firstMaterialRelativePath">Project-relative first material path to encode.</param>
        /// <param name="secondMaterialRelativePath">Project-relative second material path to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(string firstMaterialRelativePath, string secondMaterialRelativePath) {
            MeshComponent meshComponent = new MeshComponent();
            meshComponent.SetMaterials(new RuntimeMaterial[] {
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial()
            });

            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                "Materials[0]",
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial(firstMaterialRelativePath));
            saveState.SetAssetReference(
                "Materials[1]",
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial(secondMaterialRelativePath));
            return CreateAuthoredMeshComponentRecord(meshComponent, saveState).Payload;
        }

        /// <summary>
        /// Serializes one authored mesh component through the shared generic persistence registry.
        /// </summary>
        /// <param name="meshComponent">Mesh component instance to serialize.</param>
        /// <param name="saveState">Editor-time asset-reference state associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        SceneComponentAssetRecord CreateAuthoredMeshComponentRecord(MeshComponent meshComponent, EntityComponentSaveState saveState) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }

            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            return persistenceRegistry.GetDescriptor(meshComponent).SerializeComponent(meshComponent, 0, saveState);
        }

        /// <summary>
        /// Writes one serialized scene asset that contains one mesh component with two material references.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="firstMaterialRelativePath">Project-relative first material path to encode.</param>
        /// <param name="secondMaterialRelativePath">Project-relative second material path to encode.</param>
        void WriteSceneAssetWithTaggedMultiMaterialMesh(string sceneId, string firstMaterialRelativePath, string secondMaterialRelativePath) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(firstMaterialRelativePath, secondMaterialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Provides one deterministic generated text-sprite bake result for end-to-end packager tests.
        /// </summary>
        sealed class StubTextComponentSpriteBakeService : ITextComponentSpriteBakeService {
            /// <summary>
            /// Bakes the supplied text request into one deterministic generated texture result.
            /// </summary>
            /// <param name="request">Authored text request being packaged.</param>
            /// <returns>Generated texture asset and processor settings for packager verification.</returns>
            public TextComponentSpriteBakeResult Bake(TextComponentSpriteBakeRequest request) {
                return new TextComponentSpriteBakeResult(
                    new TextureAsset {
                        Id = "generated:text-sprite",
                        Width = 96,
                        Height = 24,
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8,
                        Colors = new byte[96 * 24 * 4]
                    },
                    new TextureAssetProcessorSettings {
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8
                    },
                    "packager-text-sprite");
            }
        }

        /// <summary>
        /// Writes one serialized FPS component payload.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            FPSComponent fpsComponent = new FPSComponent {
                Font = CreatePackagedFontAsset(),
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", CreateEditorFontReference());

            SceneComponentAssetRecord record = descriptor.SerializeComponent(fpsComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized debug component payload.
        /// </summary>
        /// <returns>Serialized debug component payload.</returns>
        byte[] WriteDebugComponentPayload(SceneAssetReference fontReference = null) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            DebugComponent debugComponent = new DebugComponent {
                Font = CreatePackagedFontAsset(),
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", fontReference ?? CreateEditorFontReference());

            SceneComponentAssetRecord record = descriptor.SerializeComponent(debugComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one older FPS component payload that predates packaged font references.
        /// </summary>
        /// <returns>Serialized older-version FPS component payload.</returns>
        byte[] WriteOlderVersionFpsComponentPayload() {
            byte[] payload = WriteFpsComponentPayload();
            payload[0] = 2;
            return payload;
        }

        /// <summary>
        /// Creates one authored camera component record that matches the current reflected persistence contract.
        /// </summary>
        /// <returns>Serialized authored camera component record.</returns>
        SceneComponentAssetRecord CreateCameraComponentRecord() {
            CameraComponent cameraComponent = new CameraComponent {
                CameraDrawOrder = 17,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(12f, 24f, 640f, 360f),
                NearPlaneDistance = 0.42f,
                FarPlaneDistance = 128f,
                ClearSettings = new CameraClearSettings(true, new float4(0.25f, 0.5f, 0.75f, 1f), true, 1f, true, 9),
                RenderSettings = new CameraRenderSettings {
                    DepthPrepassMode = DepthPrepassMode.Always,
                    ShadowDistance = 128f,
                    PostProcessTier = PostProcessTier.High
                }
            };

            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            return persistenceRegistry.GetDescriptor(cameraComponent).SerializeComponent(cameraComponent, 0, new EntityComponentSaveState());
        }

        /// <summary>
        /// Writes one serialized text component payload.
        /// </summary>
        /// <returns>Serialized text component payload.</returns>
        byte[] WriteTextComponentPayload() {
            return WriteTextComponentPayload(CreateEditorFontReference());
        }

        /// <summary>
        /// Writes one serialized text component payload using the supplied font asset reference.
        /// </summary>
        /// <param name="fontReference">Font reference to persist for the text component.</param>
        /// <returns>Serialized text component payload.</returns>
        byte[] WriteTextComponentPayload(SceneAssetReference fontReference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                FontScale = 2f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true
            };
            System.Reflection.PropertyInfo alignmentProperty = typeof(TextComponent).GetProperty("Alignment");
            Assert.NotNull(alignmentProperty);
            alignmentProperty.SetValue(textComponent, Enum.Parse(alignmentProperty.PropertyType, "Center"));
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(textComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized text component payload using the supplied font reference and build-time sprite-conversion flag.
        /// </summary>
        /// <param name="fontReference">Font reference to persist for the text component.</param>
        /// <param name="convertTextToSprite">True when the authored text should be baked into a sprite during packaging.</param>
        /// <returns>Serialized text component payload.</returns>
        byte[] WriteTextComponentPayload(SceneAssetReference fontReference, bool convertTextToSprite) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                FontScale = 2f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true,
                ConvertTextToSprite = convertTextToSprite
            };
            System.Reflection.PropertyInfo alignmentProperty = typeof(TextComponent).GetProperty("Alignment");
            Assert.NotNull(alignmentProperty);
            alignmentProperty.SetValue(textComponent, Enum.Parse(alignmentProperty.PropertyType, "Center"));
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(textComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized sprite component payload using the supplied texture asset reference.
        /// </summary>
        /// <param name="textureReference">Texture reference to persist for the sprite component.</param>
        /// <returns>Serialized sprite component payload.</returns>
        byte[] WriteSpriteComponentPayload(SceneAssetReference textureReference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SpriteComponent spriteComponent = new SpriteComponent {
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Size = new int2(32, 14),
                Color = new byte4(249, 243, 255, 255),
                RenderOrder2D = 34,
                LayerMask = 1
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Texture", textureReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(spriteComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one automatic reflected rigid-body component payload used by scene-packager tests.
        /// </summary>
        /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <returns>Serialized tagged rigid-body component payload.</returns>
        byte[] WriteAutomaticRigidBody3DComponentPayload(BodyKind3D bodyKind, bool useGravity) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            RigidBody3DComponent component = new RigidBody3DComponent {
                AngularVelocity = float3.Zero,
                BodyKind = bodyKind,
                GravityScale = 1d,
                LinearVelocity = float3.Zero,
                Mass = 1d,
                UseGravity = useGravity
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one automatic reflected box-collider component payload used by scene-packager tests.
        /// </summary>
        /// <param name="size">Full collider size to encode.</param>
        /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
        /// <returns>Serialized tagged box-collider component payload.</returns>
        byte[] WriteAutomaticBoxCollider3DComponentPayload(float3 size, bool isTrigger = false) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            BoxCollider3DComponent component = new BoxCollider3DComponent {
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue,
                DynamicFriction = 0.5d,
                IsTrigger = isTrigger,
                Restitution = 0d,
                Size = size,
                StaticFriction = 0.5d
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one automatic reflected kinematic-motion component payload used by scene-packager tests.
        /// </summary>
        /// <param name="startLocalPosition">Motion path start position.</param>
        /// <param name="endLocalPosition">Motion path end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the path should reverse at the end.</param>
        /// <returns>Serialized tagged kinematic-motion component payload.</returns>
        byte[] WriteAutomaticKinematicMotion3DComponentPayload(
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            KinematicMotion3DComponent component = new KinematicMotion3DComponent {
                EndLocalPosition = endLocalPosition,
                PingPong = pingPong,
                StartLocalPosition = startLocalPosition,
                TravelDurationSeconds = travelDurationSeconds
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one automatic reflected character-controller component payload used by scene-packager tests.
        /// </summary>
        /// <param name="desiredMoveDirection">Desired move direction to encode.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum upward snap height used while climbing support surfaces.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <returns>Serialized tagged character-controller component payload.</returns>
        byte[] WriteAutomaticCharacterController3DComponentPayload(
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CharacterController3DComponent component = new CharacterController3DComponent {
                DesiredMoveDirection = desiredMoveDirection,
                GravityScale = gravityScale,
                GroundSnapDistance = groundSnapDistance,
                MaximumSlopeDegrees = 45d,
                MoveSpeed = moveSpeed,
                StepHeight = stepHeight
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized directional light component payload.
        /// </summary>
        /// <returns>Serialized directional light payload.</returns>
        byte[] WriteDirectionalLightPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            DirectionalLightComponent lightComponent = new DirectionalLightComponent {
                Color = new float4(0.6f, 0.7f, 0.8f, 1f),
                Intensity = 1.8f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.65f,
                ShadowDistance = 72f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized ambient light component payload.
        /// </summary>
        /// <returns>Serialized ambient light payload.</returns>
        byte[] WriteAmbientLightPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            AmbientLightComponent lightComponent = new AmbientLightComponent {
                Color = new float4(0.15f, 0.2f, 0.3f, 1f),
                Intensity = 1.4f,
                ShadowsEnabled = false,
                ShadowMapMode = ShadowMapMode.Disabled,
                ShadowStrength = 0.2f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized point light component payload.
        /// </summary>
        /// <returns>Serialized point light payload.</returns>
        byte[] WritePointLightPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            PointLightComponent lightComponent = new PointLightComponent {
                Color = new float4(1f, 0.9f, 0.7f, 1f),
                Intensity = 2.2f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.5f,
                Range = 18f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized spot light component payload.
        /// </summary>
        /// <returns>Serialized spot light payload.</returns>
        byte[] WriteSpotLightPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SpotLightComponent lightComponent = new SpotLightComponent {
                Color = new float4(0.5f, 0.8f, 1f, 1f),
                Intensity = 3.1f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.7f,
                Range = 22f,
                InnerConeAngleDegrees = 18f,
                OuterConeAngleDegrees = 31f
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(lightComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized rounded rectangle component payload.
        /// </summary>
        /// <returns>Serialized rounded rectangle payload.</returns>
        byte[] WriteRoundedRectPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            RoundedRectComponent roundedRectComponent = new RoundedRectComponent {
                RenderOrder2D = 8,
                LayerMask = 3,
                Corners = RoundedRectCorners.All,
                Rotation = 0.45f,
                Color = new byte4(1, 2, 3, 4),
                SourceRect = new float4(0.2f, 0.3f, 0.4f, 0.5f),
                Size = new int2(280, 120),
                Radius = 14f,
                BorderThickness = 3f,
                FillColor = new byte4(4, 8, 12, 255),
                BorderColor = new byte4(80, 120, 160, 255)
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(roundedRectComponent, 0, null);
            return record.Payload;
        }

        /// <summary>
        /// Asserts one packaged component record uses the shared automatic runtime payload contract for the supplied component type.
        /// </summary>
        /// <param name="packagedRecord">Packaged component record under inspection.</param>
        /// <param name="componentType">Component type expected to back the automatic runtime payload.</param>
        void AssertUsesAutomaticRuntimePayload(SceneComponentAssetRecord packagedRecord, Type componentType) {
            if (packagedRecord == null) {
                throw new ArgumentNullException(nameof(packagedRecord));
            } else if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            Assert.Equal(AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(componentType), packagedRecord.ComponentTypeId);

            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(componentType);
            using MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(schema.Members.Count, reader.ReadInt32());
        }

        /// <summary>
        /// Asserts one automatic runtime payload preserves the expected asset reference for the supplied member name.
        /// </summary>
        /// <param name="packagedRecord">Packaged component record whose automatic runtime payload should be inspected.</param>
        /// <param name="referenceName">Stable reflected member name that owns the asset reference.</param>
        /// <param name="expectedRelativePath">Expected packaged runtime relative path stored for the asset reference.</param>
        void AssertAutomaticRuntimeAssetReference(SceneComponentAssetRecord packagedRecord, string referenceName, string expectedRelativePath) {
            if (packagedRecord == null) {
                throw new ArgumentNullException(nameof(packagedRecord));
            } else if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            } else if (string.IsNullOrWhiteSpace(expectedRelativePath)) {
                throw new ArgumentException("Expected relative path must be provided.", nameof(expectedRelativePath));
            }

            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            Component component = descriptor.DeserializeComponent(packagedRecord, saveComponent, null);

            Assert.True(saveComponent.TryGetComponentState(component, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference(referenceName, out SceneAssetReference reference));
            Assert.Equal(expectedRelativePath, reference.RelativePath);
        }

        /// <summary>
        /// Builds one authored scene containing lights and a rounded-rectangle visual for packaged runtime verification.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the asset.</param>
        /// <returns>Scene asset containing tagged editor payloads for packaged runtime verification.</returns>
        SceneAsset BuildLightingAndUiSceneAsset(string sceneId) {
            return new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Directional",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.DirectionalLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteDirectionalLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "Ambient",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.AmbientLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteAmbientLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "Point",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.PointLightComponent",
                                ComponentIndex = 0,
                                Payload = WritePointLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 4u,
                        Name = "Spot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.SpotLightComponent",
                                ComponentIndex = 0,
                                Payload = WriteSpotLightPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 5u,
                        Name = "RoundedRect",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RoundedRectComponent",
                                ComponentIndex = 0,
                                Payload = WriteRoundedRectPayload()
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
        }

        /// <summary>
        /// Creates one minimal Windows platform definition with the supplied component support metadata.
        /// </summary>
        /// <param name="componentSupportRules">Component support metadata exposed by the platform.</param>
        /// <returns>Minimal Windows platform definition for packager tests.</returns>
        static PlatformDefinition CreateWindowsPlatformDefinition(PlatformComponentSupportRule[] componentSupportRules) {
            if (componentSupportRules == null) {
                throw new ArgumentNullException(nameof(componentSupportRules));
            }

            return new PlatformDefinition(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [])
                ],
                [
                    new PlatformAssetRequirementDefinition(
                        "texture",
                        "Texture",
                        true,
                        ["png", "tga"])
                ],
                componentSupportRules);
        }

        /// <summary>
        /// Creates one minimal Windows platform definition that publishes the standard material schema for cook request verification.
        /// </summary>
        /// <returns>Minimal Windows platform definition with the standard-shader material schema.</returns>
        static PlatformDefinition CreateWindowsMaterialBuilderDefinition() {
            return new PlatformDefinition(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [])
                ],
                [
                    new PlatformAssetRequirementDefinition(
                        "texture",
                        "Texture",
                        true,
                        ["png", "tga"])
                ],
                [
                    new PlatformMaterialSchemaDefinition(
                        "standard-shader",
                        "Standard Shader",
                        ["directx11"],
                        [
                            new PlatformMaterialFieldDefinition(
                                "use-custom-shader",
                                "Use Custom Shader",
                                PlatformMaterialFieldKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "shader-asset-id",
                                "Shader Asset",
                                PlatformMaterialFieldKind.AssetReference,
                                string.Empty,
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "texture-id",
                                "Texture",
                                PlatformMaterialFieldKind.AssetReference,
                                string.Empty,
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "casts-shadow",
                                "Casts Shadow",
                                PlatformMaterialFieldKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "receives-shadow",
                                "Receives Shadow",
                                PlatformMaterialFieldKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "vertex-program",
                                "Vertex Program",
                                PlatformMaterialFieldKind.Text,
                                string.Empty,
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "pixel-program",
                                "Pixel Program",
                                PlatformMaterialFieldKind.Text,
                                string.Empty,
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "base-color",
                                "Base Color",
                                PlatformMaterialFieldKind.Color,
                                "#ffffff",
                                false,
                                [])
                        ])
                ],
                [],
                [],
                [],
                []);
        }

        /// <summary>
        /// Creates one minimal PS2 platform definition that publishes the standard renderer-family material schema for cook verification.
        /// </summary>
        /// <returns>Minimal PS2 platform definition with one generated standard material schema.</returns>
        static PlatformDefinition CreatePs2MaterialBuilderDefinition() {
            return new PlatformDefinition(
                "ps2",
                "PS2",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "ps2-standard-forward",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "ps2-standard-forward",
                        "PS2 Standard Forward",
                        "Standard PS2 forward renderer",
                        [])
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                [
                    new PlatformMaterialSchemaDefinition(
                        "ps2-unlit-textured",
                        "PS2 Unlit Textured",
                        ["ps2-standard-forward"],
                        [
                            new PlatformMaterialFieldDefinition(
                                "texture-relative-path",
                                "Texture",
                                PlatformMaterialFieldKind.Text,
                                string.Empty,
                                false,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "alpha-mode",
                                "Alpha Mode",
                                PlatformMaterialFieldKind.Choice,
                                "opaque",
                                true,
                                ["opaque", "alpha-test", "alpha-blend", "additive"]),
                            new PlatformMaterialFieldDefinition(
                                "double-sided",
                                "Double Sided",
                                PlatformMaterialFieldKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "vertex-color-mode",
                                "Vertex Color",
                                PlatformMaterialFieldKind.Choice,
                                "multiply",
                                true,
                                ["multiply", "ignore"])
                        ])
                ],
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.RootedOrContentRelative));
        }

        /// <summary>
        /// Creates one minimal shared platform definition that uses canonical content-relative packaged runtime paths while preserving the default transform support rules.
        /// </summary>
        /// <param name="componentSupportRules">Component support metadata exposed by the platform.</param>
        /// <returns>Minimal shared platform definition for canonical packaged-path tests.</returns>
        static PlatformDefinition CreateCanonicalPathPlatformDefinition(PlatformComponentSupportRule[] componentSupportRules) {
            if (componentSupportRules == null) {
                throw new ArgumentNullException(nameof(componentSupportRules));
            }

            return new PlatformDefinition(
                "external-platform",
                "External Platform",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug external-platform build",
                        "external-forward",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "external-forward",
                        "External Forward",
                        "Standard external forward renderer",
                        [])
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                componentSupportRules,
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.ContentRelativeOnly));
        }

        /// <summary>
        /// Creates one minimal DS platform definition that publishes the standard fixed-pipeline material schema for cook verification.
        /// </summary>
        /// <returns>Minimal DS platform definition with one generated standard material schema.</returns>
        static PlatformDefinition CreateDsMaterialBuilderDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                [
                    new PlatformBuildProfileDefinition(
                        "ds-default",
                        "DS Default",
                        "Debug player build",
                        "ds-main-2d",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "ds-main-2d",
                        "DS Main 2D",
                        "Nintendo DS fixed-pipeline renderer",
                        [])
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                [
                    new PlatformMaterialSchemaDefinition(
                        "ds-standard-textured",
                        "DS Standard Textured",
                        ["ds-main-2d"],
                        [
                            new PlatformMaterialFieldDefinition(
                                "texture-relative-path",
                                "Texture",
                                PlatformMaterialFieldKind.Text,
                                string.Empty,
                                false,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "double-sided",
                                "Double Sided",
                                PlatformMaterialFieldKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "vertex-color-mode",
                                "Vertex Color",
                                PlatformMaterialFieldKind.Choice,
                                "multiply",
                                true,
                                ["multiply", "ignore"]),
                            new PlatformMaterialFieldDefinition(
                                "base-color",
                                "Base Color",
                                PlatformMaterialFieldKind.Color,
                                "#FFFFFFFF",
                                true,
                                []),
                            new PlatformMaterialFieldDefinition(
                                "lighting-mode",
                                "Lighting",
                                PlatformMaterialFieldKind.Choice,
                                "lit",
                                true,
                                ["lit", "unlit"])
                        ])
                ],
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.ContentRelativeOnly));
        }

        /// <summary>
        /// Creates one shared platform definition that publishes builder-owned font-atlas texture cooking with canonical packaged runtime paths.
        /// </summary>
        /// <param name="defaultSerializedTextureSettings">Default serialized texture settings emitted when the source font has no platform override.</param>
        /// <returns>Platform definition used by builder-owned font-atlas texture packaging tests.</returns>
        static PlatformDefinition CreateBuilderOwnedFontAtlasTexturePlatformDefinition(string defaultSerializedTextureSettings) {
            return new PlatformDefinition(
                "external-platform",
                "External Platform",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug external-platform build",
                        "external-forward",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "external-forward",
                        "External Forward",
                        "Standard external forward renderer",
                        [])
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.ContentRelativeOnly),
                null,
                [
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "external-platform-texture",
                        defaultSerializedTextureSettings)
                ]);
        }

        /// <summary>
        /// Writes the stable editor-font reference used by packaged FPS overlays.
        /// </summary>
        /// <param name="writer">Writer receiving the reference payload.</param>
        void WriteEditorFontReference(EngineBinaryWriter writer) {
            writer.WriteByte(1);
            writer.WriteInt32((int)SceneAssetReferenceSourceKind.Generated);
            writer.WriteString("generated/editor/fonts/ui.hefont");
            writer.WriteString("editor");
            writer.WriteString("ui-font");
        }

        /// <summary>
        /// Records the last material cook request passed into the fake builder.
        /// </summary>
        sealed class RecordingMaterialBuilder : IPlatformAssetBuilder {
            /// <summary>
            /// Gets the factory that creates cooked material results for incoming requests.
            /// </summary>
            readonly Func<PlatformMaterialCookRequest, PlatformMaterialCookResult> CookMaterialFactory;

            /// <summary>
            /// Captures every material cook request received by the fake builder.
            /// </summary>
            readonly List<PlatformMaterialCookRequest> MaterialCookRequestsValue;

            /// <summary>
            /// Creates one recording builder that returns an empty cooked payload.
            /// </summary>
            /// <param name="definition">Builder definition exposed to the packager.</param>
            public RecordingMaterialBuilder(PlatformDefinition definition)
                : this(
                    definition,
                    request => new PlatformMaterialCookResult(Array.Empty<byte>(), Array.Empty<string>())) {
            }

            /// <summary>
            /// Creates one recording builder that returns the supplied cooked material result.
            /// </summary>
            /// <param name="definition">Builder definition exposed to the packager.</param>
            /// <param name="cookMaterialFactory">Factory used to produce the cooked material result.</param>
            public RecordingMaterialBuilder(
                PlatformDefinition definition,
                Func<PlatformMaterialCookRequest, PlatformMaterialCookResult> cookMaterialFactory) {
                Definition = definition ?? throw new ArgumentNullException(nameof(definition));
                CookMaterialFactory = cookMaterialFactory ?? throw new ArgumentNullException(nameof(cookMaterialFactory));
                MaterialCookRequestsValue = new List<PlatformMaterialCookRequest>();
                Descriptor = new PlatformBuilderDescriptor(
                    "test.material.builder",
                    "1.0.0",
                    "windows",
                    new EngineCompatibilityRange("1.0.0", "999.0.0"),
                    new ManifestCompatibilityRange(1, 1),
                    ["windows"],
                    ["debug"]);
            }

            /// <summary>
            /// Gets the builder descriptor exposed to the packager.
            /// </summary>
            public PlatformBuilderDescriptor Descriptor { get; }

            /// <summary>
            /// Gets the platform definition exposed to the packager.
            /// </summary>
            public PlatformDefinition Definition { get; }

            /// <summary>
            /// Gets the last request passed to the material cooker.
            /// </summary>
            public PlatformMaterialCookRequest LastMaterialCookRequest { get; private set; }

            /// <summary>
            /// Gets every material cook request passed to the fake builder in call order.
            /// </summary>
            public IReadOnlyList<PlatformMaterialCookRequest> MaterialCookRequests => MaterialCookRequestsValue;

            /// <summary>
            /// Records the incoming cook request and returns a minimal cooked payload.
            /// </summary>
            /// <param name="request">Material cook request to capture.</param>
            /// <returns>Empty cooked payload with no shader dependencies.</returns>
            public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
                LastMaterialCookRequest = request;
                MaterialCookRequestsValue.Add(request);
                return CookMaterialFactory(request);
            }

            /// <summary>
            /// This recording builder never runs a full platform build.
            /// </summary>
            public Task<PlatformBuildReport> BuildAsync(
                PlatformBuildRequest request,
                IPlatformBuildProgressReporter progressReporter,
                IPlatformBuildDiagnosticReporter diagnosticReporter,
                CancellationToken cancellationToken) {
                throw new NotSupportedException("The recording builder is only used for material cooking tests.");
            }
        }
    }
}



