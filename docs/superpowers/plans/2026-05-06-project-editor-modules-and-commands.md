# Project Editor Modules And Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit editor-only project modules and project-authored editor commands, then use the first client-side command to regenerate `DemoDiscMainMenu.helen` with current `Menu*` component ids.

**Architecture:** Extend the existing `code.module.json` pipeline with an explicit module kind that defaults to runtime, enforce one-way dependencies from runtime to editor, and carry that kind through generated project metadata, runtime build filtering, and editor assembly loading. Discover project-authored editor commands from loaded editor assemblies through a constrained editor command context, then author the first `city` editor command that regenerates the demo-disc main menu scene through the existing menu scene build service.

**Tech Stack:** C#/.NET 9, xUnit, generated code-module solution/project generation, editor script hot reload, scene asset serialization, menu scene build services, client project code modules under `C:\dev\helprojs\city\assets\codebase`.

---

### Task 1: Add Explicit Module Kind And Dependency Validation

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorCodeModuleKind.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCodeModuleManifestDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs`

- [ ] **Step 1: Write the failing manifest-kind and dependency tests**

```csharp
[Fact]
public void Load_WhenModuleKindIsOmitted_DefaultsToRuntime() {
    WriteManifest(
        Path.Combine(TempProjectRootPath, "assets", "codebase", "tools", "code.module.json"),
        """
{
  "moduleId": "gameplay.tools",
  "dependencyModuleIds": [],
  "loadScopes": [ "always-loaded" ]
}
""");

    EditorCodeModuleManifestDocument document = new EditorCodeModuleManifestService(TempProjectRootPath).Load();

    EditorCodeModuleManifestEntry module = Assert.Single(document.Modules, entry => entry.ModuleId == "gameplay.tools");
    Assert.Equal(EditorCodeModuleKind.Runtime, module.ModuleKind);
}

[Fact]
public void Load_WhenRuntimeModuleDependsOnEditorModule_Throws() {
    WriteManifest(
        Path.Combine(TempProjectRootPath, "assets", "codebase", "editor.tools", "code.module.json"),
        """
{
  "moduleId": "editor.tools",
  "dependencyModuleIds": [ "gameplay" ],
  "loadScopes": [ "always-loaded" ],
  "moduleKind": "editor"
}
""");
    WriteManifest(
        Path.Combine(TempProjectRootPath, "assets", "codebase", "runtime.ui", "code.module.json"),
        """
{
  "moduleId": "runtime.ui",
  "dependencyModuleIds": [ "editor.tools" ],
  "loadScopes": [ "always-loaded" ],
  "moduleKind": "runtime"
}
""");

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
        () => new EditorCodeModuleManifestService(TempProjectRootPath).Load());

    Assert.Contains("runtime.ui", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("editor.tools", exception.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the manifest tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCodeModuleManifestServiceTests.Load_WhenModuleKindIsOmitted_DefaultsToRuntime|FullyQualifiedName~EditorCodeModuleManifestServiceTests.Load_WhenRuntimeModuleDependsOnEditorModule_Throws|FullyQualifiedName~EditorCodeModuleManifestServiceRootFallbackTests" -v minimal`

Expected: `FAIL` because `EditorCodeModuleKind` and `ModuleKind` do not exist yet, and manifest loading does not validate runtime-to-editor dependencies.

- [ ] **Step 3: Implement explicit module kind and validation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Declares whether one authored code module participates in runtime packaging or editor-only tooling.
    /// </summary>
    public enum EditorCodeModuleKind {
        Runtime = 0,
        Editor = 1
    }
}
```

```csharp
public sealed class EditorCodeModuleManifestEntry {
    [JsonConstructor]
    public EditorCodeModuleManifestEntry(
        string moduleId,
        string folderPath,
        string[] dependencyModuleIds,
        string[] loadScopes,
        string[] nestedModuleFolderPaths,
        EditorCodeModuleKind moduleKind) {
        ModuleKind = moduleKind;
    }

    public EditorCodeModuleKind ModuleKind { get; }
}
```

```csharp
sealed class EditorCodeModuleManifestFileRecord {
    public string ModuleKind { get; set; } = string.Empty;
}

static EditorCodeModuleKind ParseModuleKind(string moduleKindText, string manifestFilePath) {
    if (string.IsNullOrWhiteSpace(moduleKindText)) {
        return EditorCodeModuleKind.Runtime;
    }
    if (string.Equals(moduleKindText, "runtime", StringComparison.OrdinalIgnoreCase)) {
        return EditorCodeModuleKind.Runtime;
    }
    if (string.Equals(moduleKindText, "editor", StringComparison.OrdinalIgnoreCase)) {
        return EditorCodeModuleKind.Editor;
    }

    throw new InvalidOperationException($"Code module manifest '{manifestFilePath}' declared unsupported moduleKind '{moduleKindText}'.");
}

static void ValidateDependencyKinds(EditorCodeModuleManifestEntry[] modules) {
    Dictionary<string, EditorCodeModuleManifestEntry> modulesById = modules.ToDictionary(module => module.ModuleId, StringComparer.OrdinalIgnoreCase);
    for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++) {
        EditorCodeModuleManifestEntry module = modules[moduleIndex];
        if (module.ModuleKind != EditorCodeModuleKind.Runtime) {
            continue;
        }

        for (int dependencyIndex = 0; dependencyIndex < module.DependencyModuleIds.Length; dependencyIndex++) {
            string dependencyModuleId = module.DependencyModuleIds[dependencyIndex];
            if (!modulesById.TryGetValue(dependencyModuleId, out EditorCodeModuleManifestEntry dependencyModule)) {
                continue;
            }
            if (dependencyModule.ModuleKind == EditorCodeModuleKind.Editor) {
                throw new InvalidOperationException($"Runtime code module '{module.ModuleId}' cannot depend on editor code module '{dependencyModule.ModuleId}'.");
            }
        }
    }
}
```

- [ ] **Step 4: Run the manifest suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCodeModuleManifestServiceTests|FullyQualifiedName~EditorCodeModuleManifestServiceRootFallbackTests" -v minimal`

