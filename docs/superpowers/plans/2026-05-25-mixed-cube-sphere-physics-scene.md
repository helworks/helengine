# Mixed Cube Sphere Physics Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a physics validation scene that shows dynamic cubes and spheres colliding with each other and the ground.

**Architecture:** Extend the existing editor-owned physics validation scene generator with one new scene id and one factory method. The generated city menu exposes the scene under Physics Scenes, while runtime behavior continues to use existing rigid body, box collider, and sphere collider records.

**Tech Stack:** C#, helengine editor scene generation, helengine physics3d runtime, city generated assets, Windows build harness.

---

### Task 1: Mixed Physics Scene

**Files:**
- Modify: `engine/helengine.editor/managers/physics/PhysicsValidationSceneCatalog.cs`
- Modify: `engine/helengine.editor/managers/physics/PhysicsValidationSceneFactory.cs`
- Test: `engine/helengine.editor.tests/managers/physics/PhysicsValidationSceneFactoryTests.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs`

- [ ] **Step 1: Add a failing catalog/factory test**

Add an assertion that the new id is listed and a test that loads `test_scene_dynamic_mixed_stack.helen`, finds `StackBox01`, `StackSphere01`, and verifies both dynamic collider types exist.

- [ ] **Step 2: Run focused editor tests**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --no-restore --filter FullyQualifiedName~PhysicsValidationSceneFactoryTests`

- [ ] **Step 3: Implement catalog and scene factory**

Add `DynamicMixedStackSceneId`, route it in `CreateSceneAsset`, and create a scene with static ground plus alternating dynamic boxes and spheres using existing helper methods and colored standard materials.

- [ ] **Step 4: Add menu item**

Add `Mixed Stack` to `CreatePhysicsSceneItems()` with action target `test_scene_dynamic_mixed_stack`.

- [ ] **Step 5: Verify and build**

Run the focused editor test, regenerate city physics scenes, build the Windows city package, and launch `helengine_windows.exe`.
