# Demo Disc Menu, Dynamic Code Modules, And Automated Script Serialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current one-assembly script assumptions with per-module dynamic DLL loading, automated scripted-component serialization fallback, and a demo-disc menu scene that persists full CLR provider types against the correct user module.

**Architecture:** Start by moving generated script projects outside `assets` and making module id equal CLR assembly name. Then add a shared dynamic user-type resolver and module runtime that both menu-provider resolution and scripted-component systems use. After that, add automated editor serialization plus compact generated player deserializers, and only then rewire the city demo-disc menu generator onto the new module/type system.

**Tech Stack:** C# / .NET 9, HelEngine editor/runtime libraries, existing code-module manifest discovery, collectible `AssemblyLoadContext`, scene persistence/runtime loading, xUnit, generated city assets under `C:\dev\helprojs\city`

---

## File Structure

### Script project generation

- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
  - Replace single-project generation with per-module solution generation outside `assets`.
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs`
  - Immutable model for one generated module project.
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs`
  - Immutable model for the full generated solution and output layout.
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
  - Converts `EditorCodeModuleManifestDocument` into generated module project definitions.
- Modify: `engine/helengine.editor/EditorProjectPaths.cs`
  - Add generated code workspace paths outside `assets`.
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
  - Replace one-project assertions with per-module output assertions.
- Modify: `engine/helengine.editor.tests/EditorProjectPathsTests.cs`
  - Cover the new generated code workspace paths.

### Dynamic module runtime and type resolution

- Create: `engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs`
  - Runtime record for one loaded user assembly and its module id.
- Create: `engine/helengine.core/scripting/IScriptTypeResolver.cs`
  - Shared type-resolution contract for user modules.
- Create: `engine/helengine.core/scripting/ScriptTypeResolver.cs`
  - Core resolver that matches full CLR type identity against loaded script assemblies.
- Modify: `engine/helengine.core/menu/MenuDefinitionProviderResolver.cs`
  - Resolve through the shared script type resolver before falling back to runtime assemblies.
- Modify: `engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs`
  - Expose loaded script assemblies or a resolver-backed view of them.
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs`
  - Load one DLL per module and register them with the shared resolver.
- Create: `engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs`
  - Update to validate dynamic resolution through loaded module assemblies.
- Create: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`
  - Cover multi-module load and reload behavior.

### Automated editor serialization and generated player deserializers

- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs`
  - Deterministic reflected schema for scripted components.
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs`
  - Builds reflected schema from scripted component types.
- Create: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
  - Editor fallback serializer that records member names and values.
- Create: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
  - Runtime endpoint for generated player deserializers.
- Create: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
  - Emits compact ordinal-based player deserializer code from reflected schema.
- Modify: `engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs`
  - Route unknown scripted components through the automatic fallback path.
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
  - Register the generated scripted-component runtime deserializer path.
- Create: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
  - Cover warning emission, round-trip, and unsupported-member failures.
- Create: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
  - Cover generated ordinal deserializer output.

### Demo-disc menu integration

- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
  - Resolve provider module ownership from authored folder boundaries instead of project-name guessing.
- Modify: `tools/demo-disc-scene-writer/Program.cs`
  - Accept an optional authored menu-code folder or keep one explicit default.
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
  - Validate root-module and manifest-owned provider assembly names.
- Modify: `engine/helengine.core/components/2d/menu/MenuHostComponent.cs`
  - Use the shared script type resolver path for providers and keep existing menu behavior intact.
- Verify: `C:\dev\helprojs\city\assets\Scenes\DemoDiscMainMenu.helen`
  - Regenerated scene should store the correct full CLR provider type.

## Task 1: Add Generated Script Workspace Paths Outside `assets`

**Files:**
- Modify: `engine/helengine.editor/EditorProjectPaths.cs`
- Modify: `engine/helengine.editor.tests/EditorProjectPathsTests.cs`

- [ ] **Step 1: Write the failing path tests**

```csharp
[Fact]
public void Initialize_WhenProjectRootIsProvided_ResolvesGeneratedCodeWorkspaceOutsideAssets() {
    string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-path-tests", Guid.NewGuid().ToString("N"));

    EditorProjectPaths.Initialize(projectRootPath);

    Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "generated_code"), EditorProjectPaths.GeneratedCodeRoot);
    Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "generated_code", "projects"), EditorProjectPaths.GeneratedCodeProjectsRoot);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProjectPathsTests -v minimal`

