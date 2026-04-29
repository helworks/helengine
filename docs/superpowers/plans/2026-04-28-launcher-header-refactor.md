# Launcher Header Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the launcher so `LauncherShell` owns a compact editor-aligned header with top-right page actions, while the existing `Home`, `New Project`, and `Engines` pages become cleaner body-only views.

**Architecture:** Add a small launcher-specific header contract so page views can describe their title, subtitle, and actions without rendering their own header chrome. Move the shared framing into `LauncherShell`, simplify the page views to content bodies, and add a focused launcher test project so the new shell/action layout can be verified with TDD instead of relying only on manual inspection.

**Tech Stack:** C#, Avalonia 11, xUnit, `Avalonia.Headless`, launcher desktop app, existing launcher theme classes

---

## File Map

### Existing files to modify

- `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
  - Move shared header rendering here, host top-right action buttons, and bind active page header state.
- `helengine.ui/helengine.launcher/Views/Pages/HomeView.cs`
  - Remove embedded top button row and expose only recent-project body content plus header metadata/actions.
- `helengine.ui/helengine.launcher/Views/Pages/NewProjectView.cs`
  - Remove embedded page header/action strip and expose form body plus header metadata/actions.
- `helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs`
  - Remove embedded page header/action strip and expose engine list body plus header metadata/actions.
- `helengine.ui/helengine.launcher/Theme/LauncherTheme.cs`
  - Tighten shell/header spacing and button styling so the launcher framing feels closer to the editor.
- `helengine.ui/helengine.sln`
  - Add the new launcher test project if the solution should build and run the launcher tests from the existing UI solution.

### New files to create

- `helengine.ui/helengine.launcher/Views/LauncherHeaderAction.cs`
  - Small model for one header action button, including label, visual role, enabled state, and click callback.
- `helengine.ui/helengine.launcher/Views/LauncherHeaderActionKind.cs`
  - Enum or similar type that distinguishes primary vs secondary header actions.
- `helengine.ui/helengine.launcher/Views/LauncherHeaderState.cs`
  - Immutable page-provided header state containing title, optional subtitle, and action list.
- `helengine.ui/helengine.launcher/Views/Pages/ILauncherPage.cs`
  - Page contract that lets `LauncherShell` query header state from the active page without hard-coded per-page layout logic.
- `helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj`
  - Focused xUnit test project for launcher UI behavior using Avalonia headless test support.
- `helengine.ui/helengine.launcher.tests/LauncherShellHeaderTests.cs`
  - Verifies the shared shell header renders the active page title and the correct top-right actions.
- `helengine.ui/helengine.launcher.tests/LauncherPageBodyTests.cs`
  - Verifies the page views no longer render their own duplicated header action rows and still expose their body content correctly.
- `helengine.ui/helengine.launcher.tests/TestAppBuilder.cs`
  - Minimal headless Avalonia test bootstrap helper if the headless test package needs one local setup point.

## Implementation Notes

- Keep logic in the existing launcher services. This refactor is UI composition only.
- Do not move project creation, engine detection, or recent-project persistence into new shell helpers.
- Prefer the smallest possible header contract. One interface and one immutable state object should be enough.
- Keep the current page flow and event semantics intact; only the shell-owned presentation should change.
- Keep page-specific inline status messaging in the page bodies, and keep the footer status owned by `LauncherShell`.
- Follow TDD: write the focused launcher UI tests first, then add the minimal layout code to satisfy them.

### Task 1: Add A Focused Launcher Test Harness

**Files:**
- Create: `helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj`
- Create: `helengine.ui/helengine.launcher.tests/TestAppBuilder.cs`
- Modify: `helengine.ui/helengine.sln`

- [ ] **Step 1: Create the launcher test project with headless Avalonia support**

Create `helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>helengine.editor.launcher.tests</RootNamespace>
    <AssemblyName>helengine.editor.launcher.tests</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Avalonia.Headless" Version="11.2.3" />
    <PackageReference Include="Avalonia.Headless.XUnit" Version="11.2.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.launcher\helengine.launcher.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add minimal Avalonia test bootstrap**

Create `helengine.ui/helengine.launcher.tests/TestAppBuilder.cs` with one helper that initializes headless Avalonia once for launcher control tests.

```csharp
public static class TestAppBuilder {
    public static AppBuilder Build() {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
```

- [ ] **Step 3: Add the new test project to the UI solution**

Update `helengine.ui/helengine.sln` so the launcher test project sits alongside the existing launcher projects.

