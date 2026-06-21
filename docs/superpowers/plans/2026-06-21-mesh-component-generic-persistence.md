# Mesh Component Generic Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `MeshComponent` onto the generic reflected persistence/runtime pipeline, remove `Material`, add generic asset-backed array persistence, and delete mesh-specific serializer/deserializer code with no compatibility path.

**Architecture:** Treat mesh like camera, but extend the generic persistence helpers to understand one-dimensional arrays of asset-backed members. `MeshComponent` becomes a plain reflected component with `Model`, `Materials`, and `RenderOrder3D`, while shared packaging rewrites those references generically instead of through one-off mesh branches.

**Tech Stack:** C#/.NET 9, xUnit, helengine editor/core scene persistence, runtime content loading, shared scene packaging

---

### Task 1: Lock Down The New Mesh Contract With Failing Tests

**Files:**
- Create: `engine/helengine.editor.tests/serialization/scene/MeshComponentGenericPersistenceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Delete: `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`

- [ ] **Step 1: Write the failing generic editor-persistence tests**

```csharp
namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies mesh components persist through the generic reflected descriptor.
    /// </summary>
    public sealed class MeshComponentGenericPersistenceTests {
        [Fact]
        public void SerializeAndDeserialize_WhenMeshUsesModelAndMaterialSlots_RoundTripsGenericPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial(), new TestRuntimeMaterial() },
                RenderOrder3D = 7
            };

            SceneAssetReference modelReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Cube.hasset",
                ProviderId = "engine",
                AssetId = "engine:model:cube"
            };
            SceneAssetReference firstMaterialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Materials/Standard.hasset",
                ProviderId = "engine",
                AssetId = "engine:material:standard"
            };
            SceneAssetReference secondMaterialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Materials/Accent.helmat",
                ProviderId = string.Empty,
                AssetId = string.Empty
            };

            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[0]", firstMaterialReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[1]", secondMaterialReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveComponent.GetOrCreateComponentState(meshComponent));
            MeshComponent restored = Assert.IsType<MeshComponent>(descriptor.DeserializeComponent(record, saveComponent, resolver));

            Assert.Same(resolver.ModelToReturn, restored.Model);
            Assert.Equal(2, restored.Materials.Length);
            Assert.Same(resolver.MaterialsToReturn[0], restored.Materials[0]);
            Assert.Same(resolver.MaterialsToReturn[1], restored.Materials[1]);
            Assert.Equal((byte)7, restored.RenderOrder3D);
            Assert.True(saveComponent.TryGetAssetReference(restored, "Materials[0]", out SceneAssetReference restoredReference));
            Assert.Equal("Engine/Materials/Standard.hasset", restoredReference.RelativePath);
        }
    }
}
```

- [ ] **Step 2: Write the failing runtime/generator expectation updates**

```csharp
[Fact]
public void Emit_generated_automatic_runtime_component_deserializers_includes_mesh_when_mesh_is_generic() {
    string generatedCoreRootPath = RunGeneration();
    string registrationSource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeComponentRegistry.cpp"));

    Assert.Contains("GeneratedRuntimeMeshComponentDeserializer", registrationSource, StringComparison.Ordinal);
    Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeMeshComponentDeserializer.cpp")));
    Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeMeshComponentDeserializer.hpp")));
}
```

```csharp
[Fact]
public void Load_WhenSceneContainsGenericMeshPayload_MaterializesTheComponent() {
    SceneComponentAssetRecord record = WriteAutomaticRuntimeComponentRecord(new MeshComponent {
        Model = new TestRuntimeModel(),
        Materials = new RuntimeMaterial[] { new TestRuntimeMaterial(), new TestRuntimeMaterial() },
        RenderOrder3D = 4
    });

    MeshComponent meshComponent = LoadSingleComponent<MeshComponent>(record);

    Assert.Equal((byte)4, meshComponent.RenderOrder3D);
    Assert.Equal(2, meshComponent.Materials.Length);
}
```

- [ ] **Step 3: Run the focused tests to verify the old mesh-specific contract fails**

```bash
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter \"FullyQualifiedName~MeshComponentGenericPersistenceTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~SceneSaveServiceTests\" -v minimal"
```

Expected: FAIL with missing generic mesh persistence support, old explicit runtime-deserializer expectations, and/or compile failures once mesh API cleanup starts.

- [ ] **Step 4: Remove the obsolete mesh-descriptor-only test file from the tree**

```text
Delete:
- engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs
```

- [ ] **Step 5: Commit the red test baseline**

```bash
git add engine/helengine.editor.tests/serialization/scene/MeshComponentGenericPersistenceTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git rm engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs
git commit -m "test: lock down mesh generic persistence behavior"
```

### Task 2: Remove `Material` And Make `MeshComponent` A Plain Reflected Shape

**Files:**
- Modify: `engine/helengine.core/components/3d/MeshComponent.cs`
- Modify: `engine/helengine.core/model/interfaces/IDrawable3D.cs`
- Modify: `engine/helengine.vulkan/VulkanRenderer3D.cs`
- Modify: `engine/helengine.render.validation/RenderValidationRunner.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/EditorViewportGridFactoryTests.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs`
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`

