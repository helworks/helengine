# Direct-Source Platform Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace GUID-scoped copied-project builds with serialized direct-source builds, deterministic reusable caches, direct output state, safe cache maintenance, and waiter validation that cannot accept stale artifacts.

**Architecture:** `scripts/build-platform.ps1` remains the orchestration entry point and delegates cache, lock, and state mechanics to three focused PowerShell modules. The wrapper opts the editor into stable-cache mode through `HELENGINE_BUILD_CACHE_ROOT`, `HELENGINE_BUILD_CONFIGURATION`, and `HELENGINE_BUILD_PROFILE`; without those variables, interactive editor workflows retain their current invocation isolation. The output state file and existing artifact checks jointly define successful completion.

**Tech Stack:** PowerShell 5.1, .NET 9/C#, xUnit, `System.IO.FileStream` sharing semantics, `System.Text.Json`, existing fake-native-process test harnesses.

---

## Scope and file map

This is one integrated plan because the wrapper, editor path resolver, and build waiter are three ends of one cache/completion contract. Do not split or ship only one end.

Primary files:

- Modify: `scripts/build-platform.ps1`
- Create: `scripts/build-platform/BuildPlatformCache.psm1`
- Create: `scripts/build-platform/BuildPlatformLock.psm1`
- Create: `scripts/build-platform/BuildPlatformState.psm1`
- Replace: `scripts/tests/build-platform-workspace.tests.ps1`
- Modify: `scripts/tests/build-platform-profile-behavior.tests.ps1`
- Modify: `scripts/tests/build-platform-streaming.tests.ps1`
- Create: `scripts/tests/build-platform-locking.tests.ps1`
- Create: `scripts/tests/build-platform-maintenance.tests.ps1`
- Modify: `engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspace.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspaceFactory.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphWorkspaceFactoryTests.cs`
- Create: `engine/helengine.editor.tests/testing/EditorBuildCacheEnvironmentCollection.cs`
- Create: `tools/build-waiter/BuildStateDocument.cs`
- Create: `tools/build-waiter/BuildStateVerificationResult.cs`
- Create: `tools/build-waiter/BuildStateVerifier.cs`
- Modify: `tools/build-waiter/BuildWaiter.cs`
- Modify: `tools/build-waiter/Program.cs`
- Create: `tools/build-waiter.tests/BuildStateVerifierTests.cs`
- Modify: `tools/build-waiter.tests/BuildWaiterTests.cs`
- Modify: `tools/build-waiter.tests/ProgramTests.cs`
- Modify: `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs`
- Create: `scripts/tests/build-platform-real-editor-smoke.tests.ps1`
- Create: `scripts/tests/fixtures/build-platform-smoke-builder/helengine.buildplatform.smokebuilder.csproj`
- Create: `scripts/tests/fixtures/build-platform-smoke-builder/SmokePlatformBuilder.cs`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/project.heproj`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/settings/platforms.json`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/user_settings/build_config.json`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/assets/scenes/SmokeScene.helen`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/assets/codebase/smoke/code.module.json`
- Modify: `README.md`

Historical design and plan documents are records and must not be rewritten. Preserve the user's existing unstaged change in `engine/helengine.editor.tests/ModelTessellationProcessorTests.cs`; every `git add` below names only task files.

## Task 1: Prove and implement the direct-source stable-cache wrapper contract

**Files:**

- Replace: `scripts/tests/build-platform-workspace.tests.ps1`
- Modify: `scripts/tests/build-platform-profile-behavior.tests.ps1`
- Modify: `scripts/tests/build-platform-streaming.tests.ps1`
- Create: `scripts/build-platform/BuildPlatformCache.psm1`
- Modify: `scripts/build-platform.ps1`

- [ ] **Step 1: Replace token assertions with a failing behavior harness**

Turn `build-platform-workspace.tests.ps1` into a disposable-project test that installs fake `dotnet.cmd` and `robocopy.cmd` executables. The fake `dotnet` must record every argument line, create `helengine.editor.app.dll` for publish calls, and return success. The fake `robocopy` must write a marker and fail.

Invoke the wrapper twice with the same project, platform, configuration, profile, cache root, and output. Assert:

```powershell
$CanonicalProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$EditorInvocation = Get-Content -LiteralPath $CapturePath |
    Where-Object { $_ -match '--build windows' } |
    Select-Object -Last 1

