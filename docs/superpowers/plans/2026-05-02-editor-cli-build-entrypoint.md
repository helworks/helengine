# Editor CLI Build Entrypoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let `helengine.editor.app` run a headless build from the shell with `--project`, `--build`, and `--output`, while reusing the same saved editor settings and platform build pipeline as the GUI.

**Architecture:** Keep the WinForms host as the executable entrypoint, but teach it to detect a CLI build command before UI startup. Put parsing and build orchestration into reusable editor-library services so tests can run without a window. Extract the shared project/bootstrap logic out of `EditorSession` so both GUI and CLI resolve project metadata, platform metadata, and build executors the same way, and normalize the incoming `--project` path through the same canonical `.heproj` resolver the GUI already uses.

**Tech Stack:** .NET 9, WinForms host app, existing `helengine.editor` services, `helengine.platforms`, `helengine.projectfile`, and xUnit.

---

### Task 1: Add CLI command parsing for `--project`, `--build`, and `--output`

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorCliBuildCommand.cs`
- Create: `engine/helengine.editor/managers/project/EditorCliArgumentParser.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorCliArgumentParserTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void ParseBuildCommand_parses_project_build_and_output() {
    EditorCliBuildCommand command = EditorCliArgumentParser.Parse(new[] {
        "--project", @"C:\dev\helprojs\city",
        "--build", "windows",
        "--output", @"C:\dev\helprojs\city\windows-build"
    });

    Assert.Equal(@"C:\dev\helprojs\city", command.ProjectPath);
    Assert.Equal("windows", command.BuildPlatformId);
    Assert.Equal(@"C:\dev\helprojs\city\windows-build", command.OutputPath);
}

[Fact]
public void ParseBuildCommand_rejects_missing_output() {
    var ex = Assert.Throws<InvalidOperationException>(() =>
        EditorCliArgumentParser.Parse(new[] {
            "--project", @"C:\dev\helprojs\city",
            "--build", "windows"
        }));

    Assert.Contains("--output", ex.Message);
}

