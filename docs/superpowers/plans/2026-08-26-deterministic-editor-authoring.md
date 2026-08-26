# Deterministic Editor Authoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Every implementation worker must be `gpt-5.6-luna` with reasoning effort `xhigh`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide one project-scoped public authoring session whose stable writes, batched identity services, duplicate repair, transactions, and importer access make repeated generation deterministic.

**Architecture:** The editor host creates one concrete `EditorProjectAuthoringSession` per project and exposes `IEditorProjectAuthoringSession` through `IEditorCommandContext`. Save services and project tools reuse it; no caller constructs resolvers, serializers, import managers, or global project paths.

**Tech Stack:** C#/.NET 9, xUnit, existing editor command and import systems, HELE native serializers, SHA-256 cache, filesystem journals.

**Spec:** `docs/superpowers/specs/2026-08-26-deterministic-editor-authoring-design.md`

## Global Constraints

- Sol coordinates/reviews only; all implementation and fixture edits require GPT-5.6 Luna `xhigh` workers.
- Stop rather than implementing with Sol when Luna cannot be spawned.
- All input files are current format; add no compatibility readers.
- Resolve references by asset ID, path, then SHA-256.
- Native identity is embedded; external source identity uses `.hmeta`.
- Existing destination identity wins during ordinary native regeneration.
- Identical output causes no destination write or timestamp change.
- Commands receive authoring through `IEditorCommandContext.Authoring`.
- No project code references `EditorProjectPaths`, `AssetSerializer`, `AssetImportManager`, identity-index types, or `helengine.editor.app` reflection.
- Read the TDD skill and `writing-good-tests.md` before modifying tests.

---

### Task 1: Public Project Authoring Session and Command Context

**Files:**
- Create: `engine/helengine.editor/managers/asset/IEditorProjectAuthoringSession.cs`
- Create: `engine/helengine.editor/managers/asset/EditorProjectAuthoringSession.cs`
- Modify: `engine/helengine.editor/managers/project/IEditorCommandContext.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCommandContext.cs`
- Modify: `engine/helengine.editor/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `helengine.ui/helengine.editor.app/EditorHostImporterFactory.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorProjectAuthoringSessionTests.cs`
- Modify: command-context tests under `engine/helengine.editor.tests`

**Interfaces:**
- Consumes: project root, current importer registrations, content manager, identity services.
- Produces: `IEditorCommandContext.Authoring` and one reusable project session.

- [ ] **Step 1: Add failing command-context and lifetime tests**

Use a fake session and require the context to expose the same instance:

```csharp
[Fact]
public void Authoring_ReturnsHostOwnedProjectSession() {
    FakeEditorProjectAuthoringSession authoring = new FakeEditorProjectAuthoringSession();
    EditorCommandContext context = new EditorCommandContext(ProjectRootPath, ScriptTypeResolver, authoring);
    Assert.Same(authoring, context.Authoring);
}
```

Add a session test proving project/assets roots are canonical and disposal flushes owned disposable services exactly once.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorProjectAuthoringSessionTests|FullyQualifiedName~EditorCommandContext" -v:minimal
```

- [ ] **Step 3: Define the interface and concrete composition root**

The interface contains exactly:

```csharp
public interface IEditorProjectAuthoringSession : IDisposable {
    SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind);
    AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind);
    RuntimeModel LoadImportedRuntimeModel(string relativePath);
    EditorAssetWriteResult WriteAsset(string relativePath, Asset asset);
    EditorAuthoringTransaction BeginTransaction();
    void RefreshExternalChanges();
    EditorAssetRepairReport RepairReport { get; }
}
```

The concrete constructor is host-facing and receives current importer registrations. Add `Authoring` to `IEditorCommandContext` and require it in `EditorCommandContext`.

- [ ] **Step 4: Wire GUI and CLI hosts to one session**

Create one concrete session during project bootstrap, pass it to command contexts, and dispose it with the project/editor session. Do not add a static current session. GUI and CLI use their already configured importer registration list.

