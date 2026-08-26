# Unified Asset Cook Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Every implementation worker must be `gpt-5.6-luna` with reasoning effort `xhigh`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route Windows and PS2 asset cooking through one deterministic dependency graph and delete duplicated editor cooking implementations.

**Architecture:** A graph service resolves current authored roots, discovers typed dependencies, computes deterministic cook keys, executes focused generic/platform processors into an immutable artifact store, and publishes one manifest consumed by package-layout adapters.

**Tech Stack:** C#/.NET 9, xUnit, existing editor build graph, base-platform builder interfaces, SHA-256 artifact store, current HELE packaged serializers.

**Spec:** `docs/superpowers/specs/2026-08-26-unified-asset-cook-graph-design.md`

## Global Constraints

- Sol coordinates/reviews only; GPT-5.6 Luna `xhigh` performs all implementation edits.
- Stop if Luna `xhigh` cannot be spawned.
- Inputs and outputs use current formats only.
- All roots resolve through `IEditorProjectAuthoringSession` before discovery.
- The editor owns traversal, generic transforms, keys, artifact publication, and manifests.
- Platform builders own target byte formats and target validation.
- No permanent old/new cook feature flag or fallback remains.
- Published artifacts are immutable and content-addressed.
- Windows and PS2 must use the same graph entry point.
- Read the TDD skill and `writing-good-tests.md` before modifying tests.

---

### Task 1: Cook Graph Contracts and Deterministic Node Model

**Files:**
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookGraphRequest.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookNode.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookNodeKey.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookDependency.cs`
- Create: `engine/helengine.editor/managers/project/cooking/CookedAssetArtifact.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookGraph.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorAssetCookGraphContractTests.cs`

**Interfaces:**
- Consumes: canonical authoring references and target/profile metadata.
- Produces: immutable graph/node/artifact value types with ordinal identity.

- [ ] **Step 1: Write failing value-contract tests**

Assert constructor validation, immutable collections, normalized paths, and equality based on exact cook-key text. Require graph enumeration to sort by `EditorAssetCookNodeKey.Value` using `StringComparer.Ordinal` regardless of insertion order.

- [ ] **Step 2: Run tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetCookGraphContractTests" -v:minimal
```

- [ ] **Step 3: Implement focused contracts**

Use explicit classes, not tuples. A node contains:

```csharp
public EditorAssetCookNodeKey Key { get; }
public SceneAssetReference SourceReference { get; }
public AssetEntryKind SourceKind { get; }
public IReadOnlyList<EditorAssetCookDependency> Dependencies { get; }
public string ProcessorId { get; }
```

`CookedAssetArtifact` contains cook key, runtime kind/ID, format version, content hash, byte length, store path, dependency keys, and platform/profile IDs.

- [ ] **Step 4: Run GREEN and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetCookGraphContractTests" -v:minimal
rtk git add -- engine/helengine.editor/managers/project/cooking engine/helengine.editor.tests/managers/project/cooking
rtk git commit -m "Define asset cook graph contracts"
```

### Task 2: Typed Dependency Discovery and Cycle Diagnostics

**Files:**
- Create: `engine/helengine.editor/managers/project/cooking/IEditorAssetDependencyDiscoverer.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetDependencyDiscoveryService.cs`
- Create: `engine/helengine.editor/managers/project/cooking/SceneAssetDependencyDiscoverer.cs`
- Create: `engine/helengine.editor/managers/project/cooking/BlueprintAssetDependencyDiscoverer.cs`
- Create: `engine/helengine.editor/managers/project/cooking/MaterialAssetDependencyDiscoverer.cs`
- Create: `engine/helengine.editor/managers/project/cooking/ModelAssetDependencyDiscoverer.cs`
- Create: focused discoverers for font, animation, shader material, and generated variants
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorAssetDependencyDiscoveryServiceTests.cs`

**Interfaces:**
- Consumes: current assets loaded by canonical references.
- Produces: complete typed DAG or one explicit cycle error.

- [ ] **Step 1: Add failing discovery tests**

Build a scene referencing a blueprint, shared model/material, texture, audio, font, animation, and nested scene. Assert each dependency appears once with the correct kind. Add `A -> B -> C -> A` and require the error to contain the complete normalized chain.

- [ ] **Step 2: Run tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetDependencyDiscoveryServiceTests" -v:minimal
```

- [ ] **Step 3: Implement registry-driven discovery**

Register one discoverer per current asset kind. Each returns typed references only; the service resolves them through the shared authoring session, deduplicates by canonical asset ID/path, and performs depth-first cycle detection with explicit visiting/visited sets.

- [ ] **Step 4: Run GREEN and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetDependencyDiscoveryServiceTests" -v:minimal
rtk git add -- engine/helengine.editor/managers/project/cooking engine/helengine.editor.tests/managers/project/cooking
rtk git commit -m "Discover typed cook dependencies"
```

