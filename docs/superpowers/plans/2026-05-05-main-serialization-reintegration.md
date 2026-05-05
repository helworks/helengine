# Main Serialization Reintegration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reinstate the planned module-loading and generic scripted-component serialization architecture on `main`, then reconcile the physics outer asset schema on top of that unified path.

**Architecture:** First port the real generated-code foundation from `feature/demo-disc-dynamic-modules`. Then add the missing shared script type resolver, multi-module assembly host, automatic editor serialization fallback, and generated player deserializer pipeline directly on top of `main`. Only after those layers work should the physics outer asset version and authored assets be reconciled.

**Tech Stack:** C# / .NET 9, HelEngine editor/runtime libraries, collectible `AssemblyLoadContext`, scene persistence/runtime loading, xUnit, authored city assets under `C:\dev\helprojs\city`

---

## File Structure

### Stage 1 donor merge

- Modify: `engine/helengine.editor/EditorProjectPaths.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameSolutionService.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs`

### Stage 2 shared script runtime

- Create: `engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs`
- Create: `engine/helengine.core/scripting/IScriptTypeResolver.cs`
- Create: `engine/helengine.core/scripting/ScriptTypeResolver.cs`
- Modify: `engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptHotReloadService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`
- Modify: `engine/helengine.editor.tests/EditorGameScriptHotReloadServiceTests.cs`

### Stage 3 type resolution consumers

- Modify: `engine/helengine.core/menu/MenuDefinitionProviderResolver.cs`
- Modify: `engine/helengine.core/components/2d/menu/MenuHostComponent.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs`
- Modify: `engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs`

### Stage 4 automatic editor serialization

- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionMember.cs`
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs`
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs`
- Create: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs`

### Stage 5 generated player deserializers

- Create: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Create: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- Create: `engine/helengine.editor/managers/project/ScriptComponentGeneratedDeserializerManifest.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Create: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

### Stage 6 scripted reference reintegration

- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Modify: `tools/demo-disc-scene-writer/Program.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

### Stage 7 physics schema reconciliation

- Modify: `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`
- Verify regenerated authored assets under `C:\dev\helprojs\city\assets`

## Task 1: Port The Generated-Code Foundation From The Donor Branch

**Files:**
- Modify: `engine/helengine.editor/EditorProjectPaths.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs`
- Create: `engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs`
- Modify: `engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs`

- [ ] **Step 1: Replace the project-path tests with generated-code path assertions**

```csharp
[Fact]
public void Initialize_WhenProjectRootIsProvided_ResolvesGeneratedCodeWorkspaceOutsideAssets() {
    string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-path-tests", Guid.NewGuid().ToString("N"));

    EditorProjectPaths.Initialize(projectRootPath);

    Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "generated_code"), EditorProjectPaths.GeneratedCodeRoot);
    Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "generated_code", "projects"), EditorProjectPaths.GeneratedCodeProjectsRoot);
}
```

- [ ] **Step 2: Run the solution-service tests to confirm the old single-project layout still fails the new expectations**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameSolutionServiceTests" -v minimal`

Expected: `FAIL` because `main` still writes `assets/<Project>.csproj`.

- [ ] **Step 3: Port the donor generated-code model files and path properties**

```csharp
public static string GeneratedCodeRoot => GeneratedCodeRootPath;
public static string GeneratedCodeProjectsRoot => GeneratedCodeProjectsRootPath;

GeneratedCodeRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code");
GeneratedCodeProjectsRootPath = Path.Combine(GeneratedCodeRootPath, "projects");
```

```csharp
public sealed class EditorGeneratedCodeModuleProject {
    public string ModuleId { get; }
    public string SourceFolderPath { get; }
    public IReadOnlyList<string> NestedSourceFolderPaths { get; }
    public string ProjectFilePath { get; }
    public string BaseIntermediateOutputPath { get; }
    public string BaseOutputPath { get; }
    public string OutputDirectoryPath { get; }
    public Guid ProjectGuid { get; }
}
```

