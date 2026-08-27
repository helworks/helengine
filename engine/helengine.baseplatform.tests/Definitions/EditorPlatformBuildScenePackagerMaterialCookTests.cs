using System.Reflection;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.tests.Builders;
using helengine;
using helengine.editor;
using Xunit;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies scene packaging rewrites material assets through the builder-owned material cook contract.
/// </summary>
public sealed class EditorPlatformBuildScenePackagerMaterialCookTests : IDisposable {
    /// <summary>
    /// Temporary project root used for scene-packager tests.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Temporary build root used for scene-packager tests.
    /// </summary>
    readonly string BuildRootPath;

    /// <summary>
    /// Initializes one isolated project workspace for packager verification.
    /// </summary>
    public EditorPlatformBuildScenePackagerMaterialCookTests() {
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-material-cook-packager-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures packaging uses the target platform sidecar settings instead of stale top-level material fields when custom shader mode is enabled.
        /// </summary>
        [Fact]
    public void Package_when_builder_is_available_uses_target_platform_material_sidecar_values() {
        string sceneId = "Scenes/TestScene.helen";
        string materialRelativePath = "Materials/TestMaterial.helmat";
        SeedBuiltInStandardShaderAsset(ShaderCompileTarget.DirectX11);
        WriteMaterialAsset(materialRelativePath, "StaleShader");
        WriteMaterialSettings(materialRelativePath);
        WriteSceneAsset(sceneId, materialRelativePath);

        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();
        EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
            ProjectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            "windows",
            builder,
            "debug",
            "directx11");
        EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

        string packagedMaterialPath = Path.Combine(BuildRootPath, "cooked", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        using FileStream stream = new FileStream(packagedMaterialPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ShaderMaterialAsset packagedMaterial = Assert.IsType<ShaderMaterialAsset>(global::helengine.editor.AssetSerializer.Deserialize(stream));

        Assert.Equal("CookedShader", packagedMaterial.ShaderAssetId);
        Assert.Equal("CookedShader.vs", packagedMaterial.VertexProgram);
        Assert.Equal("CookedShader.ps", packagedMaterial.PixelProgram);
        Assert.Equal("Mesh", packagedMaterial.Variant);
        Assert.Single(packagedMaterial.ConstantBuffers);
        Assert.Equal("BaseColorBuffer", packagedMaterial.ConstantBuffers[0].Name);
        Assert.Equal(16, packagedMaterial.ConstantBuffers[0].Data.Length);
        Assert.Equal(new[] { "ForwardStandardShader", "CookedShader" }, result.ReferencedShaderDependencies.Select(dependency => dependency.ShaderAssetId));
    }

    /// <summary>
    /// Ensures a cooked material output is declared as a material independently from its shader dependency identifiers.
    /// </summary>
    [Fact]
    public void Package_whenBuilderCooksMaterial_reportsExplicitMaterialOutput() {
        string sceneId = "Scenes/TestScene.helen";
        SeedBuiltInStandardShaderAsset(ShaderCompileTarget.DirectX11);
        WriteEmptySceneAsset(sceneId);

        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();
        EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
            ProjectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            "windows",
            builder,
            "debug",
            "directx11");
        EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

        PlatformCookedArtifactDeclaration materialOutput = Assert.Single(
            result.CookedArtifactDeclarations,
            declaration => string.Equals(declaration.RelativePath, "cooked/engine/materials/standard.hasset", StringComparison.Ordinal));

        Assert.Equal("material", materialOutput.ArtifactKind);
        Assert.Equal("engine:material:standard", materialOutput.LogicalArtifactId);
        Assert.Equal(new[] { "ForwardStandardShader" }, result.ReferencedShaderDependencies.Select(dependency => dependency.ShaderAssetId));
        Assert.Equal("ForwardStandardShader", Assert.Single(result.ReferencedShaderDependencies).ShaderAssetId);
    }

    /// <summary>
    /// Ensures packaging seeds missing material sidecars from the material mirrored material fields when a builder is available.
    /// </summary>
    [Fact]
    public void Package_when_builder_is_available_and_material_sidecar_is_missing_seeds_platform_material_settings_from_mirrored_fields() {
        string materialRelativePath = "Materials/TestMaterial.helmat";
        WriteMaterialAsset(materialRelativePath, "StaleShader");

        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();
        EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
            ProjectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            "windows",
            builder,
            "debug",
            "directx11");
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        ShaderMaterialAsset materialAsset = new MaterialAssetSettingsService().LoadMaterialAsset(materialPath, "windows");
        MethodInfo loadMethod = typeof(EditorPlatformBuildScenePackager).GetMethod(
            "LoadMaterialSettingsForCook",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MaterialAssetImportSettings settings = Assert.IsType<MaterialAssetImportSettings>(loadMethod.Invoke(packager, [materialPath, materialRelativePath, materialAsset]));

        Assert.Equal("standard-shader", settings.Processor.Platforms["windows"].SchemaId);
        Assert.Equal("false", settings.Processor.Platforms["windows"].FieldValues["use-custom-shader"]);
        Assert.Equal("StaleShader", settings.Processor.Platforms["windows"].FieldValues["shader-asset-id"]);
        Assert.Equal("StaleShader.vs", settings.Processor.Platforms["windows"].FieldValues["vertex-program"]);
        Assert.Equal("StaleShader.ps", settings.Processor.Platforms["windows"].FieldValues["pixel-program"]);
        Assert.False(File.Exists(materialPath + ".hasset"));
    }

    /// <summary>
    /// Ensures scene-level material asset references also route through the builder-owned material cook path instead of being copied as raw authored assets.
    /// </summary>
    [Fact]
    public void Package_when_scene_level_asset_reference_targets_material_routes_reference_through_material_cook_path() {
        string sceneId = "Scenes/TestScene.helen";
        string materialRelativePath = "Materials/TestMaterial.helmat";
        SeedBuiltInStandardShaderAsset(ShaderCompileTarget.DirectX11);
        WriteMaterialAsset(materialRelativePath, "StaleShader");
        WriteMaterialSettings(materialRelativePath);
        WriteSceneAssetWithSceneLevelMaterialReference(sceneId, materialRelativePath);

        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();
        EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
            ProjectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            "windows",
            builder,
            "debug",
            "directx11");
        packager.Package(new[] { sceneId }, BuildRootPath);

        string packagedScenePath = Path.Combine(BuildRootPath, "cooked", "scenes", "TestScene.hasset");
        using FileStream sceneStream = new FileStream(packagedScenePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        SceneAsset packagedSceneAsset = Assert.IsType<SceneAsset>(global::helengine.PackagedAssetBinarySerializer.Deserialize(sceneStream));
        SceneAssetReference packagedReference = Assert.Single(packagedSceneAsset.AssetReferences);
        Assert.Equal("cooked/materials/testmaterial.helmat", packagedReference.RelativePath);

        string packagedMaterialPath = Path.Combine(BuildRootPath, "cooked", "materials", "testmaterial.helmat");
        using FileStream materialStream = new FileStream(packagedMaterialPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ShaderMaterialAsset packagedMaterial = Assert.IsType<ShaderMaterialAsset>(global::helengine.editor.AssetSerializer.Deserialize(materialStream));
        Assert.Equal("CookedShader", packagedMaterial.ShaderAssetId);
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
            RootEntities = [
                new SceneEntityAsset {
                    Id = 1,
                    Name = "Root",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = [
                        new SceneComponentAssetRecord {
                            ComponentTypeId = "helengine.MeshComponent",
                            ComponentIndex = 0,
                            Payload = WriteMeshComponentPayload(materialRelativePath)
                        }
                    ],
                    Children = Array.Empty<SceneEntityAsset>()
                }
            ]
        };

        using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        global::helengine.editor.AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one empty scene asset so packaging exercises generated asset output without component serialization dependencies.
    /// </summary>
    /// <param name="sceneId">Scene asset id to write.</param>
    void WriteEmptySceneAsset(string sceneId) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

        SceneAsset sceneAsset = new SceneAsset {
            Id = sceneId,
            RootEntities = Array.Empty<SceneEntityAsset>()
        };

        using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        global::helengine.editor.AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one serialized scene asset that carries the supplied material path only through the scene-level asset reference list.
    /// </summary>
    /// <param name="sceneId">Scene asset id to write.</param>
    /// <param name="materialRelativePath">Project-relative material path to reference from the scene-level asset reference list.</param>
    void WriteSceneAssetWithSceneLevelMaterialReference(string sceneId, string materialRelativePath) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

        SceneAsset sceneAsset = new SceneAsset {
            Id = sceneId,
            AssetReferences = [
                global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                    "00112233445566778899aabbccddeeff",
                    materialRelativePath,
                    "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")
            ],
            RootEntities = Array.Empty<SceneEntityAsset>()
        };

        using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        global::helengine.editor.AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one current material common-settings document whose shared fields represent the authored material defaults.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path to write.</param>
    /// <param name="shaderAssetId">Shader asset id referenced by the material defaults.</param>
    void WriteMaterialAsset(string materialRelativePath, string shaderAssetId) {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

        MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument();
        document.Importer.ImporterId = "helengine.material";
        document.Importer.AssetId = materialRelativePath;
        document.Processor.SchemaId = "standard-shader";
        document.Processor.FieldValues["use-custom-shader"] = "false";
        document.Processor.FieldValues["shader-asset-id"] = shaderAssetId;
        document.Processor.FieldValues["vertex-program"] = string.Concat(shaderAssetId, ".vs");
        document.Processor.FieldValues["pixel-program"] = string.Concat(shaderAssetId, ".ps");
        document.Processor.FieldValues["variant"] = "Mesh";
        document.Processor.FieldValues["base-color"] = "#336699";

        using FileStream stream = new FileStream(materialPath, FileMode.Create, FileAccess.Write, FileShare.None);
        MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
    }

    /// <summary>
    /// Writes one current per-platform material override document whose builder-owned field values point at the cooked shader.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path whose sidecar should be written.</param>
    void WriteMaterialSettings(string materialRelativePath) {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        MaterialAssetPlatformOverrideDocument document = new MaterialAssetPlatformOverrideDocument();
        document.PlatformId = "windows";
        document.Processor.SchemaId = "standard-shader";
        document.Processor.FieldValues["use-custom-shader"] = "true";
        document.Processor.FieldValues["shader-asset-id"] = "CookedShader";
        document.Processor.FieldValues["vertex-program"] = "CookedShader.vs";
        document.Processor.FieldValues["pixel-program"] = "CookedShader.ps";
        document.Processor.FieldValues["base-color"] = "#336699";

        using FileStream stream = new FileStream(materialPath + ".windows.hasset", FileMode.Create, FileAccess.Write, FileShare.None);
        MaterialAssetPlatformOverrideDocumentBinarySerializer.Serialize(stream, document);
    }

    /// <summary>
    /// Seeds the built-in shader library cache with a prebuilt ForwardStandardShader shader asset so the test does not invoke runtime compilation.
    /// </summary>
    /// <param name="target">Shader compile target to seed.</param>
    void SeedBuiltInStandardShaderAsset(ShaderCompileTarget target) {
        string shaderPath = EditorBuiltInShaderAssetLibrary.ResolveShaderPath("ForwardStandardShader.hlsl");
        string cacheKey = string.Concat(target.ToString(), "|", shaderPath);
        ShaderAsset shaderAsset = new ShaderAsset {
            Id = "ForwardStandardShader",
            Name = "ForwardStandardShader",
            TargetName = ShaderTargetNames.GetTargetName(target),
            Programs = [
                new ShaderProgramAsset {
                    Name = "ForwardStandardShader.vs",
                    Stage = ShaderStage.Vertex,
                    EntryPoint = "VS",
                    Bindings = Array.Empty<ShaderBindingAsset>(),
                    Inputs = Array.Empty<ShaderVertexElementAsset>(),
                    Outputs = Array.Empty<ShaderVertexElementAsset>(),
                    Variants = [
                        new ShaderVariantAsset {
                            Name = "default",
                            Defines = Array.Empty<string>()
                        }
                    ]
                }
            ],
            Binaries = [
                new ShaderBinaryAsset {
                    ProgramName = "ForwardStandardShader.vs",
                    Stage = ShaderStage.Vertex,
                    TargetName = ShaderTargetNames.GetTargetName(target),
                    Variant = "default",
                    Bytecode = [1, 2, 3, 4]
                }
            ]
        };

        FieldInfo cacheField = typeof(EditorBuiltInShaderAssetLibrary).GetField("ShaderAssetsByKey", BindingFlags.Static | BindingFlags.NonPublic);
        Dictionary<string, ShaderAsset> cache = Assert.IsType<Dictionary<string, ShaderAsset>>(cacheField.GetValue(null));
        cache[cacheKey] = shaderAsset;
    }

    /// <summary>
    /// Writes one mesh-component payload that points at the supplied material path.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path to encode.</param>
    /// <returns>Serialized mesh component payload.</returns>
    byte[] WriteMeshComponentPayload(string materialRelativePath) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        MeshComponent meshComponent = new MeshComponent {
            Materials = [new RuntimeMaterial()]
        };
        EntityComponentSaveState saveState = new EntityComponentSaveState();
        saveState.SetAssetReference(
            "Materials[0]",
            global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "00112233445566778899aabbccddeeff",
                materialRelativePath,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
        return descriptor.SerializeComponent(meshComponent, 0, saveState).Payload;
    }
}


