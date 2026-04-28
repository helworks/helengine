# Shared `.heproj` Format Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a shared `.heproj` format library that owns the canonical project-file contract and refactor both the launcher and editor to consume it for project metadata, engine-version compatibility, and supported-platform display.

**Architecture:** Add one focused engine-side library that models, validates, reads, and writes `.heproj` files without depending on launcher UI or editor runtime code. Refactor the launcher to build recent-project cards from that shared contract, then refactor the editor entry and session path handling to resolve and validate project files through the same contract while keeping local settings in `settings/project.json`.

**Tech Stack:** C#/.NET 9, `System.Text.Json`, Avalonia 11, xUnit, `Avalonia.Headless`, WinForms editor host, existing `helengine.ui/helengine.sln`

---

## File Map

### New projects

- `engine/helengine.projectfile/helengine.projectfile.csproj`
  - Shared `.heproj` contract library with no launcher or editor UI dependencies.
- `engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj`
  - Focused unit tests for read/write behavior, validation, and forward-compatible parsing.

### New shared-library files

- `engine/helengine.projectfile/ProjectFileDocument.cs`
  - Canonical in-memory model for `.heproj`.
- `engine/helengine.projectfile/ProjectFileReadErrorCode.cs`
  - Enumerates structured load failure categories.
- `engine/helengine.projectfile/ProjectFileReadError.cs`
  - Carries one structured load failure.
- `engine/helengine.projectfile/ProjectFileReadResult.cs`
  - Wraps successful reads vs structured failures.
- `engine/helengine.projectfile/ProjectFileReader.cs`
  - Parses and validates `.heproj` files from disk.
- `engine/helengine.projectfile/ProjectFileWriter.cs`
  - Writes stable canonical `.heproj` JSON to disk.
- `engine/helengine.projectfile/ProjectFileJsonModel.cs`
  - Internal JSON shape used by `System.Text.Json`.

### Existing launcher files to modify

- `helengine.ui/helengine.launcher/Models/RecentProject.cs`
  - Extend the launcher view model with required engine version and supported platforms.
- `helengine.ui/helengine.launcher/Services/ProjectFileLoader.cs`
  - Replace ad hoc JSON parsing with shared-library reads and structured error handling.
- `helengine.ui/helengine.launcher/Services/ProjectScaffolder.cs`
  - Write canonical `.heproj` files through the shared writer and keep `settings/project.json` local-only.
- `helengine.ui/helengine.launcher/Views/Pages/HomeView.cs`
  - Render engine version and supported platforms from the shared contract.
- `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
  - Surface shared-library load failures cleanly when browsing/opening projects.
- `helengine.ui/helengine.launcher/helengine.launcher.csproj`
  - Add the shared project-file library reference.
- `helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj`
  - Ensure launcher tests compile against the refactored models and services.

### Existing editor files to modify

- `helengine.ui/helengine.editor.app/Program.cs`
  - Resolve the incoming project argument to a canonical `.heproj` path and reject invalid targets early.
- `helengine.ui/helengine.editor.app/MainForm.cs`
  - Use the canonical project file path while still deriving the project root for asset paths.
- `engine/helengine.editor/EditorSession.cs`
  - Consume the canonical project file path consistently for display-name/root-path resolution.
- `engine/helengine.editor/helengine.editor.csproj`
  - Add the shared project-file library reference.
- `engine/helengine.editor.tests/helengine.editor.tests.csproj`
  - Ensure editor tests compile against the new project-file dependency.

### Existing solution files to modify

- `helengine.ui/helengine.sln`
  - Add the new shared library and shared-library test project.

### New or expanded test files

- `engine/helengine.projectfile.tests/ProjectFileReaderTests.cs`
  - Coverage for valid loads, invalid JSON, missing fields, unsupported format versions, and unknown platforms.
- `engine/helengine.projectfile.tests/ProjectFileWriterTests.cs`
  - Coverage for stable write shape and round-trip preservation.
- `helengine.ui/helengine.launcher.tests/ProjectFileLoaderTests.cs`
  - Update to assert launcher loader behavior is driven by the shared contract.
- `helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs`
  - Expand to cover shared-library load failures surfacing through launcher status.
- `helengine.ui/helengine.launcher.tests/HomeViewTests.cs`
  - New focused coverage for supported-platform and required-engine display.
- `engine/helengine.editor.tests/EditorProjectFileResolutionTests.cs`
  - New coverage for `.heproj` resolution and invalid project rejection.

## Implementation Notes

- Keep `settings/project.json` local-only. It may store active platform or other local settings, but it must not remain the source of truth for `requiredEngineVersion` or `supportedPlatforms`.
- Treat `supportedPlatforms` as arbitrary string ids. Never crash on unknown values.
- Fail clearly on unsupported `projectFormatVersion`. Do not fabricate defaults for invalid canonical files.
- Preserve the current launcher header refactor and `.heproj` browse work already in the tree. This plan layers the shared-format library underneath that work instead of backing it out.
- Follow repo C# conventions while implementing:
  - one class per file,
  - substantive XML comments on classes, properties, and methods,
  - no local helper functions,
  - keep UI classes focused on presentation/input wiring and parsing logic in services.

### Task 1: Scaffold The Shared Project-File Library

**Files:**
- Create: `engine/helengine.projectfile/helengine.projectfile.csproj`
- Create: `engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj`
- Modify: `helengine.ui/helengine.sln`
- Modify: `helengine.ui/helengine.launcher/helengine.launcher.csproj`
- Modify: `engine/helengine.editor/helengine.editor.csproj`
- Modify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing structure test**

Create `engine/helengine.projectfile.tests/ProjectFileLibrarySmokeTests.cs` with one minimal compile-time assertion that references `ProjectFileDocument` and `ProjectFileReader`.

Suggested test:

```csharp
[Fact]
public void SharedProjectFileLibrary_ExposesCanonicalEntryPoints() {
    ProjectFileReader reader = new ProjectFileReader();
    ProjectFileDocument document = new ProjectFileDocument();

    Assert.NotNull(reader);
    Assert.NotNull(document);
}
```

- [ ] **Step 2: Run the new shared-library test project and verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj -v minimal
```

