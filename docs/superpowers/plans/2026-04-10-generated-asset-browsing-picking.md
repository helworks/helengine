# Generated Asset Browsing And Picking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the editor asset browser and picker surface engine-generated virtual assets alongside project files, and let model property picks resolve generated runtime models from cache instead of disk.

**Architecture:** Introduce source-aware `AssetBrowserEntry` metadata plus a generated-asset provider registry, then route browser navigation through a dedicated data source that merges filesystem and virtual entries. Keep generated assets read-only in the browser, bypass import-settings flows for them, and resolve generated models through a cache-backed built-in engine provider.

**Tech Stack:** C#/.NET 9, existing Hel engine editor UI, `ContentManager`, `RenderManager3D`, `ModelUtils`, xUnit

---

## File Structure

### New Files

- `engine/helengine.editor/components/ui/asset/AssetBrowserEntrySourceKind.cs`
  Defines whether a browser entry comes from the filesystem or a generated provider.
- `engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs`
  Owns current browser path, merges filesystem and generated entries, and reports whether the current directory is writable.
- `engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs`
  Defines the generated-asset provider contract for browseable entries and runtime-model resolution.
- `engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs`
  Stores registered providers, exposes merged generated entry lookup, and resolves generated runtime models.
- `engine/helengine.editor/managers/asset/EngineGeneratedModelCache.cs`
  Builds and caches built-in runtime models such as cube and plane by stable generated asset id.
- `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`
  Publishes `Engine/Models/*` virtual entries and resolves built-in model assets from the cache.
- `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`
  Verifies provider registration, generated entry loading, and runtime-model resolution.
- `engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs`
  Verifies merged root entries, generated-folder navigation, and read-only virtual paths.
- `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`
  Verifies the built-in engine provider publishes the expected entries and reuses cached runtime models.
- `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
  Verifies model property picks resolve generated runtime models and keep the picked display label.
- `engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs`
  Verifies generated asset selection bypasses import settings and shows a read-only summary.
- `engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs`
  Supplies deterministic generated entries and runtime models for registry and browser tests.

### Modified Files

- `engine/helengine.editor/components/ui/asset/AssetBrowserEntry.cs`
  Add source-aware metadata and static factory helpers for filesystem and generated entries.
- `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
  Emit file-backed entries through the new entry factories and stop being responsible for virtual entry classification.
- `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
  Swap direct `EditorAssetManager` usage for `AssetBrowserDataSource`, classify rows from entry metadata, and expose whether the current directory is writable.
- `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
  Suppress file/folder creation menus inside generated virtual directories.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  Resolve generated model picks through `GeneratedAssetProviderRegistry` before falling back to disk loading.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  Add a generated-asset summary view path for read-only virtual assets.
- `engine/helengine.editor/EditorSession.cs`
  Register the built-in engine provider and branch generated-asset selection away from import settings and preview.

## Task 1: Add Source-Aware Asset Entries

