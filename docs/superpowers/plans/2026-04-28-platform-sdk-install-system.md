# Platform SDK Install System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a launcher-managed platform install system that reads per-engine platform requirements from a central catalog, reuses shared SDK/platform-builder/platform-files artifacts across engine versions, persists only install-root locators in the Windows registry, and keeps authoritative install manifests on disk under the managed roots.

**Architecture:** Introduce a focused launcher install domain made of three layers: catalog contracts plus a mocked catalog source, local install-root and manifest persistence services, and an install planner that compares selected engine/platform requirements against the local registry before any install starts. Refactor the launcher `Engines` workflow to consume that planner, let users choose platforms, materialize mocked installs into managed folders, and ask before removing newly unreferenced shared artifacts during uninstall.

**Tech Stack:** C#/.NET 9, Avalonia 11, `System.Text.Json`, Windows Registry APIs, xUnit, `Avalonia.Headless`, existing `helengine.ui/helengine.sln`

---

## File Map

### New launcher model files

- `helengine.ui/helengine.launcher/Models/PlatformArtifactKind.cs`
  - Enum for `Sdk`, `PlatformBuilder`, and `PlatformFiles`.
- `helengine.ui/helengine.launcher/Models/ArtifactIdentity.cs`
  - Stable `kind + id + version` identity used for reuse checks and bindings.
- `helengine.ui/helengine.launcher/Models/CatalogArtifactRequirement.cs`
  - One required shared artifact entry from the central catalog.
- `helengine.ui/helengine.launcher/Models/EnginePlatformRequirement.cs`
  - One platform requirement under an engine version.
- `helengine.ui/helengine.launcher/Models/EngineCatalogEntry.cs`
  - One engine version and all of its platform requirements.
- `helengine.ui/helengine.launcher/Models/InstalledArtifact.cs`
  - One shared artifact installed under the toolchain root.
- `helengine.ui/helengine.launcher/Models/InstalledEnginePlatformBinding.cs`
  - Binding from one installed engine/platform pair to its shared artifacts.
- `helengine.ui/helengine.launcher/Models/LauncherInstallRoots.cs`
  - Resolved engine/toolchain roots used by the launcher install services.
- `helengine.ui/helengine.launcher/Models/PlatformInstallSelection.cs`
  - User-selected engine version plus chosen platforms for planning/install.
- `helengine.ui/helengine.launcher/Models/PlatformInstallPlan.cs`
  - Computed install plan with reusable, missing, and blocking items.
- `helengine.ui/helengine.launcher/Models/PlatformInstallPlanArtifactStatus.cs`
  - One artifact row inside the plan with state like `Reusable` or `Missing`.
- `helengine.ui/helengine.launcher/Models/UnusedArtifactRemovalDecision.cs`
  - Result model for uninstall cleanup prompts.

### Existing launcher model files to modify

- `helengine.ui/helengine.launcher/Models/EngineInstall.cs`
  - Split the current manifest helper class out of this file and extend `EngineInstall` with data needed for catalog-backed installs, while keeping one class per file.
- `helengine.ui/helengine.launcher/Models/EngineInstallManifest.cs`
  - New dedicated manifest file that tracks installed engine entries only.

### New launcher service files

- `helengine.ui/helengine.launcher/Services/IEnginePlatformCatalog.cs`
  - Catalog abstraction for available engine versions and per-platform requirements.
- `helengine.ui/helengine.launcher/Services/MockEnginePlatformCatalog.cs`
  - Local mocked catalog used until Sweet Square integration exists.
- `helengine.ui/helengine.launcher/Services/ILauncherInstallRootLocator.cs`
  - Abstraction over registry-backed root-path lookup and persistence.
- `helengine.ui/helengine.launcher/Services/WindowsLauncherInstallRootLocator.cs`
  - Windows registry implementation that stores only engine/toolchain root locators.
