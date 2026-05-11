# OBJ MTL Submesh Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add first-class submesh and per-submesh material-slot support across the engine, then extend OBJ import so `.obj` plus `.mtl` sources preserve multiple materials and generate deterministic `.helmat` material assets.

**Architecture:** The implementation proceeds bottom-up. First add submesh metadata to `ModelAsset` and runtime model resources, then update `MeshComponent` and scene persistence to store material slots, then update the renderers to issue one draw per submesh, and finally extend the Assimp/asset-import pipeline so OBJ import returns a richer result that includes generated material definitions from `.mtl` sources.

**Tech Stack:** C#/.NET 9, xUnit, existing HELE asset serialization, AssimpNet-backed model import, DirectX11 renderer, Vulkan renderer, editor asset import and packaging services.

---

## File Structure

### New Files

- `engine/helengine.core/assets/raw/ModelSubmeshAsset.cs`
  Raw serialized submesh metadata stored on `ModelAsset`.
- `engine/helengine.core/assets/RuntimeSubmesh.cs`
  Runtime submesh metadata exposed by uploaded model resources.
- `engine/helengine.editor/content/model/ImportedModelAssetSet.cs`
  Rich importer result containing the imported `ModelAsset` and generated material definitions.
- `engine/helengine.editor/content/model/ImportedModelMaterialAsset.cs`
  One generated `.helmat` payload plus deterministic path/id metadata derived from `.mtl`.

### Modified Files

- `engine/helengine.core/assets/raw/ModelAsset.cs`
  Add serialized submesh metadata.
- `engine/helengine.core/assets/RuntimeModel.cs`
  Expose submesh metadata to renderers and scene consumers.
- `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
  Persist `ModelAsset.Submeshes` with a version bump.
- `engine/helengine.directx11/assets/DirectX11ModelResource.cs`
  Store uploaded runtime submeshes.
- `engine/helengine.vulkan/VulkanModelResource.cs`
  Store uploaded runtime submeshes.
- `engine/helengine.core/components/3d/MeshComponent.cs`
  Replace single `Material` binding with ordered material slots.
- `engine/helengine.core/model/interfaces/IDrawable3D.cs`
  Expose the slot-based material binding contract.
- `engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs`
  Version mesh payloads forward and deserialize material slot references.
- `engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs`
  Persist one model reference and an ordered list of material references.
- `engine/helengine.directx11/DirectX11Renderer3D.cs`
  Upload and draw submeshes one-by-one with slot materials.
- `engine/helengine.vulkan/VulkanRenderer3D.cs`
  Upload and draw submeshes one-by-one with slot materials.
- `engine/helengine.editor/content/model/IModelImporter.cs`
  Return `ImportedModelAssetSet` instead of bare `ModelAsset`.
- `engine/helengine.editor/content/model/ModelImporterContentProcessor.cs`
  Adapt the new importer result contract.
- `engine/helengine.editor/content/model/LazyModelImporter.cs`
  Forward the new importer result contract.
- `engine/helengine.editor.fbximporter/HelengineAssimpImporter.cs`
  Resolve `.obj` plus `.mtl` through Assimp and return generated material definitions.
- `engine/helengine.editor.fbximporter/AssimpSceneModelAssetConverter.cs`
  Preserve imported mesh/material boundaries as ordered submeshes.
- `engine/helengine.editor/managers/asset/AssetImportManager.cs`
  Write cached imported models, generate/update sibling `.helmat` assets, and load slot-aware model results.
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
  Rewrite and include every slot material reference used by mesh components.
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
  Rewrite slot material references in scene payloads during packaging.

### Test Files

- `engine/helengine.editor.tests/BinarySerializationTests.cs`
- `engine/helengine.editor.tests/ModelAssetIndexDataTests.cs`
- `engine/helengine.editor.tests/AssimpModelImporterTests.cs`
- `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`
- `engine/helengine.editor.tests/testing/TestModelImporter.cs`
- `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

## Task 1: Add Serialized and Runtime Submesh Metadata