- [ ] **Step 4: Run the empty launcher test project to verify the harness builds**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal`

Expected: PASS with `0` or more tests discovered once the harness compiles cleanly.

- [ ] **Step 5: Commit the test harness**

```bash
rtk git add helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj helengine.ui/helengine.launcher.tests/TestAppBuilder.cs helengine.ui/helengine.sln
rtk git commit -m "Add launcher headless test harness"
```

### Task 2: Move Header Ownership Into `LauncherShell`

**Files:**
- Create: `helengine.ui/helengine.launcher/Views/LauncherHeaderAction.cs`
- Create: `helengine.ui/helengine.launcher/Views/LauncherHeaderActionKind.cs`
- Create: `helengine.ui/helengine.launcher/Views/LauncherHeaderState.cs`
- Create: `helengine.ui/helengine.launcher/Views/Pages/ILauncherPage.cs`
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Test: `helengine.ui/helengine.launcher.tests/LauncherShellHeaderTests.cs`

- [ ] **Step 1: Write the failing shell-header tests**

Create `LauncherShellHeaderTests.cs` with tests that:
- build a `LauncherShell`,
- assert the default `Home` page title appears in the shell header,
- assert the shell header contains `create project`, `browse project`, and `engine versions` on the top-right,
- switch to `New Project` and assert the shell header now contains `back`, `browse`, `create project`, and `clear`,
- switch to `Engines` and assert the shell header now contains `back` and `install from folder`.

Test shape:

```csharp
[Fact]
public void LauncherShell_WhenHomeIsActive_RendersHomeActionsInSharedHeader() {
    LauncherShell shell = new LauncherShell();

    IReadOnlyList<Button> headerButtons = FindHeaderButtons(shell);

    Assert.Contains(headerButtons, b => Equals(b.Content, "create project"));
    Assert.Contains(headerButtons, b => Equals(b.Content, "browse project"));
    Assert.Contains(headerButtons, b => Equals(b.Content, "engine versions"));
}
```

- [ ] **Step 2: Run the header tests to verify they fail**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherShellHeaderTests" -v minimal`

Expected: FAIL because the shell does not yet expose a shared header action area and the page views still own their own header buttons.

- [ ] **Step 3: Add the minimal header contract**

Create:

`LauncherHeaderActionKind.cs`

```csharp
public enum LauncherHeaderActionKind {
    Primary,
    Secondary
}
```

`LauncherHeaderAction.cs`

```csharp
public sealed class LauncherHeaderAction {
    public string Label { get; }
    public LauncherHeaderActionKind Kind { get; }
    public bool IsEnabled { get; }
    public Action Callback { get; }
}
```

`LauncherHeaderState.cs`

```csharp
public sealed class LauncherHeaderState {
    public string Title { get; }
    public string Subtitle { get; }
    public IReadOnlyList<LauncherHeaderAction> Actions { get; }
}
```

`ILauncherPage.cs`

```csharp
public interface ILauncherPage {
    LauncherHeaderState BuildHeaderState();
}
```

- [ ] **Step 4: Refactor `LauncherShell` to render the shared header**

Update `LauncherShell.cs` to:
- keep one shell-owned header with compact branding on the left,
- add shell-owned title/subtitle text blocks,
- add a right-aligned action host,
- read `LauncherHeaderState` from the active page,
- render header buttons consistently from `LauncherHeaderAction`,
- refresh the header whenever `ShowHome()`, `ShowNewProject()`, or `ShowEngines()` changes the active page.

Keep the existing navigation and service calls in place.

- [ ] **Step 5: Re-run the header tests to verify they pass**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherShellHeaderTests" -v minimal`

Expected: PASS.

- [ ] **Step 6: Commit the shell-header refactor**

```bash
rtk git add helengine.ui/helengine.launcher/Views/LauncherHeaderAction.cs helengine.ui/helengine.launcher/Views/LauncherHeaderActionKind.cs helengine.ui/helengine.launcher/Views/LauncherHeaderState.cs helengine.ui/helengine.launcher/Views/Pages/ILauncherPage.cs helengine.ui/helengine.launcher/Views/LauncherShell.cs helengine.ui/helengine.launcher.tests/LauncherShellHeaderTests.cs
rtk git commit -m "Move launcher page actions into shared header"
```

### Task 3: Simplify Page Views To Body-Only Content

**Files:**
- Modify: `helengine.ui/helengine.launcher/Views/Pages/HomeView.cs`
- Modify: `helengine.ui/helengine.launcher/Views/Pages/NewProjectView.cs`
- Modify: `helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs`
- Test: `helengine.ui/helengine.launcher.tests/LauncherPageBodyTests.cs`

- [ ] **Step 1: Write the failing page-body tests**

Create `LauncherPageBodyTests.cs` with tests that:
- assert `HomeView` still renders the recent-project section but no longer renders its own top action row,
- assert `NewProjectView` still renders the project form fields but no longer renders its own embedded `back` and `create project` header strip,
- assert `EnginesView` still renders its engine list/status region but no longer renders its own embedded header/action row.

Example:

```csharp
[Fact]
public void HomeView_DoesNotRenderEmbeddedTopActionRow() {
    HomeView view = new HomeView();

    Assert.DoesNotContain(FindButtons(view), b => Equals(b.Content, "browse project"));
}
```

- [ ] **Step 2: Run the page-body tests to verify they fail**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherPageBodyTests" -v minimal`

Expected: FAIL because the page views still render their old local header/button strips.

