# Build Platform Waiter Output Handshake Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a waiter-controlled build retain exclusive ownership of its canonical output until that waiter has verified the exact successful invocation proof and required artifacts.

**Architecture:** `build-waiter` starts proof and artifact verification while its wrapper child is still running, then writes an invocation-specific acknowledgment. The wrapper keeps its existing output mutex until it validates and removes that acknowledgment, with a fixed 30-second timeout that rewrites terminal state to failed exit `10`; direct wrapper callers remain outside the protocol.

**Tech Stack:** Windows PowerShell 5.1, .NET 9/C#, xUnit, `System.Diagnostics.Process`, invocation-specific JSON proof files, exact-content acknowledgment files, and the existing named output mutex.

## Global Constraints

- No repository cloning, `robocopy`, artifact copies, snapshots, hashes, manifests, or invocation-scoped cache trees.
- `HELENGINE_BUILD_INVOCATION_ID` is a canonical lowercase GUID in `D` format.
- Waiter-controlled wrappers additionally receive the exact value `HELENGINE_BUILD_WAITER_PROTOCOL=ack-v1`.
- Proof and acknowledgment names are `.helengine-build-state.<canonical-guid>.json` and `.helengine-build-state.<canonical-guid>.ack` beneath the canonical output root; callers cannot supply either path.
- Proof `buildId` and acknowledgment content comparisons use ordinal case-sensitive equality.
- Acknowledgment content is exactly the canonical invocation ID with no newline, byte-order mark, or extra fields.
- Protocol validation and pre-existing acknowledgment rejection occur before lock acquisition or filesystem mutation.
- Lock order remains project global mutex, output global mutex, cache-local project file lock; release remains the reverse order.
- The acknowledgment wait is a fixed 30 seconds and does not consume or extend `-LockTimeout`.
- A failed editor build writes failed state, skips acknowledgment waiting, preserves its native exit code, and releases all locks.
- A successful editor build without an exact acknowledgment rewrites shared state and invocation proof to failed exit `10`, then releases all locks.
- After exact successful proof validation, artifact verification is attempted and acknowledgment is written even when artifact verification fails.
- Missing, malformed, stale, failed, foreign-ID, or wrong-case proof is never acknowledged.
- Direct wrapper invocations without protocol mode retain their current non-waiting behavior.
- Invocation proof remains durable; only an exact validated acknowledgment is removed.
- Every production change follows test-first red/green development and receives a task-level specification and quality review.

---

## File Responsibility Map

- `tools/build-waiter/BuildInvocationProofPaths.cs`: canonical invocation-ID validation plus deterministic proof and acknowledgment path derivation.
- `tools/build-waiter/BuildStateVerifier.cs`: JSON proof validation, including exact embedded `buildId` equality.
- `tools/build-waiter/BuildVerificationHandshake.cs`: proof-or-process-exit polling, state verification, artifact verification, and exact acknowledgment creation.
- `tools/build-waiter/BuildVerificationHandshakeResult.cs`: carries state, artifact, and acknowledgment outcomes without collapsing their precedence.
- `tools/build-waiter/BuildWaiter.cs`: child launch, protocol environment, diagnostic streaming, process completion, deferred cancellation, and final error precedence.
- `tools/build-waiter/Program.cs`: production dependency composition.
- `scripts/build-platform/BuildPlatformWaiterHandshake.psm1`: protocol parsing, canonical file paths, pre-existing acknowledgment rejection, bounded exact-content polling, and exact acknowledgment cleanup.
- `scripts/build-platform.ps1`: wrapper preflight, terminal-state lifecycle, acknowledgment wait placement, timeout state rewrite, and lock release.
- `tools/build-waiter.tests/BuildInvocationProofPathsTests.cs`: path and canonical-ID unit contract.
- `tools/build-waiter.tests/BuildVerificationHandshakeTests.cs`: coordinator acknowledgment and failure behavior.
- `tools/build-waiter.tests/BuildStateVerifierTests.cs`: exact embedded identity regression.
- `tools/build-waiter.tests/BuildWaiterTests.cs`: live-child ordering, race reproduction, process precedence, and cancellation drainage.
- `tools/build-waiter.tests/ProgramTests.cs`: executable composition through the complete acknowledgment protocol.
- `scripts/tests/build-platform-waiter-handshake.tests.ps1`: focused PowerShell module contract.
- `scripts/tests/build-platform-workspace.tests.ps1`: wrapper pre-mutation validation, direct-call compatibility, timeout state, and failed-editor behavior.
- `scripts/tests/build-platform-locking.tests.ps1`: cross-process proof that build B cannot acquire A's output until A receives the exact acknowledgment.
- `README.md`: public completion and timeout contract.