Expected: FAIL because the project and types do not exist yet.

- [ ] **Step 3: Create the project scaffolding and solution references**

Implement the minimum scaffolding:
- add `helengine.projectfile.csproj` targeting `net9.0`,
- add `helengine.projectfile.tests.csproj` targeting `net9.0` with xUnit packages,
- reference `..\helengine.projectfile\helengine.projectfile.csproj` from the tests project,
- add `..\..\engine\helengine.projectfile\helengine.projectfile.csproj` to `helengine.launcher.csproj`,
- add `..\helengine.projectfile\helengine.projectfile.csproj` to `helengine.editor.csproj`,
- add `..\helengine.projectfile\helengine.projectfile.csproj` to `helengine.editor.tests.csproj`,
- register both new projects in `helengine.ui/helengine.sln`.

- [ ] **Step 4: Re-run the smoke test**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj --filter "FullyQualifiedName~SharedProjectFileLibrary_ExposesCanonicalEntryPoints" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the project scaffolding**

```bash
rtk git add engine/helengine.projectfile engine/helengine.projectfile.tests helengine.ui/helengine.sln helengine.ui/helengine.launcher/helengine.launcher.csproj engine/helengine.editor/helengine.editor.csproj engine/helengine.editor.tests/helengine.editor.tests.csproj
rtk git commit -m "Add shared project file library scaffolding"
```

### Task 2: Define The Canonical `.heproj` Contract Types

**Files:**
- Create: `engine/helengine.projectfile/ProjectFileDocument.cs`
- Create: `engine/helengine.projectfile/ProjectFileReadErrorCode.cs`
- Create: `engine/helengine.projectfile/ProjectFileReadError.cs`
- Create: `engine/helengine.projectfile/ProjectFileReadResult.cs`
- Create: `engine/helengine.projectfile/ProjectFileJsonModel.cs`
- Create: `engine/helengine.projectfile.tests/ProjectFileDocumentTests.cs`

- [ ] **Step 1: Write the failing contract tests**

Create `ProjectFileDocumentTests.cs` with coverage for:
- required engine version being a first-class property,
- supported platforms being preserved in order,
- `ProjectFormatVersion` defaulting to the currently supported format version constant,
- `SupportedPlatforms` accepting unknown strings.

Suggested assertions:

```csharp
[Fact]
public void ProjectFileDocument_DefaultsToSupportedFormatVersion() {
    ProjectFileDocument document = new ProjectFileDocument();
    Assert.Equal(1, document.ProjectFormatVersion);
}

[Fact]
public void ProjectFileDocument_PreservesArbitrarySupportedPlatforms() {
    ProjectFileDocument document = new ProjectFileDocument {
        SupportedPlatforms = new List<string> { "windows", "future-console" }
    };

    Assert.Equal("future-console", document.SupportedPlatforms[1]);
}
```