- `helengine.ui/helengine.launcher/Services/LauncherInstallRootResolver.cs`
  - Resolves effective roots using registry locators or default `%APPDATA%` paths.
- `helengine.ui/helengine.launcher/Services/InstalledArtifactManifest.cs`
  - On-disk manifest model for installed shared artifacts.
- `helengine.ui/helengine.launcher/Services/InstalledBindingManifest.cs`
  - On-disk manifest model for engine-platform bindings.
- `helengine.ui/helengine.launcher/Services/InstalledArtifactStore.cs`
  - Reads/writes the shared-artifact manifest under the toolchain root.
- `helengine.ui/helengine.launcher/Services/InstalledBindingStore.cs`
  - Reads/writes the engine-platform binding manifest.
- `helengine.ui/helengine.launcher/Services/EngineInstallManager.cs`
  - Refactor to use resolved install roots and on-disk manifests instead of a fixed `%APPDATA%/settings/engines.json`.
- `helengine.ui/helengine.launcher/Services/PlatformInstallPlanner.cs`
  - Computes reuse/missing/blocking state for a selected engine/platform set.
- `helengine.ui/helengine.launcher/Services/PlatformInstallExecutor.cs`
  - Mock install executor that materializes engine/artifact folders and updates manifests.
- `helengine.ui/helengine.launcher/Services/EngineUninstallPlanner.cs`
  - Computes which shared artifacts become unused after removing an engine.
- `helengine.ui/helengine.launcher/Services/EngineUninstallExecutor.cs`
  - Removes engine files, deletes bindings, and optionally removes unused artifacts.
- `helengine.ui/helengine.launcher/Services/JsonFormatting.cs`
  - Reuse the existing formatting helper if manifest files need stable formatting updates.

### Existing launcher service files to modify

- `helengine.ui/helengine.launcher/Services/ProjectScaffolder.cs`
  - Read the available engine version list from the refactored install manager without assuming only ad hoc local installs exist.
- `helengine.ui/helengine.launcher/Services/EditorProjectLauncher.cs`
  - Keep using exact engine version matching, but source installed engines from the refactored manifest-backed install manager.

### New or modified launcher view files

- `helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs`
  - Refactor from a flat installed-engine list into a catalog/install-management surface with platform selection and plan review.