if ($EditorInvocation -notmatch [regex]::Escape('--project ' + $CanonicalProjectPath)) {
    throw "The wrapper did not pass the authored project directly: '$EditorInvocation'."
}
if (Test-Path -LiteralPath $RobocopyMarkerPath) {
    throw "The wrapper invoked robocopy."
}
if ((Get-ChildItem -LiteralPath $CacheRootPath -Recurse -Directory |
        Where-Object { $_.Name -match '^[0-9a-f]{32}$' -and $_.Parent.Name -ne 'projects' }).Count -ne 0) {
    throw "The cache contains a GUID-like invocation directory."
}
```

Also parse the two recorded `--artifacts-path` and publish `-o` values and assert they are identical. Assert the editor invocation contains the exact canonical `-Output` value.

Run compatibility cases in the same harness: `-WorkspaceRoot` alone resolves the same cache layout and emits one deprecation warning; equal `-CacheRoot`/`-WorkspaceRoot` values succeed; differing values exit `2` before fake `dotnet` is reached. Assert the normal invocation prints the authored project, lock identity, editor cache, platform cache, output, and state-file paths.

- [ ] **Step 2: Run the test and confirm the old implementation fails for the intended reason**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-workspace.tests.ps1
```

Expected: non-zero because the current wrapper calls `robocopy` and passes a copied project path.

- [ ] **Step 3: Add the cache-layout module**

Export these functions from `BuildPlatformCache.psm1`:

```powershell
Export-ModuleMember -Function @(
    'Get-BuildPlatformProjectHash',
    'Get-BuildPlatformSafeSegment',
    'Resolve-BuildPlatformCacheLayout',
    'Write-BuildPlatformCacheMetadata'
)
```

`Resolve-BuildPlatformCacheLayout` must return exact canonical paths with this shape:

```powershell
[pscustomobject]@{
    CacheRootPath = $FullCacheRootPath
    ProjectHash = $ProjectHash
    ProjectCacheRootPath = Join-Path $FullCacheRootPath ("projects\" + $ProjectHash)
    LockPath = Join-Path $FullCacheRootPath ("locks\" + $ProjectHash + ".lock")
    EditorArtifactsPath = Join-Path $ProjectCacheRootPath ("editor\" + $ConfigurationSegment + "\artifacts")
    EditorPublishPath = Join-Path $ProjectCacheRootPath ("editor\" + $ConfigurationSegment + "\publish")
    PlatformCacheRootPath = Join-Path $ProjectCacheRootPath ("platforms\" + $PlatformSegment + "\" + $ConfigurationSegment + "\" + $ProfileSegment)
    MetadataPath = Join-Path $ProjectCacheRootPath 'cache-metadata.json'
}
```

Use the same first 16 SHA-256 bytes as the existing wrapper and editor resolver. Metadata must contain `projectRootPath` and `lastUsedUtc`.

- [ ] **Step 4: Rework the wrapper around direct-source paths**

Add parameters:

```powershell
[string]$CacheRoot = "",
[string]$WorkspaceRoot = "",
[TimeSpan]$LockTimeout = [TimeSpan]::FromHours(2),
[switch]$Clean,
[int]$PruneCacheOlderThanDays = 0
```

Resolve the root as follows:

```powershell
if (-not [string]::IsNullOrWhiteSpace($CacheRoot) -and
    -not [string]::IsNullOrWhiteSpace($WorkspaceRoot) -and
    [System.IO.Path]::GetFullPath($CacheRoot) -ne [System.IO.Path]::GetFullPath($WorkspaceRoot)) {
    [Console]::Error.WriteLine("CacheRoot and deprecated WorkspaceRoot must resolve to the same path when both are supplied.")
    exit 2
}

$SelectedCacheRoot = if (-not [string]::IsNullOrWhiteSpace($CacheRoot)) {
    $CacheRoot
} elseif (-not [string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    Write-Warning "WorkspaceRoot is deprecated; use CacheRoot."
    $WorkspaceRoot
} else {
    'C:\dev\helworks\builds\helengine\cache'
}
```