Expected: `PASS` with omitted `moduleKind` defaulting to runtime, explicit editor modules loading successfully, and runtime-to-editor edges rejected.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorCodeModuleKind.cs engine/helengine.editor/managers/project/EditorCodeModuleManifestDocument.cs engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs
rtk git commit -m "feat: add editor code module kinds"
```

### Task 2: Carry Module Kind Through Generated Projects And Runtime Build Filtering

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformCodeCookService.cs`
- Modify: `engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs`
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs`

- [ ] **Step 1: Write the failing generated-project and cook-filter tests**

```csharp
[Fact]
public void GenerateSolutionFiles_WhenEditorModuleExists_WritesEditorProjectWithEditorReference() {
    WriteManifest(
        Path.Combine(TempProjectRootPath, "assets", "codebase", "menu.tools", "code.module.json"),
        """
{
  "moduleId": "menu.tools",
  "dependencyModuleIds": [ "gameplay" ],
  "loadScopes": [ "always-loaded" ],
  "moduleKind": "editor"
}
""");
    File.WriteAllText(
        Path.Combine(TempProjectRootPath, "assets", "codebase", "menu.tools", "RegenerateCommand.cs"),
        "public sealed class RegenerateCommand { }");

    EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());

    service.GenerateSolutionFiles();

    string projectFilePath = Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "menu.tools", "menu.tools.csproj");
    string projectFileContents = File.ReadAllText(projectFilePath);
    Assert.Contains("helengine.editor.dll", projectFileContents, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void CompileModules_WhenEditorModulesExist_ExcludesThemFromRuntimeBuildOutputs() {
    EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestDocument([
        new EditorCodeModuleManifestEntry("gameplay", "assets", [], [ "always-loaded" ], [], EditorCodeModuleKind.Runtime),
        new EditorCodeModuleManifestEntry("menu.tools", "assets/codebase/menu.tools", [ "gameplay" ], [ "always-loaded" ], [], EditorCodeModuleKind.Editor)
    ]);

    PlatformBuildCodeModule[] compiledModules = CreateCodeCookService().CompileModules(
        manifestDocument,
        "windows",
        "native",
        "codegen.exe",
        CreateCppProfile(),
        [],
        new Dictionary<string, string>(),
        TempOutputRootPath);

    Assert.DoesNotContain(compiledModules, module => module.ModuleId == "menu.tools");
    Assert.Contains(compiledModules, module => module.ModuleId == "gameplay");
}
```

- [ ] **Step 2: Run the project-generation and code-cook tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameSolutionServiceTests.GenerateSolutionFiles_WhenEditorModuleExists_WritesEditorProjectWithEditorReference|FullyQualifiedName~EditorPlatformCodeCookServiceTests.CompileModules_WhenEditorModulesExist_ExcludesThemFromRuntimeBuildOutputs" -v minimal`

Expected: `FAIL` because generated module metadata does not track module kind, editor projects do not reference `helengine.editor`, and the code cook service compiles every discovered module.

- [ ] **Step 3: Implement module-kind propagation, references, and cook filtering**

```csharp
public sealed class EditorGeneratedCodeModuleProject {
    public EditorGeneratedCodeModuleProject(
        string moduleId,
        string sourceFolderPath,
        IReadOnlyList<string> nestedSourceFolderPaths,
        string projectFilePath,
        string baseIntermediateOutputPath,
        string baseOutputPath,
        string outputDirectoryPath,
        Guid projectGuid,
        EditorCodeModuleKind moduleKind) {
        ModuleKind = moduleKind;
    }

    public EditorCodeModuleKind ModuleKind { get; }
}
```

```csharp
moduleProjects.Add(new EditorGeneratedCodeModuleProject(
    module.ModuleId,
    module.FolderPath,
    module.NestedModuleFolderPaths,
    projectFilePath,
    baseIntermediateOutputPath,
    baseOutputPath,
    outputDirectoryPath,
    projectGuid,
    module.ModuleKind));
```

```csharp
builder.AppendLine("  <ItemGroup>");
builder.AppendLine("    <Reference Include=\"helengine.core\">");
builder.AppendLine("      <HintPath>" + EscapeXml(typeof(Component).Assembly.Location) + "</HintPath>");
builder.AppendLine("    </Reference>");
if (moduleProject.ModuleKind == EditorCodeModuleKind.Editor) {
    builder.AppendLine("    <Reference Include=\"helengine.editor\">");
    builder.AppendLine("      <HintPath>" + EscapeXml(typeof(EditorGameSolutionService).Assembly.Location) + "</HintPath>");
    builder.AppendLine("    </Reference>");
}
builder.AppendLine("  </ItemGroup>");
```

```csharp
public sealed class ScriptAssemblyDescriptor {
    public ScriptAssemblyDescriptor(string moduleId, string outputDirectoryPath, string assemblyPath, EditorCodeModuleKind moduleKind) {
        ModuleKind = moduleKind;
    }

    public EditorCodeModuleKind ModuleKind { get; }
}
```

```csharp
static EditorCodeModuleManifestEntry[] ResolveModulesToCompile(
    EditorCodeModuleManifestDocument manifestDocument,
    IReadOnlyList<string> selectedModuleIds) {
    EditorCodeModuleManifestEntry[] candidateModules = manifestDocument.Modules
        .Where(module => module.ModuleKind == EditorCodeModuleKind.Runtime)
        .ToArray();
    // Existing dependency ordering then runs on candidateModules only.
}
```

- [ ] **Step 4: Run the generated-project and code-cook suites**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameSolutionServiceTests|FullyQualifiedName~EditorPlatformCodeCookServiceTests" -v minimal`

Expected: `PASS` with editor projects generated into the solution, editor projects referencing `helengine.editor`, and runtime packaging filtering editor modules out.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs engine/helengine.editor/managers/project/EditorGameSolutionService.cs engine/helengine.editor/managers/project/EditorPlatformCodeCookService.cs engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs
rtk git commit -m "feat: propagate editor module kind through projects and builds"
```

### Task 3: Load Editor Assemblies And Discover Project Commands

**Files:**
- Create: `engine/helengine.editor/managers/project/IEditorCommand.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectCommandDescriptor.cs`
- Create: `engine/helengine.editor/managers/project/IEditorProjectCommandCatalogProvider.cs`
- Modify: `engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptHotReloadService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorProjectCommandCatalogTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorProjectCommandCatalogTests.cs`

- [ ] **Step 1: Write the failing editor-command discovery tests**

```csharp
[Fact]
public void Reload_WhenEditorAssemblyContainsEditorCommand_ExposesItThroughCatalog() {
    ScriptAssemblyDescriptor runtimeAssembly = CreateAssemblyDescriptor("gameplay", typeof(TestMenuDefinitionProvider).Assembly.Location, EditorCodeModuleKind.Runtime);
    ScriptAssemblyDescriptor editorAssembly = CreateAssemblyDescriptor("menu.tools", typeof(TestEditorCommand).Assembly.Location, EditorCodeModuleKind.Editor);
    EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(TempProjectRootPath);

    host.Reload([ runtimeAssembly, editorAssembly ]);

    EditorProjectCommandDescriptor command = Assert.Single(host.GetAvailableEditorCommands());
    Assert.Equal("menu.regenerate-demo-disc-main-menu", command.CommandId);
    Assert.Equal("Regenerate Demo Disc Main Menu", command.DisplayName);
}
```

```csharp
internal sealed class TestEditorCommand : IEditorCommand {
    public string CommandId => "menu.regenerate-demo-disc-main-menu";
    public string DisplayName => "Regenerate Demo Disc Main Menu";

    public void Execute(IEditorCommandContext context) {
    }
}
```

- [ ] **Step 2: Run the editor-command discovery tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameScriptAssemblyHostTests.Reload_WhenEditorAssemblyContainsEditorCommand_ExposesItThroughCatalog|FullyQualifiedName~EditorProjectCommandCatalogTests" -v minimal`

Expected: `FAIL` because there is no editor command contract, no command discovery API, and the assembly host only exposes script components.

- [ ] **Step 3: Implement command discovery on top of editor module assemblies**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Defines one project-authored editor command discovered from an editor module assembly.
    /// </summary>
    public interface IEditorCommand {
        string CommandId { get; }
        string DisplayName { get; }
        void Execute(IEditorCommandContext context);
    }
}
```

```csharp
public sealed class EditorProjectCommandDescriptor {
    public EditorProjectCommandDescriptor(string commandId, string displayName, Type commandType, string moduleId) {
        CommandId = commandId;
        DisplayName = displayName;
        CommandType = commandType;
        ModuleId = moduleId;
    }

