# Generated Standard Material Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Engine/Materials/Standard` as a generated engine asset, use it automatically for new primitive objects, and make generated materials browseable, pickable, and loadable.

**Architecture:** Extend the generated-asset provider contract so it can resolve `RuntimeMaterial` alongside `RuntimeModel`, then add one cached engine-generated material built from the existing built-in `EditorDefaultMesh.hlsl` shader. Once that infrastructure exists, wire the standard material into primitive creation, material picking, and scene-load reference resolution so the same generated asset behaves consistently across browse, pick, save, and load.

**Tech Stack:** C#/.NET 9, Hel editor generated-asset system, scene persistence (`SceneAssetReference`, `MeshComponentPersistenceDescriptor`), xUnit

---

## File Structure

### New Files

- `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`
  Builds and caches the generated `RuntimeMaterial` for `Engine/Materials/Standard`.
- `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
  Verifies generated material references resolve back into runtime materials.

### Modified Files

- `engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs`
  Add material-resolution support to the provider contract.
- `engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs`
  Add generated runtime material resolution through the owning provider.
- `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`
  Publish `Engine/Materials/Standard` and resolve it through the new material cache.
- `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
  Assign the generated standard material to `Cube` and `Plane` and persist the material reference.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  Accept generated materials in the material picker and resolve them through the registry.
- `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
  Resolve generated material references instead of rejecting them.
- `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`
  Extend provider coverage to include `Engine/Materials/Standard`.
- `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`
  Extend registry coverage to include `ResolveRuntimeMaterial(...)`.
- `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
  Verify primitive creation now stores both generated model and generated material references.
- `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`
  Verify created primitives are born with both model and material assigned.
- `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
  Add generated-material picker coverage parallel to the existing generated-model coverage.
- `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
  Round-trip one generated material reference through `.helen` load behavior.
- `engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs`
  Extend the test provider so registry and picker tests can resolve generated runtime materials.

## Task 1: Add Generated Material Infrastructure

**Files:**
- Create: `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`
- Modify: `engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs`
- Modify: `engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs`
- Modify: `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`
- Modify: `engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs`

- [ ] **Step 1: Write the failing provider and registry tests**

Add one provider-tree assertion in `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`:

```csharp
/// <summary>
/// Ensures the provider publishes the generated materials directory and standard material entry.
/// </summary>
[Fact]
public void LoadEntries_WhenBrowsingEngineMaterialPaths_ReturnsStandardMaterialEntry() {
    EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
    List<AssetBrowserEntry> engineEntries = new List<AssetBrowserEntry>();
    List<AssetBrowserEntry> materialEntries = new List<AssetBrowserEntry>();

    provider.LoadEntries(EngineGeneratedAssetProvider.EngineRootPath, engineEntries);
    provider.LoadEntries(EngineGeneratedAssetProvider.EngineMaterialsPath, materialEntries);

    Assert.Contains(engineEntries, entry => entry.Name == "Materials" && entry.EntryKind == AssetEntryKind.Directory);
    AssetBrowserEntry standardEntry = Assert.Single(materialEntries);
    Assert.Equal("Standard", standardEntry.Name);
    Assert.Equal(AssetEntryKind.Material, standardEntry.EntryKind);
    Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, standardEntry.AssetId);
}
```

Add one material-resolution assertion in the same file:

```csharp
/// <summary>
/// Ensures resolving the same generated material twice reuses the cached runtime material instance.
/// </summary>
[Fact]
public void TryResolveRuntimeMaterial_WhenCalledTwice_ReusesTheCachedRuntimeMaterial() {
    EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
    AssetBrowserEntry standardEntry = AssetBrowserEntry.CreateGeneratedAsset(
        "Standard",
        EngineGeneratedAssetProvider.StandardMaterialRelativePath,
        AssetEntryKind.Material,
        EngineGeneratedAssetProvider.ProviderIdValue,
        EngineGeneratedMaterialCache.StandardAssetId);

    Assert.True(provider.TryResolveRuntimeMaterial(standardEntry, out RuntimeMaterial firstMaterial));
    Assert.True(provider.TryResolveRuntimeMaterial(standardEntry, out RuntimeMaterial secondMaterial));

    Assert.Same(firstMaterial, secondMaterial);
}
```

