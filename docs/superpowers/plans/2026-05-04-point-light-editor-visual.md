# Point Light Editor Visual Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an editor-only point-light visual built from a sphere and a cylinder, attached to point-light entities and visible only through the main editor camera path.

**Architecture:** Keep the procedural mesh math in the shared editor primitive factory, then build one cached point-light visual resource from that geometry. Attach the visual as a hidden child editor entity on point lights, mirroring the existing camera-visual pattern so the mesh stays out of authored scene data and shadow/light rendering paths. Reattach the same hidden child when scenes load so both newly created and loaded point lights behave the same.

**Tech Stack:** C# / .NET 9, the editor mesh/model pipeline, xUnit, existing editor-only hidden-component conventions.

---

### Task 1: Add a reusable sphere primitive to the editor mesh factory

**Files:**
- Modify: `engine/helengine.editor/managers/gizmo/TransformGizmoMeshFactory.cs`
- Test: `engine/helengine.editor.tests/managers/gizmo/TransformGizmoMeshFactoryTests.cs`

- [ ] **Step 1: Write the failing test**

Add a new test that calls `TransformGizmoMeshFactory.CreateSphere(radius, height, segments)` or the final chosen signature and verifies:
- it returns a model with non-empty vertex and index buffers
- the poles and equator are placed where expected for a sphere centered for editor visuals
- invalid radius/segment inputs throw `ArgumentOutOfRangeException`

- [ ] **Step 2: Run the targeted test to confirm it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~TransformGizmoMeshFactoryTests.CreateSphere`
Expected: FAIL because `CreateSphere` does not exist yet.

- [ ] **Step 3: Implement the minimal sphere generator**

Add a sphere primitive to `TransformGizmoMeshFactory` that follows the same validation and `ModelAsset` creation style as the existing cylinder, cone, and tube-ring helpers. Keep the implementation deterministic and 16-bit index safe.

- [ ] **Step 4: Run the targeted test to confirm it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~TransformGizmoMeshFactoryTests.CreateSphere`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/gizmo/TransformGizmoMeshFactory.cs engine/helengine.editor.tests/managers/gizmo/TransformGizmoMeshFactoryTests.cs
git commit -m "feat: add editor sphere mesh primitive"
```

### Task 2: Add the point-light editor visual and attach it to point-light entities

**Files:**
- Create: `engine/helengine.editor/components/EditorPointLightVisualComponent.cs`
- Create: `engine/helengine.editor/components/EditorPointLightVisualResources.cs`
- Create: `engine/helengine.editor/managers/scene/EditorPointLightVisualAttachmentService.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add tests that prove:
- a point-light entity created by `EditorSceneCreationService.CreatePointLight()` gets an internal child on `EditorLayerMasks.SceneCameraVisuals`
- the child carries the point-light visual component and its runtime model/material are initialized
- loading a serialized point-light scene reattaches the same hidden visual child
- the visual child is not serialized into the `.helen` file because it is editor-only

- [ ] **Step 2: Run the targeted tests to confirm they fail**

Run:
`rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSceneCreationServiceTests.CreatePointLight`
`rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~SceneSaveServiceTests.SaveAndLoad_WhenSceneContainsPointLightEntity_RoundTripsPointLightAndReattachesHiddenEditorVisual`

Expected: FAIL because the visual child and load reattachment do not exist yet.

- [ ] **Step 3: Implement the point-light editor visual**

Create the point-light visual resource class that builds one cached runtime model from the new sphere primitive plus a short cylinder, then add the hidden mesh component and attachment service that mirrors the camera-visual pattern:
- `EditorPointLightVisualComponent` should set the shared runtime model and the standard editor material in `ComponentAdded`
- `EditorPointLightVisualAttachmentService.Attach` should add one `InternalEntity` child on `SceneCameraVisuals` and avoid duplicates
- `EditorSceneCreationService.CreatePointLight()` should attach the visual after creating the point-light component
- `SceneLoadService.LoadEntity()` should reattach the visual after point-light deserialization

- [ ] **Step 4: Run the targeted tests to confirm they pass**

Run:
`rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorSceneCreationServiceTests.CreatePointLight`
`rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~SceneSaveServiceTests.SaveAndLoad_WhenSceneContainsPointLightEntity_RoundTripsPointLightAndReattachesHiddenEditorVisual`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/EditorPointLightVisualComponent.cs engine/helengine.editor/components/EditorPointLightVisualResources.cs engine/helengine.editor/managers/scene/EditorPointLightVisualAttachmentService.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "feat: add point light editor visual"
```

### Task 3: Verify the branch end to end

**Files:**
- No new files expected

- [ ] **Step 1: Run the focused editor test slice**

Run:
`rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~TransformGizmoMeshFactoryTests|FullyQualifiedName~EditorSceneCreationServiceTests|FullyQualifiedName~SceneSaveServiceTests`

Expected: PASS for the new sphere, point-light visual, and load/save coverage.

- [ ] **Step 2: Check branch status**

Run: `rtk git status --short`
Expected: Only intentional changes remain in the feature branch.

- [ ] **Step 3: Commit the finished feature**

```bash
git add .
git commit -m "feat: add editor point light visual"
```
