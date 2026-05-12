# City Platform Info Text Binding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the runtime platform id and builder-stamped version in the bottom-right of the City main menu using serialized scene references to two text components.

**Architecture:** Add a scene-component reference type and a resolver that can round-trip `entityId + componentKey` through scene save/load. Extend the automatic script-component persistence path so script fields can store component references, then use that support in a City `PlatformInfoTextComponent` that resolves two text targets once the generated menu scene loads. The menu generator keeps authoring the scene in one place, and the generated `DemoDiscMainMenu.helen` remains the source of truth.

**Tech Stack:** C#, xUnit, existing scene asset serializer/deserializer, editor scene regeneration service, City gameplay code under `C:\dev\helprojs\city`.

---

### Task 1: Add Scene-Component Reference Serialization And Runtime Resolution

**Files:**
- Create: `engine/helengine.core/assets/raw/scene/SceneComponentReference.cs`
- Create: `engine/helengine.core/scene/runtime/SceneComponentReferenceResolver.cs`
- Modify: `engine/helengine.core/serialization/BinarySerializationExtensions.cs`
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor/EditorCore.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationExtensionsTests.cs`
- Modify: `engine/helengine.editor.tests/CoreTimingTests.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/SceneComponentReferenceResolverTests.cs`

- [ ] **Step 1: Write the failing reference-serialization tests**

Add these checks to `engine/helengine.editor.tests/BinarySerializationExtensionsTests.cs`:

```csharp
[Fact]
public void SceneComponentReference_WhenRoundTripped_PreservesEntityIdAndComponentKey() {
    using MemoryStream stream = new MemoryStream();
    using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);

    writer.WriteSceneComponentReference(new SceneComponentReference {
        EntityId = "platform-info-root",
        ComponentKey = "platform-info-name-text"
    });

    stream.Position = 0;
    using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
    SceneComponentReference reference = reader.ReadSceneComponentReference();

    Assert.Equal("platform-info-root", reference.EntityId);
    Assert.Equal("platform-info-name-text", reference.ComponentKey);
}
```

Add one `CoreTimingTests` assertion that the new resolver service is available after core initialization:

```csharp
[Fact]
public void Initialize_WhenPlatformInfoIsProvided_AlsoCreatesTheSceneComponentReferenceResolver() {
    Core core = new Core();

    core.Initialize(
        new TestRenderManager3D(),
        new TestRenderManager2D(),
        new TestInputBackend(),
        new CoreInitializationOptions(),
        new PlatformInfo("windows", "1"));

    Assert.NotNull(core.SceneComponentReferenceResolver);
}
```

Add a new resolver unit test:

```csharp
[Fact]
public void Resolve_WhenComponentWasRegistered_ReturnsTheExactComponent() {
    SceneComponentReferenceResolver resolver = new SceneComponentReferenceResolver();
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();
    TextComponent text = new TextComponent();
    entity.AddComponent(text);

    resolver.Register(entity, text, "platform-info-name-text");

    SceneComponentReference reference = new SceneComponentReference {
        EntityId = resolver.GetEntityId(entity),
        ComponentKey = "platform-info-name-text"
    };

    Assert.Same(text, resolver.Resolve(reference));
}
```

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationExtensionsTests|FullyQualifiedName~CoreTimingTests|FullyQualifiedName~SceneComponentReferenceResolverTests"
```

Expected: fail because `SceneComponentReference` and the resolver do not exist yet.

- [ ] **Step 3: Implement the reference type and resolver**

Create `engine/helengine.core/assets/raw/scene/SceneComponentReference.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Identifies one component inside a serialized scene by the owning entity id and the component key.
    /// </summary>
    public class SceneComponentReference {
        /// <summary>
        /// Gets or sets the stable id of the entity that owns the target component.
        /// </summary>
        public string EntityId { get; set; }

        /// <summary>
        /// Gets or sets the stable component key assigned during scene serialization.
        /// </summary>
        public string ComponentKey { get; set; }
    }
}
```

Create `engine/helengine.core/scene/runtime/SceneComponentReferenceResolver.cs` as the runtime lookup that:

```csharp
public void Register(Entity entity, Component component, string componentKey)
public string GetEntityId(Entity entity)
public Entity ResolveEntity(string entityId)
public Component Resolve(SceneComponentReference reference)
public void Clear()
```

The resolver should store both entity ids and component keys and throw when a reference cannot be resolved.

Update `engine/helengine.core/serialization/BinarySerializationExtensions.cs` with:

```csharp
public static void WriteSceneComponentReference(this EngineBinaryWriter writer, SceneComponentReference reference) {
    if (writer == null) {
        throw new ArgumentNullException(nameof(writer));
    }

    writer.WriteByte(reference == null ? (byte)0 : (byte)1);
    if (reference == null) {
        return;
    }

    if (string.IsNullOrWhiteSpace(reference.EntityId)) {
        throw new InvalidOperationException("Scene component references must define an entity id.");
    }
    if (string.IsNullOrWhiteSpace(reference.ComponentKey)) {
        throw new InvalidOperationException("Scene component references must define a component key.");
    }

    writer.WriteString(reference.EntityId);
    writer.WriteString(reference.ComponentKey);
}

public static SceneComponentReference ReadSceneComponentReference(this EngineBinaryReader reader) {
    if (reader == null) {
        throw new ArgumentNullException(nameof(reader));
    }

    if (reader.ReadByte() == 0) {
        return null;
    }

    return new SceneComponentReference {
        EntityId = reader.ReadString(),
        ComponentKey = reader.ReadString()
    };
}
```

Wire `Core` and `EditorCore` to create and expose the resolver service, then update `SceneLoadService` and `RuntimeSceneLoadService` so they register every loaded entity and component into that resolver as the scene is materialized.

- [ ] **Step 4: Run the focused tests again and confirm they pass**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationExtensionsTests|FullyQualifiedName~CoreTimingTests|FullyQualifiedName~SceneComponentReferenceResolverTests"
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine/helengine.core/assets/raw/scene/SceneComponentReference.cs engine/helengine.core/scene/runtime/SceneComponentReferenceResolver.cs engine/helengine.core/serialization/BinarySerializationExtensions.cs engine/helengine.core/Core.cs engine/helengine.editor/EditorCore.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs engine/helengine.editor.tests/BinarySerializationExtensionsTests.cs engine/helengine.editor.tests/CoreTimingTests.cs engine/helengine.editor.tests/serialization/scene/SceneComponentReferenceResolverTests.cs
rtk git commit -m "Add scene component reference resolver"
```

### Task 2: Extend Automatic Script Persistence For Component References

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs`
- Create: `engine/helengine.editor.tests/testing/TestPlatformInfoTextComponent.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing persistence tests**

Add a test helper component in `engine/helengine.editor.tests/testing/TestPlatformInfoTextComponent.cs`:

```csharp
namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal script component used to verify automatic serialization of scene component references.
    /// </summary>
    public sealed class TestPlatformInfoTextComponent : Component {
        /// <summary>
        /// Gets or sets the referenced text component that should show the platform name.
        /// </summary>
        public SceneComponentReference NameTextReference { get; set; }

        /// <summary>
        /// Gets or sets the referenced text component that should show the platform version.
        /// </summary>
        public SceneComponentReference VersionTextReference { get; set; }
    }
}
```

Add a persistence regression in `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs`:

```csharp
[Fact]
public void SerializeComponent_WhenScriptComponentContainsTextReferences_WritesEntityAndComponentKeys() {
    EditorEntity root = new EditorEntity();
    root.InitComponents();
    root.InitChildren();

    EntitySaveComponent saveComponent = new EntitySaveComponent();
    root.AddComponent(saveComponent);

    TextComponent nameText = new TextComponent();
    TextComponent versionText = new TextComponent();
    root.AddComponent(nameText);
    root.AddComponent(versionText);
    saveComponent.GetOrCreateComponentState(nameText).ComponentKey = "platform-info-name-text";
    saveComponent.GetOrCreateComponentState(versionText).ComponentKey = "platform-info-version-text";

    TestPlatformInfoTextComponent component = new TestPlatformInfoTextComponent {
        NameTextReference = new SceneComponentReference {
            EntityId = "platform-info-name-entity",
            ComponentKey = "platform-info-name-text"
        },
        VersionTextReference = new SceneComponentReference {
            EntityId = "platform-info-version-entity",
            ComponentKey = "platform-info-version-text"
        }
    };

    SceneComponentAssetRecord record = descriptor.SerializeComponent(component, 0, saveComponent.GetOrCreateComponentState(component));
    SceneComponentReference roundTrippedName = ReadReferenceField(record, "NameTextReference");
    SceneComponentReference roundTrippedVersion = ReadReferenceField(record, "VersionTextReference");

    Assert.Equal("platform-info-name-entity", roundTrippedName.EntityId);
    Assert.Equal("platform-info-name-text", roundTrippedName.ComponentKey);
    Assert.Equal("platform-info-version-entity", roundTrippedVersion.EntityId);
    Assert.Equal("platform-info-version-text", roundTrippedVersion.ComponentKey);
}
```

Add a runtime deserialization regression in `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs` that loads a small scene with the test helper component and asserts the resolver returns the exact `TextComponent` instances after load.

- [ ] **Step 2: Run the persistence tests and confirm they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected: fail because component references are not yet supported.

- [ ] **Step 3: Teach the automatic serializer and generated deserializer about component references**

Update `AutomaticScriptComponentPersistenceDescriptor.cs` so it recognizes `SceneComponentReference` fields beside the existing asset-reference fields and writes them through `BinarySerializationExtensions.WriteSceneComponentReference(...)`.

Update `ScriptComponentPlayerDeserializerGenerator.cs` so generated runtime code reads `SceneComponentReference` fields with `reader.ReadSceneComponentReference()` and resolves them through `Core.Instance.SceneComponentReferenceResolver`.

Update `SceneSaveService.cs`, `SceneLoadService.cs`, and `RuntimeSceneLoadService.cs` so component keys are registered into the shared resolver before any component tries to dereference them.

The load path must fail loudly if a script component contains a component reference that points at a missing entity or missing component key.

- [ ] **Step 4: Run the persistence tests and confirm they pass**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs engine/helengine.editor.tests/testing/TestPlatformInfoTextComponent.cs engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
rtk git commit -m "Persist scene component references in scripts"
```