Add one material-resolution assertion in `engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs`:

```csharp
/// <summary>
/// Ensures generated material picks resolve through the provider that owns the entry.
/// </summary>
[Fact]
public void ResolveRuntimeMaterial_WhenGeneratedMaterialEntryIsPicked_UsesTheOwningProvider() {
    GeneratedAssetProviderRegistry.ResetForTests();
    TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
    TestGeneratedAssetProvider provider = new TestGeneratedAssetProvider(
        "engine",
        new[] {
            AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", "engine:material:standard")
        },
        new TestRuntimeModel(),
        runtimeMaterial);
    GeneratedAssetProviderRegistry.Register(provider);

    RuntimeMaterial resolvedMaterial = GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(
        AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", "engine:material:standard"));

    Assert.Same(runtimeMaterial, resolvedMaterial);
}
```

- [ ] **Step 2: Run the provider and registry tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EngineGeneratedAssetProviderTests|GeneratedAssetProviderRegistryTests"`

Expected: FAIL because generated material paths, cache, provider contract, and registry resolution do not exist yet.

- [ ] **Step 3: Implement the generated material cache**

Create `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Builds and caches generated engine runtime materials.
    /// </summary>
    public static class EngineGeneratedMaterialCache {
        /// <summary>
        /// Stable generated asset id used by the standard engine material.
        /// </summary>
        public const string StandardAssetId = "engine:material:standard";

        /// <summary>
        /// Built-in shader file used by the generated standard material.
        /// </summary>
        const string StandardShaderFileName = "EditorDefaultMesh.hlsl";

        /// <summary>
        /// Shader variant used by the generated standard material.
        /// </summary>
        const string DefaultVariant = "default";

        /// <summary>
        /// Cached generated runtime materials keyed by stable asset id.
        /// </summary>
        static readonly Dictionary<string, RuntimeMaterial> RuntimeMaterials = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);

        /// <summary>
        /// Clears cached generated materials between tests.
        /// </summary>
        public static void ResetForTests() {
            RuntimeMaterials.Clear();
        }

        /// <summary>
        /// Retrieves the generated runtime material for the supplied asset id.
        /// </summary>
        /// <param name="assetId">Stable generated material identifier.</param>
        /// <returns>Cached runtime material instance.</returns>
        public static RuntimeMaterial GetRuntimeMaterial(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated material id must be provided.", nameof(assetId));
            }

            if (RuntimeMaterials.TryGetValue(assetId, out RuntimeMaterial cachedMaterial)) {
                return cachedMaterial;
            }

            RuntimeMaterial runtimeMaterial = BuildRuntimeMaterial(assetId);
            RuntimeMaterials[assetId] = runtimeMaterial;
            return runtimeMaterial;
        }

        /// <summary>
        /// Builds one generated runtime material from the supported asset id.
        /// </summary>
        /// <param name="assetId">Stable generated material identifier.</param>
        /// <returns>Runtime material instance built for the supplied id.</returns>
        static RuntimeMaterial BuildRuntimeMaterial(string assetId) {
            if (!string.Equals(assetId, StandardAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material '{assetId}'.");
            }

            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(Core.Instance.RenderManager3D, StandardShaderFileName);
            var materialAsset = new MaterialAsset {
                Id = "Engine.Materials.Standard.material",
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = "EditorDefaultMesh.vs",
                PixelProgram = "EditorDefaultMesh.ps",
                Variant = DefaultVariant
            };

            return Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }
    }
}
```

- [ ] **Step 4: Extend the provider contract and registry**

Modify `engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs`:

```csharp
/// <summary>
/// Attempts to resolve one generated material entry to a runtime material.
/// </summary>
/// <param name="entry">Generated entry requested by the editor.</param>
/// <param name="runtimeMaterial">Resolved runtime material when the provider owns the entry.</param>
/// <returns>True when the provider resolved the entry; otherwise false.</returns>
bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial);
```