    public string CommandId { get; }
    public string DisplayName { get; }
    public Type CommandType { get; }
    public string ModuleId { get; }
}
```

```csharp
public interface IEditorScriptAssemblyHost : IDisposable {
    IScriptTypeResolver ScriptTypeResolver { get; }
    void Reload(IReadOnlyList<ScriptAssemblyDescriptor> assemblies);
    IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity);
    IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands();
}
```

```csharp
public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
    List<EditorProjectCommandDescriptor> commands = [];
    foreach ((string moduleId, Assembly assembly) in CurrentAssembliesByModuleId) {
        if (!CurrentModuleKindsByModuleId.TryGetValue(moduleId, out EditorCodeModuleKind moduleKind) || moduleKind != EditorCodeModuleKind.Editor) {
            continue;
        }

        foreach (Type candidateType in assembly.GetTypes()) {
            if (candidateType.IsAbstract || !typeof(IEditorCommand).IsAssignableFrom(candidateType)) {
                continue;
            }

            IEditorCommand command = (IEditorCommand)Activator.CreateInstance(candidateType)!;
            commands.Add(new EditorProjectCommandDescriptor(command.CommandId, command.DisplayName, candidateType, moduleId));
        }
    }

    commands.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
    return commands;
}
```

- [ ] **Step 4: Run the assembly-host and command-catalog suites**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameScriptAssemblyHostTests|FullyQualifiedName~EditorProjectCommandCatalogTests" -v minimal`