---

### Task 1: Canonical Invocation Proof and Acknowledgment Paths

**Files:**
- Create: `tools/build-waiter/BuildInvocationProofPaths.cs`
- Create: `tools/build-waiter.tests/BuildInvocationProofPathsTests.cs`
- Modify: `tools/build-waiter/BuildStateVerifier.cs:8-74`
- Modify: `tools/build-waiter.tests/BuildStateVerifierTests.cs:28-155`
- Modify: `tools/build-waiter.tests/BuildStateVerifierTests.cs:719-726`

**Interfaces:**
- Consumes: `string outputRootPath`, `string invocationId`.
- Produces: `BuildInvocationProofPaths.GetProofPath(string outputRootPath, string invocationId) -> string`.
- Produces: `BuildInvocationProofPaths.GetAcknowledgementPath(string outputRootPath, string invocationId) -> string`.
- Retains: `BuildStateVerifier.Verify(string outputRootPath, DateTime waiterStartedUtc, string expectedBuildId) -> BuildStateVerificationResult`.

- [ ] **Step 1: Write failing path and exact-identity tests**

Create `BuildInvocationProofPathsTests.cs` with table-driven canonical-ID rejection and exact path assertions:

```csharp
public sealed class BuildInvocationProofPathsTests {
    const string InvocationId = "b40ab19d-4d81-4db0-a0d4-9b818b49c7c0";

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("B40AB19D-4D81-4DB0-A0D4-9B818B49C7C0")]
    [InlineData(" b40ab19d-4d81-4db0-a0d4-9b818b49c7c0")]
    [InlineData("b40ab19d4d814db0a0d49b818b49c7c0")]
    [InlineData("not-a-guid")]
    public void GetProofPath_WhenInvocationIdIsNotCanonical_Throws(string invocationId) {
        Assert.Throws<ArgumentException>(() =>
            BuildInvocationProofPaths.GetProofPath("output", invocationId));
    }

    [Fact]
    public void Paths_WhenInputsAreCanonical_ReturnExpectedOutputChildren() {
        string output = Path.GetFullPath("output");
        Assert.Equal(
            Path.Combine(output, $".helengine-build-state.{InvocationId}.json"),
            BuildInvocationProofPaths.GetProofPath(output, InvocationId));
        Assert.Equal(
            Path.Combine(output, $".helengine-build-state.{InvocationId}.ack"),
            BuildInvocationProofPaths.GetAcknowledgementPath(output, InvocationId));
    }
}
```

Add a verifier regression that writes the proof at the lowercase expected filename but gives its JSON `buildId` an uppercase hexadecimal letter. Assert failure contains `build id`.

- [ ] **Step 2: Run the focused tests and verify red**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore --filter "FullyQualifiedName~BuildInvocationProofPathsTests|FullyQualifiedName~BuildStateVerifierTests.Verify_WhenBuildIdDiffersOnlyByCase_ReturnsFailure"
```

Expected: compilation fails because `BuildInvocationProofPaths` does not exist; after the test class compiles, the verifier case fails because it currently uses `OrdinalIgnoreCase`.

- [ ] **Step 3: Implement the path helper and exact proof identity**

Implement the helper with this public surface and fixed-child containment check:

```csharp
namespace helengine.tools.buildwaiter {
    public static class BuildInvocationProofPaths {
        public static string GetProofPath(string outputRootPath, string invocationId) {
            return GetInvocationPath(outputRootPath, invocationId, ".json");
        }

        public static string GetAcknowledgementPath(string outputRootPath, string invocationId) {
            return GetInvocationPath(outputRootPath, invocationId, ".ack");
        }

        static string GetInvocationPath(string outputRootPath, string invocationId, string suffix) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (!Guid.TryParseExact(invocationId, "D", out Guid parsedInvocationId)
                || !string.Equals(invocationId, parsedInvocationId.ToString("D"), StringComparison.Ordinal)) {
                throw new ArgumentException(
                    "Invocation id must be a canonical lowercase GUID in D format.",
                    nameof(invocationId));
            }