**Files:**
- Create: `engine/helengine.core/assets/raw/ModelSubmeshAsset.cs`
- Create: `engine/helengine.core/assets/RuntimeSubmesh.cs`
- Modify: `engine/helengine.core/assets/raw/ModelAsset.cs`
- Modify: `engine/helengine.core/assets/RuntimeModel.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/ModelAssetIndexDataTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing submesh serialization tests**

```csharp
[Fact]
public void AssetSerializer_ModelAsset_round_trips_submeshes() {
    ModelAsset modelAsset = new ModelAsset {
        Id = "Models/Sponza.obj",
        Positions = new[] { new float3(0f, 0f, 0f) },
        Normals = new[] { new float3(0f, 1f, 0f) },
        TexCoords = new[] { new float2(0f, 0f) },
        Indices16 = new ushort[] { 0 },
        Submeshes = new[] {
            new ModelSubmeshAsset(0, 1, 0, "Fabric")
        }
    };

    using MemoryStream stream = new MemoryStream();
    AssetSerializer.Serialize(stream, modelAsset);
    stream.Position = 0;

    ModelAsset roundTripped = Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));

    Assert.Single(roundTripped.Submeshes);
    Assert.Equal("Fabric", roundTripped.Submeshes[0].MaterialSlotName);
    Assert.Equal(0, roundTripped.Submeshes[0].IndexStart);
    Assert.Equal(1, roundTripped.Submeshes[0].IndexCount);
}
```

```csharp
[Fact]
public void Resolve_WhenModelHasNoExplicitSubmeshes_ReturnsOneImplicitWholeMeshSubmesh() {
    ModelAsset modelAsset = new ModelAsset {
        Positions = new[] {
            new float3(0f, 0f, 0f),
            new float3(1f, 0f, 0f),
            new float3(0f, 1f, 0f)
        },
        Normals = new[] {
            new float3(0f, 0f, 1f),
            new float3(0f, 0f, 1f),
            new float3(0f, 0f, 1f)
        },
        TexCoords = new[] {
            new float2(0f, 0f),
            new float2(1f, 0f),
            new float2(0f, 1f)
        },
        Indices16 = new ushort[] { 0, 1, 2 }
    };

    RuntimeSubmesh[] submeshes = RuntimeModel.BuildRuntimeSubmeshes(modelAsset);

    Assert.Single(submeshes);
    Assert.Equal(0, submeshes[0].IndexStart);
    Assert.Equal(3, submeshes[0].IndexCount);
    Assert.Equal(0, submeshes[0].MaterialSlotIndex);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~ModelAssetIndexDataTests"
```

Expected:
- FAIL because `ModelAsset` has no `Submeshes`
- FAIL because `RuntimeModel.BuildRuntimeSubmeshes` does not exist

- [ ] **Step 3: Implement raw and runtime submesh metadata**

```csharp
public sealed class ModelSubmeshAsset {
    public ModelSubmeshAsset(int indexStart, int indexCount, int materialSlotIndex, string materialSlotName) {
        if (indexStart < 0) {
            throw new ArgumentOutOfRangeException(nameof(indexStart));
        } else if (indexCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(indexCount));
        } else if (materialSlotIndex < 0) {
            throw new ArgumentOutOfRangeException(nameof(materialSlotIndex));
        } else if (string.IsNullOrWhiteSpace(materialSlotName)) {
            throw new ArgumentException("Material slot name must be provided.", nameof(materialSlotName));
        }

        IndexStart = indexStart;
        IndexCount = indexCount;
        MaterialSlotIndex = materialSlotIndex;
        MaterialSlotName = materialSlotName;
    }

    public int IndexStart { get; }
    public int IndexCount { get; }
    public int MaterialSlotIndex { get; }
    public string MaterialSlotName { get; }
}
```

```csharp
public sealed class RuntimeSubmesh {
    public RuntimeSubmesh(int indexStart, int indexCount, int materialSlotIndex, string materialSlotName) {
        IndexStart = indexStart;
        IndexCount = indexCount;
        MaterialSlotIndex = materialSlotIndex;
        MaterialSlotName = materialSlotName;
    }

    public int IndexStart { get; }
    public int IndexCount { get; }
    public int MaterialSlotIndex { get; }
    public string MaterialSlotName { get; }
}
```

```csharp
public class ModelAsset : Asset {
    public float3[] Positions;
    public float3[] Normals;
    public float2[] TexCoords;
    public ushort[] Indices16;
    public uint[] Indices32;
    public ModelSubmeshAsset[] Submeshes = Array.Empty<ModelSubmeshAsset>();
}
```

```csharp
public abstract class RuntimeModel : RuntimeData {
    public RuntimeSubmesh[] Submeshes { get; protected set; } = Array.Empty<RuntimeSubmesh>();