- [ ] **Step 1: Write one failing component test for array ownership instead of slot-zero `Material`**

```csharp
[Fact]
public void Materials_WhenAssigned_ClonesTheIncomingArrayAndReleasesThePreviousSlots() {
    MeshComponent meshComponent = new MeshComponent();
    RuntimeMaterial[] first = new RuntimeMaterial[] { new TestRuntimeMaterial() };
    RuntimeMaterial[] second = new RuntimeMaterial[] { new TestRuntimeMaterial(), new TestRuntimeMaterial() };

    meshComponent.Materials = first;
    meshComponent.Materials = second;

    Assert.Equal(2, meshComponent.Materials.Length);
    Assert.NotSame(second, meshComponent.Materials);
}
```

- [ ] **Step 2: Replace `Material` with a writable `Materials` property and remove it from `IDrawable3D`**

```csharp
public interface IDrawable3D {
    Entity Parent { get; }
    byte RenderOrder3D { get; set; }
    RuntimeModel Model { get; }
    RuntimeMaterial[] Materials { get; set; }
}
```

```csharp
public class MeshComponent : Component, IDrawable3D {
    byte renderOrder3D;
    RuntimeMaterial[] MaterialsBySlot;

    public RuntimeModel Model { get; set; }

    public RuntimeMaterial[] Materials {
        get { return MaterialsBySlot; }
        set {
            RuntimeMaterial[] assignedMaterials = value ?? throw new ArgumentNullException(nameof(value));
            RuntimeMaterial[] previousMaterials = MaterialsBySlot;
            MaterialsBySlot = new RuntimeMaterial[assignedMaterials.Length];
            Array.Copy(assignedMaterials, MaterialsBySlot, assignedMaterials.Length);
            NativeOwnership.Release(ref previousMaterials);
        }
    }

    public void SetMaterials(RuntimeMaterial[] runtimeMaterials) {
        Materials = runtimeMaterials;
    }
}
```

- [ ] **Step 3: Update renderers and editor callers to use `Materials` explicitly**

```csharp
static RuntimeMaterial ResolveMaterial(IDrawable3D drawable, int submeshIndex) {
    RuntimeMaterial[] materials = drawable.Materials;
    if (materials == null || materials.Length == 0) {
        return null;
    }
    if (submeshIndex < materials.Length) {
        return materials[submeshIndex];
    }

    return materials[0];
}
```

```csharp
MeshComponent meshComponent = new MeshComponent {
    Model = runtimeModel,
    Materials = new RuntimeMaterial[] { runtimeMaterial }
};
```

- [ ] **Step 4: Run the focused compile-and-behavior checks**

```bash
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter \"FullyQualifiedName~EditorSceneCreationServiceTests|FullyQualifiedName~EditorSessionAddMenuTests|FullyQualifiedName~ComponentPropertiesViewGeneratedAssetTests|FullyQualifiedName~EditorViewportCanvasPlaneFactoryTests|FullyQualifiedName~EditorViewportGridFactoryTests\" -v minimal"
```

Expected: PASS for array-based mesh API, or FAIL only where later persistence tasks are still incomplete.

- [ ] **Step 5: Commit the component-shape cleanup**

```bash
git add engine/helengine.core/components/3d/MeshComponent.cs engine/helengine.core/model/interfaces/IDrawable3D.cs engine/helengine.vulkan/VulkanRenderer3D.cs engine/helengine.render.validation/RenderValidationRunner.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor.tests/EditorSessionAddMenuTests.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/managers/scene/EditorViewportGridFactoryTests.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs
git commit -m "refactor: remove mesh slot-zero material api"
```

### Task 3: Extend Generic Persistence For Asset-Backed Arrays