Expected: `PASS` with editor assemblies loaded into the editor process, runtime assemblies still usable for providers/components, and editor commands discoverable only from editor modules.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/IEditorCommand.cs engine/helengine.editor/managers/project/EditorProjectCommandDescriptor.cs engine/helengine.editor/managers/project/IEditorProjectCommandCatalogProvider.cs engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs engine/helengine.editor/managers/project/EditorGameScriptHotReloadService.cs engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs engine/helengine.editor.tests/managers/project/EditorProjectCommandCatalogTests.cs
rtk git commit -m "feat: discover editor commands from editor modules"
```

### Task 4: Add Command Context And Engine-Side Demo Scene Regeneration Services

**Files:**
- Create: `engine/helengine.editor/managers/project/IEditorCommandContext.cs`
- Create: `engine/helengine.editor/managers/project/EditorCommandExecutionService.cs`
- Create: `engine/helengine.editor/managers/menu/EditorMenuSceneRegenerationService.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorCommandExecutionServiceTests.cs`
- Create: `engine/helengine.editor.tests/managers/menu/EditorMenuSceneRegenerationServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorCommandExecutionServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/menu/EditorMenuSceneRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing command-execution and scene-regeneration tests**

```csharp
[Fact]
public void Execute_WhenCommandUsesMenuSceneRegenerationService_RewritesSceneWithMenuTypeIds() {
    string projectRootPath = CreateProjectWithLegacyDemoDiscScene();
    EditorMenuSceneRegenerationService regenerationService = new EditorMenuSceneRegenerationService(projectRootPath, CreateScriptTypeResolver());

    regenerationService.Regenerate("Scenes/DemoDiscMainMenu.helen", "city.menu.DemoDiscMenuDefinitionProvider, gameplay");

    string scenePath = Path.Combine(projectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
    string sceneBytes = Convert.ToBase64String(File.ReadAllBytes(scenePath));
    Assert.DoesNotContain("helengine.DemoMenuBuildComponent", sceneBytes, StringComparison.Ordinal);
}
```