    public static RuntimeSubmesh[] BuildRuntimeSubmeshes(ModelAsset modelAsset) {
        if (modelAsset == null) {
            throw new ArgumentNullException(nameof(modelAsset));
        }

        int indexCount = ModelAssetIndexData.Resolve(modelAsset).IndexCount;
        if (modelAsset.Submeshes != null && modelAsset.Submeshes.Length > 0) {
            return modelAsset.Submeshes
                .Select(value => new RuntimeSubmesh(value.IndexStart, value.IndexCount, value.MaterialSlotIndex, value.MaterialSlotName))
                .ToArray();
        }

        return new[] { new RuntimeSubmesh(0, indexCount, 0, "Default") };
    }
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~ModelAssetIndexDataTests"
```

Expected:
- PASS for submesh serialization and implicit-submesh coverage

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/assets/raw/ModelSubmeshAsset.cs engine/helengine.core/assets/RuntimeSubmesh.cs engine/helengine.core/assets/raw/ModelAsset.cs engine/helengine.core/assets/RuntimeModel.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/ModelAssetIndexDataTests.cs
git commit -m "feat: add submesh metadata to model assets"
```

## Task 2: Upgrade MeshComponent and Scene Persistence to Material Slots

**Files:**
- Modify: `engine/helengine.core/components/3d/MeshComponent.cs`
- Modify: `engine/helengine.core/model/interfaces/IDrawable3D.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs`
- Modify: `engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing slot-persistence tests**

```csharp
[Fact]
public void SerializeComponent_round_trips_material_slot_references() {
    MeshComponent meshComponent = new MeshComponent();
    meshComponent.Materials = new RuntimeMaterial[] {
        new TestRuntimeMaterial(),
        new TestRuntimeMaterial()
    };

    EntityComponentSaveState saveState = new EntityComponentSaveState();
    SceneAssetReference firstReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Materials/Fabric.helmat"
    };
    SceneAssetReference secondReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Materials/Wood.helmat"
    };
    saveState.SetAssetReference("Material:0", firstReference);
    saveState.SetAssetReference("Material:1", secondReference);

    MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
    SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);

    Assert.NotNull(record.Payload);
    Assert.NotEmpty(record.Payload);
}
```

```csharp
[Fact]
public void DeserializeComponent_whenReadingLegacyPayload_maps_single_material_to_slot_zero() {
    byte[] payload = WriteLegacyMeshPayload(
        CreateReference("Models/Sponza.obj"),
        CreateReference("Materials/Legacy.helmat"),
        0);
    RuntimeSceneAssetReferenceResolver resolver = CreateResolver();

    MeshComponent meshComponent = Assert.IsType<MeshComponent>(
        new RuntimeMeshComponentDeserializer().Deserialize(
            new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.MeshComponent",
                Payload = payload
            },
            resolver));

    Assert.Single(meshComponent.Materials);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MeshComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~SceneFileLoadServiceTests"
```

Expected:
- FAIL because `MeshComponent` has no slot collection
- FAIL because persistence still writes one material reference

- [ ] **Step 3: Implement slot-based mesh persistence with single-material compatibility**

```csharp
public class MeshComponent : Component, IDrawable3D {
    RuntimeMaterial[] materials = Array.Empty<RuntimeMaterial>();

    public RuntimeModel Model { get; set; }
    public RuntimeMaterial[] Materials {
        get { return materials; }
        set { materials = value ?? Array.Empty<RuntimeMaterial>(); }
    }
    public byte RenderOrder3D { get; set; }

    public RuntimeMaterial GetMaterial(int submeshIndex) {
        if (submeshIndex < 0 || submeshIndex >= materials.Length) {
            return null;
        }

        return materials[submeshIndex];
    }
}
```

```csharp
public interface IDrawable3D {
    Entity Parent { get; }
    byte RenderOrder3D { get; set; }
    RuntimeModel Model { get; }
    RuntimeMaterial[] Materials { get; }
}
```

```csharp
const byte CurrentVersion = 2;
const string MaterialReferencesFieldName = "MaterialReferences";