Modify `engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs`:

```csharp
/// <summary>
/// Resolves one generated material entry through its owning provider.
/// </summary>
/// <param name="entry">Generated entry selected by the editor.</param>
/// <returns>Runtime material resolved by the provider.</returns>
public static RuntimeMaterial ResolveRuntimeMaterial(AssetBrowserEntry entry) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }
    if (string.IsNullOrWhiteSpace(entry.ProviderId)) {
        throw new InvalidOperationException("Generated asset entries must include a provider id.");
    }

    if (!Providers.TryGetValue(entry.ProviderId, out IGeneratedAssetProvider provider)) {
        throw new InvalidOperationException($"Generated asset provider '{entry.ProviderId}' is not registered.");
    }

    if (!provider.TryResolveRuntimeMaterial(entry, out RuntimeMaterial runtimeMaterial) || runtimeMaterial == null) {
        throw new InvalidOperationException($"Generated runtime material '{entry.AssetId}' could not be resolved.");
    }

    return runtimeMaterial;
}
```

Modify `engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs` so the test helper can return materials without breaking existing call sites:

```csharp
readonly RuntimeMaterial RuntimeMaterial;

public TestGeneratedAssetProvider(
    string providerId,
    IReadOnlyList<AssetBrowserEntry> entries,
    RuntimeModel runtimeModel)
    : this(providerId, entries, runtimeModel, new TestRuntimeMaterial()) {
}

public TestGeneratedAssetProvider(
    string providerId,
    IReadOnlyList<AssetBrowserEntry> entries,
    RuntimeModel runtimeModel,
    RuntimeMaterial runtimeMaterial) {
    if (runtimeMaterial == null) {
        throw new ArgumentNullException(nameof(runtimeMaterial));
    }

    ProviderId = providerId;
    Entries = entries;
    RuntimeModel = runtimeModel;
    RuntimeMaterial = runtimeMaterial;
}

public bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    runtimeMaterial = null;
    if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
        return false;
    }

    runtimeMaterial = RuntimeMaterial;
    return true;
}
```

Also update `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs` `Dispose()` to clear both caches:

```csharp
public void Dispose() {
    EngineGeneratedModelCache.ResetForTests();
    EngineGeneratedMaterialCache.ResetForTests();
}
```

- [ ] **Step 5: Publish `Engine/Materials/Standard` from the engine provider**

Modify `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`:

```csharp
public const string EngineMaterialsPath = "Engine/Materials";
public const string StandardMaterialRelativePath = "Engine/Materials/Standard";
```

Update `LoadEntries(...)`:

```csharp
if (string.Equals(relativePath, EngineRootPath, StringComparison.Ordinal)) {
    entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Models", EngineModelsPath, ProviderId));
    entries.Add(AssetBrowserEntry.CreateGeneratedDirectory("Materials", EngineMaterialsPath, ProviderId));
    return;
}

if (string.Equals(relativePath, EngineMaterialsPath, StringComparison.Ordinal)) {
    entries.Add(AssetBrowserEntry.CreateGeneratedAsset(
        "Standard",
        StandardMaterialRelativePath,
        AssetEntryKind.Material,
        ProviderId,
        EngineGeneratedMaterialCache.StandardAssetId));
    return;
}
```

Add material resolution:

```csharp
/// <summary>
/// Resolves one engine-generated material entry through the shared runtime-material cache.
/// </summary>
/// <param name="entry">Generated entry requested by the editor.</param>
/// <param name="runtimeMaterial">Resolved runtime material when the entry belongs to this provider.</param>
/// <returns>True when the provider resolved the material; otherwise false.</returns>
public bool TryResolveRuntimeMaterial(AssetBrowserEntry entry, out RuntimeMaterial runtimeMaterial) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    runtimeMaterial = null;
    if (!string.Equals(entry.ProviderId, ProviderId, StringComparison.Ordinal)) {
        return false;
    }
    if (entry.EntryKind != AssetEntryKind.Material) {
        return false;
    }

    runtimeMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(entry.AssetId);
    return true;
}
```

