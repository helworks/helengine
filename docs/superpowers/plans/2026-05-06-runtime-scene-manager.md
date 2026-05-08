# Runtime Scene Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a runtime `SceneManager` that loads only built scenes in single or additive mode, tracks loaded scene records, and emits explicit unload events so the player owns entity and asset teardown.

**Architecture:** The feature is split into four focused pieces: a managed runtime scene catalog model and parser in `helengine.core`, an editor-side managed manifest writer that emits the catalog into packaged outputs, a runtime `SceneManager` plus core bootstrap wiring, and a consumer migration for `MenuComponent` so existing runtime scene actions route through the new manager instead of loading scenes ad hoc. The design keeps entity destruction outside the engine and makes the unload contract synchronous.

**Tech Stack:** C# 13 / .NET 9, xUnit, existing `ContentManager`, `RuntimeSceneLoadService`, editor build graph writers, JSON manifest readers built on `RuntimeManifestJsonReader`

---

## File Map

- Create: `engine/helengine.core/content/RuntimeSceneCatalogEntry.cs`
  Runtime DTO for one built scene entry.
- Create: `engine/helengine.core/content/RuntimeSceneCatalog.cs`
  Runtime catalog model, validation, lookup, and file reader.
- Modify: `engine/helengine.core/content/RuntimeManifestJsonReader.cs`
  Add parser support for the runtime scene catalog JSON shape.
- Create: `engine/helengine.editor.tests/RuntimeSceneCatalogTests.cs`
  Test catalog parsing and validation behavior.

- Create: `engine/helengine.editor/managers/project/EditorRuntimeManagedManifestWriter.cs`
  Writes `runtime-startup.json` and `runtime-scene-catalog.json` into the packaged runtime root.
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
  Invoke the new managed manifest writer during build graph packaging.
- Create: `engine/helengine.editor.tests/managers/project/EditorRuntimeManagedManifestWriterTests.cs`
  Verify managed runtime metadata emission.

- Create: `engine/helengine.core/scene/runtime/SceneLoadMode.cs`
  Runtime load mode enum for single and additive behavior.
- Create: `engine/helengine.core/scene/runtime/LoadedSceneRecord.cs`
  Immutable tracked record for one currently loaded scene.
- Create: `engine/helengine.core/scene/runtime/SceneLoadingEventArgs.cs`
  Pre-load event payload.
- Create: `engine/helengine.core/scene/runtime/SceneLoadedEventArgs.cs`
  Post-load event payload.
- Create: `engine/helengine.core/scene/runtime/SceneUnloadingEventArgs.cs`
  Pre-unload event payload that carries the roots the player must destroy.
- Create: `engine/helengine.core/scene/runtime/SceneUnloadedEventArgs.cs`
  Post-unload event payload.
- Create: `engine/helengine.core/scene/runtime/SceneManager.cs`
  Runtime scene registry, validation, loading, unloading, and event dispatch.
- Modify: `engine/helengine.core/Core.cs`
  Expose `SceneManager` and initialize it from `runtime-scene-catalog.json` when present.
- Create: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`
  Test load/unload behavior, event ordering, duplicate rejection, and core bootstrap wiring.

- Modify: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
  Route runtime scene actions through `Core.Instance.SceneManager`.
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  Add regression coverage that menu scene actions call the runtime manager contract rather than bypassing it.

## Task 1: Add the runtime scene catalog model and parser

**Files:**
- Create: `engine/helengine.core/content/RuntimeSceneCatalogEntry.cs`
- Create: `engine/helengine.core/content/RuntimeSceneCatalog.cs`
- Modify: `engine/helengine.core/content/RuntimeManifestJsonReader.cs`
- Test: `engine/helengine.editor.tests/RuntimeSceneCatalogTests.cs`

- [ ] **Step 1: Write the failing catalog tests**

```csharp
using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime scene catalog reader matches the editor-written JSON shape.
/// </summary>
public sealed class RuntimeSceneCatalogTests : IDisposable {
    /// <summary>
    /// Temporary root used by the catalog tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes the temporary test root.
    /// </summary>
    public RuntimeSceneCatalogTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-scene-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    /// <summary>
    /// Deletes the temporary test root.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the runtime scene catalog preserves scene ids and cooked relative paths.
    /// </summary>
    [Fact]
    public void ReadFromFile_parses_runtime_scene_catalog_shape() {
        string manifestPath = Path.Combine(TempRootPath, "runtime-scene-catalog.json");
        File.WriteAllText(
            manifestPath,
            """
            {
              "Entries": [
                {
                  "SceneId": "Scenes/Bootstrap.helen",
                  "CookedRelativePath": "cooked/scenes/main.hasset"
                },
                {
                  "SceneId": "Scenes/TestPlayableScene.helen",
                  "CookedRelativePath": "scenes/Scenes/TestPlayableScene.hasset"
                }
              ]
            }
            """);

        RuntimeSceneCatalog catalog = RuntimeSceneCatalog.ReadFromFile(manifestPath);

        Assert.Equal(2, catalog.Entries.Length);
        Assert.Equal("Scenes/Bootstrap.helen", catalog.Entries[0].SceneId);
        Assert.Equal("cooked/scenes/main.hasset", catalog.Entries[0].CookedRelativePath);
        Assert.True(catalog.TryGetEntry("Scenes/TestPlayableScene.helen", out RuntimeSceneCatalogEntry entry));
        Assert.Equal("scenes/Scenes/TestPlayableScene.hasset", entry.CookedRelativePath);
    }