**Files:**
- Create: `engine/helengine.editor/components/ui/asset/AssetBrowserEntrySourceKind.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserEntry.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Test: `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies generated-asset provider registration and source-aware asset entries.
    /// </summary>
    public class GeneratedAssetProviderRegistryTests {
        /// <summary>
        /// Ensures generated entries keep provider metadata and resolve models from the registry.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenProviderIsRegistered_ReturnsGeneratedEntryMetadata() {
            GeneratedAssetProviderRegistry.ResetForTests();
            TestGeneratedAssetProvider provider = new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine"),
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube")
                },
                new TestRuntimeModel {
                    Id = "engine:model:cube"
                });
            GeneratedAssetProviderRegistry.Register(provider);

            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            GeneratedAssetProviderRegistry.LoadEntries(string.Empty, entries);

            AssetBrowserEntry cubeEntry = Assert.Single(entries, entry => entry.AssetId == "engine:model:cube");
            Assert.Equal(AssetBrowserEntrySourceKind.Generated, cubeEntry.SourceKind);
            Assert.Equal("engine", cubeEntry.ProviderId);
            Assert.Equal(AssetEntryKind.Model, cubeEntry.EntryKind);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter GeneratedAssetProviderRegistryTests`

Expected: build failure because `GeneratedAssetProviderRegistry`, `AssetBrowserEntrySourceKind`, and the generated entry factory helpers do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Identifies where an asset-browser entry originates.
    /// </summary>
    public enum AssetBrowserEntrySourceKind {
        /// <summary>
        /// Entry is backed by a real filesystem path under the project assets root.
        /// </summary>
        FileSystem,
        /// <summary>
        /// Entry is supplied by a generated-asset provider.
        /// </summary>
        Generated
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Represents one file, folder, or generated asset displayed in the asset browser.
    /// </summary>
    public sealed class AssetBrowserEntry {
        /// <summary>
        /// Initializes a new asset browser entry with source-aware metadata.
        /// </summary>
        public AssetBrowserEntry(
            string name,
            string relativePath,
            string fullPath,
            bool isDirectory,
            string extension,
            AssetBrowserEntrySourceKind sourceKind,
            AssetEntryKind entryKind,
            string providerId,
            string assetId) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            RelativePath = relativePath ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
            IsDirectory = isDirectory;
            Extension = extension ?? string.Empty;
            SourceKind = sourceKind;
            EntryKind = entryKind;
            ProviderId = providerId ?? string.Empty;
            AssetId = assetId ?? string.Empty;
        }

        /// <summary>Creates one filesystem directory entry.</summary>
        public static AssetBrowserEntry CreateFileSystemDirectory(string name, string relativePath, string fullPath) {
            return new AssetBrowserEntry(name, relativePath, fullPath, true, string.Empty, AssetBrowserEntrySourceKind.FileSystem, AssetEntryKind.Directory, string.Empty, string.Empty);
        }

        /// <summary>Creates one filesystem file entry.</summary>
        public static AssetBrowserEntry CreateFileSystemFile(string name, string relativePath, string fullPath, string extension, AssetEntryKind entryKind) {
            return new AssetBrowserEntry(name, relativePath, fullPath, false, extension, AssetBrowserEntrySourceKind.FileSystem, entryKind, string.Empty, string.Empty);
        }

        /// <summary>Creates one generated directory entry.</summary>
        public static AssetBrowserEntry CreateGeneratedDirectory(string name, string relativePath, string providerId) {
            return new AssetBrowserEntry(name, relativePath, string.Empty, true, string.Empty, AssetBrowserEntrySourceKind.Generated, AssetEntryKind.Directory, providerId, string.Empty);
        }

        /// <summary>Creates one generated asset entry.</summary>
        public static AssetBrowserEntry CreateGeneratedAsset(string name, string relativePath, AssetEntryKind entryKind, string providerId, string assetId) {
            return new AssetBrowserEntry(name, relativePath, string.Empty, false, string.Empty, AssetBrowserEntrySourceKind.Generated, entryKind, providerId, assetId);
        }

        public string Name { get; }
        public string RelativePath { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
        public string Extension { get; }
        public AssetBrowserEntrySourceKind SourceKind { get; }
        public AssetEntryKind EntryKind { get; }
        public string ProviderId { get; }
        public string AssetId { get; }
        public bool IsGenerated => SourceKind == AssetBrowserEntrySourceKind.Generated;
    }
}
```

```csharp
public void LoadEntries(List<AssetBrowserEntry> entries) {
    // ...
    entries.Add(AssetBrowserEntry.CreateFileSystemDirectory(name, relativePath, dirPath));
    // ...
    AssetEntryKind entryKind = ClassifyEntryKind(extension);
    entries.Add(AssetBrowserEntry.CreateFileSystemFile(name, relativePath, filePath, extension, entryKind));
}

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

    if (audioExtensions.Contains(extension)) {
        return AssetEntryKind.Audio;
    }

    if (scriptExtensions.Contains(extension)) {
        return AssetEntryKind.Script;
    }

    if (configExtensions.Contains(extension)) {
        return AssetEntryKind.Config;
    }

    return AssetEntryKind.File;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter GeneratedAssetProviderRegistryTests`