writer.WriteField(
    MaterialReferencesFieldName,
    fieldWriter => {
        fieldWriter.WriteInt32(materialReferences.Length);
        for (int index = 0; index < materialReferences.Length; index++) {
            SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, materialReferences[index]);
        }
    });
```

```csharp
if (version == 1) {
    SceneAssetReference singleMaterialReference = ReadOptionalReference(reader);
    meshComponent.Materials = singleMaterialReference == null
        ? Array.Empty<RuntimeMaterial>()
        : new[] { referenceResolver.ResolveMaterial(singleMaterialReference) };
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MeshComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~SceneFileLoadServiceTests"
```

Expected:
- PASS for slot-array save/load behavior
- PASS for single-material payload compatibility

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/3d/MeshComponent.cs engine/helengine.core/model/interfaces/IDrawable3D.cs engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs
git commit -m "feat: persist mesh material slots"
```

## Task 3: Upload and Render Runtime Submeshes

**Files:**
- Modify: `engine/helengine.directx11/assets/DirectX11ModelResource.cs`
- Modify: `engine/helengine.vulkan/VulkanModelResource.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.vulkan/VulkanRenderer3D.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing renderer-facing tests**

```csharp
[Fact]
public void BuildModelFromRaw_preserves_runtime_submeshes_on_uploaded_model() {
    ModelAsset modelAsset = new ModelAsset {
        Positions = new[] {
            new float3(0f, 0f, 0f),
            new float3(1f, 0f, 0f),
            new float3(0f, 1f, 0f),
            new float3(1f, 1f, 0f)
        },
        Normals = new[] {
            new float3(0f, 0f, 1f),
            new float3(0f, 0f, 1f),
            new float3(0f, 0f, 1f),
            new float3(0f, 0f, 1f)
        },
        TexCoords = new[] {
            new float2(0f, 0f),
            new float2(1f, 0f),
            new float2(0f, 1f),
            new float2(1f, 1f)
        },
        Indices16 = new ushort[] { 0, 1, 2, 1, 3, 2 },
        Submeshes = new[] {
            new ModelSubmeshAsset(0, 3, 0, "Fabric"),
            new ModelSubmeshAsset(3, 3, 1, "Wood")
        }
    };

    RuntimeModel runtimeModel = new TestRenderManager3D().BuildModelFromRaw(modelAsset);

    Assert.Equal(2, runtimeModel.Submeshes.Length);
    Assert.Equal(1, runtimeModel.Submeshes[1].MaterialSlotIndex);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected:
- FAIL because uploaded runtime models do not preserve submesh metadata

- [ ] **Step 3: Implement runtime submesh upload and per-submesh draw iteration**

```csharp
public class DirectX11ModelResource : RuntimeModel {
    public Buffer VertexBuffer { get; internal set; }
    public Buffer IndexBuffer { get; internal set; }
    public int VertexCount { get; internal set; }
    public int IndexCount { get; internal set; }
    public bool Uses32BitIndices { get; internal set; }
}
```

```csharp
public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
    DirectX11ModelResource model = new DirectX11ModelResource();
    // existing vertex/index upload
    model.Submeshes = RuntimeModel.BuildRuntimeSubmeshes(data);
    return model;
}
```

```csharp
for (int submeshIndex = 0; submeshIndex < model.Submeshes.Length; submeshIndex++) {
    RuntimeSubmesh submesh = model.Submeshes[submeshIndex];
    RuntimeMaterial runtimeMaterial = drawable.Materials.Length > submesh.MaterialSlotIndex
        ? drawable.Materials[submesh.MaterialSlotIndex]
        : MissingMaterial;
    DrawIndexedSubmesh(model, submesh, runtimeMaterial, drawable);
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected:
- PASS for runtime submesh preservation coverage

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.directx11/assets/DirectX11ModelResource.cs engine/helengine.vulkan/VulkanModelResource.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.vulkan/VulkanRenderer3D.cs engine/helengine.editor.tests/testing/TestRenderManager3D.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "feat: render models per submesh"
```

## Task 4: Change the Model Importer Contract to Return Materials and Submeshes

**Files:**
- Create: `engine/helengine.editor/content/model/ImportedModelAssetSet.cs`
- Create: `engine/helengine.editor/content/model/ImportedModelMaterialAsset.cs`
- Modify: `engine/helengine.editor/content/model/IModelImporter.cs`
- Modify: `engine/helengine.editor/content/model/ModelImporterContentProcessor.cs`
- Modify: `engine/helengine.editor/content/model/LazyModelImporter.cs`
- Modify: `engine/helengine.editor.tests/testing/TestModelImporter.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing importer-contract tests**

```csharp
[Fact]
public void ImportModel_returns_model_and_generated_material_definitions() {
    TestModelImporter importer = new TestModelImporter();

    using MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3 });
    ImportedModelAssetSet result = importer.ImportModel(stream);

    Assert.NotNull(result.ModelAsset);
    Assert.NotNull(result.GeneratedMaterials);
}
```

```csharp
[Fact]
public void ImportModel_with_multiple_submeshes_preserves_material_slot_names() {
    TestModelImporter importer = new TestModelImporter();

    using MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3 });
    ImportedModelAssetSet result = importer.ImportModel(stream);

    Assert.Equal("Default", result.ModelAsset.Submeshes[0].MaterialSlotName);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests"
