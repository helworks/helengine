# BEPU Codegen Disposed-Exception Repair Plan

**Goal:** Keep managed `ObjectDisposedException` behavior while emitting a C++ runtime-supported exception in stripped native builds.

**Observed failure:** Full Windows/DX11 codegen completed, the software path tracer scene staged as payload 17/42, and the native compiler failed in generated `BepuPhysicsWorld3D.cpp` because `ObjectDisposedException` is not a declared native runtime type.

**Root cause:** `BepuPhysicsWorld3D.ThrowIfDisposed()` unconditionally emits `ObjectDisposedException`. The engine's established cross-runtime pattern, already used by `RenderManager2D`, keeps `ObjectDisposedException` for managed/editor execution and selects `InvalidOperationException` under `HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION` for native generated builds.

## Repair

1. Add a RED source/translation contract in `BepuPhysicsWorld3DTests` requiring the established preprocessor split and the native-safe disposed message.
2. Apply that split only inside `BepuPhysicsWorld3D.ThrowIfDisposed()`. Preserve all existing managed tests that assert `ObjectDisposedException`.
3. Run the focused BEPU disposed-world tests and the complete registration tests.
4. Run the exact Release physics3d codegen gate, then verify generated `BepuPhysicsWorld3D.cpp` contains `InvalidOperationException` and no `ObjectDisposedException`.
5. Commit, fast-forward engine main, and rerun the full DemoDisc Windows/DX11 build. Accept only a successful state file and fresh nonempty executable.
