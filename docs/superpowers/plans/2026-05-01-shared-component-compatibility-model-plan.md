# Shared Component Compatibility Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** make component serialization canonical in `helengine.core`, while platform builders only declare compatibility rules and shared transforms for real exceptions instead of forcing per-platform component code.

**Architecture:** core components keep one stable record shape, runtime loading uses a registry instead of hardcoded `if` ladders, and the editor packaging path consults builder-provided compatibility metadata before applying shared canonical transforms. Windows and PS2 both advertise the same rules for shared components like `MeshComponent`, `CameraComponent`, and `FPSComponent`, so adding a new normal component should not require a separate Windows and PS2 rewrite path.

**Tech Stack:** C#/.NET 9, xUnit, `helengine.baseplatform`, `helengine.core`, `helengine.editor`, `helengine-windows`, `helengine-ps2`

---

### Task 1: Add the shared component compatibility contract

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformComponentCompatibilityKind.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformComponentCompatibilityDefinition.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs`

- [ ] **Step 1: Write the failing test**

Add one assertion to the platform-definition test so the type exposes component compatibility metadata alongside build, graphics, and asset metadata.

```csharp
[Fact]
public void PlatformDefinition_preserves_build_graphics_asset_and_component_metadata() {
    PlatformDefinition definition = new(
        "windows",
        "Windows DirectX",
        [
            new PlatformBuildProfileDefinition(
                "debug",
                "Debug",
                "Debug player build",
                "directx11",
                [])
        ],
        [
            new PlatformGraphicsProfileDefinition(
                "directx11",
                "DirectX 11",
                "Default Windows renderer",
                [])
        ],
        [
            new PlatformAssetRequirementDefinition(
                "texture",
                "Texture",
                true,
                ["png", "tga"])
        ],
        [
            new PlatformComponentCompatibilityDefinition(
                "helengine.FPSComponent",
                PlatformComponentCompatibilityKind.PassThrough,
                "FPS overlay is canonical on this platform.",
                string.Empty)
        ]);

    Assert.Equal("helengine.FPSComponent", definition.ComponentCompatibilities[0].ComponentTypeId);
    Assert.Equal(PlatformComponentCompatibilityKind.PassThrough, definition.ComponentCompatibilities[0].CompatibilityKind);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj -nologo --filter "FullyQualifiedName~PlatformDefinitionTests"
```

Expected: fail because `PlatformDefinition` does not yet expose `ComponentCompatibilities`.

- [ ] **Step 3: Write the minimal implementation**

Add the new enum and definition class, then extend `PlatformDefinition` to carry the new array.

```csharp
namespace helengine.baseplatform.Definitions;

public enum PlatformComponentCompatibilityKind {
    PassThrough,
    Transform,
    Unsupported
}
```

```csharp
namespace helengine.baseplatform.Definitions;

public class PlatformComponentCompatibilityDefinition {
    public PlatformComponentCompatibilityDefinition(
        string componentTypeId,
        PlatformComponentCompatibilityKind compatibilityKind,
        string reason,
        string remediation) {
        if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id is required.", nameof(componentTypeId));
        }
        ComponentTypeId = componentTypeId;
        CompatibilityKind = compatibilityKind;
        Reason = reason ?? string.Empty;
        Remediation = remediation ?? string.Empty;
    }

    public string ComponentTypeId { get; }
    public PlatformComponentCompatibilityKind CompatibilityKind { get; }
    public string Reason { get; }
    public string Remediation { get; }
}
```

Update `PlatformDefinition` so its constructor accepts a `PlatformComponentCompatibilityDefinition[] componentCompatibilities` argument and stores it in a new `ComponentCompatibilities` property.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj -nologo
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add \
  engine/helengine.baseplatform/Definitions/PlatformComponentCompatibilityKind.cs \
  engine/helengine.baseplatform/Definitions/PlatformComponentCompatibilityDefinition.cs \
  engine/helengine.baseplatform/Definitions/PlatformDefinition.cs \
  engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs \
  engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs \
  engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs
git commit -m "feat: add shared component compatibility metadata"
```

### Task 2: Replace the core runtime `if` ladder with a shared component registry

**Files:**
- Create: `engine/helengine.core/scene/runtime/IRuntimeComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs`
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add a test that exercises case-insensitive component ids through a registry-backed runtime loader, so the runtime no longer depends on a hardcoded `if` chain.

