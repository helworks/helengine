# Minimal Real-BEPU Box/Sphere Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current full-upstream active BEPU project graph with a pruned real-source slice that supports only box-box, sphere-sphere, and box-sphere rigid-body physics, then ship a working Windows direct-start package for `test_scene_dynamic_stack_boxes`.

**Architecture:** Keep `helengine.bepu` as the stable engine-facing adapter and move the scope reduction into the vendored BEPU project layer. Introduce a minimal BEPU project surface that compiles only the real upstream files required for simulation, bodies, statics, box/sphere collidables, and their contact/solver paths, while excluding mesh, compound, sweep, reduction, and unrelated systems from the immediate native codegen graph.

**Tech Stack:** C#, vendored upstream BEPU source, `csharpcodegen`, native C++ generation, xUnit, Windows direct-start city packaging.

---

## File Map

### Vendored BEPU project pruning
- Create: `engine/vendor/bepuphysics2/BepuPhysics/BepuPhysics.Minimal.csproj`
- Create: `engine/vendor/bepuphysics2/BepuUtilities/BepuUtilities.Minimal.csproj` if a separate utilities slice is required by the reduced graph
- Modify: `engine/vendor/bepuphysics2/BepuPhysics/BepuPhysics.csproj` only if shared metadata extraction is required
- Modify: `engine/vendor/bepuphysics2/BepuUtilities/BepuUtilities.csproj` only if shared metadata extraction is required
- Create: `engine/vendor/bepuphysics2/docs/minimal-box-sphere-file-list.md`

### Helengine project wiring
- Modify: `engine/helengine.bepu/helengine.bepu.csproj`
- Modify: `engine/helengine.physics3d/helengine.physics3d.csproj` only if project references or feature symbols need adjustment
- Modify: `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs` only if validation coverage must explicitly allow mixed box/sphere scenes and reject everything else

### Regression coverage
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`
- Create: `engine/helengine.bepu.tests/BepuMinimalProjectSliceTests.cs`
- Modify: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPManagedRuntimeContractAuditTests.cs` only if the reduced graph exposes one missing generic/runtime contract that still needs generic support

### Build and validation
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json` only to restore the normal multi-scene configuration after validation

## Task 1: Freeze the required physics surface with failing tests

**Files:**
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`
- Create: `engine/helengine.bepu.tests/BepuMinimalProjectSliceTests.cs`

- [ ] **Step 1: Write the failing scope tests**

Add focused tests that prove the supported runtime surface is exactly:
- box rigid body registration succeeds
- sphere rigid body registration succeeds
- mixed box/sphere scene binding succeeds
- unsupported collider families remain rejected

Add one minimal dependency test that asserts the active BEPU project reference exposed to `helengine.bepu` is the minimal slice project once rewiring is complete.