Delete `Copy-ProjectIntoIsolatedWorkspace`, all `robocopy` logic, `$BuildExecutionId`, `$BuildInvocationRootPath`, `$IsolatedProjectRootPath`, and `$IsolatedProjectPath`. Pass `$ResolvedProjectPath` directly in `$EditorRunArguments`.

Use the cache layout's stable editor artifacts and publish paths. Before invoking the editor, save and then set:

```powershell
$env:HELENGINE_BUILD_CACHE_ROOT = $Layout.CacheRootPath
$env:HELENGINE_BUILD_CONFIGURATION = $Configuration.ToLowerInvariant()
$env:HELENGINE_BUILD_PROFILE = $ResolvedBuildProfile
```

Stop setting `HELENGINE_BUILD_WORKSPACE_ROOT`. Restore all inherited `HELENGINE_BUILD_*` and `HELENGINE_SOURCE_ROOT` values from the outermost `finally` block on success and failure.

- [ ] **Step 5: Update the streaming/profile tests to assert behavior that remains contractual**

Remove `Copy-ProjectIntoIsolatedWorkspace`, GUID, isolated-project, artifact-pattern, and archive-extension tokens from `build-platform-streaming.tests.ps1`. Keep live stdout/stderr forwarding checks and the prohibition on project-specific editor commands. Extend `build-platform-profile-behavior.tests.ps1` to pass an explicit disposable `-CacheRoot` and assert the resolved profile reaches both `--build-profile` and `HELENGINE_BUILD_PROFILE` in the fake editor process.

- [ ] **Step 6: Run focused wrapper tests**

Run each command separately:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-profile.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-profile-behavior.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-streaming.tests.ps1
```

Expected: each prints its `*_TEST_PASS` marker; the cache test confirms no project copy and stable editor paths.

- [ ] **Step 7: Commit the direct-source wrapper slice**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformCache.psm1 scripts/tests/build-platform-workspace.tests.ps1 scripts/tests/build-platform-profile.tests.ps1 scripts/tests/build-platform-profile-behavior.tests.ps1 scripts/tests/build-platform-streaming.tests.ps1
rtk git commit -m "Rework platform wrapper for direct-source caches"
```

## Task 2: Add deterministic editor cache mode without regressing interactive isolation

**Files:**

- Modify: `engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspace.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspaceFactory.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphWorkspaceFactoryTests.cs`
- Create: `engine/helengine.editor.tests/testing/EditorBuildCacheEnvironmentCollection.cs`

- [ ] **Step 1: Add failing stable-mode and fallback-mode tests**

Create a non-parallel xUnit collection because these tests modify process environment:

```csharp
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EditorBuildCacheEnvironmentCollection {
    public const string Name = "Editor build cache environment";
}
```

Annotate both test classes with `[Collection(EditorBuildCacheEnvironmentCollection.Name)]`. Save and restore all three cache environment variables in `IDisposable` cleanup.

Add tests proving:

- two different execution ids resolve the same `generated-dotnet` path when stable mode is set;
- stable paths end in `projects/<hash>/platforms/ps2/debug/profiler/generated-dotnet`;
- repeated factory calls resolve the same `build-graph`, `generated-core`, and `native` roots in stable mode;
- `native` is outside `build-graph`;
- with stable mode absent, existing repeated factory calls remain distinct.

- [ ] **Step 2: Run focused editor tests and confirm the stable assertions fail**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildIsolationPathResolverTests|FullyQualifiedName~EditorPlatformBuildGraphWorkspaceFactoryTests"
```

Expected: new stable-cache assertions fail because GUIDs are still appended.

- [ ] **Step 3: Implement a stable-cache branch in the resolver**

Add constants for `HELENGINE_BUILD_CACHE_ROOT`, `HELENGINE_BUILD_CONFIGURATION`, and `HELENGINE_BUILD_PROFILE`. Stable mode is active only when `HELENGINE_BUILD_CACHE_ROOT` is non-empty; configuration and profile are mandatory in that mode.

The central path calculation must be equivalent to:

```csharp
return Path.Combine(
    Path.GetFullPath(cacheRootPath),
    "projects",
    ProjectHashSegment,
    "platforms",
    SanitizePathSegment(platformId),
    SanitizePathSegment(configuration),
    SanitizePathSegment(buildProfile));