- [ ] **Step 5: Run focused tests and editor app build**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorProjectAuthoringSessionTests|FullyQualifiedName~EditorCommand" -v:minimal
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
```

- [ ] **Step 6: Commit Task 1**

```powershell
rtk git add -- engine/helengine.editor/managers/asset engine/helengine.editor/managers/project/IEditorCommandContext.cs engine/helengine.editor/managers/project/EditorCommandContext.cs engine/helengine.editor/EditorCliCommandRunner.cs engine/helengine.editor/EditorSession.cs helengine.ui/helengine.editor.app/EditorHostImporterFactory.cs engine/helengine.editor.tests
rtk git commit -m "Expose project-scoped editor authoring"
```

### Task 2: Batched Identity Index and Hash Cache

**Files:**
- Modify: `engine/helengine.editor/managers/asset/EditorAssetIdentityIndex.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetHashCache.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorProjectAuthoringSession.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EditorAssetIdentityIndexTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EditorAssetHashCacheTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EditorAssetReferenceResolverTests.cs`

**Interfaces:**
- Consumes: one session-owned index/cache/resolver.
- Produces: incremental registration, explicit refresh, and one deferred cache flush.

- [ ] **Step 1: Add instrumentation-based failing tests**

Inject counting filesystem enumeration, hasher, and cache-store fakes. Assert:

```csharp
session.CreateReference("models/a.obj", AssetEntryKind.Model);
session.CreateReference("models/b.obj", AssetEntryKind.Model);

Assert.Equal(1, fileCatalog.EnumerationCount);
Assert.Equal(0, cacheStore.SaveCount);
session.Dispose();
Assert.Equal(1, cacheStore.SaveCount);
```

Add tests proving session writes call `RegisterOrUpdate` without a full refresh and `RefreshExternalChanges` performs one explicit reconciliation.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetIdentityIndexTests|FullyQualifiedName~EditorAssetHashCacheTests|FullyQualifiedName~EditorAssetReferenceResolverTests" -v:minimal
```

- [ ] **Step 3: Add incremental index operations**

Implement focused methods:

```csharp
public void Initialize();
public EditorAssetIdentityEntry RegisterOrUpdate(string fullPath);
public void Remove(string fullPath);
public void ReconcileExternalChanges();
```

`Initialize` is idempotent and enumerates once. Resolver operations require an initialized index and never call `Refresh` implicitly.

- [ ] **Step 4: Defer hash-cache persistence**

`GetContentHash` updates in-memory entries and sets `IsDirty`. Add `Flush()` that atomically writes the sorted document only when dirty. Session commit/disposal calls `Flush()` once.

- [ ] **Step 5: Run GREEN and commit**

Run the Step 2 command. Expected: PASS with exact enumeration/save counts.

```powershell
rtk git add -- engine/helengine.editor/managers/asset engine/helengine.editor.tests/managers/asset
rtk git commit -m "Batch editor identity indexing and hashing"
```

### Task 3: Stable Idempotent Native Writes

**Files:**
- Create: `engine/helengine.editor/managers/asset/EditorAssetWriteDisposition.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetWriteResult.cs`
- Create: `engine/helengine.editor/managers/asset/EditorNativeAssetWriteService.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorProjectAuthoringSession.cs`
- Delete after caller routing: `engine/helengine.editor/managers/asset/GeneratedAssetWriteService.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/GeneratedAssetWriteServiceTests.cs` into `EditorNativeAssetWriteServiceTests.cs`

**Interfaces:**
- Consumes: current native serializer, embedded metadata service, session index/hash cache.
- Produces: `Created`, `Changed`, or `Unchanged` result with preserved destination identity.

- [ ] **Step 1: Write failing stable-write tests**

Cover first creation, overwrite identity preservation, duplicate caller ID rejection, content change, and no-op timestamp preservation:

```csharp
EditorAssetWriteResult first = session.WriteAsset("models/Test.hasset", CreateModel());
DateTime timestamp = File.GetLastWriteTimeUtc(first.FullPath);

EditorAssetWriteResult second = session.WriteAsset("models/Test.hasset", CreateModel());

Assert.Equal(first.AssetId, second.AssetId);
Assert.Equal(EditorAssetWriteDisposition.Unchanged, second.Disposition);
Assert.Equal(timestamp, File.GetLastWriteTimeUtc(second.FullPath));
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorNativeAssetWriteServiceTests" -v:minimal
```

- [ ] **Step 3: Implement canonical write preparation**

For existing current native destinations, load embedded identity and assign it to the incoming asset before serialization. For new destinations, validate a caller ID or mint an unowned GUID. Serialize to bytes, compare with existing bytes, and skip replacement when equal.

Return:

```csharp
return new EditorAssetWriteResult(
    relativePath,
    fullPath,
    asset.AuthoringAssetId,
    contentHash,
    disposition,
    preservedExistingIdentity);
```

- [ ] **Step 4: Add serializer determinism tests**

Build equivalent material/scene objects with reversed dictionary insertion order and require equal `AssetSerializer.SerializeToBytes` output. Sort every unordered writer by ordinal stable key until the tests pass.