- [ ] **Step 2: Run the focused BEPU adapter tests to verify the new expectations fail**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuMinimalProjectSliceTests|FullyQualifiedName~BepuPhysicsWorld3DTests|FullyQualifiedName~BepuPhysicsFeatureGuard3DTests"
```

Expected:
- the new minimal-slice project reference assertions fail because `helengine.bepu` still references the full vendored BEPU projects

- [ ] **Step 3: Commit the failing-test checkpoint**

```bash
git add engine/helengine.bepu.tests
git commit -m "Add minimal BEPU slice scope tests"
```

## Task 2: Inventory the real upstream file dependency set for box/sphere simulation

**Files:**
- Create: `engine/vendor/bepuphysics2/docs/minimal-box-sphere-file-list.md`

- [ ] **Step 1: Produce the initial dependency inventory**

Trace the compile-time dependencies starting from the shapes and entrypoints already used by `helengine.bepu`:
- `Simulation.Create`
- `BodyDescription.CreateDynamic`
- `BodyDescription.CreateKinematic`
- `StaticDescription`
- `Box`
- `Sphere`
- `TypedIndex`
- `CollidableProperty<T>`
- `INarrowPhaseCallbacks`
- `IPoseIntegratorCallbacks`
- `SpringSettings`
- `SolveDescription`

Record the required upstream files and namespaces in `engine/vendor/bepuphysics2/docs/minimal-box-sphere-file-list.md`.

- [ ] **Step 2: Verify the inventory excludes known out-of-scope systems**

Confirm the inventory does not intentionally include:
- mesh reduction
- mesh collidables
- sweep tasks beyond direct transitive requirements
- compound shapes
- convex hulls not required by box/sphere contacts

- [ ] **Step 3: Commit the dependency inventory**

```bash
git add engine/vendor/bepuphysics2/docs/minimal-box-sphere-file-list.md
git commit -m "Document minimal BEPU box sphere dependency set"
```

## Task 3: Create the minimal vendored BEPU projects

**Files:**
- Create: `engine/vendor/bepuphysics2/BepuPhysics/BepuPhysics.Minimal.csproj`
- Create: `engine/vendor/bepuphysics2/BepuUtilities/BepuUtilities.Minimal.csproj` if needed

- [ ] **Step 1: Write the failing project-reference wiring expectation**

Extend `engine/helengine.bepu.tests/BepuMinimalProjectSliceTests.cs` so it checks the generated project graph or project file text for:
- `helengine.bepu.csproj` referencing `BepuPhysics.Minimal.csproj`
- `BepuPhysics.Minimal.csproj` including only the files listed in `minimal-box-sphere-file-list.md`

- [ ] **Step 2: Run the minimal-slice wiring test and verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuMinimalProjectSliceTests"
```

Expected:
- failures because the minimal vendored project files do not exist yet

- [ ] **Step 3: Create the reduced vendored project files**

Implement `BepuPhysics.Minimal.csproj` so it includes only the real upstream files required by the recorded box/sphere dependency set.

If the utility dependency graph is still too broad through `BepuUtilities.csproj`, create `BepuUtilities.Minimal.csproj` with the corresponding reduced utility file set and have the minimal physics project reference that reduced utility project.

Do not change BEPU implementation behavior in this step. Only reduce project/file membership.

- [ ] **Step 4: Re-run the minimal-slice wiring test**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter "FullyQualifiedName~BepuMinimalProjectSliceTests"
```

Expected:
- the new minimal project existence and wiring assertions pass

- [ ] **Step 5: Commit the minimal vendored project slice**

```bash
git add engine/vendor/bepuphysics2/BepuPhysics engine/vendor/bepuphysics2/BepuUtilities engine/helengine.bepu.tests
git commit -m "Add minimal vendored BEPU project slice"
```

## Task 4: Rewire Helengine to the minimal BEPU slice

**Files:**
- Modify: `engine/helengine.bepu/helengine.bepu.csproj`
- Modify: `engine/helengine.physics3d/helengine.physics3d.csproj` only if required

- [ ] **Step 1: Update the project references**

Point `engine/helengine.bepu/helengine.bepu.csproj` at the new minimal vendored BEPU projects instead of the full `BepuPhysics.csproj` and `BepuUtilities.csproj` projects.

- [ ] **Step 2: Run the BEPU adapter test suite**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug
```

Expected:
- tests fail only where the minimal project slice is still missing direct transitive files

- [ ] **Step 3: Add only the missing direct dependencies exposed by the failures**

Widen the minimal project file lists narrowly until the BEPU adapter test suite passes. Do not reintroduce whole unrelated directories.