```

In stable mode:

- `ResolveGeneratedCodeOutputRootPath` returns `<profile>/generated-dotnet` and ignores execution id;
- `ResolveGeneratedCodeWorkspaceRootPath` returns `<profile>/generated-dotnet/workspace`;
- workspace execution root returns `<profile>/build-graph` and ignores queue/execution ids;
- expose internal resolver methods for `<profile>/generated-core` and `<profile>/native`.

When stable mode is absent, preserve every current temporary/configured-workspace path exactly, including deprecated `HELENGINE_BUILD_WORKSPACE_ROOT` behavior.

- [ ] **Step 4: Separate persistent native state from resettable graph state**

Add a three-root constructor while preserving the one-root constructor:

```csharp
public EditorPlatformBuildGraphWorkspace(
    string executionRootPath,
    string generatedCoreRootPath,
    string builderWorkingRootPath) {
    ExecutionRootPath = Path.GetFullPath(executionRootPath);
    GeneratedCoreRootPath = Path.GetFullPath(generatedCoreRootPath);
    BuilderWorkingRootPath = Path.GetFullPath(builderWorkingRootPath);
    CookRootPath = Path.Combine(ExecutionRootPath, "cooked");
    CodeRootPath = Path.Combine(ExecutionRootPath, "code");
    VariantRootPath = Path.Combine(ExecutionRootPath, "variants");
    LayoutRootPath = Path.Combine(ExecutionRootPath, "layout");
    PackageRootPath = Path.Combine(ExecutionRootPath, "package");
    LogsRootPath = Path.Combine(ExecutionRootPath, "logs");
}
```

The factory uses this constructor only in stable mode. Update `ResetExecutionDirectories` so it deletes the resettable `build-graph` root and the generated-core root, recreates both, and does not delete an external stable `native` root. In interactive mode, native remains nested beneath the unique execution root and therefore retains current cleanup behavior.

- [ ] **Step 5: Run focused and adjacent graph tests**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildIsolationPathResolverTests|FullyQualifiedName~EditorPlatformBuildGraphWorkspaceFactoryTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests"
```

Expected: all selected tests pass; stable mode contains no GUID segment, native survives stable graph reset, and interactive paths stay unique.

- [ ] **Step 6: Commit the editor stable-cache slice**

```powershell
rtk git add -- engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspace.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphWorkspaceFactory.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphWorkspaceFactoryTests.cs engine/helengine.editor.tests/testing/EditorBuildCacheEnvironmentCollection.cs
rtk git commit -m "Add deterministic headless editor build caches"
```

## Task 3: Serialize source-mutating builds with an OS-backed project lock

**Files:**

- Create: `scripts/build-platform/BuildPlatformLock.psm1`
- Create: `scripts/tests/build-platform-locking.tests.ps1`
- Modify: `scripts/build-platform.ps1`

- [ ] **Step 1: Write cross-process failing tests**

The test launches wrappers through `System.Diagnostics.Process`, using a fake editor process that blocks on a release marker. Cover five cases:

1. same project: the second wrapper does not reach fake `dotnet` until the first releases;
2. different project: the second wrapper reaches fake `dotnet` while the first remains blocked;
3. same project: the waiting wrapper proceeds after release;
4. timeout: `-LockTimeout 00:00:00.5` exits non-zero and mentions the active owner;
5. crash: terminate the owner process, then prove a new wrapper acquires the lock.

Use per-process marker paths in environment variables and cap each test process wait at 20 seconds so a regression cannot hang the suite.

- [ ] **Step 2: Run the locking test and confirm same-project builds overlap today**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-locking.tests.ps1
```

Expected: non-zero because the second same-project build reaches fake `dotnet` before release.

- [ ] **Step 3: Implement the lock module**

`Enter-BuildPlatformProjectLock` must create the lock directory and repeatedly attempt:

```powershell
$Stream = [System.IO.File]::Open(
    $LockPath,
    [System.IO.FileMode]::OpenOrCreate,
    [System.IO.FileAccess]::ReadWrite,
    [System.IO.FileShare]::Read)
