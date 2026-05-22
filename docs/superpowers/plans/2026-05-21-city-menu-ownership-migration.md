# City Menu Ownership Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the entire demo-disc menu stack out of `helengine` and into `city`, remove engine menu special-casing, and regenerate the city menu scenes so packaged builds use `city.*` menu component identities.

**Architecture:** Preserve the current baked-scene flow, but shift ownership of menu models, menu runtime components, menu deserializers, menu persistence descriptors, and menu generation/regeneration into city-owned code. Keep `helengine` limited to generic scene/UI primitives and remove `FPSComponent` menu awareness instead of replacing it with a new abstraction.

**Tech Stack:** C#/.NET 9, `helengine.core`, `helengine.editor`, city generated-code projects (`gameplay`, `menu.tools`, `rendering.tools`), xUnit, RTK, existing city scene generation commands, PS2 export pipeline.

---

## File Structure Map

### Engine files to delete or strip of menu ownership

- Delete: `engine/helengine.core/menu/IMenuDefinitionProvider.cs`
- Delete: `engine/helengine.core/menu/MenuActionDefinition.cs`
- Delete: `engine/helengine.core/menu/MenuActionKind.cs`
- Delete: `engine/helengine.core/menu/MenuDefinition.cs`
- Delete: `engine/helengine.core/menu/MenuDefinitionProviderResolver.cs`
- Delete: `engine/helengine.core/menu/MenuItemDefinition.cs`
- Delete: `engine/helengine.core/menu/MenuOverlayImageDefinition.cs`
- Delete: `engine/helengine.core/menu/MenuPanelDefinition.cs`
- Delete: `engine/helengine.core/menu/MenuPlatformInfoDefinition.cs`
- Delete: `engine/helengine.core/components/2d/menu/DemoDiscReturnToMenuRuntimeComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/DemoMenuLayout.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuHostItemRuntime.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuHostPanelRuntime.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuItemComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuItemRuntime.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuPanelComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuPanelRuntime.cs`
- Delete: `engine/helengine.core/components/2d/menu/MenuSelectedDescriptionComponent.cs`
- Delete: `engine/helengine.core/components/2d/menu/PlatformInfoTextComponent.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeMenuComponentDeserializer.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeMenuItemComponentDeserializer.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeMenuPanelComponentDeserializer.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeMenuSelectedDescriptionComponentDeserializer.cs`
- Delete: `engine/helengine.editor/serialization/scene/MenuComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/serialization/scene/MenuItemComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/serialization/scene/MenuPanelComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/serialization/scene/MenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Delete: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildService.cs`
- Delete: `engine/helengine.editor/managers/menu/DemoMenuSceneBuildVariant.cs`
- Delete: `engine/helengine.editor/managers/menu/DemoMenuNintendoDsLayout.cs`
- Delete: `engine/helengine.editor/managers/menu/EditorMenuSceneRegenerationService.cs`
- Delete: `engine/helengine.editor/managers/menu/NintendoDsDemoMenuSceneAssetFactory.cs`
- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCommandContext.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedMenuScenePreparationService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`

### City files to create or extend

- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuActionDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuActionKind.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuDefinitionProviderResolver.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuItemDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuOverlayImageDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPanelDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPlatformInfoDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/IMenuDefinitionProvider.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/DemoMenuLayout.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuHostItemRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuHostPanelRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuItemComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuItemRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPanelComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPanelRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuSelectedDescriptionComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuItemComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuPanelComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuSelectedDescriptionComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuItemComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuPanelComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/CityMenuSceneRegenerationService.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscMenuDefinitionProvider.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/PlatformInfoTextComponent.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu.tools/DemoDiscMainMenuSceneFactory.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu.tools/DemoDiscSceneGenerator.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu.tools/RegenerateDemoDiscMainMenuCommand.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneWriteService.cs`

### Test files

- Create: `engine/helengine.editor.tests/menu/CityMenuOwnershipMigrationSourceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedMenuScenePreparationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorCommandExecutionServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`

## Task 1: Pin the migration with failing ownership and runtime tests

