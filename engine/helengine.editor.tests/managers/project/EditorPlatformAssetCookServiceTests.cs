using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the editor-owned asset cook service writes runtime cooked assets and startup-scene metadata.
/// </summary>
public sealed class EditorPlatformAssetCookServiceTests : IDisposable {
    readonly string ProjectRootPath;
    readonly string BuildRootPath;

    public EditorPlatformAssetCookServiceTests() {
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-cook-tests", Guid.NewGuid().ToString("N"));
        ProjectRootPath = workspaceRootPath;
        BuildRootPath = Path.Combine(workspaceRootPath, "Build");
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache", "shader-cache"));
        Directory.CreateDirectory(BuildRootPath);
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void Cook_scene_build_outputs_runtime_hasset_and_sets_startup_scene_from_order() {
        string startupScenePath = "Scenes/MainMenu.helen";
        string secondaryScenePath = "Scenes/Level01.helen";
        string sourceModelRelativePath = "Models/Sponza.obj";
        string sourceModelPath = Path.Combine(ProjectRootPath, "assets", sourceModelRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath)!);
        File.WriteAllText(sourceModelPath, "o Sponza\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

        WriteSceneAsset(
            startupScenePath,
            new[] {
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = sourceModelRelativePath,
                    ProviderId = string.Empty,
                    AssetId = string.Empty
                }
            });
        WriteSceneAsset(secondaryScenePath, Array.Empty<SceneAssetReference>());

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            new IAssetImporterRegistration[] {
                new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" })
            },
            null);

        PlatformBuildManifest manifest = service.Cook(
            new helengine.baseplatform.Definitions.PlatformDefinition(
                "windows",
                "Windows",
                Array.Empty<helengine.baseplatform.Definitions.PlatformBuildProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformGraphicsProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformAssetRequirementDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMaterialSchemaDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformComponentSupportRule>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformCodegenProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformStorageProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMediaProfileDefinition>()),
            new[] { "MainMenu", "Level01" },
            BuildRootPath,
            new[] { "windows" });