- [ ] **Step 6: Run the provider and registry tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EngineGeneratedAssetProviderTests|GeneratedAssetProviderRegistryTests"`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs
git commit -m "feat: add generated standard material asset"
```

## Task 2: Assign The Standard Material To New Primitives

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`

- [ ] **Step 1: Write the failing primitive-creation tests**

Extend `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`:

```csharp
/// <summary>
/// Ensures primitive creation stores both generated model and generated standard material references.
/// </summary>
[Theory]
[InlineData("Cube", EngineGeneratedModelCache.CubeAssetId, EngineGeneratedAssetProvider.CubeRelativePath)]
[InlineData("Plane", EngineGeneratedModelCache.PlaneAssetId, EngineGeneratedAssetProvider.PlaneRelativePath)]
public void CreatePrimitive_StoresGeneratedModelAndMaterialReferences(string expectedName, string modelAssetId, string modelRelativePath) {
    EditorSceneCreationService service = new EditorSceneCreationService();

    EditorEntity entity = expectedName == "Cube" ? service.CreateCube() : service.CreatePlane();

    MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
    EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));

    Assert.NotNull(meshComponent.Model);
    Assert.NotNull(meshComponent.Material);
    Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
    Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference modelReference));
    Assert.True(saveState.TryGetAssetReference("Material", out SceneAssetReference materialReference));
    Assert.Equal(modelAssetId, modelReference.AssetId);
    Assert.Equal(modelRelativePath, modelReference.RelativePath);
    Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, materialReference.AssetId);
    Assert.Equal(EngineGeneratedAssetProvider.StandardMaterialRelativePath, materialReference.RelativePath);
}
```

Extend `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`:

```csharp
Assert.NotNull(meshComponent.Material);
```

inside `HandleAddCubeRequested_CreatesMeshEntityAndSelectsIt()`.

Also update the test fixture setup/teardown in both `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs` and `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`:

```csharp
EngineGeneratedMaterialCache.ResetForTests();
```

right next to the existing `EngineGeneratedModelCache.ResetForTests();` calls so the new cache stays isolated across tests.

- [ ] **Step 2: Run the primitive-creation tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSceneCreationServiceTests|EditorSessionAddMenuTests"`

Expected: FAIL because `EditorSceneCreationService` still sets only `MeshComponent.Model`.

- [ ] **Step 3: Update scene creation to assign and persist the generated material**

Modify `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`:

```csharp
/// <summary>
/// Stable save-state slot name used by MeshComponent persistence for material references.
/// </summary>
const string MeshMaterialReferenceName = "Material";
```

Update primitive creation signatures:

```csharp
public EditorEntity CreateCube() {
    return CreatePrimitive(
        "Cube",
        EngineGeneratedModelCache.CubeAssetId,
        EngineGeneratedAssetProvider.CubeRelativePath,
        EngineGeneratedMaterialCache.StandardAssetId,
        EngineGeneratedAssetProvider.StandardMaterialRelativePath);
}

public EditorEntity CreatePlane() {
    return CreatePrimitive(
        "Plane",
        EngineGeneratedModelCache.PlaneAssetId,
        EngineGeneratedAssetProvider.PlaneRelativePath,
        EngineGeneratedMaterialCache.StandardAssetId,
        EngineGeneratedAssetProvider.StandardMaterialRelativePath);
}
```

Update `CreatePrimitive(...)`:

```csharp
EditorEntity CreatePrimitive(
    string name,
    string modelAssetId,
    string modelRelativePath,
    string materialAssetId,
    string materialRelativePath) {
    RuntimeModel runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(modelAssetId);
    RuntimeMaterial runtimeMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(materialAssetId);
    EditorEntity entity = CreateBaseEntity(name);

    try {
        EntitySaveComponent saveComponent = FindSaveComponent(entity);
        MeshComponent meshComponent = new MeshComponent {
            Model = runtimeModel,
            Material = runtimeMaterial
        };
        entity.AddComponent(meshComponent);
        saveComponent.SetAssetReference(meshComponent, MeshModelReferenceName, BuildGeneratedReference(modelRelativePath, modelAssetId));
        saveComponent.SetAssetReference(meshComponent, MeshMaterialReferenceName, BuildGeneratedReference(materialRelativePath, materialAssetId));
        return entity;
    } catch {
        entity.Enabled = false;
        Core.Instance.ObjectManager.RemoveEntity(entity);
        throw;
    }
}
```

