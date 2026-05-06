# Source Font Import And Cooked Hefont Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace authored `.hefont` project assets with imported source font files while keeping runtime/player builds on cooked `.hefont` output.

**Architecture:** Extend the existing editor asset-import pipeline with a real font importer category, then route editor font loading and Windows build cooking through that shared path. Persist authored source font references in scenes and code, and rewrite them to `cooked/<relative>.hefont` only during packaging.

**Tech Stack:** C#/.NET 9, existing `ContentManager` processors, `AssetImportManager`, `GDIFontProcessor`, editor scene persistence, Windows build packaging.

---

### Task 1: Add Font Importer Infrastructure

**Files:**
- Create: `engine/helengine.editor/managers/asset/FontImporterRegistration.cs`
- Create: `engine/helengine.editor/content/font/FontImporterContentProcessor.cs`
- Create: `engine/helengine.editor.windows/content/font/GdiFontImporter.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `helengine.ui/helengine.editor.app/EditorHostImporterFactory.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerTests.cs`

- [ ] **Step 1: Write the failing font-import manager test**

```csharp
[Fact]
public void TryLoadFontAsset_WhenSourceFontExists_ImportsAndCachesFontAsset() {
    string sourcePath = WriteSourceFont("demo-title.ttf");
    ContentManager contentManager = new ContentManager(AssetsRootPath);
    AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
    manager.RegisterFontImporter(new FontImporterRegistration("gdi-font", new TestFontImporter(), new[] { ".ttf" }));

    bool loaded = manager.TryLoadFontAsset(sourcePath, out FontAsset asset);

    Assert.True(loaded);
    Assert.NotNull(asset);

    AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
    string outputPath = Path.Combine(CacheRootPath, settings.Importer.AssetId);
    Assert.True(File.Exists(outputPath));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests.TryLoadFontAsset_WhenSourceFontExists_ImportsAndCachesFontAsset" -v minimal`

Expected: FAIL because `AssetImportManager` does not expose `RegisterFontImporter` or `TryLoadFontAsset`.

- [ ] **Step 3: Add font importer registration and content processor types**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Describes a font importer registration with supported extensions.
    /// </summary>
    public class FontImporterRegistration : IAssetImporterRegistration {
        readonly string importerId;
        readonly IFontImporter importer;
        readonly string[] extensions;

        public FontImporterRegistration(string importerId, IFontImporter importer, string[] extensions) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }
            if (importer == null) {
                throw new ArgumentNullException(nameof(importer));
            }
            if (extensions == null || extensions.Length == 0) {
                throw new ArgumentException("At least one extension must be provided.", nameof(extensions));
            }

            this.importerId = importerId;
            this.importer = importer;
            this.extensions = NormalizeExtensions(extensions);
        }

        public string ImporterId => importerId;
        public IFontImporter Importer => importer;
        public string[] Extensions => extensions;

        public void Register(AssetImportManager manager) {
            if (manager == null) {
                throw new ArgumentNullException(nameof(manager));
            }

            manager.RegisterFontImporter(this);
        }

        string[] NormalizeExtensions(string[] sourceExtensions) {
            string[] normalized = new string[sourceExtensions.Length];
            for (int index = 0; index < sourceExtensions.Length; index++) {
                string extension = sourceExtensions[index];
                if (string.IsNullOrWhiteSpace(extension)) {
                    throw new ArgumentException("Extension values must be non-empty.", nameof(sourceExtensions));
                }
                if (!extension.StartsWith(".")) {
                    extension = "." + extension;
                }

                normalized[index] = extension.ToLowerInvariant();
            }

            return normalized;
        }
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Wraps one font importer inside the shared content-processor contract.
    /// </summary>
    public class FontImporterContentProcessor : IContentProcessor<FontAsset> {
        readonly IFontImporter importer;

        public FontImporterContentProcessor(IFontImporter importer) {
            this.importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        public FontAsset Read(Stream stream) {
            return importer.ImportFont(stream);
        }

        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
```

```csharp
namespace helengine.editor.windows {
    /// <summary>
    /// Imports source font files through the existing GDI-backed font rasterization path.
    /// </summary>
    public sealed class GdiFontImporter : IFontImporter {
        public FontAsset ImportFont(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (bytes.Length == 0) {
                throw new InvalidOperationException("Font source stream must contain data.");
            }

            using PrivateFontCollection fontCollection = new PrivateFontCollection();
            unsafe {
                fixed (byte* bytesPointer = bytes) {
                    fontCollection.AddMemoryFont((nint)bytesPointer, bytes.Length);
                }
            }

            if (fontCollection.Families.Length == 0) {
                throw new InvalidOperationException("Source font did not produce any installable font families.");
            }

            using Font font = new Font(fontCollection.Families[0], 32f, FontStyle.Regular, GraphicsUnit.Pixel);
            return GDIFontProcessor.ImportFont(font);
        }
    }
}
```

- [ ] **Step 4: Extend `AssetImportManager` with a first-class font branch**

```csharp
readonly Dictionary<string, IFontImporter> fontImportersById;
readonly Dictionary<string, string> defaultFontImportersByExtension;

public void RegisterFontImporter(FontImporterRegistration registration) {
    if (registration == null) {
        throw new ArgumentNullException(nameof(registration));
    }

    if (fontImportersById.ContainsKey(registration.ImporterId)) {
        throw new InvalidOperationException($"Font importer '{registration.ImporterId}' is already registered.");
    }

    fontImportersById.Add(registration.ImporterId, registration.Importer);
    AssetContentManager.RegisterProcessor(
        registration.ImporterId,
        new FontImporterContentProcessor(registration.Importer),
        registration.Extensions);

    for (int index = 0; index < registration.Extensions.Length; index++) {
        string extension = NormalizeExtension(registration.Extensions[index]);
        defaultFontImportersByExtension[extension] = registration.ImporterId;
    }
}

public bool IsFontExtension(string extension) {
    if (string.IsNullOrWhiteSpace(extension)) {
        return false;
    }

    return defaultFontImportersByExtension.ContainsKey(NormalizeExtension(extension));
}

public FontAsset ImportFont(string sourcePath) {
    AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
    EnsureFontImporterExists(settings.Importer.ImporterId);
    FontAsset asset = AssetContentManager.Load<FontAsset>(sourcePath, settings.Importer.ImporterId);
    if (asset == null) {
        throw new InvalidOperationException($"Font importer '{settings.Importer.ImporterId}' did not return an asset.");
    }

    asset.Id = settings.Importer.AssetId;
    string outputPath = GetFontAssetPath(settings.Importer.AssetId);
    EnsureDirectoryForFile(outputPath);
    using FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
    AssetSerializer.Serialize(stream, asset);
    SaveImportSettings(sourcePath, settings);
    return asset;
}

public bool TryLoadFontAsset(string sourcePath, out FontAsset asset) {
    AssetImportSettings settings;
    if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
        asset = null;
        return false;
    }

    if (!IsFontImporterRegistered(settings.Importer.ImporterId)) {
        asset = null;
        return false;
    }

    string outputPath = GetFontAssetPath(settings.Importer.AssetId);
    if (!File.Exists(outputPath)) {
        asset = ImportFont(sourcePath);
        return true;
    }

    if (TryLoadCachedFontAsset(outputPath, out asset)) {
        return true;
    }

    asset = ImportFont(sourcePath);
    return true;
}
```

- [ ] **Step 5: Register the Windows font importer in the editor host**

```csharp
public static IReadOnlyList<IAssetImporterRegistration> CreateDefault() {
    string[] textExtensions = new[] { ".txt" };
    string[] modelExtensions = new[] { ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds" };
    string[] fontExtensions = new[] { ".ttf", ".otf" };
    List<IAssetImporterRegistration> registrations = new List<IAssetImporterRegistration>(EditorHostTextureImporterFactory.CreateDefault());
    registrations.AddRange(new IAssetImporterRegistration[] {
        new TextImporterRegistration("text", new TextImporter(), textExtensions),
        new FontImporterRegistration("gdi-font", new GdiFontImporter(), fontExtensions),
        new ModelImporterRegistration(
            "assimp",
            new LazyModelImporter(new AssemblyModelImporterFactory("helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")),
            modelExtensions)
    });

    return registrations;
}
```

- [ ] **Step 6: Add the test-only font importer helper**

```csharp
sealed class TestFontImporter : IFontImporter {
    public FontAsset ImportFont(Stream stream) {
        return new FontAsset(
            new FontInfo("ImportedTestFont", 16, 4f),
            new TestRuntimeTexture {
                Width = 1,
                Height = 1
            },
            new Dictionary<char, FontChar>(),
            16f,
            1,
            1);
    }
}

string WriteSourceFont(string fileName) {
    string sourcePath = Path.Combine(AssetsRootPath, fileName);
    File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
    return sourcePath;
}
```

- [ ] **Step 7: Run the focused import-manager test**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests.TryLoadFontAsset_WhenSourceFontExists_ImportsAndCachesFontAsset" -v minimal`

Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add engine/helengine.editor/managers/asset/FontImporterRegistration.cs engine/helengine.editor/content/font/FontImporterContentProcessor.cs engine/helengine.editor.windows/content/font/GdiFontImporter.cs engine/helengine.editor/managers/asset/AssetImportManager.cs helengine.ui/helengine.editor.app/EditorHostImporterFactory.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
git commit -m "feat: add source font importer pipeline"
```

### Task 2: Route Editor Font Loading Through Imported Source Fonts

**Files:**
- Create: `engine/helengine.editor/managers/asset/EditorFileSystemFontResolver.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetEntryKind.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`

- [ ] **Step 1: Write the failing editor resolver test**

```csharp
[Fact]
public void ResolveFont_WhenReferenceIsSourceFont_UsesImportedFontAsset() {
    string fontPath = Path.Combine(TempProjectRootPath, "assets", "Fonts", "DemoDiscTitle.ttf");
    Directory.CreateDirectory(Path.GetDirectoryName(fontPath));
    File.WriteAllBytes(fontPath, new byte[] { 1, 2, 3, 4 });

    AssetImportManager assetImportManager = CreateAssetImportManager();
    assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" }));
    EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
        new ContentManager(TempProjectRootPath),
        TempProjectRootPath,
        new EditorFileSystemModelResolver(assetImportManager),
        new EditorFileSystemFontResolver(assetImportManager));

    FontAsset font = resolver.ResolveFont(new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Fonts/DemoDiscTitle.ttf"
    });

    Assert.Equal("ImportedTestFont", font.FontInfo.Name);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSceneAssetReferenceResolverTests.ResolveFont_WhenReferenceIsSourceFont_UsesImportedFontAsset" -v minimal`

Expected: FAIL because `EditorSceneAssetReferenceResolver` has no source-font resolver path.

- [ ] **Step 3: Add a dedicated editor file-system font resolver**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Resolves authored file-backed source fonts through the shared asset import manager.
    /// </summary>
    public sealed class EditorFileSystemFontResolver {
        readonly AssetImportManager AssetImportManager;

        public EditorFileSystemFontResolver(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        public FontAsset ResolveFontAsset(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!AssetImportManager.TryLoadFontAsset(sourcePath, out FontAsset fontAsset) || fontAsset == null) {
                throw new InvalidOperationException($"Font reference '{sourcePath}' could not be imported into a FontAsset.");
            }

            return fontAsset;
        }
    }
}
```

- [ ] **Step 4: Thread the resolver through editor scene loading and property editing**

```csharp
readonly EditorFileSystemFontResolver FileSystemFontResolver;

