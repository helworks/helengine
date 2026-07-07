# Content Stream Source Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace path-owned runtime content loading with one injected stream-source abstraction across core and all platform hosts, so `ContentManager` no longer owns direct filesystem access.

**Architecture:** Introduce one minimal `IContentStreamSource` interface in `helengine.core`, refactor `ContentManager` and `Core` to depend on that interface, and migrate every host to explicitly provide either a filesystem-backed or packaged-runtime-backed implementation. Keep processor dispatch in `ContentManager`, keep the abstraction narrow (`OpenRead` only), and use the migration to remove `ContentRootPath` from runtime initialization completely.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine core/editor/runtime code, generated-core C++, platform runtime hosts in sibling repos (`helengine-ds`, `helengine-3ds`, `helengine-ps2`, `helengine-psp`, `helengine-gc`, `helengine-wii`, `helengine-windows`)

---

## File Map

- Create: `C:\dev\helworks\helengine\engine\helengine.core\content\IContentStreamSource.cs`
  Defines the engine-wide runtime content stream abstraction.
- Create: `C:\dev\helworks\helengine\engine\helengine.core\content\HostFileSystemContentStreamSource.cs`
  Implements the default host-filesystem-backed source used by editor and desktop-style hosts.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\content\ContentManagerStreamSourceTests.cs`
  Verifies `ContentManager` reads through an injected source instead of direct file IO.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\testing\FakeContentStreamSource.cs`
  Shared test double for stream-source-based tests.
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\content\ContentManager.cs`
  Removes root-path/file-open ownership and routes reads through `IContentStreamSource`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs`
  Replaces `ContentRootPath` with required `ContentStreamSource`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`
  Creates/caches content managers by source instead of root path.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`
  Creates editor content managers from explicit filesystem stream sources.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\EditorCliCommandRunner.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\EditorCliBuildRunner.cs`
  Supplies the new source through `CoreInitializationOptions`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.directx11\compilation\DirectX11ShaderSourceCompiler.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.directx11\compilation\DirectX11ShaderAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\shaders\ShaderModuleManager.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\shaders\EditorShaderPackageExportService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\shaders\EditorBuiltInShaderAssetLibrary.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.shader\shaders\packages\ShaderModulePackageReader.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.shader\shaders\packages\ShaderModulePackage.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.shader\shaders\compilation\ShaderFilesystemIncludeResolver.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.shader\shaders\compilation\ShaderCompileService.cs`
  Migrates all in-repo path-based `ContentManager` construction to explicit filesystem sources.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BinarySerializationTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\ContentManagerTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneManagerTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\UnsavedChangesDialogTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BuildSettingsDialogTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BuildDialogTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BuildDialogCopySettingsDialogTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BinarySerializationExtensionsTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\AutomaticPhysicsRuntimePayloadTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\AssetPickerModalTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\AssetImportSettingsViewTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\AssetImportManagerTests.cs`
  Updates all root-path-based initialization/tests to use explicit sources.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\codegen\features\helengine-feature-catalog.json`
  Keeps `host_file_system` owned only by the true filesystem-backed seam after migration.

### Cross-Repo Platform Migration Files

- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp`
  Replaces `ContentRootPath` assignment with a host-filesystem source assignment in generated-core interop.
- Modify: `C:\dev\helworks\helengine-wii\src\platform\wii\WiiApplication.cpp`
  Replaces packaged content-root boot wiring with a packaged content source assignment.
- Modify: `C:\dev\helworks\helengine-3ds\src\platform\3ds\Nintendo3DsBootHost.cpp`
- Create/Modify: `C:\dev\helworks\helengine-3ds\src\platform\3ds\Nintendo3DsPackagedContentStreamSource.*`
  Wires 3DS packaged runtime through the new abstraction, reusing existing packaged-loader logic.
- Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsBootHost.cpp`
- Create/Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsPackagedContentStreamSource.*`
  Moves DS packaged loading behind the new stream-source seam.
- Modify: `C:\dev\helworks\helengine-ps2\tools\ps2-host-debugger\Ps2HostDebugSession.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\tools\ps2-packaged-scene-probe\ProbeSession.cs`
  Updates PS2 debugging/packaged probes for source-based initialization.
- Search/Modify equivalent boot/session files in:
  - `C:\dev\helworks\helengine-psp`
  - `C:\dev\helworks\helengine-gc`
  - `C:\dev\helworks\helengine-wii`
  Each host must stop assigning `ContentRootPath` and instead construct the right source.

---

### Task 1: Introduce The Core Stream-Source Abstraction

**Files:**
- Create: `engine/helengine.core/content/IContentStreamSource.cs`
- Create: `engine/helengine.core/content/HostFileSystemContentStreamSource.cs`
- Test: `engine/helengine.editor.tests/content/ContentManagerStreamSourceTests.cs`
- Test: `engine/helengine.editor.tests/testing/FakeContentStreamSource.cs`

- [ ] **Step 1: Write the failing stream-source tests**

Add tests that prove:
- `HostFileSystemContentStreamSource.OpenRead` can open a temp file by relative path beneath its configured root
- a fake content source can be observed by `ContentManager` without touching the filesystem

- [ ] **Step 2: Run the new tests to verify they fail**

Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ContentManagerStreamSourceTests"
```
Expected: FAIL because the abstraction and tests are not wired yet.

