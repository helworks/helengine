# Build Platform Completion Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the remaining output-deletion, argument-override, shared-output, stale-editor-cache, waiter-identity, and Windows native path-headroom risks in the direct-source build-platform rework.

**Architecture:** Validate canonical inputs before mutation, use a compact versioned deterministic cache keyed by authored project and editor checkout, serialize both project and output identities under one timeout budget, and bind build-waiter to the wrapper's exact state identifier. Keep direct-source builds and cache reuse; do not restore repository copies or invocation-scoped caches.

**Tech Stack:** Windows PowerShell 5.1, .NET 9/C#, xUnit, named `System.Threading.Mutex`, file-stream locks, SHA-256 path identities, CMake/Ninja/MSVC native smoke coverage.

## Global Constraints

- No repository cloning, `robocopy`, or invocation-scoped GUID cache directories.
- Canonical path identity is case-insensitive and directory-boundary-aware on Windows.
- Retain 16 SHA-256 bytes as 32 lowercase hexadecimal characters for path and mutex identities.
- Invalid arguments and output/cache overlap fail before any filesystem mutation.
- Lock order is project global mutex, output global mutex, then cache-local project file lock; release in reverse order.
- All locks consume one shared `-LockTimeout` budget.
- Direct wrapper calls remain supported without build-waiter.
- Earlier cache layouts are not implicitly migrated or recursively deleted.
- Every production change follows red-green-refactor and receives a task-level specification and quality review.

---

## File Responsibility Map

- `scripts/build-platform/BuildPlatformCache.psm1`: canonical path hashing, compact v2 layout, guarded maintenance enumeration.
- `engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs`: editor-side reconstruction of the same compact platform cache path.
- `scripts/build-platform.ps1`: pre-mutation validation, lock orchestration, invocation ID adoption, editor invocation.
- `scripts/build-platform/BuildPlatformLock.psm1`: named project/output mutex primitives and release behavior.
- `tools/build-waiter/BuildWaiter.cs`: invocation ID creation, child environment propagation, exact state verification request.
- `tools/build-waiter/BuildStateVerifier.cs`: state `buildId` equality validation.
- Existing focused test files own behavioral regressions; one new PowerShell script owns the opt-in native cache smoke.

---

### Task 1: Compact, Checkout-Aware Cache Layout

**Files:**
- Modify: `scripts/build-platform/BuildPlatformCache.psm1:50-173`
- Modify: `scripts/build-platform/BuildPlatformCache.psm1:300-430`
- Modify: `scripts/build-platform.ps1:175-208`
- Modify: `engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs:210-310`
- Modify: `scripts/tests/build-platform-workspace.tests.ps1:580-675`
- Modify: `scripts/tests/build-platform-maintenance.tests.ps1`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs`

**Interfaces:**
- Consumes: canonical cache root, authored-project root, editor `.csproj`, platform, configuration, profile.
- Produces: `Get-BuildPlatformPathHash -Path <string> -> string`; `Resolve-BuildPlatformCacheLayout` gains mandatory `-EditorProjectPath`; layout properties remain `CacheRootPath`, `ProjectHash`, `ProjectCacheRootPath`, `LockPath`, `EditorConfigurationRootPath`, `EditorArtifactsPath`, `EditorPublishPath`, `PlatformCacheRootPath`, and `MetadataPath`.

- [ ] **Step 1: Write failing cache-layout tests**

Add integration assertions proving:

```powershell
$LayoutA = Resolve-BuildPlatformCacheLayout -CacheRootPath $CacheRoot `
    -ProjectRootPath $ProjectRoot -EditorProjectPath $EditorProjectA `
    -Platform windows -Configuration Release -BuildProfile profiler
$LayoutARepeat = Resolve-BuildPlatformCacheLayout -CacheRootPath $CacheRoot `
    -ProjectRootPath $ProjectRoot -EditorProjectPath $EditorProjectA `
    -Platform windows -Configuration Release -BuildProfile profiler