    /// <summary>
    /// Ensures duplicate scene ids are rejected during catalog construction.
    /// </summary>
    [Fact]
    public void Constructor_whenSceneIdsAreDuplicated_throws() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene-copy.hasset")
            ]));

        Assert.Contains("Scenes/TestPlayableScene.helen", exception.Message);
    }
}
```

- [ ] **Step 2: Run the catalog tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneCatalogTests"`

Expected: FAIL with errors that `RuntimeSceneCatalog` and `RuntimeSceneCatalogEntry` do not exist.

- [ ] **Step 3: Write the minimal runtime catalog implementation**

```csharp
namespace helengine {
    /// <summary>
    /// Describes one built runtime scene entry that can be loaded by scene id.
    /// </summary>
    public sealed class RuntimeSceneCatalogEntry {
        /// <summary>
        /// Initializes one runtime scene catalog entry.
        /// </summary>
        /// <param name="sceneId">Stable built scene id.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        public RuntimeSceneCatalogEntry(string sceneId, string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path is required.", nameof(cookedRelativePath));
            }

            SceneId = sceneId;
            CookedRelativePath = cookedRelativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Gets the stable built scene id.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Describes the runtime scene catalog emitted by the editor build graph.
    /// </summary>
    public sealed class RuntimeSceneCatalog {
        /// <summary>
        /// Initializes one runtime scene catalog.
        /// </summary>
        /// <param name="entries">Built runtime scene entries.</param>
        public RuntimeSceneCatalog(RuntimeSceneCatalogEntry[] entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }
            if (Array.Exists(entries, entry => entry == null)) {
                throw new ArgumentException("Runtime scene catalog entries cannot contain null entries.", nameof(entries));
            }

            Dictionary<string, RuntimeSceneCatalogEntry> entriesBySceneId = new Dictionary<string, RuntimeSceneCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entries.Length; index++) {
                RuntimeSceneCatalogEntry entry = entries[index];
                if (entriesBySceneId.ContainsKey(entry.SceneId)) {
                    throw new InvalidOperationException($"Runtime scene catalog contains duplicate scene id '{entry.SceneId}'.");
                }

                entriesBySceneId.Add(entry.SceneId, entry);
            }

            Entries = [.. entries];
            EntriesBySceneId = entriesBySceneId;
        }

        /// <summary>
        /// Gets the ordered runtime scene entries.
        /// </summary>
        public RuntimeSceneCatalogEntry[] Entries { get; }

        /// <summary>
        /// Gets runtime scene entries keyed by scene id.
        /// </summary>
        Dictionary<string, RuntimeSceneCatalogEntry> EntriesBySceneId { get; }

        /// <summary>
        /// Reads one runtime scene catalog from a JSON file.
        /// </summary>
        /// <param name="manifestPath">Path to the runtime-scene-catalog.json file.</param>
        /// <returns>The loaded runtime scene catalog.</returns>
        public static RuntimeSceneCatalog ReadFromFile(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Runtime scene catalog path is required.", nameof(manifestPath));
            }
            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException($"Runtime scene catalog '{manifestPath}' was not found.", manifestPath);
            }

            FileStream fileStream = File.OpenRead(manifestPath);
            StreamReader reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, false, 1024, true);
            string json = reader.ReadToEnd();
            reader.Dispose();
            fileStream.Dispose();
            return RuntimeManifestJsonReader.ReadRuntimeSceneCatalog(json);
        }

        /// <summary>
        /// Attempts to resolve one runtime scene entry by scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to locate.</param>
        /// <param name="entry">Resolved runtime scene entry when found.</param>
        /// <returns>True when the scene entry exists.</returns>
        public bool TryGetEntry(string sceneId, out RuntimeSceneCatalogEntry entry) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            return EntriesBySceneId.TryGetValue(sceneId, out entry);
        }
    }
}
```

```csharp
public static RuntimeSceneCatalog ReadRuntimeSceneCatalog(string json) {
    if (string.IsNullOrWhiteSpace(json)) {
        throw new ArgumentException("Runtime scene catalog JSON is required.", nameof(json));
    }

    string entriesJson = ReadRequiredArrayProperty(json, "Entries");
    List<RuntimeSceneCatalogEntry> entries = new List<RuntimeSceneCatalogEntry>();
    int elementStart = 0;
    int elementLength = 0;
    int cursor = 1;
    while (TryReadNextArrayElement(entriesJson, ref cursor, out elementStart, out elementLength)) {
        string entryJson = entriesJson.Substring(elementStart, elementLength);
        string sceneId = ReadRequiredStringProperty(entryJson, "SceneId");
        string cookedRelativePath = ReadRequiredStringProperty(entryJson, "CookedRelativePath");
        entries.Add(new RuntimeSceneCatalogEntry(sceneId, cookedRelativePath));
    }

    return new RuntimeSceneCatalog(entries.ToArray());
}
```

- [ ] **Step 4: Run the catalog tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneCatalogTests"`

