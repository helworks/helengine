# Build Platform Safety Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the final cross-cache serialization and maintenance-containment gaps in direct-source platform builds.

**Architecture:** The wrapper acquires a project-hash named OS mutex before its existing cache-local file lock, using one shared timeout budget and reverse-order release. Cache maintenance receives canonical authored-source and output roots and rejects any recursive target that equals or contains either protected root during both initial and immediate pre-delete validation.

**Tech Stack:** Windows PowerShell 5.1, .NET `System.Threading.Mutex`, existing file-share locks, disposable cross-process PowerShell harnesses.

## Global Constraints

- The mutex name is exactly `Global\helengine.build-platform.project.v1.<project-hash>`.
- The mutex identity uses only the existing lowercase 32-hex canonical project hash; it contains no source path text.
- `-LockTimeout` is one total wait budget across global mutex and cache-local file lock acquisition.
- Lock order is global mutex then cache-local file lock; release order is cache-local lock then global mutex.
- An abandoned named mutex is acquired successfully and released normally by the new owner.
- Every recursive deletion rejects a target equal to or containing the canonical authored-project root or requested output root.
- Protected-path checks are case-insensitive and directory-boundary-aware on Windows.
- Clean fails closed with an error; prune warns and skips an overlapping candidate while holding/releasing its candidate cache lock correctly.
- Do not add repository copying, invocation GUID caches, a second metadata file, or access to `C:\dev\helworks\b`.
- Preserve all user changes in the main worktree.

---

### Task 1: Serialize canonical projects across different cache roots

**Files:**

- Modify: `scripts/build-platform/BuildPlatformLock.psm1`
- Modify: `scripts/build-platform.ps1`
- Modify: `scripts/tests/build-platform-locking.tests.ps1`

**Interfaces:**

- Produces: `Enter-BuildPlatformProjectMutex -ProjectHash <string> -ProjectPath <string> -Timeout <TimeSpan>` returning a mutex handle object.
- Produces: `Exit-BuildPlatformProjectMutex -MutexHandle <psobject>` releasing and disposing the owned mutex.
- Preserves: `Enter-BuildPlatformProjectLock`, `Enter-BuildPlatformProjectLockNonBlocking`, `Test-BuildPlatformProjectLockHeld`, and `Exit-BuildPlatformProjectLock`.

- [ ] **Step 1: Make the locking harness support per-invocation cache roots**

Add an optional `CacheRootPath` argument to `New-InvocationControl`, store the canonical selected value on the control object, and use it in `Start-WrapperInvocation`:

```powershell
function New-InvocationControl {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter()][string]$CacheRootPath = $script:CacheRootPath,
        [Parameter()][switch]$Released,
        [Parameter()][TimeSpan]$LockTimeout = [TimeSpan]::FromSeconds(20)
    )

    CacheRootPath = [System.IO.Path]::GetFullPath($CacheRootPath)
}
```

Replace the fixed wrapper argument with `$Control.CacheRootPath`.

- [ ] **Step 2: Write the cross-cache failing regression**

After the existing same-cache same-project case, start the owner and waiter with one canonical project and two cache roots:

```powershell
$CrossCacheProjectPath = New-TestProject -Name "cross-cache-project"
$CrossCacheOwner = Start-WrapperInvocation -Control (New-InvocationControl `
    -Name "cross-cache-owner" `
    -ProjectPath $CrossCacheProjectPath `
    -CacheRootPath (Join-Path $TestRootPath "cache-a"))
Wait-ForPath -Path $CrossCacheOwner.MarkerPath -Description "the cross-cache owner to enter fake dotnet"

$CrossCacheWaiter = Start-WrapperInvocation -Control (New-InvocationControl `
    -Name "cross-cache-waiter" `
    -ProjectPath $CrossCacheProjectPath `
    -CacheRootPath (Join-Path $TestRootPath "cache-b") `
    -Released)