**Files:**
- Create: `engine/helengine.editor.tests/menu/CityMenuOwnershipMigrationSourceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Write the failing source-ownership tests**

```csharp
namespace helengine.editor.tests.menu {
    /// <summary>
    /// Guards the rule that demo-disc menu ownership lives in city code rather than helengine.
    /// </summary>
    public sealed class CityMenuOwnershipMigrationSourceTests {
        /// <summary>
        /// Verifies the engine FPS component no longer branches on menu components.
        /// </summary>
        [Fact]
        public void ReadFpsComponentSource_DoesNotReferenceMenuComponent() {
            string source = File.ReadAllText(Path.Combine(RepoRootPath, "engine", "helengine.core", "components", "2d", "FPSComponent.cs"));
            Assert.DoesNotContain("MenuComponent", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies city menu source declares city-owned serialized component ids.
        /// </summary>
        [Fact]
        public void ReadCityMenuComponentSource_UsesCitySerializedTypeIds() {
            string source = ReadCitySource("menu", "MenuComponent.cs");
            Assert.Contains("public const string SerializedComponentTypeId = \"city.menu.MenuComponent, gameplay\";", source, StringComparison.Ordinal);
        }
    }
}
```

- [ ] **Step 2: Add a failing packaged runtime-load test for the city menu scene**

```csharp
[Fact]
public void Load_WhenPackagedCityDemoDiscMainMenuUsesCityMenuTypes_DoesNotThrow() {
    string scenePath = Path.Combine(CityWindowsOutputRootPath, "cooked", "scenes", "DemoDiscMainMenu.hasset");
    SceneAsset sceneAsset = ReadPackagedScene(scenePath);
    Assert.Contains(sceneAsset.RootEntities.SelectMany(entity => FlattenComponents(entity)),
        record => string.Equals(record.ComponentTypeId, "city.menu.MenuComponent, gameplay", StringComparison.Ordinal));

    Exception exception = Record.Exception(() => RuntimeSceneLoadService.Load(sceneAsset));
    Assert.Null(exception);
}
```

- [ ] **Step 3: Run the focused tests to verify RED**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CityMenuOwnershipMigrationSourceTests|FullyQualifiedName~Load_WhenPackagedCityDemoDiscMainMenuUsesCityMenuTypes_DoesNotThrow' 2>&1 | Select-Object -Last 140 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
FAIL ReadFpsComponentSource_DoesNotReferenceMenuComponent
FAIL ReadCityMenuComponentSource_UsesCitySerializedTypeIds
FAIL Load_WhenPackagedCityDemoDiscMainMenuUsesCityMenuTypes_DoesNotThrow
```

- [ ] **Step 4: Commit the failing-test checkpoint**

```bash
rtk git add engine/helengine.editor.tests/menu/CityMenuOwnershipMigrationSourceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs
rtk git commit -m "test: pin city menu ownership migration"
```

## Task 2: Move menu models and runtime components into city ownership

**Files:**
- Create: `C:/dev/helprojs/city/assets/codebase/menu/IMenuDefinitionProvider.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuActionDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuActionKind.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuDefinitionProviderResolver.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuItemDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuOverlayImageDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPanelDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPlatformInfoDefinition.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/DemoMenuLayout.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuHostItemRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuHostPanelRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuItemComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuItemRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPanelComponent.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuPanelRuntime.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/MenuSelectedDescriptionComponent.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu/DemoDiscMenuDefinitionProvider.cs`

- [ ] **Step 1: Implement the city-owned menu model types with city namespaces**

```csharp
namespace city.menu {
    /// <summary>
    /// Describes one authored demo-disc menu item.
    /// </summary>
    public sealed class MenuItemDefinition {
        /// <summary>
        /// Initializes one authored menu item definition.
        /// </summary>
        public MenuItemDefinition(string itemId, string label, string description, bool enabled, MenuActionDefinition action) {
            ItemId = string.IsNullOrWhiteSpace(itemId) ? throw new ArgumentException("Item id must be provided.", nameof(itemId)) : itemId;
            Label = string.IsNullOrWhiteSpace(label) ? throw new ArgumentException("Label must be provided.", nameof(label)) : label;
            Description = description ?? string.Empty;
            Enabled = enabled;
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <summary>
        /// Gets the stable authored item id.
        /// </summary>
        public string ItemId { get; }
    }
}
```

- [ ] **Step 2: Port the baked runtime components with `city.*` serialized type ids**

```csharp
namespace city.menu {
    /// <summary>
    /// Stores baked menu metadata and drives runtime navigation for the city demo-disc menu.
    /// </summary>
    public sealed class MenuComponent : UpdateComponent {
        /// <summary>
        /// Stable serialized component type id used by packaged city menu scenes.
        /// </summary>
        public const string SerializedComponentTypeId = "city.menu.MenuComponent, gameplay";

        /// <summary>
        /// Gets or sets the authored provider type name used when regenerating the menu scene.
        /// </summary>
        public string ProviderTypeName { get; set; }
    }
}
```

- [ ] **Step 3: Update city content to reference the city-owned menu types**

```csharp
namespace city.menu {
    /// <summary>
    /// Produces the demo-disc menu definition using city-owned menu models.
    /// </summary>
    public sealed class DemoDiscMenuDefinitionProvider : IMenuDefinitionProvider {
        /// <summary>
        /// Builds the complete demo-disc menu definition.
        /// </summary>
        public MenuDefinition CreateMenuDefinition() {
            return new MenuDefinition(
                string.Empty,
                string.Empty,
                "main",
                theme.TitleFontPath,
                theme.BodyFontPath,
                theme.BackgroundColor,
                theme.SurfaceColor,
                theme.SurfaceBorderColor,
                theme.AccentColor,
                theme.AccentSecondaryColor,
                theme.TextColor,
                theme.MutedTextColor,
                panels,
                overlayImage,
                platformInfoOverlay);
        }
    }
}
```

- [ ] **Step 4: Run the ownership tests to verify GREEN for the moved types**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CityMenuOwnershipMigrationSourceTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
Passed! 2 tests passed.
```

- [ ] **Step 5: Commit the city-owned menu-model migration**

```bash
rtk git add C:/dev/helprojs/city/assets/codebase/menu engine/helengine.editor.tests/menu/CityMenuOwnershipMigrationSourceTests.cs
rtk git commit -m "refactor: move menu models into city ownership"
```

## Task 3: Move menu persistence, regeneration, and runtime deserialization into city

**Files:**
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuItemComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuPanelComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu/RuntimeMenuSelectedDescriptionComponentDeserializer.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuItemComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuPanelComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/MenuSelectedDescriptionComponentPersistenceDescriptor.cs`
- Create: `C:/dev/helprojs/city/assets/codebase/menu.tools/CityMenuSceneRegenerationService.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu.tools/DemoDiscMainMenuSceneFactory.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu.tools/DemoDiscSceneGenerator.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/menu.tools/RegenerateDemoDiscMainMenuCommand.cs`
- Modify: `C:/dev/helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneWriteService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedMenuScenePreparationService.cs`
- Modify: `engine/helengine.editor/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorCommandContext.cs`

- [ ] **Step 1: Add city-owned persistence descriptors that mirror current payload behavior**

```csharp
namespace city.menu.tools {
    /// <summary>
    /// Persists the city menu root metadata stored on one authored menu entity.
    /// </summary>
    public sealed class MenuComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(city.menu.MenuComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into city menu scenes.
        /// </summary>
        public string ComponentTypeId => city.menu.MenuComponent.SerializedComponentTypeId;
    }
}
```

- [ ] **Step 2: Register the city persistence descriptors in the city scene writer**

```csharp
ComponentPersistenceRegistry CreatePersistenceRegistry() {
    ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
    persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
    persistenceRegistry.Register(new CameraComponentPersistenceDescriptor());
    persistenceRegistry.Register(new city.menu.tools.MenuComponentPersistenceDescriptor());
    persistenceRegistry.Register(new city.menu.tools.MenuPanelComponentPersistenceDescriptor());
    persistenceRegistry.Register(new city.menu.tools.MenuItemComponentPersistenceDescriptor());
    persistenceRegistry.Register(new city.menu.tools.MenuSelectedDescriptionComponentPersistenceDescriptor());
    return persistenceRegistry;
}
```

- [ ] **Step 3: Replace editor-owned regeneration with a city-owned regeneration service**

```csharp
namespace city.menu.tools {
    /// <summary>
    /// Rebuilds the city demo-disc menu scenes from the city-owned provider and scene factories.
    /// </summary>
    public sealed class CityMenuSceneRegenerationService {
        /// <summary>
        /// Rebuilds the requested city menu scene inside the project assets folder.
        /// </summary>
        public void Regenerate(string projectRootPath, string sceneId, string providerTypeName) {
            DemoDiscMenuDefinitionProvider provider = new DemoDiscMenuDefinitionProvider();
            MenuDefinition definition = provider.CreateMenuDefinition();
            GeneratedAuthoringSceneDefinition sceneDefinition = new DemoDiscMainMenuSceneFactory().CreateSceneDefinition(providerTypeName, definition);
            new GeneratedAuthoringSceneWriteService().WriteScene(projectRootPath, sceneDefinition);
        }
    }
}
```

- [ ] **Step 4: Add city-owned runtime deserializers and wire the runtime-load test green**

```csharp
namespace city.menu {
    /// <summary>
    /// Deserializes packaged city menu root records.
    /// </summary>
    public sealed class RuntimeMenuComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Gets the serialized type id handled by this deserializer.
        /// </summary>
        public string ComponentTypeId => MenuComponent.SerializedComponentTypeId;
    }
}
```

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Load_WhenPackagedCityDemoDiscMainMenuUsesCityMenuTypes_DoesNotThrow|FullyQualifiedName~EditorGeneratedMenuScenePreparationServiceTests|FullyQualifiedName~EditorCommandExecutionServiceTests' 2>&1 | Select-Object -Last 160 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
Passed! targeted regeneration and runtime-load tests passed.
```

- [ ] **Step 5: Commit the city-owned persistence and regeneration move**

```bash
rtk git add C:/dev/helprojs/city/assets/codebase/menu.tools C:/dev/helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneWriteService.cs engine/helengine.editor/managers/project/EditorGeneratedMenuScenePreparationService.cs engine/helengine.editor/EditorCliCommandRunner.cs engine/helengine.editor/managers/project/EditorCommandContext.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedMenuScenePreparationServiceTests.cs engine/helengine.editor.tests/managers/project/EditorCommandExecutionServiceTests.cs
rtk git commit -m "refactor: move menu regeneration into city tools"
```

## Task 4: Remove engine menu awareness and delete engine-owned menu registrations

**Files:**
- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Delete: all engine menu files listed in the file map

- [ ] **Step 1: Remove the failing `FPSComponent` menu branch**

```csharp
public override void Update() {
    InputSystem inputSystem = Core.Instance.Input;
    if (inputSystem == null) {
        return;
    }

    UpdateMovement(inputSystem);
    UpdateLook(inputSystem);
}
```

- [ ] **Step 2: Remove built-in menu deserializer registration from the engine runtime registry**

```csharp
public static RuntimeComponentRegistry CreateDefault() {
    RuntimeComponentRegistry registry = new RuntimeComponentRegistry();
    registry.Register(new RuntimeMeshComponentDeserializer());
    registry.Register(new RuntimeCameraComponentDeserializer());
    registry.Register(new RuntimeFPSComponentDeserializer());
    registry.Register(new RuntimeSceneMapComponentDeserializer());
    RegisterGeneratedRuntimeComponentDeserializers(registry);
    return registry;
}
```

- [ ] **Step 3: Strip menu persistence descriptor registration from engine editor registries**

```csharp
static ComponentPersistenceRegistry CreateComponentPersistenceRegistry(IScriptTypeResolver scriptTypeResolver) {
    ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry(scriptTypeResolver);
    persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
    persistenceRegistry.Register(new CameraComponentPersistenceDescriptor());
    persistenceRegistry.Register(new TextComponentPersistenceDescriptor());
    persistenceRegistry.Register(new SpriteComponentPersistenceDescriptor());
    return persistenceRegistry;
}
```

- [ ] **Step 4: Run the focused ownership tests and registry tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CityMenuOwnershipMigrationSourceTests|FullyQualifiedName~ComponentPersistenceRegistryTests|FullyQualifiedName~RuntimeSceneLoadServiceTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
Passed! targeted registry and ownership tests passed.
```

- [ ] **Step 5: Commit the engine cleanup**

```bash
rtk git add engine/helengine.core/components/2d/FPSComponent.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs
rtk git rm engine/helengine.core/menu/*.cs engine/helengine.core/components/2d/menu/*.cs engine/helengine.core/scene/runtime/RuntimeMenu*.cs engine/helengine.editor/serialization/scene/Menu*.cs engine/helengine.editor/managers/menu/DemoMenu*.cs engine/helengine.editor/managers/menu/EditorMenuSceneRegenerationService.cs engine/helengine.editor/managers/menu/NintendoDsDemoMenuSceneAssetFactory.cs
rtk git commit -m "refactor: remove engine menu ownership"
```

## Task 5: Regenerate city scenes, verify packaged outputs, and rebuild PS2

**Files:**
- Modify: `C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenu.helen`
- Modify: `C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenuDs.helen`
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`

- [ ] **Step 1: Add a test that the generated menu scenes emit `city.*` component type ids**

```csharp
[Fact]
public void DeserializeCityDemoDiscMainMenuSceneAsset_UsesCityMenuComponentTypeIds() {
    SceneAsset sceneAsset = ReadTopLevelSceneAsset("DemoDiscMainMenu.helen");
    Assert.Contains(sceneAsset.RootEntities.SelectMany(entity => FlattenComponents(entity)),
        record => string.Equals(record.ComponentTypeId, "city.menu.MenuComponent, gameplay", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run the city menu regeneration command**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --editor-command menu.regenerate-demo-disc-main-menu 2>&1 | Select-Object -Last 160 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
Generated DemoDiscMainMenu.helen
Generated DemoDiscMainMenuDs.helen
```

- [ ] **Step 3: Run the focused editor tests and confirm the regenerated scenes use `city.*` ids**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~CityRenderingSceneAuthoringTests|FullyQualifiedName~EditorBuildQueueItemFactoryTests|FullyQualifiedName~RuntimeSceneLoadServiceTests' 2>&1 | Select-Object -Last 200 | Out-String -Width 240 | Write-Output"
```

Expected:

```text
Passed! city scene authoring and runtime-load tests passed.
```

- [ ] **Step 4: Rebuild the PS2 demo disc and launch PCSX2**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "$env:HELENGINE_ROOT='C:\dev\helworks\helengine'; Get-Process pcsx2-qtx64 -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build ps2 --output 'C:\dev\helprojs\output\ps2-city-demo-disc' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Then:

```powershell
rtk proxy powershell -NoProfile -Command "& { $exe = 'C:\Users\Helena\AppData\Roaming\EmuDeck\Emulators\PCSX2-Qt\pcsx2-qtx64.exe'; $iso = 'C:\dev\helprojs\output\ps2-city-demo-disc\game.iso'; $process = Start-Process -FilePath $exe -ArgumentList @('-portable','-fastboot','-nofullscreen','--', $iso) -PassThru; Start-Sleep -Seconds 3; Get-Process -Id $process.Id | Select-Object Id, ProcessName, MainWindowTitle | Out-String -Width 220 | Write-Output }"
```

Expected:

```text
Build completed for platform 'ps2'
pcsx2-qtx64  HELENGIN.ELF [?]
```

- [ ] **Step 5: Commit the regenerated city scenes and final migration**

```bash
rtk git add C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenu.helen C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenuDs.helen engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs
rtk git commit -m "refactor: finish city menu ownership migration"
```

## Self-Review

### Spec coverage

- Move menu ownership into city: covered by Tasks 2 and 3.
- Remove engine menu special-casing: covered by Task 4.
- Switch serialized identities to `city.*`: covered by Tasks 2 and 5.
- Regenerate scenes and verify packaged behavior: covered by Task 5.
- Preserve baked-scene architecture rather than redesigning UX: reflected in Tasks 2 and 3.

### Placeholder scan

- No `TODO` or `TBD` markers remain.
- Each task names concrete files, commands, and expected outcomes.
- Each validation step is targeted rather than broad.

### Type consistency

- City menu runtime component type ids are consistently `city.menu.*, gameplay` in tests and runtime tasks.
- Regeneration responsibility stays in `city.menu.tools.CityMenuSceneRegenerationService`.
- Engine runtime registry cleanup consistently removes built-in menu deserializers rather than renaming them.
