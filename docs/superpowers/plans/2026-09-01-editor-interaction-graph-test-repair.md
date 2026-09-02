# Editor Interaction Graph Test Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Restore the 25 `EditorSessionWorkspaceTests` without weakening the production renderer-resource ownership validator.

**Architecture:** The workspace-test harness already creates one interaction service and assigns it to `Core.SessionInteractionServices`, but it does not mirror the production binding of that same object to `Core.SessionInteractionGraph`. Bind both properties in the harness so generated renderer resources and their owning core share one canonical interaction graph.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine editor interaction graph

---

### Task 1: Repair the workspace harness interaction graph binding

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`

- [ ] **Step 1: Preserve the observed RED evidence**

Run one representative workspace test before editing:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests.UiShow_WhenViewportIsOpenedTwice_CreatesTwoIndependentViewportInstances" -v:minimal
```

Expected: the test fails with `Renderer resources must use the interaction graph attached to their owning core.`

- [ ] **Step 2: Bind the test core to its canonical interaction graph**

In the `EditorSessionHarness` constructor, immediately after assigning `CoreValue.SessionInteractionServices`, assign the same `InteractionServices` instance to `CoreValue.SessionInteractionGraph`:

```csharp
CoreValue.SessionInteractionServices = InteractionServices;
CoreValue.SessionInteractionGraph = InteractionServices;
```

Do not change `EditorProjectAuthoringSession.ValidateGeneratedAssetGraph`, `TestGeneratedAssetGraph`, or production ownership behavior.

- [ ] **Step 3: Verify the complete workspace-test class**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests" -v:minimal
```

Expected: all 25 formerly failing tests pass.

- [ ] **Step 4: Verify adjacent ownership behavior**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~GeneratedSessionIsolationBehaviorTests|FullyQualifiedName~EditorProjectAuthoringSessionTests" -v:minimal
```

Expected: all selected ownership tests pass.

- [ ] **Step 5: Commit only the planned repair**

Run:

```powershell
rtk git add -- engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs
rtk git diff --cached --check
rtk git commit -m "Bind editor workspace test interaction graph"
```

Expected: only the listed test file is committed; the unrelated dirty runtime deserializer remains unstaged and untouched.