- [ ] **Step 4: Re-run the BEPU adapter test suite**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug
```

Expected:
- all `helengine.bepu` tests pass against the minimal real-source slice

- [ ] **Step 5: Commit the Helengine rewiring**

```bash
git add engine/helengine.bepu/helengine.bepu.csproj engine/helengine.physics3d/helengine.physics3d.csproj engine/vendor/bepuphysics2 engine/helengine.bepu.tests
git commit -m "Wire helengine BEPU integration to minimal slice"
```

## Task 5: Re-run codegen and fix only remaining generic runtime gaps

**Files:**
- Modify only the specific generic `csharpcodegen` or runtime files proven necessary by the reduced graph

- [ ] **Step 1: Reproduce codegen against the reduced physics graph**

Run:

```bash
rtk proxy C:\dev\helworks\csharpcodegen\codegen\bin\Debug\net9.0\codegen.exe --cpp --project C:\dev\helworks\helengine\engine\helengine.physics3d\helengine.physics3d.csproj --output C:\tmp\bepu-minimal-repro --feature-catalog C:\dev\helworks\helengine\engine\helengine.editor\codegen\features\helengine-feature-catalog.json --platform windows --language cpp --set include-project-defined-preprocessor-symbols=false --set additional-preprocessor-symbols=HELENGINE_INPUT_KEYBOARD,HELENGINE_INPUT_MOUSE,DESKTOP_PLATFORM,HELENGINE_RUNTIME_SUPPORTS_RENDER_MANAGER_2D_TEXTURE_RELEASE_FLUSH,HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION,HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION,HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES,HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT
```

Expected:
- either successful completion or a materially smaller set of remaining conversion/runtime failures than the full-library graph produced

- [ ] **Step 2: Add one failing regression per remaining generic converter/runtime blocker**

Only for blockers still exposed by the reduced slice, add focused tests in `C:\dev\helworks\csharpcodegen\cs2.cpp.tests` before implementation.

- [ ] **Step 3: Implement the minimal generic fixes**

Apply only portable, generic fixes in `csharpcodegen`. Do not add Helengine- or BEPU-specific hardcoding.

- [ ] **Step 4: Re-run the focused converter tests and the reduced repro**

Run the targeted `cs2.cpp.tests` filters plus the reduced repro command again.

Expected:
- tests pass
- the reduced repro completes

- [ ] **Step 5: Commit the reduced-graph converter fixes**

```bash
git add C:\dev\helworks\csharpcodegen
git commit -m "Fix converter gaps exposed by minimal BEPU slice"
```

## Task 6: Build and launch the direct-start Windows package

**Files:**
- Use the existing direct-start temporary config in `C:\dev\helprojs\city\user_settings\build_config.json`

- [ ] **Step 1: Run the Windows city build**

Run:

```bash
rtk proxy powershell -NoProfile -Command "& 'C:\Program Files\dotnet\dotnet.exe' 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll' --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\city\windows-build-20260529-stack-box-direct-real-bepu'"
```

Expected:
- build completes with a launchable Windows package

- [ ] **Step 2: Launch the direct-start package**

Run the generated executable from:

```bash
C:\dev\helprojs\city\windows-build-20260529-stack-box-direct-real-bepu\helengine_windows.exe
```

Expected:
- the package launches successfully into `test_scene_dynamic_stack_boxes`
- the authored stack topples visibly

- [ ] **Step 3: Validate the sphere-stack and mixed-stack scenes**

Run the narrowest available validation path for:
- `test_scene_dynamic_sphere_stack`
- `test_scene_dynamic_mixed_stack`

Expected:
- sphere-only and mixed box/sphere contacts behave without reintroducing unsupported colliders

- [ ] **Step 4: Commit the working Windows validation checkpoint**

```bash
git add engine C:\dev\helprojs\city
git commit -m "Ship minimal real BEPU box sphere windows build"
```

## Task 7: Restore normal city build settings

**Files:**
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json`

- [ ] **Step 1: Restore the standard multi-scene menu build configuration**

Replace the temporary direct-start one-scene configuration with the normal multi-scene menu configuration used before the physics validation build.

- [ ] **Step 2: Verify the file contents changed back**

Run:

```bash
rtk proxy powershell -NoProfile -Command "Get-Content -Path 'C:\dev\helprojs\city\user_settings\build_config.json'"
```

Expected:
- the restored config no longer points only at `test_scene_dynamic_stack_boxes`

- [ ] **Step 3: Commit the build-config restoration**

```bash
git add C:\dev\helprojs\city\user_settings\build_config.json
git commit -m "Restore city build configuration"
```
