# Local Engine and Platform Publishing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Every implementation worker must be `gpt-5.6-luna` with reasoning effort `xhigh`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish one exact clean source revision, register validated matching platform entries, and optionally update a project's exact engine pin with one idempotent command.

**Architecture:** A small .NET local-publisher tool owns version resolution, staging, validation, manifest updates, and project-file writes. PowerShell is a thin ergonomic wrapper. Exact platform matching remains unchanged.

**Tech Stack:** C#/.NET 9 console tool, `helengine.platforms`, `helengine.projectfile`, `System.Text.Json`, PowerShell, xUnit/script tests, Git CLI.

**Spec:** `docs/superpowers/specs/2026-08-26-local-engine-platform-publishing-design.md`

## Global Constraints

- Sol plans/reviews only; GPT-5.6 Luna `xhigh` performs every implementation edit.
- Stop if Luna `xhigh` cannot be spawned.
- Publish clean Git revisions only.
- Exact version is `<engine-version.json version>+<full lowercase commit>`.
- Do not add floating aliases, nearest-version fallback, or compatibility ranges.
- Installation manifest replacement is atomic and preserves unrelated entries.
- Project pin changes only after normal platform resolution validates every selected entry.
- Repeating an unchanged validated publication produces no payload or manifest churn.
- Read the TDD skill and `writing-good-tests.md` before changing tests.

---

### Task 1: Shared Exact Source Version Resolver

**Files:**
- Create: `engine-version.json`
- Create: `tools/helengine.localpublisher/helengine.localpublisher.csproj`
- Create: `tools/helengine.localpublisher/EngineSourceVersion.cs`
- Create: `tools/helengine.localpublisher/EngineSourceVersionResolver.cs`
- Create: `tools/helengine.localpublisher.tests/helengine.localpublisher.tests.csproj`
- Create: `tools/helengine.localpublisher.tests/EngineSourceVersionResolverTests.cs`

**Interfaces:**
- Consumes: root version document and clean Git checkout.
- Produces: exact base version, commit ID, and combined engine version.

- [ ] **Step 1: Add failing resolver tests with a fake Git process**

```csharp
[Fact]
public void Resolve_CleanCheckout_ReturnsBaseVersionPlusFullCommit() {
    FakeGitCommandRunner git = new FakeGitCommandRunner(
        commit: "fb94b93fbfd8c1e895c910a57903970c0e303900",
        status: string.Empty);
    EngineSourceVersion result = new EngineSourceVersionResolver(git)
        .Resolve(RepositoryRootPath);
    Assert.Equal("1.0.0+fb94b93fbfd8c1e895c910a57903970c0e303900", result.ExactVersion);
}
```

Add dirty checkout, missing file, malformed JSON, invalid semantic base version, and invalid commit-output cases.

- [ ] **Step 2: Run tests and verify RED**

```powershell
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj --filter "FullyQualifiedName~EngineSourceVersionResolverTests" -v:minimal
```

- [ ] **Step 3: Implement the resolver and root document**

Write `engine-version.json` as:

```json
{
  "version": "1.0.0"
}
```

The resolver executes `git rev-parse HEAD` and `git status --porcelain --untracked-files=no`, validates a 40-character lowercase commit, rejects any tracked dirty output, and returns an immutable result.

- [ ] **Step 4: Run GREEN and commit**

```powershell
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj -v:minimal
rtk git add -- engine-version.json tools/helengine.localpublisher tools/helengine.localpublisher.tests
rtk git commit -m "Define exact local engine versions"
```

### Task 2: Atomic Platform Installation Manifest Writer

**Files:**
- Create: `engine/helengine.platforms/PlatformInstallationWriter.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationStore.cs`
- Modify: `engine/helengine.platforms/PlatformInstallationManifest.cs`
- Modify: `engine/helengine.platforms.tests/PlatformInstallationResolverTests.cs`
- Create: `engine/helengine.platforms.tests/PlatformInstallationWriterTests.cs`

**Interfaces:**
- Consumes: validated `PlatformInstallationEntry` records.
- Produces: deterministic atomic upsert keyed by engine version and platform ID.