- `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
  - Wire the new catalog/install services into the `Engines` workflow, root selection flow, install confirmation flow, and uninstall cleanup prompts.

### New or expanded launcher tests

- `helengine.ui/helengine.launcher.tests/MockEnginePlatformCatalogTests.cs`
  - Verifies mocked catalog shape and engine/platform requirement loading.
- `helengine.ui/helengine.launcher.tests/LauncherInstallRootResolverTests.cs`
  - Verifies registry-locator fallback and resolved root behavior.
- `helengine.ui/helengine.launcher.tests/EngineInstallManagerTests.cs`
  - Verifies manifest persistence under managed roots and rediscovery from those roots.
- `helengine.ui/helengine.launcher.tests/PlatformInstallPlannerTests.cs`
  - Verifies reuse, missing-artifact, and blocking-state computation.
- `helengine.ui/helengine.launcher.tests/PlatformInstallExecutorTests.cs`
  - Verifies mocked installs materialize folders and update manifests correctly.
- `helengine.ui/helengine.launcher.tests/EngineUninstallPlannerTests.cs`
  - Verifies unused shared-artifact detection after engine removal.
- `helengine.ui/helengine.launcher.tests/EnginesViewTests.cs`
  - Verifies platform selection and plan display behavior in the UI.
- `helengine.ui/helengine.launcher.tests/LauncherShellEngineInstallWorkflowTests.cs`
  - Verifies the shell-driven install/uninstall workflow end to end.

### Existing solution/project files to modify

- `helengine.ui/helengine.launcher/helengine.launcher.csproj`
  - Add any new source files automatically through SDK includes; no special project reference change expected unless registry APIs require an explicit package.
- `helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj`
  - Ensure the new focused launcher tests compile with any additional helpers.
- `helengine.ui/helengine.sln`
  - Include any newly added launcher test files automatically through the project; no new project expected.

## Implementation Notes

- Keep registry usage minimal. Only store the engine install root and shared toolchain root locators there.
- Keep the authoritative manifests on disk under those roots, not in `%APPDATA%/settings`.
- Treat artifact reuse as an exact identity match on `kind + id + version`. Do not add best-effort compatibility heuristics.
- Keep planning and install-state mutation out of Avalonia views. `EnginesView` should stay focused on presentation and event wiring.
- Follow repo conventions:
  - one class per file,
  - substantive XML comments on classes, properties, and methods,
  - no local helper functions,
  - keep fields PascalCase,
  - do not swallow install-state corruption with silent fallback if a valid state is required.
- The first execution should stay fully mocked for content acquisition. “Installing” an engine or shared artifact means creating the managed folder structure plus updating manifests so the planner, launcher, and future network layer all exercise the same boundaries.

### Task 1: Split The Install Domain Into Focused Models

**Files:**
- Create: `helengine.ui/helengine.launcher/Models/PlatformArtifactKind.cs`
- Create: `helengine.ui/helengine.launcher/Models/ArtifactIdentity.cs`
- Create: `helengine.ui/helengine.launcher/Models/CatalogArtifactRequirement.cs`
- Create: `helengine.ui/helengine.launcher/Models/EnginePlatformRequirement.cs`
- Create: `helengine.ui/helengine.launcher/Models/EngineCatalogEntry.cs`
- Create: `helengine.ui/helengine.launcher/Models/InstalledArtifact.cs`
- Create: `helengine.ui/helengine.launcher/Models/InstalledEnginePlatformBinding.cs`
- Create: `helengine.ui/helengine.launcher/Models/LauncherInstallRoots.cs`
- Create: `helengine.ui/helengine.launcher/Models/PlatformInstallSelection.cs`
- Create: `helengine.ui/helengine.launcher/Models/PlatformInstallPlan.cs`
- Create: `helengine.ui/helengine.launcher/Models/PlatformInstallPlanArtifactStatus.cs`
- Create: `helengine.ui/helengine.launcher/Models/UnusedArtifactRemovalDecision.cs`
- Modify: `helengine.ui/helengine.launcher/Models/EngineInstall.cs`
- Create: `helengine.ui/helengine.launcher/Models/EngineInstallManifest.cs`
- Test: `helengine.ui/helengine.launcher.tests/PlatformInstallDomainModelTests.cs`

- [ ] **Step 1: Write the failing model tests**

Add `PlatformInstallDomainModelTests.cs` with focused assertions for exact artifact identity and plan shape.

Suggested tests:

```csharp
[Fact]
public void ArtifactIdentity_EqualsOnlyWhenKindIdAndVersionMatch() {
    ArtifactIdentity first = new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0");
    ArtifactIdentity second = new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0");
    ArtifactIdentity differentVersion = new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "35.0");

    Assert.Equal(first, second);
    Assert.NotEqual(first, differentVersion);
}

[Fact]
public void PlatformInstallPlan_StartsWithEmptyCollections() {
    PlatformInstallPlan plan = new PlatformInstallPlan();

    Assert.Empty(plan.ReusableArtifacts);
    Assert.Empty(plan.MissingArtifacts);
    Assert.Empty(plan.BlockingIssues);
}
```

- [ ] **Step 2: Run the model tests to verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~PlatformInstallDomainModelTests" -v minimal
```

Expected: FAIL because the new model files and split manifest file do not exist yet.

- [ ] **Step 3: Implement the install domain models**

Implement the new files with:
- exact identity types for catalog and installed artifacts,
- empty-list defaults for plan collections,
- one-class-per-file compliance by moving `EngineInstallManifest` out of `EngineInstall.cs`,
- XML comments on all classes, properties, and constructors.