Start-Sleep -Milliseconds 750
if (Test-Path -LiteralPath $CrossCacheWaiter.MarkerPath) {
    throw "Same canonical project wrappers bypassed serialization through different cache roots."
}
Release-Invocation -Control $CrossCacheOwner
$null = Assert-SuccessfulInvocation -Control $CrossCacheOwner
$null = Assert-SuccessfulInvocation -Control $CrossCacheWaiter
```

Retain the existing different-project overlap case as the concurrency control.

- [ ] **Step 3: Run the locking test and capture RED**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
```

Expected: non-zero with `bypassed serialization through different cache roots`.

- [ ] **Step 4: Add the named mutex API**

Implement the two exported functions in `BuildPlatformLock.psm1`:

```powershell
function Enter-BuildPlatformProjectMutex {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectHash,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][TimeSpan]$Timeout
    )

    if ($ProjectHash -cnotmatch '^[0-9a-f]{32}$') { throw "Project hash must be 32 lowercase hexadecimal characters." }
    if ([string]::IsNullOrWhiteSpace($ProjectPath)) { throw "Project path must be provided." }
    if ($Timeout -lt [TimeSpan]::Zero) { throw "Lock timeout must be zero or positive." }

    $MutexName = "Global\helengine.build-platform.project.v1.$ProjectHash"
    $Mutex = New-Object System.Threading.Mutex($false, $MutexName)
    $OwnsMutex = $false
    try {
        try {
            $OwnsMutex = $Mutex.WaitOne($Timeout)
        } catch [System.Threading.AbandonedMutexException] {
            $OwnsMutex = $true
        }
        if (-not $OwnsMutex) {
            throw "Timed out after $($Timeout.ToString('c')) waiting for project mutex '$MutexName' for canonical project '$ProjectPath'."
        }
        return [pscustomobject]@{ Name = $MutexName; Mutex = $Mutex; OwnsMutex = $true }
    } catch {
        if ($OwnsMutex) { $Mutex.ReleaseMutex() }
        $Mutex.Dispose()
        throw
    }
}

function Exit-BuildPlatformProjectMutex {
    param([Parameter(Mandatory = $true)][psobject]$MutexHandle)
    try {
        if ($MutexHandle.OwnsMutex) { $MutexHandle.Mutex.ReleaseMutex() }
    } finally {
        $MutexHandle.Mutex.Dispose()
    }
}
```

Export both functions and update the locking test's exact export list.

- [ ] **Step 5: Wire dual locking with one timeout budget**

In `build-platform.ps1`, initialize `$ProjectMutex = $null` and acquire it before `$ProjectLock`:

```powershell
$LockWaitStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$ProjectMutex = Enter-BuildPlatformProjectMutex `
    -ProjectHash $Layout.ProjectHash `
    -ProjectPath $ResolvedProjectPath `
    -Timeout $LockTimeout
$RemainingLockTimeout = $LockTimeout - $LockWaitStopwatch.Elapsed
if ($RemainingLockTimeout -lt [TimeSpan]::Zero) { $RemainingLockTimeout = [TimeSpan]::Zero }
$ProjectLock = Enter-BuildPlatformProjectLock `
    -LockPath $Layout.LockPath `
    -Metadata $LockMetadata `
    -Timeout $RemainingLockTimeout
```

Keep all existing terminal state/metadata work under both locks. Release the cache-local file lock first, then release the mutex from the next outer `finally`, ensuring local acquisition failure still releases the mutex.

- [ ] **Step 6: Add direct mutex contract coverage**

Extend the locking script to assert:

- invalid hashes and negative timeouts throw;
- zero timeout returns a timeout error while another process owns the named mutex;
- terminating an owner process abandons the mutex and the next acquisition succeeds;
- `Exit-BuildPlatformProjectMutex` releases ownership so a contender can acquire it.

Use a disposable helper PowerShell process importing `BuildPlatformLock.psm1`, writing an `owned.marker` after acquisition, and waiting on a `release.marker`; terminate only the exact recorded process in the abandonment case.

The helper body is:

```powershell
Import-Module $LockModulePath -Force
$Handle = Enter-BuildPlatformProjectMutex `
    -ProjectHash $ProjectHash `
    -ProjectPath $ProjectPath `
    -Timeout ([TimeSpan]::FromSeconds(10))