- [ ] **Step 5: Run tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorNativeAssetWriteServiceTests|FullyQualifiedName~BinarySerializationTests" -v:minimal
rtk git add -- engine/helengine.editor/managers/asset engine/helengine.files engine/helengine.editor/serialization engine/helengine.editor.tests
rtk git commit -m "Make native asset generation idempotent"
```

### Task 4: Evidence-Based Duplicate Resolution and Repair Reports

**Files:**
- Create: `engine/helengine.editor/managers/asset/EditorAssetRepairKind.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetRepairRecord.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetRepairReport.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetIdentityIndex.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetReferenceResolution.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorProjectAuthoringSession.cs`
- Modify: resolver/index tests

**Interfaces:**
- Consumes: saved ID/path/hash plus current/former identity candidates.
- Produces: deterministic winner and append-only repair report.

- [ ] **Step 1: Add failing tie-break and reporting tests**

Create candidates that distinguish every score tier. Assert exact current ID beats former alias, saved path wins the remaining ID tie, hash wins after path, recorded owner wins after hash, and lexical path is final. Assert every mutation appends a record with old/new IDs and evidence.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetIdentityIndexTests|FullyQualifiedName~EditorAssetReferenceResolverTests" -v:minimal
```

- [ ] **Step 3: Implement one candidate scorer**

Represent the score with a comparable explicit type, not a tuple:

```csharp
sealed class EditorAssetResolutionCandidateScore : IComparable<EditorAssetResolutionCandidateScore> {
    public bool IsCurrentId { get; }
    public bool MatchesSavedPath { get; }
    public bool MatchesSavedHash { get; }
    public bool IsRecordedOwner { get; }
    public string RelativePath { get; }
}
```

Sort descending for Boolean evidence and ordinal ascending for the final path. Keep the outer fallback order ID, then path, then hash.

- [ ] **Step 4: Record and surface repairs**

Append immutable records for metadata creation, saved-ID adoption, duplicate reassignment, path/hash healing, and canonical refresh. CLI command completion prints `RepairReport.CreateSummary()`; GUI routes the same report to its existing output/problem surface.

- [ ] **Step 5: Run tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetIdentityIndexTests|FullyQualifiedName~EditorAssetReferenceResolverTests|FullyQualifiedName~EditorCliCommandRunner" -v:minimal
rtk git add -- engine/helengine.editor/managers/asset engine/helengine.editor/EditorCliCommandRunner.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests
rtk git commit -m "Report deterministic asset repairs"
```

### Task 5: Recoverable Multi-File Authoring Transactions

**Files:**
- Create: `engine/helengine.editor/managers/asset/EditorAuthoringTransaction.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAuthoringTransactionDocument.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAuthoringTransactionEntry.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAuthoringProjectLock.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAuthoringTransactionRecoveryService.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorProjectAuthoringSession.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAuthoringTransactionTests.cs`

**Interfaces:**
- Consumes: stable write preparation from Task 3.
- Produces: one active staged transaction with rollback journal and crash recovery.

- [ ] **Step 1: Add failing commit, rollback, race, and recovery tests**

Use injectable publication hooks to fail after the first replacement. Verify both destinations retain their original bytes. Write a committing journal fixture and verify opening a new session restores backups before indexing.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAuthoringTransactionTests" -v:minimal
```

- [ ] **Step 3: Implement staging and manifest validation**

Stage beneath `cache/editor/authoring-transactions/<id>`. Record normalized destination, staged hash, prior existence/hash, and backup path. `WriteAsset` inside a transaction prepares canonical bytes but does not touch `assets`.

- [ ] **Step 4: Implement locked publication and rollback**

Acquire an OS file-handle lock keyed by canonical project root. Revalidate prior hashes, mark journal `Committing`, back up changed existing files, replace only changed outputs, mark `Committed`, flush index/cache, then remove the transaction directory. On exception, restore backups and delete newly created destinations.

- [ ] **Step 5: Implement startup recovery**

Before session index initialization, scan only the current transaction root. Delete `Staging` journals, roll back `Committing` journals, and delete completed directories. Reject malformed paths instead of broadening cleanup.