```csharp
[Fact]
public void Load_WhenSceneContainsFpsOverlay_UsesTheRuntimeComponentRegistry() {
    Core.Instance.DefaultFontAsset = CreateFont();

    RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault();
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(
        Core.Instance.SceneAssetReferenceResolver,
        registry);

    SceneAsset sceneAsset = new SceneAsset {
        RootEntities = new[] {
            new SceneEntityAsset {
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = "Helengine.FPSComponent",
                        ComponentIndex = 0,
                        Payload = WriteFpsComponentPayload()
                    }
                }
            }
        }
    };

    IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);

    Assert.IsType<FPSComponent>(Assert.Single(loadedRoots[0].Components, component => component is FPSComponent));
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected: fail because `RuntimeSceneLoadService` still uses the hardcoded component type ladder.

- [ ] **Step 3: Write the minimal implementation**

Add a registry and one deserializer per built-in component type, then update `Core.Initialize` to create the default registry once.

```csharp
namespace helengine;

public interface IRuntimeComponentDeserializer {
    string ComponentTypeId { get; }
    Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver);
}
```

```csharp
namespace helengine;

public sealed class RuntimeComponentRegistry {
    readonly Dictionary<string, IRuntimeComponentDeserializer> DescriptorsByTypeId =
        new Dictionary<string, IRuntimeComponentDeserializer>(StringComparer.OrdinalIgnoreCase);

    public static RuntimeComponentRegistry CreateDefault() {
        RuntimeComponentRegistry registry = new RuntimeComponentRegistry();
        registry.Register(new RuntimeMeshComponentDeserializer());
        registry.Register(new RuntimeCameraComponentDeserializer());
        registry.Register(new RuntimeFPSComponentDeserializer());
        return registry;
    }

    public void Register(IRuntimeComponentDeserializer descriptor) { /* validate and store by type id */ }
    public bool TryGet(string componentTypeId, out IRuntimeComponentDeserializer descriptor) { /* lookup */ }
}
```

`RuntimeSceneLoadService` should read the registry instead of branching on `MeshComponent`, `CameraComponent`, and `FPSComponent` directly.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected: pass for the runtime scene-load tests that cover `MeshComponent`, `CameraComponent`, `FPSComponent`, and the unknown-component failure path.

- [ ] **Step 5: Commit**

```bash
git add \
  engine/helengine.core/scene/runtime/IRuntimeComponentDeserializer.cs \
  engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs \
  engine/helengine.core/scene/runtime/RuntimeMeshComponentDeserializer.cs \
  engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs \
  engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs \
  engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs \
  engine/helengine.core/Core.cs \
  engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "refactor: load runtime components through a registry"
```

### Task 3: Move editor packaging onto the compatibility model and shared transforms

**Files:**
- Create: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing test**

Add one test that proves the packager can fail on an explicitly unsupported component without hardcoded per-platform branching, and one test that proves the existing `FPSComponent` payload still packages to the canonical lowercase id.

```csharp
[Fact]
public void Package_WhenPlatformMarksComponentUnsupported_FailsWithTheBuilderReason() {
    PlatformDefinition definition = new(
        "windows",
        "Windows DirectX",
        [],
        [],
        [],
        [
            new PlatformComponentCompatibilityDefinition(
                "helengine.BadComponent",
                PlatformComponentCompatibilityKind.Unsupported,
                "This platform does not support the component.",
                "Remove the component before building.")
        ]);

    EditorWindowsBuildScenePackager packager = new EditorWindowsBuildScenePackager(ProjectRootPath, definition);

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        packager.Package(new[] { "Scenes/Bad.helen" }, BuildRootPath));

    Assert.Contains("This platform does not support the component.", ex.Message);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: fail because the packager still hardcodes `MeshComponent`, `CameraComponent`, and `FPSComponent`.

- [ ] **Step 3: Write the minimal implementation**

Move the existing component rewrite logic out of `EditorWindowsBuildScenePackager` into one shared helper so the packager can make a generic pass over `SceneComponentAssetRecord` values.

```csharp
namespace helengine.editor;

public static class SceneComponentPackagingTransformService {
    public static bool TryTransform(SceneComponentAssetRecord record, out SceneComponentAssetRecord transformedRecord) {
        // MeshComponent, CameraComponent, and FPSComponent rewrite logic lives here.
        // The packager decides whether to call this based on platform compatibility metadata.
    }
}
```

Update the packager flow so it:

1. builds a dictionary from `PlatformDefinition.ComponentCompatibilities`
2. matches component ids case-insensitively
3. emits pass-through records unchanged
4. invokes `SceneComponentPackagingTransformService` when the compatibility kind is `Transform`
5. throws with the builder-provided reason when the compatibility kind is `Unsupported`