```

Once held, truncate and write UTF-8 JSON metadata containing process id, canonical project, platform, profile, output, and `startedUtc`, then flush without closing the stream. On `IOException`, read metadata with a separate read handle, print bounded status no more often than every five seconds, and retry until `LockTimeout`. A leftover readable file is never treated as ownership.

Each wait status line must include the active owner metadata when readable plus elapsed wait time. The timeout error must include the canonical project and lock path so callers can identify the blocked build.

Export `Enter-BuildPlatformProjectLock`, `Test-BuildPlatformProjectLockHeld`, and `Exit-BuildPlatformProjectLock`. `Exit` only disposes the owner stream; it does not depend on deleting the metadata file.

- [ ] **Step 4: Hold the lock across every mutating/build stage**

Acquire after canonical path/layout resolution and before cleanup, restore, publish, prebuild, cook, or package. Store the returned handle and release it from the wrapper's outermost `finally`:

```powershell
$ProjectLock = $null
try {
    $ProjectLock = Enter-BuildPlatformProjectLock -LockPath $Layout.LockPath -Metadata $LockMetadata -Timeout $LockTimeout
    # maintenance, state, restore, publish, and editor invocation
} finally {
    if ($null -ne $ProjectLock) {
        Exit-BuildPlatformProjectLock -LockHandle $ProjectLock
    }
}
```

- [ ] **Step 5: Run the cross-process locking suite**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-locking.tests.ps1
```

Expected: `LOCKING_TEST_PASS`; same-project builds serialize, different projects overlap, timeout is clear, and a killed owner leaves no permanent lock.

- [ ] **Step 6: Commit locking**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformLock.psm1 scripts/tests/build-platform-locking.tests.ps1
rtk git commit -m "Serialize direct-source project builds"
```

## Task 4: Record direct-output build state on every terminal path

**Files:**

- Create: `scripts/build-platform/BuildPlatformState.psm1`
- Modify: `scripts/tests/build-platform-workspace.tests.ps1`
- Modify: `scripts/build-platform.ps1`

- [ ] **Step 1: Add failing success/failure state assertions**

Extend the wrapper behavior test with a fake-dotnet failure mode. In that mode, the fake editor writes one mutation beneath the authored project and one partial-output sentinel before returning non-zero. Assert success writes `.helengine-build-state.json` with `status == 'succeeded'` and failure writes `status == 'failed'`; after failure, both the authored mutation and partial output remain, as do stable cache sentinels. Assert `startedUtc`, `completedUtc`, `buildId`, canonical project path, platform, profile, configuration, and exit code fields.

- [ ] **Step 2: Run and confirm the state file is currently absent**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-workspace.tests.ps1
```

Expected: non-zero with a missing `.helengine-build-state.json` assertion.

- [ ] **Step 3: Implement the state writer and a single terminal funnel**

Export `Write-BuildPlatformState`. Emit exactly this schema using two writes per normal build—one `running`, one terminal:

```json
{
  "buildId": "diagnostic-guid-only",
  "projectPath": "C:\\absolute\\project.heproj",
  "platform": "ps2",
  "buildProfile": "profiler",
  "configuration": "Debug",
  "startedUtc": "2026-08-15T12:00:00.0000000Z",
  "completedUtc": null,
  "status": "running",
  "exitCode": null
}
```

Create `-Output`, write `running` only after the lock is held, and write `succeeded` or `failed` before releasing the lock. Preserve child exit codes. The catch path may use wrapper code `10` only for exceptions that do not already carry a native process code. Do not stage or replace the output tree.

- [ ] **Step 4: Run direct-source state tests**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-profile-behavior.tests.ps1
```

Expected: success and failure states are accurate; authored project mutations, partial output, and caches remain after failure.

- [ ] **Step 5: Commit state handling**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformState.psm1 scripts/tests/build-platform-workspace.tests.ps1 scripts/tests/build-platform-profile-behavior.tests.ps1
rtk git commit -m "Record direct-output platform build state"
```

## Task 5: Require current successful state in build-waiter

**Files:**

