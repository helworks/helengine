# BEPU Owned-World Dispose Codegen Repair Plan

**Goal:** Make `RegistrationState.Dispose()` visibly release its `NativeOwnedMember` on every normal exit while preserving idempotent detach/dispose behavior.

**Observed failure:** Direct physics3d ownership validation reached `CPPOWN007` at `RegistrationState.Dispose()`: codegen cannot prove that calling `ReplaceOwnedRuntimeWorld(this, null)` releases `RuntimeWorld` on every exit.

**Root cause:** Owned-member validation recognizes canonical cleanup operations on the member itself. Cleanup hidden behind the replacement helper plus the early disposed return is not a provable every-exit release.

## Task 1: Add a RED teardown contract

**File:** `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`

Add a focused source assertion requiring `RegistrationState.Dispose()` to detach the runtime when necessary and call `NativeOwnership.DisposeAndRelease(ref RuntimeWorld)` directly. Require the cleanup call to remain reachable on repeated disposal so every normal exit is safe.

## Task 2: Use canonical member cleanup

**File:** `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`

Restructure `RegistrationState.Dispose()` so it:

1. Performs one-time state changes and detaches `Core.PhysicsRuntime` when it references `RuntimeWorld`.
2. Always reaches `NativeOwnership.DisposeAndRelease(ref RuntimeWorld)` before normal return.
3. Remains idempotent and clears `SceneBindingRegistered` once.

Do not remove `NativeOwnedMember`, suppress the diagnostic, or duplicate ownership.

## Task 3: Verify through the direct native gate

Run focused BEPU registration tests and targeted physics tests. Then run the exact physics3d codegen invocation from the failed build with a verification output under the writable worktree. Monitor `codegen.exe` and WerFault. Only after direct codegen is clean, rerun the full DemoDisc Windows/DX11 Release build.