$LayoutB = Resolve-BuildPlatformCacheLayout -CacheRootPath $CacheRoot `
    -ProjectRootPath $ProjectRoot -EditorProjectPath $EditorProjectB `
    -Platform windows -Configuration Release -BuildProfile profiler

if ($LayoutA.EditorArtifactsPath -cne $LayoutARepeat.EditorArtifactsPath) { throw 'Editor cache was not stable.' }
if ($LayoutA.EditorArtifactsPath -ceq $LayoutB.EditorArtifactsPath) { throw 'Different editor checkouts shared artifacts.' }
if ($LayoutA.PlatformCacheRootPath -cne $LayoutB.PlatformCacheRootPath) { throw 'Editor identity leaked into platform cache identity.' }
if ($LayoutA.PlatformCacheRootPath.Length -ge $LegacyVerbosePath.Length) { throw 'The v2 platform path was not compacted.' }
```

Update resolver tests to expect the wrapper-compatible compact path `<cache>\v2\<project-hash>\b\<platform>\<configuration>\<profile>` and maintenance tests to enumerate only valid 32-hex project directories beneath `v2` while excluding `v2\l`.

- [ ] **Step 2: Run tests and verify the intended red state**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-maintenance.tests.ps1
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorBuildIsolationPathResolverTests
```

Expected: failures because `-EditorProjectPath` is unknown and the current `projects/.../platforms/...` layout does not match v2.

- [ ] **Step 3: Implement the compact layout and generic path hash**

Refactor the current SHA-256 implementation into:

```powershell
function Get-BuildPlatformPathHash {
    param([Parameter(Mandatory = $true)][string]$Path)
    $CanonicalPath = (Get-BuildPlatformCanonicalDirectoryPath -Path $Path).ToLowerInvariant()
    $PathBytes = [System.Text.Encoding]::UTF8.GetBytes($CanonicalPath)
    $Sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $HashBytes = $Sha256.ComputeHash($PathBytes)
    } finally {
        $Sha256.Dispose()
    }
    $Builder = New-Object System.Text.StringBuilder
    for ($Index = 0; $Index -lt 16; $Index++) {
        $null = $Builder.Append($HashBytes[$Index].ToString('x2'))
    }
    return $Builder.ToString()
}
```

Make `Get-BuildPlatformProjectHash` call the generic helper. Resolve the v2 tree exactly as:

```text
<cache>\v2\l\<project-hash>.lock
<cache>\v2\<project-hash>\m.json
<cache>\v2\<project-hash>\e\<editor-path-hash>\<configuration>\a
<cache>\v2\<project-hash>\e\<editor-path-hash>\<configuration>\p
<cache>\v2\<project-hash>\b\<platform>\<configuration>\<profile>
```

Pass `$ResolvedEditorProject` into `Resolve-BuildPlatformCacheLayout`. Mirror only the platform path in `EditorBuildIsolationPathResolver.ResolveStableProfileRootPath`; the wrapper continues to own editor publish paths. Update prune discovery to inspect `<cache>\v2`, accept only 32-lowercase-hex project directories, skip `l`, and retain the existing reparse/containment/pre-delete checks.

- [ ] **Step 4: Run focused tests and verify green**

Run the three Step 2 commands. Expected: all pass with `WORKSPACE_TEST_PASS`, `MAINTENANCE_TEST_PASS`, and a green resolver test filter.

- [ ] **Step 5: Commit Task 1**

```powershell
rtk git add -- scripts/build-platform/BuildPlatformCache.psm1 scripts/build-platform.ps1 scripts/tests/build-platform-workspace.tests.ps1 scripts/tests/build-platform-maintenance.tests.ps1 engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs engine/helengine.editor.tests/managers/project/EditorBuildIsolationPathResolverTests.cs
rtk git commit -m "Compact and isolate build caches by editor checkout"
```

---

### Task 2: Reject Unsafe Output and Additional Arguments

**Files:**
- Modify: `scripts/build-platform.ps1:48-65`
- Modify: `scripts/build-platform.ps1:127-230`
- Modify: `scripts/tests/build-platform-workspace.tests.ps1`

