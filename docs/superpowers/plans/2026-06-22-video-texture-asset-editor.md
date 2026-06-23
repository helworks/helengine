# VideoTextureAsset Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add first-class editor-only `VideoTextureAsset` support for `.mp4` files so authored videos can be assigned anywhere `RuntimeTexture` references are used, autoplay, loop, and render through a DirectX11-backed GPU decode path.

**Architecture:** Keep authored video assets on the existing asset/import/scene-reference seams instead of inventing a parallel media system. Treat `.mp4` as a texture-compatible source asset, add one explicit `RenderManager2D.BuildTextureFromVideo(...)` seam, and let the DirectX11 backend own the playback/update path via a stable `RuntimeTexture` instance whose GPU texture contents refresh over time.

**Tech Stack:** C#, xUnit, SharpDX Direct3D11, native C++ DLL exports, FFmpeg, existing HelEngine content processors and binary serializers.

---

## File Map

### Core asset and serialization

- Create: `engine/helengine.core/assets/raw/ITextureSourceAsset.cs`
  - Shared authored-asset marker for source assets that can materialize into `RuntimeTexture`.
- Create: `engine/helengine.core/assets/raw/VideoTextureAsset.cs`
  - Metadata-only authored video asset for imported `.mp4` sources.
- Modify: `engine/helengine.core/assets/raw/TextureAsset.cs`
  - Implement the shared texture-source interface.
- Modify: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
  - Add a new serialized value kind for `VideoTextureAsset`.
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
  - Write/read `VideoTextureAsset` payloads and bump version.
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
  - Read `VideoTextureAsset` payloads on the runtime/editor load side.
- Modify: `engine/helengine.core/managers/rendering/RenderManager2D.cs`
  - Add `BuildTextureFromVideo(VideoTextureAsset asset, string sourcePath)` virtual seam.

### Editor import pipeline

- Create: `engine/helengine.editor/managers/asset/VideoTextureAssetImportSettings.cs`
  - Typed sidecar model for video importer metadata.
- Create: `engine/helengine.editor/content/video/IVideoImporter.cs`
  - Video importer contract returning a `VideoTextureAsset`.
- Create: `engine/helengine.editor/content/video/VideoImporterContentProcessor.cs`
  - Content-processor bridge for registered video importers.
- Create: `engine/helengine.editor/managers/asset/VideoImporterRegistration.cs`
  - Registration wrapper mirroring texture/model importer registration patterns.
- Create: `engine/helengine.editor/serialization/VideoTextureAssetImportSettingsBinarySerializer.cs`
  - Serializer for typed video `.hasset` sidecars.
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinaryValueKind.cs`
  - Add one new typed value kind for video settings.
- Modify: `engine/helengine.editor/content/EditorContentProcessorIds.cs`
  - Register stable processor ids for `VideoTextureAsset` and `VideoTextureAssetImportSettings`.
- Modify: `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`
  - Register content processors for video assets and video sidecars.
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
  - Add video importer registries, sidecar load/save, cache import, and texture-compatible asset load APIs.
- Create: `engine/helengine.editor/tests/testing/TestVideoImporter.cs`
  - Deterministic fake importer for manager and preview tests.

### Editor browsing, preview, and scene resolution

- Modify: `engine/helengine.editor/managers/asset/AssetEntryKind.cs`
  - Add `Video`.
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
  - Classify `.mp4` assets as `AssetEntryKind.Video`.
- Modify: `engine/helengine.editor/managers/asset/EditorFileSystemTextureResolver.cs`
  - Resolve either `TextureAsset` or `VideoTextureAsset` through one texture-source API.
- Modify: `engine/helengine.editor/managers/preview/TexturePreviewSource.cs`
  - Build previews from static textures or video textures.
- Modify: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
  - Treat `Video` entries as previewable texture sources.
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
  - Branch between static texture and video texture runtime creation.
- Modify: `engine/helengine.editor/EditorSession.cs`
  - Add video-aware import-settings handling, preview restoration, and per-frame render-manager updates.
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
  - Keep importer UI for videos but suppress texture-processor UI.
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
  - Add a video icon/category path.

### DirectX11 managed runtime

- Create: `engine/helengine.directx11/DirectX11VideoTextureResource.cs`
  - `DirectX11TextureResource` subclass that owns decoder state and the persistent shader-readable texture.
- Modify: `engine/helengine.directx11/DirectX11Renderer2D.cs`
  - Override `BuildTextureFromVideo`, track active video textures, update them each frame, and release them safely.
- Create: `engine/helengine.directx11.video/VideoFileProbe.cs`
  - Managed metadata probe wrapper used by importers without a D3D device.
- Modify: `engine/helengine.directx11.video/FfmpegNativeApi.cs`
  - Add probe exports alongside the decoder exports.
- Modify: `engine/helengine.directx11.video/DirectX11VideoDecoder.cs`
  - Preserve current API and extend only if the final native contract requires one explicit reset or loop helper.

### Windows host and native backend

- Modify: `engine/helengine.editor.windows/content/textures/EditorHostTextureImporterFactory.cs`
  - Register the `.mp4` video importer alongside existing texture importers.
- Create: `engine/helengine.editor.windows/content/video/WindowsVideoTextureImporter.cs`
  - Importer that uses `VideoFileProbe` to validate `.mp4`/H.264 and emit `VideoTextureAsset`.
- Create: `engine/helengine.video.ffmpeg/helengine.video.ffmpeg.vcxproj`
  - Native project for the FFmpeg-backed DLL.
- Create: `engine/helengine.video.ffmpeg/include/helengine_video_ffmpeg.h`
  - Export declarations shared by native source files.
- Create: `engine/helengine.video.ffmpeg/src/VideoFileProbeExports.cpp`
  - Metadata probe exports for importer use.
- Create: `engine/helengine.video.ffmpeg/src/VideoDecoderExports.cpp`
  - D3D11 decode exports used by `DirectX11VideoDecoder`.
- Modify: `helengine.ui/helengine.sln`
  - Add the native project and any new managed projects/files.

### Test files

- Create: `engine/helengine.editor.tests/serialization/VideoTextureAssetBinarySerializerTests.cs`
- Create: `engine/helengine.editor.tests/serialization/VideoTextureAssetImportSettingsBinarySerializerTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager2D.cs`
- Create: `engine/helengine.editor.windows.tests/rendering/DirectX11VideoTextureResourceTests.cs`
- Modify: `engine/helengine.editor.windows.tests/content/textures/EditorHostTextureImporterFactoryTests.cs`

## Task 1: Add `VideoTextureAsset` and Binary Serialization

**Files:**
- Create: `engine/helengine.core/assets/raw/ITextureSourceAsset.cs`
- Create: `engine/helengine.core/assets/raw/VideoTextureAsset.cs`
- Modify: `engine/helengine.core/assets/raw/TextureAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/serialization/VideoTextureAssetBinarySerializerTests.cs`

- [ ] **Step 1: Write the failing serializer test**

```csharp
using Xunit;