Set-Content -LiteralPath $OwnedMarkerPath -Value $PID -NoNewline
try {
    while (-not (Test-Path -LiteralPath $ReleaseMarkerPath)) {
        Start-Sleep -Milliseconds 25
    }
} finally {
    Exit-BuildPlatformProjectMutex -MutexHandle $Handle
}
```

For abandonment, stop the exact helper PID after `owned.marker` appears and do not create `release.marker`; the next zero-timeout acquisition must succeed through `AbandonedMutexException`.

- [ ] **Step 7: Run focused regressions**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-streaming.tests.ps1
```

Expected: `LOCKING_TEST_PASS`, `WORKSPACE_TEST_PASS`, and `STREAMING_TEST_PASS`.

- [ ] **Step 8: Commit project-wide locking**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformLock.psm1 scripts/tests/build-platform-locking.tests.ps1
rtk git commit -m "Serialize projects across cache roots"
```

---

### Task 2: Protect authored source and output from maintenance overlap

**Files:**

- Modify: `scripts/build-platform/BuildPlatformCache.psm1`
- Modify: `scripts/build-platform.ps1`
- Modify: `scripts/tests/build-platform-maintenance.tests.ps1`

**Interfaces:**

- Changes: `Remove-BuildPlatformSelectedCache -Layout <psobject> -ProtectedPath <string[]>`.
- Changes: `Remove-BuildPlatformExpiredProjectCaches -CacheRootPath <string> -OlderThanDays <int> -ProtectedPath <string[]> [-NowUtc <DateTime>]`.
- Internal: every call to `Remove-BuildPlatformGuardedDirectory` supplies the same protected path list.

- [ ] **Step 1: Write selected-clean overlap regressions**

Create a clean layout with an output sentinel beneath the editor target and prove clean fails before deleting either selected slice:

```powershell
$ProtectedOutputPath = Join-Path $LayoutA.EditorArtifactsPath "requested-output"
$ProtectedOutputSentinel = Join-Path $ProtectedOutputPath "output-sentinel.txt"
New-Sentinel -LiteralPath $ProtectedOutputSentinel
New-Sentinel -LiteralPath (Join-Path $LayoutA.PlatformCacheRootPath "platform-sentinel.txt")
Assert-Throws -CaseName "Selected clean containing requested output" -Action {
    Remove-BuildPlatformSelectedCache `
        -Layout $LayoutA `
        -ProtectedPath @($ProjectARootPath, $ProtectedOutputPath)
}
Assert-PathExists -LiteralPath $ProtectedOutputSentinel
Assert-PathExists -LiteralPath (Join-Path $LayoutA.PlatformCacheRootPath "platform-sentinel.txt")
```

Add a sibling-boundary control whose protected path is `editor\debug-sibling\output`; selected clean must still remove `editor\debug`.

- [ ] **Step 2: Write prune overlap regressions**

Create two valid expired candidates. Place the current source sentinel beneath one candidate and the requested output sentinel beneath the other, while each candidate metadata still hashes its own separate project root. Invoke prune with both protected paths and assert both candidates and sentinels survive. Assert a third expired non-overlapping candidate is removed.

The calls must have this shape:

```powershell
Remove-BuildPlatformExpiredProjectCaches `
    -CacheRootPath $PruneRootPath `
    -OlderThanDays 30 `
    -ProtectedPath @($ProtectedSourcePath, $ProtectedOutputPath) `
    -NowUtc $NowUtc
```

- [ ] **Step 3: Run maintenance and capture RED**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-maintenance.tests.ps1
```

Expected: parameter binding or sentinel-deletion failure because protected paths are not enforced yet.

- [ ] **Step 4: Add boundary-aware protected-path validation**

Add an internal helper in `BuildPlatformCache.psm1`:

```powershell
function Assert-BuildPlatformDeleteTargetDoesNotContainProtectedPath {
    param(
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [Parameter(Mandatory = $true)][string[]]$ProtectedPath
    )

    $CanonicalTargetPath = Get-BuildPlatformCanonicalDirectoryPath -Path $TargetPath
    $TargetPrefix = $CanonicalTargetPath.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    foreach ($CandidateProtectedPath in $ProtectedPath) {
        if ([string]::IsNullOrWhiteSpace($CandidateProtectedPath)) { throw "Protected path must be provided." }
        $CanonicalProtectedPath = Get-BuildPlatformCanonicalDirectoryPath -Path $CandidateProtectedPath
        if ($CanonicalProtectedPath.Equals($CanonicalTargetPath, [System.StringComparison]::OrdinalIgnoreCase) -or
            $CanonicalProtectedPath.StartsWith($TargetPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Delete target '$CanonicalTargetPath' contains protected path '$CanonicalProtectedPath'."
        }
    }
}
```

Call it from `Get-BuildPlatformGuardedDeleteTarget` after strict descendant validation. Thread `ProtectedPath` through initial validation and immediate per-target revalidation in `Remove-BuildPlatformGuardedDirectory`.

- [ ] **Step 5: Thread protected paths through clean and prune**

Make `ProtectedPath` mandatory on both exported maintenance functions. Selected clean passes it directly to guarded deletion. Prune passes it into candidate validation so overlaps are caught by the candidate's existing warning/skip boundary, then passes it again to final guarded deletion while holding the candidate cache lock.

In `build-platform.ps1`, define once after canonical path resolution:

```powershell
$MaintenanceProtectedPaths = @($ResolvedProjectRootPath, $ResolvedOutputPath)
```

Pass that array to both maintenance calls. Ordinary builds still invoke neither deletion function.

- [ ] **Step 6: Update existing direct helper calls**

Every existing maintenance test invocation supplies non-overlapping protected roots unless the case explicitly tests overlap. Keep source/output sentinels as the standard protected values:

```powershell
-ProtectedPath @($ProjectARootPath, $ProjectAOutputPath)
```

Keep the exact exported command list unchanged.

- [ ] **Step 7: Run maintenance and wrapper regressions**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-maintenance.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile-behavior.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-streaming.tests.ps1
```

Expected: all six pass markers, protected sentinels survive, and no lock remains owned.

- [ ] **Step 8: Commit protected maintenance**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformCache.psm1 scripts/tests/build-platform-maintenance.tests.ps1
rtk git commit -m "Protect source and output during cache maintenance"
```

---

### Task 3: Full verification and final review

**Files:**

- Verify only; modify Task 1 or Task 2 files only if a regression is attributable to the safety changes.

**Interfaces:**

- Consumes: dual lock APIs and protected maintenance signatures from Tasks 1 and 2.
- Produces: final verification record and review disposition.

- [ ] **Step 1: Run every PowerShell contract**

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile-behavior.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-streaming.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-maintenance.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-real-editor-smoke.tests.ps1
```

Expected: all seven pass markers.

- [ ] **Step 2: Run focused .NET verification**

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildIsolationPathResolverTests|FullyQualifiedName~EditorPlatformBuildGraphWorkspaceFactoryTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorCliBuildRunner"
```

Expected: waiter tests pass. Record the known three committed-point-shadow native integration failures only if their retained logs again show the 254-character CMake path-limit and MSVC C1041 signature; do not count the prior shortened-temp timeouts as passes.

- [ ] **Step 3: Run scope and formatting audits**

```powershell
rtk rg -n "Copy-ProjectIntoIsolatedWorkspace|robocopy|BuildInvocationRootPath|IsolatedProjectPath" scripts/build-platform.ps1 scripts/build-platform
rtk rg -n "Guid.NewGuid" scripts/build-platform.ps1 scripts/build-platform
rtk git status --short
rtk git diff --check
```

Expected: no copy/GUID tokens, clean feature worktree, and no whitespace errors.

- [ ] **Step 4: Request final review**

Provide the reviewer the safety-design commit, both implementation commits, full test evidence, the environmental caveat, and the requirement that no exact `C:\dev\helworks\b` reference or main-worktree overlap exists. Resolve attributable findings through the same implementer and re-review before handoff.

- [ ] **Step 5: Do not create an empty verification commit**

If verification requires no code changes, preserve the two implementation commits as the final branch tip. If an attributable fix is required, rerun the affected focused suite plus Steps 1-3 and commit only the corrected Task 1/2 files as `Finish build-platform safety hardening`.