- [ ] **Step 3: Add `IContentStreamSource`**

Create the minimal interface with:
- one `Stream OpenRead(string assetPath)` method
- substantive XML documentation

- [ ] **Step 4: Add the host-filesystem implementation**

Implement `HostFileSystemContentStreamSource` with:
- constructor-root validation
- relative-path resolution beneath the configured root
- virtual-root preservation behavior matching the current `ContentManager` path semantics where required
- `File.OpenRead` ownership moved here, not left in `ContentManager`

- [ ] **Step 5: Add the fake test source**

Implement a focused in-memory/fake stream source in editor test helpers that:
- records the requested asset path
- returns a prepared stream
- throws clearly when a requested key is missing

- [ ] **Step 6: Run the new tests to verify they pass**

Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ContentManagerStreamSourceTests"
```
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.core/content/IContentStreamSource.cs engine/helengine.core/content/HostFileSystemContentStreamSource.cs engine/helengine.editor.tests/content/ContentManagerStreamSourceTests.cs engine/helengine.editor.tests/testing/FakeContentStreamSource.cs
git commit -m "Add content stream source abstraction"
```

### Task 2: Refactor ContentManager To Depend On The Stream Source

**Files:**
- Modify: `engine/helengine.core/content/ContentManager.cs`
- Modify: `engine/helengine.editor.tests/ContentManagerTests.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write failing tests for source-based content loading**

Add or extend tests to prove:
- `ContentManager` calls the injected stream source
- processor resolution still works by type/extension
- binary serialization tests still load through configured processors

- [ ] **Step 2: Run the focused tests to verify they fail**

Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ContentManagerTests|FullyQualifiedName~BinarySerializationTests"
```
Expected: FAIL because `ContentManager` still expects path ownership.

- [ ] **Step 3: Refactor `ContentManager` constructor and fields**

Change `ContentManager` to:
- remove `RootDirectoryPath`
- store `IContentStreamSource StreamSource`
- validate the source in the constructor

- [ ] **Step 4: Replace direct file opening**

Update `LoadProcessedContent` so it:
- preserves `EngineBinaryReadContext` behavior
- calls `StreamSource.OpenRead(assetPath)`
- does not call `File.OpenRead` or resolve filesystem paths internally

- [ ] **Step 5: Remove obsolete path helpers that no longer belong in `ContentManager`**

Delete or relocate only the helpers that are now dead:
- root-path normalization
- virtual-root path combination
- direct path resolution methods

Do not delete helpers still needed by the host-filesystem implementation until they have been moved there.

- [ ] **Step 6: Run the focused tests to verify they pass**

Run the same test command from step 2.
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.core/content/ContentManager.cs engine/helengine.editor.tests/ContentManagerTests.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "Refactor ContentManager to use stream sources"
```

### Task 3: Break CoreInitializationOptions And Core Over To Source-Based Initialization

**Files:**
- Modify: `engine/helengine.core/CoreInitializationOptions.cs`
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`
- Modify: `engine/helengine.editor.tests/AutomaticPhysicsRuntimePayloadTests.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationExtensionsTests.cs`

- [ ] **Step 1: Write failing initialization tests**

Add tests that prove:
- `Core` throws when `ContentStreamSource` is missing
- `Core` can initialize and create a `SceneManager` when one explicit content source is supplied