### Task 3: Add PlatformInfoTextComponent In City And Bake It Into DemoDiscMainMenu

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\menu\PlatformInfoTextComponent.cs`
- Modify: `engine/helengine.editor\managers\menu\DemoMenuSceneAssetFactory.cs`
- Modify: `engine/helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the City component and the generated-scene regression**

Create `C:\dev\helprojs\city\assets\codebase\menu\PlatformInfoTextComponent.cs`:

```csharp
namespace city.menu {
    /// <summary>
    /// Populates the City main-menu platform labels from the runtime platform info that Core exposes.
    /// </summary>
public sealed class PlatformInfoTextComponent : Component {
        /// <summary>
        /// Gets or sets the serialized reference to the text component that shows the platform name.
        /// </summary>
        public SceneComponentReference NameTextReference { get; set; }

        /// <summary>
        /// Gets or sets the serialized reference to the text component that shows the builder version.
        /// </summary>
        public SceneComponentReference VersionTextReference { get; set; }

        /// <summary>
        /// Resolves both text targets once the scene is loaded and writes the platform metadata into them.
        /// </summary>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            ApplyPlatformInfo();
        }

        /// <summary>
        /// Resolves the two target text components and copies the runtime platform metadata into them.
        /// </summary>
        void ApplyPlatformInfo() {
            if (Core.Instance == null || Core.Instance.PlatformInfo == null) {
                throw new InvalidOperationException("Platform info is required before the menu labels can be populated.");
            }

            TextComponent nameText = ResolveTextComponent(NameTextReference);
            TextComponent versionText = ResolveTextComponent(VersionTextReference);
            nameText.Text = Core.Instance.PlatformInfo.Name;
            versionText.Text = Core.Instance.PlatformInfo.Version;
        }

        /// <summary>
        /// Resolves one serialized component reference to the live text component instance it identifies.
        /// </summary>
        /// <param name="reference">Serialized component reference to resolve.</param>
        /// <returns>Resolved live text component.</returns>
        TextComponent ResolveTextComponent(SceneComponentReference reference) {
            if (reference == null) {
                throw new InvalidOperationException("Platform info text references must be populated before the menu can initialize.");
            }

            if (Core.Instance == null || Core.Instance.SceneComponentReferenceResolver == null) {
                throw new InvalidOperationException("Scene component references cannot be resolved before the runtime resolver is initialized.");
            }

            return (TextComponent)Core.Instance.SceneComponentReferenceResolver.Resolve(reference);
        }
    }
}
```

Add a regression to `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs` that proves the generated `DemoDiscMainMenu.helen` contains:

```csharp
SceneEntityAsset menuRoot = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
SceneEntityAsset platformInfoRoot = Assert.Single(menuRoot.Children, entity => entity.Id == "platform-info-root");
SceneComponentAssetRecord platformInfoRecord = Assert.Single(
    platformInfoRoot.Components,
    component => component.ComponentTypeId == "city.menu.PlatformInfoTextComponent, gameplay");
EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(platformInfoRecord.Payload);
```