**Interfaces:**
- Consumes: resolved output path, `Layout.ProjectCacheRootPath`, `string[] AdditionalArgs`.
- Produces: private wrapper functions `Test-BuildPlatformPathOverlap([string]$FirstPath, [string]$SecondPath) -> bool` and `Assert-BuildPlatformAdditionalArguments([string[]]$Arguments)`.

- [ ] **Step 1: Write failing wrapper regressions**

Add cases for output equal to project cache, output beneath it, and project cache beneath output. Before each invocation create a sentinel beside the proposed path, then assert exit code `2`, no editor invocation, no state file, and the sentinel remains.

Add table-driven cases for exact, mixed-case, and inline forms:

```powershell
$ReservedCases = @(
    @('--project', 'C:\decoy\project.heproj'),
    @('--PROJECT=C:\decoy\project.heproj'),
    @('--build', 'ps2'),
    @('--build=ps2'),
    @('--build-profile', 'release'),
    @('--build-profile=release'),
    @('--output', 'C:\decoy\output'),
    @('--output=C:\decoy\output')
)
```

Each case must fail before cache/output creation and mention the rejected switch. Include one allowed custom argument to prove pass-through remains functional.

- [ ] **Step 2: Run workspace tests and verify red**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
```

Expected: overlap cases mutate or enter the editor, and reserved switches are accepted.

- [ ] **Step 3: Implement pre-mutation validation**

Use canonical paths plus separator-boundary prefixes:

```powershell
function Test-BuildPlatformPathContains {
    param([string]$ParentPath, [string]$CandidatePath)
    $Parent = Get-CanonicalDirectoryPath $ParentPath
    $Candidate = Get-CanonicalDirectoryPath $CandidatePath
    $Prefix = $Parent
    if (-not $Prefix.EndsWith([IO.Path]::DirectorySeparatorChar)) {
        $Prefix += [IO.Path]::DirectorySeparatorChar
    }
    return $Candidate.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-BuildPlatformPathOverlap {
    param([string]$FirstPath, [string]$SecondPath)
    $First = Get-CanonicalDirectoryPath $FirstPath
    $Second = Get-CanonicalDirectoryPath $SecondPath
    return $First.Equals($Second, [StringComparison]::OrdinalIgnoreCase) -or
        (Test-BuildPlatformPathContains -Parent $First -Candidate $Second) -or
        (Test-BuildPlatformPathContains -Parent $Second -Candidate $First)
}
```

Implement `Test-BuildPlatformPathContains` without raw prefix matching. Reject reserved switches with `OrdinalIgnoreCase` equality or a following `=` only; do not reject names such as `--projectile`. Call both validations immediately after layout resolution and before lock metadata, directory creation, maintenance, state, or child processes. Return wrapper usage exit code `2` with a precise diagnostic.

- [ ] **Step 4: Run workspace, profile, and streaming tests**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-profile-behavior.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-streaming.tests.ps1
```

Expected: `WORKSPACE_TEST_PASS`, `PROFILE_BEHAVIOR_TEST_PASS`, and `STREAMING_TEST_PASS`.

- [ ] **Step 5: Commit Task 2**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/tests/build-platform-workspace.tests.ps1
rtk git commit -m "Reject unsafe build output and argument overrides"
```

---

### Task 3: Serialize Shared Output Across Projects

**Files:**
- Modify: `scripts/build-platform/BuildPlatformLock.psm1:170-270`
- Modify: `scripts/build-platform.ps1:163-230`
- Modify: `scripts/build-platform.ps1:417-443`
- Modify: `scripts/tests/build-platform-locking.tests.ps1`

**Interfaces:**
- Consumes: canonical output path and `Get-BuildPlatformPathHash`.
- Produces: `Enter-BuildPlatformOutputMutex -OutputHash <32-hex> -OutputPath <canonical> -Timeout <TimeSpan>` and `Exit-BuildPlatformOutputMutex -MutexHandle <psobject>`; mutex name `Global\helengine.build-platform.output.v1.<hash>`. The wrapper adds private `Get-RemainingBuildPlatformLockTimeout([Stopwatch]$Stopwatch, [TimeSpan]$Timeout) -> TimeSpan`, clamped to zero.

- [ ] **Step 1: Write failing cross-process output-lock tests**

Add a harness case launching project A and project B with distinct cache roots but one output. Hold A inside the fake editor, start B, and assert B has not entered the editor. Release A and assert B completes. Add timeout, release-after-failure, abandoned mutex, exact-name, and invalid-hash tests parallel to the project-mutex coverage.

Also retain the existing test proving different projects with different outputs overlap.

- [ ] **Step 2: Run locking tests and verify red**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-locking.tests.ps1
```

Expected: the same-output different-project case overlaps and output-mutex validation functions are missing.

- [ ] **Step 3: Implement the output mutex and shared timeout budget**

Generalize the named-mutex mechanics only enough to avoid duplicated acquisition/release logic. The public output entry point must validate the 32-hex hash, use the exact versioned output mutex name, treat abandonment as ownership, and dispose on every failure.

In the wrapper:

```powershell
$ProjectMutex = Enter-BuildPlatformProjectMutex -ProjectHash $Layout.ProjectHash -ProjectPath $ResolvedProjectPath -Timeout $LockTimeout
$OutputHash = Get-BuildPlatformPathHash -Path $ResolvedOutputPath
$OutputMutex = Enter-BuildPlatformOutputMutex -OutputHash $OutputHash -OutputPath $ResolvedOutputPath -Timeout (Get-RemainingBuildPlatformLockTimeout -Stopwatch $LockWaitStopwatch -Timeout $LockTimeout)
$ProjectLock = Enter-BuildPlatformProjectLock -LockPath $Layout.LockPath -Metadata $LockMetadata -Timeout (Get-RemainingBuildPlatformLockTimeout -Stopwatch $LockWaitStopwatch -Timeout $LockTimeout)
```

Release project file lock, then output mutex, then project mutex, with environment restoration outermost. A timeout before state start must not create or replace output state.

- [ ] **Step 4: Run locking and workspace tests**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-locking.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
```

Expected: `LOCKING_TEST_PASS` and `WORKSPACE_TEST_PASS`.

- [ ] **Step 5: Commit Task 3**

```powershell
rtk git add -- scripts/build-platform/BuildPlatformLock.psm1 scripts/build-platform.ps1 scripts/tests/build-platform-locking.tests.ps1 scripts/tests/build-platform-workspace.tests.ps1
rtk git commit -m "Serialize builds by final output"
```

---

### Task 4: Bind Build-Waiter to Exact Wrapper State

**Files:**
- Modify: `tools/build-waiter/BuildWaiter.cs:35-105`
- Modify: `tools/build-waiter/BuildStateVerifier.cs:20-82`
- Modify: `tools/build-waiter.tests/BuildWaiterTests.cs`
- Modify: `tools/build-waiter.tests/BuildStateVerifierTests.cs`
- Modify: `scripts/build-platform.ps1:163-250`
- Modify: `scripts/tests/build-platform-workspace.tests.ps1`

**Interfaces:**
- Consumes: environment variable `HELENGINE_BUILD_INVOCATION_ID` containing a canonical `Guid` in `D` format.
- Produces: `BuildStateVerifier.Verify(string outputRootPath, DateTime waiterStartedUtc, string expectedBuildId)`; wrapper state continues using the existing `buildId` JSON property.

- [ ] **Step 1: Write failing verifier and process tests**

Add verifier coverage:

```csharp
BuildStateVerificationResult result = new BuildStateVerifier().Verify(
    outputRootPath,
    waiterStartedUtc,
    "expected-build-id");
Assert.False(result.Succeeded);
Assert.Contains("build id", result.Message, StringComparison.OrdinalIgnoreCase);
```

The state fixture must contain a fresh successful `foreign-build-id`. Add argument validation for blank expected ID. In `BuildWaiterTests`, make the PowerShell child write `$env:HELENGINE_BUILD_INVOCATION_ID` as `buildId`, prove success, then make it deliberately write another ID and prove failure despite fresh state/artifacts and exit zero.

Add wrapper tests proving a valid environment ID is recorded unchanged, an absent value generates a valid canonical GUID, and malformed values fail before output mutation.

- [ ] **Step 2: Run waiter and workspace tests and verify red**

```powershell
rtk dotnet test tools/build-waiter.tests/helengine.buildwaiter.tests.csproj --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
```

Expected: compile failures for the new verifier signature and wrapper identity tests failing because the environment value is ignored.

- [ ] **Step 3: Implement invocation propagation and exact verification**

In `BuildWaiter.WaitAsync`:

```csharp
string invocationId = Guid.NewGuid().ToString("D");
startInfo.Environment["HELENGINE_BUILD_INVOCATION_ID"] = invocationId;
// After zero child exit:
StateVerifier.Verify(options.OutputRootPath, buildStartedUtc, invocationId);
```

Validate `expectedBuildId` as nonblank and compare `document.BuildId` with `StringComparison.OrdinalIgnoreCase` before timestamp/status success. In the wrapper, accept only `Guid.TryParseExact(value, "D", out Guid parsed)` and normalize with `parsed.ToString("D")`; otherwise print a usage error and exit `2`. If the variable is absent, generate `Guid.NewGuid().ToString("D")`. Do not change the JSON schema.

- [ ] **Step 4: Run waiter and wrapper suites**

```powershell
rtk dotnet test tools/build-waiter.tests/helengine.buildwaiter.tests.csproj --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-real-editor-smoke.tests.ps1
```

Expected: all waiter tests pass, `WORKSPACE_TEST_PASS`, and `REAL_EDITOR_SMOKE_TEST_PASS`.

- [ ] **Step 5: Commit Task 4**

```powershell
rtk git add -- tools/build-waiter tools/build-waiter.tests scripts/build-platform.ps1 scripts/tests/build-platform-workspace.tests.ps1
rtk git commit -m "Bind build waiter to exact invocation state"
```

---

### Task 5: Native Stable-Cache Smoke and Documentation

**Files:**
- Create: `scripts/tests/build-platform-native-cache-smoke.tests.ps1`
- Modify: `README.md`
- Modify: `scripts/tests/build-platform-real-editor-smoke.tests.ps1` only if a shared fixture helper can be reused without coupling native and fake-builder assertions.

**Interfaces:**
- Consumes: sibling Windows platform source at `C:\dev\helworks\helengine-windows`, Visual Studio developer tools, CMake, Ninja, the smoke authored-project fixture, and the production wrapper.
- Produces: explicit `NATIVE_CACHE_SMOKE_TEST_PASS` test command; no default fast-suite dependency on external repositories.

- [ ] **Step 1: Write the native smoke test and observe the current path behavior**

The script must create disposable source/cache/output roots beneath `C:\tmp` or `$env:TEMP`, copy only the tiny authored-project fixture, configure the real Windows builder through disposable user settings, invoke the production wrapper with its compact stable cache, and require a non-empty Windows executable plus matching successful state. It must print the resolved `Platform cache:` path and its character count so a failure retains exact path-headroom evidence; the real CMake/MSVC compile is the acceptance criterion rather than an invented static margin.

Use strict prerequisite checks:

```powershell
if (-not (Test-Path -LiteralPath 'C:\dev\helworks\helengine-windows' -PathType Container)) { throw 'Windows platform source is required.' }
if ($null -eq (Get-Command cmake.exe -ErrorAction SilentlyContinue)) { throw 'cmake.exe is required.' }
```

Always restore environment and remove only the exact disposable test root in `finally` after verifying its canonical path is beneath the chosen temporary root.

- [ ] **Step 2: Run native smoke before any smoke-specific production adjustment**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-native-cache-smoke.tests.ps1
```

Expected: either pass immediately from Task 1's compact layout or fail with an exact path-headroom/native configuration diagnostic. If it fails, add no alternate cache override; adjust only compact segment construction in Task 1-owned layout code, rerun Task 1 focused tests, then rerun this smoke.

- [ ] **Step 3: Document the final contract**

Update `README.md` with:

- compact v2 cache identity and editor-checkout isolation;
- output/cache disjointness;
- reserved `AdditionalArgs` switches;
- same-output serialization across projects;
- `HELENGINE_BUILD_INVOCATION_ID` as a wrapper/waiter internal contract, not a normal user setting;
- native smoke command and prerequisites;
- explicit statement that no repository copy is made.

- [ ] **Step 4: Run smoke and documentation scans**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-native-cache-smoke.tests.ps1
rtk rg -n "v2|same output|AdditionalArgs|HELENGINE_BUILD_INVOCATION_ID|repository cop" README.md
```

Expected: `NATIVE_CACHE_SMOKE_TEST_PASS`; each documented contract is present.

- [ ] **Step 5: Commit Task 5**

```powershell
rtk git add -- scripts/tests/build-platform-native-cache-smoke.tests.ps1 README.md scripts/build-platform/BuildPlatformCache.psm1 engine/helengine.editor/managers/project/EditorBuildIsolationPathResolver.cs
rtk git commit -m "Smoke-test native stable build caches"
```

---

### Task 6: Full Verification and Final Review

**Files:**
- Modify only files required by one consolidated review fix, if findings are reproduced and covered by a failing test first.

**Interfaces:**
- Consumes: Tasks 1-5 commits.
- Produces: clean verification ledger, clean independent review, integration-ready feature branch.

- [ ] **Step 1: Run all fast wrapper suites**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-workspace.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-profile.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-profile-behavior.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-streaming.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-locking.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-maintenance.tests.ps1
```

Expected: all six `*_TEST_PASS` sentinels.

- [ ] **Step 2: Run integration and .NET verification**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-real-editor-smoke.tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/build-platform-native-cache-smoke.tests.ps1
rtk dotnet test tools/build-waiter.tests/helengine.buildwaiter.tests.csproj --no-restore
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildIsolationPathResolverTests|FullyQualifiedName~EditorPlatformBuildGraphWorkspaceFactoryTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorCliBuildRunner"
```

Expected: both smoke sentinels and all waiter tests pass. Record the exact focused editor result; only the three already documented 254-character temporary-path CMake/MSVC failures may remain as an environmental caveat.

- [ ] **Step 3: Run scope and cleanliness audits**

```powershell
rtk rg -n "Copy-ProjectIntoIsolatedWorkspace|robocopy|BuildInvocationRootPath|IsolatedProjectPath" scripts/build-platform.ps1 scripts/build-platform
rtk rg -n "Guid.NewGuid" scripts/build-platform.ps1 scripts/build-platform
rtk rg -n -i "C:\\dev\\helworks\\b(?:[\\/]|$)" .
rtk git diff --check
rtk git status --short
```

Expected: no clone/isolation or legacy `b` target matches; `Guid.NewGuid` appears only for direct-call state identity, not cache allocation; diff check and feature status are clean.

- [ ] **Step 4: Request one whole-branch independent review**

Review from merge base `8491a115ca33656d1fe75eaf365b6918778e3645` through `HEAD`, with the approved completion design, this plan, and verification ledger. Require findings first with file/line references. Explicitly recheck all six risks: output deletion, argument override, shared output, waiter identity, editor checkout reuse, and native path headroom.

- [ ] **Step 5: Handle review once**

If the review is clean, record it and stop changing code. If it reports findings, reproduce each first; dispatch one consolidated fix worker; require failing regressions before production changes; rerun affected suites; then request one re-review. Do not loop additional agent waves.

- [ ] **Step 6: Confirm user-edit isolation and branch status**

Compare feature changed paths against the user's main-worktree `git status --short`. Expected: no overlap. Report branch name, tip commit, verification evidence, environmental caveat if present, and integration options without merging or pushing automatically.