- [ ] **Step 2: Run the contract tests and verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj --filter "FullyQualifiedName~ProjectFileDocumentTests" -v minimal
```

Expected: FAIL because the model types and defaults are not implemented yet.

- [ ] **Step 3: Implement the canonical model types**

Implement:
- `ProjectFileDocument` with `ProjectFormatVersion`, `Name`, `Version`, `RequiredEngineVersion`, `SupportedPlatforms`, `Created`, `LastOpened`, and `Description`,
- `ProjectFileReadErrorCode` enum values for `InvalidJson`, `MissingRequiredField`, `UnsupportedFormatVersion`, and `InvalidFieldValue`,
- `ProjectFileReadError` and `ProjectFileReadResult` for structured failure reporting,
- `ProjectFileJsonModel` as the internal JSON transport shape used by reader/writer code.

Implementation requirements:
- add XML comments to every class, property, and method,
- default `ProjectFormatVersion` to `1`,
- initialize `SupportedPlatforms` to an empty list rather than null.

- [ ] **Step 4: Re-run the contract tests**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj --filter "FullyQualifiedName~ProjectFileDocumentTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the contract types**

```bash
rtk git add engine/helengine.projectfile/ProjectFileDocument.cs engine/helengine.projectfile/ProjectFileReadErrorCode.cs engine/helengine.projectfile/ProjectFileReadError.cs engine/helengine.projectfile/ProjectFileReadResult.cs engine/helengine.projectfile/ProjectFileJsonModel.cs engine/helengine.projectfile.tests/ProjectFileDocumentTests.cs
rtk git commit -m "Define shared heproj contract types"
```

### Task 3: Implement Shared Read/Write Validation

**Files:**
- Create: `engine/helengine.projectfile/ProjectFileReader.cs`
- Create: `engine/helengine.projectfile/ProjectFileWriter.cs`
- Create: `engine/helengine.projectfile.tests/ProjectFileReaderTests.cs`
- Create: `engine/helengine.projectfile.tests/ProjectFileWriterTests.cs`

- [ ] **Step 1: Write the failing reader and writer tests**

Create `ProjectFileReaderTests.cs` with cases for:
- valid `.heproj` reads,
- invalid JSON,
- missing `requiredEngineVersion`,
- missing `supportedPlatforms`,
- unsupported newer `projectFormatVersion`,
- unknown extra JSON properties not failing the read,
- unknown platform ids surviving the read.

Create `ProjectFileWriterTests.cs` with cases for:
- writing stable camelCase JSON,
- round-tripping a valid document through writer then reader,
- preserving arbitrary supported-platform ids.

Suggested round-trip test:

```csharp
[Fact]
public async Task WriteAsync_WhenDocumentIsValid_RoundTripsThroughReader() {
    ProjectFileDocument document = new ProjectFileDocument {
        Name = "city",
        Version = "1.0.0",
        RequiredEngineVersion = "0.1.0",
        SupportedPlatforms = new List<string> { "windows", "browser-next" }
    };

    await Writer.WriteAsync(ProjectFilePath, document);
    ProjectFileReadResult result = await Reader.ReadAsync(ProjectFilePath);

    Assert.True(result.Success);
    Assert.Equal("browser-next", result.Document.SupportedPlatforms[1]);
}
```

- [ ] **Step 2: Run the focused shared-library tests and verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj --filter "FullyQualifiedName~ProjectFileReaderTests|FullyQualifiedName~ProjectFileWriterTests" -v minimal
```

Expected: FAIL because the reader and writer do not exist yet.

- [ ] **Step 3: Implement `ProjectFileReader` and `ProjectFileWriter`**

Implementation requirements:
- `ProjectFileReader.ReadAsync(string projectFilePath)` returns `ProjectFileReadResult`,
- it validates `.heproj` extension and file existence,
- it treats unsupported format versions as structured failures,
- it rejects missing required fields instead of fabricating defaults,
- it allows unknown extra properties and arbitrary supported-platform strings,
- `ProjectFileWriter.WriteAsync(string projectFilePath, ProjectFileDocument document)` emits stable indented camelCase JSON,
- use one shared `JsonSerializerOptions` definition inside the library rather than repeating serializer setup in consumers.

