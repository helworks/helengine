# Generic Menu Components And Dynamic Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the demo-specific menu component family to generic menu components and make reflected ordinal packaging the default fallback when platform compatibility metadata is missing.

**Architecture:** Keep explicit `PassThrough` and `Transform` compatibility handling unchanged, but route missing compatibility through the same reflected ordinal packaging path currently reserved for automatic script components. Rename the current `DemoMenu*` component family to `Menu*`, update all editor/runtime registrations to the new ids, and let the packager and native deserializer generation treat those menu components like any other reflectable component.

**Tech Stack:** C#/.NET 9, xUnit, engine scene persistence descriptors, Windows scene packager, reflected ordinal serializer, generated native C++ runtime codegen.

---

### Task 1: Rename The Menu Component Family

**Files:**
- Create: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/MenuPanelComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/MenuItemComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/MenuSelectedDescriptionComponent.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMenuComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMenuPanelComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMenuItemComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMenuSelectedDescriptionComponentDeserializer.cs`
- Create: `engine/helengine.editor/serialization/scene/MenuComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/MenuPanelComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/MenuItemComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/MenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/components/2d/menu/DemoMenuPanelRuntime.cs`
- Modify: `engine/helengine.core/components/2d/menu/DemoMenuItemRuntime.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Delete: `engine/helengine.core/components/2d/menu/DemoMenuBuildComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/DemoMenuPanelComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/DemoMenuItemComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/DemoMenuSelectedDescriptionComponent.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeDemoMenuBuildComponentDeserializer.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeDemoMenuPanelComponentDeserializer.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeDemoMenuItemComponentDeserializer.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeDemoMenuSelectedDescriptionComponentDeserializer.cs`
- Delete: `engine/helengine.editor/serialization/scene/DemoMenuBuildComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/serialization/scene/DemoMenuPanelComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/serialization/scene/DemoMenuItemComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/serialization/scene/DemoMenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing rename regressions**

```csharp
[Fact]
public void LoadScene_WhenMenuComponentIsPersisted_UsesGenericMenuTypeId() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    registry.Register(new MenuComponentPersistenceDescriptor());
    registry.Register(new MenuPanelComponentPersistenceDescriptor());
    registry.Register(new MenuItemComponentPersistenceDescriptor());
    registry.Register(new MenuSelectedDescriptionComponentPersistenceDescriptor());

    SceneComponentAssetRecord record = new SceneComponentAssetRecord {
        ComponentTypeId = MenuComponent.SerializedComponentTypeId,
        ComponentIndex = 0,
        Payload = new MenuComponentPersistenceDescriptor().SerializeComponent(
            new MenuComponent {
                ProviderTypeName = "city.menu.DemoDiscMenuDefinitionProvider, city",
                InitialPanelId = "main"
            },
            0,
            null).Payload
    };

    Assert.Equal("helengine.MenuComponent", record.ComponentTypeId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneFileLoadServiceTests.LoadScene_WhenMenuComponentIsPersisted_UsesGenericMenuTypeId|FullyQualifiedName~RuntimeSceneLoadServiceTests.LoadScene_WhenMenuComponentIsPersisted_UsesGenericMenuRuntimeTypes|FullyQualifiedName~DemoDiscSceneWriterTests.WriteScene_WhenMenuIsGenerated_UsesGenericMenuComponentIds" -v minimal`

Expected: `FAIL` because `MenuComponent*` types and ids do not exist yet.

- [ ] **Step 3: Write the minimal rename implementation**

```csharp
namespace helengine {
    /// <summary>
    /// Stores authored menu metadata and drives runtime navigation against the baked menu hierarchy.
    /// </summary>
    public class MenuComponent : UpdateComponent {
        /// <summary>
        /// Current payload version used by packaged menu payloads.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id for generic menu components.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.MenuComponent";
    }
}
```

```csharp
// RuntimeComponentRegistry.cs
registry.Register(new RuntimeMenuComponentDeserializer());
registry.Register(new RuntimeMenuPanelComponentDeserializer());
registry.Register(new RuntimeMenuItemComponentDeserializer());
registry.Register(new RuntimeMenuSelectedDescriptionComponentDeserializer());
```

```csharp
// EditorSession.cs
persistenceRegistry.Register(new MenuComponentPersistenceDescriptor());
persistenceRegistry.Register(new MenuPanelComponentPersistenceDescriptor());
persistenceRegistry.Register(new MenuItemComponentPersistenceDescriptor());
persistenceRegistry.Register(new MenuSelectedDescriptionComponentPersistenceDescriptor());
```

- [ ] **Step 4: Run the rename-focused tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: `PASS` for the renamed menu component coverage, or smaller residual failures that are purely in packaging/codegen tasks below.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core/components/2d/menu engine/helengine.core/scene/runtime engine/helengine.editor/serialization/scene engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "refactor: rename demo menu components"
```

### Task 2: Make Missing Compatibility Use Generic Reflected Packaging

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs`
- Modify: `engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing packager fallback regressions**

```csharp
[Fact]
public void Package_WhenComponentHasNoCompatibilityEntry_UsesGenericReflectedFallback() {
    EditorWindowsBuildScenePackager packager = CreatePackagerWithoutMenuCompatibility();
    SceneComponentAssetRecord sourceRecord = BuildMenuComponentRecord();

    EditorWindowsBuildScenePackagerResult result = packager.Package(
        "C:\\build-root",
        BuildSceneDocumentWithComponent(sourceRecord));

    SceneComponentAssetRecord packagedRecord = Assert.Single(result.Scene.Entities[0].Components);
    Assert.Equal(MenuComponent.SerializedComponentTypeId, packagedRecord.ComponentTypeId);
    Assert.NotEmpty(packagedRecord.Payload);
}

[Fact]
public void Package_WhenExplicitTransformExists_PrefersTransformOverGenericFallback() {
    SceneComponentAssetRecord meshRecord = BuildMeshRecord();
    SceneComponentAssetRecord transformed = PackageSingle(meshRecord);

    Assert.Equal(meshRecord.ComponentTypeId, transformed.ComponentTypeId);
    Assert.NotEqual(meshRecord.Payload, transformed.Payload);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenComponentHasNoCompatibilityEntry_UsesGenericReflectedFallback|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenExplicitTransformExists_PrefersTransformOverGenericFallback" -v minimal`

Expected: `FAIL` because the packager still throws `Platform 'windows' does not declare compatibility`.

- [ ] **Step 3: Implement the generic fallback path**

```csharp
// EditorWindowsBuildScenePackager.cs
PlatformComponentCompatibilityDefinition TryGetComponentCompatibility(string componentTypeId) {
    if (ComponentCompatibilitiesByTypeId.TryGetValue(componentTypeId, out PlatformComponentCompatibilityDefinition compatibility)) {
        return compatibility;
    }

    return null;
}

SceneComponentAssetRecord PackageComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
    PlatformComponentCompatibilityDefinition compatibility = TryGetComponentCompatibility(record.ComponentTypeId);
    if (compatibility == null) {
        if (TransformService.TryTransform(record, buildRootPath, out SceneComponentAssetRecord fallbackRecord)) {
            return fallbackRecord;
        }

        throw new InvalidOperationException($"Platform '{PlatformId}' cannot package component '{record.ComponentTypeId}' through explicit compatibility or generic reflected fallback.");
    }

    if (compatibility.CompatibilityKind == PlatformComponentCompatibilityKind.PassThrough) {
        return record;
    }

    if (compatibility.CompatibilityKind == PlatformComponentCompatibilityKind.Transform && TransformService.TryTransform(record, buildRootPath, out SceneComponentAssetRecord transformedRecord)) {
        return transformedRecord;
    }

    throw new InvalidOperationException(BuildUnsupportedComponentMessage(record.ComponentTypeId, compatibility));
}
```

```csharp
// SceneComponentPackagingTransformService.cs
if (TryRewriteReflectedComponentRecord(record, out transformedRecord)) {
    return true;
}
```

```csharp
SceneComponentAssetRecord RewriteReflectedComponentRecord(SceneComponentAssetRecord record) {
    IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(record.ComponentTypeId);
    Component component = descriptor.DeserializeComponent(record, null, null);
    ScriptComponentReflectionSchema schema = ScriptComponentSchemaBuilder.Build(component.GetType());

    using MemoryStream stream = new MemoryStream();
    using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
    writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
    writer.WriteInt32(schema.Members.Count);
    for (int index = 0; index < schema.Members.Count; index++) {
        ScriptComponentReflectionMember member = schema.Members[index];
        AutomaticScriptComponentPersistenceDescriptor.WriteSupportedValue(writer, member.ValueType, member.GetValue(component));
    }

    return new SceneComponentAssetRecord {
        ComponentTypeId = record.ComponentTypeId,
        ComponentIndex = record.ComponentIndex,
        Payload = stream.ToArray()
    };
}
```

- [ ] **Step 4: Run the packager fallback tests**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v minimal`

Expected: `PASS` for missing-compatibility fallback, explicit transform precedence, and unsupported-shape errors.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchemaBuilder.cs engine/helengine.editor/serialization/scene/ScriptComponentReflectionSchema.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs
rtk git commit -m "feat: add generic reflected packaging fallback"
```

### Task 3: Update Native Runtime Deserializer Generation And Generated-Core Surfaces

**Files:**
- Modify: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing native generation regressions**

```csharp
[Fact]
public void Generate_WhenMenuComponentUsesGenericFallback_EmitsOrderedDeserializerReads() {
    ScriptComponentReflectionSchema schema = BuildSchema(
        typeof(MenuComponent),
        nameof(MenuComponent.ProviderTypeName),
        nameof(MenuComponent.InitialPanelId));

    string source = new ScriptComponentPlayerDeserializerGenerator().Generate(schema);

    Assert.Contains("component.ProviderTypeName = reader.ReadString();", source);
    Assert.Contains("component.InitialPanelId = reader.ReadString();", source);
}

[Fact]
public void Regenerate_WhenMenuComponentFileNamesAreGeneric_UpdatesGeneratedCorePatches() {
    string normalizedSource = RegenerateMenuSource();

    Assert.Contains("class MenuComponent", normalizedSource);
    Assert.DoesNotContain("DemoMenuBuildComponent", normalizedSource);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal`

Expected: `FAIL` because generator and regeneration tests still expect `DemoMenu*` class names and old assumptions.

- [ ] **Step 3: Implement the codegen and generated-core updates**

```csharp
// ScriptComponentPlayerDeserializerGenerator.cs
builder.AppendLine($"    {schema.ComponentType.FullName} component = new {schema.ComponentType.FullName}();");
for (int index = 0; index < schema.Members.Count; index++) {
    ScriptComponentReflectionMember member = schema.Members[index];
    builder.AppendLine($"    component.{member.Name} = {BuildReadExpression(member.ValueType)};");
}
```

```csharp
// EditorGeneratedCoreRegenerationService.cs
if (string.Equals(fileName, "MenuComponent.cpp", StringComparison.OrdinalIgnoreCase)) {
    updatedContents = updatedContents.Replace(
        "InputGamepadState* MenuComponent::ReadPrimaryGamepadState()",
        "InputGamepadState MenuComponent::ReadPrimaryGamepadState()");
}
```

```csharp
if (string.Equals(fileName, "MenuComponent.hpp", StringComparison.OrdinalIgnoreCase)) {
    updatedContents = updatedContents.Replace(
        "class MenuItemComponent;",
        "class MenuItemComponent;\nclass MenuSelectedDescriptionComponent;");
}
```

- [ ] **Step 4: Run the native/codegen verification**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenSceneContainsMenuComponent_LeavesPackagedComponentLoadable" -v minimal`

Expected: `PASS` with generated deserializers reading ordinal payloads in schema order and generated-core patches using `Menu*` names.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs engine/helengine.editor.tests/managers/project/ScriptComponentPlayerDeserializerGeneratorTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
rtk git commit -m "feat: update native menu deserializer generation"
```

### Task 4: Remove Menu-Specific Compatibility Assumptions And Verify End-To-End

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the final no-custom-menu compatibility regression**

```csharp
[Fact]
public void Package_WhenMenuComponentsHaveNoExplicitCompatibility_StillBuildsAndLoads() {
    EditorWindowsBuildScenePackager packager = CreatePackagerWithoutMenuCompatibility();
    EditorWindowsBuildScenePackagerResult result = packager.Package(
        "C:\\build-root",
        BuildGeneratedMenuScene());

    Entity loadedRoot = LoadPackagedScene(result);
    MenuComponent menuComponent = Assert.IsType<MenuComponent>(
        Assert.Single(loadedRoot.Components, component => component is MenuComponent));
    Assert.Equal("main", menuComponent.InitialPanelId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenMenuComponentsHaveNoExplicitCompatibility_StillBuildsAndLoads" -v minimal`

Expected: `FAIL` until menu-specific compatibility entries and transform assumptions are removed cleanly.

- [ ] **Step 3: Remove the menu-specific compatibility assumptions**

```csharp
// EditorWindowsBuildScenePackager.cs
return [
    new PlatformComponentCompatibilityDefinition(MeshComponentTypeId, PlatformComponentCompatibilityKind.Transform, "...", string.Empty),
    new PlatformComponentCompatibilityDefinition(CameraComponentTypeId, PlatformComponentCompatibilityKind.Transform, "...", string.Empty),
    new PlatformComponentCompatibilityDefinition(FPSComponentTypeId, PlatformComponentCompatibilityKind.Transform, "...", string.Empty),
    new PlatformComponentCompatibilityDefinition(TextComponentTypeId, PlatformComponentCompatibilityKind.Transform, "...", string.Empty)
];
```

```csharp
// SceneComponentPackagingTransformService.cs
// Remove the old RewriteDemoMenu*ComponentRecord branches entirely so menu components use RewriteReflectedComponentRecord instead.
```

- [ ] **Step 4: Run the end-to-end verification suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: `PASS` with menu components building, packaging, and loading through the generic fallback while explicit custom transforms continue to function.

- [ ] **Step 5: Run the broader native/build slices**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ScriptComponentPlayerDeserializerGeneratorTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorPlatformCodeCookServiceTests" -v minimal`

Expected: `PASS` with no `DemoMenu*` references remaining in generated/native packaging paths.

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
rtk git commit -m "feat: route generic menu packaging through reflected fallback"
```
