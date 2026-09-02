# BEPU Replacement-World Codegen Ownership Repair Plan

**Goal:** Make the BEPU runtime-world replacement path express its real native ownership transfers so the DemoDisc Release codegen pass succeeds without changing managed lifetime behavior.

**Failure:** DemoDisc's fresh Windows/DX11 Release build reaches `helengine.physics3d` codegen and stops with `CPPOWN001` because the owned local `replacementWorld` crosses `ReplaceOwnedRuntimeWorld` through an unannotated parameter.

**Constraints:** Preserve the existing one-owner lifetime model, do not weaken or suppress native ownership diagnostics, keep the change inside the engine BEPU registration code and tests, and do not touch the unrelated dirty `AutomaticScriptComponentRuntimeDeserializer.cs` file.

## Task 1: Capture the ownership contract in a failing test

**Files:**

- Modify: `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`

Add reflection assertions that:

1. `RegistrationState.RuntimeWorld` is a native-owned member.
2. `ReplaceOwnedRuntimeWorld` takes ownership of its replacement parameter.
3. Public `AttachRuntimeWorld` takes ownership of a caller-supplied world.

Run the focused BEPU registration test class and confirm the new assertions fail before production changes.

## Task 2: Make adoption and reattachment semantically distinct

**Files:**

- Modify: `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`
- Modify only if required by the focused contract: `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`

Declare the registration state's reserved world as native-owned. Mark only true adoption boundaries with `NativeTakesOwnership`:

- the replacement helper adopts a newly supplied world;
- public `AttachRuntimeWorld` adopts a caller-supplied world.

Do not pass a world already owned by `RegistrationState` through an ownership-taking method merely to reattach it. Reattach that reserved world through the borrowed `Core.AttachPhysicsRuntime` boundary. Keep replacement disposal, same-world idempotence, lazy scene attachment, and core teardown behavior unchanged.

If `Register(Core)` attempts to adopt an already attached borrowed runtime, remove that implicit claim rather than fabricating a transfer contract: supported ownership enters through the scheduled-world creation path, lazy creation path, or explicit `AttachRuntimeWorld` adoption API.

## Task 3: Verify managed lifetime behavior and native translation

Run:

1. The focused `BepuRuntimeComponentRegistrationTests` class.
2. The complete `helengine.bepu.tests` project.
3. The focused physics/runtime tests affected by generated module registration.
4. The DemoDisc Windows/DX11 Release build with its exact local platform manifest override.

During codegen, monitor every launched `codegen.exe` for responsiveness and Windows Error Reporting/Application Error UI. If a UI fault appears, terminate that launched process immediately and diagnose before retrying.

The repair is accepted only when the prior `CPPOWN001` is gone, the build produces a fresh nonempty `helengine_windows.exe`, and the build-state file records success.