```csharp
public sealed class EditorGeneratedCodeSolution {
    public IReadOnlyList<EditorGeneratedCodeModuleProject> ModuleProjects { get; }
    public EditorGeneratedCodeModuleProject PrimaryModuleProject => ModuleProjects[0];
}
```

- [ ] **Step 4: Port the per-module solution builder and update solution generation expectations**

```csharp
string projectDirectoryPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "projects", module.ModuleId);
string projectFilePath = Path.Combine(projectDirectoryPath, module.ModuleId + ".csproj");
string baseIntermediateOutputPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "obj", module.ModuleId);
string baseOutputPath = Path.Combine(fullProjectRootPath, "user_settings", "generated_code", "bin", module.ModuleId);
string outputDirectoryPath = Path.Combine(baseOutputPath, "Debug", TargetFrameworkValue);
```

```csharp
Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj")));
Assert.Contains("user_settings/generated_code/projects/gameplay/gameplay.csproj", solutionFileContents);
Assert.Equal(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "bin", "gameplay", "Debug", "net9.0"), service.GeneratedOutputDirectoryPath);
Assert.Equal(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "bin", "gameplay", "Debug", "net9.0", "gameplay.dll"), service.GeneratedOutputAssemblyPath);
```

- [ ] **Step 5: Run the focused generated-code tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameSolutionServiceTests|FullyQualifiedName~EditorProjectPathsTests" -v minimal`

Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.editor/EditorProjectPaths.cs engine/helengine.editor/managers/project/EditorGeneratedCodeModuleProject.cs engine/helengine.editor/managers/project/EditorGeneratedCodeSolution.cs engine/helengine.editor/managers/project/EditorGeneratedCodeSolutionBuilder.cs engine/helengine.editor/managers/project/EditorGameSolutionService.cs engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs engine/helengine.editor.tests/EditorProjectPathsTests.cs
rtk git commit -m "feat: port generated code workspace foundation"
```

## Task 2: Port Root Gameplay-Module Fallback For Loose Scripts

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs`

- [ ] **Step 1: Add the root-fallback failing test from the donor branch**

```csharp
[Fact]
public void Load_when_scripts_exist_outside_manifests_emits_root_gameplay_module() {
    EditorCodeModuleManifestService service = new(ProjectRootPath);

    EditorCodeModuleManifestDocument document = service.Load();

    EditorCodeModuleManifestEntry gameplay = Assert.Single(document.Modules, module => module.ModuleId == "gameplay");
    Assert.Equal("assets", gameplay.FolderPath);
    Assert.Contains("assets/Scripts/Ui", gameplay.NestedModuleFolderPaths);
    Assert.Equal(new[] { "always-loaded" }, gameplay.LoadScopes);
}
```

- [ ] **Step 2: Run the manifest tests to observe the current missing-root-module behavior**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCodeModuleManifestServiceTests" -v minimal`

Expected: `FAIL` because folder-scoped manifests currently suppress the root fallback entirely.

- [ ] **Step 3: Port the donor root-fallback logic into the manifest service**

```csharp
if (discoveredModules.Length > 0) {
    if (ProjectContainsScriptsOutsideModules(assetsRootPath, discoveredModules)) {
        return new EditorCodeModuleManifestDocument(BuildRootFallbackManifestEntries(discoveredModules));
    }

    return new EditorCodeModuleManifestDocument(discoveredModules);
}
```

```csharp
new EditorCodeModuleManifestEntry(
    DefaultModuleId,
    DefaultSourceRoot,
    [],
    [DefaultLoadScope],
    [.. discoveredModules.Select(module => module.FolderPath).OrderBy(static folderPath => folderPath, StringComparer.OrdinalIgnoreCase)])
```

- [ ] **Step 4: Add a multi-module generated-solution test that proves nested modules create one project per module**