- [ ] **Step 1: Write failing insert/replace/preserve/order tests**

```csharp
writer.Upsert(new[] { newEntry });
PlatformInstallationManifest saved = store.Load();

Assert.Contains(saved.Platforms, entry =>
    entry.EngineVersion == ExactVersion && entry.PlatformId == "windows");
Assert.Equal(saved.Platforms
    .OrderBy(entry => entry.EngineVersion, StringComparer.Ordinal)
    .ThenBy(entry => entry.PlatformId, StringComparer.Ordinal), saved.Platforms);
```

Add tests for duplicate input keys, invalid paths, atomic failure preserving old bytes, and unrelated revisions surviving replacement.

- [ ] **Step 2: Run platform tests and verify RED**

```powershell
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --no-restore --filter "FullyQualifiedName~PlatformInstallationWriterTests" -v:minimal
```

- [ ] **Step 3: Implement writer validation and atomic save**

Validate nonblank exact version/platform/display name, resolved builder file, player directory, optional generated-core directory/codegen file/plugin manifest, and unique keys. Merge in memory, sort, serialize camel-case JSON, write adjacent temp, then `File.Move(temp, manifest, true)`.

- [ ] **Step 4: Verify resolver round-trip and commit**

```powershell
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --no-restore -v:minimal
rtk git add -- engine/helengine.platforms engine/helengine.platforms.tests
rtk git commit -m "Write platform installations atomically"
```

### Task 3: Staged Local Publisher and Receipt

**Files:**
- Create: `tools/helengine.localpublisher/LocalEnginePublishOptions.cs`
- Create: `tools/helengine.localpublisher/LocalEnginePublicationDocument.cs`
- Create: `tools/helengine.localpublisher/LocalPlatformPublication.cs`
- Create: `tools/helengine.localpublisher/LocalEnginePublisher.cs`
- Create: `tools/helengine.localpublisher/LocalPublicationLock.cs`
- Create: `tools/helengine.localpublisher/PlatformSourceDiscoveryService.cs`
- Create: `tools/helengine.localpublisher.tests/LocalEnginePublisherTests.cs`
- Create: `tools/helengine.localpublisher.tests/PlatformSourceDiscoveryServiceTests.cs`

**Interfaces:**
- Consumes: exact version, selected IDs, current manifest entries as source templates, and build command runner.
- Produces: staged validated engine/platform publication plus receipt.

- [ ] **Step 1: Add failing discovery and publication tests**

Use temporary sibling platform repos with `builder/*.csproj`, player roots, and plugin manifests. Assert selected IDs resolve uniquely, engine/editor publish and builder builds are invoked, invalid builder load prevents manifest mutation, identical valid receipt causes zero build calls, and concurrent callers serialize.

- [ ] **Step 2: Run publisher tests and verify RED**

```powershell
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj --filter "FullyQualifiedName~LocalEnginePublisherTests|FullyQualifiedName~PlatformSourceDiscoveryServiceTests" -v:minimal
```

- [ ] **Step 3: Implement source discovery**

For each selected platform, use the latest existing manifest entry only as a path template. Resolve its player root, find exactly one `builder/*.csproj`, resolve optional plugin/codegen/generated-core paths, and fail on ambiguity. Do not copy its old engine version.

- [ ] **Step 4: Implement staging and validation**

Publish the editor app into `<stage>/engine`, build each builder project into `<stage>/platforms/<id>/builder`, copy its plugin manifest when present, and retain validated player source roots. Load each builder assembly through the same dynamic loader used by the editor and require its platform ID to match.

- [ ] **Step 5: Implement receipt reuse and publication lock**

Hash builder assemblies and plugin manifests into `publication.json`. When an existing successful receipt, paths, and hashes match, return `Unchanged`. Otherwise publish staging to the stable exact-version/configuration directory while holding the OS-handle lock.

- [ ] **Step 6: Run tests and commit**

```powershell
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj -v:minimal
rtk git add -- tools/helengine.localpublisher tools/helengine.localpublisher.tests
rtk git commit -m "Stage validated local engine publications"
```

