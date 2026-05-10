# Remove Runtime Scene Catalog JSON From Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove JSON from `helengine.core` runtime bootstrap entirely, inject runtime scene metadata as in-memory data, and make PS2 and other packaged runtimes initialize scene management from generated native/runtime-owned data instead of manifest files.
**Architecture:** `helengine.core` becomes format-agnostic and accepts prebuilt runtime metadata through initialization options. Runtime manifest parsing and file generation stay outside core. The editor/build pipeline generates native/runtime-owned scene catalog data, and platform hosts wire that data into `Core` and `SceneManager`.
**Tech Stack:** C#, .NET 9, native generated-core C++, PS2 builder/runtime, xUnit/NUnit-style existing test suites, Docker-backed PS2 packaging flow.

---

## Constraints

- [ ] Keep JSON out of `helengine.core`. Do not add a compatibility fallback.
- [ ] Preserve `SceneManager` behavior after initialization; only change how its catalog is supplied.
- [ ] Do not rely on runtime filesystem probes for startup scene, runtime scene catalog, or runtime code module metadata inside `helengine.core`.
- [ ] Do not revert unrelated local changes already present in the repository.

## Task 1: Inject runtime scene catalog into `Core`

### Files

- [ ] Update `C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneManagerTests.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\RuntimeSceneLoadServiceTests.cs`

### Implementation

- [ ] Add an explicit `RuntimeSceneCatalog SceneCatalog` member to `CoreInitializationOptions`.
  - Make it a required runtime dependency for packaged runtime startup paths.
  - Document it with XML comments describing that hosts/builders must provide the resolved runtime scene catalog before `Core` initialization.
- [ ] Change `Core` so `SceneManager` is created from `InitializationOptions.SceneCatalog`.
  - Remove `CreateSceneManager(ContentManager contentManager)`.
  - Remove `ResolveRuntimeSceneCatalogPath()`.
  - Keep `SceneManager` construction in `Core`, but make it consume only in-memory data.
- [ ] Update tests that currently write `runtime-scene-catalog.json` to temp content roots.
  - Replace file-writing helpers with in-memory `RuntimeSceneCatalog` construction.
  - Ensure tests still cover scene lookup, scene switching, and menu-driven loading behavior.

### Expected code shape

```csharp
/// <summary>
/// Gets or sets the runtime scene catalog supplied by the host during core initialization.
/// </summary>
public RuntimeSceneCatalog SceneCatalog { get; set; }
```

```csharp
SceneManager = new SceneManager(
    InitializationOptions.SceneCatalog,
    contentManager,
    SceneLoadService,
    ObjectManager);
```

### Verification

- [ ] Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneManagerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests"
```
- [ ] Confirm no remaining test helpers write `runtime-scene-catalog.json` for core bootstrap.

### Commit

- [ ] Commit this slice as: `refactor: inject runtime scene catalog into core`

## Task 2: Remove runtime JSON manifest parsing from `helengine.core`

### Files

- [ ] Update `C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeSceneCatalog.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeStartupManifest.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeCodeModuleManifest.cs`
- [ ] Delete `C:\dev\helworks\helengine\engine\helengine.core\content\RuntimeManifestJsonReader.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\RuntimeSceneCatalogTests.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\RuntimeStartupManifestTests.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\RuntimeCodeModuleManifestTests.cs`

### Implementation

- [ ] Strip file-reading responsibilities from runtime manifest model classes in core.
  - `RuntimeSceneCatalog` should remain a pure runtime data structure plus lookup logic.
  - `RuntimeStartupManifest` should remain a pure startup data structure.
  - `RuntimeCodeModuleManifest` should remain a pure runtime code-module data structure.
- [ ] Remove `ReadFromFile(...)` APIs from those types.
- [ ] Delete the shared JSON reader from core.
- [ ] Rewrite affected tests to validate construction, lookup, and invariants without file I/O.
  - If editor-side JSON parsing is still needed for tooling, plan that parsing to live in editor code only in Task 3.

### Expected code shape

```csharp
public sealed class RuntimeSceneCatalog {
    readonly Dictionary<string, string> ScenePathsById;

    public RuntimeSceneCatalog(IReadOnlyDictionary<string, string> scenePathsById) {
        ArgumentNullException.ThrowIfNull(scenePathsById);
        ScenePathsById = new Dictionary<string, string>(scenePathsById, StringComparer.Ordinal);
    }
}
```

### Verification

- [ ] Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~RuntimeSceneCatalogTests|FullyQualifiedName~RuntimeStartupManifestTests|FullyQualifiedName~RuntimeCodeModuleManifestTests"
```
- [ ] Run:
```powershell
rtk dotnet build C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj -c Debug
```
- [ ] Confirm `helengine.core` no longer contains JSON parsing/runtime manifest file readers.

### Commit

- [ ] Commit this slice as: `refactor: remove runtime json parsing from core`

## Task 3: Move runtime scene catalog generation and parsing to the editor/build boundary

### Files

- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorRuntimeNativeManifestWriter.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformBuildGraphRunner.cs`
- [ ] Update or remove `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorRuntimeManagedManifestWriter.cs`
- [ ] Add or update editor-only runtime manifest parsing helpers under `C:\dev\helworks\helengine\engine\helengine.editor\...`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorRuntimeNativeManifestWriterTests.cs`
- [ ] Update `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorGeneratedCoreRegenerationServiceTests.cs`

### Implementation