```csharp
[Fact]
public void Execute_WhenProjectCommandThrows_DoesNotRewriteTargetScene() {
    string scenePath = CreateLegacySceneFile();
    EditorCommandExecutionService executionService = CreateExecutionService(scenePath, new ThrowingEditorCommand());

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
        () => executionService.Execute("menu.throw"));

    Assert.Contains("menu.throw", exception.Message, StringComparison.OrdinalIgnoreCase);
    Assert.True(File.Exists(scenePath));
}
```

- [ ] **Step 2: Run the command-execution and regeneration tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCommandExecutionServiceTests|FullyQualifiedName~EditorMenuSceneRegenerationServiceTests" -v minimal`

Expected: `FAIL` because there is no editor command context/execution service and no engine-side service that regenerates the menu scene through normal serialization.

- [ ] **Step 3: Implement the command context and regeneration service**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Exposes the editor-safe capabilities available to project-authored editor commands.
    /// </summary>
    public interface IEditorCommandContext {
        string ProjectRootPath { get; }
        IScriptTypeResolver ScriptTypeResolver { get; }
        EditorMenuSceneRegenerationService MenuSceneRegenerationService { get; }
    }
}
```

```csharp
public sealed class EditorMenuSceneRegenerationService {
    readonly string ProjectRootPath;
    readonly DemoMenuSceneBuildService SceneBuildService;

    public EditorMenuSceneRegenerationService(string projectRootPath, IScriptTypeResolver scriptTypeResolver) {
        ProjectRootPath = Path.GetFullPath(projectRootPath);
        SceneBuildService = new DemoMenuSceneBuildService(scriptTypeResolver);
    }