**Files:**
- Modify: `engine/helengine.core/scene/runtime/AutomaticComponentAssetReferenceSupport.cs`
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Modify: `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/MeshComponentGenericPersistenceTests.cs`

- [ ] **Step 1: Write the failing save-state key and asset-array helper expectations**

```csharp
[Fact]
public void BuildIndexedReferenceName_WhenMemberAndIndexAreProvided_ReturnsBracketedKey() {
    Assert.Equal("Materials[0]", AutomaticComponentAssetReferenceSupport.BuildIndexedReferenceName("Materials", 0));
    Assert.Equal("Materials[2]", AutomaticComponentAssetReferenceSupport.BuildIndexedReferenceName("Materials", 2));
}
```

- [ ] **Step 2: Add shared asset-array helper methods**

```csharp
public static bool IsSupportedAssetReferenceArrayType(Type valueType) {
    if (valueType == null || !valueType.IsArray || valueType.GetArrayRank() != 1) {
        return false;
    }

    Type elementType = valueType.GetElementType();
    return IsSupportedAssetReferenceType(elementType);
}

public static string BuildIndexedReferenceName(string memberName, int index) {
    if (string.IsNullOrWhiteSpace(memberName)) {
        throw new ArgumentException("Member name must be provided.", nameof(memberName));
    } else if (index < 0) {
        throw new ArgumentOutOfRangeException(nameof(index), "Asset reference array index must be non-negative.");
    }

    return string.Concat(memberName, "[", index.ToString(), "]");
}
```

- [ ] **Step 3: Teach the automatic editor descriptor to serialize and deserialize asset-backed arrays**

```csharp
internal static void WriteSupportedMemberValue(
    EngineBinaryWriter writer,
    ScriptComponentReflectionMember member,
    Component component,
    EntityComponentSaveState saveState) {
    if (TryWriteAssetReferenceBackedArrayMemberValue(writer, member, component, saveState)) {
        return;
    }
    if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(member.ValueType)) {
        WriteAssetReferenceBackedMemberValue(writer, member, component, saveState);
        return;
    }

    WriteSupportedValue(writer, member.ValueType, member.GetValue(component));
}
```

```csharp
static bool TryWriteAssetReferenceBackedArrayMemberValue(
    EngineBinaryWriter writer,
    ScriptComponentReflectionMember member,
    Component component,
    EntityComponentSaveState saveState) {
    if (!AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(member.ValueType)) {
        return false;
    }

    Array runtimeValues = member.GetValue(component) as Array ?? Array.Empty<object>();
    SceneAssetReference[] references = new SceneAssetReference[runtimeValues.Length];
    for (int index = 0; index < runtimeValues.Length; index++) {
        object runtimeValue = runtimeValues.GetValue(index);
        if (runtimeValue == null) {
            continue;
        }

        string referenceName = AutomaticComponentAssetReferenceSupport.BuildIndexedReferenceName(member.Name, index);
        if (saveState == null || !saveState.TryGetAssetReference(referenceName, out SceneAssetReference reference)) {
            throw new InvalidOperationException($"Component member '{member.Name}' slot {index} is assigned but has no stored scene asset reference.");
        }

        references[index] = reference;
    }

    SceneComponentBinaryFieldEncoding.WriteOptionalReferenceArray(writer, references);
    return true;
}
```

- [ ] **Step 4: Mirror the same support in the runtime generic deserializer**

```csharp
static object ReadSupportedValue(EngineBinaryReader reader, Type valueType, RuntimeSceneAssetReferenceResolver referenceResolver) {
    if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(valueType)) {
        SceneAssetReference[] references = SceneComponentBinaryFieldEncoding.ReadOptionalReferenceArray(reader);
        Type elementType = valueType.GetElementType() ?? throw new InvalidOperationException("Asset-backed array element type is required.");
        Array runtimeValues = Array.CreateInstance(elementType, references.Length);
        for (int index = 0; index < references.Length; index++) {
            runtimeValues.SetValue(
                AutomaticComponentAssetReferenceSupport.ResolveRuntimeAssetReference(elementType, references[index], referenceResolver),
                index);
        }

        return runtimeValues;
    }

    if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(valueType)) {
        SceneAssetReference reference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
        return AutomaticComponentAssetReferenceSupport.ResolveRuntimeAssetReference(valueType, reference, referenceResolver);
    }

    // existing generic value handling follows
}
```

- [ ] **Step 5: Run the focused generic persistence suite and commit**