Expected: PASS with 2 passing tests in `RuntimeSceneCatalogTests`.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core/content/RuntimeSceneCatalogEntry.cs engine/helengine.core/content/RuntimeSceneCatalog.cs engine/helengine.core/content/RuntimeManifestJsonReader.cs engine/helengine.editor.tests/RuntimeSceneCatalogTests.cs
rtk git commit -m "feat: add runtime scene catalog model"
```

## Task 2: Emit managed runtime scene metadata from the build graph

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorRuntimeManagedManifestWriter.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorRuntimeManagedManifestWriterTests.cs`

- [ ] **Step 1: Write the failing managed manifest writer test**

```csharp
using System.Collections.Generic;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the managed runtime manifest writer emits startup and scene catalog JSON files.
/// </summary>
public sealed class EditorRuntimeManagedManifestWriterTests : IDisposable {
    /// <summary>
    /// Temporary workspace used by the test.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes the temporary workspace.
    /// </summary>
    public EditorRuntimeManagedManifestWriterTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-managed-manifest-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Deletes the temporary workspace.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
    /// Ensures startup and runtime scene catalog files are written for managed players.
    /// </summary>
    [Fact]
    public void Write_emits_runtime_startup_and_scene_catalog_json_files() {
        PlatformBuildManifest manifest = new(
            2,
            "project",
            "1.0.0",
            "1.0.0",
            "Scenes/Bootstrap.helen",
            [
                new PlatformBuildScene(
                    "Scenes/Bootstrap.helen",
                    "Bootstrap",
                    "Scenes/Bootstrap.helen",
                    Array.Empty<PlatformBuildPayloadReference>(),
                    [
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/main.hasset")
                    ]),
                new PlatformBuildScene(
                    "Scenes/TestPlayableScene.helen",
                    "TestPlayableScene",
                    "Scenes/TestPlayableScene.helen",
                    Array.Empty<PlatformBuildPayloadReference>(),
                    [
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "scenes/Scenes/TestPlayableScene.hasset")
                    ])
            ],
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));

        EditorRuntimeManagedManifestWriter writer = new();
        writer.Write(RootPath, manifest, "windows-loose-files");

        string startupPath = Path.Combine(RootPath, "runtime-startup.json");
        string sceneCatalogPath = Path.Combine(RootPath, "runtime-scene-catalog.json");

        Assert.True(File.Exists(startupPath));
        Assert.True(File.Exists(sceneCatalogPath));

        string startupJson = File.ReadAllText(startupPath);
        string sceneCatalogJson = File.ReadAllText(sceneCatalogPath);

        Assert.Contains("\"StartupSceneId\": \"Scenes/Bootstrap.helen\"", startupJson);
        Assert.Contains("\"Value\": \"windows-loose-files\"", startupJson);
        Assert.Contains("\"SceneId\": \"Scenes/TestPlayableScene.helen\"", sceneCatalogJson);
        Assert.Contains("\"CookedRelativePath\": \"scenes/Scenes/TestPlayableScene.hasset\"", sceneCatalogJson);
    }
}
```

- [ ] **Step 2: Run the managed manifest writer test to verify it fails**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorRuntimeManagedManifestWriterTests"`

Expected: FAIL with errors that `EditorRuntimeManagedManifestWriter` does not exist.

- [ ] **Step 3: Write the minimal managed writer and build-graph integration**