### Task 4: CLI, PowerShell Wrapper, Manifest Registration, and Project Pin

**Files:**
- Create: `tools/helengine.localpublisher/Program.cs`
- Create: `tools/helengine.localpublisher/LocalEnginePublishResult.cs`
- Create: `scripts/publish-local-engine.ps1`
- Create: `scripts/tests/publish-local-engine.tests.ps1`
- Modify: `tools/helengine.localpublisher/LocalEnginePublisher.cs`
- Modify: `engine/helengine.projectfile/ProjectFileWriter.cs` only if atomic replacement is not already guaranteed

**Interfaces:**
- Consumes: command arguments and Task 3 publication.
- Produces: registered exact entries, optional exact project pin, and final JSON result.

- [ ] **Step 1: Write failing CLI/script behavior tests**

Cover required/default arguments, selected platform list, `-NoBuild`, `-Force`, custom roots, subprocess failure, final JSON shape, and project update ordering. The fake publisher must prove `ProjectFileWriter` is not called before resolver validation.

- [ ] **Step 2: Run tests and verify RED**

```powershell
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj --filter "FullyQualifiedName~Program|FullyQualifiedName~ProjectPin" -v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\publish-local-engine.tests.ps1
```

- [ ] **Step 3: Implement CLI orchestration**

Parse options, run publisher, upsert exact platform entries with `PlatformInstallationWriter`, reload through `AvailablePlatformProviderResolver`, and only then update optional `ProjectFileDocument.RequiredEngineVersion`. Print prose diagnostics followed by:

```json
{"status":"published","engineVersion":"1.0.0+...","publishPath":"...","manifestPath":"...","platforms":["windows"],"projectPath":"...","projectUpdated":true}
```

- [ ] **Step 4: Implement thin PowerShell wrapper**

The script validates only shell-level path/argument shape, runs `dotnet run --project tools/helengine.localpublisher`, forwards the exit code, and performs no independent version or manifest logic.

- [ ] **Step 5: Run tests and commit**

```powershell
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj -v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\publish-local-engine.tests.ps1
rtk git add -- tools/helengine.localpublisher tools/helengine.localpublisher.tests scripts/publish-local-engine.ps1 scripts/tests/publish-local-engine.tests.ps1 engine/helengine.projectfile
rtk git commit -m "Publish and register local engine revisions"
```

### Task 5: Current-Revision Demodisc Build Verification

**Files:**
- Create: `scripts/tests/publish-local-engine-demodisc-smoke.tests.ps1`
- Modify: `README.md`
- Modify: build documentation that currently describes manual platform manifest edits
- Modify: `C:/dev/helprojs/demodisc/project.heproj` through the publisher command

**Interfaces:**
- Consumes: completed publisher and canonical `scripts/build-platform.ps1`.
- Produces: proof that exact current pin cooks without a temporary project rewrite.

- [ ] **Step 1: Add the smoke test**

The test creates a disposable demodisc project copy, publishes the current engine plus Windows, updates the copy's pin, asserts normal platform discovery finds Windows, and invokes the canonical build wrapper with a lightweight output target. It must not create or edit `project.verify.heproj`.

- [ ] **Step 2: Run the smoke test and verify behavior**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\publish-local-engine-demodisc-smoke.tests.ps1
```

Expected: PASS and a current exact pin in the disposable project.

- [ ] **Step 3: Document the one-command workflow**

Document:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-local-engine.ps1 -Platforms windows -Project C:\dev\helprojs\demodisc\project.heproj
```

State that dirty checkouts fail and exact matching is intentional.

- [ ] **Step 4: Run final verification and commit**

```powershell
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --no-restore -v:minimal
rtk dotnet test tools\helengine.localpublisher.tests\helengine.localpublisher.tests.csproj -v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\publish-local-engine.tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\publish-local-engine-demodisc-smoke.tests.ps1
rtk git diff --check
rtk git add -- scripts docs README.md tools engine-version.json
rtk git commit -m "Verify exact local platform publication"
```