public EditorSceneAssetReferenceResolver(
    ContentManager assetContentManager,
    string projectRootPath,
    EditorFileSystemModelResolver fileSystemModelResolver,
    EditorFileSystemFontResolver fileSystemFontResolver) {
    AssetContentManager = assetContentManager ?? throw new ArgumentNullException(nameof(assetContentManager));
    ProjectRootPath = Path.GetFullPath(projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath)));
    AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
    FileSystemModelResolver = fileSystemModelResolver;
    FileSystemFontResolver = fileSystemFontResolver;
}

FontAsset ResolveFileSystemFont(SceneAssetReference reference) {
    string fullPath = ResolveFileSystemAssetPath(reference);
    if (FileSystemFontResolver != null) {
        return FileSystemFontResolver.ResolveFontAsset(fullPath);
    }

    return AssetContentManager.Load<FontAsset>(fullPath, RuntimeContentProcessorIds.FontAsset);
}
```

```csharp
FontAsset LoadFont(AssetBrowserEntry entry) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    if (entry.IsGenerated) {
        throw new InvalidOperationException("Font picker does not support generated font assets.");
    }

    return FileSystemFontResolver.ResolveFontAsset(entry.FullPath);
}
```

- [ ] **Step 5: Classify source font files explicitly in the asset browser**

```csharp
public enum AssetEntryKind {
    Directory,
    Image,
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
```

```csharp
readonly HashSet<string> fontExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    ".ttf", ".otf"
};