            string outputRoot = Path.GetFullPath(outputRootPath);
            string candidate = Path.GetFullPath(Path.Combine(
                outputRoot,
                ".helengine-build-state." + invocationId + suffix));
            string relative = Path.GetRelativePath(outputRoot, candidate);
            if (Path.IsPathRooted(relative)
                || string.Equals(relative, "..", StringComparison.Ordinal)
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                throw new ArgumentException("Invocation file must remain beneath the output root.", nameof(outputRootPath));
            }
            return candidate;
        }
    }
}
```

Replace `BuildStateVerifier`'s private filename constants and `Path.Combine` call with `BuildInvocationProofPaths.GetProofPath`. Change only the embedded identity comparison to:

```csharp
!string.Equals(document.BuildId, expectedBuildId, StringComparison.Ordinal)
```

Keep status parsing case-insensitive and retain all timestamp, exit-code, and freshness checks.

- [ ] **Step 4: Run all path and state-verifier tests**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore --filter "FullyQualifiedName~BuildInvocationProofPathsTests|FullyQualifiedName~BuildStateVerifierTests"
```

Expected: all selected tests pass; malformed IDs throw before path construction and the uppercase embedded ID fails validation.

- [ ] **Step 5: Commit Task 1**

```powershell
rtk git add -- tools/build-waiter/BuildInvocationProofPaths.cs tools/build-waiter/BuildStateVerifier.cs tools/build-waiter.tests/BuildInvocationProofPathsTests.cs tools/build-waiter.tests/BuildStateVerifierTests.cs
rtk git commit -m "Use exact invocation proof identities"
```

---

### Task 2: Verify and Acknowledge While the Child Is Active

**Files:**
- Create: `tools/build-waiter/BuildVerificationHandshake.cs`
- Create: `tools/build-waiter/BuildVerificationHandshakeResult.cs`
- Create: `tools/build-waiter.tests/BuildVerificationHandshakeTests.cs`
- Modify: `tools/build-waiter/BuildWaiter.cs:8-115`
- Modify: `tools/build-waiter/Program.cs:22-43`
- Modify: `tools/build-waiter.tests/BuildWaiterTests.cs`
- Modify: `tools/build-waiter.tests/ProgramTests.cs:19-56`

**Interfaces:**
- Consumes: Task 1's `BuildInvocationProofPaths.GetAcknowledgementPath(string, string)` and existing state/artifact verifiers.
- Produces: `BuildVerificationHandshake(BuildStateVerifier stateVerifier, BuildArtifactVerifier artifactVerifier, TimeSpan proofPollInterval)`.
- Produces: `Task<BuildVerificationHandshakeResult> VerifyAndAcknowledgeAsync(string outputRootPath, string[] requiredArtifactRelativePaths, DateTime waiterStartedUtc, string expectedBuildId, Task childExitTask)`.
- Produces result properties `StateVerificationResult`, nullable `ArtifactVerificationResult`, and nullable `AcknowledgementFailureMessage`.
- Changes `BuildWaiter` construction to `BuildWaiter(BuildVerificationHandshake verificationHandshake)`.

- [ ] **Step 1: Write failing coordinator tests**

Create `BuildVerificationHandshakeTests.cs`. Use a 10-millisecond poll interval and a `TaskCompletionSource` with `RunContinuationsAsynchronously` so each test controls whether the child is still active.

The success test writes a current exact proof and non-empty artifact, starts `VerifyAndAcknowledgeAsync`, waits for the acknowledgment path, and asserts its bytes decode to the exact invocation ID with length `36`:

```csharp
TaskCompletionSource childExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
Task<BuildVerificationHandshakeResult> verification = CreateHandshake().VerifyAndAcknowledgeAsync(
    outputRootPath, ["game.iso"], waiterStartedUtc, InvocationId, childExit.Task);

await WaitForFileAsync(BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, InvocationId));
byte[] acknowledgementBytes = File.ReadAllBytes(
    BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, InvocationId));
Assert.Equal(InvocationId, System.Text.Encoding.ASCII.GetString(acknowledgementBytes));
Assert.Equal(36, acknowledgementBytes.Length);

BuildVerificationHandshakeResult result = await verification;
Assert.True(result.StateVerificationResult.Succeeded);
Assert.True(result.ArtifactVerificationResult.Succeeded);
Assert.Null(result.AcknowledgementFailureMessage);
```

Add one theory for missing, malformed, stale, failed, foreign-ID, and wrong-case proof. Complete `childExit` after the invalid proof is in place, then assert no acknowledgment and that the original state diagnostic survives.

