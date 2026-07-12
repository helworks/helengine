using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using helengine.directx11;
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
        ShaderBackendRegistry shaderBackendRegistry = new();
        shaderBackendRegistry.Register(new DirectX11ShaderBackend());
        EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
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
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemModel(sourceModelRelativePath)
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
            PackagedFontAssetFactory.Create());
        TestPlatformMaterialAssetBuilder builder = new TestPlatformMaterialAssetBuilder();

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            new[] { "MainMenu", "Level01" },
            BuildRootPath,
            new[] { "windows" },
            builder);

        Assert.Equal("MainMenu", manifest.StartupSceneId);
        Assert.Contains(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "MainMenu.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "startup.hasset")));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "Level01.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath);
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.Physics3DSceneFeatureFlags && entry.Value == "0");
        Assert.Contains(manifest.Scenes[0].ResolvedMetadata, entry => entry.Key == PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds && entry.Value == string.Empty);
    }

    /// <summary>
    /// Verifies cooked scene metadata records the used automatic runtime component type ids so later build phases can discover code-driven runtime feature requirements.
    /// </summary>
    [Fact]
    public void Cook_scene_build_outputs_automatic_runtime_component_type_ids_metadata() {
        string scenePath = "Scenes/ScriptedScene.helen";
        string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(GeneratedRuntimeModuleRegistrationTestComponent));
        DictionaryScriptTypeResolver scriptTypeResolver = new();
        scriptTypeResolver.Register(componentTypeId, typeof(GeneratedRuntimeModuleRegistrationTestComponent));
        WriteSceneAsset(
            scenePath,
            [
                new SceneComponentAssetRecord {
                    ComponentTypeId = componentTypeId,
                    ComponentIndex = 0,
                    Payload = Array.Empty<byte>()
                }
            ],
            Array.Empty<SceneAssetReference>());

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            Array.Empty<IAssetImporterRegistration>(),
            PackagedFontAssetFactory.Create(),
            scriptTypeResolver);
        TestPlatformMaterialAssetBuilder builder = new();

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            ["ScriptedScene"],
            BuildRootPath,
            ["windows"],
            builder);

        Assert.Contains(
            manifest.Scenes[0].ResolvedMetadata,
            entry => entry.Key == PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds
                && entry.Value == componentTypeId);
    }

    /// <summary>
    /// Verifies generated boot scene source overrides keep the canonical packaged scene path while still loading the overridden authored scene contents.
    /// </summary>
    [Fact]
    public void Cook_when_generated_boot_scene_uses_override_source_path_preserves_canonical_packaged_scene_path() {
        const string canonicalScenePath = "Scenes/GeneratedBootScene.helen";
        const string overrideScenePath = ".generated-build/3ds/build123/GeneratedBootScene_build123.helen";
        WriteSceneAsset(canonicalScenePath, "CanonicalRoot", Array.Empty<SceneAssetReference>());
        WriteSceneAsset(overrideScenePath, "OverrideRoot", Array.Empty<SceneAssetReference>());

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            Array.Empty<IAssetImporterRegistration>(),
            PackagedFontAssetFactory.Create());
        TestPlatformMaterialAssetBuilder builder = new TestPlatformMaterialAssetBuilder();

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            ["GeneratedBootScene"],
            BuildRootPath,
            ["3ds"],
            builder,
            scenePathOverrides: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["GeneratedBootScene"] = overrideScenePath
            });

        string cookedScenePath = Path.Combine(BuildRootPath, "cooked", "scenes", "generatedbootscene.hasset");
        Assert.True(File.Exists(cookedScenePath));
        Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", ".generated-build")));

        using FileStream stream = File.OpenRead(cookedScenePath);
        SceneAsset cookedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Assert.Equal("GeneratedBootScene", cookedScene.Id);
        Assert.Equal("OverrideRoot", Assert.Single(cookedScene.RootEntities).Name);
        Assert.Contains(
            manifest.Scenes[0].ResolvedMetadata,
            entry => entry.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath
                && entry.Value == "cooked/scenes/generatedbootscene.hasset");
        Assert.DoesNotContain(
            manifest.Scenes[0].ResolvedMetadata,
            entry => entry.Value.Contains(".generated-build", StringComparison.OrdinalIgnoreCase));
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
            PackagedFontAssetFactory.Create());
        TestPlatformMaterialAssetBuilder builder = new TestPlatformMaterialAssetBuilder();

        PlatformBuildManifest manifest = service.Cook(
            builder.Definition,
            new[] { "menu", "directional_shadow_plaza" },
            BuildRootPath,
            new[] { "windows" },
            builder);

        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "menu.hasset")));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "scenes", "rendering", "directional_shadow_plaza.hasset")));
        Assert.Contains(
            manifest.Scenes[1].ResolvedMetadata,
            entry => entry.Key == PlatformBuildSceneMetadataKeys.CookedRelativePath
                && entry.Value == "cooked/scenes/rendering/directional_shadow_plaza.hasset");
    }

    /// <summary>
    /// Verifies packaged animation clips are flattened for the selected target platform before they are written into the player content root.
    /// </summary>
    [Fact]
    public void Cook_when_scene_references_animation_clip_resolves_platform_override_before_packaging() {
        string scenePath = "Scenes/AnimatedLogo.helen";
        string animationRelativePath = "Animations/DemoDiscLogoIdle.hanim";
        WriteAnimationClipAsset(
            animationRelativePath,
            new AnimationClipAsset {
                Id = animationRelativePath,
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(0f, 0f, 0f), AnimationInterpolationMode.Step) {
                                FrameId = "base-000"
                            },
                            new PositionKeyframeAsset(1f, new float3(8f, 0f, 0f), AnimationInterpolationMode.Linear) {
                                FrameId = "base-001"
                            }
                        ]
                    }
                ],
                PlatformOverrides = [
                    new AnimationClipPlatformOverrideAsset {
                        PlatformId = "windows",
                        Mode = AnimationClipPlatformOverrideMode.OverrideFrames,
                        PositionTracks = [
                            new PlatformPositionKeyframeTrackAsset {
                                Keyframes = [
                                    new PositionKeyframeAsset(0.5f, new float3(4f, 2f, 0f), AnimationInterpolationMode.Linear),
                                    new PositionKeyframeAsset(1f, new float3(12f, -2f, 0f), AnimationInterpolationMode.Linear) {
                                        FrameId = "base-001"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            });
        WriteSceneAsset(
            scenePath,
            [
                SceneAssetReferenceTestFactory.CreateFileSystemAnimationClip(animationRelativePath)
            ]);

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            Array.Empty<IAssetImporterRegistration>(),
            PackagedFontAssetFactory.Create());
        TestPlatformMaterialAssetBuilder builder = new();

        service.Cook(
            builder.Definition,
            ["AnimatedLogo"],
            BuildRootPath,
            ["windows"],
            builder);

        string packagedAnimationPath = Path.Combine(BuildRootPath, "Animations", "DemoDiscLogoIdle.hanim");
        Assert.True(File.Exists(packagedAnimationPath));

        using FileStream stream = new(packagedAnimationPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        AnimationClipAsset packagedClip = Assert.IsType<AnimationClipAsset>(AssetSerializer.Deserialize(stream));
        PositionKeyframeAsset[] keyframes = Assert.Single(packagedClip.PositionTracks).Keyframes;
        Assert.Empty(packagedClip.PlatformOverrides);
        Assert.Collection(
            keyframes,
            keyframe => Assert.Equal(0f, keyframe.Time),
            keyframe => Assert.Equal(0.5f, keyframe.Time),
            keyframe => Assert.Equal(1f, keyframe.Time));
        Assert.Equal(new float3(12f, -2f, 0f), keyframes[2].Value);
        Assert.All(keyframes, keyframe => Assert.True(string.IsNullOrEmpty(keyframe.FrameId)));
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
            PackagedFontAssetFactory.Create());

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
        string materialRelativePath = "Materials/physics/PhysicsDemoNeutral.hasset";
        WriteMaterialAsset(materialRelativePath, "PhysicsDemoNeutral");
        WriteSceneAssetWithMaterial(scenePath, materialRelativePath);

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            bootstrap.RequiredEngineVersion,
            bootstrap.ProjectName,
            bootstrap.ProjectVersion,
            Array.Empty<IAssetImporterRegistration>(),
            PackagedFontAssetFactory.Create());

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
        string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "Materials", "physics", "PhysicsDemoNeutral.hasset");
        Assert.True(File.Exists(cookedMaterialPath));

        using FileStream stream = new FileStream(cookedMaterialPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ShaderMaterialAsset cookedMaterial = Assert.IsType<ShaderMaterialAsset>(AssetSerializer.Deserialize(stream));
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
            Submeshes = Array.Empty<ModelSubmeshAsset>()
        });

        string artifactKind = InvokeResolveArtifactKind(importedModelPath, "cooked/imported/FEDCBA9876543210FEDCBA9876543210.hasset");

        Assert.Equal("model", artifactKind);
    }

    /// <summary>
    /// Verifies cooked material artifacts that live beneath a model directory still classify as materials instead of models.
    /// </summary>
    [Fact]
    public void ResolveArtifactKind_when_cooked_material_is_stored_beneath_models_directory_returns_material() {
        string cookedMaterialPath = Path.Combine(BuildRootPath, "cooked", "models", "Riemers", "racer", "x3ds_mat_Material_1_2.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(cookedMaterialPath)!);
        WriteSerializedAsset(cookedMaterialPath, new ShaderMaterialAsset {
            Id = "RacerMaterial",
            ShaderAssetId = "ForwardStandardShader",
            ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
        });

        string artifactKind = InvokeResolveArtifactKind(cookedMaterialPath, "cooked/models/Riemers/racer/x3ds_mat_Material_1_2.hasset");

        Assert.Equal("material", artifactKind);
    }

    /// <summary>
    /// Verifies cooked audio assets classify as audio so manifests can surface runtime media correctly.
    /// </summary>
    [Fact]
    public void ResolveArtifactKind_when_cooked_audio_is_supplied_returns_audio() {
        string cookedAudioPath = Path.Combine(BuildRootPath, "cooked", "audio", "menu", "theme.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(cookedAudioPath)!);
        WriteSerializedAsset(cookedAudioPath, new AudioAsset {
            Id = "theme",
            PlaybackMode = AudioPlaybackMode.Streamed,
            EncodingFamilyId = "pcm-streamed",
            Channels = 2,
            SampleRate = 44100,
            DurationSeconds = 3.5f,
            Chunks = [
                new AudioChunkDescriptor {
                    ByteOffset = 0,
                    ByteLength = 4
                }
            ],
            EncodedBytes = [1, 2, 3, 4]
        });

        string artifactKind = InvokeResolveArtifactKind(cookedAudioPath, "cooked/audio/menu/theme.hasset");

        Assert.Equal("audio", artifactKind);
    }

    /// <summary>
    /// Verifies builder-owned GameCube texture capabilities are emitted as manifest work items and removed from cooked artifacts.
    /// </summary>
    [Fact]
        public void Cook_when_platform_owns_texture_cooking_emits_platform_cook_work_item_and_removes_generic_cooked_texture_artifact() {
            string sceneId = "Scenes/TexturedMaterialScene.helen";
            string materialRelativePath = "Materials/rendering/textured_cube_grid/Cube00.hasset";
            string textureRelativePath = "Textures/Cube00.png";
            string textureAssetId = WriteSourceTextureAssetAndReturnAssetId(textureRelativePath, ".png", "gamecube");

            WriteCityStyleStandardMaterialAsset(materialRelativePath, textureAssetId);
            WriteSceneAssetWithMaterial(sceneId, materialRelativePath);

            EditorPlatformAssetCookService service = new(
                ProjectRootPath,
                "1.0.0-engine",
                "game",
                "1.0.0",
                [
                    new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"])
                ],
                PackagedFontAssetFactory.Create());

        PlatformBuildManifest manifest = service.Cook(
            CreateGameCubeTexturePlatformDefinition(),
            ["TexturedMaterialScene"],
            BuildRootPath,
            ["gamecube"],
            new DescriptorOnlyPlatformAssetBuilder(CreateGameCubeTexturePlatformDefinition()));

        PlatformCookWorkItem workItem = Assert.Single(manifest.PlatformCookWorkItems);
        Assert.Equal("texture", workItem.SourceAssetKind);
        Assert.Equal("runtime-texture", workItem.TargetArtifactKind);
        Assert.Equal($"cooked/imported/{textureAssetId}", workItem.OutputRelativePath);
        Assert.DoesNotContain(
            manifest.CookedArtifacts,
            artifact => string.Equals(artifact.RelativePath, $"cooked/imported/{textureAssetId}", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "cooked", "imported", textureAssetId)));
    }

    /// <summary>
    /// Writes one authored scene asset that uses the default root-entity name.
    /// </summary>
    /// <param name="sceneId">Project-relative scene path to write beneath the temporary assets root.</param>
    /// <param name="assetReferences">Asset references that should be serialized into the authored scene.</param>
    void WriteSceneAsset(string sceneId, SceneAssetReference[] assetReferences) {
        WriteSceneAsset(sceneId, "Root", Array.Empty<SceneComponentAssetRecord>(), assetReferences);
    }

    /// <summary>
    /// Writes one authored scene asset with a caller-supplied root-entity name so override-source tests can distinguish which scene payload was packaged.
    /// </summary>
    /// <param name="sceneId">Project-relative scene path to write beneath the temporary assets root.</param>
    /// <param name="rootEntityName">Name assigned to the single serialized root entity.</param>
    /// <param name="assetReferences">Asset references that should be serialized into the authored scene.</param>
    void WriteSceneAsset(string sceneId, string rootEntityName, SceneAssetReference[] assetReferences) {
        WriteSceneAsset(sceneId, rootEntityName, Array.Empty<SceneComponentAssetRecord>(), assetReferences);
    }

    /// <summary>
    /// Writes one authored scene asset with caller-supplied serialized component records on the root entity.
    /// </summary>
    /// <param name="sceneId">Project-relative scene path to write beneath the temporary assets root.</param>
    /// <param name="componentRecords">Serialized component records that should be written onto the root entity.</param>
    /// <param name="assetReferences">Asset references that should be serialized into the authored scene.</param>
    void WriteSceneAsset(string sceneId, SceneComponentAssetRecord[] componentRecords, SceneAssetReference[] assetReferences) {
        WriteSceneAsset(sceneId, "Root", componentRecords, assetReferences);
    }

    /// <summary>
    /// Writes one authored scene asset with caller-supplied root-entity data.
    /// </summary>
    /// <param name="sceneId">Project-relative scene path to write beneath the temporary assets root.</param>
    /// <param name="rootEntityName">Name assigned to the single serialized root entity.</param>
    /// <param name="componentRecords">Serialized component records that should be written onto the root entity.</param>
    /// <param name="assetReferences">Asset references that should be serialized into the authored scene.</param>
    void WriteSceneAsset(string sceneId, string rootEntityName, SceneComponentAssetRecord[] componentRecords, SceneAssetReference[] assetReferences) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            AssetReferences = assetReferences ?? Array.Empty<SceneAssetReference>(),
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = 1u,
                    Name = rootEntityName,
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = componentRecords ?? Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            }
        };

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one authored material document with standard-shader defaults and no platform override.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path to write.</param>
    /// <param name="materialAssetId">Serialized material asset identifier.</param>
    void WriteMaterialAsset(string materialRelativePath, string materialAssetId) {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);

        MaterialAssetImportSettings settings = new() {
            Importer = new AssetImporterSettings {
                ImporterId = "helengine.material",
                SourceChecksum = string.Empty,
                AssetId = materialAssetId
            },
            Processor = new MaterialAssetProcessorPlatformSettings()
        };
        settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
            SchemaId = "standard-shader",
            FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["use-custom-shader"] = "false",
                ["texture-id"] = string.Empty,
                ["casts-shadow"] = "true",
                ["receives-shadow"] = "true",
                ["base-color"] = "#FFFFFFFF"
            }
        };

        MaterialAssetSettingsService settingsService = new();
        settingsService.Save(materialPath, settings);
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
                    Id = 1u,
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
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial(materialRelativePath)
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
    /// Writes one serialized animation clip asset into the temporary project assets tree.
    /// </summary>
    /// <param name="animationRelativePath">Project-relative animation clip path to write.</param>
    /// <param name="animationClipAsset">Animation clip payload to serialize.</param>
    void WriteAnimationClipAsset(string animationRelativePath, AnimationClipAsset animationClipAsset) {
        string animationPath = Path.Combine(ProjectRootPath, "assets", animationRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(animationPath)!);
        WriteSerializedAsset(animationPath, animationClipAsset);
    }

    /// <summary>
    /// Writes one project-style standard material settings document that references one optional imported diffuse texture id.
    /// </summary>
    /// <param name="materialRelativePath">Project-relative material path to write.</param>
    /// <param name="diffuseTextureAssetId">Optional imported texture asset id referenced by the material.</param>
    void WriteCityStyleStandardMaterialAsset(string materialRelativePath, string diffuseTextureAssetId = "") {
        string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);

        MaterialAssetImportSettings settings = new() {
            Importer = new AssetImporterSettings {
                ImporterId = "helengine.material",
                SourceChecksum = string.Empty,
                AssetId = materialRelativePath
            },
            Processor = new MaterialAssetProcessorPlatformSettings()
        };
        settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
            SchemaId = "standard-shader",
            FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["use-custom-shader"] = "false",
                ["texture-id"] = diffuseTextureAssetId ?? string.Empty,
                ["casts-shadow"] = "true",
                ["receives-shadow"] = "true",
                ["base-color"] = "#FF4040FF"
            }
        };

        MaterialAssetSettingsService settingsService = new();
        settingsService.Save(materialPath, settings);
    }

    /// <summary>
    /// Writes one cached imported texture asset at the project cache path expected by scene packaging.
    /// </summary>
    /// <param name="textureAssetId">Imported texture asset identifier to write.</param>
    void WriteCachedTextureAsset(string textureAssetId) {
        string texturePath = Path.Combine(ProjectRootPath, "cache", textureAssetId);
        Directory.CreateDirectory(Path.GetDirectoryName(texturePath)!);

        TextureAsset textureAsset = new() {
            Width = 1,
            Height = 1,
            Colors = [255, 255, 255, 255]
        };

        using FileStream stream = new(texturePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, textureAsset);
    }

    /// <summary>
    /// Creates one minimal GameCube platform definition that publishes builder-owned runtime texture cooking.
    /// </summary>
    /// <returns>GameCube platform definition used by the work-item emission test.</returns>
    static PlatformDefinition CreateGameCubeTexturePlatformDefinition() {
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
    /// Writes one source texture file and returns the asset id that the editor importer settings resolve for it.
    /// </summary>
    /// <param name="textureRelativePath">Project-relative source texture path to create.</param>
    /// <param name="extension">Texture extension registered for the test importer.</param>
    /// <returns>Importer-resolved texture asset id for the written source texture.</returns>
    string WriteSourceTextureAssetAndReturnAssetId(string textureRelativePath, string extension, string platformId) {
        string textureSourcePath = Path.Combine(ProjectRootPath, "assets", textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(textureSourcePath)!);
        File.WriteAllBytes(textureSourcePath, [1, 2, 3, 4]);

        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
        AssetImportManager assetImportManager = new(ProjectRootPath, contentManager);
        assetImportManager.CurrentPlatformId = platformId;
        assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [extension]));

        TextureAssetImportSettings settings;
        Assert.True(assetImportManager.TryLoadOrCreateTextureImportSettings(textureSourcePath, out settings));
        Assert.NotNull(settings);
        Assert.NotNull(settings.Importer);
        Assert.False(string.IsNullOrWhiteSpace(settings.Importer.AssetId));
        return settings.Importer.AssetId;
    }

    /// <summary>
    /// Provides one descriptor-only builder so the asset-cook service can stamp platform metadata without enabling builder-owned material translation.
    /// </summary>
    sealed class DescriptorOnlyPlatformAssetBuilder : helengine.baseplatform.Builders.IPlatformAssetBuilder {
        /// <summary>
        /// Initializes the descriptor-only builder with the supplied platform definition.
        /// </summary>
        /// <param name="definition">Platform definition exposed to the asset-cook service.</param>
        public DescriptorOnlyPlatformAssetBuilder(PlatformDefinition definition) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Descriptor = new PlatformBuilderDescriptor(
                "helengine.editor.tests.descriptor-only-builder",
                "1.0.0",
                definition.PlatformId,
                new EngineCompatibilityRange("1.0.0", "999.0.0"),
                new ManifestCompatibilityRange(1, 2),
                [definition.PlatformId],
                ["debug"]);
        }

        /// <summary>
        /// Gets the descriptor exposed to the asset-cook service.
        /// </summary>
        public PlatformBuilderDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the platform definition exposed to the asset-cook service.
        /// </summary>
        public PlatformDefinition Definition { get; }

        /// <summary>
        /// Descriptor-only test builders never cook materials.
        /// </summary>
        /// <param name="request">Material cook request that should never be issued.</param>
        /// <returns>This method always throws because the test never expects material cooking.</returns>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
            throw new NotSupportedException("Descriptor-only test builders do not support material cooking.");
        }

        /// <summary>
        /// Descriptor-only test builders never execute full platform builds.
        /// </summary>
        /// <param name="request">Build request that should never be issued.</param>
        /// <param name="progressReporter">Progress reporter supplied by the caller.</param>
        /// <param name="diagnosticReporter">Diagnostic reporter supplied by the caller.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the caller.</param>
        /// <returns>This method always throws because the test never expects full builder execution.</returns>
        public Task<PlatformBuildReport> BuildAsync(
            PlatformBuildRequest request,
            IPlatformBuildProgressReporter progressReporter,
            IPlatformBuildDiagnosticReporter diagnosticReporter,
            CancellationToken cancellationToken) {
            throw new NotSupportedException("Descriptor-only test builders do not support full build execution.");
        }
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