AssetEntryKind ClassifyEntryKind(string extension) {
    if (string.IsNullOrWhiteSpace(extension)) {
        return AssetEntryKind.Unknown;
    }
    if (imageExtensions.Contains(extension)) {
        return AssetEntryKind.Image;
    }
    if (modelExtensions.Contains(extension)) {
        return AssetEntryKind.Model;
    }
    if (materialExtensions.Contains(extension)) {
        return AssetEntryKind.Material;
    }
    if (sceneExtensions.Contains(extension)) {
        return AssetEntryKind.Scene;
    }
    if (audioExtensions.Contains(extension)) {
        return AssetEntryKind.Audio;
    }
    if (scriptExtensions.Contains(extension)) {
        return AssetEntryKind.Script;
    }
    if (configExtensions.Contains(extension)) {
        return AssetEntryKind.Config;
    }
    if (fontExtensions.Contains(extension)) {
        return AssetEntryKind.Font;
    }

    return AssetEntryKind.File;
}
```

- [ ] **Step 6: Add asset browser presentation for `Font` entries**

```csharp
case AssetEntryKind.Font:
    color = new byte4(114, 149, 255, 255);
    label = "Fn";
    textColor = new byte4(255, 255, 255, 255);
    break;
```

- [ ] **Step 7: Run the focused resolver test**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSceneAssetReferenceResolverTests.ResolveFont_WhenReferenceIsSourceFont_UsesImportedFontAsset" -v minimal`

Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add engine/helengine.editor/managers/asset/EditorFileSystemFontResolver.cs engine/helengine.editor/managers/asset/AssetEntryKind.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs
git commit -m "feat: resolve source fonts in editor"
```

### Task 3: Cook Source Fonts Into Runtime `.hefont` Output

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing packager regression for source-font paths**

```csharp
[Fact]
public void Package_WhenSceneContainsSourceFontReference_WritesCookedHefontAndRewritesPayload() {
    string sceneId = "Scenes/TextScene.helen";
    string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
    WriteSourceFont(fontRelativePath);
    WriteSceneAsset(sceneId, "Helengine.TextComponent", WriteTextComponentPayload(CreateFileFontReference(fontRelativePath)), new[] { CreateFileFontReference(fontRelativePath) });

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        ProjectRootPath,
        CreateImporters(),
        CreatePackagedFontAsset());

    packager.Package(new[] { sceneId }, BuildRootPath);

    Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "Fonts", "DemoDiscTitle.hefont")));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenSceneContainsSourceFontReference_WritesCookedHefontAndRewritesPayload" -v minimal`

