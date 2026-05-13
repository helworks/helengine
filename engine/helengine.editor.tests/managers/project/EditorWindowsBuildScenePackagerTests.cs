using System.Reflection;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Results;
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
        /// Ensures the real Windows builder compatibility metadata and gameplay script-type resolver can package city-style scripted scene components without relying on a private repro project tree.
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
            scriptTypeResolver.Register("city.menu.DemoDiscReturnToMenuComponent, gameplay", typeof(TestDemoDiscReturnToMenuComponent));
            scriptTypeResolver.Register("city.rendering.DirectionalShadowTowerSpinComponent, gameplay", typeof(TestDirectionalShadowTowerSpinComponent));

            SceneComponentAssetRecord returnToMenuRecord = new SceneComponentAssetRecord {
                ComponentTypeId = "city.menu.DemoDiscReturnToMenuComponent, gameplay",
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
            towerSpinRecord.ComponentTypeId = "city.rendering.DirectionalShadowTowerSpinComponent, gameplay";

            string sceneId = "Scenes/CityStyleCompatibilityScene.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "city-root",
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
                componentRecord => string.Equals(componentRecord.ComponentTypeId, "city.menu.DemoDiscReturnToMenuComponent, gameplay", StringComparison.Ordinal));
            SceneComponentAssetRecord packagedTowerSpinRecord = Assert.Single(
                packagedRoot.Components,
                componentRecord => string.Equals(componentRecord.ComponentTypeId, DirectionalShadowTowerSpinComponent.SerializedComponentTypeId, StringComparison.Ordinal));

            using (MemoryStream payloadStream = new MemoryStream(packagedReturnToMenuRecord.Payload ?? Array.Empty<byte>(), false))
            using (EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian)) {
                Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(1, reader.ReadInt32());
                Assert.Equal((byte)0, reader.ReadByte());
            }

            using (MemoryStream payloadStream = new MemoryStream(packagedTowerSpinRecord.Payload ?? Array.Empty<byte>(), false))
            using (EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian)) {
                Assert.Equal(DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion, reader.ReadByte());
                Assert.Equal(0.75f, reader.ReadSingle());
                Assert.Equal(1.5f, reader.ReadSingle());
            }
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
        /// Ensures tagged mesh-component payloads that reference multiple materials are rewritten into packaged runtime payloads that preserve every material slot.
        /// </summary>
        [Fact]
        public void Package_WhenTaggedMeshUsesMultipleMaterials_RewritesEveryMaterialReference() {
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
                platformDefinition);
            packager.Package(new[] { sceneId }, BuildRootPath);

            SceneAsset packagedSceneAsset;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedSceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset packagedRoot = Assert.Single(packagedSceneAsset.RootEntities);
            SceneComponentAssetRecord meshRecord = Assert.Single(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.MeshComponent", StringComparison.Ordinal));

            using MemoryStream payloadStream = new MemoryStream(meshRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(2, reader.ReadByte());
            SceneAssetReference modelReference = ReadOptionalReference(reader);
            int materialReferenceCount = reader.ReadInt32();
            SceneAssetReference firstMaterialReference = ReadOptionalReference(reader);
            SceneAssetReference secondMaterialReference = ReadOptionalReference(reader);
            byte renderOrder3D = reader.ReadByte();

            Assert.Null(modelReference);
            Assert.Equal(2, materialReferenceCount);
            Assert.Equal("cooked/Materials/SponzaWalls.hasset", firstMaterialReference.RelativePath);
            Assert.Equal("cooked/Materials/SponzaTrim.hasset", secondMaterialReference.RelativePath);
            Assert.Equal((byte)0, renderOrder3D);
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
            MaterialAsset materialAsset = settingsService.LoadMaterialAsset(materialPath, "windows");

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
        public void Package_WhenSceneReferencesGeneratedStandardMaterial_CooksPs2MaterialAsset() {
            string sceneId = "Scenes/GeneratedStandardMaterialScene.helen";
            WriteSceneAsset(sceneId, CreateGeneratedStandardMaterialReference());

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreatePs2MaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    AssetSerializer.SerializeToBytes(new Ps2MaterialAsset {
                        RendererFamilyId = "ps2-standard-forward",
                        LightingMode = Ps2MaterialLightingMode.Unlit,
                        AlphaMode = Ps2MaterialAlphaMode.Opaque,
                        RenderClass = Ps2RenderClass.Opaque,
                        TextureRelativePath = string.Empty,
                        DoubleSided = false,
                        CastShadows = false,
                        UseVertexColor = false,
                        ExpensiveModeAllowed = false,
                        Roughness = 0f,
                        SpecularStrength = 0f,
                        EmissiveStrength = 0f
                    }),
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
            using FileStream stream = File.OpenRead(cookedMaterialPath);
            Ps2MaterialAsset cookedMaterial = Assert.IsType<Ps2MaterialAsset>(AssetSerializer.Deserialize(stream));

            Assert.Equal("ps2-standard-forward", cookedMaterial.RendererFamilyId);
            Assert.Equal(Ps2MaterialLightingMode.Unlit, cookedMaterial.LightingMode);
            Assert.Equal(Ps2MaterialAlphaMode.Opaque, cookedMaterial.AlphaMode);
            Assert.Equal(Ps2RenderClass.Opaque, cookedMaterial.RenderClass);
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

            RecordingMaterialBuilder materialBuilder = new RecordingMaterialBuilder(
                CreatePs2MaterialBuilderDefinition(),
                request => new PlatformMaterialCookResult(
                    AssetSerializer.SerializeToBytes(new Ps2MaterialAsset {
                        RendererFamilyId = "ps2-standard-forward",
                        LightingMode = Ps2MaterialLightingMode.Unlit,
                        AlphaMode = Ps2MaterialAlphaMode.Opaque,
                        RenderClass = Ps2RenderClass.Opaque,
                        TextureRelativePath = request.FieldValues["texture-relative-path"],
                        DoubleSided = false,
                        CastShadows = true,
                        UseVertexColor = false,
                        ExpensiveModeAllowed = false,
                        Roughness = 0f,
                        SpecularStrength = 0f,
                        EmissiveStrength = 0f
                    }),
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
            Assert.Equal(
                "cooked/imported/" + textureAssetId,
                materialBuilder.LastMaterialCookRequest.FieldValues["texture-relative-path"]);
        }

        /// <summary>
        /// Ensures packaged scenes preserve FPS overlay components for the player runtime loader.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsFpsOverlay_LeavesPackagedComponentLoadable() {
            string sceneId = "Scenes/FpsScene.helen";

            WriteSceneAsset(sceneId, "Helengine.FPSComponent", WriteFpsComponentPayload(), new[] { CreateEditorFontReference() });

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

            Assert.Equal("helengine.FPSComponent", packagedScene.RootEntities[0].Components[0].ComponentTypeId);
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

            WriteSceneAsset(sceneId, "Helengine.FPSComponent", WriteOlderVersionFpsComponentPayload(), Array.Empty<SceneAssetReference>());

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));
            Assert.Contains("Unsupported FPS component payload version", exception.Message);
        }

        /// <summary>
        /// Ensures packaged scenes rewrite text component font references into file-backed assets.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsTextComponent_LeavesPackagedComponentLoadable() {
            string sceneId = "Scenes/TextScene.helen";

            WriteSceneAsset(sceneId, "Helengine.TextComponent", WriteTextComponentPayload(), new[] { CreateEditorFontReference() });

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

            Assert.Equal("helengine.TextComponent", packagedScene.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Single(packagedScene.AssetReferences);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedScene.AssetReferences[0].SourceKind);
            Assert.Equal("cooked/fonts/default.hefont", packagedScene.AssetReferences[0].RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont")));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            TextComponent loadedTextComponent = Assert.IsType<TextComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is TextComponent));

            Assert.Equal("Hello world", loadedTextComponent.Text);
            Assert.NotNull(loadedTextComponent.Font);
            Assert.Equal(defaultFont.FontInfo.Name, loadedTextComponent.Font.FontInfo.Name);
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
                MaterialAsset cookedMaterial = Assert.IsType<MaterialAsset>(AssetSerializer.Deserialize(cookedMaterialStream));
                Assert.Equal("default", cookedMaterial.Variant);
            }

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            Assert.NotNull(firstMeshComponent.Material);
        }

        /// <summary>
        /// Ensures a city-style standard-shader material packages with a shader contract the player can resolve.
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
            MaterialAsset cookedMaterial = Assert.IsType<MaterialAsset>(AssetSerializer.Deserialize(cookedMaterialStream));

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
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);

            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            MeshComponent firstMeshComponent = Assert.IsType<MeshComponent>(
                Assert.Single(loadedRoots[0].Components, component => component is MeshComponent));
            RuntimeTexture resolvedTexture = firstMeshComponent.Material.ResolveTexture();
            Assert.NotNull(resolvedTexture);
        }

        /// <summary>
        /// Ensures builder-backed Windows material cooking still copies imported diffuse textures into the player-visible cooked texture location.
        /// </summary>
        [Fact]
        public void Package_WhenBuilderCooksMaterialWithImportedDiffuseTexture_WritesCookedImportedTexture() {
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
            WriteSceneAsset(sceneId, "Helengine.TextComponent", WriteTextComponentPayload(fontReference), new[] { fontReference });

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                new IAssetImporterRegistration[] {
                    new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
                },
                CreatePackagedFontAsset());
            packager.Package(new[] { sceneId }, BuildRootPath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "Fonts", "DemoDiscTitle.hefont");
            Assert.True(File.Exists(cookedFontPath));

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Contains(packagedScene.AssetReferences, reference =>
                string.Equals(reference.RelativePath, "cooked/Fonts/DemoDiscTitle.hefont", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures older binary camera payloads are rejected during packaging.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsLegacyVersionedCameraPayload_ThrowsUnsupportedPayloadVersion() {
            string sceneId = "Scenes/CameraScene.helen";
            byte[] olderVersionPayload;
            using (MemoryStream stream = new MemoryStream()) {
                using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
                writer.WriteByte(2);
                writer.WriteByte(17);
                writer.WriteUInt16(EditorLayerMasks.SceneObjects);
                writer.WriteSingle(12f);
                writer.WriteSingle(24f);
                writer.WriteSingle(640f);
                writer.WriteSingle(360f);
                writer.WriteByte(1);
                writer.WriteSingle(0.25f);
                writer.WriteSingle(0.5f);
                writer.WriteSingle(0.75f);
                writer.WriteSingle(1f);
                writer.WriteByte(1);
                writer.WriteSingle(0.42f);
                writer.WriteByte(1);
                writer.WriteByte(9);
                writer.WriteByte((byte)DepthPrepassMode.Always);
                writer.WriteSingle(128f);
                writer.WriteByte((byte)PostProcessTier.High);
                olderVersionPayload = stream.ToArray();
            }

            WriteSceneAsset(sceneId, "helengine.CameraComponent", olderVersionPayload);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));
            Assert.Contains("Unsupported camera component payload version", exception.Message);
        }

        /// <summary>
        /// <summary>
        /// Ensures older binary mesh payloads are rejected during packaging.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsLegacyVersionedMeshPayload_ThrowsUnsupportedPayloadVersion() {
            string sceneId = "Scenes/MeshScene.helen";
            byte[] olderVersionPayload;
            using (MemoryStream stream = new MemoryStream()) {
                using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
                writer.WriteByte(1);
                writer.WriteByte(0);
                writer.WriteByte(0);
                writer.WriteByte(23);
                olderVersionPayload = stream.ToArray();
            }

            WriteSceneAsset(sceneId, "helengine.MeshComponent", olderVersionPayload);

            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(ProjectRootPath);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));
            Assert.Contains("Unsupported mesh component payload version", exception.Message);
        }

        /// <summary>
        /// Ensures packaged scenes preserve baked demo menu components and their file-backed font dependencies for the player runtime loader.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsMenuComponent_LeavesPackagedComponentLoadable() {
            string menuSceneId = "Scenes/MenuScene.helen";
            string playableSceneId = "Scenes/TestPlayableScene.helen";

            WriteFontAsset("fonts/title.hefont", CreatePackagedFontAsset());
            WriteFontAsset("fonts/body.hefont", CreatePackagedFontAsset());
            WriteSceneAsset(menuSceneId, BuildDemoMenuSceneAsset(menuSceneId));
            WriteEmptySceneAsset(playableSceneId);

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);
            packager.Package(new[] { menuSceneId, playableSceneId }, BuildRootPath);

            string packagedMenuScenePath = GetPackagedScenePath(BuildRootPath, menuSceneId);
            string packagedPlayableScenePath = GetPackagedScenePath(BuildRootPath, playableSceneId);
            Assert.True(File.Exists(packagedMenuScenePath));
            Assert.True(File.Exists(packagedPlayableScenePath));
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "fonts", "title.hefont")));
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "fonts", "body.hefont")));

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedMenuScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = BuildRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));

            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            Assert.Equal(2, loadedRoots.Count);
            Entity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is MenuComponent));
            MenuComponent menuHostComponent = Assert.IsType<MenuComponent>(
                Assert.Single(loadedRoot.Components, component => component is MenuComponent));

            Assert.False(menuHostComponent.IsInitialized);
            Assert.Single(loadedRoot.Children);
            Assert.NotEmpty(loadedRoot.Children[0].Children);
        }

        /// <summary>
        /// Ensures menu components still package successfully when the platform omits explicit support metadata and the packager falls back to the automatic transform path.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformOmitsMenuSupportRules_StillPackagesMenuComponentThroughFallback() {
            string menuSceneId = "Scenes/MenuScene.helen";
            string playableSceneId = "Scenes/TestPlayableScene.helen";

            WriteFontAsset("fonts/title.hefont", CreatePackagedFontAsset());
            WriteFontAsset("fonts/body.hefont", CreatePackagedFontAsset());
            WriteSceneAsset(menuSceneId, BuildDemoMenuSceneAsset(menuSceneId));
            WriteEmptySceneAsset(playableSceneId);

            FontAsset defaultFont = CreatePackagedFontAsset();
            PlatformDefinition platformDefinition = CreateWindowsPlatformDefinition(Array.Empty<PlatformComponentSupportRule>());
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                platformDefinition,
                defaultFont);

            packager.Package(new[] { menuSceneId, playableSceneId }, BuildRootPath);

            string packagedMenuScenePath = GetPackagedScenePath(BuildRootPath, menuSceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedMenuScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());

            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
            Entity loadedRoot = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is MenuComponent));
            Assert.IsType<MenuComponent>(Assert.Single(loadedRoot.Components, component => component is MenuComponent));
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
                        Id = "line-root",
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
                platformDefinition);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
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
                        Id = "script-root",
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
                        Id = "update-root",
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
        /// Ensures the Windows packager rewrites directional-shadow motion script components into built-in player component types.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsDirectionalShadowMotionScriptComponents_RewritesThemToBuiltInPlayerTypes() {
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

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "camera-root",
                        Name = "CameraRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("city.rendering.DirectionalShadowCameraOrbitComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "orbit-root",
                        Name = "OrbitRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("gameplay.rendering.DirectionalShadowOrbitComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "sun-root",
                        Name = "SunRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("city.rendering.DirectionalShadowSunSweepComponent, gameplay", serializedRecord)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "tower-root",
                        Name = "TowerRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateDirectionalShadowComponentRecord("gameplay.rendering.DirectionalShadowTowerSpinComponent, gameplay", serializedRecord)
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
                new FakeScriptTypeResolver(typeof(TestDirectionalShadowMotionScriptComponent)));

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Collection(
                packagedScene.RootEntities,
                cameraRoot => Assert.Equal(DirectionalShadowCameraOrbitComponent.SerializedComponentTypeId, Assert.Single(cameraRoot.Components).ComponentTypeId),
                orbitRoot => Assert.Equal(DirectionalShadowOrbitComponent.SerializedComponentTypeId, Assert.Single(orbitRoot.Components).ComponentTypeId),
                sunRoot => Assert.Equal(DirectionalShadowSunSweepComponent.SerializedComponentTypeId, Assert.Single(sunRoot.Components).ComponentTypeId),
                towerRoot => Assert.Equal(DirectionalShadowTowerSpinComponent.SerializedComponentTypeId, Assert.Single(towerRoot.Components).ComponentTypeId));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
            RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
            IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);

            Assert.Collection(
                loadedRoots,
                cameraRoot => {
                    DirectionalShadowCameraOrbitComponent loadedComponent = Assert.IsType<DirectionalShadowCameraOrbitComponent>(Assert.Single(cameraRoot.Components));
                    Assert.Equal(component.OrbitCenter, loadedComponent.OrbitCenter);
                    Assert.Equal(component.LookDownPitchRadians, loadedComponent.LookDownPitchRadians);
                },
                orbitRoot => {
                    DirectionalShadowOrbitComponent loadedComponent = Assert.IsType<DirectionalShadowOrbitComponent>(Assert.Single(orbitRoot.Components));
                    Assert.Equal(component.OrbitRadius, loadedComponent.OrbitRadius);
                    Assert.Equal(component.AngularSpeedRadians, loadedComponent.AngularSpeedRadians);
                },
                sunRoot => {
                    DirectionalShadowSunSweepComponent loadedComponent = Assert.IsType<DirectionalShadowSunSweepComponent>(Assert.Single(sunRoot.Components));
                    Assert.Equal(component.MinYawRadians, loadedComponent.MinYawRadians);
                    Assert.Equal(component.PitchRadians, loadedComponent.PitchRadians);
                },
                towerRoot => {
                    DirectionalShadowTowerSpinComponent loadedComponent = Assert.IsType<DirectionalShadowTowerSpinComponent>(Assert.Single(towerRoot.Components));
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
                        Id = "axis-root",
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
            Assert.DoesNotContain(packagedRoot.Components, record => string.Equals(record.ComponentTypeId, DirectionalShadowTowerSpinComponent.SerializedComponentTypeId, StringComparison.Ordinal));

            using MemoryStream payloadStream = new MemoryStream(packagedRecord.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(payloadStream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(3, reader.ReadInt32());
            Assert.Equal((float)(Math.PI / 2.0), reader.ReadSingle());
            Assert.Equal(new float3(0f, 1f, 0f), reader.ReadFloat3());
            Assert.Equal((byte)0, reader.ReadByte());
        }

        /// <summary>
        /// Ensures builder-supplied support metadata does not remove the default pass-through physics component support rules required by packaged runtime scenes.
        /// </summary>
        [Fact]
        public void Package_WhenPlatformOmitsPassThroughPhysicsSupportRules_PreservesDefaultPhysicsSupportRules() {
            string sceneId = "Scenes/PhysicsScene.helen";
            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "physics-root",
                        Name = "PhysicsRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteRigidBody3DComponentPayload(BodyKind3D.Dynamic, true)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteBoxCollider3DComponentPayload(new float3(1f, 2f, 3f))
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
                platformDefinition);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedScenePath = GetPackagedScenePath(BuildRootPath, sceneId);
            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(packagedScenePath)) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneEntityAsset loadedRoot = Assert.Single(packagedScene.RootEntities);
            SceneComponentAssetRecord rigidBodyRecord = Assert.Single(
                loadedRoot.Components,
                componentRecord => string.Equals(componentRecord.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            SceneComponentAssetRecord boxColliderRecord = Assert.Single(
                loadedRoot.Components,
                componentRecord => string.Equals(componentRecord.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));

            Assert.Equal(WriteRigidBody3DComponentPayload(BodyKind3D.Dynamic, true), rigidBodyRecord.Payload);
            Assert.Equal(WriteBoxCollider3DComponentPayload(new float3(1f, 2f, 3f)), boxColliderRecord.Payload);
        }

        /// <summary>
        /// Ensures packaged lights and rounded rectangles are rewritten into strict runtime payloads that still load correctly.
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

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
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
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                runtimeContentManager,
                BuildRootPath,
                ShaderCompileTarget.DirectX11);
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
                    new SceneAssetReference {
                        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                        RelativePath = sourceModelRelativePath,
                        ProviderId = string.Empty,
                        AssetId = string.Empty
                    }
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
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
            Assert.Equal("cooked/imported/Models/Sponza.hasset", packagedScene.AssetReferences[0].RelativePath);
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
                        Id = "root-entity",
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
        /// Ensures packaging for one target platform materializes platform-only components into the packaged runtime scene and strips the editor-only existence override metadata.
        /// </summary>
        [Fact]
        public void Package_WhenSceneEntityDefinesWindowsPlatformOnlyCamera_AppendsTheAddedComponentToThePackagedScene() {
            string sceneId = "Scenes/PlatformAddedCamera.helen";
            SceneComponentAssetRecord cameraRecord = CreateTaggedCameraComponentRecord();
            cameraRecord.ComponentKey = "windows-camera";

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
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
                component => string.Equals(component.ComponentTypeId, "helengine.CameraComponent", StringComparison.Ordinal));

            Assert.NotNull(packagedCameraRecord);
            Assert.Empty(packagedRoot.PlatformComponentOverrides);
        }

        /// <summary>
        /// Ensures tagged camera payloads on ordinary authored scene components package into runtime scenes without failing the shared transform path.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsTaggedCameraComponent_WritesPackagedScene() {
            string sceneId = "Scenes/TaggedCameraScene.helen";
            SceneComponentAssetRecord cameraRecord = CreateTaggedCameraComponentRecord();

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "camera-root",
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
                component => string.Equals(component.ComponentTypeId, "helengine.CameraComponent", StringComparison.Ordinal));

            Assert.NotNull(packagedCameraRecord);
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
        /// Ensures zero-member automatic script components authored in runtime payload form package without being re-read as tagged editor payloads.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsRuntimeEncodedZeroMemberAutomaticScriptComponent_PackagesSuccessfully() {
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
                        Id = "return-root",
                        Name = "ReturnRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "city.menu.DemoDiscReturnToMenuComponent, gameplay",
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
                new FakeScriptTypeResolver(typeof(TestDemoDiscReturnToMenuComponent)));

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);
            Assert.NotNull(result);
            Assert.True(File.Exists(GetPackagedScenePath(BuildRootPath, sceneId)));
        }

        /// <summary>
        /// Ensures runtime directional-shadow tower-spin records remain supported when a scene already contains the built-in packaged component id.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsRuntimeDirectionalShadowTowerSpin_PreservesSupportedRuntimeRecord() {
            string sceneId = "Scenes/RuntimeDirectionalShadowTowerSpin.helen";
            byte[] payload;
            using (MemoryStream stream = new MemoryStream()) {
                using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
                writer.WriteByte(DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion);
                DirectionalShadowMotionComponentScenePayloadSerializer.WriteTowerSpin(
                    writer,
                    new DirectionalShadowTowerSpinComponent {
                        BaseYawRadians = 0.5f,
                        AngularSpeedRadians = 1.25f
                    });
                payload = stream.ToArray();
            }

            WriteSceneAsset(sceneId, new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "tower-root",
                        Name = "TowerRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = DirectionalShadowTowerSpinComponent.SerializedComponentTypeId,
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
                platformDefinition);

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);
            Assert.NotNull(result);

            SceneAsset packagedScene;
            using (FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId))) {
                packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord packagedRecord = Assert.Single(Assert.Single(packagedScene.RootEntities).Components);
            Assert.Equal(DirectionalShadowTowerSpinComponent.SerializedComponentTypeId, packagedRecord.ComponentTypeId);
            Assert.Equal(payload, packagedRecord.Payload);
        }

        /// <summary>
        /// Ensures the build packager exports a default packaged font asset that the runtime content pipeline can reload.
        /// </summary>
        [Fact]
        public void Package_WhenDefaultFontAssetIsAvailable_WritesPackagedDefaultFontThatCanBeReloaded() {
            string sceneId = "Scenes/FontScene.helen";
            WriteSceneAsset(sceneId, "Helengine.FPSComponent", WriteFpsComponentPayload(), new[] { CreateEditorFontReference() });

            FontAsset defaultFont = CreatePackagedFontAsset();
            EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath,
                Array.Empty<IAssetImporterRegistration>(),
                defaultFont);

            packager.Package(new[] { sceneId }, BuildRootPath);

            string packagedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "default.hefont");
            Assert.True(File.Exists(packagedFontPath));

            InitializeRuntimeCore(BuildRootPath);
            ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
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
        /// Ensures the default scene packager preserves the current pass-through physics records required by the 3D box-body runtime.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DBoxBody_PreservesPhysicsComponentRecords() {
            string sceneId = "Scenes/PhysicsBoxes.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "ground-entity",
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = new float3(8f, 1f, 8f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteBoxCollider3DComponentPayload(new float3(8f, 1f, 8f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "box-entity",
                        Name = "Box",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteRigidBody3DComponentPayload(BodyKind3D.Dynamic, true)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteBoxCollider3DComponentPayload(new float3(1f, 1f, 1f))
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

            Assert.Contains(packagedScene.RootEntities[0].Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(packagedScene.RootEntities[0].Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(packagedScene.RootEntities[1].Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(packagedScene.RootEntities[1].Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Equal((uint)PhysicsSceneFeatureFlags3D.BoxBoxContact, packagedScene.Physics3DSceneFeatureFlags);
        }

        /// <summary>
        /// Ensures the default scene packager preserves the current pass-through physics records required by the runtime kinematic-motion path.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DKinematicMotion_PreservesPhysicsComponentRecords() {
            string sceneId = "Scenes/PhysicsKinematic.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "kinematic-entity",
                        Name = "KinematicPusher",
                        LocalPosition = new float3(-2f, 0.5f, 0f),
                        LocalScale = new float3(1.5f, 1f, 1.5f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteRigidBody3DComponentPayload(BodyKind3D.Kinematic, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteBoxCollider3DComponentPayload(new float3(1.5f, 1f, 1.5f))
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.KinematicMotion3DComponent",
                                ComponentIndex = 2,
                                Payload = WriteKinematicMotion3DComponentPayload(new float3(-2f, 0.5f, 0f), new float3(0.5f, 0.5f, 0f), 1d, true)
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

            Assert.Contains(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.RigidBody3DComponent", StringComparison.Ordinal));
            Assert.Contains(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.BoxCollider3DComponent", StringComparison.Ordinal));
            Assert.Contains(packagedRoot.Components, component => string.Equals(component.ComponentTypeId, "helengine.KinematicMotion3DComponent", StringComparison.Ordinal));
            Assert.Equal((uint)PhysicsSceneFeatureFlags3D.KinematicMotion, packagedScene.Physics3DSceneFeatureFlags);
        }

        /// <summary>
        /// Ensures the default scene packager preserves the current pass-through physics records required by the runtime character-controller path.
        /// </summary>
        [Fact]
        public void Package_WhenSceneContainsPhysics3DCharacterController_PreservesPhysicsComponentRecords() {
            string sceneId = "Scenes/PhysicsCharacterController.helen";
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "ground-entity",
                        Name = "Ground",
                        LocalPosition = new float3(0f, -0.5f, 0f),
                        LocalScale = new float3(8f, 1f, 8f),
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteBoxCollider3DComponentPayload(new float3(8f, 1f, 8f))
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "controller-entity",
                        Name = "Controller",
                        LocalPosition = new float3(0f, 1f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.CharacterController3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteCharacterController3DComponentPayload(new float3(1f, 0f, 0f), 4.5d, 1d, 0.4d, 0.25d)
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

            Assert.Contains(packagedScene.RootEntities[1].Components, component => string.Equals(component.ComponentTypeId, "helengine.CharacterController3DComponent", StringComparison.Ordinal));
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
                        Id = "trigger-entity",
                        Name = "Trigger",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.RigidBody3DComponent",
                                ComponentIndex = 0,
                                Payload = WriteRigidBody3DComponentPayload(BodyKind3D.Static, false)
                            },
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.BoxCollider3DComponent",
                                ComponentIndex = 1,
                                Payload = WriteBoxCollider3DComponentPayload(new float3(3f, 2f, 3f), true)
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
                        Id = "root-entity",
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
                        Id = "second-root-entity",
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
                        Id = "root-entity",
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
                        Id = "root-entity",
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
        /// Creates the generated scene reference used for the editor's built-in font asset.
        /// </summary>
        /// <returns>Generated editor font scene reference.</returns>
        static SceneAssetReference CreateEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hefont",
                ProviderId = "editor",
                AssetId = "ui-font"
            };
        }

        /// <summary>
        /// Creates the generated scene reference used for the engine's built-in standard material.
        /// </summary>
        /// <returns>Generated engine standard-material scene reference.</returns>
        static SceneAssetReference CreateGeneratedStandardMaterialReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            };
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
        void InitializeRuntimeCore(string contentRootPath) {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = contentRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates the file-backed font reference used by packaged demo-disc menu scenes.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <returns>File-backed scene reference.</returns>
        static SceneAssetReference CreateFileFontReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
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
                        Id = "root-entity",
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
                "standard-shader",
                useCustomShader: false,
                "#FFFFFFFF");

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(materialPath, settings);
        }

        /// <summary>
        /// Writes one city-style standard material document that mirrors the colored cube-grid authored content.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to write.</param>
        void WriteCityStyleStandardMaterialAsset(string materialRelativePath, string diffuseTextureAssetId = "") {
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

            MaterialAssetImportSettings settings = CreateMaterialSettings(
                materialRelativePath,
                "ForwardStandardShader",
                diffuseTextureAssetId,
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
            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent {
                Material = new TestRuntimeMaterial()
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Material", new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = materialRelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            });

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);
            return record.Payload;
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

            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent {
                Material = new TestRuntimeMaterial()
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Material", materialReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one mesh-component payload that points at two tagged material references.
        /// </summary>
        /// <param name="firstMaterialRelativePath">Project-relative first material path to encode.</param>
        /// <param name="secondMaterialRelativePath">Project-relative second material path to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(string firstMaterialRelativePath, string secondMaterialRelativePath) {
            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent();
            meshComponent.SetMaterials(new RuntimeMaterial[] {
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial()
            });

            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Material", new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = firstMaterialRelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            });
            saveState.SetAssetReference("Material[1]", new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = secondMaterialRelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            });

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized scene asset that contains a tagged mesh component with two material references.
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
                        Id = "root-entity",
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
        /// Reads one optional scene asset reference from a packaged runtime payload.
        /// </summary>
        /// <param name="reader">Payload reader positioned at the optional reference flag.</param>
        /// <returns>Decoded scene asset reference.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Writes one serialized FPS component payload.
        /// </summary>
        /// <returns>Serialized FPS component payload.</returns>
        byte[] WriteFpsComponentPayload() {
            FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();
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
        /// Writes one older FPS component payload that predates packaged font references.
        /// </summary>
        /// <returns>Serialized older-version FPS component payload.</returns>
        byte[] WriteOlderVersionFpsComponentPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(0.5d));
            writer.WriteInt2(new int2(8, 6));
            writer.WriteByte(250);
            return stream.ToArray();
        }

        /// <summary>
        /// Creates one tagged camera component record that matches the modern editor scene payload shape.
        /// </summary>
        /// <returns>Serialized tagged camera component record.</returns>
        SceneComponentAssetRecord CreateTaggedCameraComponentRecord() {
            CameraRenderSettings renderSettings = new CameraRenderSettings {
                DepthPrepassMode = DepthPrepassMode.Always,
                ShadowDistance = 128f,
                PostProcessTier = PostProcessTier.High
            };
            CameraClearSettings clearSettings = new CameraClearSettings(true, new float4(0.25f, 0.5f, 0.75f, 1f), true, 1f, true, 9);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(17));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(EditorLayerMasks.SceneObjects));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(12f, 24f, 640f, 360f)));
            writer.WriteField("NearPlaneDistance", fieldWriter => fieldWriter.WriteSingle(0.42f));
            writer.WriteField("FarPlaneDistance", fieldWriter => fieldWriter.WriteSingle(128f));
            writer.WriteField("ClearSettings", fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(fieldWriter, clearSettings));
            writer.WriteField("RenderSettings", fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(fieldWriter, renderSettings));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CameraComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };
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
            TextComponentPersistenceDescriptor descriptor = new TextComponentPersistenceDescriptor();
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7,
                SelectionEnabled = true
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Font", fontReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(textComponent, 0, saveState);
            return record.Payload;
        }

        /// <summary>
        /// Writes one serialized rigid-body component payload used by scene-packager tests.
        /// </summary>
        /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <returns>Serialized rigid-body component payload.</returns>
        byte[] WriteRigidBody3DComponentPayload(BodyKind3D bodyKind, bool useGravity) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte((byte)bodyKind);
            writer.WriteByte(useGravity ? (byte)1 : (byte)0);
            writer.WriteSingle(1f);
            writer.WriteSingle(1f);
            writer.WriteFloat3(float3.Zero);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized box-collider component payload used by scene-packager tests.
        /// </summary>
        /// <param name="size">Full collider size to encode.</param>
        /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
        /// <returns>Serialized box-collider component payload.</returns>
        byte[] WriteBoxCollider3DComponentPayload(float3 size, bool isTrigger = false) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(2);
            writer.WriteFloat3(size);
            writer.WriteUInt16(1);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteByte(isTrigger ? (byte)1 : (byte)0);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized kinematic-motion component payload used by scene-packager tests.
        /// </summary>
        /// <param name="startLocalPosition">Motion path start position.</param>
        /// <param name="endLocalPosition">Motion path end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the path should reverse at the end.</param>
        /// <returns>Serialized kinematic-motion component payload.</returns>
        byte[] WriteKinematicMotion3DComponentPayload(
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(startLocalPosition);
            writer.WriteFloat3(endLocalPosition);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(travelDurationSeconds));
            writer.WriteByte(pingPong ? (byte)1 : (byte)0);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized character-controller component payload used by scene-packager tests.
        /// </summary>
        /// <param name="desiredMoveDirection">Desired move direction to encode.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum upward snap height used while climbing support surfaces.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <returns>Serialized character-controller component payload.</returns>
        byte[] WriteCharacterController3DComponentPayload(
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(desiredMoveDirection);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(moveSpeed));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(gravityScale));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(stepHeight));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(groundSnapDistance));
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one serialized directional light component payload.
        /// </summary>
        /// <returns>Serialized directional light payload.</returns>
        byte[] WriteDirectionalLightPayload() {
            DirectionalLightComponentPersistenceDescriptor descriptor = new DirectionalLightComponentPersistenceDescriptor();
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
            AmbientLightComponentPersistenceDescriptor descriptor = new AmbientLightComponentPersistenceDescriptor();
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
            PointLightComponentPersistenceDescriptor descriptor = new PointLightComponentPersistenceDescriptor();
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
            SpotLightComponentPersistenceDescriptor descriptor = new SpotLightComponentPersistenceDescriptor();
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
            RoundedRectComponentPersistenceDescriptor descriptor = new RoundedRectComponentPersistenceDescriptor();
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
        /// Builds one baked demo menu scene asset for packaged runtime-load verification.
        /// </summary>
        /// <param name="sceneId">Scene id assigned to the baked asset.</param>
        /// <returns>Baked demo menu scene asset.</returns>
        SceneAsset BuildDemoMenuSceneAsset(string sceneId) {
            DemoMenuSceneAssetFactory factory = new DemoMenuSceneAssetFactory();
            return factory.BuildSceneAsset(
                sceneId,
                typeof(TestMenuDefinitionProvider).AssemblyQualifiedName,
                new MenuDefinition(
                    "Demo",
                    "Packager",
                    "main",
                    "fonts/title.hefont",
                    "fonts/body.hefont",
                    new byte4(10, 10, 20, 255),
                    new byte4(30, 30, 50, 255),
                    new byte4(60, 60, 90, 255),
                    new byte4(120, 120, 255, 255),
                    new byte4(80, 180, 200, 255),
                    new byte4(255, 255, 255, 255),
                    new byte4(210, 210, 220, 255),
                    new[] {
                        new MenuPanelDefinition(
                            "main",
                            "Main Menu",
                            "Packager test panel.",
                            4,
                            new[] {
                                new MenuItemDefinition("select-scene", "Select Scene", "Loads a scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "Scenes/TestPlayableScene.helen")),
                                new MenuItemDefinition("back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                            })
                    }));
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
                        Id = "directional-root",
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
                        Id = "ambient-root",
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
                        Id = "point-root",
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
                        Id = "spot-root",
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
                        Id = "rounded-root",
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
                Array.Empty<PlatformMediaProfileDefinition>());
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
            /// Records the incoming cook request and returns a minimal cooked payload.
            /// </summary>
            /// <param name="request">Material cook request to capture.</param>
            /// <returns>Empty cooked payload with no shader dependencies.</returns>
            public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
                LastMaterialCookRequest = request;
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