```csharp
[Fact]
public void GenerateSolutionFiles_WhenModulesExist_WritesOneProjectPerModuleOutsideAssets() {
    Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay", "gameplay.csproj")));
    Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "user_settings", "generated_code", "projects", "gameplay.ui", "gameplay.ui.csproj")));
}
```

- [ ] **Step 5: Run the manifest and generated-solution tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorCodeModuleManifestServiceTests|FullyQualifiedName~EditorCodeModuleManifestServiceRootFallbackTests|FullyQualifiedName~EditorGameSolutionServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorCodeModuleManifestService.cs engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceTests.cs engine/helengine.editor.tests/managers/project/EditorCodeModuleManifestServiceRootFallbackTests.cs engine/helengine.editor.tests/EditorGameSolutionServiceTests.cs
rtk git commit -m "feat: add root gameplay module fallback"
```

## Task 3: Add Shared Script Type Resolution And Multi-Module Assembly Hosting

**Files:**
- Create: `engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs`
- Create: `engine/helengine.core/scripting/IScriptTypeResolver.cs`
- Create: `engine/helengine.core/scripting/ScriptTypeResolver.cs`
- Modify: `engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs`
- Modify: `engine/helengine.editor.tests/EditorGameScriptHotReloadServiceTests.cs`

- [ ] **Step 1: Add a failing assembly-host test that expects more than one module assembly**

```csharp
[Fact]
public void Reload_WhenTwoModuleAssembliesExist_RegistersBothAssembliesByModuleId() {
    EditorGameScriptAssemblyHost host = new EditorGameScriptAssemblyHost(ProjectRootPath);

    host.Reload(new[] {
        new ScriptAssemblyDescriptor("gameplay", gameplayOutputDirectoryPath, gameplayAssemblyPath),
        new ScriptAssemblyDescriptor("gameplay.ui", gameplayUiOutputDirectoryPath, gameplayUiAssemblyPath)
    });

    Assert.Equal("gameplay", host.ScriptTypeResolver.Resolve("city.PlayerComponent, gameplay").Assembly.GetName().Name);
    Assert.Equal("gameplay.ui", host.ScriptTypeResolver.Resolve("city.ui.InventoryPanelComponent, gameplay.ui").Assembly.GetName().Name);
}
```

- [ ] **Step 2: Run the hot-reload tests to verify the current host interface is too narrow**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameScriptHotReloadServiceTests|FullyQualifiedName~EditorGameScriptAssemblyHostTests" -v minimal`

Expected: `FAIL` because `IEditorScriptAssemblyHost.Reload` still accepts exactly one assembly path.

- [ ] **Step 3: Add the shared core type-resolution types**

```csharp
public sealed class ScriptAssemblyDescriptor {
    public ScriptAssemblyDescriptor(string moduleId, string outputDirectoryPath, string assemblyPath) { ... }
    public string ModuleId { get; }
    public string OutputDirectoryPath { get; }
    public string AssemblyPath { get; }
}
```

```csharp
public interface IScriptTypeResolver {
    Type Resolve(string assemblyQualifiedTypeName);
}
```

```csharp
public sealed class ScriptTypeResolver : IScriptTypeResolver {
    readonly Dictionary<string, Assembly> AssembliesByName = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string moduleId, Assembly assembly) { ... }
    public Type Resolve(string assemblyQualifiedTypeName) { ... }
}
```

- [ ] **Step 4: Expand the assembly host to load snapshots per module and expose the resolver**

```csharp
public interface IEditorScriptAssemblyHost : IDisposable {
    IScriptTypeResolver ScriptTypeResolver { get; }
    void Reload(IReadOnlyList<ScriptAssemblyDescriptor> assemblies);
    IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity);
}
```

```csharp
readonly Dictionary<string, Assembly> CurrentAssembliesByModuleId;
readonly Dictionary<string, EditorCollectibleScriptAssemblyLoadContext> CurrentLoadContextsByModuleId;
public IScriptTypeResolver ScriptTypeResolver { get; }
```

