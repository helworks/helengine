# BEPU Native vs Managed Differential Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a deterministic reduced-BEPU differential harness that emits managed and native traces for `test_scene_dynamic_stack_boxes`, compares them automatically, and reports the first divergence.

**Architecture:** Add a shared compact trace format in `helengine.bepu`, emit it from the existing managed and native reduced-BEPU diagnostics boundaries, and add test-side parsing plus diff helpers that compare matching `frame + phase + body_handle` records. Keep the first implementation narrow: one scene, tracked handles `0..3`, and the currently instrumented stack-box phases closest to the known bug.

**Tech Stack:** C#, .NET 9, xUnit, reduced vendored BEPU source, Helengine native Windows host, generated C++ runtime trace output

---

## File Structure

### Existing files to modify

- `C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3DDiagnostics.cs`
  - Replace ad hoc mixed diagnostics accumulation with structured trace record emission and bounded per-run buffers.
- `C:\dev\helworks\helengine\engine\helengine.bepu\HelengineBepuPoseIntegratorCallbacks.cs`
  - Emit structured `integrate_velocity_callback` records using the shared trace writer.
- `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\BepuNativeConversionDiagnostics.cs`
  - Convert the temporary native-only probes into shared schema record emission for native boundaries.
- `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Solver_Solve.cs`
  - Emit `integration_responsibility_assignment` records for tracked handles.
- `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Constraints\TypeProcessor.cs`
  - Emit `gather_and_integrate_before` and `gather_and_integrate_after` records in the constrained-body integration path.
- `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Constraints\TwoBodyTypeProcessor.cs`
  - Emit `two_body_solve_before` and `two_body_solve_after` records in the two-body contact path.
- `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs`
  - Add managed golden-trace generation coverage and targeted assertions for the first supported scene.
- `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp`
  - Write the native differential trace to a stable package-local file path and keep the startup log readable.
- `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.hpp`
  - Add state for bounded differential-trace file emission if required by the host path.

### New production files to create

- `C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTracePhase3D.cs`
  - Shared phase constants or enum for managed/native trace emission.
- `C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTraceRecord3D.cs`
  - Shared record model with fixed field names and formatting rules.
- `C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTraceWriter3D.cs`
  - Central managed helper for line-oriented trace emission and compact numeric formatting.

### New test/support files to create

- `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuDifferentialTraceParser.cs`
  - Parses the shared line-oriented trace format into comparable test records.
- `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuDifferentialTraceComparer.cs`
  - Compares managed and native traces and reports the first mismatch with field-level detail.
- `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs`
  - End-to-end harness tests for parsing, diffing, and known divergence detection.

## Notes for the implementer

- Work primarily in `C:\dev\helworks\helengine`, with one small host integration touch in `C:\dev\helworks\helengine-windows`.
- Keep the trace schema plain UTF-8 text. Do not switch to JSON.
- Keep all new diagnostics bounded and deterministic.
- Do not remove the reduced-BEPU scene tests while adding the harness.
- Do not mix renderer diagnostics into the differential schema.
- Prefer using body handles as the main cross-runtime identity field.
- The current temporary native probes are allowed to evolve into the shared trace emitter, but remove duplicate ad hoc messages once the structured path exists.

### Task 1: Define the shared differential trace format in `helengine.bepu`

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTracePhase3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTraceRecord3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTraceWriter3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3DDiagnostics.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs`

- [ ] **Step 1: Write the failing parser/format tests for the shared schema**

Add tests that require:

- one line per trace record
- stable field names like `frame=`, `phase=`, `body_handle=`
- compact vector formatting for `position`, `orientation`, `linear_velocity`, and `angular_velocity`
- omission of irrelevant fields without breaking parsing

- [ ] **Step 2: Run the new tests to verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuNativeManagedDifferentialHarnessTests"
```

Expected: FAIL because the shared trace types and parser do not exist yet.

- [ ] **Step 3: Implement the shared trace model and managed writer**

Implement:

- phase definitions for the initial phase set
- one immutable or effectively immutable record model
- one writer that emits plain text using invariant formatting
- one bounded buffer path in `BepuPhysicsWorld3DDiagnostics`

- [ ] **Step 4: Re-run the focused tests**

Run the same command from Step 2.

Expected: PASS for schema and format tests.

- [ ] **Step 5: Commit**

```powershell
git add C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTracePhase3D.cs C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTraceRecord3D.cs C:\dev\helworks\helengine\engine\helengine.bepu\BepuDifferentialTraceWriter3D.cs C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3DDiagnostics.cs C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs
git commit -m "feat: add shared BEPU differential trace schema"
```

### Task 2: Emit the managed golden trace for the reduced stack-box scene

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\HelengineBepuPoseIntegratorCallbacks.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3DDiagnostics.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs`

- [ ] **Step 1: Write the failing managed golden-trace test**

Add a test that loads `test_scene_dynamic_stack_boxes`, advances a bounded frame count, emits a managed trace, and asserts that:

- the trace contains `integrate_velocity_callback`
- the trace contains `sync_snapshot`
- tracked handles `0..3` appear in deterministic order

- [ ] **Step 2: Run the managed golden-trace test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~LoadCityStackBoxesScene_WhenDifferentialTraceRequested_EmitsManagedGoldenTrace"
```

Expected: FAIL because the managed trace emitter is incomplete.

- [ ] **Step 3: Implement managed trace emission at the approved phases**

Use the shared writer to emit:

- `integrate_velocity_callback`
- `sync_snapshot`

for the first pass. Keep the scene deterministic and bounded.

