# Remove DS Generated Font Engine Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the generated `editor:ds-debug-font` reference and all Nintendo DS-specific font resolution from shared `engine/helengine.editor` code, with no backward compatibility.

**Architecture:** Delete the DS-only generated font branch at every shared-engine/editor entry point instead of moving it behind a new abstraction. Shared code will only support the editor default generated font plus ordinary file-backed font references, and tests will be rewritten or removed so they no longer pin the deleted DS-specific behavior.

**Tech Stack:** C#, xUnit, .NET 9, PowerShell, `dotnetw.ps1`

---

### Task 1: Remove the shared font-reference factory surface

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`

- [ ] **Step 1: Write the failing tests by deleting DS-generated reference authors**

Replace the DS-specific helper bodies in the tests so they stop manufacturing `generated/editor/fonts/ds-debug.hefont`.

```csharp
static SceneAssetReference CreateNintendoDsDebugFontReference() {
    return new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Fonts/ds-debug.hefont",
        ProviderId = string.Empty,
        AssetId = string.Empty
    };
}
```

Also rename any test that only exists to prove DS-generated rewriting so the new name describes ordinary file-backed font rewriting instead.

- [ ] **Step 2: Run the focused tests to verify they fail for the expected reason**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildScenePackagerTests&(FullyQualifiedName~DebugComponent|FullyQualifiedName~FpsOverlay)" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: FAIL because production code still expects `ds-debug-font` or the rewritten test names/assertions no longer match the old behavior.

- [ ] **Step 3: Delete the DS generated-reference factory from shared code**

Remove these members from `FontAssetScenePersistenceSupport.cs`:

```csharp
const string NintendoDsDebugFontAssetId = "ds-debug-font";
const string NintendoDsDebugFontRelativePath = "generated/editor/fonts/ds-debug.hefont";

internal static SceneAssetReference BuildNintendoDsDebugFontReference() {
    return new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.Generated,
        RelativePath = NintendoDsDebugFontRelativePath,
        ProviderId = EditorGeneratedProviderId,
        AssetId = NintendoDsDebugFontAssetId
    };
}
```

Leave the editor default font helper intact.

- [ ] **Step 4: Re-run the focused tests to verify this task stays green**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildScenePackagerTests&(FullyQualifiedName~DebugComponent|FullyQualifiedName~FpsOverlay)" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: still FAIL, but now only because the remaining runtime/shared services still contain DS-specific resolution branches.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs
git commit -m "Remove DS generated font reference factory"
```

### Task 2: Remove editor-time DS font resolution from shared services

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/managers/project/TextComponentSpriteBakeService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing test for unsupported generated DS font resolution**

Add or rewrite one test so shared code now rejects the removed generated asset id instead of resolving it through `helengine.editor.app`.

```csharp
[Fact]
public void PackageBuild_WhenDebugComponentUsesRemovedDsGeneratedFont_ThrowsUnsupportedGeneratedReference() {
    string sceneId = "Scenes/DebugScene.helen";
    SceneAssetReference removedReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.Generated,
        RelativePath = "generated/editor/fonts/ds-debug.hefont",
        ProviderId = "editor",
        AssetId = "ds-debug-font"
    };

    WriteSceneAsset(sceneId, "helengine.DebugComponent", WriteDebugComponentPayload(removedReference), new[] { removedReference });

    FontAsset defaultFont = CreatePackagedFontAsset();
    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        ProjectRootPath,
        Array.Empty<IAssetImporterRegistration>(),
        defaultFont);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => packager.Package(new[] { sceneId }, BuildRootPath));
    Assert.Contains("Unsupported generated", exception.Message);
}
```

- [ ] **Step 2: Run the focused test to verify it fails before production edits**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PackageBuild_WhenDebugComponentUsesRemovedDsGeneratedFont_ThrowsUnsupportedGeneratedReference" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: FAIL because shared code still resolves the DS font through `helengine.editor.app`.

- [ ] **Step 3: Delete the DS resolver branches from shared editor services**

In `EditorSceneAssetReferenceResolver.cs`, reduce the generated-font handling to only the editor default font:

```csharp
if (string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
    if (Core.Instance is not EditorCore editorCore || editorCore.DefaultFontAssetForEditor == null) {
        throw new InvalidOperationException("The editor font is not available in the active editor core.");
    }

    return editorCore.DefaultFontAssetForEditor;
}

throw new InvalidOperationException($"Unsupported generated font asset id '{reference.AssetId}'.");
```

Delete `NintendoDsDebugFontAssetId` and `ResolveGeneratedNintendoDsDebugFont()`.

In `TextComponentSpriteBakeService.cs`, reduce the generated-font path to:

```csharp
if (string.Equals(fontReference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
    return DefaultEditorFontAsset;
}

throw new InvalidOperationException($"Unsupported generated font asset id '{fontReference.AssetId}'.");
```

Delete `NintendoDsDebugFontAssetId` and `ResolveGeneratedNintendoDsDebugFont()`.

