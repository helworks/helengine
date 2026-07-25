# Runtime Script Component Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep existing scenes buildable after their automatic script components move between declared runtime modules.

**Architecture:** `ScriptTypeResolver` retains exact module lookup and adds a uniquely-resolved type-name fallback across loaded runtime modules. Scene packaging writes the resolved component's canonical assembly-qualified id, ensuring its cooked record matches native generated-core deserializer registration.

**Tech Stack:** C#, xUnit, HelEngine scene serialization and platform packaging.

---

### Task 1: Cover module-move resolution

**Files:**
- Modify: `engine/helengine.editor.tests/EditorGameScriptAssemblyHostTests.cs`
- Modify: `engine/helengine.core/scripting/ScriptTypeResolver.cs`

- [ ] **Step 1: Write failing resolver tests**

```csharp
[Fact]
public void Resolve_WhenPersistedModuleNoLongerOwnsOneUniqueType_ResolvesMovedType() {
    ScriptTypeResolver resolver = new ScriptTypeResolver();
    resolver.Register("rendering", typeof(TestUpdateOnlyScriptComponent).Assembly);

    Type result = resolver.Resolve(typeof(TestUpdateOnlyScriptComponent).FullName + ", gameplay");

    Assert.Equal(typeof(TestUpdateOnlyScriptComponent), result);
}
```

Add a second test that registers two different module ids containing the same full type name and asserts that `Resolve` throws an `InvalidOperationException` describing the ambiguity.

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorGameScriptAssemblyHostTests`

Expected: the moved-type test fails with `Script assembly 'gameplay' is not loaded`.

- [ ] **Step 3: Implement exact-first, unique-fallback resolution**

In `ScriptTypeResolver.Resolve`, retain the current `moduleId` lookup and return its exact type when found. If that lookup is absent or does not contain `typeName`, scan `AssembliesByModuleId` for `assembly.GetType(typeName, false, false)`. Return the single match; throw an `InvalidOperationException` when there are zero or more than one matches.

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorGameScriptAssemblyHostTests`

Expected: PASS.

### Task 2: Canonicalize cooked automatic component ids

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`

- [ ] **Step 1: Write the failing cook regression test**

Create a dynamic test component assembly named `rendering`, register it under module id `rendering`, serialize one automatic component record, and replace its type id with `namespace.Type, gameplay`. Package the scene and assert the cooked record type id equals `namespace.Type, rendering`.

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorPlatformBuildScenePackagerTests`

Expected: FAIL because the current resolver rejects the legacy module id or preserves it in the cooked record.

- [ ] **Step 3: Write the canonical id during automatic-component packaging**

Replace the `baseRecord.ComponentTypeId` argument passed to `BuildAutomaticRuntimeComponentRecord` with `AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(component.GetType())`. Do not alter authored source records, platform override selection, or non-automatic component behavior.

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorPlatformBuildScenePackagerTests`

Expected: PASS.

### Task 3: Verify and commit

**Files:**
- Modify: only the two production and two test files above

- [ ] **Step 1: Run combined regression coverage**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorGameScriptAssemblyHostTests|FullyQualifiedName~EditorPlatformBuildScenePackagerTests"`

Expected: PASS.

- [ ] **Step 2: Build the full PS2 Demo Disc profile with build-waiter**

Run the existing `scripts/build-platform.ps1` through `tools/build-waiter` for `C:\dev\helprojs\demodisc\project.heproj`, platform `ps2`, profile `debug`, requiring `game.iso`.

Expected: the prior `DemoDiscOrbitCameraComponent, gameplay` support error is absent and the ISO exists.

- [ ] **Step 3: Commit only the implementation and regression tests**

```powershell
git add engine/helengine.core/scripting/ScriptTypeResolver.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/EditorGameScriptAssemblyHostTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "fix(editor): preserve script components across module moves"
```
