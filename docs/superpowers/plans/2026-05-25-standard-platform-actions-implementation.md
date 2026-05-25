# Standard Platform Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an engine-owned `Accept` / `Return` action layer backed by per-platform project settings, then migrate `city` to use it instead of raw face-button polling.

**Architecture:** Reuse the existing `InputSystem` logical-action pipeline instead of inventing a second binding system. Persist one standard-action mapping per platform in `settings/platform.<platform-id>.json`, resolve those mappings into runtime startup metadata, and register them into one reserved engine-owned input context during core initialization.

**Tech Stack:** C#, .NET, `helengine.input`, `helengine.core`, `helengine.editor`, generated native runtime manifest code, Nintendo DS native host bootstrap, xUnit, `dotnet test`

---

## File Structure

### Engine input/runtime files

- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformAction.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformActionIds.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformActionBinding.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformInputConfiguration.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformInput.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.input\InputSystem.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`

### Editor settings/runtime-manifest files

- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorInputProfileSettingsDocument.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorStandardPlatformActionSettingsDocument.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorInputControlSettingsDocument.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformProfileSettingsDocument.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorProfileSettingsService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorRuntimeNativeManifestWriter.cs`

### Native DS host files

- Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsBootHost.cpp`

### Tests

- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorProfileSettingsServiceTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorRuntimeNativeManifestWriterTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\StandardPlatformInputTests.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder.tests\CityNintendoDsSceneSourceAuditTests.cs`

### City project files

- Modify: `C:\dev\helprojs\city\settings\platform.ds.json`
- Modify: `C:\dev\helprojs\city\settings\platform.ps2.json`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\MenuComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscReturnToMenuComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\NintendoDsReturnOverlayComponent.cs`

## Task 1: Persist standard platform actions in project settings

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorInputProfileSettingsDocument.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorStandardPlatformActionSettingsDocument.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorInputControlSettingsDocument.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformProfileSettingsDocument.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorProfileSettingsService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorProfileSettingsServiceTests.cs`

- [ ] **Step 1: Add failing persistence tests for the new `input.standardActions` section**

Add two tests to `EditorProfileSettingsServiceTests.cs`:

```csharp
[Fact]
public void Load_WhenDsPlatformFileIsMissing_SeedsStandardPlatformActions() {
    EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

    EditorProfileSettingsDocument document = service.Load(new List<string> { "ds" });

    EditorPlatformProfileSettingsDocument ds = Assert.Single(document.Platforms);
    Assert.Equal("ds", ds.PlatformId);
    Assert.NotNull(ds.Input);
    Assert.NotNull(ds.Input.StandardActions.Accept);
    Assert.NotNull(ds.Input.StandardActions.Return);
}

[Fact]
public void Save_WhenStandardPlatformActionsAreConfigured_PersistsInputSection() {
    EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);
    EditorProfileSettingsDocument document = service.Load(new List<string> { "ps2" });

    EditorPlatformProfileSettingsDocument ps2 = Assert.Single(document.Platforms);
    ps2.Input.StandardActions.Accept.ControlIndex = 0;
    ps2.Input.StandardActions.Return.ControlIndex = 3;
    service.Save(document);

    string json = File.ReadAllText(Path.Combine(TempRootPath, "settings", "platform.ps2.json"));
    Assert.Contains("\"input\"", json, StringComparison.Ordinal);
    Assert.Contains("\"standardActions\"", json, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the focused profile-settings tests and confirm failure**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProfileSettingsServiceTests --no-restore -v minimal
```

Expected: FAIL because `EditorPlatformProfileSettingsDocument` does not have an `Input` section yet.

- [ ] **Step 3: Add the document types and seed logic**

Implement:

- `EditorInputProfileSettingsDocument`
- `EditorStandardPlatformActionSettingsDocument`
- `EditorInputControlSettingsDocument`

Then:

- add `Input` to `EditorPlatformProfileSettingsDocument`
- extend `EditorProfileSettingsService` normalization/seeding so:
  - `ds` seeds `Accept` and `Return`
  - `ps2` seeds `Accept` and `Return`
  - other platforms keep an empty but non-null `Input` object

Use persisted `deviceKind`, `controlKind`, `deviceIndex`, and `controlIndex` fields so the JSON stays generic.

- [ ] **Step 4: Re-run the focused profile-settings tests**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProfileSettingsServiceTests --no-restore -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the settings contract**

Run:

```powershell
git add C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorInputProfileSettingsDocument.cs C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorStandardPlatformActionSettingsDocument.cs C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorInputControlSettingsDocument.cs C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformProfileSettingsDocument.cs C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorProfileSettingsService.cs C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorProfileSettingsServiceTests.cs
git commit -m "feat: persist standard platform input actions"
```