- [ ] **Step 5: Update hot-reload orchestration to pass module descriptors instead of one output path**

```csharp
IReadOnlyList<ScriptAssemblyDescriptor> assemblies = GameSolutionService.GeneratedModuleProjects
    .Select(project => new ScriptAssemblyDescriptor(project.ModuleId, project.OutputDirectoryPath, Path.Combine(project.OutputDirectoryPath, project.ModuleId + ".dll")))
    .ToArray();
AssemblyHost.Reload(assemblies);
```

- [ ] **Step 6: Run the multi-module host and hot-reload tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameScriptAssemblyHostTests|FullyQualifiedName~EditorGameScriptHotReloadServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 7: Commit**

```bash
rtk git add engine/helengine.core/scripting/ScriptAssemblyDescriptor.cs engine/helengine.core/scripting/IScriptTypeResolver.cs engine/helengine.core/scripting/ScriptTypeResolver.cs engine/helengine.editor/managers/project/IEditorScriptAssemblyHost.cs engine/helengine.editor/managers/project/EditorGameScriptAssemblyHost.cs engine/helengine.editor/managers/project/EditorGameScriptHotReloadService.cs engine/helengine.editor.tests/managers/project/EditorGameScriptAssemblyHostTests.cs engine/helengine.editor.tests/EditorGameScriptHotReloadServiceTests.cs
rtk git commit -m "feat: add shared script type resolution"
```

## Task 4: Route Menu Provider Resolution Through The Shared Resolver

**Files:**
- Modify: `engine/helengine.core/menu/MenuDefinitionProviderResolver.cs`
- Modify: `engine/helengine.core/components/2d/menu/MenuHostComponent.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs`
- Modify: `engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs`

- [ ] **Step 1: Add a failing resolver test that uses a registered script module assembly**

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

- [ ] **Step 2: Run the menu resolver tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MenuDefinitionProviderResolverTests" -v minimal`

Expected: `FAIL` because the resolver still hardcodes `Type.GetType(...)`.

- [ ] **Step 3: Inject the shared resolver and keep the existing validation behavior**

```csharp
readonly IScriptTypeResolver ScriptTypeResolver;

public MenuDefinitionProviderResolver(IScriptTypeResolver scriptTypeResolver = null) {
    ScriptTypeResolver = scriptTypeResolver;
}

Type providerType = ScriptTypeResolver != null
    ? ScriptTypeResolver.Resolve(providerTypeName)
    : Type.GetType(providerTypeName, false);
```

- [ ] **Step 4: Rewire menu-host creation sites to pass the loaded script resolver**

```csharp
menuHostComponent.ProviderResolver = new MenuDefinitionProviderResolver(scriptTypeResolver);
```

- [ ] **Step 5: Run the menu resolver and runtime scene-load tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MenuDefinitionProviderResolverTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.core/menu/MenuDefinitionProviderResolver.cs engine/helengine.core/components/2d/menu/MenuHostComponent.cs engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs engine/helengine.editor.tests/menu/MenuDefinitionProviderResolverTests.cs
rtk git commit -m "feat: resolve menu providers through script modules"
```

## Task 5: Add Automatic Editor Serialization Fallback For Scripted Components

**Files:**
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionMember.cs`
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs`
- Create: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs`
- Create: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs`

- [ ] **Step 1: Add the failing automatic-fallback tests**

```csharp
[Fact]
public void SerializeComponent_WhenScriptComponentHasNoExplicitDescriptor_UsesAutomaticFallbackAndLogsWarning() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    TestScriptSerializableComponent component = new TestScriptSerializableComponent {
        DisplayName = "Menu Row",
        Visible = true
    };

    IComponentPersistenceDescriptor descriptor = registry.GetDescriptor(component);
    SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());

    Assert.Equal(typeof(TestScriptSerializableComponent).AssemblyQualifiedName, record.ComponentTypeId);
}
```

```csharp
[Fact]
public void SerializeComponent_WhenMemberTypeIsUnsupported_Throws() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    Assert.Throws<InvalidOperationException>(() => descriptor.SerializeComponent(new UnsupportedScriptComponent(), 0, new EntityComponentSaveState()));
}
```

- [ ] **Step 2: Run the persistence tests to confirm the registry still rejects unknown scripted components**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPersistenceRegistryTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v minimal`

