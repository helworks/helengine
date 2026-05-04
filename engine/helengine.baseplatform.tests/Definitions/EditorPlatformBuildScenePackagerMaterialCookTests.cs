using System.Reflection;
using helengine.baseplatform.Builders;
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
    /// Ensures packaging uses the target platform sidecar settings instead of stale top-level material fields when a builder is available.
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

        string packagedMaterialPath = Path.Combine(BuildRootPath, materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        using FileStream stream = new FileStream(packagedMaterialPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        MaterialAsset packagedMaterial = Assert.IsType<MaterialAsset>(global::helengine.editor.AssetSerializer.Deserialize(stream));

        Assert.Equal("CookedShader", packagedMaterial.ShaderAssetId);
        Assert.Equal("CookedShader.vs", packagedMaterial.VertexProgram);
        Assert.Equal("CookedShader.ps", packagedMaterial.PixelProgram);
        Assert.Equal(new[] { "CookedShader" }, result.ReferencedShaderAssetIds);
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
                    Id = "root-entity",
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
    /// Writes one serialized material asset that still points at stale top-level compatibility fields.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path to write.</param>
    /// <param name="shaderAssetId">Stale top-level shader asset id referenced by the material.</param>
    void WriteMaterialAsset(string materialRelativePath, string shaderAssetId) {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

        MaterialAsset materialAsset = new MaterialAsset {
            Id = materialRelativePath,
            ShaderAssetId = shaderAssetId,
            VertexProgram = string.Concat(shaderAssetId, ".vs"),
            PixelProgram = string.Concat(shaderAssetId, ".ps"),
            Variant = "default",
            RenderState = new MaterialRenderState(),
            ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
        };

        using FileStream stream = new FileStream(materialPath, FileMode.Create, FileAccess.Write, FileShare.None);
        global::helengine.editor.AssetSerializer.Serialize(stream, materialAsset);
    }

    /// <summary>
    /// Writes one per-platform material settings sidecar whose builder-owned field values point at the cooked shader.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path whose sidecar should be written.</param>
    void WriteMaterialSettings(string materialRelativePath) {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        AssetImportSettings settings = new AssetImportSettings();
        settings.Importer.ImporterId = "helengine.material";
        settings.Importer.SourceChecksum = string.Empty;
        settings.Importer.AssetId = materialRelativePath;

        AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings();
        platformSettings.Material.SchemaId = "standard-shader";
        platformSettings.Material.FieldValues["shader-asset-id"] = "CookedShader";
        platformSettings.Material.FieldValues["vertex-program"] = "CookedShader.vs";
        platformSettings.Material.FieldValues["pixel-program"] = "CookedShader.ps";
        platformSettings.Material.FieldValues["variant"] = "default";
        settings.Processor.Platforms["windows"] = platformSettings;

        using FileStream stream = new FileStream(materialPath + ".hasset", FileMode.Create, FileAccess.Write, FileShare.None);
        AssetImportSettingsBinarySerializer.Serialize(stream, settings);
    }

    /// <summary>
    /// Seeds the built-in shader library cache with a prebuilt EditorDefaultMesh shader asset so the test does not invoke runtime compilation.
    /// </summary>
    /// <param name="target">Shader compile target to seed.</param>
    void SeedBuiltInStandardShaderAsset(ShaderCompileTarget target) {
        string shaderPath = EditorBuiltInShaderAssetLibrary.ResolveShaderPath("EditorDefaultMesh.hlsl");
        string cacheKey = string.Concat(target.ToString(), "|", shaderPath);
        ShaderAsset shaderAsset = new ShaderAsset {
            Id = "EditorDefaultMesh",
            Name = "EditorDefaultMesh",
            TargetName = ShaderTargetNames.GetTargetName(target),
            Programs = [
                new ShaderProgramAsset {
                    Name = "EditorDefaultMesh.vs",
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
                    ProgramName = "EditorDefaultMesh.vs",
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
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(1);
        writer.WriteByte(0);
        writer.WriteByte(1);
        writer.WriteInt32((int)SceneAssetReferenceSourceKind.FileSystem);
        writer.WriteString(materialRelativePath);
        writer.WriteString(string.Empty);
        writer.WriteString(string.Empty);
        writer.WriteByte(0);
        return stream.ToArray();
    }
}