```

Expected:
- FAIL because `IModelImporter.ImportModel` still returns `ModelAsset`

- [ ] **Step 3: Implement the richer importer result contract**

```csharp
public sealed class ImportedModelAssetSet {
    public ImportedModelAssetSet(ModelAsset modelAsset, ImportedModelMaterialAsset[] generatedMaterials) {
        ModelAsset = modelAsset ?? throw new ArgumentNullException(nameof(modelAsset));
        GeneratedMaterials = generatedMaterials ?? Array.Empty<ImportedModelMaterialAsset>();
    }

    public ModelAsset ModelAsset { get; }
    public ImportedModelMaterialAsset[] GeneratedMaterials { get; }
}
```

```csharp
public sealed class ImportedModelMaterialAsset {
    public ImportedModelMaterialAsset(string materialName, string relativeMaterialPath, MaterialAsset materialAsset) {
        MaterialName = materialName;
        RelativeMaterialPath = relativeMaterialPath;
        MaterialAsset = materialAsset;
    }

    public string MaterialName { get; }
    public string RelativeMaterialPath { get; }
    public MaterialAsset MaterialAsset { get; }
}
```

```csharp
public interface IModelImporter {
    ImportedModelAssetSet ImportModel(Stream stream);
}
```

```csharp
public class ModelImporterContentProcessor : IContentProcessor<ImportedModelAssetSet> {
    public Type OutputType => typeof(ImportedModelAssetSet);