- [ ] **Step 3: Remove duplicated header chrome from the page views**

Update `HomeView.cs` to:
- implement `ILauncherPage`,
- return a `LauncherHeaderState` for `create project`, `browse project`, and `engine versions`,
- remove the top button grid from the body content,
- keep the recent-project list body intact.

Update `NewProjectView.cs` to:
- implement `ILauncherPage`,
- return a `LauncherHeaderState` for `back`, `browse`, `create project`, and `clear`,
- remove the embedded page header row and bottom action strip,
- keep the form fields and inline status text in the body.

Update `EnginesView.cs` to:
- implement `ILauncherPage`,
- return a `LauncherHeaderState` for `back` and `install from folder`,
- remove the embedded page header/action row,
- keep the engine list and local status message in the body.

- [ ] **Step 4: Re-run the page-body tests to verify they pass**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherPageBodyTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Re-run the header tests to catch regressions**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherShellHeaderTests|FullyQualifiedName~LauncherPageBodyTests" -v minimal`

Expected: PASS.

- [ ] **Step 6: Commit the page simplification**

```bash
rtk git add helengine.ui/helengine.launcher/Views/Pages/HomeView.cs helengine.ui/helengine.launcher/Views/Pages/NewProjectView.cs helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs helengine.ui/helengine.launcher.tests/LauncherPageBodyTests.cs
rtk git commit -m "Simplify launcher pages to body-only views"
```

### Task 4: Tighten Visual Layout And Verify The Desktop Experience

**Files:**
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Modify: `helengine.ui/helengine.launcher/Theme/LauncherTheme.cs`
- Test: `helengine.ui/helengine.launcher.tests/LauncherShellHeaderTests.cs`

- [ ] **Step 1: Write one failing layout-focused test**

Add one targeted assertion to `LauncherShellHeaderTests.cs` that verifies the shell header action host is aligned as a distinct right-side container instead of being mixed into the page body.

Example:

```csharp
[Fact]
public void LauncherShell_RendersHeaderActionsInsideDedicatedRightAlignedHost() {
    LauncherShell shell = new LauncherShell();

    Panel host = FindHeaderActionHost(shell);

    Assert.Equal(HorizontalAlignment.Right, host.HorizontalAlignment);
}
```

- [ ] **Step 2: Run the combined launcher tests to verify the new assertion fails**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal`

Expected: FAIL because the shell still needs its final action-host and spacing polish.

- [ ] **Step 3: Apply the minimal layout and theme refinements**

Update `LauncherShell.cs` and `LauncherTheme.cs` to:
- reduce top-bar padding and static-brand visual weight,
- keep branding compact on the left,
- keep page title/subtitle on the same visual band as the header actions,
- normalize primary and secondary header button sizes,
- reduce outer content padding so the page body uses more of the window,
- preserve the existing dark-lilac palette while making the frame feel closer to the editor.

- [ ] **Step 4: Re-run the full launcher test project**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal`

Expected: PASS.

- [ ] **Step 5: Build and run the desktop launcher for manual verification**

Run:

```bash
rtk dotnet build helengine.ui/helengine.launcher.Desktop/helengine.launcher.Desktop.csproj -v minimal
rtk dotnet run --project helengine.ui/helengine.launcher.Desktop/helengine.launcher.Desktop.csproj
```

Manual checklist:
- header buttons sit on the top-right and align vertically with the header,
- page content starts higher and uses more vertical space,
- `Home`, `New Project`, and `Engines` still navigate correctly,
- `browse`, `create project`, and `install from folder` still work from their new header positions,
- launcher visuals feel closer to the editor without losing the current color identity.

- [ ] **Step 6: Commit the visual pass**

```bash
rtk git add helengine.ui/helengine.launcher/Views/LauncherShell.cs helengine.ui/helengine.launcher/Theme/LauncherTheme.cs helengine.ui/helengine.launcher.tests/LauncherShellHeaderTests.cs
rtk git commit -m "Polish launcher header layout"
```

### Task 5: Final Verification

**Files:**
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Modify: `helengine.ui/helengine.launcher/Views/Pages/HomeView.cs`
- Modify: `helengine.ui/helengine.launcher/Views/Pages/NewProjectView.cs`
- Modify: `helengine.ui/helengine.launcher/Views/Pages/EnginesView.cs`
- Modify: `helengine.ui/helengine.launcher/Theme/LauncherTheme.cs`
- Create: `helengine.ui/helengine.launcher.tests/*`

- [ ] **Step 1: Run the launcher test project one more time**

Run: `rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal`

Expected: PASS.

- [ ] **Step 2: Run a final desktop launcher build**

Run: `rtk dotnet build helengine.ui/helengine.launcher.Desktop/helengine.launcher.Desktop.csproj -v minimal`

Expected: PASS.

- [ ] **Step 3: Check git diff for accidental unrelated changes**

Run: `rtk git status --short`

Expected: Only the launcher refactor files and the known untracked `.codex` directory.