Add one theory for missing, empty, stale, rooted, and escaping required artifact paths. Each fixture has a valid proof; assert the exact acknowledgment exists and `ArtifactVerificationResult.Message` retains the existing detailed failure.

Add an acknowledgment-write failure case by creating a directory at the computed `.ack` path before valid proof publication. Assert `AcknowledgementFailureMessage` names the acknowledgment path and the state/artifact results are preserved.

- [ ] **Step 2: Run coordinator tests and verify red**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore --filter FullyQualifiedName~BuildVerificationHandshakeTests
```

Expected: compilation fails because the coordinator and result types do not exist.

- [ ] **Step 3: Implement the focused coordinator**

Implement the result as a non-collapsing value holder:

```csharp
public sealed class BuildVerificationHandshakeResult {
    public BuildVerificationHandshakeResult(
        BuildStateVerificationResult stateVerificationResult,
        BuildArtifactVerificationResult artifactVerificationResult,
        string acknowledgementFailureMessage) {
        StateVerificationResult = stateVerificationResult
            ?? throw new ArgumentNullException(nameof(stateVerificationResult));
        ArtifactVerificationResult = artifactVerificationResult;
        AcknowledgementFailureMessage = acknowledgementFailureMessage;
    }

    public BuildStateVerificationResult StateVerificationResult { get; }
    public BuildArtifactVerificationResult ArtifactVerificationResult { get; }
    public string AcknowledgementFailureMessage { get; }
}
```

Implement coordinator polling in this order:

```csharp
while (true) {
    BuildStateVerificationResult state = StateVerifier.Verify(
        outputRootPath, waiterStartedUtc, expectedBuildId);
    if (state.Succeeded) {
        BuildArtifactVerificationResult artifacts = ArtifactVerifier.Verify(
            outputRootPath, requiredArtifactRelativePaths, waiterStartedUtc);
        string acknowledgementFailure = WriteAcknowledgement(
            outputRootPath, expectedBuildId);
        return new BuildVerificationHandshakeResult(state, artifacts, acknowledgementFailure);
    }

    if (childExitTask.IsCompleted) {
        return new BuildVerificationHandshakeResult(state, null, null);
    }

    await Task.WhenAny(childExitTask, Task.Delay(ProofPollInterval));
}
```

`WriteAcknowledgement` must use `BuildInvocationProofPaths.GetAcknowledgementPath`, `FileMode.CreateNew`, `FileAccess.Write`, `FileShare.Read`, ASCII bytes, and `Flush(true)`. Catch `UnauthorizedAccessException` and `IOException` and return one precise protocol diagnostic; do not overwrite a pre-existing path. Validate a positive poll interval and non-null dependencies in the constructor.

- [ ] **Step 4: Run coordinator tests and verify green**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore --filter FullyQualifiedName~BuildVerificationHandshakeTests
```

Expected: all coordinator cases pass, invalid proof never acknowledges, every artifact failure acknowledges, and acknowledgment bytes are exact.

- [ ] **Step 5: Write failing live-process ordering tests**

Update `BuildWaiterTests`' PowerShell child fixture to assert `HELENGINE_BUILD_WAITER_PROTOCOL` is exactly `ack-v1`. On successful proof publication it waits for the exact `.ack` file, reads it with `[IO.File]::ReadAllText`, and exits only after exact ordinal content is present. Cap every test-child acknowledgment wait at five seconds and exit `92` on expiry so the intended red run terminates promptly.

Add the deterministic artifact race:

```powershell
[IO.File]::WriteAllText($artifactPath, 'artifact-a')
[IO.File]::WriteAllText($proofPath, $stateJson)
$ackStopwatch = [Diagnostics.Stopwatch]::StartNew()
while (-not (Test-Path -LiteralPath $ackPath) -and $ackStopwatch.Elapsed -lt [TimeSpan]::FromSeconds(5)) {
    Start-Sleep -Milliseconds 10
}
if (-not (Test-Path -LiteralPath $ackPath)) { exit 92 }
if ([IO.File]::ReadAllText($ackPath) -cne $env:HELENGINE_BUILD_INVOCATION_ID) { exit 91 }
[IO.File]::WriteAllText($artifactPath, 'artifact-b-after-ack')
exit 0
```

Assert waiter success and final artifact content `artifact-b-after-ack`. This is red against the current waiter because it waits for child exit before verification, while the child waits for acknowledgment.