    public ImportedModelAssetSet Read(Stream stream) {
        return Importer.ImportModel(stream);
    }
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests"
```

Expected:
- PASS for importer result contract coverage

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/content/model/ImportedModelAssetSet.cs engine/helengine.editor/content/model/ImportedModelMaterialAsset.cs engine/helengine.editor/content/model/IModelImporter.cs engine/helengine.editor/content/model/ModelImporterContentProcessor.cs engine/helengine.editor/content/model/LazyModelImporter.cs engine/helengine.editor.tests/testing/TestModelImporter.cs engine/helengine.editor.tests/AssetImportManagerModelTests.cs
git commit -m "refactor: return material-aware model import results"
```

## Task 5: Preserve OBJ Submeshes and Generate .helmat Assets from .mtl

**Files:**
- Modify: `engine/helengine.editor.fbximporter/HelengineAssimpImporter.cs`
- Modify: `engine/helengine.editor.fbximporter/AssimpSceneModelAssetConverter.cs`
- Modify: `engine/helengine.editor.tests/AssimpModelImporterTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing OBJ/MTL importer tests**

```csharp
[Fact]
public void ImportModel_whenObjUsesTwoMaterials_returns_two_submeshes_and_two_generated_materials() {
    string objPath = WriteObjFixtureWithMtl("sponza.obj");
    HelengineAssimpImporter importer = new HelengineAssimpImporter();

    using FileStream stream = new FileStream(objPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    ImportedModelAssetSet result = importer.ImportModel(stream);

    Assert.Equal(2, result.ModelAsset.Submeshes.Length);
    Assert.Equal("Fabric", result.ModelAsset.Submeshes[0].MaterialSlotName);
    Assert.Equal("Wood", result.ModelAsset.Submeshes[1].MaterialSlotName);
    Assert.Equal(2, result.GeneratedMaterials.Length);
}
```

```csharp
[Fact]
public void ImportModel_whenMtlDefinesMapKd_sets_generated_material_diffuse_texture_asset_id() {
    string objPath = WriteObjFixtureWithMtl("textured.obj");
    HelengineAssimpImporter importer = new HelengineAssimpImporter();

    using FileStream stream = new FileStream(objPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    ImportedModelAssetSet result = importer.ImportModel(stream);

    ImportedModelMaterialAsset material = Assert.Single(result.GeneratedMaterials, value => value.MaterialName == "Fabric");
    Assert.Equal("Textures/Fabric.png", material.MaterialAsset.DiffuseTextureAssetId);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssimpModelImporterTests"
```

Expected:
- FAIL because the Assimp importer still flattens meshes and returns no generated materials

- [ ] **Step 3: Implement slot-preserving OBJ/MTL import**

```csharp
public ImportedModelAssetSet ImportModel(Stream stream) {
    string formatHint = ResolveFormatHint(stream);
    using AssimpContext importer = new AssimpContext();
    Scene scene = importer.ImportFileFromStream(stream, ImportPostProcessSteps, formatHint);
    ModelAsset modelAsset = SceneConverter.Convert(scene);
    ImportedModelMaterialAsset[] generatedMaterials = BuildGeneratedMaterials(scene);
    return new ImportedModelAssetSet(modelAsset, generatedMaterials);
}
```

```csharp
public ModelAsset Convert(Scene scene) {
    List<ModelSubmeshAsset> submeshes = new List<ModelSubmeshAsset>();
    // preserve mesh order, compute index ranges per mesh, and map mesh.MaterialIndex to slot name
    return new ModelAsset {
        Positions = positions,
        Normals = normals,
        TexCoords = texCoords,
        Indices16 = indices16,
        Indices32 = indices32,
        Submeshes = submeshes.ToArray()
    };
}
```

```csharp
ImportedModelMaterialAsset[] BuildGeneratedMaterials(Scene scene) {
    // build deterministic .helmat payloads from scene.Materials and map_Kd texture paths
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssimpModelImporterTests"
```

Expected:
- PASS for multiple submesh preservation
- PASS for `.mtl`-derived `.helmat` generation coverage

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.fbximporter/HelengineAssimpImporter.cs engine/helengine.editor.fbximporter/AssimpSceneModelAssetConverter.cs engine/helengine.editor.tests/AssimpModelImporterTests.cs
git commit -m "feat: preserve obj submeshes and import mtl materials"
```

## Task 6: Generate Deterministic Material Assets During Model Import

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing asset-import manager tests**

```csharp
[Fact]
public void ImportModel_whenImporterReturnsGeneratedMaterials_writesSiblingHelmatAssets() {
    string sourcePath = WriteSourceModel("sponza.obj");
    AssetImportManager manager = CreateManager(new TestModelImporter());

    ModelAsset importedAsset = manager.ImportModel(sourcePath);

    Assert.True(File.Exists(Path.Combine(AssetsRootPath, "sponza", "Fabric.helmat")));
    Assert.True(File.Exists(Path.Combine(AssetsRootPath, "sponza", "Wood.helmat")));
}
```

```csharp
[Fact]
public void ImportModel_whenReimporting_updatesExistingGeneratedHelmat_in_place() {
    string sourcePath = WriteSourceModel("sponza.obj");
    AssetImportManager manager = CreateManager(new TestModelImporter());

    manager.ImportModel(sourcePath);
    DateTime firstWriteUtc = File.GetLastWriteTimeUtc(Path.Combine(AssetsRootPath, "sponza", "Fabric.helmat"));
    manager.ImportModel(sourcePath);
    DateTime secondWriteUtc = File.GetLastWriteTimeUtc(Path.Combine(AssetsRootPath, "sponza", "Fabric.helmat"));

    Assert.True(secondWriteUtc >= firstWriteUtc);
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests"
```

Expected:
- FAIL because `AssetImportManager.ImportModel` only writes the cached model asset

- [ ] **Step 3: Implement deterministic `.helmat` generation in the model import path**

```csharp
public ModelAsset ImportModel(string sourcePath) {
    AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
    ImportedModelAssetSet importResult = AssetContentManager.Load<ImportedModelAssetSet>(sourcePath, settings.Importer.ImporterId);
    ModelAsset asset = importResult.ModelAsset;
    ModelAssetProcessor.Apply(asset, GetCurrentPlatformModelProcessorSettings(settings));
    asset.Id = settings.Importer.AssetId;
    WriteGeneratedModelMaterials(sourcePath, importResult.GeneratedMaterials);
    WriteCachedModelAsset(asset);
    SaveImportSettings(sourcePath, settings);
    return asset;
}
```

```csharp
void WriteGeneratedModelMaterials(string sourcePath, ImportedModelMaterialAsset[] generatedMaterials) {
    string modelDirectoryPath = Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Model directory could not be resolved.");
    for (int index = 0; index < generatedMaterials.Length; index++) {
        ImportedModelMaterialAsset generatedMaterial = generatedMaterials[index];
        string fullMaterialPath = Path.Combine(modelDirectoryPath, generatedMaterial.RelativeMaterialPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullMaterialPath) ?? modelDirectoryPath);
        using FileStream stream = new FileStream(fullMaterialPath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, generatedMaterial.MaterialAsset);
    }
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests"
```

Expected:
- PASS for sibling `.helmat` generation
- PASS for deterministic reimport behavior

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests/AssetImportManagerModelTests.cs
git commit -m "feat: generate helmat assets from imported obj materials"
```

## Task 7: Rewrite and Package Slot Material References

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing packaging tests**

```csharp
[Fact]
public void Build_whenSceneMeshUsesTwoMaterialSlots_packages_both_material_assets() {
    string scenePath = WriteSceneWithMultiSlotMesh();
    EditorWindowsBuildScenePackager packager = CreatePackager();

    string buildRootPath = packager.Build(scenePath);

    Assert.True(File.Exists(Path.Combine(buildRootPath, "cooked", "materials", "Fabric.helmat")));
    Assert.True(File.Exists(Path.Combine(buildRootPath, "cooked", "materials", "Wood.helmat")));
}
```

```csharp
[Fact]
public void RewriteSceneAssetReference_whenMeshPayloadContainsSlotMaterials_rewrites_each_slot_reference() {
    byte[] rewrittenPayload = RewriteMeshPayloadWithTwoMaterialSlots();

    Assert.Contains("Fabric.helmat", DecodePayload(rewrittenPayload));
    Assert.Contains("Wood.helmat", DecodePayload(rewrittenPayload));
}
```

- [ ] **Step 2: Run the focused tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected:
- FAIL because packagers still only rewrite one mesh material reference

- [ ] **Step 3: Implement slot-aware scene rewriting and packaging**

```csharp
SceneAssetReference[] RewriteMaterialReferences(SceneAssetReference[] references, string buildRootPath) {
    SceneAssetReference[] rewritten = new SceneAssetReference[references.Length];
    for (int index = 0; index < references.Length; index++) {
        rewritten[index] = RewriteMaterialReference(references[index], buildRootPath);
    }

    return rewritten;
}
```

```csharp
if (version >= 2) {
    int materialReferenceCount = reader.ReadInt32();
    SceneAssetReference[] materialReferences = new SceneAssetReference[materialReferenceCount];
    for (int index = 0; index < materialReferenceCount; index++) {
        materialReferences[index] = RewriteMaterialReference(ReadOptionalReference(reader), buildRootPath);
    }
    WriteMaterialReferences(writer, materialReferences);
}
```

- [ ] **Step 4: Re-run the focused tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected:
- PASS for slot-material packaging coverage

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: package mesh material slots"
```

## Task 8: Run the Full Regression Set

**Files:**
- Modify: `docs/superpowers/plans/2026-05-08-obj-mtl-submesh-import.md`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Run the focused regression commands**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~ModelAssetIndexDataTests|FullyQualifiedName~MeshComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~AssimpModelImporterTests|FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected:
- PASS for all submesh, importer, scene persistence, and packaging regressions

- [ ] **Step 2: Run the broader editor test project if the focused suite is green**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj
```

Expected:
- PASS, or a documented unrelated pre-existing failure with no regressions in the new coverage

- [ ] **Step 3: Commit the final integrated slice**

```bash
git add engine/helengine.core engine/helengine.directx11 engine/helengine.vulkan engine/helengine.editor engine/helengine.editor.fbximporter engine/helengine.editor.tests
git commit -m "feat: add obj mtl submesh import pipeline"
```