```csharp
using System.Text;
using helengine.baseplatform.Manifest;

namespace helengine.editor {
    /// <summary>
    /// Writes managed runtime manifest files into the packaged build output root.
    /// </summary>
    public sealed class EditorRuntimeManagedManifestWriter {
        /// <summary>
        /// Writes managed runtime startup and scene catalog metadata.
        /// </summary>
        /// <param name="runtimeRootPath">Packaged runtime root that receives the JSON files.</param>
        /// <param name="cookedManifest">Cooked build manifest that owns the final scene layout.</param>
        /// <param name="selectedStorageProfileId">Stable runtime storage profile id.</param>
        public void Write(string runtimeRootPath, PlatformBuildManifest cookedManifest, string selectedStorageProfileId) {
            if (string.IsNullOrWhiteSpace(runtimeRootPath)) {
                throw new ArgumentException("Runtime root path must be provided.", nameof(runtimeRootPath));
            }
            if (cookedManifest == null) {
                throw new ArgumentNullException(nameof(cookedManifest));
            }
            if (string.IsNullOrWhiteSpace(selectedStorageProfileId)) {
                throw new ArgumentException("Selected storage profile id must be provided.", nameof(selectedStorageProfileId));
            }

            Directory.CreateDirectory(runtimeRootPath);
            File.WriteAllText(Path.Combine(runtimeRootPath, "runtime-startup.json"), BuildStartupManifestJson(cookedManifest, selectedStorageProfileId));
            File.WriteAllText(Path.Combine(runtimeRootPath, "runtime-scene-catalog.json"), BuildSceneCatalogJson(cookedManifest));
        }

        /// <summary>
        /// Builds the managed runtime startup manifest JSON.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest that contains the startup scene id.</param>
        /// <param name="selectedStorageProfileId">Stable runtime storage profile id.</param>
        /// <returns>Managed runtime startup manifest JSON.</returns>
        static string BuildStartupManifestJson(PlatformBuildManifest cookedManifest, string selectedStorageProfileId) {
            if (string.IsNullOrWhiteSpace(cookedManifest.StartupSceneId)) {
                throw new InvalidOperationException("Cooked manifest did not define a startup scene.");
            }

            return
                "{\n"
                + "  \"StartupSceneId\": \"" + EscapeJson(cookedManifest.StartupSceneId) + "\",\n"
                + "  \"StorageProfileId\": {\n"
                + "    \"Value\": \"" + EscapeJson(selectedStorageProfileId) + "\"\n"
                + "  }\n"
                + "}\n";
        }

        /// <summary>
        /// Builds the managed runtime scene catalog JSON.
        /// </summary>
        /// <param name="cookedManifest">Cooked manifest that contains the final built scenes.</param>
        /// <returns>Managed runtime scene catalog JSON.</returns>
        static string BuildSceneCatalogJson(PlatformBuildManifest cookedManifest) {
            if (cookedManifest.Scenes == null) {
                throw new InvalidOperationException("Cooked manifest did not define any built scenes.");
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"Entries\": [");
            for (int index = 0; index < cookedManifest.Scenes.Length; index++) {
                PlatformBuildScene scene = cookedManifest.Scenes[index];
                string cookedRelativePath = ResolveCookedRelativePath(scene);
                builder.AppendLine("    {");
                builder.AppendLine("      \"SceneId\": \"" + EscapeJson(scene.SceneId) + "\",");
                builder.Append("      \"CookedRelativePath\": \"" + EscapeJson(cookedRelativePath) + "\"");
                builder.AppendLine();
                builder.Append("    }");
                if (index < cookedManifest.Scenes.Length - 1) {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Resolves the cooked runtime path for one built scene entry.
        /// </summary>
        /// <param name="scene">Built scene entry to inspect.</param>
        /// <returns>Cooked runtime-relative scene payload path.</returns>
        static string ResolveCookedRelativePath(PlatformBuildScene scene) {
            for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
                KeyValuePair<string, string> metadata = scene.ResolvedMetadata[index];
                if (string.Equals(metadata.Key, PlatformBuildSceneMetadataKeys.CookedRelativePath, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(metadata.Value)) {
                    return metadata.Value.Replace('\\', '/');
                }
            }

            throw new InvalidOperationException($"Built scene '{scene.SceneId}' did not define a cooked relative path.");
        }

        /// <summary>
        /// Escapes one string for JSON output.
        /// </summary>
        /// <param name="value">String value to escape.</param>
        /// <returns>Escaped JSON string contents.</returns>
        static string EscapeJson(string value) {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
```

```csharp
void WriteManagedRuntimeManifestFiles(
    PlatformBuildManifest cookedManifest,
    string packageRootPath,
    string selectedStorageProfileId) {
    EditorRuntimeManagedManifestWriter writer = new();
    writer.Write(packageRootPath, cookedManifest, selectedStorageProfileId);
}
```

```csharp
WriteManagedRuntimeManifestFiles(
    cookedManifest,
    workspace.PackageRootPath,
    selectedStorageProfileId);
```

- [ ] **Step 4: Run the managed manifest writer tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorRuntimeManagedManifestWriterTests"`

Expected: PASS with 1 passing test in `EditorRuntimeManagedManifestWriterTests`.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorRuntimeManagedManifestWriter.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorRuntimeManagedManifestWriterTests.cs
rtk git commit -m "feat: emit managed runtime scene metadata"
```

## Task 3: Implement the runtime SceneManager and core bootstrap wiring

**Files:**
- Create: `engine/helengine.core/scene/runtime/SceneLoadMode.cs`
- Create: `engine/helengine.core/scene/runtime/LoadedSceneRecord.cs`
- Create: `engine/helengine.core/scene/runtime/SceneLoadingEventArgs.cs`
- Create: `engine/helengine.core/scene/runtime/SceneLoadedEventArgs.cs`
- Create: `engine/helengine.core/scene/runtime/SceneUnloadingEventArgs.cs`
- Create: `engine/helengine.core/scene/runtime/SceneUnloadedEventArgs.cs`
- Create: `engine/helengine.core/scene/runtime/SceneManager.cs`
- Modify: `engine/helengine.core/Core.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Write the failing SceneManager tests**

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene;