Add process-result cases for child exit `7` before proof, child exit `0` without proof, and process start failure. Add two valid-proof cases with forced acknowledgment-write failure: child exit `0` reports the protocol diagnostic, while child exit `9` preserves exit `9` as the more authoritative result. Add a cancellation case where cancellation is requested after proof publication; the child must still receive acknowledgment, exit, and close stdout/stderr before `OperationCanceledException` is observed.

Update `ProgramTests`' successful PowerShell child to use the same bounded five-second wait and validate the exact acknowledgment before exiting.

- [ ] **Step 6: Run live-process tests and verify red**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore --filter "FullyQualifiedName~BuildWaiterTests|FullyQualifiedName~ProgramTests"
```

Expected: the new success/race cases time out or fail because `BuildWaiter` does not set the protocol and does not verify until child exit.

- [ ] **Step 7: Integrate the coordinator into `BuildWaiter`**

Set both child environment values before start:

```csharp
startInfo.Environment["HELENGINE_BUILD_INVOCATION_ID"] = invocationId;
startInfo.Environment["HELENGINE_BUILD_WAITER_PROTOCOL"] = "ack-v1";
```

Start the uncancelled process-exit task and coordinator immediately after output readers start:

```csharp
Task processExitTask = process.WaitForExitAsync(CancellationToken.None);
Task<BuildVerificationHandshakeResult> handshakeTask = VerificationHandshake.VerifyAndAcknowledgeAsync(
    options.OutputRootPath,
    options.RequiredArtifactRelativePaths,
    buildStartedUtc,
    invocationId,
    processExitTask);
```

Continue emitting the ten-second status message while `processExitTask` is active. Await process exit, both redirected-stream completion tasks, and the handshake result before returning or throwing cancellation. Apply final precedence exactly:

```csharp
if (process.ExitCode != 0) { return childFailure; }
cancellationToken.ThrowIfCancellationRequested();
if (!string.IsNullOrWhiteSpace(handshake.AcknowledgementFailureMessage)) { return protocolFailure; }
if (!handshake.StateVerificationResult.Succeeded) { return stateFailure; }
if (!handshake.ArtifactVerificationResult.Succeeded) { return artifactFailure; }
return verifiedSuccess;
```

This intentionally defers cancellation until the child has exited and both streams have drained, preventing waiter cancellation from abandoning a wrapper in its bounded acknowledgment phase. Compose production dependencies in `Program.RunAsync` with a 25-millisecond proof poll interval.

- [ ] **Step 8: Run all build-waiter tests**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore
```

Expected: the complete build-waiter test project passes. The race test proves verification precedes acknowledgment and child-side artifact replacement.

- [ ] **Step 9: Commit Task 2**

```powershell
rtk git add -- tools/build-waiter tools/build-waiter.tests
rtk git commit -m "Verify build output before wrapper release"
```

---

### Task 3: Hold the Wrapper Output Mutex Until Exact Acknowledgment

**Files:**
- Create: `scripts/build-platform/BuildPlatformWaiterHandshake.psm1`
- Create: `scripts/tests/build-platform-waiter-handshake.tests.ps1`
- Modify: `scripts/build-platform.ps1:40-47`
- Modify: `scripts/build-platform.ps1:235-374`
- Modify: `scripts/build-platform.ps1:487-588`
- Modify: `scripts/tests/build-platform-workspace.tests.ps1:45-140`
- Modify: `scripts/tests/build-platform-workspace.tests.ps1:1100-1205`
- Modify: `scripts/tests/build-platform-locking.tests.ps1:7-225`
- Modify: `scripts/tests/build-platform-locking.tests.ps1:897-974`

**Interfaces:**
- Consumes: environment values `HELENGINE_BUILD_INVOCATION_ID` and `HELENGINE_BUILD_WAITER_PROTOCOL`.
- Produces: `Resolve-BuildPlatformWaiterHandshake -ProtocolValue <string|null> -InvocationIdWasSupplied <bool> -InvocationId <string> -OutputRootPath <string> -> psobject` with `Enabled`, `InvocationId`, `ProofPath`, and `AcknowledgementPath`.
- Produces: `Wait-BuildPlatformWaiterAcknowledgement -Handshake <psobject> -Timeout <TimeSpan> -> bool`.
- Produces: `Remove-BuildPlatformWaiterAcknowledgement -Handshake <psobject>`.
- Retains: wrapper lock acquisition and reverse release order.

- [ ] **Step 1: Write failing PowerShell module tests**