Replace `BuildGeneratedModelReference(...)` with one reusable helper:

```csharp
SceneAssetReference BuildGeneratedReference(string relativePath, string assetId) {
    return new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.Generated,
        RelativePath = relativePath,
        ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
        AssetId = assetId
    };
}
```

- [ ] **Step 4: Run the primitive-creation tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSceneCreationServiceTests|EditorSessionAddMenuTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/EditorSessionAddMenuTests.cs
git commit -m "feat: assign generated standard material to new primitives"
```

## Task 3: Make Generated Materials Pickable And Loadable

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`

- [ ] **Step 1: Write the failing generated-material picker and resolver tests**

Extend `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`:

```csharp
/// <summary>
/// Ensures generated material picks assign the provider runtime material and persist the generated reference.
/// </summary>
[Fact]
public void HandleMaterialPicked_WhenEntryIsGenerated_AssignsTheProviderRuntimeMaterialDisplayLabelAndGeneratedReference() {
    TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
    GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
        "engine",
        new[] {
            AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
        },
        new TestRuntimeModel(),
        runtimeMaterial));

    MeshComponent meshComponent = new MeshComponent();
    EditorEntity entity = CreateEntityWithComponent(meshComponent);
    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
    view.ShowComponents(entity);

    ComponentPropertyRow materialRow = FindMaterialRow(view);
    MethodInfo handleMaterialPicked = typeof(ComponentPropertiesView).GetMethod("HandleMaterialPicked", BindingFlags.Instance | BindingFlags.NonPublic);
    handleMaterialPicked.Invoke(view, new object[] {
        materialRow,
        AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
    });

    Assert.Same(runtimeMaterial, meshComponent.Material);
    Assert.Equal("Standard", materialRow.ValueText.Text);
    EntitySaveComponent saveComponent = GetSaveComponent(entity);
    Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
    Assert.True(saveState.TryGetAssetReference("Material", out SceneAssetReference reference));
    Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
    Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, reference.AssetId);
}
```

Add `FindMaterialRow(...)` in the same test file:

```csharp
ComponentPropertyRow FindMaterialRow(ComponentPropertiesView view) {
    FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
    List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
    return Assert.Single(rows, row => row.Kind == ComponentPropertyRowKind.Material);
}
```

Create `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies generated material references resolve through the generated-asset registry.
    /// </summary>
    public class EditorSceneAssetReferenceResolverTests : IDisposable {
        readonly string TempProjectRootPath;

        public EditorSceneAssetReferenceResolverTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-material-resolver-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        public void Dispose() {
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        [Fact]
        public void ResolveMaterial_WhenReferenceIsGenerated_UsesGeneratedAssetProviderRegistry() {
            TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
                },
                new TestRuntimeModel(),
                runtimeMaterial));
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(new ContentManager(TempProjectRootPath), TempProjectRootPath);

            RuntimeMaterial resolvedMaterial = resolver.ResolveMaterial(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Materials/Standard",
                ProviderId = "engine",
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            });

            Assert.Same(runtimeMaterial, resolvedMaterial);
        }
    }
}
```

Update `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs` so the main load test uses one generated material reference instead of a filesystem material reference:

```csharp
SceneAssetReference materialReference = new SceneAssetReference {
    SourceKind = SceneAssetReferenceSourceKind.Generated,
    RelativePath = "Engine/Materials/Standard",
    ProviderId = "engine",
    AssetId = EngineGeneratedMaterialCache.StandardAssetId
};
```

and keep `CreateLoadService(...)` explicit by registering that exact reference with `TestSceneAssetReferenceResolver`:

```csharp
SceneFileLoadService CreateLoadService(SceneAssetReference modelReference, SceneAssetReference materialReference) {
    TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
    resolver.RegisterModel(modelReference, new TestRuntimeModel());
    resolver.RegisterMaterial(materialReference, new TestRuntimeMaterial());
    return new SceneFileLoadService(TempProjectRootPath, CreatePersistenceRegistry(), resolver);
}
```

- [ ] **Step 2: Run the picker and resolver tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ComponentPropertiesViewGeneratedAssetTests|EditorSceneAssetReferenceResolverTests|SceneFileLoadServiceTests"`

Expected: FAIL because generated material picks still flow through filesystem-only material loading and generated material references are still rejected by `EditorSceneAssetReferenceResolver`.

- [ ] **Step 3: Allow generated materials in the picker**

Modify `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`:

```csharp
RuntimeMaterial LoadMaterial(AssetBrowserEntry entry) {
    if (entry == null) {
        throw new ArgumentNullException(nameof(entry));
    }

    if (entry.IsGenerated) {
        return GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(entry);
    }

    string extension = entry.Extension;
    if (!IsMaterialExtension(extension)) {
        throw new InvalidOperationException("Selected asset is not a material.");
    }

    MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
    if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
        return null;
    }

    ShaderAsset shaderAsset = LoadShaderAsset(materialAsset.ShaderAssetId);
    return Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
}
```

Keep `StorePickedAssetReference(row, entry);` unchanged so generated material picks automatically persist through `SceneAssetReferenceFactory.CreateFromEntry(entry)`.

- [ ] **Step 4: Resolve generated material references during scene load**

Modify `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`:

```csharp
/// <summary>
/// Resolves one generated material reference through the generated-asset registry.
/// </summary>
/// <param name="reference">Generated material reference to resolve.</param>
/// <returns>Runtime material published by the owning generated-asset provider.</returns>
RuntimeMaterial ResolveGeneratedMaterial(SceneAssetReference reference) {
    AssetBrowserEntry entry = BuildGeneratedEntry(reference, AssetEntryKind.Material);
    return GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(entry);
}
```

- [ ] **Step 5: Run the picker and resolver tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ComponentPropertiesViewGeneratedAssetTests|EditorSceneAssetReferenceResolverTests|SceneFileLoadServiceTests"`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs
git commit -m "feat: support generated standard material pick and load"
```

## Task 4: Run Focused And Full Verification

**Files:**
- No code changes expected

- [ ] **Step 1: Run focused generated-material regressions**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EngineGeneratedAssetProviderTests|GeneratedAssetProviderRegistryTests|EditorSceneCreationServiceTests|EditorSessionAddMenuTests|ComponentPropertiesViewGeneratedAssetTests|EditorSceneAssetReferenceResolverTests|SceneFileLoadServiceTests"`

Expected: PASS

- [ ] **Step 2: Run the full editor test suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS for the full suite, with only pre-existing warnings.

- [ ] **Step 3: Review spec coverage before closing**

Checklist:

- `Engine/Materials/Standard` appears in the engine generated asset tree.
- Generated asset providers resolve `RuntimeMaterial` as well as `RuntimeModel`.
- `Cube` and `Plane` are created with both generated model and generated material references.
- `Empty` remains unchanged.
- Material picker accepts generated engine materials.
- Generated material references load correctly through `EditorSceneAssetReferenceResolver`.
- `.helen` round-trip keeps the generated material reference on `MeshComponent`.

- [ ] **Step 4: Commit the final verified state**

```bash
git status --short
git add engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs engine/helengine.editor/managers/asset/IGeneratedAssetProvider.cs engine/helengine.editor/managers/asset/GeneratedAssetProviderRegistry.cs engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs engine/helengine.editor.tests/managers/asset/GeneratedAssetProviderRegistryTests.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/EditorSessionAddMenuTests.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/testing/TestGeneratedAssetProvider.cs
git commit -m "feat: add generated standard material for primitives"
```
