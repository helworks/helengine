# HelPhysics migration for DemoDisc

## Objective and authorization

Finish the HelPhysics features needed to build and run DemoDisc through the engine's normal scene/runtime path. The user approved implementation on 2026-09-21 and requested written plans and Luna implementation agents. Preserve existing user edits in the engine editor/build configuration and DemoDisc assets. Do not edit generated C++.

## Confirmed gaps

- HelPhysics currently binds one rigid body and box collider per entity.
- DemoDisc physics stacks and TiltPlay use sphere colliders.
- TiltPlay coins and goals use trigger colliders and the engine trigger observer contract.
- DemoDisc's character slope scene authors a character controller with a box collider.
- HelPhysics has a standalone factory but lacks the normal scene-binding and packaged runtime registration path.

Static mesh and capsule requirements must be established from actual authored/runtime data before expanding scope; scene names alone are not evidence of collider usage.

## Implementation plans

1. [Shapes and triggers](2026-09-21-helphysics-shapes-and-triggers.md): extend the low-level world, shape storage and narrow phase while preserving box behavior.
2. [Scene binding and controllers](2026-09-21-helphysics-scene-runtime.md): bind supported components, synchronize lifecycle and controller motion, expose the normal runtime contracts.
3. [Build integration and validation](2026-09-21-helphysics-build-integration.md): select/package HelPhysics and verify DemoDisc builds and generated C++.

Luna agents own separate files where possible and agree on cross-layer API changes before editing shared files. The coordinator reviews changes and runs final focused validation. No commits, publication, generated-source rewriting or user-change cleanup are part of this task.

## Completion criteria

- Sphere/sphere and sphere/box contact behavior works in both pair orders, with filtering and valid inertia/bounds.
- Trigger enter/stay/exit events do not produce physical response and tolerate removal and scene changes.
- DemoDisc's authored character controller receives movement, gravity, grounded/support, slope and step behavior through HelPhysics.
- Scene load/unload and entity lifetime use one correctly owned HelPhysics runtime, with fixed-step scheduling and explicit rejection of unsupported components.
- Runtime selection and generated build inputs include HelPhysics without pulling in the legacy physics implementation as a hidden fallback.
- Focused managed tests pass, generated C++ validation is attempted, and a real DemoDisc build is attempted with precise reporting of any environmental blockers.

## Validation and output locations

Use the smallest meaningful physics, registration and build tests for each change, then the integration checks above. Keep all agent-owned build outputs, scripts and logs below `C:/dev/helworks/builds/helengine/physics-migration/`. Inspect the existing validation script before invoking it. Read applicable AGENTS.md files and prefix shell commands with `rtk`. Never capture screenshots.

The native validation script constrains output to the repository's `.validation/` directory. This workspace-owned location is also permitted when using that script; preserve its containment checks.

## Baseline findings

- .NET SDK 9.0.308 is installed.
- Initial tests failed compilation because `HelPhysicsBepuBenchmarkWorld3D` calls an outdated narrow-phase callback constructor. Update that fixture before running regressions.
- Core owns fixed-step accumulation; the adapter must use `core.PhysicsScheduler.StepSeconds`.
- Scene allocation must account for actual body counts instead of assuming the standalone 32-body capacity is sufficient.
- Runtime registrations must unsubscribe scene callbacks when replaced or disposed.
- Source audit confirmed a box controller, spheres, and coin/goal triggers in DemoDisc; no capsule/static-mesh collider authoring usage was found.

## Build validation status (runtime acceptance reopened)

- Full non-benchmark HelPhysics suite: **296 passed, 0 failed** (`C:/dev/helworks/builds/helengine/physics-migration/scene-full.trx`). Includes sphere contacts/settling, trigger filtering/removal/generation reuse, dynamic mutations, controller step/ramp/wall scenes, registration, rebind and faulted-world disposal.
- Editor integration: 60 generation tests, 15 module-cooking tests and 2 feature-discovery tests passed against the current DLLs. Fixed the test project legacy copy target so alternate artifact roots cannot be overwritten by stale default-bin assemblies.
- Legacy runtime manifest regression: 1/1 passed (`manifest-review.log`).
- Standalone native generation, ownership checks, audit, and MSVC compilation now pass. Verified object: `.validation/helphysics-build-integration-final-11/build/msvc/generated_unity.obj`. Native fixes include explicit body-handle equality/hash and preallocated trigger storage exposed through a supported read-only collection.
- Controller review implemented cached resolver lifetime, Z/diagonal blocking, bounded rotated top-face sampling, prior-binder reference cleanup, and whole-batch input validation before mutation. The final full-suite rerun passes after test-fixture corrections; TRX counters independently verified.
- DemoDisc Windows Debug native build succeeded. Executable: C:/dev/helprojs/demodisc/output/windows-helphysics-final-10/helengine_windows.exe. Both terminal proof files report succeeded/exitCode 0; native compilation and linking completed. The generated registration function includes HelPhysics and no BEPU files are generated, but the host did not call that function; runtime acceptance failed and is tracked below. Module dependency inputs survive conversion.

- Generator regression tests: 3/3 passed; imported-header regression verified failing on baseline. Four existing emitter fixture failures reproduce unchanged on baseline and are documented in the build plan.
- Interactive gameplay was not validated for this build. Compilation acceptance passed; runtime acceptance is reopened. Source changes remain uncommitted, including the csharpcodegen submodule changes.

The successful build used scripts/build-platform.ps1 with -BuildProfile debug and -Output C:/dev/helprojs/demodisc/output/windows-helphysics-final-10. Debug has no configured prebuild regeneration commands and preserves modified authored assets. The controller acceptance fixture uses DemoDisc's 18-degree box ramp, 0.9 x 1.5 x 0.9 controller, speed 3, step height 0.75, and snap distance 0.3.

Runtime correction: the Windows host never called the generated registration function, and native scene callbacks retained a dangling local reference. See [runtime acceptance correction](2026-09-21-helphysics-runtime-acceptance.md). A successful build did not establish functioning demos.

## Runtime correction result

Fixed Windows startup to invoke generated module registration and changed scene subscriptions to native-supported bound instance methods with idempotent registration state. Managed physics: 298/298 passed. Durable native runtime smoke: pass for real scene event binding, motion, floor contact, triggers and unload/reload. Native DemoDisc-style controller ramp: pass. Replacement Windows player built successfully and its startup log confirms generated module registration and completed frames.

Replacement executable: C:/dev/helprojs/demodisc/output/windows-helphysics-runtime-fixed/helengine_windows.exe. See the runtime acceptance plan for build proof, logs and the distinction between native behavioral tests and individual demos not manually played.