```bash
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter \"FullyQualifiedName~MeshComponentGenericPersistenceTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests\" -v minimal"
git add engine/helengine.core/scene/runtime/AutomaticComponentAssetReferenceSupport.cs engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs engine/helengine.editor.tests/serialization/scene/MeshComponentGenericPersistenceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "feat: add generic asset-backed array persistence"
```

Expected: PASS for generic mesh save/load tests, or only remaining failures from explicit mesh wiring that Task 4 removes.

### Task 4: Remove Mesh-Specific Descriptor And Runtime Wiring

**Files:**
- Delete: `engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Update the runtime-regeneration and registry expectations to require mesh generic handling**

```csharp
Assert.Contains("GeneratedRuntimeMeshComponentDeserializer", registrationSource, StringComparison.Ordinal);
Assert.True(File.Exists(Path.Combine(generatedCoreRootPath, "GeneratedRuntimeMeshComponentDeserializer.cpp")));
```

```csharp
// remove:
registry.Register(new RuntimeMeshComponentDeserializer());

// keep type-id normalization mapping only if still needed for old engine namespaces:
case "helengine.MeshComponent, helengine.core":
    return "helengine.MeshComponent";
```

- [ ] **Step 2: Remove the explicit editor descriptor registrations**

```csharp
// remove from EditorSession.CreatePersistenceRegistry():
persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());

// remove from ComponentPlatformEditingService.CreatePersistenceRegistry():
persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
```

- [ ] **Step 3: Rewrite mesh-focused tests to use the automatic descriptor/runtime helpers**

```csharp
AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);
MeshComponent restored = Assert.IsType<MeshComponent>(descriptor.DeserializeComponent(record, restoredSaveComponent, resolver));
```

```csharp
SceneComponentAssetRecord record = WriteAutomaticRuntimeComponentRecord(new MeshComponent {
    Model = new TestRuntimeModel(),
    Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() },
    RenderOrder3D = 3
});
```

- [ ] **Step 4: Delete the explicit implementation files and run the focused suite**

```bash
git rm engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs
git rm engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter \"FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests\" -v minimal"
```

Expected: PASS with mesh loading/saving through the generic path and generator tests now expecting mesh output.

- [ ] **Step 5: Commit the mesh-specific persistence/runtime removal**

```bash
git add engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
git commit -m "refactor: move mesh onto generic persistence pipeline"
```

### Task 5: Make Shared Packaging Rewrite Mesh Generically

**Files:**
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs`

- [ ] **Step 1: Write the failing packaging test against generic `Materials[n]` save-state keys**

```csharp
[Fact]
public void TryTransform_WhenGenericMeshUsesMultipleMaterialSlots_RewritesEachSlotReference() {
    SceneComponentAssetRecord record = CreateAutomaticMeshComponentRecord(
        modelRelativePath: "Models/TestModel.hasset",
        materialRelativePaths: new[] { "Materials/First.helmat", "Materials/Second.helmat" });

    SceneComponentAssetRecord transformed = Transform(record);
    MeshComponent transformedMesh = DeserializePackagedMesh(transformed);

    Assert.Equal(2, transformedMesh.Materials.Length);
    Assert.Equal("cooked/materials/first.hasset", ReadMaterialReference(transformed, 0).RelativePath);
    Assert.Equal("cooked/materials/second.hasset", ReadMaterialReference(transformed, 1).RelativePath);
}
```

- [ ] **Step 2: Add generic asset-array rewrite handling to `TryRewriteAutomaticComponentRecord`**

```csharp
if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(member.ValueType)) {
    RewriteAssetReferenceArrayMember(record, rewrittenComponent, rewrittenSaveState, member.Name, member.ValueType, buildRootPath);
    continue;
}
if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(member.ValueType)) {
    RewriteAssetReferenceMember(record, rewrittenComponent, rewrittenSaveState, member.Name, member.ValueType, buildRootPath);
    continue;
}
```

```csharp
void RewriteAssetReferenceArrayMember(
    SceneComponentAssetRecord record,
    Component rewrittenComponent,
    EntityComponentSaveState rewrittenSaveState,
    string memberName,
    Type memberType,
    string buildRootPath) {
    Array runtimeValues = (Array)ReadPropertyValue(rewrittenComponent, memberName);
    Type elementType = memberType.GetElementType() ?? throw new InvalidOperationException("Asset-backed array element type is required.");
    for (int index = 0; index < runtimeValues.Length; index++) {
        string referenceName = AutomaticComponentAssetReferenceSupport.BuildIndexedReferenceName(memberName, index);
        if (!rewrittenSaveState.TryGetAssetReference(referenceName, out SceneAssetReference authoredReference)) {
            continue;
        }

        rewrittenSaveState.SetAssetReference(referenceName, RewriteReferenceForMemberType(elementType, authoredReference, buildRootPath));
    }
}
```