### Task 3: Cook Keys, Immutable Artifact Store, and Executor

**Files:**
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookKeyBuilder.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookContractVersion.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorCookArtifactStore.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorCookArtifactReceipt.cs`
- Create: `engine/helengine.editor/managers/project/cooking/IEditorAssetCookProcessor.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookGraphExecutor.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorAssetCookKeyBuilderTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorAssetCookGraphExecutorTests.cs`

**Interfaces:**
- Consumes: discovered DAG, normalized settings, platform capability identity.
- Produces: cache hits or immutable validated artifacts.

- [ ] **Step 1: Add failing key tests**

Require path-only moves to keep keys, content/settings changes to alter affected keys, irrelevant settings to leave keys unchanged, platform contract changes to split keys, and processor ID changes to invalidate. Build canonical key input text explicitly and hash it with SHA-256.

- [ ] **Step 2: Add failing artifact/executor tests**

Assert staging then atomic publication, reuse of valid existing receipts, rejection/rebuild of corrupt bytes, one execution for concurrent requests of the same key, dependency-before-dependent execution, deterministic manifest order, and no manifest on required-node failure.

- [ ] **Step 3: Run tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetCookKeyBuilderTests|FullyQualifiedName~EditorAssetCookGraphExecutorTests" -v:minimal
```

- [ ] **Step 4: Implement canonical keys and store**

Key material includes current source content hash, kind, `EditorAssetCookContractVersion.Current`, normalized relevant settings, target contract/profile, dependency keys when embedded, and processor ID. Store objects at `cook/objects/<key>/<runtime-file>` with a receipt containing content hash and length.

- [ ] **Step 5: Implement deterministic executor**

Execute ready independent nodes concurrently with a dictionary of shared tasks keyed by cook key. Sort all published receipts and diagnostics by ordinal key. Cancel dependents on failure and publish no root manifest until all roots succeed.

- [ ] **Step 6: Run GREEN and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetCookKeyBuilderTests|FullyQualifiedName~EditorAssetCookGraphExecutorTests" -v:minimal
rtk git add -- engine/helengine.editor/managers/project/cooking engine/helengine.editor.tests/managers/project/cooking
rtk git commit -m "Execute immutable asset cook graphs"
```

### Task 4: Focused Generic and Platform Leaf Processors

**Files:**
- Create: processors under `engine/helengine.editor/managers/project/cooking/processors/`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: base-platform request/result types only where an asset kind lacks a typed current interface
- Create: processor tests under `engine/helengine.editor.tests/managers/project/cooking/processors/`

**Interfaces:**
- Consumes: current authored asset plus cooked dependency artifacts.
- Produces: one staged current runtime artifact per node.

- [ ] **Step 1: Add contract tests per asset kind**

Cover model, material, texture, audio, font, animation, shader, generated geometry, and generic transformed component records. For each, assert normalized platform request fields, dependency artifact IDs, and current runtime serialization.

- [ ] **Step 2: Run processor tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~CookProcessor" -v:minimal
```

- [ ] **Step 3: Extract one processor at a time**

Implement `IEditorAssetCookProcessor.CanProcess(AssetEntryKind)` and `Cook(EditorAssetCookProcessorContext)`. Move generic logic from `SceneComponentPackagingTransformService`; call current typed platform builder interfaces for target-specific bytes. Delete the corresponding old method immediately after its processor tests pass.

- [ ] **Step 4: Remove duplicated file writes and dependency lookup**

Processors return bytes/metadata to `EditorCookArtifactStore`; they never choose output paths, recursively resolve dependencies, or write directly to the package root.

- [ ] **Step 5: Run existing asset cook regressions and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~EditorPlatformAssetCookService|FullyQualifiedName~CookProcessor" -v:minimal
rtk dotnet test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --no-restore -v:minimal
rtk git add -- engine/helengine.editor/managers/project engine/helengine.baseplatform engine/helengine.editor.tests engine/helengine.baseplatform.tests
rtk git commit -m "Centralize asset cook processors"
```

### Task 5: Current Scene Assembly and Cook Manifest

**Files:**
- Create: `engine/helengine.editor/managers/project/cooking/EditorCookedSceneAssembler.cs`
- Create: `engine/helengine.editor/managers/project/cooking/AssetCookManifest.cs`
- Create: `engine/helengine.editor/managers/project/cooking/AssetCookManifestEntry.cs`
- Create: `engine/helengine.editor/managers/project/cooking/AssetCookManifestWriter.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorCookedSceneAssemblerTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/AssetCookManifestWriterTests.cs`

**Interfaces:**
- Consumes: transformed current scene plus dependency artifacts.
- Produces: current packaged scene and deterministic root manifest.

- [ ] **Step 1: Add failing assembly/manifest tests**

Require all authored references to become runtime artifact identities, no `.hmeta`/authoring hash/source path bytes in packages, current packaged format header, root scene order preservation, ordinal manifest entries, and atomic manifest write.

- [ ] **Step 2: Run tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorCookedSceneAssemblerTests|FullyQualifiedName~AssetCookManifestWriterTests" -v:minimal
```