Expected: FAIL because the packager does not import `.ttf`/`.otf` sources into cooked `.hefont`.

- [ ] **Step 3: Rewrite scene-level and component-level font references through imported source fonts**

```csharp
SceneAssetReference RewriteFontReference(SceneAssetReference reference, string buildRootPath) {
    if (reference == null) {
        throw new InvalidOperationException("Text and FPS components require a font reference before packaging.");
    }

    if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
        if (string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal) &&
            string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
            WriteFontAsset(Path.Combine(buildRootPath, EditorFontRelativePath), DefaultFontAsset);
            return CreateFileSystemReference(EditorFontRelativePath);
        }

        throw new InvalidOperationException($"Unsupported generated font provider '{reference.ProviderId}:{reference.AssetId}'.");
    }

    if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
        string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
        FontAsset fontAsset = ResolveFileSystemFontAsset(sourcePath);
        string cookedRelativePath = BuildCookedFontRelativePath(reference.RelativePath);
        WriteFontAsset(Path.Combine(buildRootPath, cookedRelativePath), fontAsset);
        return CreateFileSystemReference(cookedRelativePath);
    }

    throw new InvalidOperationException($"Unsupported font reference source kind '{reference.SourceKind}'.");
}

FontAsset ResolveFileSystemFontAsset(string sourcePath) {
    if (!AssetImportManager.TryLoadFontAsset(sourcePath, out FontAsset fontAsset) || fontAsset == null) {
        throw new InvalidOperationException($"Font source '{sourcePath}' could not be imported for packaging.");
    }

    return fontAsset;
}

static string BuildCookedFontRelativePath(string relativePath) {
    string normalized = NormalizeRelativePath(relativePath);
    string changedExtensionPath = Path.ChangeExtension(normalized, ".hefont");
    return NormalizeRelativePath(Path.Combine("cooked", changedExtensionPath));
}
```