- [ ] **Step 2: Run the focused tests to verify they fail**

Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneManagerTests|FullyQualifiedName~AutomaticPhysicsRuntimePayloadTests|FullyQualifiedName~BinarySerializationExtensionsTests"
```
Expected: FAIL because the initialization API still expects `ContentRootPath`.

- [ ] **Step 3: Replace `ContentRootPath`**

Update `CoreInitializationOptions`:
- remove `ContentRootPath`
- add required `IContentStreamSource ContentStreamSource`
- document the breaking change in XML comments

- [ ] **Step 4: Update `Core`**

Change `Core` so it:
- creates its primary `ContentManager` from `InitializationOptions.ContentStreamSource`
- removes root-string-based cache keys
- either caches by source identity or reduces to one primary content manager if no production path needs multi-source runtime caches

- [ ] **Step 5: Update tests and any helper initializers**

Replace all test setup that previously assigned `ContentRootPath` with explicit source construction.

- [ ] **Step 6: Run the focused tests to verify they pass**

Run the same test command from step 2.
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.core/CoreInitializationOptions.cs engine/helengine.core/Core.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs engine/helengine.editor.tests/AutomaticPhysicsRuntimePayloadTests.cs engine/helengine.editor.tests/BinarySerializationExtensionsTests.cs
git commit -m "Replace content root paths with content stream sources"
```

### Task 4: Migrate In-Repo Filesystem Call Sites

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/EditorCliBuildRunner.cs`
- Modify: `engine/helengine.directx11/compilation/DirectX11ShaderSourceCompiler.cs`
- Modify: `engine/helengine.directx11/compilation/DirectX11ShaderAssetBuilder.cs`
- Modify: `engine/helengine.editor/shaders/ShaderModuleManager.cs`
- Modify: `engine/helengine.editor/shaders/EditorShaderPackageExportService.cs`
- Modify: `engine/helengine.editor/shaders/EditorBuiltInShaderAssetLibrary.cs`
- Modify: `engine/helengine.shader/shaders/packages/ShaderModulePackageReader.cs`
- Modify: `engine/helengine.shader/shaders/packages/ShaderModulePackage.cs`
- Modify: `engine/helengine.shader/shaders/compilation/ShaderFilesystemIncludeResolver.cs`
- Modify: `engine/helengine.shader/shaders/compilation/ShaderCompileService.cs`
- Modify: `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogCopySettingsDialogTests.cs`
- Modify: `engine/helengine.editor.tests/AssetPickerModalTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`

- [ ] **Step 1: Write one failing migration test for an editor/runtime initialization path**

Pick one representative test currently assigning `ContentRootPath` and convert it to the new API.

- [ ] **Step 2: Run that test to verify it fails**

Use the narrowest single-test filter possible.

- [ ] **Step 3: Migrate the editor and shader/tooling call sites**

Replace every `new ContentManager(path)` with:
- `new ContentManager(new HostFileSystemContentStreamSource(path))`

Replace every `ContentRootPath = ...` initialization with:
- `ContentStreamSource = new HostFileSystemContentStreamSource(...)`

- [ ] **Step 4: Update the affected tests**

Convert all impacted test initialization/setup to the new explicit source construction.

- [ ] **Step 5: Run the focused editor/tooling tests**

Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~BuildDialogTests|FullyQualifiedName~BuildDialogCopySettingsDialogTests|FullyQualifiedName~AssetPickerModalTests|FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~AssetImportManagerTests"
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor engine/helengine.directx11 engine/helengine.shader engine/helengine.editor.tests
git commit -m "Migrate editor and tooling to content stream sources"
```

### Task 5: Migrate DS To A Packaged Content Stream Source

**Files:**
- Create or Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsPackagedContentStreamSource.hpp`
- Create or Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsPackagedContentStreamSource.cpp`
- Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsBootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsPackagedAssetLoader.*`
- Modify: `C:\dev\helworks\helengine-ds\builder\NintendoDsGeneratedCoreStager.cs`
- Test: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsGeneratedCoreStagerTests.cs`
- Test: `C:\dev\helworks\helengine-ds\builder.tests\NintendoDsBootHostSourceAuditTests.cs`

- [ ] **Step 1: Write the failing DS source-audit tests**