Create `build-platform-waiter-handshake.tests.ps1` and assert the module exports exactly the three public functions above. Cover:

```powershell
$Handshake = Resolve-BuildPlatformWaiterHandshake `
    -ProtocolValue 'ack-v1' `
    -InvocationIdWasSupplied $true `
    -InvocationId 'b40ab19d-4d81-4db0-a0d4-9b818b49c7c0' `
    -OutputRootPath $OutputRoot

if (-not $Handshake.Enabled) { throw 'ack-v1 was not enabled.' }
if ($Handshake.ProofPath -cne (Join-Path $OutputRoot '.helengine-build-state.b40ab19d-4d81-4db0-a0d4-9b818b49c7c0.json')) { throw 'Proof path changed.' }
if ($Handshake.AcknowledgementPath -cne (Join-Path $OutputRoot '.helengine-build-state.b40ab19d-4d81-4db0-a0d4-9b818b49c7c0.ack')) { throw 'Acknowledgment path changed.' }
```

Assert direct mode (`-ProtocolValue $null`) returns `Enabled = $false`. Assert `ACK-V1`, an absent caller-supplied ID, malformed/uppercase/padded IDs, and a pre-existing acknowledgment throw before creating any additional path.

For wrong ID, wrong case, partial ID, and newline-suffixed content, call `Wait-BuildPlatformWaiterAcknowledgement` with 100 milliseconds and assert `$false`. For exact no-newline content assert `$true`, then call removal and assert only that exact acknowledgment is gone while the proof and sibling sentinel remain.

- [ ] **Step 2: Run module tests and verify red**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-waiter-handshake.tests.ps1
```

Expected: import fails because `BuildPlatformWaiterHandshake.psm1` does not exist.

- [ ] **Step 3: Implement the PowerShell handshake module**

Use `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'`. `Resolve-BuildPlatformWaiterHandshake` must:

1. select disabled direct mode only when `ProtocolValue` is absent, and select enabled mode only for exact `ack-v1`;
2. require `InvocationIdWasSupplied = $true` in enabled protocol mode;
3. in both modes, validate and normalize the ID with `Guid.TryParseExact(..., 'D', ...)` plus `-ceq` against `ToString('D')`;
4. canonicalize the output with `[IO.Path]::GetFullPath` and trim only trailing separators beyond the root;
5. construct the fixed proof and acknowledgment child names;
6. verify each candidate begins with the canonical output plus one directory separator using `OrdinalIgnoreCase`;
7. in enabled mode, reject a pre-existing acknowledgment before returning;
8. return the normalized ID and both canonical paths in both modes, with `Enabled` distinguishing direct and waiter-controlled lifecycle behavior.

Implement exact polling without rewriting the acknowledgment:

```powershell
$Stopwatch = [Diagnostics.Stopwatch]::StartNew()
do {
    if (Test-Path -LiteralPath $Handshake.AcknowledgementPath -PathType Leaf) {
        try {
            $Contents = [IO.File]::ReadAllText($Handshake.AcknowledgementPath)
            if ($Contents -ceq $Handshake.InvocationId) { return $true }
        } catch [IO.IOException] {
        } catch [UnauthorizedAccessException] {
        }
    }
    if ($Stopwatch.Elapsed -ge $Timeout) { return $false }
    Start-Sleep -Milliseconds 25
} while ($true)
```

Removal must reread and require exact content immediately before `Remove-Item -LiteralPath`; never enumerate or wildcard-delete acknowledgments.