- [ ] **Step 4: Add runtime load coverage for packaged source-font scenes**

```csharp
[Fact]
public void Load_WhenPackagedSceneUsesSourceFontReference_ResolvesCookedFontAsset() {
    string sceneId = "Scenes/MenuScene.helen";
    WriteSourceFont("Fonts/DemoDiscTitle.ttf");
    WriteSourceFont("Fonts/DemoDiscBody.ttf");
    WriteSceneAsset(sceneId, BuildDemoMenuSceneAssetWithSourceFonts(sceneId));

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        ProjectRootPath,
        CreateImporters(),
        CreatePackagedFontAsset());
    packager.Package(new[] { sceneId }, BuildRootPath);

    string packagedScenePath = Path.Combine(BuildRootPath, EditorPlatformBuildScenePackager.MainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
    using FileStream stream = File.OpenRead(packagedScenePath);
    SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

    InitializeRuntimeCore(BuildRootPath);
    ContentManager runtimeContentManager = new ContentManager(BuildRootPath);
    RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(runtimeContentManager);
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(
        new RuntimeSceneAssetReferenceResolver(runtimeContentManager, BuildRootPath, ShaderCompileTarget.DirectX11),
        RuntimeComponentRegistry.CreateDefault());

    IReadOnlyList<Entity> loadedRoots = loadService.Load(packagedScene);
    Assert.NotEmpty(loadedRoots);
}
```