```csharp
using (reader.TryGetFieldReader("NameTextReference", out EngineBinaryReader nameReader)) {
    SceneComponentReference nameTextReference = nameReader.ReadSceneComponentReference();
    Assert.Equal("platform-info-name-entity", nameTextReference.EntityId);
    Assert.Equal("platform-info-name-text", nameTextReference.ComponentKey);
}

using (reader.TryGetFieldReader("VersionTextReference", out EngineBinaryReader versionReader)) {
    SceneComponentReference versionTextReference = versionReader.ReadSceneComponentReference();
    Assert.Equal("platform-info-version-entity", versionTextReference.EntityId);
    Assert.Equal("platform-info-version-text", versionTextReference.ComponentKey);
}
```

The test should deserialize the `PlatformInfoTextComponent` payload and assert:

```csharp
Assert.Equal("platform-info-name-entity", nameTextReference.EntityId);
Assert.Equal("platform-info-name-text", nameTextReference.ComponentKey);
Assert.Equal("platform-info-version-entity", versionTextReference.EntityId);
Assert.Equal("platform-info-version-text", versionTextReference.ComponentKey);
```

- [ ] **Step 2: Update the menu scene factory to author the labels and references**

Modify `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs` so the generated menu scene creates one bottom-right container entity with two text children:

```csharp
SceneEntityAsset BuildPlatformInfoEntityAsset(string platformInfoFontPath) {
    SceneEntityAsset platformInfoNameEntity = BuildTextEntityAsset(
        "platform-info-name-entity",
        new float3(0f, 0f, 0.1f),
        string.Empty,
        platformInfoFontPath,
        definition.TextColor,
        new int2(220, 24),
        36);
    SceneEntityAsset platformInfoVersionEntity = BuildTextEntityAsset(
        "platform-info-version-entity",
        new float3(0f, 24f, 0.1f),
        string.Empty,
        platformInfoFontPath,
        definition.MutedTextColor,
        new int2(220, 24),
        35);

    PlatformInfoTextComponent platformInfoComponent = new PlatformInfoTextComponent {
        NameTextReference = new SceneComponentReference {
            EntityId = "platform-info-name-entity",
            ComponentKey = "platform-info-name-text"
        },
        VersionTextReference = new SceneComponentReference {
            EntityId = "platform-info-version-entity",
            ComponentKey = "platform-info-version-text"
        }
    };

    SceneComponentAssetRecord platformInfoRecord = AutomaticDescriptor.SerializeComponent(platformInfoComponent, 0, null);
    return new SceneEntityAsset {
        Id = "platform-info-root",
        Name = "PlatformInfoRoot",
        LocalPosition = float3.Zero,
        LocalScale = float3.One,
        LocalOrientation = float4.Identity,
        Components = new[] { platformInfoRecord },
        Children = new[] { platformInfoNameEntity, platformInfoVersionEntity }
    };
}
```

Give the two text component records stable component keys so the references resolve by `entityId + componentKey`, and place the platform-info entity after the other menu shell entities so the loader can resolve the text targets without a polling pass.

- [ ] **Step 3: Run the scene-writer regression and confirm it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DemoDiscSceneWriterTests
```

Expected: fail because the generated menu scene does not yet contain the new platform labels.

- [ ] **Step 4: Regenerate the menu scene and confirm the regression passes**

Run the City menu regeneration command through the existing editor regeneration flow, then re-run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DemoDiscSceneWriterTests
```

Expected: pass, with `C:\dev\helprojs\city\assets\scenes\DemoDiscMainMenu.helen` containing the new bottom-right labels and their serialized references.

- [ ] **Step 5: Commit**

```powershell
rtk git add C:\dev\helprojs\city\assets\codebase\menu\PlatformInfoTextComponent.cs engine/helengine.editor\managers\menu\DemoMenuSceneAssetFactory.cs engine/helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs C:\dev\helprojs\city\assets\scenes\DemoDiscMainMenu.helen
rtk git commit -m "Add city platform info menu labels"
```

### Task 4: Full Verification And Cleanup

**Files:**
- No new files; verify the changed engine and City files from Tasks 1-3.

- [ ] **Step 1: Run the narrow verification bundle**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationExtensionsTests|FullyQualifiedName~CoreTimingTests|FullyQualifiedName~SceneComponentReferenceResolverTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~DemoDiscSceneWriterTests"
```

Expected: pass.

- [ ] **Step 2: Run the full editor test suite**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj
```

Expected: pass.

- [ ] **Step 3: Verify the City menu output**

Check that `C:\dev\helprojs\city\assets\scenes\DemoDiscMainMenu.helen` contains one platform-info container entity, two text children, and the serialized references that point at the two text components by stable entity id and component key.

- [ ] **Step 4: Final commit review**

Confirm the engine repo commit contains only the serialization/runtime plumbing and tests, and the City repo commit contains only the menu component, generator, and regenerated scene.