- [ ] **Step 6: Run tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAuthoringTransactionTests|FullyQualifiedName~EditorProjectAuthoringSessionTests" -v:minimal
rtk git add -- engine/helengine.editor/managers/asset engine/helengine.editor.tests/managers/asset
rtk git commit -m "Add recoverable authoring transactions"
```

### Task 6: Route Save Services and Project Commands Through the Session

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/blueprint/BlueprintSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorAssetReferenceCanonicalizationService.cs`
- Modify: editor material/generated save services
- Modify: `C:/dev/helprojs/demodisc/assets/codebase/scene.tools/DemoDiscEditorAssetReferenceFactory.cs`
- Modify: `C:/dev/helprojs/demodisc/assets/codebase/scene.tools/GeneratedSceneWriteService.cs`
- Modify: `C:/dev/helprojs/demodisc/assets/codebase/game.tools/ZombislayerAssetPreparationService.cs`
- Modify: every demodisc editor command and factory that authors references/assets
- Delete after caller routing: obsolete demodisc wrappers whose only purpose is hiding low-level editor calls

**Interfaces:**
- Consumes: `IEditorCommandContext.Authoring` and transactions.
- Produces: no independent resolver/writer/importer construction.

- [ ] **Step 1: Add source-contract tests before routing**

Require demodisc production code to contain none of:

```text
Assembly.Load("helengine.editor.app")
EditorHostImporterFactory
new AssetImportManager
AssetSerializer.Serialize
new EditorAssetReferenceResolver
new GeneratedAssetWriteService
EditorProjectPaths
```

Add engine tests proving scene and blueprint saves reuse the injected authoring session.

- [ ] **Step 2: Run tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~BlueprintSaveServiceTests|FullyQualifiedName~Authoring" -v:minimal
rtk dotnet test C:\dev\helprojs\demodisc\city.sln --no-restore --filter "FullyQualifiedName~SourceTests" -v:minimal
```

- [ ] **Step 3: Inject session into engine save paths**

Construct save services with `IEditorProjectAuthoringSession`. Canonicalize references through it and write current native outputs through a single-output transaction. Delete direct resolver/writer construction.

- [ ] **Step 4: Route demodisc commands**

At each `IEditorCommand.Execute`, take `context.Authoring` and pass it explicitly to factories/generators. Use `BeginTransaction()` once per generation command. Replace the Zombislayer reflection/import setup with `context.Authoring.LoadImportedRuntimeModel(...)`.

- [ ] **Step 5: Delete superseded engine and project facades**

After all callers compile, delete `EditorAssetReferenceFactory`, `GeneratedAssetWriteService`, global-path-dependent demodisc helpers, and their tests. Keep no forwarding API.

- [ ] **Step 6: Run tests and commit engine/demodisc separately**

```powershell
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~BlueprintSaveServiceTests|FullyQualifiedName~EditorCommand" -v:minimal
rtk dotnet test C:\dev\helprojs\demodisc\city.sln --no-restore -v:minimal
rtk git add -- engine helengine.ui
rtk git commit -m "Route editor saves through project authoring"
rtk git -C C:\dev\helprojs\demodisc add -- assets/codebase
rtk git -C C:\dev\helprojs\demodisc commit -m "Use public project authoring API"
```

### Task 7: Demodisc Determinism and End-to-End Recovery

**Files:**
- Create: `engine/helengine.editor.tests/DemoDiscAuthoringDeterminismTests.cs`
- Modify: demodisc generated current native assets
- Modify: existing generation source-contract tests as required by the public API

**Interfaces:**
- Consumes: all authoring contracts from Tasks 1–6.
- Produces: end-to-end proof of no-op generation and deterministic healing.

- [ ] **Step 1: Add the end-to-end harness**

Copy the minimal generated project fixture to a temp root, execute its editor commands twice, hash every authored file after each pass, and assert identical path/hash maps plus zero second-pass `Changed` results. Add move, deleted-metadata, and duplicated-ID scenarios and assert repair records and canonical winners.

- [ ] **Step 2: Run the harness and verify RED before final fixes**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~DemoDiscAuthoringDeterminismTests" -v:minimal
```

- [ ] **Step 3: Remove remaining nondeterminism only at its owner**

Fix unordered serializer output, random payload fields, repeated index refreshes, or unconditional writes in engine services. Do not normalize the fixture after the fact.

- [ ] **Step 4: Run full verification**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore -v:minimal
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
rtk dotnet test C:\dev\helprojs\demodisc\city.sln --no-restore -v:minimal
rtk git diff --check
```

- [ ] **Step 5: Commit Task 7**

```powershell
rtk git add -- engine/helengine.editor.tests engine/helengine.editor engine/helengine.files
rtk git commit -m "Verify deterministic project authoring"
rtk git -C C:\dev\helprojs\demodisc add -- assets
rtk git -C C:\dev\helprojs\demodisc commit -m "Regenerate deterministic authored assets"
```