- [ ] **Step 4: Re-run the focused managed trace test**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add C:\dev\helworks\helengine\engine\helengine.bepu\HelengineBepuPoseIntegratorCallbacks.cs C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3DDiagnostics.cs C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs
git commit -m "feat: emit managed BEPU differential golden trace"
```

### Task 3: Emit the native trace at matching reduced-BEPU boundaries

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\BepuNativeConversionDiagnostics.cs`
- Modify: `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Solver_Solve.cs`
- Modify: `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Constraints\TypeProcessor.cs`
- Modify: `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Constraints\TwoBodyTypeProcessor.cs`
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp`
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.hpp`

- [ ] **Step 1: Write the failing native-trace parsing test**

Add a test fixture or sample trace that requires the parser to recognize:

- `integration_responsibility_assignment`
- `gather_and_integrate_before`
- `gather_and_integrate_after`
- `two_body_solve_before`
- `two_body_solve_after`

- [ ] **Step 2: Run the parser test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuNativeManagedDifferentialHarnessTests.Parse_native_phase_records"
```

Expected: FAIL because the native schema records are not emitted consistently yet.

- [ ] **Step 3: Replace ad hoc native probe strings with shared trace records**

Emit the shared schema from:

- solver integration-responsibility prepass
- constrained gather/integrate path
- two-body solve path

Write the resulting native trace to a dedicated package-local file, not the general startup log.

- [ ] **Step 4: Build a fresh Windows package and verify the native trace file exists**

Run:

```powershell
rtk powershell -NoProfile -Command "& 'C:\Program Files\dotnet\dotnet.exe' 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll' --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\city\windows-build-20260603-stack-boxes-differential-harness'"
```

Then launch once and verify the dedicated native trace file is created in the package directory.

- [ ] **Step 5: Commit**

```powershell
git add C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\BepuNativeConversionDiagnostics.cs C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Solver_Solve.cs C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Constraints\TypeProcessor.cs C:\dev\helworks\helengine\engine\vendor\bepuphysics2\BepuPhysics\Constraints\TwoBodyTypeProcessor.cs C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.hpp
git commit -m "feat: emit native BEPU differential trace"
```

### Task 4: Build the parser and first-divergence comparer

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuDifferentialTraceParser.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuDifferentialTraceComparer.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs`

- [ ] **Step 1: Write the failing comparer tests**

Add tests that require:

- grouping by `frame + phase + body_handle`
- float tolerance comparison
- first-mismatch reporting with field name, managed value, and native value

- [ ] **Step 2: Run the comparer tests to verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuNativeManagedDifferentialHarnessTests.Compare_"
```

Expected: FAIL because parser/comparer implementation is missing.

- [ ] **Step 3: Implement the parser and comparer**

Implement:

- line parser for the shared trace schema
- diff engine that stops at the first mismatch
- useful failure messages for bundle and body context

- [ ] **Step 4: Re-run the focused comparer tests**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuDifferentialTraceParser.cs C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuDifferentialTraceComparer.cs C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs
git commit -m "feat: add BEPU differential trace parser and comparer"
```

### Task 5: Wire the end-to-end differential harness for the stack-box scene

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs`

- [ ] **Step 1: Write the failing end-to-end differential test**

Add a test that:

- generates the managed golden trace
- loads the latest native trace file from the dedicated package path
- runs the comparer
- asserts that the current known native bug produces a first mismatch

- [ ] **Step 2: Run the end-to-end harness test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuNativeManagedDifferentialHarnessTests.Run_stack_boxes_differential_harness"
```

Expected: FAIL until the wiring is complete.

- [ ] **Step 3: Implement the end-to-end harness**

Make the test emit the managed golden trace, consume the native trace, and print the first divergence in a compact readable format.

- [ ] **Step 4: Re-run the end-to-end harness test**

Run the same command from Step 2.

Expected: PASS, with the current bug reproduced as a deliberate first-difference report.

- [ ] **Step 5: Commit**

```powershell
git add C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs
git commit -m "feat: wire stack-boxes BEPU differential harness"
```

### Task 6: Use the harness to identify and lock the first real divergence

**Files:**
- Modify: `C:\dev\helworks\helengine\docs\physics\bepu-physics2-collision-analysis.md`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs`

- [ ] **Step 1: Run the harness against the current native bug**

Run the end-to-end harness command from Task 5.

Expected: PASS as a test harness, while reporting one concrete first divergence.

- [ ] **Step 2: Capture the first divergence as a locked regression note**

Update `bepu-physics2-collision-analysis.md` with:

- the exact phase
- the exact frame
- the exact handle
- the differing field values

- [ ] **Step 3: Add one assertion that locks the discovered first divergence shape**

This assertion should stay until the actual bug fix work starts, so future debugging does not regress back into vague symptoms.

- [ ] **Step 4: Run the smallest necessary verification**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuNativeManagedDifferentialHarnessTests|FullyQualifiedName~LoadCityStackBoxesScene_WhenDifferentialTraceRequested_EmitsManagedGoldenTrace"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add C:\dev\helworks\helengine\docs\physics\bepu-physics2-collision-analysis.md C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuNativeManagedDifferentialHarnessTests.cs
git commit -m "docs: capture first BEPU native differential divergence"
```

## Final Verification

- [ ] Run the focused BEPU harness suite:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuNativeManagedDifferentialHarnessTests|FullyQualifiedName~LoadCityStackBoxesScene_WhenDifferentialTraceRequested_EmitsManagedGoldenTrace"
```

- [ ] Rebuild the Windows stack-box differential package:

```powershell
rtk powershell -NoProfile -Command "& 'C:\Program Files\dotnet\dotnet.exe' 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll' --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\city\windows-build-20260603-stack-boxes-differential-harness'"
```

- [ ] Launch the package once to generate the native trace file and verify the end-to-end diff path still works.