- [ ] **Step 5: Run the focused packaging and runtime tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenSceneContainsSourceFontReference_WritesCookedHefontAndRewritesPayload|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenPackagedSceneUsesSourceFontReference_ResolvesCookedFontAsset|FullyQualifiedName~EditorPlatformAssetCookServiceTests" -v minimal`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "feat: cook source fonts into runtime hefont assets"
```

### Task 4: Migrate The Demo Menu To Source Fonts

**Files:**
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscMenuTheme.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Create: `C:/dev/helprojs/city/assets/Fonts/DemoDiscTitle.ttf`
- Create: `C:/dev/helprojs/city/assets/Fonts/DemoDiscBody.ttf`
- Delete: `C:/dev/helprojs/city/assets/Fonts/DemoDiscTitle.hefont`
- Delete: `C:/dev/helprojs/city/assets/Fonts/DemoDiscBody.hefont`

- [ ] **Step 1: Add a hard preflight test for source demo fonts**

```csharp
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_UsesSourceFontPaths() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset;
    string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
    using (FileStream stream = File.OpenRead(scenePath)) {
        sceneAsset = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
    }

    Assert.Contains(sceneAsset.AssetReferences, reference => string.Equals(reference.RelativePath, "Fonts/DemoDiscTitle.ttf", StringComparison.Ordinal));
    Assert.Contains(sceneAsset.AssetReferences, reference => string.Equals(reference.RelativePath, "Fonts/DemoDiscBody.ttf", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_UsesSourceFontPaths" -v minimal`

Expected: FAIL because the theme and generated scene still point at `.hefont`.

- [ ] **Step 3: Stage the real source fonts and update the theme**

```csharp
public sealed class DemoDiscMenuTheme {
    public string TitleFontPath => "Fonts/DemoDiscTitle.ttf";
    public string BodyFontPath => "Fonts/DemoDiscBody.ttf";
}
```

```powershell
if (!(Test-Path C:\dev\helprojs\city\assets\Fonts\DemoDiscTitle.ttf)) {
    throw "Missing required source font asset: C:\\dev\\helprojs\\city\\assets\\Fonts\\DemoDiscTitle.ttf"
}
if (!(Test-Path C:\dev\helprojs\city\assets\Fonts\DemoDiscBody.ttf)) {
    throw "Missing required source font asset: C:\\dev\\helprojs\\city\\assets\\Fonts\\DemoDiscBody.ttf"
}
```

- [ ] **Step 4: Regenerate the demo scene and remove authored cooked blobs**

Run: `rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --editor-command menu.regenerate-demo-disc-main-menu`

Expected: `Editor command 'menu.regenerate-demo-disc-main-menu' executed successfully.`

Run:

```powershell
Remove-Item C:\dev\helprojs\city\assets\Fonts\DemoDiscTitle.hefont -Force
Remove-Item C:\dev\helprojs\city\assets\Fonts\DemoDiscBody.hefont -Force
```

- [ ] **Step 5: Run demo-scene and packaging verification**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal`

Expected: PASS

Run: `rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\tmp\helengine-source-font-build-verify`

Expected: `Build completed for platform 'windows': C:\tmp\helengine-source-font-build-verify`

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs C:/dev/helprojs/city/assets/codebase/menu/DemoDiscMenuTheme.cs C:/dev/helprojs/city/assets/Fonts/DemoDiscTitle.ttf C:/dev/helprojs/city/assets/Fonts/DemoDiscBody.ttf C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenu.helen
git rm C:/dev/helprojs/city/assets/Fonts/DemoDiscTitle.hefont C:/dev/helprojs/city/assets/Fonts/DemoDiscBody.hefont
git commit -m "feat: migrate demo menu to source font assets"
```

### Task 5: Final Verification Sweep

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/AssetImportManagerTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Run the focused editor and packaging suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~EditorSceneAssetReferenceResolverTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal
```

Expected: PASS

- [ ] **Step 2: Run the real city headless regeneration and Windows build**

Run:

```bash
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --editor-command menu.regenerate-demo-disc-main-menu
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\tmp\helengine-source-font-final-verify
```

Expected:

```text
Editor command 'menu.regenerate-demo-disc-main-menu' executed successfully.
Build completed for platform 'windows': C:\tmp\helengine-source-font-final-verify
```

- [ ] **Step 3: Spot-check the final packaged output**

Run:

```powershell
Test-Path C:\tmp\helengine-source-font-final-verify\cooked\Fonts\DemoDiscTitle.hefont
Test-Path C:\tmp\helengine-source-font-final-verify\cooked\Fonts\DemoDiscBody.hefont
```

Expected:

```text
True
True
```

- [ ] **Step 4: Commit**

```bash
git status --short
```