Assert that:
- DS boot no longer writes `ContentRootPath`
- DS runtime uses a packaged stream-source object for generated-core initialization

- [ ] **Step 2: Run the DS source-audit tests to verify they fail**

Run the narrowest DS builder/source-audit tests.

- [ ] **Step 3: Add the DS packaged stream source**

Wrap the existing packaged-loading logic behind the new content-source shape instead of maintaining a separate side path.

- [ ] **Step 4: Update DS boot initialization**

Change DS boot so generated-core receives the packaged content source instead of a root-path string.

- [ ] **Step 5: Update generated-core staging if any generated interop surface changed**

Keep the generated-core staging rewrite logic aligned with the new initialization API.

- [ ] **Step 6: Run the DS tests and one DS build**

Run:
- targeted DS builder/source-audit tests
- one end-to-end DS build command for `city`

Expected: PASS and build completes.

- [ ] **Step 7: Commit**

```bash
git add src/platform/ds builder builder.tests
git commit -m "Migrate DS runtime to packaged content stream sources"
```

### Task 6: Migrate The Remaining Platform Hosts

**Files:**
- Modify: `C:\dev\helworks\helengine-3ds\src\platform\3ds\Nintendo3DsBootHost.cpp`
- Create or Modify packaged source implementation files in `helengine-3ds`
- Modify: `C:\dev\helworks\helengine-ps2\tools\ps2-host-debugger\Ps2HostDebugSession.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\tools\ps2-packaged-scene-probe\ProbeSession.cs`
- Search/Modify the equivalent boot files in:
  - `C:\dev\helworks\helengine-psp`
  - `C:\dev\helworks\helengine-gc`
  - `C:\dev\helworks\helengine-wii`
  - `C:\dev\helworks\helengine-windows`

- [ ] **Step 1: Identify one representative packaged platform and one desktop platform failing at compile time**

Use the first repo that still references `ContentRootPath` after the core change.

- [ ] **Step 2: Add/update the packaged and filesystem source adapters in each repo**

Packaged hosts:
- construct a packaged content stream source
- stop assigning `ContentRootPath`

Desktop hosts:
- construct a filesystem source
- stop assigning `ContentRootPath`

- [ ] **Step 3: Run the smallest host/build verification per repo**

Examples:
- one source-audit or builder test where available
- one representative build or compile command per platform repo touched

- [ ] **Step 4: Commit**

Commit once each platform family is green, or one repo at a time if that is easier to review.

### Task 7: Tighten Feature Ownership And Verify The New Seam

**Files:**
- Modify: `engine/helengine.editor/codegen/features/helengine-feature-catalog.json`
- Modify: `engine/helengine.editor.tests/managers/project/HelengineFeatureCatalogIntegrityTests.cs`
- Modify any DS/packaged-platform source-audit tests that assert generated-core content ownership

- [ ] **Step 1: Write the failing feature-catalog test**

Assert the post-migration ownership you want:
- `host_file_system` remains only on the true filesystem-backed seam
- stale packaged-runtime ownership is absent

- [ ] **Step 2: Run the focused integrity test to verify it fails**

Run:
```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~HelengineFeatureCatalogIntegrityTests"
```

- [ ] **Step 3: Update the feature catalog**

Change ownership only after the runtime seam is fully migrated and verified.

- [ ] **Step 4: Run the focused test and one packaged-platform build**

Expected: PASS and no build regressions.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/codegen/features/helengine-feature-catalog.json engine/helengine.editor.tests/managers/project/HelengineFeatureCatalogIntegrityTests.cs
git commit -m "Tighten host filesystem feature ownership"
```

## Final Verification

- [ ] Run the focused `helengine.editor.tests` suites touched by the migration.
- [ ] Run one end-to-end build for each packaged platform repo touched in this pass.
- [ ] Run one desktop/editor startup path to verify the filesystem source still works.
- [ ] Check `rg -n "ContentRootPath" C:\dev\helworks\helengine C:\dev\helworks\helengine-ds C:\dev\helworks\helengine-3ds C:\dev\helworks\helengine-ps2 C:\dev\helworks\helengine-psp C:\dev\helworks\helengine-gc C:\dev\helworks\helengine-wii C:\dev\helworks\helengine-windows`
  Expected: no live runtime initialization usages remain.