- [ ] **Step 4: Re-run the model tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~PlatformInstallDomainModelTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the model split**

```bash
rtk git add helengine.ui/helengine.launcher/Models helengine.ui/helengine.launcher.tests/PlatformInstallDomainModelTests.cs
rtk git commit -m "Add launcher platform install domain models"
```

### Task 2: Add Root Locator And Install-Root Resolution

**Files:**
- Create: `helengine.ui/helengine.launcher/Services/ILauncherInstallRootLocator.cs`
- Create: `helengine.ui/helengine.launcher/Services/WindowsLauncherInstallRootLocator.cs`
- Create: `helengine.ui/helengine.launcher/Services/LauncherInstallRootResolver.cs`
- Test: `helengine.ui/helengine.launcher.tests/LauncherInstallRootResolverTests.cs`

- [ ] **Step 1: Write the failing root-resolution tests**

Add tests covering:
- default root resolution when no registry values exist,
- preservation of explicitly chosen engine/toolchain roots,
- separate engine root and shared toolchain root values.

Suggested test:

```csharp
[Fact]
public void ResolveRoots_WhenNoRegistryValuesExist_UsesDefaultHelenginePaths() {
    FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator();
    LauncherInstallRootResolver resolver = new LauncherInstallRootResolver(locator);

    LauncherInstallRoots roots = resolver.Resolve();

    Assert.EndsWith(Path.Combine("helengine", "engines"), roots.EngineInstallRoot);
    Assert.EndsWith(Path.Combine("helengine", "toolchains"), roots.SharedToolchainRoot);
}
```

- [ ] **Step 2: Run the root-resolution tests to verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherInstallRootResolverTests" -v minimal
```

Expected: FAIL because the locator abstraction and resolver are not implemented yet.

- [ ] **Step 3: Implement registry locator and resolver services**

Implement:
- a small locator interface that only gets/sets the two root paths,
- a Windows registry implementation storing only those locator strings,
- a resolver that maps missing values to default `%APPDATA%/helengine/engines` and `%APPDATA%/helengine/toolchains`.

Keep raw registry access inside `WindowsLauncherInstallRootLocator`; no view or manager should touch registry APIs directly.

- [ ] **Step 4: Re-run the root-resolution tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherInstallRootResolverTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the root-location layer**

```bash
rtk git add helengine.ui/helengine.launcher/Services/ILauncherInstallRootLocator.cs helengine.ui/helengine.launcher/Services/WindowsLauncherInstallRootLocator.cs helengine.ui/helengine.launcher/Services/LauncherInstallRootResolver.cs helengine.ui/helengine.launcher.tests/LauncherInstallRootResolverTests.cs
rtk git commit -m "Add launcher install root locator services"
```

### Task 3: Move Install State To Managed On-Disk Manifests

**Files:**
- Create: `helengine.ui/helengine.launcher/Services/InstalledArtifactManifest.cs`
- Create: `helengine.ui/helengine.launcher/Services/InstalledBindingManifest.cs`
- Create: `helengine.ui/helengine.launcher/Services/InstalledArtifactStore.cs`
- Create: `helengine.ui/helengine.launcher/Services/InstalledBindingStore.cs`
- Modify: `helengine.ui/helengine.launcher/Services/EngineInstallManager.cs`
- Test: `helengine.ui/helengine.launcher.tests/EngineInstallManagerTests.cs`

- [ ] **Step 1: Write the failing manifest-persistence tests**

Add tests covering:
- installed engines load from the resolved engine root instead of a fixed settings file,
- installed artifacts persist under the shared toolchain root,
- a fresh manager instance rediscovers prior installs from those manifests.

Suggested test:

```csharp
[Fact]
public void Load_WhenManagedRootContainsEngineManifest_RestoresInstalledEngines() {
    string tempRoot = CreateTempRoot();
    WriteEngineManifest(tempRoot, "1.2.3");

    EngineInstallManager manager = CreateManagerForRoots(tempRoot, Path.Combine(tempRoot, "toolchains"));

    Assert.Single(manager.InstalledEngines);
    Assert.Equal("1.2.3", manager.InstalledEngines[0].Version);
}
```

- [ ] **Step 2: Run the install-manager tests to verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~EngineInstallManagerTests" -v minimal
```