[Fact]
public void ParseBuildCommand_normalizes_relative_project_and_output_paths() {
    string previousDirectory = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(@"C:\dev\helworks\helengine");
    try {
        EditorCliBuildCommand command = EditorCliArgumentParser.Parse(new[] {
            "--project", @".\samples\city",
            "--build", "windows",
            "--output", @".\out\windows"
        });

        Assert.Equal(Path.GetFullPath(@".\samples\city"), command.ProjectPath);
        Assert.Equal(Path.GetFullPath(@".\out\windows"), command.OutputPath);
    } finally {
        Directory.SetCurrentDirectory(previousDirectory);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCliArgumentParserTests" -v minimal
```

Expected: fail because `EditorCliBuildCommand` and `EditorCliArgumentParser` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Implement:

- `EditorCliBuildCommand` as a small immutable value object with `ProjectPath`, `BuildPlatformId`, and `OutputPath`
- `EditorCliArgumentParser.Parse(string[] args)` that requires:
  - `--project <path>`
  - `--build <platform-id>`
  - `--output <path>`
- `EditorCliArgumentParser.TryParse(string[] args, out EditorCliBuildCommand command)` so the app host can decide whether to enter CLI mode or GUI mode
- `ProjectFilePathResolver`-backed canonicalization of the CLI project argument so the build runner can accept a project root directory or a direct `.heproj` file path and normalize it to the canonical project file

Parsing rules:

- accept both absolute and relative paths
- normalize paths with `Path.GetFullPath`
- reject missing values with clear `InvalidOperationException` messages
- treat the first non-switch GUI argument as the current project path only when `--build` is absent

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCliArgumentParserTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorCliBuildCommand.cs engine/helengine.editor/managers/project/EditorCliArgumentParser.cs engine/helengine.editor.tests/managers/project/EditorCliArgumentParserTests.cs
git commit -m "feat: add editor cli argument parsing"
```

### Task 2: Extract shared project/build bootstrap from `EditorSession`

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorProjectBuildContext.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectBuildContextFactory.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorProjectBuildContextFactoryTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorBuildConfigServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void TryLoadExisting_returns_null_when_build_config_is_missing() {
    string projectRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(projectRoot);

    EditorBuildConfigService service = new(projectRoot);
    EditorBuildConfigDocument? document = service.TryLoadExisting();

    Assert.Null(document);
}

[Fact]
public void CreateContext_loads_project_metadata_and_supported_platforms() {
    string projectRoot = CreateTempProjectWithProjectFile(
        projectName: "City",
        projectVersion: "1.0.0",
        requiredEngineVersion: "1.0.0+13db86b8a91031015e3d0475799b6e6b1a56b309",
        supportedPlatforms: new[] { "windows", "ps2" });

    EditorProjectBuildContextFactory factory = new();
    EditorProjectBuildContext context = factory.Create(projectRoot, Array.Empty<IAssetImporterRegistration>());

    Assert.Equal("City", context.ProjectName);
    Assert.Equal("1.0.0", context.ProjectVersion);
    Assert.Contains("windows", context.SupportedPlatforms);
    Assert.Contains("ps2", context.SupportedPlatforms);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectBuildContextFactoryTests|FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal
```

Expected: fail because the new context factory and existing-config load method do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Implement:

- `EditorProjectBuildContext` as a small immutable holder for the resolved project metadata used by builds
- `EditorProjectBuildContextFactory.Create(string projectPath, IReadOnlyList<IAssetImporterRegistration> importers)` to move the existing `EditorSession` project loading logic into one reusable place and normalize `projectPath` through `ProjectFilePathResolver`
- `EditorBuildConfigService.TryLoadExisting()` to read `user_settings/build_config.json` without seeding defaults or creating missing platform entries
- update `EditorSession` so it gets its project metadata and build-executor routing from the shared context factory instead of carrying duplicate bootstrap logic

The shared context should expose the values the build pipeline already needs:

- canonical project file path
- project root path
- required engine version
- project name
- project version
- supported platforms
- importers
- platform provider resolver or catalog service creation helpers

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectBuildContextFactoryTests|FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorProjectBuildContext.cs engine/helengine.editor/managers/project/EditorProjectBuildContextFactory.cs engine/helengine.editor/managers/project/EditorBuildConfigService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/project/EditorProjectBuildContextFactoryTests.cs engine/helengine.editor.tests/managers/project/EditorBuildConfigServiceTests.cs
git commit -m "refactor: extract shared editor build bootstrap"
```

### Task 3: Add a headless build runner that reuses the saved editor build settings

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorCliBuildRunner.cs`
- Create: `engine/helengine.editor/managers/project/EditorCliBuildResult.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorCliBuildRunnerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void RunBuild_uses_saved_platform_settings_and_overrides_the_output_path() {
    string projectRoot = CreateTempProjectRoot();
    SeedProjectFile(projectRoot, projectName: "City", projectVersion: "1.0.0", requiredEngineVersion: "1.0.0+13db86b8a91031015e3d0475799b6e6b1a56b309", supportedPlatforms: new[] { "windows" });
    SeedExistingBuildConfig(projectRoot, platformId: "windows", outputPath: @"C:\old-output", selectedSceneIds: new[] { "scenes/startup.helen" }, buildProfileId: "debug", graphicsProfileId: "directx11", codegenProfileId: "default");

    RecordingEditorBuildExecutor executor = new();
    EditorCliBuildRunner runner = new(projectRoot, executor);

    EditorCliBuildResult result = runner.Run(new EditorCliBuildCommand(projectRoot, "windows", @"C:\new-output"));

    Assert.True(result.Succeeded);
    Assert.Equal(@"C:\new-output", executor.LastQueueItem.OutputDirectoryPath);
    Assert.Equal("windows", executor.LastQueueItem.PlatformId);
    Assert.Equal("debug", executor.LastQueueItem.SelectedBuildProfileId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCliBuildRunnerTests" -v minimal
```

Expected: fail because the new runner does not exist yet.

- [ ] **Step 3: Write minimal implementation**

Implement `EditorCliBuildRunner` so it:

- loads the project context through `EditorProjectBuildContextFactory`
- loads the existing `user_settings/build_config.json`
- finds the saved platform tab for the requested build platform
- fails cleanly if the platform tab does not exist
- creates a transient `EditorBuildQueueItemDocument` from the saved platform tab settings
- overrides only `OutputDirectoryPath` with the CLI `--output` path
- routes the queued item through the existing build executor for that platform
- returns a process-friendly result object containing success/failure and a message

Keep the GUI and CLI paths aligned by reusing the same executor-routing logic rather than inventing a separate build system.

Add a tiny test double:

```csharp
sealed class RecordingEditorBuildExecutor : IEditorBuildExecutor {
    public EditorBuildQueueItemDocument LastQueueItem { get; private set; }

    public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
        LastQueueItem = queueItem;
        return EditorBuildExecutionResult.Success("ok");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCliBuildRunnerTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorCliBuildRunner.cs engine/helengine.editor/managers/project/EditorCliBuildResult.cs engine/helengine.editor.tests/managers/project/EditorCliBuildRunnerTests.cs
git commit -m "feat: add headless editor build runner"
```

### Task 4: Wire the WinForms app entrypoint to dispatch CLI builds before GUI startup

**Files:**
- Modify: `helengine.ui/helengine.editor.app/Program.cs`
- Modify: `helengine.ui/helengine.editor.app/Properties/launchSettings.json`
- Test: `engine/helengine.editor.tests/managers/project/EditorCliBuildRunnerTests.cs` (reuse the runner test as the coverage for the headless path)

- [ ] **Step 1: Write the failing test**

Add one smoke-style assertion to the runner test file that mirrors the actual command-line shape:

```csharp
[Fact]
public void CommandLine_shape_matches_project_build_output_contract() {
    EditorCliBuildCommand command = EditorCliArgumentParser.Parse(new[] {
        "--project", @"C:\dev\helprojs\city",
        "--build", "windows",
        "--output", @"C:\dev\helprojs\city\windows-build"
    });

    Assert.Equal(@"C:\dev\helprojs\city", command.ProjectPath);
    Assert.Equal("windows", command.BuildPlatformId);
    Assert.Equal(@"C:\dev\helprojs\city\windows-build", command.OutputPath);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCliBuildRunnerTests" -v minimal
```

Expected: fail until `Program.cs` delegates into the new CLI path and the command parser/runners are wired together.

- [ ] **Step 3: Write minimal implementation**

Update `Program.Main(string[] args)` so it:

- checks for the new CLI build command first
- runs the headless build runner and exits with its code when `--build` is present
- falls back to the existing GUI project-path flow when `--build` is absent
- keeps the current GUI startup path unchanged for normal editor launches

Also add a launch profile for local developer smoke tests, for example:

```json
{
  "profiles": {
    "helengine.editor.app": {
      "commandName": "Project",
      "commandLineArgs": "\"C:\\dev\\helprojs\\city\\project.heproj\""
    },
    "cli-build-windows": {
      "commandName": "Project",
      "commandLineArgs": "--project \"C:\\dev\\helprojs\\city\" --build windows --output \"C:\\dev\\helprojs\\city\\windows-build\""
    }
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCliBuildRunnerTests" -v minimal
```

Expected: pass.

- [ ] **Step 5: Smoke test the CLI build path**

Run:

```bash
dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project "C:\dev\helprojs\city" --build windows --output "C:\dev\helprojs\city\windows-build"
```

Expected:

- no WinForms window opens
- the process exits with code 0 on success
- the output path contains the built player and staged assets

- [ ] **Step 6: Commit**

```bash
git add helengine.ui/helengine.editor.app/Program.cs helengine.ui/helengine.editor.app/Properties/launchSettings.json engine/helengine.editor/managers/project/EditorCliBuildRunner.cs engine/helengine.editor/managers/project/EditorCliBuildResult.cs engine/helengine.editor/managers/project/EditorCliBuildCommand.cs engine/helengine.editor/managers/project/EditorCliArgumentParser.cs engine/helengine.editor/managers/project/EditorProjectBuildContext.cs engine/helengine.editor/managers/project/EditorProjectBuildContextFactory.cs engine/helengine.editor/managers/project/EditorBuildConfigService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/project/EditorCliArgumentParserTests.cs engine/helengine.editor.tests/managers/project/EditorProjectBuildContextFactoryTests.cs engine/helengine.editor.tests/managers/project/EditorBuildConfigServiceTests.cs engine/helengine.editor.tests/managers/project/EditorCliBuildRunnerTests.cs
git commit -m "feat: add editor cli build entrypoint"
```