/// <summary>
/// Verifies runtime scene management behavior for built scenes.
/// </summary>
public sealed class SceneManagerTests : IDisposable {
    /// <summary>
    /// Temporary content root used by the test harness.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes the runtime test harness.
    /// </summary>
    public SceneManagerTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);

        Core core = new Core(new CoreInitializationOptions {
            ContentRootPath = TempRootPath
        });

        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
        Core.Instance.DefaultFontAsset = CreateFont();
    }

    /// <summary>
    /// Deletes the temporary content root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures additive loading preserves the previous scene and tracks both records.
    /// </summary>
    [Fact]
    public void LoadScene_whenModeIsAdditive_tracksBothLoadedScenes() {
        WriteScene("cooked/scenes/main.hasset", "BootstrapRoot");
        WriteScene("scenes/Scenes/TestPlayableScene.hasset", "PlayableRoot");

        RuntimeSceneCatalog catalog = new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene.hasset")
            ]);
        SceneManager sceneManager = new SceneManager(catalog, Core.Instance.SceneLoadService, Core.Instance.ContentManager);

        sceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
        sceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Additive);

        Assert.Equal(2, sceneManager.LoadedScenes.Count);
        Assert.Equal("Scenes/Bootstrap.helen", sceneManager.LoadedScenes[0].SceneId);
        Assert.Equal("Scenes/TestPlayableScene.helen", sceneManager.LoadedScenes[1].SceneId);
    }

    /// <summary>
    /// Ensures single-scene replacement unloads the previous scene before loading the next one.
    /// </summary>
    [Fact]
    public void LoadScene_whenModeIsSingle_unloadsPreviousSceneBeforeLoadingReplacement() {
        WriteScene("cooked/scenes/main.hasset", "BootstrapRoot");
        WriteScene("scenes/Scenes/TestPlayableScene.hasset", "PlayableRoot");

        RuntimeSceneCatalog catalog = new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene.hasset")
            ]);
        SceneManager sceneManager = new SceneManager(catalog, Core.Instance.SceneLoadService, Core.Instance.ContentManager);
        List<string> eventLog = new List<string>();

        sceneManager.SceneUnloading += (_, args) => eventLog.Add("unloading:" + args.SceneId);
        sceneManager.SceneLoaded += (_, args) => eventLog.Add("loaded:" + args.SceneId);

        sceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
        sceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);

        Assert.Equal(
            [
                "loaded:Scenes/Bootstrap.helen",
                "unloading:Scenes/Bootstrap.helen",
                "loaded:Scenes/TestPlayableScene.helen"
            ],
            eventLog);
        Assert.Single(sceneManager.LoadedScenes);
        Assert.Equal("Scenes/TestPlayableScene.helen", sceneManager.LoadedScenes[0].SceneId);
    }

    /// <summary>
    /// Ensures unload notifications expose the tracked root entities and remove only the scene record.
    /// </summary>
    [Fact]
    public void UnloadScene_raisesTrackedRootsWithoutDisposingEntities() {
        WriteScene("cooked/scenes/main.hasset", "BootstrapRoot");

        RuntimeSceneCatalog catalog = new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset")
            ]);
        SceneManager sceneManager = new SceneManager(catalog, Core.Instance.SceneLoadService, Core.Instance.ContentManager);
        IReadOnlyList<Entity> notifiedRoots = Array.Empty<Entity>();

        sceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
        Entity trackedRoot = sceneManager.LoadedScenes[0].RootEntities[0];
        sceneManager.SceneUnloading += (_, args) => notifiedRoots = args.RootEntities;

        sceneManager.UnloadScene("Scenes/Bootstrap.helen");

        Assert.Same(trackedRoot, Assert.Single(notifiedRoots));
        Assert.Empty(sceneManager.LoadedScenes);
        Assert.Contains(trackedRoot, Core.Instance.ObjectManager.Entities);
    }

    /// <summary>
    /// Writes a minimal packaged scene asset at the supplied relative path.
    /// </summary>
    /// <param name="relativePath">Content-relative cooked scene path.</param>
    /// <param name="entityName">Root entity name stored in the scene asset.</param>
    void WriteScene(string relativePath, string entityName) {
        string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        SceneAsset sceneAsset = new SceneAsset {
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = entityName,
                    Name = entityName,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            }
        };

        using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Creates a minimal runtime font for the current test harness.
    /// </summary>
    /// <returns>Minimal runtime font asset.</returns>
    static FontAsset CreateFont() {
        return new FontAsset {
            FontInfo = new FontInfo("TestFont", FontStyle.Regular, 16f),
            LineHeight = 16f,
            Glyphs = Array.Empty<FontGlyphAsset>(),
            Texture = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            }
        };
    }
}
```

- [ ] **Step 2: Run the SceneManager tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneManagerTests"`

Expected: FAIL with errors that `SceneManager`, `SceneLoadMode`, and `LoadedSceneRecord` do not exist.

- [ ] **Step 3: Write the minimal SceneManager types and core wiring**