- [ ] **Step 4: Re-run the focused shared-library tests**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj --filter "FullyQualifiedName~ProjectFileReaderTests|FullyQualifiedName~ProjectFileWriterTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the read/write implementation**

```bash
rtk git add engine/helengine.projectfile/ProjectFileReader.cs engine/helengine.projectfile/ProjectFileWriter.cs engine/helengine.projectfile.tests/ProjectFileReaderTests.cs engine/helengine.projectfile.tests/ProjectFileWriterTests.cs
rtk git commit -m "Implement shared heproj reader and writer"
```

### Task 4: Refactor Launcher Models And Loader To Consume The Shared Contract

**Files:**
- Modify: `helengine.ui/helengine.launcher/Models/RecentProject.cs`
- Modify: `helengine.ui/helengine.launcher/Services/ProjectFileLoader.cs`
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Create: `helengine.ui/helengine.launcher.tests/HomeViewTests.cs`
- Modify: `helengine.ui/helengine.launcher.tests/ProjectFileLoaderTests.cs`
- Modify: `helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs`

- [ ] **Step 1: Write the failing launcher tests**

Add or update tests to cover:
- `RecentProject` capturing `RequiredEngineVersion`,
- `RecentProject` capturing `SupportedPlatforms`,
- `ProjectFileLoader` building a recent-project entry from `ProjectFileDocument`,
- `LauncherShell` surfacing shared-library load failures through the existing status path,
- `HomeView` rendering required engine version and supported platforms from the recent project metadata.

Suggested assertions:

```csharp
[Fact]
public async Task LoadAsync_WhenProjectFileIsValid_PopulatesEngineVersionAndPlatforms() {
    RecentProject project = await Loader.LoadAsync(ProjectFilePath);

    Assert.Equal("0.1.0", project.RequiredEngineVersion);
    Assert.Contains("linux", project.SupportedPlatforms);
}
```

- [ ] **Step 2: Run the focused launcher tests and verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~ProjectFileLoaderTests|FullyQualifiedName~LauncherShellProjectSelectionTests|FullyQualifiedName~HomeViewTests" -v minimal
```

Expected: FAIL because the launcher still owns ad hoc `.heproj` parsing and recent-project metadata is too small.

- [ ] **Step 3: Refactor launcher loading and display around the shared library**

Implementation requirements:
- extend `RecentProject` with `RequiredEngineVersion` and `SupportedPlatforms`,
- replace manual `JsonDocument` parsing inside `ProjectFileLoader` with `ProjectFileReader`,
- map structured load failures to the existing launcher error message path instead of swallowing them,
- keep the full `.heproj` path as the canonical `RecentProject.Path`,
- update `HomeView` so each card shows the required engine version and supported-platform list without moving layout responsibilities into `LauncherShell`.

- [ ] **Step 4: Re-run the focused launcher tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~ProjectFileLoaderTests|FullyQualifiedName~LauncherShellProjectSelectionTests|FullyQualifiedName~HomeViewTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the launcher shared-contract integration**

```bash
rtk git add helengine.ui/helengine.launcher/Models/RecentProject.cs helengine.ui/helengine.launcher/Services/ProjectFileLoader.cs helengine.ui/helengine.launcher/Views/LauncherShell.cs helengine.ui/helengine.launcher/Views/Pages/HomeView.cs helengine.ui/helengine.launcher.tests/ProjectFileLoaderTests.cs helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs helengine.ui/helengine.launcher.tests/HomeViewTests.cs
rtk git commit -m "Load launcher projects through shared heproj library"
```

### Task 5: Refactor Launcher Project Creation To Write Canonical `.heproj`

**Files:**
- Modify: `helengine.ui/helengine.launcher/Services/ProjectScaffolder.cs`
- Modify: `helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs`
- Create or Modify: `helengine.ui/helengine.launcher.tests/ProjectScaffolderTests.cs`

- [ ] **Step 1: Write the failing scaffolder tests**

Add coverage for:
- new projects writing `project.heproj` with `projectFormatVersion`,
- new projects writing `requiredEngineVersion` into `.heproj`,
- new projects writing `supportedPlatforms` into `.heproj`,
- `settings/project.json` no longer storing canonical shared metadata beyond local state needs.

Suggested assertions:

```csharp
[Fact]
public async Task CreateAsync_WritesCanonicalProjectFile() {
    ProjectCreateResult result = await Scaffolder.CreateAsync(BaseLocation, "city", EngineInstall);

    ProjectFileReadResult readResult = await Reader.ReadAsync(Path.Combine(result.ProjectPath, "project.heproj"));
    Assert.True(readResult.Success);
    Assert.Equal(EngineInstall.Version, readResult.Document.RequiredEngineVersion);
}
```

- [ ] **Step 2: Run the focused scaffolder tests and verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~ProjectScaffolderTests|FullyQualifiedName~LauncherShellProjectSelectionTests" -v minimal
```