Expected: `FAIL` because `ComponentPersistenceRegistry.GetDescriptor(Component)` still throws for all unregistered component types.

- [ ] **Step 3: Add the reflected schema builder and member model**

```csharp
public sealed class ScriptComponentReflectionSchema {
    public Type ComponentType { get; }
    public IReadOnlyList<ScriptComponentReflectionMember> Members { get; }
}
```

```csharp
MemberInfo[] members = componentType
    .GetMembers(BindingFlags.Instance | BindingFlags.Public)
    .Where(member => member is PropertyInfo || member is FieldInfo)
    .OrderBy(member => member.Name, StringComparer.Ordinal)
    .ToArray();
```

- [ ] **Step 4: Implement the automatic descriptor with named-field editor payloads**

```csharp
writer.WriteByte(EditorTaggedSceneComponentPayloadFormat.CurrentVersion);
writer.WriteInt32(schema.Members.Count);
foreach (ScriptComponentReflectionMember member in schema.Members) {
    writer.WriteString(member.Name);
    byte[] fieldPayload = member.SerializeValue(component);
    writer.WriteInt32(fieldPayload.Length);
    writer.WriteByteArray(fieldPayload);
}
```

```csharp
return new SceneComponentAssetRecord {
    ComponentTypeId = schema.ComponentType.AssemblyQualifiedName,
    ComponentIndex = componentIndex,
    Payload = stream.ToArray()
};
```

- [ ] **Step 5: Update the registry to fall back only for eligible scripted components**

```csharp
if (!DescriptorsByComponentType.TryGetValue(componentType, out IComponentPersistenceDescriptor descriptor)) {
    if (componentType.Assembly != typeof(Component).Assembly && typeof(Component).IsAssignableFrom(componentType)) {
        return AutomaticDescriptor;
    }

    throw new InvalidOperationException($"No scene persistence descriptor is registered for '{componentType.Name}'.");
}
```

- [ ] **Step 6: Run the editor persistence tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~ComponentPersistenceRegistryTests|FullyQualifiedName~SceneSaveServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 7: Commit**

```bash
rtk git add engine/helengine.editor/serialization/scene/ScriptComponentReflectionMember.cs engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs
rtk git commit -m "feat: add automatic editor serialization for script components"
```

## Task 6: Generate Compact Player Deserializers For Scripted Components

**Files:**
- Create: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Create: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- Create: `engine/helengine.editor/managers/project/ScriptComponentGeneratedDeserializerManifest.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Create: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Add the failing generated-deserializer tests**

```csharp
[Fact]
public void Generate_WhenSchemaContainsSupportedMembers_EmitsOrdinalDeserializerSource() {
    ScriptComponentReflectionSchema schema = BuildSchema(typeof(TestScriptSerializableComponent));
    ScriptComponentPlayerDeserializerGenerator generator = new ScriptComponentPlayerDeserializerGenerator();

    string source = generator.Generate(schema);

    Assert.Contains("ReadString()", source);
    Assert.Contains("ReadBoolean()", source);
    Assert.DoesNotContain("DisplayName", source, StringComparison.Ordinal);
}
```

```csharp
[Fact]
public void CreateDefault_WhenScriptedRuntimeDeserializerIsRegistered_ResolvesScriptedComponentTypeId() {
    RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault();
    Assert.NotNull(registry.GetDeserializer("city.TestScriptSerializableComponent"));
}
```

- [ ] **Step 2: Run the runtime tests to verify there is no scripted runtime path yet**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal`

Expected: `FAIL` because the runtime registry does not know about scripted component type ids.