- [ ] Extend the native manifest writer to emit runtime scene catalog data alongside startup/code-module manifests.
  - Generate a native scene catalog manifest pair in generated-core.
  - Follow existing startup/code-module manifest writer patterns for namespace, symbol naming, and output paths.
- [ ] Remove runtime scene-catalog JSON from the packaged runtime build graph.
  - If `EditorRuntimeManagedManifestWriter` still serves editor-only tooling, narrow it so it no longer participates in packaged runtime output.
  - If it only exists for runtime packaging, delete it and its call sites.
- [ ] Add an editor-side adapter that can still produce `RuntimeSceneCatalog` instances from editor data structures when needed, without leaking JSON parsing into core.
- [ ] Update generated-core regeneration tests to assert the new native scene catalog artifacts exist.

### Expected native outputs

- [ ] `generated-core/runtime/runtime_scene_catalog_manifest.hpp`
- [ ] `generated-core/runtime/runtime_scene_catalog_manifest.cpp`

### Verification

- [ ] Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests"
```
- [ ] Run:
```powershell
rtk dotnet build C:\dev\helworks\helengine\engine\helengine.editor\helengine.editor.csproj -c Debug
```
- [ ] Inspect the generated-core output for the native scene catalog manifest files.

### Commit

- [ ] Commit this slice as: `feat: generate native runtime scene catalog manifests`

## Task 4: Update PS2 runtime and builder to consume native scene catalog data

### Files

- [ ] Update `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- [ ] Update `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- [ ] Update `C:\dev\helworks\helengine-ps2\builder\Ps2RuntimeAssetPathManifestWriter.cs`
- [ ] Update `C:\dev\helworks\helengine-ps2\builder\Ps2DiscPathResolver.cs`
- [ ] Update `C:\dev\helworks\helengine-ps2\builder.tests\Ps2RuntimeAssetPathManifestWriterTests.cs`
- [ ] Update `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`
- [ ] Update `C:\dev\helworks\helengine-ps2\builder.tests\Ps2DiscLayoutWriterTests.cs`

### Implementation

- [ ] Remove JSON scene-catalog loading from the PS2 host.
  - Delete `RuntimeSceneCatalog::ReadFromFile(...)` usage in `Ps2BootHost`.
  - Initialize `SceneManager` from the generated native scene catalog manifest data.
- [ ] Replace the temporary PS2 scene-catalog path export with native scene-catalog symbol access.
  - `Ps2RuntimeAssetPathManifestWriter` should stop generating `he_get_runtime_ps2_scene_catalog_path()`.
  - Generate or reference native scene catalog content instead.
- [ ] Remove `runtime-scene-catalog.json` from the PS2 disc package path and tests.
  - `Ps2DiscPathResolver` and disc-layout tests should no longer expect a root/runtime scene-catalog JSON artifact.
- [ ] Rebuild the PS2 builder and export a fresh ISO after the runtime path is fully native.

### Expected behavioral outcome

- [ ] PS2 boots without trying to open `cdrom0:\runtime-scene-catalog.json;1`.
- [ ] Menu-driven scene switching uses the in-memory/native catalog and reaches the directional-light scene path without the prior initialization exception.

### Verification

- [ ] Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -c Debug
```
- [ ] Run:
```powershell
rtk dotnet build C:\dev\helworks\helengine-ps2\builder\helengine.ps2.builder.csproj -c Debug
```
- [ ] Rebuild the editor app:
```powershell
rtk dotnet build C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj -c Debug
```
- [ ] Rebuild the city PS2 export and watch logs live:
```powershell
rtk dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --headless --project "C:\dev\helprojs\city\project.heproj" --build ps2
```
- [ ] Verify the resulting ISO exists at `C:\dev\helprojs\output\ps2\game.iso`.
- [ ] Boot the ISO and confirm:
  - no JSON scene-catalog file lookup at startup
  - main menu loads
  - selecting the directional light scene no longer fails with the previous scene-manager initialization error

### Commit

- [ ] Commit this slice as: `refactor: remove ps2 runtime json scene catalog dependency`

## Final Verification

- [ ] Run the targeted editor test suites from Tasks 1 through 3 again after Task 4.
- [ ] Run the full PS2 builder test suite again after the final ISO build.
- [ ] Check `git diff --stat` and verify only intended files changed.
- [ ] Record the final ISO path, timestamp, and any runtime validation notes in the handoff message.

## Suggested Execution Order

- [ ] Complete Task 1 first. It establishes the new core boundary.
- [ ] Complete Task 2 second. It removes JSON dependencies from the core runtime types.
- [ ] Complete Task 3 third. It gives packaged runtimes a native scene catalog source.
- [ ] Complete Task 4 last. It consumes the new native path in PS2 and verifies the actual boot flow.

## Risks To Watch

- [ ] Existing tests may implicitly rely on `ContentRootPath`-based scene-catalog discovery. Update them rather than recreating the file path behavior elsewhere.
- [ ] There may be other runtime callers of `RuntimeStartupManifest.ReadFromFile(...)` or `RuntimeCodeModuleManifest.ReadFromFile(...)` outside the core bootstrap path. Remove or relocate those callers instead of leaving JSON helpers in core.
- [ ] Generated-core symbol naming must stay synchronized between editor writers and native consumers. Verify both header and source output together.
- [ ] The PS2 repo may still contain temporary instrumentation around startup exceptions. Keep that if useful for verification, but do not let it hide the underlying native manifest integration work.
