# HelPhysics runtime acceptance correction

## Observed failure

The final10 Windows build compiles and links, but the player never invokes RegisterGeneratedRuntimeModules. Windows startup still only calls the conditional legacy Physics3DRuntimeComponentRegistration path. The generated HelPhysics registration function exists but is unused. The user's startup log records a fatal SceneEntityTriggerObserverComponent error while entering TiltPlay because no trigger-capable physics runtime is attached.

## Plan and ownership

1. Luna build: correct the handwritten Windows host bootstrap after Core.Initialize and before the first scene load. Invoke the generated runtime-module registration contract. Preserve explicit legacy solver settings without duplicate registration or a hidden backend fallback.
2. Luna build/physics: use direct event subscriptions to persistent state instance methods. The native event bridge ignores Action-pointer subscriptions; direct method groups lower to its supported Event::Bind representation. Track registration with a boolean and unsubscribe the same instance/method pair. This also removes escaped lambdas that captured a local state pointer by reference.
3. Luna scene: exercise the real scene-manager event path, fixed-step core updates and unload/reload. Verify the registered runtime exists when trigger observers update. Avoid direct calls to internal registration handlers as the only acceptance test.
4. Luna physics: add and execute a bounded native runtime smoke harness using generated production code. Check body motion/contact, trigger delivery and scene transitions, not just successful compilation.
5. Coordinator: review changes, preserve original player logs, rebuild the native player, verify terminal status/artifact and inspect runtime evidence. No screenshots or generated C++ rewrites. Keep all validation fixtures and logs in workspace-owned directories.

## Acceptance

- Windows startup actually executes the generated module bootstrap before scene loading.
- A physics scene attaches HelPhysics; fixed updates change dynamic transforms and preserve floor contact.
- Trigger observers run without the missing-runtime exception and receive events.
- Leaving and re-entering physics scenes does not retain a disposed world or lose subscriptions.
- Validation distinguishes the native behavior checks from compilation and any gameplay paths not exercised.

## Status

Completed for the diagnosed Windows runtime activation failure. Managed and native behavioral regressions pass, and the rebuilt Windows player executes generated module registration during startup. Individual demos have not all been manually played.
## Validation in progress

- Windows host regression: 1/1 passed; generated registration occurs after Core.Initialize and before startup scene loading.
- Managed HelPhysics suite: 298/298 passed after adding real SceneManager event, trigger observer, gravity motion, and unload/reload coverage. Log: C:/dev/helworks/builds/helengine/physics-migration/runtime-review-tests.log.
- Profiler feature boundary: 1/1 passed. Log: runtime-profiler-review.log.
- Fresh generated native compilation passes at .validation/helphysics-runtime-fixed-2. The callbacks now use he_cpp_bind_front, which stores the receiver pointer by value.
- Initial native fixture crashes came from missing required content-source and scene-catalog setup, not proven callback execution. After both fixture contracts were corrected, the old Action-pointer subscription path reproducibly fails with "runtime was not attached after scene-loaded callback". Baseline: C:/dev/helworks/builds/helengine/physics-migration/runtime-smoke/baseline-with-valid-catalog.log. This matches the user report of static objects. Native execution of the final direct-method-group path passes.
- Recovery note: an RTK-forwarded search metacharacter accidentally overwrote the untracked adapter source. Recovered its behavior from the known-good workspace assembly, compared against preserved generated C++, restored source formatting/documentation and profiler guards, and removed the decompiler-expanded redundant inherited interface. Managed and native compilation checks above cover the recovered source. No private-session recovery was used.
- Final managed rerun after direct subscriptions and boolean registration state: 298/298 passed (runtime-final-managed.log). The repeated-registration test checks actual event invocation-list identity and a single subscriber. Native runtime fixture is now repository-owned at scripts/fixtures/helphysics-runtime-smoke.cpp and runs as part of scripts/validate-helphysics-generated-cpp.ps1.

- Final native validator passed: .validation/helphysics-runtime-final/native-runtime-smoke/run.log reports PASS native runtime smoke. This exercises real registered scene events, 240 Core.Update steps, falling/settling sphere, trigger events, unload/reload/second unload, and core disposal. Generated production subscriptions use Event::Bind on the persistent registration state.
- Native controller smoke passed against the final generated object: DemoDisc-style 18-degree ramp, start X=-4, final X=2.75, final Y=1.97411, with runtime released on unload. Fixture/script/log: C:/dev/helworks/builds/helengine/physics-migration/runtime-smoke/native-controller-smoke.cpp, build-and-run-controller.ps1, native-controller-smoke-run.log.

## Rebuilt player and limits

- Executable: C:/dev/helprojs/demodisc/output/windows-helphysics-runtime-fixed/helengine_windows.exe (1,852,928 bytes).
- Canonical build: scripts/build-platform.ps1, Windows debug, completed 2026-09-22T00:29:40Z. Build ID b44faa49-7f73-474e-8431-35dea819974f. Both terminal proof files report succeeded and exitCode 0. Log: C:/dev/helworks/builds/helengine/physics-migration/demodisc-runtime-fixed-3.log.
- Packaged-player startup: ran for 15 seconds, logged Generated runtime modules registered, and completed update/draw/present frames without a fatal exception. Stopped only the validation process afterward. Preserved log: C:/dev/helworks/builds/helengine/physics-migration/runtime-fixed-startup-verified.log.
- Native behavior evidence comes from the registered SceneManager event path and generated production physics code, plus a separate DemoDisc-style controller ramp fixture. Startup execution does not constitute manually playing every authored demo. No screenshots were taken.
- Original final10 build and user logs were preserved. Changes remain uncommitted.