- [ ] **Step 3: Add the generator and runtime endpoint**

```csharp
public sealed class ScriptComponentPlayerDeserializerGenerator {
    public string Generate(ScriptComponentReflectionSchema schema) {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {");
        builder.AppendLine($"    {schema.ComponentType.FullName} component = new {schema.ComponentType.FullName}();");
        builder.AppendLine("    using MemoryStream stream = new MemoryStream(record.Payload);");
        builder.AppendLine("    using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);");
        builder.AppendLine("    component.DisplayName = reader.ReadString();");
        builder.AppendLine("    component.Visible = reader.ReadBoolean();");
        builder.AppendLine("    return component;");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
```

```csharp
public sealed class AutomaticScriptComponentRuntimeDeserializer : IRuntimeComponentDeserializer {
    public string ComponentTypeId { get; }
    public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) { ... }
}
```

- [ ] **Step 4: Make packaging rewrite automatic editor payloads into strict ordinal runtime payloads**

```csharp
if (LooksLikeAutomaticScriptedComponent(record.ComponentTypeId)) {
    transformedRecord = RewriteAutomaticScriptComponentRecord(record);
    return true;
}
```

```csharp
writer.WriteString(memberStringValue);
writer.WriteBoolean(memberBooleanValue);
```

- [ ] **Step 5: Register generated scripted deserializers in the runtime registry**

```csharp
foreach (AutomaticScriptComponentRuntimeDeserializer deserializer in ScriptComponentGeneratedDeserializerManifest.CreateDefault()) {
    registry.Register(deserializer);
}
```

- [ ] **Step 6: Run the generator and runtime scene-load tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~SceneSaveServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 7: Commit**

```bash
rtk git add engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs engine/helengine.editor/managers/project/ScriptComponentGeneratedDeserializerManifest.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
rtk git commit -m "feat: add generated player deserializers for script components"
```

## Task 7: Rewire Demo-Disc And Scripted References To Module Ownership

**Files:**
- Modify: `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`
- Modify: `tools/demo-disc-scene-writer/Program.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Add failing tests for root-module and folder-scoped module ownership**

```csharp
[Fact]
public void WriteAll_WhenMenuCodeLivesOutsideManifestBoundary_UsesGameplayAssemblyName() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());
    writer.WriteAll(ProjectRootPath);
    Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, gameplay", ReadProviderTypeName());
}
```

```csharp
[Fact]
public void WriteAll_WhenMenuCodeLivesInsideManifestBoundary_UsesOwningModuleAssemblyName() {
    File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "codebase", "menu", "code.module.json"), """
    {
      "moduleId": "gameplay.menu",
      "dependencyModuleIds": [ "gameplay" ],
      "loadScopes": [ "always-loaded" ]
    }
    """);

    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());
    writer.WriteAll(ProjectRootPath);
    Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, gameplay.menu", ReadProviderTypeName());
}
```

- [ ] **Step 2: Run the demo-disc writer tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: `FAIL` because the writer still derives the provider assembly from project name sanitization.

- [ ] **Step 3: Resolve provider ownership from authored module boundaries**

```csharp
EditorCodeModuleManifestDocument manifestDocument = new EditorCodeModuleManifestService(projectRootPath).Load();
string authoredMenuFolderPath = "assets/codebase/menu";
string owningModuleId = "gameplay";
foreach (EditorCodeModuleManifestEntry module in manifestDocument.Modules) {
    string normalizedModuleFolderPath = module.FolderPath.Replace('\\', '/');
    if (authoredMenuFolderPath.StartsWith(normalizedModuleFolderPath, StringComparison.OrdinalIgnoreCase)) {
        owningModuleId = module.ModuleId;
    }
}