- [ ] **Step 4: Re-run the focused test to verify it passes**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PackageBuild_WhenDebugComponentUsesRemovedDsGeneratedFont_ThrowsUnsupportedGeneratedReference" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/managers/project/TextComponentSpriteBakeService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "Remove DS font resolution from shared editor services"
```

### Task 3: Remove DS-specific packaging and transform rewrites

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`

- [ ] **Step 1: Write the failing tests for ordinary file-backed font rewriting**

Rewrite the old DS-specific tests so they assert ordinary file-backed font references are rewritten instead of any DS-generated behavior.

```csharp
[Fact]
public void PackageBuild_WhenSceneContainsDebugComponentWithFileBackedFont_RewritesRuntimePayloadAndFontReference() {
    string sceneId = "Scenes/DebugScene.helen";
    SceneAssetReference fontReference = CreateEditorFontReference();

    WriteSceneAsset(sceneId, "helengine.DebugComponent", WriteDebugComponentPayload(fontReference), new[] { fontReference });

    FontAsset defaultFont = CreatePackagedFontAsset();
    EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
        ProjectRootPath,
        Array.Empty<IAssetImporterRegistration>(),
        defaultFont);
    packager.Package(new[] { sceneId }, BuildRootPath);

    using FileStream stream = File.OpenRead(GetPackagedScenePath(BuildRootPath, sceneId));
    SceneAsset packagedScene = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    SceneComponentAssetRecord componentRecord = packagedScene.RootEntities[0].Components[0];

    AssertUsesAutomaticRuntimePayload(componentRecord, typeof(DebugComponent));
    AssertAutomaticRuntimeAssetReference(componentRecord, "Font", "cooked/fonts/default.hefont");
}
```

For `SceneComponentPackagingTransformServiceTests`, replace the old DS-generated helper with the same file-backed reference pattern and keep the expectation on the rewritten cooked path.

- [ ] **Step 2: Run the focused transform and packager tests to verify they fail first**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~EditorPlatformBuildScenePackagerTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: FAIL in the updated DS-replacement cases until the remaining production branches are removed.

- [ ] **Step 3: Delete the DS-specific generated-font branches from production packaging code**

In `EditorWindowsBuildScenePackager.cs`, delete:

```csharp
const string NintendoDsDebugFontAssetId = "ds-debug-font";
const string NintendoDsDebugFontRelativePath = "cooked/fonts/ds-debug.hefont";
```

Delete the generated-reference branch:

```csharp
if (string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal) &&
    string.Equals(reference.AssetId, NintendoDsDebugFontAssetId, StringComparison.Ordinal)) {
    return RewriteGeneratedNintendoDsDebugFontReference(buildRootPath);
}
```

Delete:

```csharp
SceneAssetReference RewriteGeneratedNintendoDsDebugFontReference(string buildRootPath) { ... }
static FontAsset ResolveGeneratedNintendoDsDebugFont() { ... }
```

In `SceneComponentPackagingTransformService.cs`, delete the DS-specific generated-font asset id constant and the special-case branch inside `RewriteFontReference(...)` so file-backed and editor-default font handling remain the only supported paths.

- [ ] **Step 4: Re-run the focused transform and packager tests to verify they pass**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~EditorPlatformBuildScenePackagerTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: PASS for the updated file-backed-font cases, with no attempt to load `helengine.editor.app` for DS font generation.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs
git commit -m "Remove DS font packaging branches from shared editor code"
```

### Task 4: Final sweep and focused verification

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the final regression assertions**

Add one final source-compatibility assertion where useful by checking unsupported generated references now throw, and keep the ordinary `FPSComponent` / `DebugComponent` runtime assertions unchanged.

```csharp
Assert.Contains("Unsupported generated", exception.Message);
AssertUsesAutomaticRuntimePayload(componentRecord, typeof(DebugComponent));
AssertAutomaticRuntimeAssetReference(componentRecord, "Font", "cooked/fonts/default.hefont");
```

- [ ] **Step 2: Run the exact focused verification slice**

Run: `.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~EditorPlatformBuildScenePackagerTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false`

Expected: PASS for the updated focused slice.

- [ ] **Step 3: Run the source sweep for stale DS generated-font references**

Run: `rg -n "ds-debug-font|generated/editor/fonts/ds-debug\\.hefont|NintendoDsDebugFont" engine`

Expected: no matches inside shared `engine/helengine.editor` code paths that were in scope for this spec. Remaining app-side matches are acceptable.

- [ ] **Step 4: Commit**

```bash
git add engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "Finalize DS generated font removal verification"
```

## Self-Review

- Spec coverage:
  - remove packager DS path: Task 3
  - remove resolver DS path: Task 2
  - remove sprite-baker DS path: Task 2
  - remove generated reference helper: Task 1
  - rewrite/delete DS-specific tests: Tasks 1 and 3
  - final source sweep: Task 4
- Placeholder scan:
  - no `TODO` or `TBD`
  - each task includes concrete files, commands, and code snippets
- Type consistency:
  - uses existing `SceneAssetReference`, `FontAsset`, `DebugComponent`, `EditorPlatformBuildScenePackager`, `SceneComponentPackagingTransformService`, and focused xUnit test naming patterns already present in the repo