Expected: `FAIL` because the generated code workspace paths do not exist on `EditorProjectPaths` yet.

- [ ] **Step 3: Add generated code workspace paths**

```csharp
public static string GeneratedCodeRoot { get; private set; }

public static string GeneratedCodeProjectsRoot { get; private set; }

public static void Initialize(string projectRootPath) {
    ProjectRootPath = Path.GetFullPath(projectRootPath);
    AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
    CacheRootPath = Path.Combine(ProjectRootPath, "cache");
    ShaderCachePath = Path.Combine(CacheRootPath, "shader-cache");
    GeneratedCodeRoot = Path.Combine(ProjectRootPath, "user_settings", "generated_code");
    GeneratedCodeProjectsRoot = Path.Combine(GeneratedCodeRoot, "projects");
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProjectPathsTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorProjectPaths.cs engine/helengine.editor.tests/EditorProjectPathsTests.cs
rtk git commit -m "feat: add generated code workspace paths"
```

## Task 2: Replace Single Script Project Generation With Per-Module Generation

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs`

- [ ] **Step 1: Write the failing multi-module solution test**

```csharp
[Fact]
public void GenerateSolutionFiles_WhenModulesExist_WritesOneProjectPerModuleOutsideAssets() {
    Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay"));
    Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "ui"));
    File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "code.module.json"), """
    { "moduleId": "gameplay", "dependencyModuleIds": [], "loadScopes": [ "always-loaded" ] }
    """);
    File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "gameplay", "ui", "code.module.json"), """
    { "moduleId": "gameplay.ui", "dependencyModuleIds": [ "gameplay" ], "loadScopes": [ "scene-loaded" ] }
    """);

    EditorGameSolutionService service = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());

    string solutionPath = service.GenerateSolutionFiles();

    Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj")));
    Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay.ui", "gameplay.ui.csproj")));
    Assert.DoesNotContain("assets/SkyRider.csproj", File.ReadAllText(solutionPath), StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGameSolutionServiceTests -v minimal`

Expected: `FAIL` because the service still writes one project under `assets`.

- [ ] **Step 3: Implement per-module generated solution planning**

```csharp
public sealed class EditorGeneratedCodeModuleProject {
    public EditorGeneratedCodeModuleProject(string moduleId, string sourceFolderPath, string projectFilePath, string outputDirectoryPath, IReadOnlyList<string> nestedSourceFolderPaths) {
        ModuleId = moduleId;
        SourceFolderPath = sourceFolderPath;
        ProjectFilePath = projectFilePath;
        OutputDirectoryPath = outputDirectoryPath;
        NestedSourceFolderPaths = nestedSourceFolderPaths;
    }

    public string ModuleId { get; }

    public string SourceFolderPath { get; }

    public string ProjectFilePath { get; }

    public string OutputDirectoryPath { get; }

    public IReadOnlyList<string> NestedSourceFolderPaths { get; }
}
```

```csharp
public sealed class EditorGeneratedCodeSolutionBuilder {
    public EditorGeneratedCodeSolution Build(string projectRootPath, EditorCodeModuleManifestDocument manifestDocument) {
        List<EditorGeneratedCodeModuleProject> projects = [];
        foreach (EditorCodeModuleManifestEntry module in manifestDocument.Modules) {
            string projectFilePath = Path.Combine(EditorProjectPaths.GeneratedCodeProjectsRoot, module.ModuleId, module.ModuleId + ".csproj");
            string outputDirectoryPath = Path.Combine(EditorProjectPaths.GeneratedCodeRoot, "bin", module.ModuleId, "Debug", "net9.0");
            projects.Add(new EditorGeneratedCodeModuleProject(module.ModuleId, module.FolderPath, projectFilePath, outputDirectoryPath, module.NestedModuleFolderPaths));
        }

        return new EditorGeneratedCodeSolution(projects);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameSolutionServiceTests|FullyQualifiedName~EditorCodeModuleManifestServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs engine/helengine.editor/managers/project/EditorGameSolutionService.cs engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs
rtk git commit -m "feat: generate one script project per code module"
```

## Task 3: Load One Dynamic Assembly Per Module And Share Type Resolution

**Files:**
- Create: `engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs`
- Create: `engine/helengine.core/scripting/IScriptTypeResolver.cs`
- Create: `engine/helengine.core/scripting/ScriptTypeResolver.cs`
- Modify: `engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`

- [ ] **Step 1: Write the failing multi-module load test**

```csharp
[Fact]
public void Reload_WhenTwoModuleAssembliesExist_RegistersBothAssembliesByModuleId() {
    EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);

    host.Reload(OutputRootPath, new[] {
        Path.Combine(OutputRootPath, "gameplay", "Debug", "net9.0", "gameplay.dll"),
        Path.Combine(OutputRootPath, "gameplay.ui", "Debug", "net9.0", "gameplay.ui.dll")
    });

    Assert.Equal("gameplay", host.ScriptTypeResolver.ResolveAssemblyName("city.PlayerComponent, gameplay").GetName().Name);
    Assert.Equal("gameplay.ui", host.ScriptTypeResolver.ResolveAssemblyName("city.ui.InventoryPanelComponent, gameplay.ui").GetName().Name);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGameScriptAssemblyHostTests -v minimal`

Expected: `FAIL` because the host only reloads one assembly path today.

- [ ] **Step 3: Add shared script type resolution and multi-module reload**

```csharp
public interface IScriptTypeResolver {
    Type Resolve(string assemblyQualifiedTypeName);
}
```

```csharp
public sealed class ScriptTypeResolver : IScriptTypeResolver {
    readonly Dictionary<string, Assembly> AssembliesByName = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string moduleId, Assembly assembly) {
        AssembliesByName[moduleId] = assembly;
    }

    public Type Resolve(string assemblyQualifiedTypeName) {
        string[] parts = assemblyQualifiedTypeName.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) {
            throw new InvalidOperationException($"Type '{assemblyQualifiedTypeName}' is not assembly-qualified.");
        }
        if (!AssembliesByName.TryGetValue(parts[1], out Assembly assembly)) {
            throw new InvalidOperationException($"Script assembly '{parts[1]}' is not loaded for type '{assemblyQualifiedTypeName}'.");
        }

        return assembly.GetType(parts[0], throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException($"Type '{parts[0]}' was not found in loaded script assembly '{parts[1]}'.");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGameScriptAssemblyHostTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs engine/helengine.core/scripting/IScriptTypeResolver.cs engine/helengine.core/scripting/ScriptTypeResolver.cs engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs
rtk git commit -m "feat: add dynamic module type resolution"
```

## Task 4: Route Menu Provider Resolution Through The Shared Script Type Resolver

**Files:**
- Modify: `engine/helengine.core/menu/MenuDefinitionProviderResolver.cs`
- Modify: `engine/helengine.core/components/2d/menu/MenuHostComponent.cs`
- Modify: `engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs`

- [ ] **Step 1: Write the failing resolver test for loaded script assemblies**

```csharp
[Fact]
public void Resolve_WhenProviderTypeLivesInLoadedScriptModule_ReturnsProviderInstance() {
    ScriptTypeResolver scriptTypeResolver = new ScriptTypeResolver();
    scriptTypeResolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
    MenuDefinitionProviderResolver resolver = new MenuDefinitionProviderResolver(scriptTypeResolver);

    IMenuDefinitionProvider provider = resolver.Resolve(typeof(TestMenuDefinitionProvider).AssemblyQualifiedName);

    Assert.IsType<TestMenuDefinitionProvider>(provider);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~MenuDefinitionProviderResolverTests -v minimal`

Expected: `FAIL` because the resolver is still defaulting to direct `Type.GetType(...)`.

- [ ] **Step 3: Update menu provider resolution**

```csharp
public sealed class MenuDefinitionProviderResolver {
    readonly IScriptTypeResolver ScriptTypeResolver;

    public MenuDefinitionProviderResolver(IScriptTypeResolver scriptTypeResolver = null) {
        ScriptTypeResolver = scriptTypeResolver;
    }

    public IMenuDefinitionProvider Resolve(string providerTypeName) {
        Type providerType = ScriptTypeResolver != null
            ? ScriptTypeResolver.Resolve(providerTypeName)
            : Type.GetType(providerTypeName, false);
        if (providerType == null) {
            throw new InvalidOperationException($"Menu provider type '{providerTypeName}' could not be resolved.");
        }

        return (IMenuDefinitionProvider)Activator.CreateInstance(providerType);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~MenuDefinitionProviderResolverTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core/menu/MenuDefinitionProviderResolver.cs engine/helengine.core/components/2d/menu/MenuHostComponent.cs engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs
rtk git commit -m "feat: resolve menu providers through dynamic script assemblies"
```

## Task 5: Add Automated Editor Serialization Fallback For Scripted Components

**Files:**
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs`
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs`
- Create: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`

- [ ] **Step 1: Write the failing automated-serialization test**

```csharp
[Fact]
public void SerializeComponent_WhenScriptComponentHasNoExplicitDescriptor_UsesAutomaticFallbackAndLogsWarning() {
    TestLogger logger = new TestLogger();
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry(logger);
    TestScriptSerializableComponent component = new TestScriptSerializableComponent {
        DisplayName = "Menu Row",
        Visible = true
    };

    SceneComponentAssetRecord record = registry.SerializeUnknownComponent(component, 0, new EntityComponentSaveState());

    Assert.Equal(typeof(TestScriptSerializableComponent).AssemblyQualifiedName, record.ComponentTypeId);
    Assert.Contains("automatic serialization", logger.WarningMessages[0], StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests -v minimal`

Expected: `FAIL` because no automatic fallback serializer exists yet.

- [ ] **Step 3: Implement reflected editor serialization fallback**

```csharp
public sealed class ScriptComponentReflectionSchemaBuilder {
    public ScriptComponentReflectionSchema Build(Type componentType) {
        MemberInfo[] members = componentType
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Where(member => member is PropertyInfo || member is FieldInfo)
            .OrderBy(member => member.Name, StringComparer.Ordinal)
            .ToArray();
        return new ScriptComponentReflectionSchema(componentType, members);
    }
}
```

```csharp
public sealed class AutomaticScriptComponentPersistenceDescriptor {
    public SceneComponentAssetRecord Serialize(Component component, ScriptComponentReflectionSchema schema, ILogger logger) {
        logger.WriteWarning($"Component '{schema.ComponentType.FullName}' is using automatic serialization.");
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(1);
        writer.WriteInt32(schema.Members.Count);
        foreach (ScriptComponentReflectionMember member in schema.Members) {
            writer.WriteString(member.Name);
            member.WriteValue(writer, component);
        }

        return new SceneComponentAssetRecord {
            ComponentTypeId = schema.ComponentType.AssemblyQualifiedName,
            Payload = stream.ToArray()
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs
rtk git commit -m "feat: add automatic editor serialization for script components"
```

## Task 6: Generate Compact Player Deserializers From Reflected Schema

**Files:**
- Create: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- Create: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Create: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`

- [ ] **Step 1: Write the failing generated-deserializer test**

```csharp
[Fact]
public void Generate_WhenSchemaContainsSupportedMembers_EmitsOrdinalDeserializerSource() {
    ScriptComponentReflectionSchema schema = BuildSchema(typeof(TestScriptSerializableComponent));
    ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

    string source = generator.Generate(schema);

    Assert.Contains("reader.ReadString()", source);
    Assert.Contains("reader.ReadBoolean()", source);
    Assert.DoesNotContain("DisplayName", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests -v minimal`

Expected: `FAIL` because no generator exists yet.

- [ ] **Step 3: Implement compact ordinal deserializer generation**

```csharp
public sealed class ScriptComponentPlayerDeserializerGenerator {
    public string Generate(ScriptComponentReflectionSchema schema) {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("public override Component Deserialize(EngineBinaryReader reader) {");
        builder.AppendLine($"    {schema.ComponentType.FullName} component = new {schema.ComponentType.FullName}();");
        builder.AppendLine("    component.DisplayName = reader.ReadString();");
        builder.AppendLine("    component.Visible = reader.ReadBoolean();");
        builder.AppendLine("    return component;");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs
rtk git commit -m "feat: generate compact player deserializers for script components"
```

## Task 7: Rewire The Demo-Disc Writer To Use Module Ownership

**Files:**
- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Modify: `tools/demo-disc-scene-writer/Program.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing module-ownership tests**

```csharp
[Fact]
public void WriteAll_WhenMenuCodeLivesOutsideManifestBoundary_UsesMainModuleAssemblyName() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    MenuHostComponent component = LoadMenuHost(ProjectRootPath);
    Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, gameplay", component.ProviderTypeName);
}

[Fact]
public void WriteAll_WhenMenuCodeLivesInsideManifestBoundary_UsesOwningModuleAssemblyName() {
    Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "menu"));
    File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "menu", "code.module.json"), """
    { "moduleId": "gameplay.menu", "dependencyModuleIds": [ "gameplay" ], "loadScopes": [ "always-loaded" ] }
    """);

    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());
    writer.WriteAll(ProjectRootPath);

    MenuHostComponent component = LoadMenuHost(ProjectRootPath);
    Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, gameplay.menu", component.ProviderTypeName);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~DemoDiscSceneWriterTests -v minimal`

Expected: `FAIL` because the writer does not derive provider ownership from module boundaries yet.

- [ ] **Step 3: Implement provider module ownership resolution**

```csharp
string ResolveProviderTypeName(string projectRootPath, string authoredMenuFolderPath) {
    EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestService(projectRootPath).Load();
    string normalizedFolderPath = authoredMenuFolderPath.Replace('\\', '/');
    string owningModuleId = null;
    foreach (EditorCodeModuleManifestEntry module in manifestDocument.Modules) {
        string normalizedModuleFolderPath = module.FolderPath.Replace('\\', '/');
        if (normalizedFolderPath.StartsWith(normalizedModuleFolderPath, StringComparison.OrdinalIgnoreCase)) {
            owningModuleId = module.ModuleId;
        }
    }
    if (string.IsNullOrWhiteSpace(owningModuleId)) {
        owningModuleId = "gameplay";
    }

    return $"city.menu.DemoDiscMenuDefinitionProvider, {owningModuleId}";
}
```

- [ ] **Step 4: Run test to verify it passes and regenerate city assets**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~DemoDiscSceneWriterTests -v minimal`

Expected: `PASS`

Run: `rtk dotnet run --project tools/demo-disc-scene-writer/helengine.demo-disc.scene-writer.csproj -- C:\dev\helprojs\city`

Expected: `Demo disc menu assets were written successfully.`

- [ ] **Step 5: Commit**

```bash
rtk git add tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs tools/demo-disc-scene-writer/Program.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "feat: derive demo disc provider type from module ownership"
```

## Task 8: Run Focused Verification Across Modules, Serialization, And Menu Integration

**Files:**
- Verify: `engine/helengine.editor.tests/EditorProjectPathsTests.cs`
- Verify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- Verify: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`
- Verify: `engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs`
- Verify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Verify: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
- Verify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Run the focused architecture test suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectPathsTests|FullyQualifiedName~EditorGameSolutionServiceTests|FullyQualifiedName~EditorGameScriptAssemblyHostTests|FullyQualifiedName~MenuDefinitionProviderResolverTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: `PASS`

- [ ] **Step 2: Build and reload city scripts through the editor script pipeline**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGameScriptHotReloadServiceTests -v minimal`

Expected: `PASS`

- [ ] **Step 3: Regenerate the city demo-disc assets**

Run: `rtk dotnet run --project tools/demo-disc-scene-writer/helengine.demo-disc.scene-writer.csproj -- C:\dev\helprojs\city`

Expected: `Demo disc menu assets were written successfully.`

- [ ] **Step 4: Verify the generated scene still points to the expected provider type**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~DemoDiscSceneWriterTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add docs/superpowers/plans/2026-05-04-demo-disc-main-menu.md
rtk git commit -m "docs: record dynamic module and menu execution plan"
```

## Self-Review

**Spec coverage:** Task 1 and Task 2 cover generated project placement outside `assets` and module-owned assembly identity. Task 3 and Task 4 cover dynamic loading and full-type resolution for menu providers and scripted types. Task 5 and Task 6 cover automated editor serialization with warnings plus compact generated player deserializers. Task 7 rewires the demo-disc writer to use module ownership. Task 8 verifies the integrated path.

**Placeholder scan:** The plan uses exact file paths, explicit tests, concrete commands, and concrete class names. No `TODO`, `TBD`, or vague “handle appropriately” placeholders remain.

**Type consistency:** The plan consistently uses `EditorGeneratedCodeModuleProject`, `EditorGeneratedCodeSolution`, `IScriptTypeResolver`, `ScriptTypeResolver`, `AutomaticScriptComponentPersistenceDescriptor`, `ScriptComponentPlayerDeserializerGenerator`, and `MenuDefinitionProviderResolver` across tasks.