- [ ] **Step 4: Run module tests and verify green**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-waiter-handshake.tests.ps1
```

Expected: `WAITER_HANDSHAKE_TEST_PASS` and no leftover exact acknowledgment.

- [ ] **Step 5: Write failing wrapper preflight and lifecycle tests**

Extend the workspace harness so each invocation can explicitly set or clear both waiter environment variables and restores the parent process environment afterward.

Add pre-mutation cases:

- `ack-v1` with no invocation ID exits `2`, does not reach fake dotnet, and creates neither cache nor output;
- `ACK-V1` with a valid ID exits `2` with the same no-mutation assertions;
- a pre-existing exact acknowledgment exits `2`, leaves that file byte-for-byte unchanged, and creates no state/cache files;
- no protocol value completes normally without a 30-second delay and leaves no acknowledgment;
- fake editor exit `17` under `ack-v1` completes without acknowledgment waiting, writes failed proof, and returns `17`.

Add a timeout case with a released successful fake editor, exact `ack-v1`, and no acknowledgment. Allow 40 seconds, then assert wrapper exit `10`, shared state and the exact proof both contain `status = failed` and `exitCode = 10`, and unrelated files beneath output remain.

Extend the locking harness control with nullable `InvocationId` and `WaiterProtocol` values. Add a same-output case that:

1. starts A in `ack-v1` and holds its fake editor;
2. starts direct-mode B against the same output and proves B is blocked;
3. releases A's editor and waits for A's successful proof;
4. proves A remains active and B still cannot reach fake dotnet;
5. writes wrong-ID, wrong-case, partial, and newline-suffixed acknowledgment content in turn, proving neither A nor B proceeds;
6. writes the exact no-newline acknowledgment;
7. proves A exits zero, removes the acknowledgment, and only then B reaches fake dotnet.

Add a separate same-output timeout sequence: A publishes successful proof in `ack-v1`, B is already waiting on the same output, and no acknowledgment is written. Assert A exits `10` after approximately 30 seconds, A's durable proof records failed exit `10`, and B reaches fake dotnet only after A's timeout release. The workspace timeout case remains the authoritative assertion for A's shared-state rewrite before another build can replace compatibility state.

- [ ] **Step 6: Run wrapper tests and verify red**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
```

Expected: protocol preflight cases are accepted or ignored, A releases its output mutex immediately after proof publication, and B reaches fake dotnet before acknowledgment.

- [ ] **Step 7: Integrate protocol preflight before lock acquisition**

Import the new module beside the existing build-platform modules. Record whether `HELENGINE_BUILD_INVOCATION_ID` was supplied before generating a direct-call ID. After output canonicalization, cache layout resolution, overlap validation, and additional-argument validation—but before lock metadata or acquisition—call:

```powershell
$WaiterProtocolValue = if (Test-Path -LiteralPath 'Env:HELENGINE_BUILD_WAITER_PROTOCOL') {
    $env:HELENGINE_BUILD_WAITER_PROTOCOL
} else {
    $null
}
$Handshake = Resolve-BuildPlatformWaiterHandshake `
    -ProtocolValue $WaiterProtocolValue `
    -InvocationIdWasSupplied $InvocationBuildIdWasSupplied `
    -InvocationId $InvocationBuildId `
    -OutputRootPath $ResolvedOutputPath
$BuildId = $Handshake.InvocationId
$StateFilePath = Join-Path $ResolvedOutputPath '.helengine-build-state.json'
$TerminalProofPath = $Handshake.ProofPath
```

Catch preflight validation locally, print its precise diagnostic, and exit `2`. Remove the later duplicate proof-path construction. Do not change or restore either caller-owned waiter environment variable in the wrapper.

- [ ] **Step 8: Hold locks through acknowledgment and rewrite timeout state**

After both terminal state writes have succeeded, and only when the editor result is successful and `$Handshake.Enabled`, call the wait function with `[TimeSpan]::FromSeconds(30)` while project mutex, output mutex, and project file lock are all still owned.

On exact acknowledgment, call exact removal before entering the existing reverse lock-release chain. On timeout or removal error:

```powershell
$BuildTerminalStatus = 'failed'
$BuildTerminalExitCode = 10
$BuildCompletedUtc = [DateTime]::UtcNow.ToString('o')
$TerminalExitOverrideRequired = $true
```

Rewrite shared state and exact proof with those values in separate guarded attempts. Print each rewrite failure but always continue to cache metadata and reverse lock release. At the end of `finally`, execute `exit $BuildTerminalExitCode` when either terminal state writing failed or `$TerminalExitOverrideRequired` is true. Failed editor status must bypass the wait and retain its original code.