- [ ] **Step 3: Implement assembly and manifest**

Build the scene only after all dependency artifacts exist. Replace each canonical reference using the graph's exact edge-to-artifact map; fail on missing or extra required mappings. Manifest entries contain root order, node key, artifact path/hash/kind, dependencies, platform, and profile.

- [ ] **Step 4: Run GREEN and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorCookedSceneAssemblerTests|FullyQualifiedName~AssetCookManifestWriterTests" -v:minimal
rtk git add -- engine/helengine.editor/managers/project/cooking engine/helengine.editor.tests/managers/project/cooking
rtk git commit -m "Assemble cooked scenes from graph artifacts"
```

### Task 6: Route Windows and PS2 Through the Graph

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: Windows package-layout adapter or create `EditorWindowsPackageLayoutService.cs`
- Modify: PS2 builder integration under `C:/dev/helworks/helengine-ps2`
- Modify: corresponding editor, Windows builder, and PS2 builder tests
- Delete: obsolete independent cook paths after routing

**Interfaces:**
- Consumes: `AssetCookManifest` from Task 5.
- Produces: platform package layouts with no asset cooking.

- [ ] **Step 1: Add build-route source and behavior tests**

Assert Windows and PS2 construct the same `EditorAssetCookGraphRequest`; package adapters receive only manifest/artifact roots; and `EditorWindowsBuildScenePackager` no longer owns reference resolution, import, or cook methods.

- [ ] **Step 2: Run route tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorWindowsBuildScenePackager" -v:minimal
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore -v:minimal
```

- [ ] **Step 3: Integrate cook phase once**

`EditorPlatformBuildGraphRunner` runs discovery/execution and passes the resulting manifest to the selected platform packaging phase. Windows layout copies/links manifest artifacts; PS2 consumes the same manifest through its current builder request.

- [ ] **Step 4: Delete old cookers**

Remove `EditorWindowsBuildScenePackager` if all layout behavior has moved, otherwise reduce it and rename it to a layout-only service. Delete duplicated `WriteAsset`, import, model/material/texture/audio/font/shader cook methods and old tests asserting those internals.

- [ ] **Step 5: Run both platform suites and commit repos separately**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~Windows" -v:minimal
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore -v:minimal
rtk git add -- engine
rtk git commit -m "Route platform builds through asset cook graph"
rtk git -C C:\dev\helworks\helengine-ps2 add -- builder builder.tests
rtk git -C C:\dev\helworks\helengine-ps2 commit -m "Consume shared asset cook manifests"
```

### Task 7: Demodisc Graph and Cache Verification

**Files:**
- Create: `engine/helengine.editor.tests/DemoDiscCookGraphIntegrationTests.cs`
- Modify: demodisc only if a current authored dependency is missing from explicit references

**Interfaces:**
- Consumes: exact local engine/platform publication and complete graph.
- Produces: end-to-end current Windows and PS2 cook proof.

- [ ] **Step 1: Add integration assertions**

Cook the selected demodisc scene set twice. First run records artifact inventory and hashes. Second run must report all nodes as cache hits and produce the identical manifest. Assert raw authored extensions and `.hmeta` are absent from packaged roots.

- [ ] **Step 2: Run Windows and PS2 integration**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~DemoDiscCookGraphIntegrationTests" -v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform windows -Output C:\dev\helworks\builds\demodisc-cook-graph\windows -Configuration Debug
```

Run the PS2 builder suite and the project build against the same exact project pin:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore -v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform ps2 -Output C:\dev\helworks\builds\demodisc-cook-graph\ps2 -Configuration Debug
```

- [ ] **Step 3: Run full focused suites and source audit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~Cook|FullyQualifiedName~Packaging|FullyQualifiedName~BuildGraph" -v:minimal
rg -n "EditorWindowsBuildScenePackager|WriteAsset\(" engine\helengine.editor\managers\project -g '*.cs'
rtk git diff --check
```

Expected: no independent Windows cooker or duplicate package-root asset writer.

- [ ] **Step 4: Commit final integration**

```powershell
rtk git add -- engine docs scripts
rtk git commit -m "Verify unified asset cooking"
```