Expected: FAIL because the manager still uses the fixed `%APPDATA%/settings/engines.json` path and the artifact/binding stores do not exist yet.

- [ ] **Step 3: Implement on-disk manifest stores and refactor the manager**

Implement:
- one store for shared installed artifacts,
- one store for engine-platform bindings,
- a refactored `EngineInstallManager` that resolves its engine manifest path from `LauncherInstallRootResolver`,
- directory creation under the managed roots instead of the old settings folder.

Keep the public `InstalledEngines` surface stable enough that existing project-launch and project-create code can keep consuming it.

- [ ] **Step 4: Re-run the install-manager tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~EngineInstallManagerTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the manifest-backed persistence**

```bash
rtk git add helengine.ui/helengine.launcher/Services/InstalledArtifactManifest.cs helengine.ui/helengine.launcher/Services/InstalledBindingManifest.cs helengine.ui/helengine.launcher/Services/InstalledArtifactStore.cs helengine.ui/helengine.launcher/Services/InstalledBindingStore.cs helengine.ui/helengine.launcher/Services/EngineInstallManager.cs helengine.ui/helengine.launcher.tests/EngineInstallManagerTests.cs
rtk git commit -m "Move launcher install state under managed roots"
```

### Task 4: Add The Mock Catalog And Install Planner

**Files:**
- Create: `helengine.ui/helengine.launcher/Services/IEnginePlatformCatalog.cs`
- Create: `helengine.ui/helengine.launcher/Services/MockEnginePlatformCatalog.cs`
- Create: `helengine.ui/helengine.launcher/Services/PlatformInstallPlanner.cs`
- Create: `helengine.ui/helengine.launcher.tests/MockEnginePlatformCatalogTests.cs`
- Create: `helengine.ui/helengine.launcher.tests/PlatformInstallPlannerTests.cs`

- [ ] **Step 1: Write the failing catalog and planner tests**

Add catalog tests for:
- known engine versions returning platform requirements,
- exact SDK/builder/platform-files identities being exposed.

Add planner tests for:
- exact artifact reuse,
- missing shared artifacts,
- blocking state when local manifests reference missing install paths.

Suggested planner test:

```csharp
[Fact]
public void BuildPlan_WhenExactSdkAlreadyExists_MarksItReusable() {
    MockEnginePlatformCatalog catalog = CreateCatalog();
    PlatformInstallPlanner planner = CreatePlanner(catalog, installedArtifacts: new[] {
        new InstalledArtifact(new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0"), @"C:\toolchains\sdks\android-sdk-34.0")
    });

    PlatformInstallPlan plan = planner.Build(new PlatformInstallSelection("1.2.3", new[] { "android" }));

    Assert.Contains(plan.ReusableArtifacts, item => item.Identity.Id == "android-sdk");
    Assert.Empty(plan.BlockingIssues);
}
```