namespace helengine.editor.tests.serialization {
    public sealed class VideoTextureAssetBinarySerializerTests {
        [Fact]
        public void SerializeDeserialize_WhenVideoTextureAssetUsesMp4Metadata_RoundTripsAllFields() {
            VideoTextureAsset asset = new VideoTextureAsset {
                Id = "videos/logo-loop",
                Width = 1920,
                Height = 1080,
                FrameRate = 29.97,
                DurationTicks = TimeSpan.FromSeconds(12).Ticks,
                SourceExtension = ".mp4",
                CodecId = "h264",
                ContainerId = "mp4",
                AutoPlay = true,
                Loop = true
            };

            using MemoryStream stream = new MemoryStream();
            helengine.files.EditorAssetBinarySerializer.Serialize(stream, asset);
            stream.Position = 0;

            VideoTextureAsset roundTripped = Assert.IsType<VideoTextureAsset>(helengine.files.EditorAssetBinarySerializer.Deserialize(stream));
            Assert.Equal(asset.Id, roundTripped.Id);
            Assert.Equal(1920, roundTripped.Width);
            Assert.Equal(1080, roundTripped.Height);
            Assert.Equal(29.97, roundTripped.FrameRate, 3);
            Assert.Equal(asset.DurationTicks, roundTripped.DurationTicks);
            Assert.Equal(".mp4", roundTripped.SourceExtension);
            Assert.Equal("h264", roundTripped.CodecId);
            Assert.True(roundTripped.AutoPlay);
            Assert.True(roundTripped.Loop);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~VideoTextureAssetBinarySerializerTests" -v minimal`

Expected: FAIL with missing `VideoTextureAsset` type and unsupported editor asset value kind.

- [ ] **Step 3: Add the new asset type and serializer support**

```csharp
namespace helengine {
    public interface ITextureSourceAsset {
        ushort Width { get; }
        ushort Height { get; }
    }

    public sealed class VideoTextureAsset : Asset, ITextureSourceAsset {
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public double FrameRate { get; set; }
        public long DurationTicks { get; set; }
        public string SourceExtension { get; set; } = ".mp4";
        public string CodecId { get; set; } = "h264";
        public string ContainerId { get; set; } = "mp4";
        public bool AutoPlay { get; set; } = true;
        public bool Loop { get; set; } = true;
    }
}

// EditorAssetBinaryValueKind.cs
VideoTextureAsset = 10

// EditorAssetBinarySerializer.cs
if (asset is VideoTextureAsset videoTextureAsset) {
    WriteVideoTextureAsset(writer, videoTextureAsset);
    return;
}

static void WriteVideoTextureAsset(EngineBinaryWriter writer, VideoTextureAsset asset) {
    EnsureRuntimeAssetIdentity(asset);
    WriteAssetIdentity(writer, asset);
    writer.WriteUInt16(asset.Width);
    writer.WriteUInt16(asset.Height);
    writer.WriteDouble(asset.FrameRate);
    writer.WriteInt64(asset.DurationTicks);
    writer.WriteString(asset.SourceExtension ?? string.Empty);
    writer.WriteString(asset.CodecId ?? string.Empty);
    writer.WriteString(asset.ContainerId ?? string.Empty);
    writer.WriteByte(asset.AutoPlay ? (byte)1 : (byte)0);
    writer.WriteByte(asset.Loop ? (byte)1 : (byte)0);
}

static VideoTextureAsset ReadVideoTextureAsset(EngineBinaryReader reader, byte version) {
    VideoTextureAsset asset = new VideoTextureAsset();
    ReadAssetIdentity(reader, asset, version);
    asset.Width = reader.ReadUInt16();
    asset.Height = reader.ReadUInt16();
    asset.FrameRate = reader.ReadDouble();
    asset.DurationTicks = reader.ReadInt64();
    asset.SourceExtension = reader.ReadString();
    asset.CodecId = reader.ReadString();
    asset.ContainerId = reader.ReadString();
    asset.AutoPlay = reader.ReadByte() != 0;
    asset.Loop = reader.ReadByte() != 0;
    return asset;
}
```

- [ ] **Step 4: Run the serializer test again**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~VideoTextureAssetBinarySerializerTests" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.core/assets/raw/ITextureSourceAsset.cs engine/helengine.core/assets/raw/VideoTextureAsset.cs engine/helengine.core/assets/raw/TextureAsset.cs engine/helengine.core/assets/EditorAssetBinaryValueKind.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/serialization/VideoTextureAssetBinarySerializerTests.cs
git -C C:\dev\helworks\helengine commit -m "feat: add video texture asset serialization"
```

## Task 2: Add Typed Video Import Settings and Asset-Import Manager Support

**Files:**
- Create: `engine/helengine.editor/managers/asset/VideoTextureAssetImportSettings.cs`
- Create: `engine/helengine.editor/content/video/IVideoImporter.cs`
- Create: `engine/helengine.editor/content/video/VideoImporterContentProcessor.cs`
- Create: `engine/helengine.editor/managers/asset/VideoImporterRegistration.cs`
- Create: `engine/helengine.editor/serialization/VideoTextureAssetImportSettingsBinarySerializer.cs`
- Create: `engine/helengine.editor.tests/testing/TestVideoImporter.cs`
- Create: `engine/helengine.editor.tests/serialization/VideoTextureAssetImportSettingsBinarySerializerTests.cs`
- Modify: `engine/helengine.editor/content/EditorContentProcessorIds.cs`
- Modify: `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinaryValueKind.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerTests.cs`

- [ ] **Step 1: Write the failing sidecar and asset-manager tests**

```csharp
[Fact]
public void SerializeDeserialize_WhenVideoImportSettingsUseMp4Importer_RoundTripsImporterMetadata() {
    VideoTextureAssetImportSettings settings = new VideoTextureAssetImportSettings();
    settings.Importer.ImporterId = "windows-video";
    settings.Importer.SourceChecksum = "sha256:test";
    settings.Importer.AssetId = "cache/video/test";

    using MemoryStream stream = new MemoryStream();
    VideoTextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
    stream.Position = 0;

    VideoTextureAssetImportSettings roundTripped = VideoTextureAssetImportSettingsBinarySerializer.Deserialize(stream);
    Assert.Equal("windows-video", roundTripped.Importer.ImporterId);
    Assert.Equal("cache/video/test", roundTripped.Importer.AssetId);
}

[Fact]
public void TryLoadTextureSourceAsset_WhenSourceIsMp4_LoadsVideoTextureAsset() {
    string sourcePath = WriteSourceFile("intro.mp4", new byte[] { 1, 2, 3, 4 });
    AssetImportManager manager = CreateManager();
    manager.RegisterVideoImporter(new VideoImporterRegistration("windows-video", new TestVideoImporter(), new[] { ".mp4" }));

    bool loaded = manager.TryLoadTextureSourceAsset(sourcePath, out ITextureSourceAsset asset);

    Assert.True(loaded);
    VideoTextureAsset videoAsset = Assert.IsType<VideoTextureAsset>(asset);
    Assert.Equal(320, videoAsset.Width);
    Assert.Equal(180, videoAsset.Height);
    Assert.True(videoAsset.AutoPlay);
    Assert.True(videoAsset.Loop);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~VideoTextureAssetImportSettingsBinarySerializerTests|FullyQualifiedName~TryLoadTextureSourceAsset_WhenSourceIsMp4_LoadsVideoTextureAsset" -v minimal`

Expected: FAIL with missing video import-settings types, missing registration API, and missing `TryLoadTextureSourceAsset`.

- [ ] **Step 3: Implement the video importer pipeline**

```csharp
namespace helengine.editor {
    public sealed class VideoTextureAssetImportSettings {
        public VideoTextureAssetImportSettings() {
            Importer = new AssetImporterSettings();
        }

        public AssetImporterSettings Importer { get; set; }
    }

    public interface IVideoImporter {
        VideoTextureAsset Import(string sourcePath);
    }

    public sealed class VideoImporterRegistration : IAssetImporterRegistration {
        public VideoImporterRegistration(string importerId, IVideoImporter importer, string[] extensions) {
            ImporterId = importerId;
            Importer = importer;
            Extensions = extensions;
        }

        public string ImporterId { get; }
        public IVideoImporter Importer { get; }
        public string[] Extensions { get; }

        public void Register(AssetImportManager manager) => manager.RegisterVideoImporter(this);
    }
}

// AssetImportManager.cs
readonly Dictionary<string, IVideoImporter> videoImportersById;
readonly Dictionary<string, string> defaultVideoImportersByExtension;
readonly Dictionary<string, List<string>> videoImporterIdsByExtension;

public void RegisterVideoImporter(VideoImporterRegistration registration) { /* mirror texture registration */ }
public bool IsVideoTextureExtension(string extension) { /* extension lookup */ }

public bool TryLoadTextureSourceAsset(string sourcePath, out ITextureSourceAsset asset) {
    if (IsVideoTextureExtension(Path.GetExtension(sourcePath)) && TryLoadVideoTextureAsset(sourcePath, out VideoTextureAsset videoAsset)) {
        asset = videoAsset;
        return true;
    }
    if (TryLoadTextureAsset(sourcePath, out TextureAsset textureAsset)) {
        asset = textureAsset;
        return true;
    }
    asset = null;
    return false;
}

public bool TryLoadVideoTextureAsset(string sourcePath, out VideoTextureAsset asset) {
    VideoTextureAssetImportSettings settings;
    if (!TryLoadOrCreateVideoTextureImportSettings(sourcePath, out settings)) {
        asset = null;
        return false;
    }

    string outputPath = GetTextureAssetPath(settings.Importer.AssetId);
    if (!File.Exists(outputPath)) {
        asset = ImportVideoTexture(sourcePath);
        return true;
    }

    asset = ContentManager.Load<VideoTextureAsset>(outputPath, EditorContentProcessorIds.VideoTextureAsset);
    return true;
}
```

- [ ] **Step 4: Run the sidecar and manager tests again**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~VideoTextureAssetImportSettingsBinarySerializerTests|FullyQualifiedName~TryLoadTextureSourceAsset_WhenSourceIsMp4_LoadsVideoTextureAsset" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.editor/managers/asset/VideoTextureAssetImportSettings.cs engine/helengine.editor/content/video/IVideoImporter.cs engine/helengine.editor/content/video/VideoImporterContentProcessor.cs engine/helengine.editor/managers/asset/VideoImporterRegistration.cs engine/helengine.editor/serialization/VideoTextureAssetImportSettingsBinarySerializer.cs engine/helengine.editor/content/EditorContentProcessorIds.cs engine/helengine.editor/content/EditorContentManagerConfiguration.cs engine/helengine.editor/serialization/AssetImportSettingsBinaryValueKind.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests/testing/TestVideoImporter.cs engine/helengine.editor.tests/serialization/VideoTextureAssetImportSettingsBinarySerializerTests.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
git -C C:\dev\helworks\helengine commit -m "feat: import mp4 video texture assets"
```

## Task 3: Make Video Assets Texture-Compatible in Browser, Preview, and Scene Resolution

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetEntryKind.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorFileSystemTextureResolver.cs`
- Modify: `engine/helengine.editor/managers/preview/TexturePreviewSource.cs`
- Modify: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Test: `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`

- [ ] **Step 1: Write the failing preview and resolver tests**

```csharp
[Fact]
public void TryResolve_WhenVideoAssetIsSelected_ReturnsTexturePreviewSource() {
    PreviewSourceResolver resolver = CreateResolverWithVideoImporter();
    string sourcePath = WriteSourceFile("Preview.mp4", new byte[] { 1, 2, 3, 4 });
    AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
        "Preview.mp4",
        "Videos/Preview.mp4",
        sourcePath,
        ".mp4",
        AssetEntryKind.Video);

    bool resolved = resolver.TryResolve(entry, null, out IPreviewSource source);

    Assert.True(resolved);
    Assert.IsType<TexturePreviewSource>(source);
}

[Fact]
public void ResolveTexture_WhenReferenceIsSourceVideo_UsesBuildTextureFromVideo() {
    string videoPath = Path.Combine(TempProjectRootPath, "assets", "Videos", "Loop.mp4");
    Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);
    File.WriteAllBytes(videoPath, new byte[] { 1, 2, 3, 4 });
    AssetImportManager assetImportManager = CreateAssetImportManagerWithVideoImporter();
    EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
        new ContentManager(TempProjectRootPath),
        TempProjectRootPath,
        new EditorFileSystemModelResolver(assetImportManager),
        new EditorFileSystemFontResolver(assetImportManager),
        new EditorFileSystemTextureResolver(assetImportManager));

    RuntimeTexture texture = resolver.ResolveTexture(new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Videos/Loop.mp4"
    });

    TestRenderManager2D renderManager = Assert.IsType<TestRenderManager2D>(Core.Instance.RenderManager2D);
    Assert.NotNull(texture);
    Assert.Equal(1, renderManager.BuildTextureFromVideoCallCount);
    Assert.Equal(0, renderManager.BuildTextureFromRawCallCount);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~TryResolve_WhenVideoAssetIsSelected_ReturnsTexturePreviewSource|FullyQualifiedName~ResolveTexture_WhenReferenceIsSourceVideo_UsesBuildTextureFromVideo" -v minimal`

Expected: FAIL because `.mp4` is not classified as previewable and `EditorSceneAssetReferenceResolver` only calls `BuildTextureFromRaw`.

- [ ] **Step 3: Implement the texture-compatible editor integration**

```csharp
public enum AssetEntryKind {
    Directory,
    Image,
    Video,
    Model,
    Material,
    Scene,
    Audio,
    Script,
    Config,
    Font,
    Unknown,
    File
}

// EditorAssetManager.cs
if (videoExtensions.Contains(extension)) {
    return AssetEntryKind.Video;
}

// EditorFileSystemTextureResolver.cs
public ITextureSourceAsset ResolveTextureSourceAsset(string sourcePath) {
    if (!AssetImportManager.TryLoadTextureSourceAsset(sourcePath, out ITextureSourceAsset asset) || asset == null) {
        throw new InvalidOperationException($"Texture reference '{sourcePath}' could not be imported into a texture source asset.");
    }

    return asset;
}

// TexturePreviewSource.cs
ITextureSourceAsset textureSourceAsset;
if (!assetImportManager.TryLoadTextureSourceAsset(entry.FullPath, out textureSourceAsset)) {
    source = null;
    return false;
}

RuntimeTexture runtimeTexture = textureSourceAsset switch {
    VideoTextureAsset videoAsset => renderManager2D.BuildTextureFromVideo(videoAsset, entry.FullPath),
    TextureAsset textureAsset => renderManager2D.BuildTextureFromRaw(textureAsset),
    _ => throw new InvalidOperationException("Unsupported texture source asset.")
};

source = new TexturePreviewSource(runtimeTexture);
return true;

// EditorSceneAssetReferenceResolver.cs
ITextureSourceAsset textureSourceAsset = ResolveFileSystemTextureSource(reference);
return textureSourceAsset switch {
    VideoTextureAsset videoAsset => Core.Instance.RenderManager2D.BuildTextureFromVideo(videoAsset, fullPath),
    TextureAsset textureAsset => Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset),
    _ => throw new InvalidOperationException("Unsupported texture source asset.")
};
```

- [ ] **Step 4: Run the preview and resolver tests again**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~TryResolve_WhenVideoAssetIsSelected_ReturnsTexturePreviewSource|FullyQualifiedName~ResolveTexture_WhenReferenceIsSourceVideo_UsesBuildTextureFromVideo" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.editor/managers/asset/AssetEntryKind.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor/managers/asset/EditorFileSystemTextureResolver.cs engine/helengine.editor/managers/preview/TexturePreviewSource.cs engine/helengine.editor/managers/preview/PreviewSourceResolver.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs
git -C C:\dev\helworks\helengine commit -m "feat: wire video assets into editor texture flows"
```

## Task 4: Add the Render-Manager Video Seam and Update Hook

**Files:**
- Modify: `engine/helengine.core/managers/rendering/RenderManager2D.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager2D.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`

- [ ] **Step 1: Write the failing renderer-seam tests**

```csharp
[Fact]
public void ResolveTexture_WhenReferenceIsSourceVideo_UsesBuildTextureFromVideo() {
    // existing resolver test from Task 3 now asserts the new call count
}

[Fact]
public void UpdateFrame_WhenCalled_UpdatesRenderManager2DBeforeDraw() {
    TestRenderManager2D render2D = new TestRenderManager2D();
    EditorSession session = CreateSession(render2D);

    session.UpdateFrame(1280, 720);

    Assert.Equal(1, render2D.UpdateCallCount);
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~UsesBuildTextureFromVideo|FullyQualifiedName~UpdatesRenderManager2DBeforeDraw" -v minimal`

Expected: FAIL because `RenderManager2D` does not expose `BuildTextureFromVideo` and editor frames do not call `RenderManager2D.Update()`.

- [ ] **Step 3: Add the explicit video-build seam**

```csharp
public abstract class RenderManager2D : IDisposable {
    public abstract RuntimeTexture BuildTextureFromRaw(TextureAsset data);

    public virtual RuntimeTexture BuildTextureFromVideo(VideoTextureAsset data, string sourcePath) {
        if (data == null) {
            throw new ArgumentNullException(nameof(data));
        }
        if (string.IsNullOrWhiteSpace(sourcePath)) {
            throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
        }

        throw new NotSupportedException("This renderer does not support video texture creation.");
    }

    public virtual void Update() { }
}

internal class TestRenderManager2D : RenderManager2D {
    public int UpdateCallCount { get; private set; }
    public int BuildTextureFromVideoCallCount { get; private set; }

    public override RuntimeTexture BuildTextureFromVideo(VideoTextureAsset data, string sourcePath) {
        BuildTextureFromVideoCallCount++;
        return new TestRuntimeTexture {
            Width = data.Width,
            Height = data.Height
        };
    }

    public override void Update() {
        UpdateCallCount++;
    }
}

// EditorSession.cs
public void UpdateFrame(int renderWidth, int renderHeight) {
    Update();
    core.RenderManager2D?.Update();
    UpdateLayout(renderWidth, renderHeight);
    bool layoutDirty = UpdateDocking(renderWidth, renderHeight);
    if (layoutDirty) {
        UpdateLayout(renderWidth, renderHeight);
    }
    RefreshHierarchy();
    Draw();
}
```

- [ ] **Step 4: Run the renderer-seam tests again**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~UsesBuildTextureFromVideo|FullyQualifiedName~UpdatesRenderManager2DBeforeDraw" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.core/managers/rendering/RenderManager2D.cs engine/helengine.editor.tests/testing/TestRenderManager2D.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs
git -C C:\dev\helworks\helengine commit -m "feat: add render manager video texture seam"
```

## Task 5: Implement the DirectX11 Video Texture Runtime

**Files:**
- Create: `engine/helengine.directx11/DirectX11VideoTextureResource.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer2D.cs`
- Modify: `engine/helengine.directx11.video/DirectX11VideoDecoder.cs`
- Test: `engine/helengine.editor.windows.tests/rendering/DirectX11VideoTextureResourceTests.cs`

- [ ] **Step 1: Write the failing DirectX11 runtime test**

```csharp
[Fact]
public void BuildTextureFromVideo_WhenVideoAssetIsValid_ReturnsDirectX11VideoTextureResource() {
    DirectX11Renderer2D renderer = CreateRenderer();
    VideoTextureAsset asset = new VideoTextureAsset {
        Width = 320,
        Height = 180,
        AutoPlay = true,
        Loop = true
    };

    RuntimeTexture texture = renderer.BuildTextureFromVideo(asset, ResolveSampleVideoPath("loop.mp4"));

    DirectX11VideoTextureResource videoTexture = Assert.IsType<DirectX11VideoTextureResource>(texture);
    Assert.Equal(320, videoTexture.Width);
    Assert.Equal(180, videoTexture.Height);
    Assert.NotNull(videoTexture.Resource);
}
```

- [ ] **Step 2: Run the DirectX11 runtime test to verify it fails**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~BuildTextureFromVideo_WhenVideoAssetIsValid_ReturnsDirectX11VideoTextureResource" -v minimal`

Expected: FAIL because `DirectX11Renderer2D` does not override `BuildTextureFromVideo`.

- [ ] **Step 3: Implement the stable GPU texture resource and per-frame update path**

```csharp
public sealed class DirectX11VideoTextureResource : DirectX11TextureResource {
    readonly DirectX11VideoDecoder decoder;
    readonly Device device;
    readonly DeviceContext context;
    TimeSpan playbackClock;

    public DirectX11VideoTextureResource(Device device, string sourcePath, VideoTextureAsset asset) {
        this.device = device;
        context = device.ImmediateContext;
        Width = asset.Width;
        Height = asset.Height;
        decoder = new DirectX11VideoDecoder(new DirectX11VideoDecoderOptions(device, sourcePath, VideoDecoderHardwareMode.Required));
        Texture = CreateDestinationTexture(device, asset.Width, asset.Height);
        Resource = new ShaderResourceView(device, Texture);
    }

    public void Update(double elapsedSeconds) {
        if (IsDisposed) {
            return;
        }

        playbackClock += TimeSpan.FromSeconds(elapsedSeconds);
        while (decoder.TryGetNextFrame(out VideoFrame frame)) {
            using (frame) {
                if (frame.Timestamp > playbackClock) {
                    break;
                }

                using Texture2D frameTexture = frame.CreateTextureReference();
                context.CopySubresourceRegion(frameTexture, frame.SubresourceIndex, null, Texture, 0);
            }
        }
    }

    public override void Dispose() {
        if (IsDisposed) {
            return;
        }
        Resource?.Dispose();
        Texture?.Dispose();
        decoder.Dispose();
        base.Dispose();
    }
}

// DirectX11Renderer2D.cs
readonly List<DirectX11VideoTextureResource> activeVideoTextures = new();

public override RuntimeTexture BuildTextureFromVideo(VideoTextureAsset data, string sourcePath) {
    DirectX11VideoTextureResource resource = new DirectX11VideoTextureResource(Device, sourcePath, data);
    activeVideoTextures.Add(resource);
    return resource;
}

public override void Update() {
    double deltaSeconds = Core.Instance?.FrameDeltaSeconds ?? 0d;
    for (int index = activeVideoTextures.Count - 1; index >= 0; index--) {
        DirectX11VideoTextureResource texture = activeVideoTextures[index];
        if (texture.IsDisposed) {
            activeVideoTextures.RemoveAt(index);
            continue;
        }

        texture.Update(deltaSeconds);
    }
}
```

- [ ] **Step 4: Run the DirectX11 runtime test again**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~BuildTextureFromVideo_WhenVideoAssetIsValid_ReturnsDirectX11VideoTextureResource" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.directx11/DirectX11VideoTextureResource.cs engine/helengine.directx11/DirectX11Renderer2D.cs engine/helengine.directx11.video/DirectX11VideoDecoder.cs engine/helengine.editor.windows.tests/rendering/DirectX11VideoTextureResourceTests.cs
git -C C:\dev\helworks\helengine commit -m "feat: add directx11 video texture runtime"
```

## Task 6: Add the Native FFmpeg Probe/Decoder DLL and Windows Host Importer Registration

**Files:**
- Create: `engine/helengine.directx11.video/VideoFileProbe.cs`
- Modify: `engine/helengine.directx11.video/FfmpegNativeApi.cs`
- Create: `engine/helengine.editor.windows/content/video/WindowsVideoTextureImporter.cs`
- Modify: `engine/helengine.editor.windows/content/textures/EditorHostTextureImporterFactory.cs`
- Create: `engine/helengine.video.ffmpeg/helengine.video.ffmpeg.vcxproj`
- Create: `engine/helengine.video.ffmpeg/include/helengine_video_ffmpeg.h`
- Create: `engine/helengine.video.ffmpeg/src/VideoFileProbeExports.cpp`
- Create: `engine/helengine.video.ffmpeg/src/VideoDecoderExports.cpp`
- Modify: `helengine.ui/helengine.sln`
- Test: `engine/helengine.editor.windows.tests/content/textures/EditorHostTextureImporterFactoryTests.cs`

- [ ] **Step 1: Write the failing importer-registration test**

```csharp
[Fact]
public void CreateDefault_WhenCalled_IncludesVideoImporterForMp4() {
    IReadOnlyList<IAssetImporterRegistration> registrations = EditorHostTextureImporterFactory.CreateDefault();

    VideoImporterRegistration videoRegistration = Assert.IsType<VideoImporterRegistration>(
        registrations.Single(registration => registration is VideoImporterRegistration));

    Assert.Equal("windows-video", videoRegistration.ImporterId);
    Assert.Equal(new[] { ".mp4" }, videoRegistration.Extensions, StringComparer.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the host importer test to verify it fails**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~IncludesVideoImporterForMp4" -v minimal`

Expected: FAIL because no video importer registration exists.

- [ ] **Step 3: Add the metadata probe, importer, and native exports**

```csharp
public sealed class VideoFileProbe : IDisposable {
    IntPtr handle;

    public VideoFileProbe(string sourcePath) {
        handle = FfmpegNativeApi.he_video_probe_open(sourcePath, out FfmpegNativeVideoStreamInfo info);
        if (handle == IntPtr.Zero) {
            throw new InvalidOperationException("Video probe failed.");
        }

        StreamInfo = new VideoStreamInfo(info.Width, info.Height, info.FrameRate, TimeSpan.FromTicks(info.DurationTicks), info.FrameFormat, false);
        CodecId = info.CodecId;
        ContainerId = info.ContainerId;
    }

    public VideoStreamInfo StreamInfo { get; }
    public string CodecId { get; }
    public string ContainerId { get; }
}

public sealed class WindowsVideoTextureImporter : IVideoImporter {
    public VideoTextureAsset Import(string sourcePath) {
        using VideoFileProbe probe = new VideoFileProbe(sourcePath);
        if (!string.Equals(probe.ContainerId, "mp4", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(probe.CodecId, "h264", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("Only H.264 `.mp4` video assets are supported.");
        }

        return new VideoTextureAsset {
            Width = checked((ushort)probe.StreamInfo.Width),
            Height = checked((ushort)probe.StreamInfo.Height),
            FrameRate = probe.StreamInfo.FrameRate,
            DurationTicks = probe.StreamInfo.Duration.Ticks,
            SourceExtension = ".mp4",
            CodecId = probe.CodecId,
            ContainerId = probe.ContainerId,
            AutoPlay = true,
            Loop = true
        };
    }
}

// EditorHostTextureImporterFactory.cs
return [
    CreateTextureRegistration("gdi", ...),
    CreateTextureRegistration("pfim", ...),
    CreateTextureRegistration("magick", ...),
    new VideoImporterRegistration("windows-video", new WindowsVideoTextureImporter(), new[] { ".mp4" })
];
```

```cpp
extern "C" __declspec(dllexport) void* he_video_probe_open(const wchar_t* sourcePath, he_video_stream_info* streamInfo);
extern "C" __declspec(dllexport) void  he_video_probe_close(void* probe);
extern "C" __declspec(dllexport) void* he_video_decoder_create(void* d3d11Device, const wchar_t* sourcePath, int hardwareMode, he_video_stream_info* streamInfo);
extern "C" __declspec(dllexport) int   he_video_decoder_try_get_frame(void* decoder, he_video_frame* frame);
extern "C" __declspec(dllexport) void  he_video_decoder_release_frame(void* decoder, he_video_frame* frame);
extern "C" __declspec(dllexport) int   he_video_decoder_seek(void* decoder, long long timestampTicks);
extern "C" __declspec(dllexport) void  he_video_decoder_flush(void* decoder);
extern "C" __declspec(dllexport) void  he_video_decoder_destroy(void* decoder);
```

- [ ] **Step 4: Run the host importer test again**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~IncludesVideoImporterForMp4" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.directx11.video/VideoFileProbe.cs engine/helengine.directx11.video/FfmpegNativeApi.cs engine/helengine.editor.windows/content/video/WindowsVideoTextureImporter.cs engine/helengine.editor.windows/content/textures/EditorHostTextureImporterFactory.cs engine/helengine.video.ffmpeg/helengine.video.ffmpeg.vcxproj engine/helengine.video.ffmpeg/include/helengine_video_ffmpeg.h engine/helengine.video.ffmpeg/src/VideoFileProbeExports.cpp engine/helengine.video.ffmpeg/src/VideoDecoderExports.cpp helengine.ui/helengine.sln engine/helengine.editor.windows.tests/content/textures/EditorHostTextureImporterFactoryTests.cs
git -C C:\dev\helworks\helengine commit -m "feat: add ffmpeg-backed mp4 video importer"
```

## Task 7: Run Focused Managed Verification and One Editor Smoke Pass

**Files:**
- Test: `engine/helengine.editor.tests/serialization/VideoTextureAssetBinarySerializerTests.cs`
- Test: `engine/helengine.editor.tests/serialization/VideoTextureAssetImportSettingsBinarySerializerTests.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Test: `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Test: `engine/helengine.editor.windows.tests/rendering/DirectX11VideoTextureResourceTests.cs`
- Test: `engine/helengine.editor.windows.tests/content/textures/EditorHostTextureImporterFactoryTests.cs`

- [ ] **Step 1: Run the focused managed test slice**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~VideoTextureAsset|FullyQualifiedName~VideoTextureAssetImportSettings|FullyQualifiedName~TryResolve_WhenVideoAssetIsSelected|FullyQualifiedName~ResolveTexture_WhenReferenceIsSourceVideo|FullyQualifiedName~BuildTextureFromVideo_WhenVideoAssetIsValid|FullyQualifiedName~IncludesVideoImporterForMp4" -v minimal`

Expected: PASS with the new video-asset, import, preview, and resolver tests green.

- [ ] **Step 2: Run one broader editor regression slice that exercises texture-adjacent code**

Run: `dotnet test C:\dev\helworks\helengine\helengine.ui\helengine.sln --filter "FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~PreviewSourceResolverTests|FullyQualifiedName~EditorSceneAssetReferenceResolverTests" -v minimal`

Expected: PASS without breaking existing texture, model, and preview behavior.

- [ ] **Step 3: Perform one manual editor smoke pass**

```text
1. Place one H.264 `.mp4` under `assets/Videos/`.
2. Open the asset browser and confirm the file appears with the video category.
3. Select the `.mp4` and confirm the preview panel shows moving frames.
4. Assign the `.mp4` to one sprite or material texture field that already accepts textures.
5. Load the scene and confirm autoplay + looping.
6. Change scene or close the editor and watch for clean disposal without repeated error spam.
```

- [ ] **Step 4: Record the smoke-pass outcome in the working notes**

```text
- Sample asset: assets/Videos/<sample>.mp4
- Result: autoplay/loop OK or exact failure
- Logs: note first bounded error if decoder/probe/native DLL load fails
- Follow-up: only file bugs that reproduce after a clean editor restart
```

- [ ] **Step 5: Commit**

```bash
git -C C:\dev\helworks\helengine status --short
git -C C:\dev\helworks\helengine commit --allow-empty -m "chore: verify video texture editor integration"
```

## Self-Review

- Spec coverage: authored `VideoTextureAsset`, `.mp4` import, texture-compatible scene usage, autoplay/loop-only playback, ignored audio, DirectX11 GPU decode path, editor-only scope, placeholder-on-failure behavior, and native FFmpeg backend are all covered by Tasks 1-7.
- Placeholder scan: no `TODO`, `TBD`, or "implement later" instructions remain in the task steps.
- Type consistency: the plan uses `VideoTextureAsset`, `ITextureSourceAsset`, `TryLoadTextureSourceAsset`, `BuildTextureFromVideo(VideoTextureAsset, string)`, and `DirectX11VideoTextureResource` consistently across all tasks.
- Intentional scope limit: runtime packaging/player support stays untouched; only editor import, preview, scene resolution, and DirectX11 playback are implemented here.