return $"city.menu.DemoDiscMenuDefinitionProvider, {owningModuleId}";
```

- [ ] **Step 4: Update build-config module selection to include the owning module**

```csharp
windowsPlatform["selectedCodeModuleIds"] = new JsonArray(owningModuleId);
```

- [ ] **Step 5: Run the demo-disc writer tests and regenerate the city menu assets**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: `PASS`

Run: `rtk dotnet run --project tools/demo-disc-scene-writer/helengine.demo-disc.scene-writer.csproj -- C:\dev\helprojs\city`

Expected: success message from the writer.

- [ ] **Step 6: Commit**

```bash
rtk git add tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs tools/demo-disc-scene-writer/Program.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "feat: derive demo disc provider assembly from module ownership"
```

## Task 8: Reconcile The Physics Outer Asset Schema On Top Of The Unified Architecture

**Files:**
- Modify: `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`

- [ ] **Step 1: Add the failing binary serialization regression tests for the reconciled outer asset schema**

```csharp
[Fact]
public void SerializeSceneAsset_WhenPhysicsFlagsArePresent_RoundTripsVersionFivePayload() {
    SceneAsset sceneAsset = new SceneAsset {
        Id = "scene-id",
        Physics3DSceneFeatureFlags = 1234u,
        RootEntities = Array.Empty<SceneEntityAsset>()
    };

    using MemoryStream stream = new MemoryStream();
    EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
    stream.Position = 0;

    SceneAsset deserialized = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
    Assert.Equal(1234u, deserialized.Physics3DSceneFeatureFlags);
}
```

- [ ] **Step 2: Run the serialization and packaging tests to verify `main` still rejects version `5`**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests" -v minimal`

Expected: `FAIL` until the final reconciled schema is applied.

- [ ] **Step 3: Reapply the physics outer-schema changes only after the earlier stages are green**

```csharp
public uint Physics3DSceneFeatureFlags { get; set; }
public const byte CurrentVersion = 5;
writer.WriteUInt32(asset.Physics3DSceneFeatureFlags);
Physics3DSceneFeatureFlags = version >= 5 ? reader.ReadUInt32() : 0u
```

```csharp
sceneAsset.Physics3DSceneFeatureFlags = (uint)PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);
```

- [ ] **Step 4: Regenerate the affected city authored assets from the unified branch**

Run: `rtk dotnet run --project tools/demo-disc-scene-writer/helengine.demo-disc.scene-writer.csproj -- C:\dev\helprojs\city`

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests" -v minimal`

Expected: regenerated `city` assets use the same landed schema as the engine branch.

- [ ] **Step 5: Run the final focused verification suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGameSolutionServiceTests|FullyQualifiedName~EditorCodeModuleManifestServiceTests|FullyQualifiedName~EditorCodeModuleManifestServiceRootFallbackTests|FullyQualifiedName~EditorGameScriptAssemblyHostTests|FullyQualifiedName~EditorGameScriptHotReloadServiceTests|FullyQualifiedName~MenuDefinitionProviderResolverTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests" -v minimal`

Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.core/assets/raw/scene/SceneAsset.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs
rtk git commit -m "feat: reconcile physics asset schema on unified serialization path"
```

## Self-Review

**Spec coverage:** Task 1 and Task 2 cover the donor generated-code foundation and root gameplay-module fallback. Task 3 and Task 4 cover the missing shared module runtime and consumer rewiring. Task 5 and Task 6 cover automatic editor serialization plus generated player deserializers. Task 7 covers demo-disc/module ownership reintegration. Task 8 explicitly delays and then reconciles the physics outer asset schema after the generic architecture is in place.

**Placeholder scan:** The plan names concrete files, commands, tests, and target types for every stage. No `TODO`, `TBD`, or “handle appropriately” placeholders remain.

**Type consistency:** The plan consistently uses `EditorGeneratedCodeModuleProject`, `EditorGeneratedCodeSolution`, `ScriptAssemblyDescriptor`, `IScriptTypeResolver`, `ScriptTypeResolver`, `AutomaticScriptComponentPersistenceDescriptor`, and `ScriptComponentPlayerDeserializerGenerator` across all tasks.