        Assert.Equal("MainMenu", manifest.StartupSceneId);
        Assert.Contains(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "MainMenu.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "startup.hasset")));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "Level01.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath);
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags && entry.Value == "0");
    }

    /// <summary>
    /// Verifies secondary scene outputs stay beneath `cooked/scenes` and do not duplicate the authored `scenes/` root segment.
    /// </summary>
    [Fact]
    public void Cook_when_secondary_scene_uses_lowercase_scenes_root_writes_it_beneath_cooked_scenes_without_duplicate_root_segment() {
        string startupScenePath = "scenes/menu.helen";
        string secondaryScenePath = "scenes/rendering/directional_shadow_plaza.helen";

        WriteSceneAsset(startupScenePath, Array.Empty<SceneAssetReference>());
        WriteSceneAsset(secondaryScenePath, Array.Empty<SceneAssetReference>());

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            Array.Empty<IAssetImporterRegistration>(),
            null);

        PlatformBuildManifest manifest = service.Cook(
            new helengine.baseplatform.Definitions.PlatformDefinition(
                "windows",
                "Windows",
                Array.Empty<helengine.baseplatform.Definitions.PlatformBuildProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformGraphicsProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformAssetRequirementDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMaterialSchemaDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformComponentSupportRule>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformCodegenProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformStorageProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMediaProfileDefinition>()),
            new[] { "menu", "directional_shadow_plaza" },
            BuildRootPath,
            new[] { "windows" });

        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "menu.hasset")));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "rendering", "directional_shadow_plaza.hasset")));
        Assert.Contains(
            manifest.Scenes[1].ResolvedMetadata,
            entry => entry.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath
                && entry.Value == "cooked/scenes/rendering/directional_shadow_plaza.hasset");
    }

    /// <summary>
    /// Verifies the committed point-shadow rendering scene cooks successfully with the installed Windows builder metadata.
    /// </summary>
    [Fact]
    public void Cook_when_using_committed_point_shadow_scene_with_windows_builder_metadata_succeeds() {
        string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
        string sourceProjectRootPath = Path.Combine(repositoryRootPath, "test-project");
        CopyDirectory(Path.Combine(sourceProjectRootPath, "assets"), Path.Combine(ProjectRootPath, "assets"));
        string sourceCacheRootPath = Path.Combine(sourceProjectRootPath, "cache");
        if (Directory.Exists(sourceCacheRootPath)) {
            CopyDirectory(sourceCacheRootPath, Path.Combine(ProjectRootPath, "cache"));
        }

        EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(sourceProjectRootPath, "project.heproj"));
        AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("windows");
        EditorPlatformAssetBuilderLoader builderLoader = new();
        helengine.baseplatform.Builders.IPlatformAssetBuilder builder = builderLoader.Load(platformDescriptor.BuilderAssemblyPath);
        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            bootstrap.RequiredEngineVersion,
            bootstrap.ProjectName,
            bootstrap.ProjectVersion,
            Array.Empty<IAssetImporterRegistration>(),
            null);

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            ["point-shadow"],
            BuildRootPath,
            ["windows"],
            builder,
            "debug",
            "directx11");

        Assert.Equal("point-shadow", manifest.StartupSceneId);
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "rendering", "point-shadow.hasset")));
    }

    /// <summary>
    /// Verifies the Windows builder publishes material schema metadata and cooks materials with a base-color buffer.
    /// </summary>
    [Fact]
    public void Cook_when_builder_definition_publishes_standard_material_schema_cooks_material_with_base_color_buffer() {
        string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
        string sourceProjectRootPath = Path.Combine(repositoryRootPath, "test-project");
        EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(sourceProjectRootPath, "project.heproj"));
        AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("windows");
        EditorPlatformAssetBuilderLoader builderLoader = new();
        helengine.baseplatform.Builders.IPlatformAssetBuilder builder = builderLoader.Load(platformDescriptor.BuilderAssemblyPath);

        string scenePath = "Scenes/PhysicsTrigger.helen";
        string materialRelativePath = "Materials/physics/PhysicsDemoNeutral.helmat";
        WriteMaterialAsset(materialRelativePath, "PhysicsDemoNeutral");
        WriteSceneAssetWithMaterial(scenePath, materialRelativePath);

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            bootstrap.RequiredEngineVersion,
            bootstrap.ProjectName,
            bootstrap.ProjectVersion,
            Array.Empty<IAssetImporterRegistration>(),
            null);

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            ["PhysicsTrigger"],
            BuildRootPath,
            ["windows"],
            builder,
            "debug",
            "directx11");

        Assert.Equal("PhysicsTrigger", manifest.StartupSceneId);
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "PhysicsTrigger.hasset")));
        string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "Materials", "physics", "PhysicsDemoNeutral.helmat");
        Assert.True(File.Exists(cookedMaterialPath));

        using FileStream stream = new FileStream(cookedMaterialPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        MaterialAsset cookedMaterial = Assert.IsType<MaterialAsset>(AssetSerializer.Deserialize(stream));
        Assert.Equal("ForwardStandardShader", cookedMaterial.ShaderAssetId);
        Assert.Single(cookedMaterial.ConstantBuffers);
        Assert.Equal("BaseColorBuffer", cookedMaterial.ConstantBuffers[0].Name);
        Assert.Equal(16, cookedMaterial.ConstantBuffers[0].Data.Length);
    }

    /// <summary>
    /// Verifies imported cooked texture assets stay classified as generic assets instead of models.
    /// </summary>
    [Fact]
    public void ResolveArtifactKind_when_imported_cooked_texture_is_supplied_returns_asset() {
        string importedTexturePath = Path.Combine(BuildRootPath, "cooked", "imported", "0123456789ABCDEF0123456789ABCDEF.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(importedTexturePath)!);
        WriteSerializedAsset(importedTexturePath, new TextureAsset {
            Id = "ImportedTexture",
            Width = 2,
            Height = 2,
            Colors = new byte[] {
                0xFF, 0x00, 0x00, 0xFF,
                0x00, 0xFF, 0x00, 0xFF,
                0x00, 0x00, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF
            }
        });

        string artifactKind = InvokeResolveArtifactKind(importedTexturePath, "cooked/imported/0123456789ABCDEF0123456789ABCDEF.hasset");

        Assert.Equal("asset", artifactKind);
    }

    /// <summary>
    /// Verifies imported cooked model assets still classify as models when their runtime path does not include a `Models` segment.
    /// </summary>
    [Fact]
    public void ResolveArtifactKind_when_imported_cooked_model_is_supplied_returns_model() {
        string importedModelPath = Path.Combine(BuildRootPath, "cooked", "imported", "FEDCBA9876543210FEDCBA9876543210.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(importedModelPath)!);
        WriteSerializedAsset(importedModelPath, new ModelAsset {
            Id = "ImportedModel",
            Positions = [float3.Zero, new float3(1.0f, 0.0f, 0.0f), new float3(0.0f, 1.0f, 0.0f)],
            Normals = [new float3(0.0f, 0.0f, 1.0f), new float3(0.0f, 0.0f, 1.0f), new float3(0.0f, 0.0f, 1.0f)],
            TexCoords = [new float2(0.0f, 0.0f), new float2(1.0f, 0.0f), new float2(0.0f, 1.0f)],
            BoundsMin = float3.Zero,
            BoundsMax = new float3(1.0f, 1.0f, 0.0f),
            Indices16 = [0, 1, 2],
            Indices32 = Array.Empty<uint>(),
            Submeshes = Array.Empty<ModelSubmeshAsset>(),
            Ps2PackedMeshBytes = Array.Empty<byte>()
        });

        string artifactKind = InvokeResolveArtifactKind(importedModelPath, "cooked/imported/FEDCBA9876543210FEDCBA9876543210.hasset");

        Assert.Equal("model", artifactKind);
    }

    void WriteSceneAsset(string sceneId, SceneAssetReference[] assetReferences) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            AssetReferences = assetReferences ?? Array.Empty<SceneAssetReference>(),
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

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one serialized material asset with shader-backed material fields and no import-settings sidecar.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path to write.</param>
    /// <param name="materialAssetId">Serialized material asset identifier.</param>
    void WriteMaterialAsset(string materialRelativePath, string materialAssetId) {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);

        MaterialAsset materialAsset = new() {
            Id = materialAssetId,
            ShaderAssetId = "PhysicsDemoShader",
            VertexProgram = "PhysicsDemo.vs",
            PixelProgram = "PhysicsDemo.ps",
            Variant = "default",
            RenderState = new MaterialRenderState(),
            ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
        };

        using FileStream stream = new(materialPath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, materialAsset);
    }

    /// <summary>
    /// Writes one serialized scene asset whose mesh component references the supplied file-backed material.
    /// </summary>
    /// <param name="sceneId">Scene asset identifier to write.</param>
    /// <param name="materialRelativePath">Project-relative material path referenced by the mesh component.</param>
    void WriteSceneAssetWithMaterial(string sceneId, string materialRelativePath) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            RootEntities = [
                new SceneEntityAsset {
                    Id = "mesh-root",
                    Name = "MeshRoot",
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

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one mesh-component payload that references one file-backed material.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path encoded into the payload.</param>
    /// <returns>Serialized mesh-component payload.</returns>
    static byte[] WriteMeshComponentPayload(string materialRelativePath) {
        EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
        writer.WriteField("MaterialReferences", fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReferenceArray(
            fieldWriter,
            [
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = materialRelativePath,
                    ProviderId = string.Empty,
                    AssetId = string.Empty
                }
            ]));
        writer.WriteField("RenderOrder3D", fieldWriter => fieldWriter.WriteByte(0));

        return writer.BuildPayload();
    }

    /// <summary>
    /// Invokes the private artifact-kind resolver so regression coverage can stay pinned to the exact exporter seam.
    /// </summary>
    /// <param name="fullPath">Full cooked file path passed to the resolver.</param>
    /// <param name="relativePath">Runtime-relative cooked path passed to the resolver.</param>
    /// <returns>Resolved artifact kind string.</returns>
    static string InvokeResolveArtifactKind(string fullPath, string relativePath) {
        Type serviceType = typeof(EditorPlatformAssetCookService);
        System.Reflection.MethodInfo method = serviceType.GetMethod(
            "ResolveArtifactKind",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(string), typeof(string)],
            null) ?? throw new InvalidOperationException("EditorPlatformAssetCookService.ResolveArtifactKind(string, string) was not found.");
        object result = method.Invoke(null, [fullPath, relativePath]) ?? throw new InvalidOperationException("Artifact kind resolver returned null.");
        return Assert.IsType<string>(result);
    }

    /// <summary>
    /// Serializes one asset to disk so classification tests can use real cooked payloads instead of synthetic markers.
    /// </summary>
    /// <param name="fullPath">Full destination path for the serialized asset.</param>
    /// <param name="asset">Asset instance to serialize.</param>
    static void WriteSerializedAsset(string fullPath, Asset asset) {
        using FileStream stream = new(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, asset);
    }

    /// <summary>
    /// Copies one directory tree into the temporary test workspace while preserving relative paths.
    /// </summary>
    /// <param name="sourceRootPath">Source directory tree to copy.</param>
    /// <param name="destinationRootPath">Destination directory root.</param>
    static void CopyDirectory(string sourceRootPath, string destinationRootPath) {
        Directory.CreateDirectory(destinationRootPath);
        string[] sourceFilePaths = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
        Array.Sort(sourceFilePaths, StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < sourceFilePaths.Length; index++) {
            string sourceFilePath = sourceFilePaths[index];
            string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
            string destinationPath = Path.Combine(destinationRootPath, relativePath);
            string destinationDirectoryPath = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(destinationDirectoryPath);
            File.Copy(sourceFilePath, destinationPath, true);
        }
    }
}
