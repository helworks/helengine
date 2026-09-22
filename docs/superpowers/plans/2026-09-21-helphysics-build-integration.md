# Plan: HelPhysics packaging and DemoDisc validation

## Ownership and scope

Luna build owns runtime registration, module selection, native compatibility and validation. Luna scene owns the native module dependency correction. The coordinator writes plans, reviews source and runs final verification. Preserve user changes and never edit generated C++.

## Implementation

1. Register HelPhysics for authored rigid bodies, box/sphere colliders, controllers and trigger observers. Keep BEPU available only through explicit legacy activation.
2. Generate only HelPhysics for default physics scenes; do not unconditionally convert the legacy helengine.physics3d/BEPU graph. Explicit additional legacy projects remain supported through GeneratedCoreProjectPaths. Bind scenes transactionally, size fixed-capacity worlds from scene contents and use the core fixed-step scheduler.
3. Make source compatible with native ownership and exception-free conversion. Use stable body-handle equality/hashing and supported preallocated trigger collection storage.
4. Honor the runtime-profiler feature switch in both core and HelPhysics provider contracts.
5. Preserve authored module dependencies in temporary native-conversion projects. Prepare all projects in the selected dependency closure, including empty modules, and emit their project references before conversion starts. Store these input projects in a sibling directory outside converter outputs, because conversion clears each output directory recursively.
6. Correct native header dependency emission for imported shared types used in method signatures. Add a focused regression that fails before the correction and passes afterward; do not rely on incidental unity ordering or legacy backend includes.
7. Include the default HelPhysics provider assembly explicitly in runtime manifest discovery and feature requirement discovery. Scene component contracts reside in a separate assembly, so AppDomain loaded assemblies alone can omit the provider. Verify emitted registration calls HelPhysics and excludes the legacy default.
8. Validate focused managed tests, standalone generated C++ and a complete DemoDisc Windows Debug build.

The dependency correction addresses the real menu compilation failure: TiltPlay's temporary conversion project previously omitted its declared DemoDisc dependency. The ordinary managed projects already built correctly. The full build after removing the legacy backend exposed a separate converter header dependency gap: imported shared physics return types appear in a component header without declarations. Luna owns a generic generator correction and focused regression.

## Validation

- Editor generation: 60/60 passed after explicit provider discovery. Feature requirement discovery: 2/2 passed. Tests now skip the legacy default-bin DLL copy for alternate artifact roots, preventing stale assemblies from overwriting the tested build. Logs: bootstrap-root-review-final.log and feature-discovery-root-review-final.log.
- Generator regressions: 3/3 passed (imported headers, native exception identity and duplicate generic base headers). The imported-header regression fails on the unchanged baseline and passes with the fix. Broader emitter check: 6/10 pass; the same four existing fixture failures also occur on the baseline (5/10 pass there, with the new regression accounting for the additional failure). Logs: emitter-root-review.log and emitter-baseline-root-review.log.
- Runtime manifest regression: 1/1 passed.
- Profiler-enabled runtime tests: 3/3 passed.
- Profiler feature-boundary source audit: 1/1 passed.
- Native module project tests: 15/15 passed against the current editor DLL (code-cook-root-review-final.log), including dependency paths, an empty dependency module, and a runner that clears output directories between conversions. The small two-project converter case also succeeds with inputs isolated from output, with or without restore.
- Full HelPhysics non-benchmark suite: 296/296 passed.
- Standalone native validator: exit 0, including ownership checks, emitted-file audit and MSVC compilation. Object: .validation/helphysics-build-integration-final-11/build/msvc/generated_unity.obj.
- Managed DemoDisc and TiltPlay validation: zero errors and warnings.
- DemoDisc Windows Debug final9 verifies the shared-type header includes and actual HelPhysics startup registration, with no BEPU registration. The resolver now preserves framework-exception identity, removes duplicate unqualified generic bases, and retains native framework-generic mappings. Standalone native validation passes with these corrections. Full final10 succeeded: native compilation and linking completed, and helengine_windows.exe was packaged. Both terminal proof files report status succeeded and exitCode 0 (build 8f5d9c6d-017a-4cbc-80f2-f6696fb25b13). The outer redirected PowerShell invocation reported 1; the wrapper terminal proofs, native log and packaged executable establish the successful build. Log: C:/dev/helworks/builds/helengine/physics-migration/demodisc-final10.log.

## Final build command

Run scripts/build-platform.ps1 with -Project C:/dev/helprojs/demodisc/project.heproj -Platform windows -BuildProfile debug -Output C:/dev/helprojs/demodisc/output/windows-helphysics-final-10, using rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File.

Debug has no configured authored-asset regeneration steps. Do not use asset cleanup or generated-source rewriting to obtain a successful build. Verify the resulting executable and HelPhysics bootstrap selection; distinguish build validation from an interactive gameplay smoke test.

## Completed acceptance

- Executable: C:/dev/helprojs/demodisc/output/windows-helphysics-final-10/helengine_windows.exe (1,596,928 bytes).
- GeneratedRuntimeModuleRegistration.cpp includes HelPhysicsRuntimeComponentRegistration.hpp and calls HelPhysicsRuntimeComponentRegistration::Register(core). No BEPU registration or generated Bepu files remain in the default build.
- TiltPlay input project still references ../DemoDisc/DemoDisc.csproj; both input projects survive conversion.
- Generated header audit found zero missing quoted includes. Main repository and generator submodule diff checks pass.
- Interactive gameplay was not launched; this acceptance covers managed behavior tests and the complete native build.