## Task 2: Add engine-owned standard platform action runtime support

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformAction.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformActionIds.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformActionBinding.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformInputConfiguration.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformInput.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.input\InputSystem.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\StandardPlatformInputTests.cs`

- [ ] **Step 1: Add failing runtime tests for reserved action registration and querying**

Create `StandardPlatformInputTests.cs` with focused tests like:

```csharp
[Fact]
public void Initialize_WhenStandardPlatformBindingsAreConfigured_RegistersAcceptAction() {
    using Core core = CreateCoreWithStandardPlatformBindings(
        CreateBinding(StandardPlatformAction.Accept, InputGamepadButton.South));

    TestInputBackend input = (TestInputBackend)core.Input.Backend;
    input.SetGamepadButtonDown(0, InputGamepadButton.South, true);
    core.Input.EarlyUpdate();
    core.Input.Update();

    Assert.True(core.StandardPlatformInput.WasActionPressed(StandardPlatformAction.Accept));
}

[Fact]
public void Initialize_WhenStandardPlatformBindingsAreMissing_LeavesReturnUnbound() {
    using Core core = CreateCoreWithStandardPlatformBindings();

    Assert.False(core.StandardPlatformInput.WasActionPressed(StandardPlatformAction.Return));
}
```

Keep the helper code inside the test class methods that already exist in the file style for this test suite. Do not introduce local helper functions in production code.

- [ ] **Step 2: Run the focused runtime tests and confirm failure**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~StandardPlatformInputTests --no-restore -v minimal
```

Expected: FAIL because the standard action types and bootstrap path do not exist yet.

- [ ] **Step 3: Implement the minimal standard-action runtime layer**

Implement:

- `StandardPlatformAction`
- reserved ids in `StandardPlatformActionIds`
- one immutable configuration type holding the configured bindings
- one runtime helper that wraps `InputSystem.WasActionPressed(...)` and `IsActionDown(...)`

Then:

- add one `StandardPlatformInputConfiguration` property to `CoreInitializationOptions`
- add one `StandardPlatformInput` property to `Core`
- register the reserved bindings during `Core.Initialize(...)`

If `InputSystem` needs a small capability to clear or replace bindings for the reserved context, add the smallest API required there instead of bolting on duplicate state in `Core`.

- [ ] **Step 4: Re-run the focused standard-action runtime tests**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~StandardPlatformInputTests --no-restore -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the engine runtime layer**

Run:

```powershell
git add C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformAction.cs C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformActionIds.cs C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformActionBinding.cs C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformInputConfiguration.cs C:\dev\helworks\helengine\engine\helengine.input\StandardPlatformInput.cs C:\dev\helworks\helengine\engine\helengine.input\InputSystem.cs C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs C:\dev\helworks\helengine\engine\helengine.core\Core.cs C:\dev\helworks\helengine\engine\helengine.editor.tests\StandardPlatformInputTests.cs
git commit -m "feat: add standard platform input actions"
```

## Task 3: Carry standard action bindings into packaged runtime startup

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorRuntimeNativeManifestWriter.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorRuntimeNativeManifestWriterTests.cs`
- Modify: `C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsBootHost.cpp`

- [ ] **Step 1: Add failing tests for generated native manifest output**

Extend `EditorRuntimeNativeManifestWriterTests.cs` with assertions that the generated runtime source contains one standard-platform-action manifest entrypoint and both `Accept` and `Return` records when the cooked manifest carries those bindings.

Use checks similar to:

```csharp
Assert.Contains("he_runtime_standard_platform_actions", source, StringComparison.Ordinal);
Assert.Contains("Accept", source, StringComparison.Ordinal);
Assert.Contains("Return", source, StringComparison.Ordinal);
```

Add one DS-facing source audit expectation if needed later, but keep the first failure in the generic writer test.

- [ ] **Step 2: Run the writer tests and confirm failure**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorRuntimeNativeManifestWriterTests --no-restore -v minimal
```

Expected: FAIL because the native manifest writer does not emit standard-action data yet.

- [ ] **Step 3: Implement generated-manifest output and DS host consumption**

Update `EditorRuntimeNativeManifestWriter.cs` to emit one additional generated runtime source pair for standard platform actions, following the existing `runtime_startup_manifest` and `runtime_scene_catalog_manifest` pattern.

Update `NintendoDsBootHost.cpp` to:

- include the generated standard-platform-action manifest header
- build one `StandardPlatformInputConfiguration`
- assign it into `EngineOptions` before `Core.Initialize(...)`

Do not teach the DS boot host how to choose accept/return buttons. It should only consume generated metadata.