Update `EditorPlatformBuildExecutor` so it passes `builder.Definition` into the packager before the scene package step.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: pass, including the `FPSComponent` canonicalization case and the unsupported-component failure case.

- [ ] **Step 5: Commit**

```bash
git add \
  engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs \
  engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs \
  engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs \
  engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "refactor: move scene component packaging to shared transforms"
```

### Task 4: Publish the compatibility tables from the Windows and PS2 builders

**Files:**
- Modify: `helengine-windows/builder/WindowsPlatformDefinitionFactory.cs`
- Modify: `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs`
- Modify: `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

Extend the builder metadata tests so both builders declare the same compatibility intent for shared engine components.

```csharp
[Fact]
public void Descriptor_and_definition_expose_component_compatibility_metadata() {
    WindowsPlatformAssetBuilder builder = new();

    Assert.Contains(builder.Definition.ComponentCompatibilities,
        compatibility => compatibility.ComponentTypeId == "helengine.FPSComponent"
            && compatibility.CompatibilityKind == PlatformComponentCompatibilityKind.PassThrough);
    Assert.Contains(builder.Definition.ComponentCompatibilities,
        compatibility => compatibility.ComponentTypeId == "helengine.MeshComponent"
            && compatibility.CompatibilityKind == PlatformComponentCompatibilityKind.Transform);
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -nologo
dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -nologo
```

Expected: fail until both `PlatformDefinitionFactory` files provide the new component compatibility array.

- [ ] **Step 3: Write the minimal implementation**

Add the same shared component entries to both platform definitions so the editor sees a stable policy across Windows and PS2.

```csharp
new PlatformComponentCompatibilityDefinition(
    "helengine.MeshComponent",
    PlatformComponentCompatibilityKind.Transform,
    "Mesh components are normalized during packaging.",
    string.Empty),
new PlatformComponentCompatibilityDefinition(
    "helengine.CameraComponent",
    PlatformComponentCompatibilityKind.Transform,
    "Camera components are normalized during packaging.",
    string.Empty),
new PlatformComponentCompatibilityDefinition(
    "helengine.FPSComponent",
    PlatformComponentCompatibilityKind.PassThrough,
    "FPS overlay payload is canonical across platforms.",
    string.Empty)
```

Use canonical lowercase ids in both builders. The editor should match incoming ids case-insensitively so existing authored records such as `Helengine.FPSComponent` still package successfully.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -nologo
dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -nologo
```

Expected: pass.

- [ ] **Step 5: Commit**

```bash
git add \
  helengine-windows/builder/WindowsPlatformDefinitionFactory.cs \
  helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs \
  helengine-windows/builder.tests/WindowsPlatformAssetBuilderTests.cs \
  helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs
git commit -m "feat: publish component compatibility metadata from platform builders"
```

### Task 5: Run the focused verification matrix

**Files:**
- No code changes expected unless a test exposes a real regression.

- [ ] **Step 1: Run the baseplatform contract tests**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.baseplatform.tests/helengine.baseplatform.tests.csproj -nologo
```

Expected: pass.

- [ ] **Step 2: Run the editor runtime and packaging tests**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: pass for the new registry-backed runtime loader and the generic packaging checks.

- [ ] **Step 3: Run the platform builder metadata tests**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine-windows/builder.tests/helengine.windows.builder.tests.csproj -nologo
dotnet test /mnt/c/dev/helworks/helengine-ps2/builder.tests/helengine.ps2.builder.tests.csproj -nologo
```

Expected: pass.

- [ ] **Step 4: Record the result**

If all focused tests pass, note the exact pass/fail state in the commit message or handoff comment so the next session knows this architecture is in place.

```text
Shared component compatibility model verified across baseplatform, editor packaging, runtime loading, Windows builder metadata, and PS2 builder metadata.
```

- [ ] **Step 5: Commit any verification-only fixes**

If this step surfaces a real regression, commit the smallest follow-up fix separately so the implementation history stays readable.

```bash
git add \
  engine/helengine.baseplatform \
  engine/helengine.core \
  engine/helengine.editor \
  engine/helengine.baseplatform.tests \
  engine/helengine.editor.tests \
  helengine-windows/builder \
  helengine-windows/builder.tests \
  helengine-ps2/builder \
  helengine-ps2/builder.tests
git commit -m "fix: address shared component compatibility regression"
```
