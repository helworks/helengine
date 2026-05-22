# Editor Generated-Core Postprocessing Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove editor-side generated-core post-processing so `helengine.editor` no longer rewrites or repairs generated native output after codegen.

**Architecture:** This is a deletion-first cleanup. `EditorPlatformBuildGraphRunner` will stop running its generated-core finalization phase, and tests that assert generated output rewrites will be removed or inverted. Any broken generated output that appears afterward must be fixed in codegen or plugin-owned generators, not in the editor.

**Tech Stack:** C#, .NET, `helengine.editor`, `helengine.editor.tests`, xUnit, `rtk` command wrapper

---

## File Structure

### Production files to modify

- `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`

### Test files to modify

- `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

### Validation targets

- `engine/helengine.editor.tests/helengine.editor.tests.csproj`

---

### Task 1: Remove The Final Generated-Core Postprocessing Call

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing test**

Add a focused test proving the build-graph runner no longer exposes the final generated-core postprocessing method.

```csharp
[Fact]
public void FinalizeGeneratedCoreSources_is_not_part_of_the_editor_build_graph_runner() {
    MethodInfo finalizeMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
        "FinalizeGeneratedCoreSources",
        BindingFlags.Instance | BindingFlags.NonPublic);

    Assert.Null(finalizeMethod);
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~FinalizeGeneratedCoreSources_is_not_part_of_the_editor_build_graph_runner' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: FAIL because `FinalizeGeneratedCoreSources(...)` still exists.

- [ ] **Step 3: Remove the call site**

In `EditorPlatformBuildGraphRunner.Execute(...)`, remove:

```csharp
FinalizeGeneratedCoreSources(workspace.GeneratedCoreRootPath);
```

Do not replace it with another editor-side postprocessing call.

- [ ] **Step 4: Delete the postprocessing method**

Delete the method:

```csharp
void FinalizeGeneratedCoreSources(string generatedCoreRootPath)
```

If it is only a wrapper around other postprocessing helpers, delete those helpers in the same task when they become unreachable.

- [ ] **Step 5: Run the focused test to verify it passes**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~FinalizeGeneratedCoreSources_is_not_part_of_the_editor_build_graph_runner' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk git commit -m "Remove editor generated-core finalization pass"
```

### Task 2: Remove Tests That Assert Generated Output Rewrites

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Delete rewrite-based tests**

Remove or rewrite tests that validate editor-side generated output mutation, including any test that asserts:

- duplicate generated `delete loadResult;` cleanup
- generated feature-manifest rewriting
- post-hoc promotion of generated shader feature outputs

Examples to remove or invert include tests named like:

```csharp
FinalizeGeneratedCoreSources_collapses_duplicate_scene_manager_load_result_deletes
```

- [ ] **Step 2: Replace them with boundary assertions where needed**

If a deleted test leaves a coverage gap, replace it with a boundary assertion that the editor does not own that rewrite anymore.

Example:

```csharp
[Fact]
public void EditorBuildGraphRunner_does_not_patch_generated_scene_manager_source() {
    MethodInfo finalizeMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
        "FinalizeGeneratedCoreSources",
        BindingFlags.Instance | BindingFlags.NonPublic);

    Assert.Null(finalizeMethod);
}
```

Do not add a new source-rewrite test.

- [ ] **Step 3: Run the focused build-graph test slice**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorPlatformBuildGraphRunnerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: PASS for the build-graph test class without rewrite-based expectations.

- [ ] **Step 4: Commit**

```powershell
rtk git add engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk git commit -m "Remove generated-output rewrite assertions from editor tests"
```

### Task 3: Final Verification And Handoff

**Files:**
- Modify: any touched file from Tasks 1-2 if final cleanup is required

- [ ] **Step 1: Search for the removed postprocessing symbol**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "rg -n 'FinalizeGeneratedCoreSources' 'engine/helengine.editor' 'engine/helengine.editor.tests' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: no remaining production or test references.

- [ ] **Step 2: Run the final focused validation slice**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~FinalizeGeneratedCoreSources_is_not_part_of_the_editor_build_graph_runner' 2>&1 | Select-Object -Last 240 | Out-String -Width 260 | Write-Output"
```

Expected: PASS.

- [ ] **Step 3: Record the change in Graphiti**

Use the Graphiti durable handoff note flow and record that:

- editor no longer runs generated-core postprocessing
- generated source rewrite expectations were removed from tests
- any future failures in generated native output belong to codegen or plugin-owned generators

- [ ] **Step 4: Commit final cleanup if needed**

```powershell
rtk git add -A
rtk git commit -m "Stop editor from rewriting generated native output"
```

Skip this commit if the previous task commits already leave the branch clean.