Expected: build still fails, but now only on the missing provider contract and registry classes introduced by the test.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/asset/AssetBrowserEntry.cs engine/helengine.editor/components/ui/asset/AssetBrowserEntrySourceKind.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs
git commit -m "feat: add source-aware asset browser entries"
```

## Task 2: Add Generated Asset Provider Contracts And Registry

**Files:**
- Create: `engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs`
- Create: `engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs`
- Create: `engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`

- [ ] **Step 1: Expand the failing test to cover runtime-model resolution**

```csharp
[Fact]
public void ResolveRuntimeModel_WhenGeneratedModelEntryIsPicked_UsesTheOwningProvider() {
    GeneratedAssetProviderRegistry.ResetForTests();
    TestRuntimeModel runtimeModel = new TestRuntimeModel {
        Id = "engine:model:cube"
    };
    TestGeneratedAssetProvider provider = new TestGeneratedAssetProvider(
        "engine",
        new[] {
            AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube")
        },
        runtimeModel);
    GeneratedAssetProviderRegistry.Register(provider);

    RuntimeModel resolvedModel = GeneratedAssetProviderRegistry.ResolveRuntimeModel(
        AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube"));

    Assert.Same(runtimeModel, resolvedModel);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter GeneratedAssetProviderRegistryTests`

Expected: build failure because `IGeneratedAssetProvider`, `TestGeneratedAssetProvider`, and `ResolveRuntimeModel(...)` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Supplies browseable generated assets and resolves runtime models for generated model entries.
    /// </summary>
    public interface IGeneratedAssetProvider {
        /// <summary>Gets the stable provider identifier.</summary>
        string ProviderId { get; }

        /// <summary>Appends entries that live directly under the supplied virtual relative path.</summary>
        void LoadEntries(string relativePath, List<AssetBrowserEntry> entries);

        /// <summary>Resolves a generated model entry to a runtime model when the provider owns the entry.</summary>
        bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel runtimeModel);
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores generated-asset providers and routes generated entry resolution to the owning provider.
    /// </summary>
    public static class GeneratedAssetProviderRegistry {
        static readonly Dictionary<string, IGeneratedAssetProvider> Providers = new Dictionary<string, IGeneratedAssetProvider>(StringComparer.Ordinal);

        public static void ResetForTests() {
            Providers.Clear();
        }

        public static void Register(IGeneratedAssetProvider provider) {
            if (provider == null) {
                throw new ArgumentNullException(nameof(provider));
            }

            Providers[provider.ProviderId] = provider;
        }

        public static void LoadEntries(string relativePath, List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            foreach (IGeneratedAssetProvider provider in Providers.Values) {
                provider.LoadEntries(relativePath ?? string.Empty, entries);
            }
        }

        public static RuntimeModel ResolveRuntimeModel(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!Providers.TryGetValue(entry.ProviderId, out IGeneratedAssetProvider provider)) {
                throw new InvalidOperationException($"Generated asset provider '{entry.ProviderId}' is not registered.");
            }

            if (!provider.TryResolveRuntimeModel(entry, out RuntimeModel runtimeModel) || runtimeModel == null) {
                throw new InvalidOperationException($"Generated runtime model '{entry.AssetId}' could not be resolved.");
            }

            return runtimeModel;
        }
    }
}
```

```csharp
namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides deterministic generated entries and one runtime model for tests.
    /// </summary>
    public class TestGeneratedAssetProvider : IGeneratedAssetProvider {
        readonly IReadOnlyList<AssetBrowserEntry> entries;
        readonly RuntimeModel runtimeModel;

        public TestGeneratedAssetProvider(string providerId, IReadOnlyList<AssetBrowserEntry> entries, RuntimeModel runtimeModel) {
            ProviderId = providerId;
            this.entries = entries;
            this.runtimeModel = runtimeModel;
        }

        public string ProviderId { get; }

        public void LoadEntries(string relativePath, List<AssetBrowserEntry> targetEntries) {
            for (int i = 0; i < entries.Count; i++) {
                AssetBrowserEntry entry = entries[i];
                string parentPath = string.Empty;
                int slashIndex = entry.RelativePath.LastIndexOf('/');
                if (slashIndex >= 0) {
                    parentPath = entry.RelativePath.Substring(0, slashIndex);
                }

                if (string.Equals(parentPath, relativePath ?? string.Empty, StringComparison.Ordinal)) {
                    targetEntries.Add(entry);
                }
            }
        }

        public bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel resolvedModel) {
            resolvedModel = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
                return false;
            }

            resolvedModel = runtimeModel;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter GeneratedAssetProviderRegistryTests`

Expected: PASS with `2` tests.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs
git commit -m "feat: add generated asset provider registry"
```

## Task 3: Route Asset Browser Navigation Through A Unified Data Source

**Files:**
- Create: `engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- Test: `engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies merged filesystem and generated-asset browsing behavior.
    /// </summary>
    public class AssetBrowserDataSourceTests {
        [Fact]
        public void LoadEntries_AtRoot_IncludesGeneratedTopLevelDirectoriesAlongsideFilesystemEntries() {
            GeneratedAssetProviderRegistry.ResetForTests();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine")
                },
                new TestRuntimeModel()));

            string projectPath = Path.Combine(Path.GetTempPath(), "helengine-generated-assets-root");
            Directory.CreateDirectory(Path.Combine(projectPath, "assets"));
            File.WriteAllText(Path.Combine(projectPath, "assets", "texture.png"), "x");

            AssetBrowserDataSource dataSource = new AssetBrowserDataSource(projectPath);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            dataSource.LoadEntries(entries);

            Assert.Contains(entries, entry => entry.Name == "Engine" && entry.IsGenerated);
            Assert.Contains(entries, entry => entry.Name == "texture.png" && entry.SourceKind == AssetBrowserEntrySourceKind.FileSystem);
        }

        [Fact]
        public void TryNavigateTo_WhenEnteringGeneratedDirectory_MarksDirectoryAsReadOnly() {
            GeneratedAssetProviderRegistry.ResetForTests();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine"),
                    AssetBrowserEntry.CreateGeneratedDirectory("Models", "Engine/Models", "engine")
                },
                new TestRuntimeModel()));

            AssetBrowserDataSource dataSource = new AssetBrowserDataSource(Path.GetTempPath());

            Assert.True(dataSource.TryNavigateTo("Engine"));
            Assert.False(dataSource.CanCreateFileSystemEntries);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter AssetBrowserDataSourceTests`

Expected: build failure because `AssetBrowserDataSource` does not exist and `AssetBrowserView` / `AssetBrowserPanel` still use `EditorAssetManager` directly.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Provides the current asset-browser directory view across filesystem and generated sources.
    /// </summary>
public class AssetBrowserDataSource {
    readonly EditorAssetManager fileSystemAssets;
    readonly Dictionary<string, AssetBrowserEntrySourceKind> directorySources;
    string currentRelativePath;
    bool currentDirectoryIsGenerated;

    public AssetBrowserDataSource(string projectPath) {
        fileSystemAssets = new EditorAssetManager(projectPath);
        directorySources = new Dictionary<string, AssetBrowserEntrySourceKind>(StringComparer.Ordinal);
        currentRelativePath = string.Empty;
        currentDirectoryIsGenerated = false;
    }

    public string CurrentRelativePath => currentRelativePath;
    public string CurrentDirectoryPath => CanCreateFileSystemEntries ? fileSystemAssets.CurrentFullPath : string.Empty;
    public bool CanCreateFileSystemEntries => !currentDirectoryIsGenerated;

        public string GetDisplayPath() {
            return string.IsNullOrWhiteSpace(currentRelativePath) ? "assets" : currentRelativePath;
        }

        public void LoadEntries(List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            entries.Clear();
        if (currentDirectoryIsGenerated) {
            GeneratedAssetProviderRegistry.LoadEntries(currentRelativePath, entries);
        } else {
                fileSystemAssets.TryNavigateTo(currentRelativePath);
                fileSystemAssets.LoadEntries(entries);
                if (string.IsNullOrWhiteSpace(currentRelativePath)) {
                    GeneratedAssetProviderRegistry.LoadEntries(string.Empty, entries);
                }
            }

            entries.Sort(CompareEntries);
        }

        public bool TryNavigateTo(string relativePath) {
            string normalized = NormalizeRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalized)) {
                currentRelativePath = string.Empty;
                return true;
            }

            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            LoadEntries(entries);
            for (int i = 0; i < entries.Count; i++) {
                AssetBrowserEntry entry = entries[i];
                if (entry.IsDirectory && string.Equals(entry.RelativePath, normalized, StringComparison.Ordinal)) {
                    currentRelativePath = normalized;
                    currentDirectoryIsGenerated = entry.IsGenerated;
                    directorySources[normalized] = entry.SourceKind;
                    return true;
                }
            }

            return false;
        }

        public bool TryNavigateUp() {
            if (string.IsNullOrWhiteSpace(currentRelativePath)) {
                return false;
            }

            string parentPath = GetParentPath(currentRelativePath);
            currentRelativePath = parentPath;
            if (string.IsNullOrWhiteSpace(parentPath)) {
                currentDirectoryIsGenerated = false;
                return true;
            }

            if (!directorySources.TryGetValue(parentPath, out AssetBrowserEntrySourceKind sourceKind)) {
                throw new InvalidOperationException($"Directory source metadata is missing for '{parentPath}'.");
            }

            currentDirectoryIsGenerated = sourceKind == AssetBrowserEntrySourceKind.Generated;
            return true;
        }

        static string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            string normalized = relativePath.Replace('\\', '/').Trim('/');
            return normalized;
        }

        static string GetParentPath(string relativePath) {
            int slashIndex = relativePath.LastIndexOf('/');
            return slashIndex < 0 ? string.Empty : relativePath.Substring(0, slashIndex);
        }

        static int CompareEntries(AssetBrowserEntry left, AssetBrowserEntry right) {
            if (left == null && right == null) {
                return 0;
            }
            if (left == null) {
                return 1;
            }
            if (right == null) {
                return -1;
            }

            if (left.IsDirectory != right.IsDirectory) {
                return left.IsDirectory ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

```csharp
// AssetBrowserView constructor and fields
readonly AssetBrowserDataSource DataSource;
public string CurrentDirectoryPath => DataSource.CurrentDirectoryPath;
public bool CanCreateFileSystemEntries => DataSource.CanCreateFileSystemEntries;

public AssetBrowserView(...) {
    DataSource = new AssetBrowserDataSource(projectPath);
    // existing entity setup...
}

public void RefreshEntries() {
    DataSource.LoadEntries(Entries);
    ApplyExtensionFilter();
    PathText.Text = DataSource.GetDisplayPath();
    LayoutToolbar();
    LayoutRows();
}

void NavigateTo(string relativePath) {
    if (DataSource.TryNavigateTo(relativePath)) {
        RefreshEntries();
    }
}

void NavigateUp() {
    if (DataSource.TryNavigateUp()) {
        RefreshEntries();
    }
}

void GetIconForEntry(AssetBrowserEntry entry, out byte4 color, out string label, out byte4 textColor) {
    switch (entry.EntryKind) {
        case AssetEntryKind.Directory:
            color = ThemeManager.Colors.AccentSecondary;
            label = "DIR";
            textColor = ThemeManager.Colors.TextOnAccent;
            return;
        case AssetEntryKind.Model:
            color = ThemeManager.Colors.StateWarning;
            label = "3D";
            textColor = ThemeManager.Colors.TextOnAccent;
            return;
        // keep existing cases for Image/Audio/Script/Config/Unknown/File
    }
}
```

```csharp
// AssetBrowserPanel.UpdateContextMenuInput
if (!BrowserView.CanCreateFileSystemEntries) {
    AssetContextMenu.Hide();
    FileTemplateMenu.Hide();
    return;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter AssetBrowserDataSourceTests`

Expected: PASS with `2` tests.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs
git commit -m "feat: merge generated entries into asset browser navigation"
```

## Task 4: Add The Built-In Engine Generated Model Provider

**Files:**
- Create: `engine/helengine.editor/managers/asset/EngineGeneratedModelCache.cs`
- Create: `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies built-in engine-generated assets exposed to the browser.
    /// </summary>
    public class EngineGeneratedAssetProviderTests {
        [Fact]
        public void LoadEntries_AtRoot_AddsEngineDirectoryAndModelFolder() {
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

            provider.LoadEntries(string.Empty, entries);

            Assert.Contains(entries, entry => entry.Name == "Engine" && entry.IsDirectory);
        }

        [Fact]
        public void TryResolveRuntimeModel_WhenCalledTwice_ReusesTheCachedRuntimeModel() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), null, null);

            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube");

            Assert.True(provider.TryResolveRuntimeModel(cubeEntry, out RuntimeModel firstModel));
            Assert.True(provider.TryResolveRuntimeModel(cubeEntry, out RuntimeModel secondModel));
            Assert.Same(firstModel, secondModel);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EngineGeneratedAssetProviderTests`

Expected: build failure because `EngineGeneratedAssetProvider` and `EngineGeneratedModelCache` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Builds and caches runtime models for built-in generated engine assets.
    /// </summary>
    public static class EngineGeneratedModelCache {
        static readonly Dictionary<string, RuntimeModel> CachedModels = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);

        public static void ResetForTests() {
            CachedModels.Clear();
        }

        public static RuntimeModel GetRuntimeModel(string assetId) {
            if (CachedModels.TryGetValue(assetId, out RuntimeModel cachedModel)) {
                return cachedModel;
            }

            ModelAsset modelAsset;
            if (string.Equals(assetId, "engine:model:cube", StringComparison.Ordinal)) {
                modelAsset = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            } else if (string.Equals(assetId, "engine:model:plane", StringComparison.Ordinal)) {
                modelAsset = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            } else {
                throw new InvalidOperationException($"Unknown generated engine model '{assetId}'.");
            }

            RuntimeModel runtimeModel = Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            runtimeModel.Id = assetId;
            CachedModels[assetId] = runtimeModel;
            return runtimeModel;
        }
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Publishes built-in generated engine assets such as primitive models.
    /// </summary>
    public class EngineGeneratedAssetProvider : IGeneratedAssetProvider {
        public string ProviderId => "engine";

        public void LoadEntries(string relativePath, List<AssetBrowserEntry> entries) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", ProviderId));
                return;
            }

            if (string.Equals(relativePath, "Engine", StringComparison.Ordinal)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Models", "Engine/Models", ProviderId));
                return;
            }

            if (string.Equals(relativePath, "Engine/Models", StringComparison.Ordinal)) {
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, ProviderId, "engine:model:cube"));
                entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Plane", "Engine/Models/Plane", AssetEntryKind.Model, ProviderId, "engine:model:plane"));
            }
        }

        public bool TryResolveRuntimeModel(AssetBrowserEntry entry, out RuntimeModel runtimeModel) {
            runtimeModel = null;
            if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal) || entry.EntryKind != AssetEntryKind.Model) {
                return false;
            }

            runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(entry.AssetId);
            return true;
        }
    }
}
```

```csharp
// EditorSession constructor, after asset import manager initialization
GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EngineGeneratedAssetProviderTests`

Expected: PASS with `2` tests.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/asset/EngineGeneratedModelCache.cs engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs
git commit -m "feat: add built-in generated engine model provider"
```

## Task 5: Resolve Generated Models In The Model Picker

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated model picks in component property rows.
    /// </summary>
    public class ComponentPropertiesViewGeneratedAssetTests : IDisposable {
        readonly string tempRootPath;

        public ComponentPropertiesViewGeneratedAssetTests() {
            tempRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-model-picker-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = tempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        public void Dispose() {
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(tempRootPath)) {
                Directory.Delete(tempRootPath, true);
            }
        }

        [Fact]
        public void HandleModelPicked_WhenEntryIsGenerated_AssignsTheProviderRuntimeModelAndDisplayLabel() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel {
                Id = "engine:model:cube"
            };
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube")
                },
                runtimeModel));

            MeshComponent meshComponent = new MeshComponent();
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(tempRootPath));
            view.ShowComponents(CreateEntityWithComponent(meshComponent));

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleModelPicked.Invoke(view, new object[] {
                modelRow,
                AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube")
            });

            Assert.Same(runtimeModel, meshComponent.Model);
            Assert.Equal("Cube", modelRow.ValueText.Text);
        }

        EditorEntity CreateEntityWithComponent(Component component) {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(component);
            return entity;
        }

        ComponentPropertyRow FindModelRow(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
            return Assert.Single(rows, row => row.Kind == ComponentPropertyRowKind.Model);
        }

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
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter ComponentPropertiesViewGeneratedAssetTests`

Expected: FAIL because `LoadModel(...)` still tries to read `entry.FullPath` for generated entries and throws a model path error.

- [ ] **Step 3: Write minimal implementation**

```csharp
RuntimeModel LoadModel(AssetBrowserEntry entry) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    if (entry.IsGenerated) {
        return GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
    }

    ModelAsset modelAsset = LoadModelAsset(entry.FullPath);
    return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
}
```

```csharp
void HandleModelPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
    if (row.TargetComponent == null || row.Property == null) {
        return;
    }

    try {
        RuntimeModel model = LoadModel(entry);
        row.Property.SetValue(row.TargetComponent, model);
        ModelLabels[model] = entry.Name ?? string.Empty;
        UpdateModelRow(row);
    } catch (Exception ex) {
        Logger.WriteError($"Model pick failed: {ex.Message}");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter ComponentPropertiesViewGeneratedAssetTests`

Expected: PASS with `1` test.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs
git commit -m "feat: resolve generated models in component picker"
```

## Task 6: Bypass Import Settings For Generated Asset Selection

**Files:**
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated assets selected from the asset browser bypass import workflows.
    /// </summary>
    public class EditorSessionGeneratedAssetTests : IDisposable {
        readonly string tempRootPath;

        public EditorSessionGeneratedAssetTests() {
            tempRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-session-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = tempRootPath
            });
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        public void Dispose() {
            if (Directory.Exists(tempRootPath)) {
                Directory.Delete(tempRootPath, true);
            }
        }

        [Fact]
        public void HandleAssetSelected_WhenEntryIsGenerated_ShowsReadOnlySummaryInsteadOfImportSettings() {
            EditorSession session = CreateSessionForGeneratedSelection();
            AssetBrowserEntry generatedEntry = AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube");

            InvokePrivate(session, "HandleAssetSelected", generatedEntry);

            PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
            TextComponent header = GetPrivateField<TextComponent>(panel, "headerText");
            TextComponent status = GetPrivateField<TextComponent>(panel, "statusText");

            Assert.Equal("Properties", header.Text);
            Assert.Equal("Source: Generated", status.Text);
        }

        EditorSession CreateSessionForGeneratedSelection() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            PropertiesPanel propertiesPanel = new PropertiesPanel(CreateFont(), new ContentManager(tempRootPath));
            PreviewPanel previewPanel = new PreviewPanel(CreateFont());

            SetPrivateField(session, "propertiesPanel", propertiesPanel);
            SetPrivateField(session, "previewPanel", previewPanel);

            return session;
        }

        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionGeneratedAssetTests`

Expected: FAIL because `HandleAssetSelected(...)` still enters the import-settings path and requires `entry.FullPath`.

- [ ] **Step 3: Write minimal implementation**

```csharp
public void ShowGeneratedAssetSummary(AssetBrowserEntry entry) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    currentEntry = null;
    importSettingsView.Hide();
    MaterialView.Hide();
    SetTransformVisible(false);
    ComponentView.Hide();
    ApplyLines(new[] {
        "Properties",
        $"Asset: {BuildAssetLabel(entry)}",
        $"Provider: {entry.ProviderId}",
        $"Asset Id: {entry.AssetId}",
        $"Kind: {entry.EntryKind}",
        "Source: Generated"
    });
    LayoutLines();
}
```

```csharp
void HandleAssetSelected(AssetBrowserEntry entry) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    if (entry.IsDirectory) {
        return;
    }

    if (entry.IsGenerated) {
        propertiesPanel.ShowGeneratedAssetSummary(entry);
        previewPanel.ClearPreview();
        return;
    }

    // existing material/import-settings flow stays unchanged
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionGeneratedAssetTests`

Expected: PASS with `1` test.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs
git commit -m "feat: show generated asset summaries in properties panel"
```

## Task 7: Full Verification

**Files:**
- Test: `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`
- Test: `engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs`
- Test: `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs`

- [ ] **Step 1: Run the focused generated-asset test set**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "GeneratedAssetProviderRegistryTests|AssetBrowserDataSourceTests|EngineGeneratedAssetProviderTests|ComponentPropertiesViewGeneratedAssetTests|EditorSessionGeneratedAssetTests"`

Expected: PASS with the new generated-asset tests all green.

- [ ] **Step 2: Run the full editor test suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS with the full editor suite green and no new regressions introduced by generated-asset browsing.

- [ ] **Step 3: Commit the verification-complete implementation**

```bash
git add engine/helengine.editor/components/ui/asset/AssetBrowserEntry.cs engine/helengine.editor/components/ui/asset/AssetBrowserEntrySourceKind.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs engine/helengine.editor/managers/asset/EngineGeneratedModelCache.cs engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs
git commit -m "feat: add generated asset browsing and picking"
```

## Self-Review

### Spec Coverage

- Source-aware asset entries: covered by Task 1.
- Provider registry and stable id resolution: covered by Task 2.
- Built-in engine provider and cache-backed cube/plane entries: covered by Task 4.
- Unified browser and picker flow: covered by Task 3.
- Model picker resolving generated assets instead of disk: covered by Task 5.
- Generated assets bypassing import settings and preview: covered by Task 6.
- Test coverage for merged browsing, navigation, resolution, labels, and failure boundaries: covered by Tasks 1 through 7.

### Placeholder Scan

- No `TODO`, `TBD`, or `implement later` placeholders remain.
- Every task lists exact files and concrete commands.
- Every code-writing step includes concrete C# snippets rather than abstract instructions.

### Type Consistency

- `AssetBrowserEntrySourceKind`, `IGeneratedAssetProvider`, `GeneratedAssetProviderRegistry`, `AssetBrowserDataSource`, `EngineGeneratedModelCache`, and `EngineGeneratedAssetProvider` are named consistently across all tasks.
- `AssetBrowserEntry.CreateGeneratedDirectory(...)` and `AssetBrowserEntry.CreateGeneratedAsset(...)` are used consistently in tests and implementation.
- `ResolveRuntimeModel(...)`, `CanCreateFileSystemEntries`, and `ShowGeneratedAssetSummary(...)` are named consistently wherever they appear.