- [ ] **Step 4: Re-run the writer tests**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorRuntimeNativeManifestWriterTests --no-restore -v minimal
```

Expected: PASS.

- [ ] **Step 5: Run one DS builder-focused validation**

Run:

```powershell
dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter FullyQualifiedName~NintendoDsProgramTests --no-restore -v minimal
```

Expected: PASS, or tighten the filter to the smallest DS manifest/builder test that exercises generated-core staging if `NintendoDsProgramTests` is too broad after the first run.

- [ ] **Step 6: Commit the packaged-runtime plumbing**

Run:

```powershell
git add C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorRuntimeNativeManifestWriter.cs C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorRuntimeNativeManifestWriterTests.cs C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsBootHost.cpp
git commit -m "feat: pass standard platform actions into DS runtime"
```

## Task 4: Migrate `city` to standard actions and verify DS behavior

**Files:**
- Modify: `C:\dev\helprojs\city\settings\platform.ds.json`
- Modify: `C:\dev\helprojs\city\settings\platform.ps2.json`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\MenuComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscReturnToMenuComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\NintendoDsReturnOverlayComponent.cs`
- Modify: `C:\dev\helworks\helengine-ds\builder.tests\CityNintendoDsSceneSourceAuditTests.cs`

- [ ] **Step 1: Add failing city DS source-audit expectations**

Update `CityNintendoDsSceneSourceAuditTests.cs` so it expects standard action usage instead of raw DS face-button polling. Replace the current `InputGamepadButton.East` assertion with checks that the city sources call the new standard-action API.

Example assertions:

```csharp
Assert.Contains("StandardPlatformAction.Return", dsReturnOverlaySource, StringComparison.Ordinal);
Assert.DoesNotContain("InputGamepadButton.East", dsReturnOverlaySource, StringComparison.Ordinal);
Assert.Contains("StandardPlatformAction.Accept", menuComponentSource, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the focused city DS audit and confirm failure**

Run:

```powershell
dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter FullyQualifiedName~CityNintendoDsSceneSourceAuditTests --no-restore -v minimal
```

Expected: FAIL because `city` still polls raw `South` and `East`.

- [ ] **Step 3: Update city platform settings and menu call sites**

Modify:

- `platform.ds.json`
  - add `input.standardActions.accept`
  - add `input.standardActions.return`
- `platform.ps2.json`
  - add `input.standardActions.accept`
  - add `input.standardActions.return`
- `MenuComponent.cs`
  - use `Accept`
  - keep D-pad navigation raw in this slice
- `DemoDiscReturnToMenuComponent.cs`
  - use `Return`
- `NintendoDsReturnOverlayComponent.cs`
  - use `Return`

Preserve keyboard and pointer behavior. Only replace the platform-facing gamepad intent checks.

- [ ] **Step 4: Re-run the city DS source audit**

Run:

```powershell
dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter FullyQualifiedName~CityNintendoDsSceneSourceAuditTests --no-restore -v minimal
```

Expected: PASS.

- [ ] **Step 5: Run the smallest full verification for this slice**

Run:

```powershell
dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProfileSettingsServiceTests|FullyQualifiedName~StandardPlatformInputTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests --no-restore -v minimal
dotnet test C:\dev\helworks\helengine-ds\builder.tests\helengine.ds.builder.tests.csproj --filter FullyQualifiedName~CityNintendoDsSceneSourceAuditTests --no-restore -v minimal
dotnet build C:\dev\helprojs\city\city.sln --no-restore -v minimal
```

Expected: PASS.

- [ ] **Step 6: Commit the city migration**

Run:

```powershell
git add C:\dev\helprojs\city\settings\platform.ds.json C:\dev\helprojs\city\settings\platform.ps2.json C:\dev\helprojs\city\assets\codebase\menu\MenuComponent.cs C:\dev\helprojs\city\assets\codebase\menu\DemoDiscReturnToMenuComponent.cs C:\dev\helprojs\city\assets\codebase\menu\NintendoDsReturnOverlayComponent.cs C:\dev\helworks\helengine-ds\builder.tests\CityNintendoDsSceneSourceAuditTests.cs
git commit -m "feat: use standard platform actions in city menus"
```

## Notes for the implementing agent

- Keep the first slice intentionally narrow: `Accept` and `Return` only.
- Do not add editor UI for editing the mappings in this plan.
- Do not change D-pad navigation to logical actions yet.
- Do not normalize the DS backend raw button naming in this plan. The action mapping must make `city` behave correctly even if raw DS face-button names remain awkward internally.
- Follow the repo rules: one class per file, XML comments on every class/member, no local helper functions in production code, and use the smallest validation necessary for each change.