- [ ] **Step 2: Run the catalog and planner tests to verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~MockEnginePlatformCatalogTests|FullyQualifiedName~PlatformInstallPlannerTests" -v minimal
```

Expected: FAIL because the catalog abstraction and planner do not exist yet.

- [ ] **Step 3: Implement the mock catalog and planning service**

Implement:
- one interface for catalog access,
- one mocked catalog with a few hard-coded engine/platform combinations,
- one planner that compares catalog artifact identities against installed manifests and produces explicit `Reusable`, `Missing`, and `Blocking` output.

Do not let the planner mutate disk state; it should be a pure decision service.

- [ ] **Step 4: Re-run the catalog and planner tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~MockEnginePlatformCatalogTests|FullyQualifiedName~PlatformInstallPlannerTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the catalog and planner**

```bash
rtk git add helengine.ui/helengine.launcher/Services/IEnginePlatformCatalog.cs helengine.ui/helengine.launcher/Services/MockEnginePlatformCatalog.cs helengine.ui/helengine.launcher/Services/PlatformInstallPlanner.cs helengine.ui/helengine.launcher.tests/MockEnginePlatformCatalogTests.cs helengine.ui/helengine.launcher.tests/PlatformInstallPlannerTests.cs
rtk git commit -m "Add mocked platform catalog and install planner"
```

### Task 5: Implement Mock Install And Uninstall Execution

**Files:**
- Create: `helengine.ui/helengine.launcher/Services/PlatformInstallExecutor.cs`
- Create: `helengine.ui/helengine.launcher/Services/EngineUninstallPlanner.cs`
- Create: `helengine.ui/helengine.launcher/Services/EngineUninstallExecutor.cs`
- Create: `helengine.ui/helengine.launcher.tests/PlatformInstallExecutorTests.cs`
- Create: `helengine.ui/helengine.launcher.tests/EngineUninstallPlannerTests.cs`
- Modify: `helengine.ui/helengine.launcher/Services/ProjectScaffolder.cs`
- Modify: `helengine.ui/helengine.launcher/Services/EditorProjectLauncher.cs`

- [ ] **Step 1: Write the failing executor tests**

Add tests covering:
- install creates engine/artifact folders under the correct roots,
- install writes engine, artifact, and binding manifests,
- uninstall identifies newly unused artifacts,
- uninstall keeps or removes those artifacts based on the explicit user decision.

Suggested uninstall-planner test:

```csharp
[Fact]
public void BuildUnusedArtifactSet_WhenRemovedEngineWasLastReference_ReturnsArtifactsForPrompt() {
    EngineUninstallPlanner planner = CreateUninstallPlannerWithSingleReference();

    IReadOnlyList<ArtifactIdentity> unused = planner.GetUnusedArtifactsAfterRemoving("1.2.3");

    Assert.Single(unused);
    Assert.Equal("android-sdk", unused[0].Id);
}
```

- [ ] **Step 2: Run the executor tests to verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~PlatformInstallExecutorTests|FullyQualifiedName~EngineUninstallPlannerTests" -v minimal
```

Expected: FAIL because the execution services do not exist yet.

- [ ] **Step 3: Implement mocked materialization and uninstall cleanup services**

Implement:
- mock folder creation for engines and shared artifacts,
- manifest updates on successful install,
- binding removal and unused-artifact computation on uninstall,
- optional artifact deletion based on the prompt decision,
- any required manager updates so `ProjectScaffolder` and `EditorProjectLauncher` still see the installed engine list after a mock install.

- [ ] **Step 4: Re-run the executor tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~PlatformInstallExecutorTests|FullyQualifiedName~EngineUninstallPlannerTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the execution layer**

```bash
rtk git add helengine.ui/helengine.launcher/Services/PlatformInstallExecutor.cs helengine.ui/helengine.launcher/Services/EngineUninstallPlanner.cs helengine.ui/helengine.launcher/Services/EngineUninstallExecutor.cs helengine.ui/helengine.launcher/Services/ProjectScaffolder.cs helengine.ui/helengine.launcher/Services/EditorProjectLauncher.cs helengine.ui/helengine.launcher.tests/PlatformInstallExecutorTests.cs helengine.ui/helengine.launcher.tests/EngineUninstallPlannerTests.cs
rtk git commit -m "Add mocked platform install execution"
```

### Task 6: Refactor The Launcher `Engines` Workflow Around Planning