- [ ] **Step 9: Run all focused PowerShell tests**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-waiter-handshake.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-streaming.tests.ps1
```

Expected: `WAITER_HANDSHAKE_TEST_PASS`, `WORKSPACE_TEST_PASS`, `LOCKING_TEST_PASS`, and `STREAMING_TEST_PASS`. The timeout case takes approximately 30 seconds and ends with failed exit `10` state rather than a stuck lock.

- [ ] **Step 10: Commit Task 3**

```powershell
rtk git add -- scripts/build-platform.ps1 scripts/build-platform/BuildPlatformWaiterHandshake.psm1 scripts/tests/build-platform-waiter-handshake.tests.ps1 scripts/tests/build-platform-workspace.tests.ps1 scripts/tests/build-platform-locking.tests.ps1
rtk git commit -m "Hold build output through waiter verification"
```

---

### Task 4: Document and Verify the Complete Handshake

**Files:**
- Modify: `README.md:45-82`
- Verify only: all Task 1-3 production and test files.

**Interfaces:**
- Consumes: Task 1 exact paths, Task 2 active-child verification, and Task 3 output-lock acknowledgment lifecycle.
- Produces: documented waiter-controlled completion semantics and final branch verification evidence.

- [ ] **Step 1: Update the public completion contract**

State in `README.md` that waiter-controlled same-output builds stay serialized until the waiter validates exact terminal proof and attempts required-artifact verification; the wrapper then consumes an exact acknowledgment. State that direct wrapper calls do not use this phase and that a missing exact acknowledgment converts an otherwise successful wrapper into failed exit `10` after 30 seconds.

Do not document the acknowledgment as a user-authored configuration mechanism; both environment variables remain an internal wrapper/waiter contract.

- [ ] **Step 2: Run every PowerShell contract**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-waiter-handshake.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-workspace.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-profile-behavior.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-streaming.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-locking.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-maintenance.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-real-editor-smoke-ownership.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-native-cache-smoke-ownership.tests.ps1
```

Expected: all nine pass markers. No protocol file remains after a successful waiter-controlled invocation.

- [ ] **Step 3: Run build-waiter and editor regression tests**

Run:

```powershell
rtk dotnet test tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --no-restore
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildIsolationPathResolverTests|FullyQualifiedName~EditorPlatformBuildGraphWorkspaceFactoryTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorCliBuildRunner"
```

Expected: all build-waiter tests pass. The editor filter passes except that the three existing point-shadow native tests may retain only their documented CMake/MSVC `C1041` failure at 254-character `%TEMP%` object paths; preserve logs and do not classify any other signature as the known exception.

- [ ] **Step 4: Run real-editor and native stable-cache smoke tests**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-real-editor-smoke.tests.ps1
rtk powershell -NoProfile -ExecutionPolicy Bypass -File scripts\tests\build-platform-native-cache-smoke.tests.ps1
```

Expected: `REAL_EDITOR_SMOKE_TEST_PASS` and `NATIVE_CACHE_SMOKE_TEST_PASS`. Both invocations reuse stable cache identities, and the native output artifact is non-empty.

- [ ] **Step 5: Run scope, formatting, and worktree audits**

Run:

```powershell
rtk rg -n "git\s+clone|robocopy|Copy-ProjectIntoIsolatedWorkspace|BuildInvocationRootPath|IsolatedProjectPath|C:\\dev\\helworks\\b" scripts tools README.md
rtk rg -n "HELENGINE_BUILD_WAITER_PROTOCOL|\.helengine-build-state\..*\.ack" scripts tools README.md
rtk git diff --check
rtk git status --short --branch
```

Expected: the prohibited clone/copy/legacy-path scan has no matches; protocol matches are limited to the new waiter, module, wrapper tests, and documentation; `git diff --check` is silent; only the README change is uncommitted at this point.

- [ ] **Step 6: Commit documentation**

```powershell
rtk git add -- README.md
rtk git commit -m "Document waiter output handshake"
```

- [ ] **Step 7: Request an independent whole-branch review**

Use `superpowers:requesting-code-review` with the approved design commit `343bcd9f`, all Task 1-4 commits, and the complete test record. Require the reviewer to inspect:

- proof and acknowledgment identity are ordinal and canonical;
- verification and acknowledgment occur before A releases the output mutex;
- B cannot enter the same canonical output before that release;
- invalid proof never acknowledges;
- artifact failure does acknowledge and retains its detailed waiter failure;
- timeout and acknowledgment-removal errors rewrite exit `10` state and release every lock;
- failed editors bypass waiting and retain native exit codes;
- cancellation cannot stop stream drainage or strand the wrapper in acknowledgment wait;
- no clone, broad cleanup, artifact copy, or invocation cache was introduced.

Resolve attributable findings in the owning task's files, rerun that task's focused suite plus Steps 2-5, and request one clean re-review.

- [ ] **Step 8: Confirm branch and user-change isolation**

Run:

```powershell
rtk git status --short --branch
rtk git log --oneline -8
```

Expected: feature worktree is clean on `feature/build-platform-direct-source`; no merge, push, or modification of the user's main-worktree changes has occurred. Report the final tip, test evidence, known point-shadow exception only if reproduced with the exact signature, and integration options.