```csharp
namespace helengine {
    /// <summary>
    /// Describes the runtime scene load mode.
    /// </summary>
    public enum SceneLoadMode {
        /// <summary>
        /// Replaces all currently loaded scenes.
        /// </summary>
        Single = 0,

        /// <summary>
        /// Preserves currently loaded scenes and appends the new one.
        /// </summary>
        Additive = 1
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Tracks one currently loaded runtime scene.
    /// </summary>
    public sealed class LoadedSceneRecord {
        /// <summary>
        /// Initializes one loaded scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene id for the loaded scene.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        /// <param name="rootEntities">Tracked root entities materialized for the scene.</param>
        public LoadedSceneRecord(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path is required.", nameof(cookedRelativePath));
            }
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            SceneId = sceneId;
            CookedRelativePath = cookedRelativePath;
            RootEntities = rootEntities;
        }

        /// <summary>
        /// Gets the stable scene id for the loaded scene.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Gets the tracked root entities for the loaded scene.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Carries scene lifecycle metadata for pre-load notifications.
    /// </summary>
    public sealed class SceneLoadingEventArgs : EventArgs {
        /// <summary>
        /// Initializes one scene-loading event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene id being loaded.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        public SceneLoadingEventArgs(string sceneId, string cookedRelativePath) {
            SceneId = sceneId ?? throw new ArgumentNullException(nameof(sceneId));
            CookedRelativePath = cookedRelativePath ?? throw new ArgumentNullException(nameof(cookedRelativePath));
        }

        /// <summary>
        /// Gets the stable scene id being loaded.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Carries scene lifecycle metadata for post-load notifications.
    /// </summary>
    public sealed class SceneLoadedEventArgs : EventArgs {
        /// <summary>
        /// Initializes one scene-loaded event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene id that finished loading.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        /// <param name="rootEntities">Tracked root entities that were materialized.</param>
        public SceneLoadedEventArgs(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities) {
            SceneId = sceneId ?? throw new ArgumentNullException(nameof(sceneId));
            CookedRelativePath = cookedRelativePath ?? throw new ArgumentNullException(nameof(cookedRelativePath));
            RootEntities = rootEntities ?? throw new ArgumentNullException(nameof(rootEntities));
        }

        /// <summary>
        /// Gets the stable scene id that finished loading.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Gets the tracked root entities that were materialized.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Carries scene lifecycle metadata for pre-load and pre-unload notifications.
    /// </summary>
    public sealed class SceneUnloadingEventArgs : EventArgs {
        /// <summary>
        /// Initializes one scene-unloading event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene id being unloaded.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        /// <param name="rootEntities">Tracked root entities that the player must destroy.</param>
        public SceneUnloadingEventArgs(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities) {
            SceneId = sceneId ?? throw new ArgumentNullException(nameof(sceneId));
            CookedRelativePath = cookedRelativePath ?? throw new ArgumentNullException(nameof(cookedRelativePath));
            RootEntities = rootEntities ?? throw new ArgumentNullException(nameof(rootEntities));
        }

        /// <summary>
        /// Gets the stable scene id being unloaded.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Gets the tracked root entities that the player must destroy.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Carries scene lifecycle metadata for post-unload notifications.
    /// </summary>
    public sealed class SceneUnloadedEventArgs : EventArgs {
        /// <summary>
        /// Initializes one scene-unloaded event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene id that was removed from tracking.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        public SceneUnloadedEventArgs(string sceneId, string cookedRelativePath) {
            SceneId = sceneId ?? throw new ArgumentNullException(nameof(sceneId));
            CookedRelativePath = cookedRelativePath ?? throw new ArgumentNullException(nameof(cookedRelativePath));
        }

        /// <summary>
        /// Gets the stable scene id that was removed from tracking.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Owns runtime scene bookkeeping and dispatches explicit scene lifecycle notifications.
    /// </summary>
    public sealed class SceneManager {
        /// <summary>
        /// Build-scene lookup used by the runtime scene manager.
        /// </summary>
        readonly RuntimeSceneCatalog SceneCatalog;

        /// <summary>
        /// Runtime scene loader used to materialize packaged scene assets.
        /// </summary>
        readonly RuntimeSceneLoadService SceneLoadService;

        /// <summary>
        /// Content manager used to deserialize cooked scene payloads.
        /// </summary>
        readonly ContentManager ContentManager;

        /// <summary>
        /// Loaded scene records in load order.
        /// </summary>
        readonly List<LoadedSceneRecord> LoadedSceneRecords;

        /// <summary>
        /// Loaded scene records keyed by scene id.
        /// </summary>
        readonly Dictionary<string, LoadedSceneRecord> LoadedScenesById;

        /// <summary>
        /// Initializes one runtime scene manager.
        /// </summary>
        /// <param name="sceneCatalog">Runtime build-scene lookup.</param>
        /// <param name="sceneLoadService">Runtime scene materialization service.</param>
        /// <param name="contentManager">Content manager rooted at the packaged runtime content path.</param>
        public SceneManager(RuntimeSceneCatalog sceneCatalog, RuntimeSceneLoadService sceneLoadService, ContentManager contentManager) {
            SceneCatalog = sceneCatalog ?? throw new ArgumentNullException(nameof(sceneCatalog));
            SceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
            ContentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            LoadedSceneRecords = new List<LoadedSceneRecord>();
            LoadedScenesById = new Dictionary<string, LoadedSceneRecord>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Raised before a scene payload is loaded and materialized.
        /// </summary>
        public event EventHandler<SceneLoadingEventArgs> SceneLoading;

        /// <summary>
        /// Raised after a scene payload has been materialized and tracked.
        /// </summary>
        public event EventHandler<SceneLoadedEventArgs> SceneLoaded;

        /// <summary>
        /// Raised before a loaded scene record is removed.
        /// </summary>
        public event EventHandler<SceneUnloadingEventArgs> SceneUnloading;

        /// <summary>
        /// Raised after a loaded scene record has been removed.
        /// </summary>
        public event EventHandler<SceneUnloadedEventArgs> SceneUnloaded;

        /// <summary>
        /// Gets the loaded scene records in load order.
        /// </summary>
        public IReadOnlyList<LoadedSceneRecord> LoadedScenes => LoadedSceneRecords;

        /// <summary>
        /// Loads one built scene using the requested load mode.
        /// </summary>
        /// <param name="sceneId">Stable scene id to load.</param>
        /// <param name="loadMode">Runtime load mode.</param>
        public void LoadScene(string sceneId, SceneLoadMode loadMode) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (!SceneCatalog.TryGetEntry(sceneId, out RuntimeSceneCatalogEntry entry)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' was not found in the build scene catalog.");
            }
            if (loadMode == SceneLoadMode.Additive && LoadedScenesById.ContainsKey(sceneId)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is already loaded.");
            }
            if (loadMode == SceneLoadMode.Single) {
                UnloadAllScenes();
            }

            SceneLoading?.Invoke(this, new SceneLoadingEventArgs(entry.SceneId, entry.CookedRelativePath));
            SceneAsset sceneAsset = ContentManager.Load<SceneAsset>(entry.CookedRelativePath, RuntimeContentProcessorIds.SceneAsset);
            IReadOnlyList<Entity> rootEntities = SceneLoadService.Load(sceneAsset);
            LoadedSceneRecord record = new LoadedSceneRecord(entry.SceneId, entry.CookedRelativePath, rootEntities);
            LoadedSceneRecords.Add(record);
            LoadedScenesById.Add(record.SceneId, record);
            SceneLoaded?.Invoke(this, new SceneLoadedEventArgs(record.SceneId, record.CookedRelativePath, record.RootEntities));
        }

        /// <summary>
        /// Unloads one currently tracked scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene id to unload.</param>
        public void UnloadScene(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (!LoadedScenesById.TryGetValue(sceneId, out LoadedSceneRecord record)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is not currently loaded.");
            }

            SceneUnloading?.Invoke(this, new SceneUnloadingEventArgs(record.SceneId, record.CookedRelativePath, record.RootEntities));
            LoadedScenesById.Remove(record.SceneId);
            LoadedSceneRecords.Remove(record);
            SceneUnloaded?.Invoke(this, new SceneUnloadedEventArgs(record.SceneId, record.CookedRelativePath));
        }

        /// <summary>
        /// Returns whether the supplied scene is currently loaded.
        /// </summary>
        /// <param name="sceneId">Stable scene id to test.</param>
        /// <returns>True when the scene is currently loaded.</returns>
        public bool IsSceneLoaded(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            return LoadedScenesById.ContainsKey(sceneId);
        }

        /// <summary>
        /// Attempts to resolve one currently loaded scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene id to resolve.</param>
        /// <param name="loadedScene">Resolved loaded scene record when one exists.</param>
        /// <returns>True when the scene is currently loaded.</returns>
        public bool TryGetLoadedScene(string sceneId, out LoadedSceneRecord loadedScene) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            return LoadedScenesById.TryGetValue(sceneId, out loadedScene);
        }

        /// <summary>
        /// Unloads every currently loaded scene in load order.
        /// </summary>
        void UnloadAllScenes() {
            while (LoadedSceneRecords.Count > 0) {
                UnloadScene(LoadedSceneRecords[0].SceneId);
            }
        }
    }
}
```