Expected: FAIL because `ProjectScaffolder` still writes the old ad hoc `.heproj` and stores engine version only in `settings/project.json`.

- [ ] **Step 3: Refactor `ProjectScaffolder` to use `ProjectFileWriter`**

Implementation requirements:
- build a `ProjectFileDocument` for new projects,
- write `project.heproj` through `ProjectFileWriter`,
- keep `settings/project.json` only for local settings, such as active-platform defaults if the file already exists for that purpose,
- ensure `ProjectCreateResult.ProjectPath` still returns the project root directory so existing launcher navigation logic does not break.

- [ ] **Step 4: Re-run the focused scaffolder tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~ProjectScaffolderTests|FullyQualifiedName~LauncherShellProjectSelectionTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the launcher project-creation refactor**

```bash
rtk git add helengine.ui/helengine.launcher/Services/ProjectScaffolder.cs helengine.ui/helengine.launcher.tests/ProjectScaffolderTests.cs helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs
rtk git commit -m "Write canonical heproj files from launcher"
```

### Task 6: Refactor Editor Project Resolution To Consume The Shared Contract

**Files:**
- Modify: `helengine.ui/helengine.editor.app/Program.cs`
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorProjectFileResolutionTests.cs`

- [ ] **Step 1: Write the failing editor project-resolution tests**

Create `EditorProjectFileResolutionTests.cs` with coverage for:
- resolving a direct `.heproj` argument,
- resolving a directory argument to `project.heproj` when present,
- rejecting missing or invalid `.heproj` files,
- deriving the project root from the canonical file path without losing asset-path behavior.

Suggested assertions:

```csharp
[Fact]
public void ResolveProjectInput_WhenDirectoryContainsProjectFile_ReturnsCanonicalHeprojPath() {
    string resolvedPath = EditorProjectFileResolver.Resolve(ProjectRootPath);
    Assert.EndsWith("project.heproj", resolvedPath, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the focused editor tests and verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectFileResolutionTests" -v minimal
```

Expected: FAIL because the editor still accepts any existing file or directory without shared `.heproj` validation.

- [ ] **Step 3: Refactor editor project resolution around the shared library**

Implementation requirements:
- add a small resolver type if needed, but keep each class in its own file,
- `Program` should accept either a `.heproj` file path or a directory containing `project.heproj`,
- invalid targets should fail before the editor host initializes,
- `MainForm` should retain project-root derivation for assets but use the canonical project file path as its source of truth,
- `EditorSession` should use the canonical file path consistently for display/root derivation.

- [ ] **Step 4: Re-run the focused editor project-resolution tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectFileResolutionTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the editor project-resolution refactor**

```bash
rtk git add helengine.ui/helengine.editor.app/Program.cs helengine.ui/helengine.editor.app/MainForm.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorProjectFileResolutionTests.cs
rtk git commit -m "Resolve editor projects through shared heproj library"
```

### Task 7: Full Verification And Cleanup

**Files:**
- Modify: any touched files from Tasks 1-6 as needed after verification

- [ ] **Step 1: Run the shared-library test suite**

Run:

```bash
rtk dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj -v minimal
```

Expected: PASS.

- [ ] **Step 2: Run the launcher test suite**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal
```

Expected: PASS.

- [ ] **Step 3: Run the focused editor project-resolution tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectFileResolutionTests|FullyQualifiedName~EditorSessionSceneSaveTests" -v minimal
```

Expected: PASS.

- [ ] **Step 4: Build the launcher and editor projects**

Run:

```bash
rtk dotnet build helengine.ui/helengine.launcher/helengine.launcher.csproj -v minimal
rtk dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -v minimal
```

Expected: PASS with `0 errors`.

- [ ] **Step 5: Commit the final verification adjustments**

```bash
rtk git add engine/helengine.projectfile engine/helengine.projectfile.tests helengine.ui/helengine.launcher helengine.ui/helengine.launcher.tests helengine.ui/helengine.editor.app engine/helengine.editor helengine.ui/helengine.sln
rtk git commit -m "Share heproj format between launcher and editor"
```