**Files:**
- Modify: `helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs`
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Create: `helengine.ui/helengine.launcher.tests/EnginesViewTests.cs`
- Create: `helengine.ui/helengine.launcher.tests/LauncherShellEngineInstallWorkflowTests.cs`

- [ ] **Step 1: Write the failing view and workflow tests**

Add tests covering:
- the engines page renders catalog engines and per-platform selectors,
- selecting platforms causes the shell to request a plan,
- confirming the plan runs the mocked install executor,
- uninstall prompts include newly unused shared artifacts.

Suggested shell workflow test:

```csharp
[Fact]
public async Task InstallWorkflow_WhenPlatformsAreSelected_UsesPlannerAndExecutor() {
    LauncherShell shell = CreateShellWithMockCatalogAndExecutors();

    await shell.ShowEnginesAsync();
    await SelectPlatformAsync(shell, "1.2.3", "android");
    await ConfirmInstallAsync(shell);

    Assert.Contains("installed", shell.StatusText.Text, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the view and workflow tests to verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~EnginesViewTests|FullyQualifiedName~LauncherShellEngineInstallWorkflowTests" -v minimal
```

Expected: FAIL because the current `EnginesView` still renders only installed local builds and the shell does not know about planning/install flows.

- [ ] **Step 3: Implement the UI refactor**

Implement:
- an `EnginesView` surface that displays catalog engines, platform choices, and plan summary rows,
- shell event wiring for root selection, plan generation, install confirmation, and uninstall cleanup prompt decisions,
- status messages that clearly distinguish planning failures, successful installs, and uninstall cleanup prompts.

Keep control creation and pointer/selection wiring in the view, and keep all planning/execution decisions in services.

- [ ] **Step 4: Re-run the view and workflow tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~EnginesViewTests|FullyQualifiedName~LauncherShellEngineInstallWorkflowTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the launcher workflow refactor**

```bash
rtk git add helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs helengine.ui/helengine.launcher/Views/LauncherShell.cs helengine.ui/helengine.launcher.tests/EnginesViewTests.cs helengine.ui/helengine.launcher.tests/LauncherShellEngineInstallWorkflowTests.cs
rtk git commit -m "Refactor launcher engines workflow around platform installs"
```

### Task 7: Final Verification And Cleanup

**Files:**
- Review: `helengine.ui/helengine.launcher/Models/*.cs`
- Review: `helengine.ui/helengine.launcher/Services/*.cs`
- Review: `helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs`
- Review: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Review: `helengine.ui/helengine.launcher.tests/*.cs`

- [ ] **Step 1: Run the focused launcher install test set**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~PlatformInstallDomainModelTests|FullyQualifiedName~LauncherInstallRootResolverTests|FullyQualifiedName~EngineInstallManagerTests|FullyQualifiedName~MockEnginePlatformCatalogTests|FullyQualifiedName~PlatformInstallPlannerTests|FullyQualifiedName~PlatformInstallExecutorTests|FullyQualifiedName~EngineUninstallPlannerTests|FullyQualifiedName~EnginesViewTests|FullyQualifiedName~LauncherShellEngineInstallWorkflowTests" -v minimal
```

Expected: PASS.

- [ ] **Step 2: Run the broader launcher test suite**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal
```

Expected: PASS.

- [ ] **Step 3: Build the launcher**

Run:

```bash
rtk dotnet build helengine.ui/helengine.launcher/helengine.launcher.csproj -v minimal
```

Expected: `0 errors, 0 warnings`.

- [ ] **Step 4: Review the worktree for unintended changes**

Run:

```bash
rtk git status --short
```

Expected: only the intended launcher install-system files are modified before the final commit.

- [ ] **Step 5: Commit the verification pass**

```bash
rtk git add helengine.ui/helengine.launcher helengine.ui/helengine.launcher.tests
rtk git commit -m "Finalize platform SDK install system"
```