```csharp
/// <summary>
/// Gets the runtime scene manager configured for built scene loading.
/// </summary>
public SceneManager SceneManager { get; private set; }
```

```csharp
string runtimeSceneCatalogPath = Path.Combine(InitializationOptions.ContentRootPath, "runtime-scene-catalog.json");
if (File.Exists(runtimeSceneCatalogPath)) {
    RuntimeSceneCatalog runtimeSceneCatalog = RuntimeSceneCatalog.ReadFromFile(runtimeSceneCatalogPath);
    SceneManager = new SceneManager(runtimeSceneCatalog, SceneLoadService, contentManager);
} else {
    SceneManager = null;
}
```

- [ ] **Step 4: Run the SceneManager tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneManagerTests"`

Expected: PASS with 3 passing tests in `SceneManagerTests`.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core/scene/runtime/SceneLoadMode.cs engine/helengine.core/scene/runtime/LoadedSceneRecord.cs engine/helengine.core/scene/runtime/SceneLoadingEventArgs.cs engine/helengine.core/scene/runtime/SceneLoadedEventArgs.cs engine/helengine.core/scene/runtime/SceneUnloadingEventArgs.cs engine/helengine.core/scene/runtime/SceneUnloadedEventArgs.cs engine/helengine.core/scene/runtime/SceneManager.cs engine/helengine.core/Core.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs
rtk git commit -m "feat: add runtime scene manager"
```

## Task 4: Migrate runtime menu scene actions to the SceneManager

**Files:**
- Modify: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing menu regression test**

```csharp
[Fact]
public void Load_WhenRuntimeMenuActionLoadsScene_routesThroughSceneManager() {
    string projectRootPath = Path.Combine(TempRootPath, "menu-runtime-scene-manager-project");
    string assetsRootPath = Path.Combine(projectRootPath, "assets");
    string buildRootPath = Path.Combine(TempRootPath, "menu-runtime-scene-manager-build");
    Directory.CreateDirectory(assetsRootPath);
    Directory.CreateDirectory(buildRootPath);

    string titleFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscTitle.ttf");
    Directory.CreateDirectory(Path.GetDirectoryName(titleFontPath));
    File.WriteAllBytes(titleFontPath, new byte[] { 1, 2, 3, 4 });

    string bodyFontPath = Path.Combine(assetsRootPath, "Fonts", "DemoDiscBody.ttf");
    Directory.CreateDirectory(Path.GetDirectoryName(bodyFontPath));
    File.WriteAllBytes(bodyFontPath, new byte[] { 5, 6, 7, 8 });

    string playableScenePath = Path.Combine(assetsRootPath, "Scenes", "TestPlayableScene.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(playableScenePath));
    using (FileStream playableSceneStream = new FileStream(playableScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
        EditorAssetBinarySerializer.Serialize(
            playableSceneStream,
            new SceneAsset {
                Id = "Scenes/TestPlayableScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "playable-root",
                        Name = "PlayableRoot",
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });
    }

    SceneAsset authoredSceneAsset = BuildDemoMenuSceneAsset();
    string authoredScenePath = Path.Combine(assetsRootPath, "Scenes", "TestMenu.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(authoredScenePath));
    using (FileStream authoredSceneStream = new FileStream(authoredScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
        EditorAssetBinarySerializer.Serialize(authoredSceneStream, authoredSceneAsset);
    }

    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        projectRootPath,
        new IAssetImporterRegistration[] {
            new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" })
        },
        CreateFont());
    packager.Package(new[] { "Scenes/TestMenu.helen", "Scenes/TestPlayableScene.helen" }, buildRootPath);

    File.WriteAllText(
        Path.Combine(buildRootPath, "runtime-scene-catalog.json"),
        """
        {
          "Entries": [
            {
              "SceneId": "Scenes/TestMenu.helen",
              "CookedRelativePath": "cooked/scenes/main.hasset"
            },
            {
              "SceneId": "Scenes/TestPlayableScene.helen",
              "CookedRelativePath": "scenes/Scenes/TestPlayableScene.hasset"
            }
          ]
        }
        """);

    Core core = new Core(new CoreInitializationOptions {
        ContentRootPath = buildRootPath
    });
    core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
    Core.Instance.DefaultFontAsset = CreateFont();

    MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
    TestInputBackend input = Assert.IsType<TestInputBackend>(Core.Instance.InputSystem.Backend);

    input.SetKeyboardState(new KeyboardState());
    input.EarlyUpdate();
    menuHostComponent.Update();
    input.Update();

    input.SetKeyboardState(new KeyboardState(Keys.Enter));
    input.EarlyUpdate();
    menuHostComponent.Update();
    input.Update();

    input.SetKeyboardState(new KeyboardState());
    input.EarlyUpdate();
    menuHostComponent.Update();
    input.Update();

    input.SetKeyboardState(new KeyboardState(Keys.Enter));
    input.EarlyUpdate();
    menuHostComponent.Update();
    input.Update();

    Assert.NotNull(Core.Instance.SceneManager);
    Assert.True(Core.Instance.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
}
```

- [ ] **Step 2: Run the menu regression test to verify it fails**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenRuntimeMenuActionLoadsScene_routesThroughSceneManager"`

Expected: FAIL because `MenuComponent` still bypasses `Core.Instance.SceneManager` and does not track the loaded scene.

- [ ] **Step 3: Write the minimal menu migration**

```csharp
void LoadScene(string scenePath) {
    if (string.IsNullOrWhiteSpace(scenePath)) {
        throw new InvalidOperationException("Scene-loading baked menu items must provide a scene path.");
    }
    if (Core.Instance == null) {
        throw new InvalidOperationException("A core instance must exist before loading a scene from the baked menu.");
    }

    if (ComponentExecutionContext.CurrentMode == ComponentExecutionMode.Editor) {
        if (Core.Instance.SceneLoadService == null) {
            throw new InvalidOperationException("Core scene loading services must be initialized before loading a scene from the baked menu.");
        }

        string resolvedScenePath = ResolveSceneContentPath(scenePath);
        SceneAsset sceneAsset = Core.Instance.ContentManager.Load<SceneAsset>(resolvedScenePath, RuntimeContentProcessorIds.SceneAsset);
        Core.Instance.SceneLoadService.Load(sceneAsset);
        Parent.Enabled = false;
        return;
    }

    if (Core.Instance.SceneManager == null) {
        throw new InvalidOperationException("Core scene manager must be initialized before runtime menu scene loading can occur.");
    }

    Core.Instance.SceneManager.LoadScene(scenePath, SceneLoadMode.Single);
    Parent.Enabled = false;
}
```

- [ ] **Step 4: Run the menu regression test to verify it passes**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenRuntimeMenuActionLoadsScene_routesThroughSceneManager"`

Expected: PASS with the new menu scene-loading regression test green.

- [ ] **Step 5: Run the focused manifest and scene-manager regression suite**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneCatalogTests|FullyQualifiedName~EditorRuntimeManagedManifestWriterTests|FullyQualifiedName~SceneManagerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests"`

Expected: PASS with all new scene catalog, writer, manager, and menu-routing tests green.

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.core/components/2d/menu/MenuComponent.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
rtk git commit -m "feat: route runtime menu scene loads through scene manager"
```

## Self-Review Checklist

- Spec coverage:
  - Runtime scene catalog artifact: Task 1 and Task 2.
  - Runtime `SceneManager` API and scene tracking: Task 3.
  - Single versus additive load behavior: Task 3.
  - Explicit unload events with player-owned teardown: Task 3.
  - Current runtime consumer migration: Task 4.
- Placeholder scan:
  - No `TODO`, `TBD`, or "similar to" instructions remain.
  - Every test and command is concrete.
- Type consistency:
  - `RuntimeSceneCatalog`, `RuntimeSceneCatalogEntry`, `SceneManager`, `LoadedSceneRecord`, and `SceneLoadMode` use the same names in all tasks.
  - The managed writer emits `runtime-scene-catalog.json`, which matches the `Core` bootstrap and runtime reader path.