- [ ] **Step 3: Delete the shared mesh-specific packaging branch once the generic path passes**

```csharp
// remove:
const byte MeshComponentPayloadVersion = MeshComponentScenePayloadSerializer.CurrentVersion;
const string MeshComponentTypeId = "helengine.MeshComponent";
SceneComponentAssetRecord RewriteMeshComponentRecord(SceneComponentAssetRecord record, string buildRootPath) { ... }

// remove special-case call site:
if (string.Equals(record.ComponentTypeId, MeshComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
    transformedRecord = RewriteMeshComponentRecord(record, buildRootPath);
    return true;
}
```

- [ ] **Step 4: Run the packaging-focused test sweep**

```bash
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter \"FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests\" -v minimal"
```

Expected: PASS with generic mesh packaging and no shared mesh branch remaining.

- [ ] **Step 5: Commit the packaging transform cleanup**

```bash
git add engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs
git commit -m "refactor: package mesh generically"
```

### Task 6: Rewrite Committed Scene Assets And Run Final Verification

**Files:**
- Modify: `test-project/assets/Scenes/rendering/*.helen`
- Modify: `engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Add one temporary migration utility to rewrite committed mesh scene records**

```csharp
AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());

foreach (string scenePath in Directory.GetFiles(scenesRootPath, "*.helen", SearchOption.AllDirectories)) {
    SceneAsset sceneAsset = LoadScene(scenePath);
    bool changed = RewriteMeshRecords(sceneAsset.RootEntities, descriptor);
    if (!changed) {
        continue;
    }

    SaveScene(scenePath, sceneAsset);
}
```

```csharp
SceneComponentAssetRecord RewriteLegacyMeshRecord(SceneComponentAssetRecord record) {
    MeshComponent meshComponent = ReadLegacyMeshRecord(record);
    return Descriptor.SerializeComponent(meshComponent, record.ComponentIndex, SaveState);
}
```

- [ ] **Step 2: Run the utility and verify the committed rendering scenes change in-place**

```bash
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd run --project C:\dev\helworks\helengine\.codex-temp\mesh-migrate\mesh-migrate.csproj -- C:\dev\helworks\helengine"
rtk proxy git -C C:\dev\helworks\helengine diff -- test-project/assets/Scenes/rendering
```

Expected: only `.helen` payload updates for mesh-bearing scenes.

- [ ] **Step 3: Remove the temporary migration utility and update any catalog/runtime assertions that still assume old mesh payload helpers**

```bash
rtk proxy powershell -NoProfile -Command "Remove-Item -LiteralPath 'C:\dev\helworks\helengine\.codex-temp\mesh-migrate' -Recurse -Force"
```

```csharp
AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
MeshComponent meshComponent = Assert.IsType<MeshComponent>(descriptor.DeserializeComponent(meshRecord, null, resolver));
Assert.Equal(expectedMaterialCount, meshComponent.Materials.Length);
```

- [ ] **Step 4: Run the final focused verification sweep**

```bash
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~MeshComponentGenericPersistenceTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~SceneSaveServiceTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~SceneFileLoadServiceTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~RenderingSceneCatalogTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v minimal"
```

Expected: PASS across the focused mesh persistence, runtime loading, generation, packaging, and committed-scene suites.

- [ ] **Step 5: Commit the migrated assets and final cleanups**

```bash
git add test-project/assets/Scenes/rendering engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs
git commit -m "test: migrate mesh scenes to generic persistence"
```

## Self-Review

- Spec coverage: the plan covers mesh API cleanup, generic asset-backed array support, removal of explicit mesh descriptor/runtime code, shared packaging rewrite, committed scene migration, and focused verification.
- Placeholder scan: no `TODO`, `TBD`, or "handle appropriately" language remains; each task names exact files, code, and commands.
- Type consistency: the plan uses `Model`, `Materials`, `RenderOrder3D`, `BuildIndexedReferenceName`, and `RuntimeMaterial[]` consistently across component, persistence, runtime, packaging, and tests.

Plan complete and saved to `docs/superpowers/plans/2026-06-21-mesh-component-generic-persistence.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
