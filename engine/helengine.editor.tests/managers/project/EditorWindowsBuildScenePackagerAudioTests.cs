using helengine.directx11;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies scene packaging rewrites and validates authored audio references.
/// </summary>
public sealed class EditorWindowsBuildScenePackagerAudioTests : IDisposable {
    readonly string ProjectRootPath;
    readonly string AssetsRootPath;
    readonly string BuildRootPath;

    /// <summary>
    /// Creates one isolated project workspace for audio packaging tests.
    /// </summary>
    public EditorWindowsBuildScenePackagerAudioTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-packager-audio-tests", Guid.NewGuid().ToString("N"));
        AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
        BuildRootPath = Path.Combine(ProjectRootPath, "Build");
        Directory.CreateDirectory(AssetsRootPath);
        Directory.CreateDirectory(BuildRootPath);

        ShaderBackendRegistry shaderBackendRegistry = new();
        shaderBackendRegistry.Register(new DirectX11ShaderBackend());
        EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
    }

    /// <summary>
    /// Deletes the temporary workspace after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures packaging rewrites authored source-audio references into cooked audio assets and scene references.
    /// </summary>
    [Fact]
    public void PackagePreservingIdentityPaths_WhenSceneReferencesAudio_WritesCookedAudioAsset() {
        const string audioRelativePath = "audio/menu/theme.wav";
        const string scenePath = "Scenes/MenuAudio.helen";
        string sourcePath = WriteSourceAudio(audioRelativePath);
        ConfigureAudioImportSettings(
            sourcePath,
            "windows",
            new AudioAssetProcessorSettings {
                PlaybackMode = AudioPlaybackMode.Streamed,
                EncodingFamilyId = "pcm-streamed",
                DefaultBusId = "music",
                DefaultLoop = true,
                StreamChunkByteSize = 4
            });
        WriteSceneAsset(scenePath, [SceneAssetReferenceTestFactory.CreateFileSystemAudio(audioRelativePath)]);

        EditorPlatformBuildScenePackager packager = CreatePackager("windows");

        packager.PackagePreservingIdentityPaths([scenePath], [scenePath], BuildRootPath);

        string cookedAudioPath = Path.Combine(BuildRootPath, "cooked", "audio", "menu", "theme.hasset");
        Assert.True(File.Exists(cookedAudioPath));

        using FileStream audioStream = File.OpenRead(cookedAudioPath);
        AudioAsset cookedAudio = Assert.IsType<AudioAsset>(AssetSerializer.Deserialize(audioStream));
        Assert.Equal(AudioPlaybackMode.Streamed, cookedAudio.PlaybackMode);
        Assert.Equal("music", cookedAudio.DefaultBusId);
        Assert.True(cookedAudio.DefaultLoop);

        string packagedScenePath = Path.Combine(BuildRootPath, PackagedScenePathResolver.BuildRelativePath(scenePath, 0).Replace('/', Path.DirectorySeparatorChar));
        using FileStream sceneStream = File.OpenRead(packagedScenePath);
        SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(sceneStream));
        SceneAssetReference packagedAudioReference = Assert.Single(packagedScene.AssetReferences);
        Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, packagedAudioReference.SourceKind);
        Assert.Equal("cooked/audio/menu/theme.hasset", packagedAudioReference.RelativePath);
    }

    /// <summary>
    /// Ensures DS packaging rejects audio assets whose imported format exceeds the currently supported limits.
    /// </summary>
    [Fact]
    public void PackagePreservingIdentityPaths_WhenDsAudioSettingsExceedLimits_Throws() {
        const string audioRelativePath = "audio/menu/theme.wav";
        const string scenePath = "Scenes/MenuAudio.helen";
        string sourcePath = WriteSourceAudio(audioRelativePath);
        ConfigureAudioImportSettings(
            sourcePath,
            "ds",
            new AudioAssetProcessorSettings {
                PlaybackMode = AudioPlaybackMode.Predecoded,
                EncodingFamilyId = "adpcm-buffered",
                DefaultBusId = "music"
            });
        WriteSceneAsset(scenePath, [SceneAssetReferenceTestFactory.CreateFileSystemAudio(audioRelativePath)]);

        EditorPlatformBuildScenePackager packager = CreatePackager("ds");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.PackagePreservingIdentityPaths([scenePath], [scenePath], BuildRootPath));

        Assert.Contains("ds", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sample rate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    string WriteSourceAudio(string relativePath) {
        string sourcePath = Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Audio source directory could not be resolved."));
        File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);
        return sourcePath;
    }

    void ConfigureAudioImportSettings(string sourcePath, string platformId, AudioAssetProcessorSettings processorSettings) {
        ContentManager contentManager = new(new HostFileSystemContentStreamSource(AssetsRootPath));
        AssetImportManager manager = new(ProjectRootPath, contentManager);
        manager.CurrentPlatformId = platformId;
        manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));

        AudioAssetImportSettings settings = manager.LoadOrCreateAudioImportSettings(sourcePath);
        settings.Processor.Platforms[platformId] = processorSettings;
        manager.SaveAudioImportSettings(sourcePath, settings);
    }

    void WriteSceneAsset(string sceneId, SceneAssetReference[] assetReferences) {
        string scenePath = Path.Combine(AssetsRootPath, sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath) ?? throw new InvalidOperationException("Scene directory could not be resolved."));

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            AssetReferences = assetReferences,
            RootEntities = [
                new SceneEntityAsset {
                    Id = 1u,
                    Name = "Root",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            ]
        };

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    EditorPlatformBuildScenePackager CreatePackager(string targetPlatformId) {
        return new EditorPlatformBuildScenePackager(
            ProjectRootPath,
            [
                new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"])
            ],
            targetPlatformId);
    }

    sealed class TestAudioImporter : IAudioImporter {
        public ImportedAudioSource ImportAudio(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return new ImportedAudioSource {
                Channels = 2,
                SampleRate = 44100,
                DurationSeconds = 3.5f,
                Pcm16Bytes = [1, 2, 3, 4]
            };
        }
    }
}
