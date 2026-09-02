# BEPU Attach Post-Transfer Codegen Repair Plan

**Goal:** Preserve `AttachRuntimeWorld` as a true caller-to-registration-state ownership transfer while preventing the transferred parameter from also escaping through the core attachment call.

**Observed failure:** The guarded DemoDisc Windows/DX11 Release build passed the former `CPPOWN001`, then codegen reported `CPPOWN006` on `AttachRuntimeWorld`: its declared `NativeTakesOwnership` contract contradicted inferred `Escapes` behavior.

**Root cause:** `AttachRuntimeWorld` transfers `world` into `RegistrationState.RuntimeWorld`, then uses the transferred parameter again in `core.AttachPhysicsRuntime(world)`. The core attachment is non-owning, but passing the original parameter through it makes the method summary escape-oriented and is also a post-transfer use. The registration state must remain the sole native owner.

## Task 1: Add a RED source contract

**File:** `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`

Add a focused source assertion proving that `AttachRuntimeWorld` attaches `state.RuntimeWorld` after `ReplaceOwnedRuntimeWorld(state, world)` and does not attach the original `world` parameter afterward.

Run the focused registration test class and confirm the new assertion fails against the current implementation.

## Task 2: Attach through the owner-held reference

**File:** `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`

After transferring `world` into the registration state, call `core.AttachPhysicsRuntime(state.RuntimeWorld)`. Do not weaken `NativeTakesOwnership`, remove `NativeOwnedMember`, suppress the diagnostic, or introduce a second owner.

## Task 3: Verify and rebuild

Run the focused BEPU registration tests and targeted physics registration/scene-load tests. Then rerun the exact DemoDisc Windows/DX11 Release build with the local platform manifest override. Monitor codegen for responsiveness, window titles, and WerFault; stop on any new ownership diagnostic rather than retrying blindly.