- Create: `tools/build-waiter/BuildStateDocument.cs`
- Create: `tools/build-waiter/BuildStateVerificationResult.cs`
- Create: `tools/build-waiter/BuildStateVerifier.cs`
- Modify: `tools/build-waiter/BuildWaiter.cs`
- Modify: `tools/build-waiter/Program.cs`
- Create: `tools/build-waiter.tests/BuildStateVerifierTests.cs`
- Modify: `tools/build-waiter.tests/BuildWaiterTests.cs`
- Modify: `tools/build-waiter.tests/ProgramTests.cs`

- [ ] **Step 1: Add failing state-verifier tests**

Test valid current success plus these failures independently: missing file, malformed JSON, `running`, `failed`, missing build id, missing completion time, completion before start, and state start before the waiter invocation.

The public verifier contract is:

```csharp
public BuildStateVerificationResult Verify(string outputRootPath, DateTime waiterStartedUtc);
```

- [ ] **Step 2: Update waiter integration tests first**

Change the successful child command to create both a non-empty artifact and a valid current state file. Add a regression where the child exits zero and leaves a fresh artifact but writes `failed`; the result must fail with a state-related message.

- [ ] **Step 3: Run tests and confirm compilation/behavior fails**

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore --filter "FullyQualifiedName~BuildStateVerifierTests|FullyQualifiedName~BuildWaiterTests|FullyQualifiedName~ProgramTests"
```

Expected: compilation fails until the new verifier types exist, then the fresh-artifact/failed-state regression remains red until waiter wiring is complete.

- [ ] **Step 4: Implement strict JSON state verification**

`BuildStateDocument` is one class in its own file with properties matching the wrapper JSON. Deserialize with `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`. Success requires:

```csharp
string.Equals(document.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(document.BuildId)
    && document.StartedUtc >= waiterStartedUtc
    && document.CompletedUtc.HasValue
    && document.CompletedUtc.Value >= document.StartedUtc
    && document.ExitCode == 0
```

Missing, unreadable, malformed, or invalid state returns a descriptive failed result; it does not throw through normal verification.

- [ ] **Step 5: Wire state before artifact acceptance**

Give `BuildWaiter` a `BuildStateVerifier` dependency. After a zero child exit, verify state first and artifact freshness second. Update all constructor calls:

```csharp
new BuildWaiter(new BuildArtifactVerifier(), new BuildStateVerifier())
```

Do not change the existing rule that a non-zero child exit is returned immediately.

- [ ] **Step 6: Run the complete waiter suite**

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore
```

Expected: all waiter tests pass; a fresh artifact cannot override missing, stale, running, failed, or malformed state.

- [ ] **Step 7: Commit waiter state verification**

```powershell
rtk git add -- tools/build-waiter tools/build-waiter.tests
rtk git commit -m "Require current successful build state"
```

## Task 6: Add explicit path-safe clean and prune maintenance

**Files:**

- Modify: `scripts/build-platform/BuildPlatformCache.psm1`
- Modify: `scripts/build-platform/BuildPlatformLock.psm1`
- Create: `scripts/tests/build-platform-maintenance.tests.ps1`
- Modify: `scripts/build-platform.ps1`

- [ ] **Step 1: Write failing maintenance tests around disposable trees**

Create two project hashes, two platforms, two configurations, and two profiles. Place sentinels in every cache slice, the authored project, and output. Verify:

- `-Clean` removes only selected editor-configuration and platform/configuration/profile slices;
- prune removes only metadata-expired project caches;
- fresh metadata survives;
- a separately held project lock causes prune to skip that project;
- malformed metadata fails closed;
- a candidate reparse point fails closed;
- `..`/outside-root candidates are rejected;
- source and output sentinels always survive.

- [ ] **Step 2: Run and confirm maintenance parameters do not work yet**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-maintenance.tests.ps1
```

Expected: non-zero because clean/prune helpers are not implemented.

- [ ] **Step 3: Implement one fail-closed descendant guard**

Every deletion must pass a shared guard equivalent to:

```powershell
$FullRoot = [System.IO.Path]::GetFullPath($AllowedRootPath).TrimEnd('\') + '\'
$FullTarget = [System.IO.Path]::GetFullPath($TargetPath)
if (-not $FullTarget.StartsWith($FullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Cleanup target '$FullTarget' escapes '$AllowedRootPath'."
}
$Item = Get-Item -LiteralPath $FullTarget -Force
if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Cleanup target '$FullTarget' is a reparse point."
}
```

Also reject deleting the allowed root itself and require the expected 32-lowercase-hex project directory name plus matching valid metadata.

- [ ] **Step 4: Implement selected clean and age-based prune**

Export `Remove-BuildPlatformSelectedCache` and `Remove-BuildPlatformExpiredProjectCaches`. Clean removes only:

```text
<project>/editor/<configuration>
<project>/platforms/<platform>/<configuration>/<profile>
```

Prune enumerates direct children only under `<CacheRoot>/projects`, reads `lastUsedUtc`, checks age, and uses `Test-BuildPlatformProjectLockHeld` before removal. `PruneCacheOlderThanDays` must be zero (disabled) or positive; negative values are argument errors.

- [ ] **Step 5: Invoke maintenance under the current project lock**

Run maintenance after acquiring the current project lock and before writing `running`. Update metadata after maintenance and again at terminal completion. Ordinary builds perform no cache deletion.

- [ ] **Step 6: Run maintenance and wrapper regressions**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-maintenance.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-workspace.tests.ps1
```

Expected: all pass and no deletion reaches source, output, cache root, or an unrelated cache slice.

- [ ] **Step 7: Commit maintenance**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformCache.psm1 scripts/build-platform/BuildPlatformLock.psm1 scripts/tests/build-platform-maintenance.tests.ps1
rtk git commit -m "Add safe platform cache maintenance"
```

## Task 7: Document the new contract and add an end-to-end smoke

**Files:**

- Modify: `README.md`
- Modify: `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs`
- Create: `scripts/tests/build-platform-real-editor-smoke.tests.ps1`
- Create: `scripts/tests/fixtures/build-platform-smoke-builder/helengine.buildplatform.smokebuilder.csproj`
- Create: `scripts/tests/fixtures/build-platform-smoke-builder/SmokePlatformBuilder.cs`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/project.heproj`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/settings/platforms.json`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/user_settings/build_config.json`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/assets/scenes/SmokeScene.helen`
- Create: `scripts/tests/fixtures/build-platform-smoke-project/assets/codebase/smoke/code.module.json`

- [ ] **Step 1: Add an isolated platform-manifest override for tests and CI**

Add `HELENGINE_ENGINE_USER_SETTINGS_ROOT` handling to `ResolveSharedEngineUserSettingsRootPath`. A non-empty value returns its canonical path; when absent, retain the current shared-source-root behavior. Add tests that save/restore the variable and prove both branches. This lets the smoke provide its own `platforms.json` without reading or modifying `helengine/user_settings/platforms.json`.

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorSourceBuildWorkspaceLocatorTests
```

Expected: the override and existing fallback tests pass.

- [ ] **Step 2: Add the minimal smoke builder and authored fixture**

The builder project references `engine/helengine.baseplatform/helengine.baseplatform.csproj`. `SmokePlatformBuilder` must be a public sealed `IPlatformAssetBuilder`, expose one minimal profile/target/cook definition, and write `smoke-build.txt` directly beneath `request.OutputRoot` from `BuildAsync` before returning a successful `PlatformBuildReport`. Keep one class per C# file and XML-document every member.

The five named smoke-project fixture files define engine version `1.0.0-smoke`, supported platform `smoke`, selected scene `SmokeScene`, profile `release`, one runtime code module with no source files, and no prebuild commands. The smoke script creates a disposable `engine-user-settings/platforms.json` whose `builderAssemblyPath` points to the built smoke builder and whose `codegenToolPath` points to `C:\dev\helworks\csharpcodegen\codegen\bin\Release\net9.0\codegen.exe`.

- [ ] **Step 3: Write the real-editor wrapper smoke**

The smoke script copies only the five tiny immutable project fixture files to a disposable test directory, builds `C:\dev\helworks\csharpcodegen\codegen\codegen.csproj` in Release and the smoke builder in Debug, creates the disposable platform manifest, sets `HELENGINE_ENGINE_USER_SETTINGS_ROOT` for the child wrapper, then invokes the real `build-platform.ps1` twice with the same disposable cache and output roots. It restores the inherited environment in `finally` and never edits shared engine user settings. Assert:

- both invocations exit zero;
- the editor receives and builds the disposable authored project directly;
- `smoke-build.txt` and a current succeeded state exist;
- editor publish, generated-dotnet, and native paths are identical across runs;
- no GUID directory appears below the stable cache;
- the fixture's authored-source sentinel remains in the direct project tree.

- [ ] **Step 4: Run the smoke**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-real-editor-smoke.tests.ps1
```

Expected: `REAL_EDITOR_SMOKE_TEST_PASS`. This is the only test in the plan that restores/publishes the real editor.

- [ ] **Step 5: Update README contract text**

Replace the copied-project paragraph with direct-source behavior. Document `-BuildProfile`, `-CacheRoot`, deprecated `-WorkspaceRoot`, `-LockTimeout`, `-Clean`, and `-PruneCacheOlderThanDays`. State that same-project builds wait, different projects can overlap, source mutations and partial output remain after failure, and build-waiter requires `.helengine-build-state.json` in addition to fresh artifacts.

Add a cache override example:

```powershell
-CacheRoot D:\helengine-cache
```

Do not tell users to delete `C:\dev\helworks\b` automatically; legacy cleanup remains a separate explicit operation.

- [ ] **Step 6: Check all live caller documentation**

```powershell
rtk rg -n "isolated copied project|WorkspaceRoot|HELENGINE_BUILD_WORKSPACE_ROOT|C:\\dev\\helworks\\b" README.md scripts tools engine -g "*.md" -g "*.ps1" -g "*.cs"
```

Expected: only compatibility implementation/tests mention `WorkspaceRoot` or `HELENGINE_BUILD_WORKSPACE_ROOT`; no live docs describe copied-project builds or the legacy `b` root.

- [ ] **Step 7: Commit smoke and documentation**

```powershell
rtk git add -- README.md engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs engine/helengine.editor.tests/managers/project/EditorSourceBuildWorkspaceLocatorTests.cs scripts/tests/build-platform-real-editor-smoke.tests.ps1 scripts/tests/fixtures/build-platform-smoke-builder scripts/tests/fixtures/build-platform-smoke-project
rtk git commit -m "Document and smoke-test direct-source builds"
```

## Task 8: Full verification and scope audit

**Files:**

- Verify only; modify task files only if a regression is found.

- [ ] **Step 1: Run every PowerShell contract test**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile-behavior.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-streaming.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-maintenance.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-real-editor-smoke.tests.ps1
```

Expected: all seven scripts print pass markers and exit zero.

- [ ] **Step 2: Run focused .NET suites**

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildIsolationPathResolverTests|FullyQualifiedName~EditorPlatformBuildGraphWorkspaceFactoryTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorCliBuildRunner"
```

Expected: both commands exit zero with no failed tests.

- [ ] **Step 3: Scan for the write-amplifying implementation**

```powershell
rtk rg -n "Copy-ProjectIntoIsolatedWorkspace|robocopy|BuildInvocationRootPath|IsolatedProjectPath|HELENGINE_BUILD_WORKSPACE_ROOT" scripts/build-platform.ps1 scripts/build-platform engine/helengine.editor
rtk rg -n "Guid.NewGuid" scripts/build-platform.ps1 scripts/build-platform
```

Expected: the wrapper/support modules contain none of the project-copy/GUID tokens. The editor may retain `HELENGINE_BUILD_WORKSPACE_ROOT` and GUID generation only for deprecated compatibility and non-stable interactive isolation.

- [ ] **Step 4: Audit the cache tree produced by tests**

Confirm the smoke cache has only `locks`, `projects/<hash>/editor/<configuration>`, and `projects/<hash>/platforms/<platform>/<configuration>/<profile>`; no full project tree and no invocation directory exists. Confirm final output was written directly and contains the state file.

- [ ] **Step 5: Verify worktree scope**

```powershell
rtk git status --short
rtk git diff --check
rtk git diff --stat HEAD~7..HEAD
```

Expected: no whitespace errors; the pre-existing `ModelTessellationProcessorTests.cs` modification remains unstaged and absent from implementation commits.

- [ ] **Step 6: Final implementation commit only if verification required fixes**

Stage exact corrected files, rerun the affected focused test and the full verification above, then commit:

```powershell
rtk git commit -m "Finish direct-source build cache rework"
```

Do not create an empty commit.