    public void Regenerate(string sceneId, string providerTypeName) {
        SceneAsset sceneAsset = SceneBuildService.BuildSceneAsset(sceneId, providerTypeName);
        string fullScenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        string tempScenePath = fullScenePath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(fullScenePath)!);
        using (FileStream stream = new FileStream(tempScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        File.Move(tempScenePath, fullScenePath, true);
    }
}
```

```csharp
public sealed class EditorCommandExecutionService {
    public void Execute(string commandId) {
        EditorProjectCommandDescriptor descriptor = ResolveRequiredCommand(commandId);
        IEditorCommand command = (IEditorCommand)Activator.CreateInstance(descriptor.CommandType)!;
        try {
            command.Execute(Context);
        } catch (Exception exception) {
            throw new InvalidOperationException($"Editor command '{commandId}' failed: {exception.Message}", exception);
        }
    }
}
```

- [ ] **Step 4: Run the command execution and regeneration suites**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCommandExecutionServiceTests|FullyQualifiedName~EditorMenuSceneRegenerationServiceTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: `PASS` with deterministic command execution, atomic scene rewriting, and regenerated menu scenes containing `helengine.Menu*` ids.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/IEditorCommandContext.cs engine/helengine.editor/managers/project/EditorCommandExecutionService.cs engine/helengine.editor/managers/menu/EditorMenuSceneRegenerationService.cs engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/project/EditorCommandExecutionServiceTests.cs engine/helengine.editor.tests/managers/menu/EditorMenuSceneRegenerationServiceTests.cs
rtk git commit -m "feat: add editor command execution and menu regeneration"
```

### Task 5: Author The First Client-Side Editor Module And Regenerate The City Scene

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\menu.tools\code.module.json`
- Create: `C:\dev\helprojs\city\assets\codebase\menu.tools\RegenerateDemoDiscMainMenuCommand.cs`
- Modify: `C:\dev\helprojs\city\assets\scenes\DemoDiscMainMenu.helen`
- Test: `engine/helengine.editor.tests/managers/menu/EditorMenuSceneRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing end-to-end city command regression**

```csharp
[Fact]
public void Execute_WhenCityRegenerateDemoDiscCommandRuns_RewritesLegacySceneIds() {
    string projectRootPath = CopyCityProjectFixtureWithLegacyDemoDiscScene();
    EditorBuildExecutionResult buildResult = BuildAndReloadProjectScripts(projectRootPath);
    Assert.True(buildResult.Succeeded, buildResult.Message);

    EditorCommandExecutionService executionService = CreateExecutionServiceFromProject(projectRootPath);

    executionService.Execute("menu.regenerate-demo-disc-main-menu");

    string scenePath = Path.Combine(projectRootPath, "assets", "scenes", "DemoDiscMainMenu.helen");
    byte[] sceneBytes = File.ReadAllBytes(scenePath);
    Assert.DoesNotContain("helengine.DemoMenuBuildComponent", Encoding.UTF8.GetString(sceneBytes), StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the city-command regression to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorMenuSceneRegenerationServiceTests.Execute_WhenCityRegenerateDemoDiscCommandRuns_RewritesLegacySceneIds" -v minimal`

Expected: `FAIL` because the client project does not yet contain an editor module manifest or the concrete regenerate command.

- [ ] **Step 3: Add the first client-side editor module and command**

```json
{
  "moduleId": "menu.tools",
  "dependencyModuleIds": [ "gameplay" ],
  "loadScopes": [ "always-loaded" ],
  "moduleKind": "editor"
}
```

```csharp
namespace city.menu.tools {
    /// <summary>
    /// Regenerates the baked demo-disc main menu scene using the current menu serialization pipeline.
    /// </summary>
    public sealed class RegenerateDemoDiscMainMenuCommand : IEditorCommand {
        public string CommandId => "menu.regenerate-demo-disc-main-menu";
        public string DisplayName => "Regenerate Demo Disc Main Menu";

        public void Execute(IEditorCommandContext context) {
            context.MenuSceneRegenerationService.Regenerate(
                "Scenes/DemoDiscMainMenu.helen",
                "city.menu.DemoDiscMenuDefinitionProvider, gameplay");
        }
    }
}
```

- [ ] **Step 4: Build scripts, execute the command, and verify the scene rewrite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorMenuSceneRegenerationServiceTests|FullyQualifiedName~EditorGameScriptAssemblyHostTests|FullyQualifiedName~EditorGameSolutionServiceTests" -v minimal`

Expected: `PASS` with the client editor module compiled, the command discovered by the editor command catalog, and `DemoDiscMainMenu.helen` rewritten without `helengine.DemoMenu*` ids.

Run: `rtk rg -a -n "helengine\\.DemoMenuBuildComponent|helengine\\.DemoMenuPanelComponent|helengine\\.DemoMenuItemComponent|helengine\\.DemoMenuSelectedDescriptionComponent" C:\dev\helprojs\city\assets\scenes\DemoDiscMainMenu.helen`

Expected: no matches.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor.tests/managers/menu/EditorMenuSceneRegenerationServiceTests.cs
rtk git add C:\dev\helprojs\city\assets\codebase\menu.tools\code.module.json C:\dev\helprojs\city\assets\codebase\menu.tools\RegenerateDemoDiscMainMenuCommand.cs C:\dev\helprojs\city\assets\scenes\DemoDiscMainMenu.helen
rtk git commit -m "feat: add project-authored demo scene regeneration command"
```
