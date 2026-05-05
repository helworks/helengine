# PS2 Renderer Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first PS2 renderer foundation slice: renderer-family platform metadata, PS2 schema-gated material cooking, runtime support for platform-owned cooked materials, and a custom PS2 opaque render path with unlit and simple-lit execution.

**Architecture:** This plan implements only the first execution slice from the approved PS2 renderer architecture spec. The transpiled `helengine.core` runtime remains authoritative for gameplay, while the PS2 player gains renderer-family-aware material authoring, PS2-owned cooked material payloads, runtime render-proxy and frame-plan scaffolding, and a first custom 3D path in `helengine-ps2`. Mesh and gameplay-facing component contracts stay compatible with the existing runtime while PS2 rendering becomes platform-owned.

**Tech Stack:** C# / .NET 9, xUnit, `helengine.baseplatform`, `helengine.core`, `helengine.editor`, `helengine-ps2`, Docker, PS2SDK, gsKit, transpiled C++ runtime.

---

## Scope Check

The approved PS2 architecture spec covers several later subsystems that should not be forced into one implementation batch:

- richer PS2 mesh artifact conversion
- dedicated PS2 texture export formats and residency tiers
- shadow systems
- alternate renderer families such as toon
- final skinned-character execution

This plan intentionally targets the first implementation slice only:

- PS2 renderer family selection through graphics profiles
- PS2 material schema gating by renderer family
- PS2 cooked material contract
- runtime support for platform-owned cooked materials
- PS2 render proxy and frame-plan skeleton
- one custom opaque PS2 path with unlit and simple-lit execution

## File Structure

### Platform metadata and editor-facing schema filtering

- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformDefinitionFactory.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Definitions\EditorPlatformBuildSelectionModelTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Definitions\AssetImportSettingsMaterialSerializationTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

### Shared PS2 cooked material contracts

- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2MaterialAsset.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2MaterialLightingMode.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2MaterialAlphaMode.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2RenderClass.cs`

### PS2 builder cooking

- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2MaterialSchemaIds.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2MaterialCooker.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

### Shared runtime cooked-material resolution

- Modify: `C:\dev\helworks\helengine\engine\helengine.core\managers\rendering\RenderManager3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\scene\runtime\RuntimeSceneAssetReferenceResolver.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\testing\TestRenderManager3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\RuntimeSceneAssetReferenceResolverTests.cs`

### PS2 runtime renderer skeleton

- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeModel.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeModel.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeMaterial.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeMaterial.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderProxy.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderProxy.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePassKind.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlan.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlan.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlanner.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlanner.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderManager3D.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderManager3D.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\Makefile`

### End-to-end verification

- Modify: `C:\dev\helworks\helengine-ps2\README.md`

## Task 1: Expose PS2 renderer families through platform metadata

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformDefinitionFactory.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Definitions\EditorPlatformBuildSelectionModelTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Definitions\AssetImportSettingsMaterialSerializationTests.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing metadata tests for PS2 renderer-family filtering**

Add these assertions to `helengine.baseplatform.tests/Definitions/EditorPlatformBuildSelectionModelTests.cs`:

```csharp
[Fact]
public void ResolveMaterialSchemas_for_ps2_renderer_families_filters_by_graphics_profile() {
    PlatformDefinition definition = helengine.ps2.builder.Ps2PlatformDefinitionFactory.Create();
    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(definition);

    PlatformMaterialSchemaDefinition[] standardSchemas = selectionModel.ResolveMaterialSchemas("ps2-standard-forward");
    PlatformMaterialSchemaDefinition[] showcaseSchemas = selectionModel.ResolveMaterialSchemas("ps2-showcase-forward");

    Assert.Contains(standardSchemas, schema => schema.SchemaId == "ps2-unlit-textured");
    Assert.Contains(standardSchemas, schema => schema.SchemaId == "ps2-simple-lit-textured");
    Assert.DoesNotContain(standardSchemas, schema => schema.SchemaId == "ps2-showcase-lit-textured");

    Assert.Contains(showcaseSchemas, schema => schema.SchemaId == "ps2-showcase-lit-textured");
}
```

Add these assertions to `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`:

```csharp
[Fact]
public void Descriptor_and_definition_publish_renderer_family_profiles_and_ps2_material_schemas() {
    Ps2PlatformAssetBuilder builder = new();

    Assert.Contains(builder.Definition.GraphicsProfiles, profile => profile.ProfileId == "ps2-standard-forward");
    Assert.Contains(builder.Definition.GraphicsProfiles, profile => profile.ProfileId == "ps2-showcase-forward");
    Assert.Contains(builder.Definition.MaterialSchemas, schema => schema.SchemaId == "ps2-unlit-textured");
    Assert.Contains(builder.Definition.MaterialSchemas, schema => schema.SchemaId == "ps2-simple-lit-textured");
    Assert.Contains(builder.Definition.MaterialSchemas, schema => schema.SchemaId == "ps2-showcase-lit-textured");
}
```

- [ ] **Step 2: Run the focused metadata tests and verify they fail**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildSelectionModelTests|FullyQualifiedName~AssetImportSettingsMaterialSerializationTests" -v minimal
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Descriptor_and_definition" -v minimal
```

Expected: FAIL because the PS2 platform still exposes only the `gs-kit` graphics profile and does not publish renderer-family-specific material schemas.

- [ ] **Step 3: Replace the single PS2 graphics profile with renderer-family profiles and schema-gated materials**

Update `helengine-ps2/builder/Ps2PlatformDefinitionFactory.cs` so the graphics-profile section looks like:

```csharp
new PlatformGraphicsProfileDefinition(
    "ps2-standard-forward",
    "PS2 Standard Forward",
    "Standard PS2 forward renderer for gameplay scenes.",
    [
        new PlatformSettingDefinition("default-width", "Default Width", PlatformSettingKind.Text, "640", true, []),
        new PlatformSettingDefinition("default-height", "Default Height", PlatformSettingKind.Text, "448", true, []),
        new PlatformSettingDefinition("vsync-enabled", "VSync Enabled", PlatformSettingKind.Boolean, "true", true, []),
        new PlatformSettingDefinition("fullscreen-enabled", "Fullscreen Enabled", PlatformSettingKind.Boolean, "false", true, [])
    ]),
new PlatformGraphicsProfileDefinition(
    "ps2-showcase-forward",
    "PS2 Showcase Forward",
    "Expensive PS2 forward renderer for tiny showcase scenes.",
    [
        new PlatformSettingDefinition("default-width", "Default Width", PlatformSettingKind.Text, "640", true, []),
        new PlatformSettingDefinition("default-height", "Default Height", PlatformSettingKind.Text, "448", true, []),
        new PlatformSettingDefinition("vsync-enabled", "VSync Enabled", PlatformSettingKind.Boolean, "true", true, []),
        new PlatformSettingDefinition("fullscreen-enabled", "Fullscreen Enabled", PlatformSettingKind.Boolean, "false", true, [])
    ])
```

Add a material-schema section like:

```csharp
[
    new PlatformMaterialSchemaDefinition(
        "ps2-unlit-textured",
        "PS2 Unlit Textured",
        ["ps2-standard-forward", "ps2-showcase-forward"],
        [
            new PlatformMaterialFieldDefinition("texture-relative-path", "Texture", PlatformMaterialFieldKind.Text, string.Empty, false, []),
            new PlatformMaterialFieldDefinition("alpha-mode", "Alpha Mode", PlatformMaterialFieldKind.Choice, "opaque", true, ["opaque", "alpha-test", "alpha-blend", "additive"]),
            new PlatformMaterialFieldDefinition("double-sided", "Double Sided", PlatformMaterialFieldKind.Boolean, "false", true, []),
            new PlatformMaterialFieldDefinition("vertex-color-mode", "Vertex Color", PlatformMaterialFieldKind.Choice, "multiply", true, ["multiply", "ignore"])
        ]),
    new PlatformMaterialSchemaDefinition(
        "ps2-simple-lit-textured",
        "PS2 Simple Lit Textured",
        ["ps2-standard-forward", "ps2-showcase-forward"],
        [
            new PlatformMaterialFieldDefinition("texture-relative-path", "Texture", PlatformMaterialFieldKind.Text, string.Empty, false, []),
            new PlatformMaterialFieldDefinition("alpha-mode", "Alpha Mode", PlatformMaterialFieldKind.Choice, "opaque", true, ["opaque", "alpha-test", "alpha-blend", "additive"]),
            new PlatformMaterialFieldDefinition("double-sided", "Double Sided", PlatformMaterialFieldKind.Boolean, "false", true, []),
            new PlatformMaterialFieldDefinition("vertex-color-mode", "Vertex Color", PlatformMaterialFieldKind.Choice, "multiply", true, ["multiply", "ignore"])
        ]),
    new PlatformMaterialSchemaDefinition(
        "ps2-showcase-lit-textured",
        "PS2 Showcase Lit Textured",
        ["ps2-showcase-forward"],
        [
            new PlatformMaterialFieldDefinition("texture-relative-path", "Texture", PlatformMaterialFieldKind.Text, string.Empty, false, []),
            new PlatformMaterialFieldDefinition("alpha-mode", "Alpha Mode", PlatformMaterialFieldKind.Choice, "opaque", true, ["opaque", "alpha-test", "alpha-blend", "additive"]),
            new PlatformMaterialFieldDefinition("double-sided", "Double Sided", PlatformMaterialFieldKind.Boolean, "false", true, []),
            new PlatformMaterialFieldDefinition("vertex-color-mode", "Vertex Color", PlatformMaterialFieldKind.Choice, "multiply", true, ["multiply", "ignore"]),
            new PlatformMaterialFieldDefinition("expensive-mode-allowed", "Expensive Mode", PlatformMaterialFieldKind.Boolean, "true", true, [])
        ])
]
```

Also change the PS2 build profile default graphics profile id from `gs-kit` to `ps2-standard-forward`.

- [ ] **Step 4: Re-run the metadata tests and verify they pass**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildSelectionModelTests|FullyQualifiedName~AssetImportSettingsMaterialSerializationTests" -v minimal
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Descriptor_and_definition" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the renderer-family metadata changes**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine\helengine.baseplatform.tests\Definitions\EditorPlatformBuildSelectionModelTests.cs engine\helengine.baseplatform.tests\Definitions\AssetImportSettingsMaterialSerializationTests.cs
rtk proxy git -C C:\dev\helworks\helengine-ps2 add builder\Ps2PlatformDefinitionFactory.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "test: cover PS2 renderer-family schema filtering"
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: publish PS2 renderer families and material schemas"
```

## Task 2: Add the shared PS2 cooked material contract and builder cook path

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2MaterialAsset.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2MaterialLightingMode.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2MaterialAlphaMode.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\ps2\Ps2RenderClass.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2MaterialSchemaIds.cs`
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2MaterialCooker.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write the failing PS2 material-cook test**

Add this test to `helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`:

```csharp
[Fact]
public void CookMaterial_when_using_ps2_simple_lit_schema_returns_ps2_material_asset() {
    Ps2PlatformAssetBuilder builder = new();

    PlatformMaterialCookResult result = builder.CookMaterial(new PlatformMaterialCookRequest(
        "Materials/Wall.helmat",
        "Materials/Wall.helmat",
        "ps2",
        "ps2-default",
        "ps2-standard-forward",
        "ps2-simple-lit-textured",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["texture-relative-path"] = "cooked/textures/wall.hasset",
            ["alpha-mode"] = "opaque",
            ["double-sided"] = "false",
            ["vertex-color-mode"] = "multiply"
        }));

    Ps2MaterialAsset materialAsset = Assert.IsType<Ps2MaterialAsset>(AssetSerializer.DeserializeFromBytes(result.CookedMaterialBytes));
    Assert.Equal("ps2-standard-forward", materialAsset.RendererFamilyId);
    Assert.Equal(Ps2MaterialLightingMode.SimpleLit, materialAsset.LightingMode);
    Assert.Equal(Ps2MaterialAlphaMode.Opaque, materialAsset.AlphaMode);
    Assert.Equal(Ps2RenderClass.Opaque, materialAsset.RenderClass);
    Assert.Empty(result.ReferencedShaderAssetIds);
}
```

- [ ] **Step 2: Run the PS2 builder tests and verify they fail**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -v minimal
```

Expected: FAIL because `Ps2PlatformAssetBuilder.CookMaterial(...)` still throws `InvalidOperationException`.

- [ ] **Step 3: Create the PS2 cooked material asset and implement builder translation**

Add the shared asset files:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one PS2-native cooked material payload selected by the PS2 builder.
    /// </summary>
    public class Ps2MaterialAsset : Asset {
        /// <summary>
        /// Gets or sets the PS2 renderer family that owns this material payload.
        /// </summary>
        public string RendererFamilyId;

        /// <summary>
        /// Gets or sets the lighting mode selected by the PS2 schema.
        /// </summary>
        public Ps2MaterialLightingMode LightingMode;

        /// <summary>
        /// Gets or sets the alpha behavior selected by the PS2 schema.
        /// </summary>
        public Ps2MaterialAlphaMode AlphaMode;

        /// <summary>
        /// Gets or sets the coarse PS2 render class for frame routing.
        /// </summary>
        public Ps2RenderClass RenderClass;

        /// <summary>
        /// Gets or sets the cooked texture path consumed by the PS2 runtime.
        /// </summary>
        public string TextureRelativePath;

        /// <summary>
        /// Gets or sets whether the material should render two-sided geometry.
        /// </summary>
        public bool DoubleSided;

        /// <summary>
        /// Gets or sets whether vertex color should modulate the final output.
        /// </summary>
        public bool UseVertexColor;

        /// <summary>
        /// Gets or sets whether the author explicitly allowed an expensive path for this material.
        /// </summary>
        public bool ExpensiveModeAllowed;
    }
}
```

```csharp
namespace helengine {
    public enum Ps2MaterialLightingMode {
        Unlit,
        SimpleLit,
        ShowcaseLit
    }
}
```

```csharp
namespace helengine {
    public enum Ps2MaterialAlphaMode {
        Opaque,
        AlphaTest,
        AlphaBlend,
        Additive
    }
}
```

```csharp
namespace helengine {
    public enum Ps2RenderClass {
        Opaque,
        AlphaTest,
        Transparent
    }
}
```

Add `helengine-ps2/builder/Ps2MaterialSchemaIds.cs`:

```csharp
namespace helengine.ps2.builder;

public static class Ps2MaterialSchemaIds {
    public const string UnlitTextured = "ps2-unlit-textured";
    public const string SimpleLitTextured = "ps2-simple-lit-textured";
    public const string ShowcaseLitTextured = "ps2-showcase-lit-textured";

    public const string TextureRelativePathFieldId = "texture-relative-path";
    public const string AlphaModeFieldId = "alpha-mode";
    public const string DoubleSidedFieldId = "double-sided";
    public const string VertexColorModeFieldId = "vertex-color-mode";
    public const string ExpensiveModeAllowedFieldId = "expensive-mode-allowed";
}
```

Add `helengine-ps2/builder/Ps2MaterialCooker.cs`:

```csharp
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.ps2.builder;

public sealed class Ps2MaterialCooker {
    public PlatformMaterialCookResult Cook(PlatformMaterialCookRequest request) {
        Ps2MaterialAsset cookedAsset = new Ps2MaterialAsset {
            Id = request.MaterialAssetId,
            RendererFamilyId = request.SelectedGraphicsProfileId,
            LightingMode = ResolveLightingMode(request.SchemaId),
            AlphaMode = ResolveAlphaMode(request.FieldValues),
            RenderClass = ResolveRenderClass(request.FieldValues),
            TextureRelativePath = ResolveString(request.FieldValues, Ps2MaterialSchemaIds.TextureRelativePathFieldId),
            DoubleSided = ResolveBoolean(request.FieldValues, Ps2MaterialSchemaIds.DoubleSidedFieldId),
            UseVertexColor = !string.Equals(
                ResolveString(request.FieldValues, Ps2MaterialSchemaIds.VertexColorModeFieldId),
                "ignore",
                StringComparison.OrdinalIgnoreCase),
            ExpensiveModeAllowed = ResolveBoolean(request.FieldValues, Ps2MaterialSchemaIds.ExpensiveModeAllowedFieldId)
        };

        return new PlatformMaterialCookResult(
            AssetSerializer.SerializeToBytes(cookedAsset),
            Array.Empty<string>());
    }
}
```

Update `Ps2PlatformAssetBuilder.CookMaterial(...)` to call the cooker instead of throwing:

```csharp
public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
    if (request == null) {
        throw new ArgumentNullException(nameof(request));
    }

    return new Ps2MaterialCooker().Cook(request);
}
```

- [ ] **Step 4: Re-run the PS2 builder tests and verify they pass**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the PS2 cooked-material contract work**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine\helengine.core\assets\raw\ps2
rtk proxy git -C C:\dev\helworks\helengine-ps2 add builder\Ps2MaterialSchemaIds.cs builder\Ps2MaterialCooker.cs builder\Ps2PlatformAssetBuilder.cs builder.tests\Ps2PlatformAssetBuilderTests.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: add shared PS2 cooked material asset"
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: cook PS2 materials from renderer-family schemas"
```

## Task 3: Let the player runtime resolve platform-owned cooked material assets

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\managers\rendering\RenderManager3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\scene\runtime\RuntimeSceneAssetReferenceResolver.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\testing\TestRenderManager3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\RuntimeSceneAssetReferenceResolverTests.cs`

- [ ] **Step 1: Write the failing runtime cooked-material resolution test**

Create `engine/helengine.editor.tests/serialization/scene/RuntimeSceneAssetReferenceResolverTests.cs` with:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene;

public sealed class RuntimeSceneAssetReferenceResolverTests : IDisposable {
    readonly string TempRootPath;

    public RuntimeSceneAssetReferenceResolverTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-scene-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    [Fact]
    public void ResolveMaterial_when_given_ps2_cooked_material_uses_platform_owned_build_path() {
        Core core = new();
        TestRenderManager3D renderManager = new();
        core.Initialize(renderManager, new TestRenderManager2D(), new TestInputBackend());

        Ps2MaterialAsset materialAsset = new Ps2MaterialAsset {
            Id = "Materials/Test.helmat",
            RendererFamilyId = "ps2-standard-forward",
            LightingMode = Ps2MaterialLightingMode.Unlit,
            AlphaMode = Ps2MaterialAlphaMode.Opaque,
            RenderClass = Ps2RenderClass.Opaque
        };
        string materialPath = Path.Combine(TempRootPath, "cooked", "materials", "test.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);
        File.WriteAllBytes(materialPath, AssetSerializer.SerializeToBytes(materialAsset));

        RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
            new ContentManager(),
            TempRootPath,
            ShaderCompileTarget.DirectX11);

        RuntimeMaterial runtimeMaterial = resolver.ResolveMaterial(new SceneAssetReference {
            SourceKind = SceneAssetReferenceSourceKind.FileSystem,
            RelativePath = "cooked/materials/test.hasset"
        });

        Assert.NotNull(runtimeMaterial);
        Assert.Single(renderManager.BuiltCookedMaterialAssets);
        Assert.IsType<Ps2MaterialAsset>(renderManager.BuiltCookedMaterialAssets[0]);
    }
}
```

- [ ] **Step 2: Run the focused runtime resolver tests and verify they fail**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneAssetReferenceResolverTests" -v minimal
```

Expected: FAIL because `RuntimeSceneAssetReferenceResolver.ResolveMaterial(...)` assumes every cooked material is a shader-backed `MaterialAsset` with a packaged shader dependency.

- [ ] **Step 3: Add a platform-owned cooked-material entrypoint to `RenderManager3D` and update runtime resolution**

Update `RenderManager3D.cs` to add:

```csharp
/// <summary>
/// Builds a runtime material from one platform-owned cooked material asset payload.
/// </summary>
/// <param name="materialAsset">Cooked material asset loaded from packaged content.</param>
/// <returns>Runtime material instance.</returns>
public virtual RuntimeMaterial BuildMaterialFromAsset(Asset materialAsset) {
    throw new NotSupportedException("This renderer does not support cooked material asset creation.");
}
```

Update `RuntimeSceneAssetReferenceResolver.ResolveMaterial(...)` to:

```csharp
string fullPath = ResolveFileBackedAssetPath(reference);
Asset cookedMaterialAsset = AssetSerializer.DeserializeFromBytes(File.ReadAllBytes(fullPath));
if (cookedMaterialAsset is MaterialAsset shaderMaterialAsset) {
    ShaderAsset shaderAsset = AssetContentManager.Load<ShaderAsset>(
        ResolveShaderPackagePath(shaderMaterialAsset.ShaderAssetId),
        RuntimeContentProcessorIds.ShaderAsset);
    return Core.Instance.RenderManager3D.BuildMaterialFromRaw(shaderMaterialAsset, shaderAsset);
}

return Core.Instance.RenderManager3D.BuildMaterialFromAsset(cookedMaterialAsset);
```

Update `TestRenderManager3D.cs` to record cooked material assets:

```csharp
readonly List<Asset> BuiltCookedMaterialAssetsValue;

public IReadOnlyList<Asset> BuiltCookedMaterialAssets => BuiltCookedMaterialAssetsValue;

public override RuntimeMaterial BuildMaterialFromAsset(Asset materialAsset) {
    if (materialAsset == null) {
        throw new ArgumentNullException(nameof(materialAsset));
    }

    BuiltCookedMaterialAssetsValue.Add(materialAsset);
    return new TestRuntimeMaterial();
}
```

- [ ] **Step 4: Re-run the runtime resolver tests and verify they pass**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneAssetReferenceResolverTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the shared cooked-material runtime resolution changes**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine\helengine.core\managers\rendering\RenderManager3D.cs engine\helengine.core\scene\runtime\RuntimeSceneAssetReferenceResolver.cs engine\helengine.editor.tests\testing\TestRenderManager3D.cs engine\helengine.editor.tests\serialization\scene\RuntimeSceneAssetReferenceResolverTests.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: let players resolve platform-owned cooked materials"
```

## Task 4: Add the PS2 render-proxy and frame-plan runtime skeleton

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeModel.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeModel.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeMaterial.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeMaterial.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderProxy.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderProxy.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePassKind.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlan.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlan.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlanner.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlanner.cpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderManager3D.hpp`
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderManager3D.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\Makefile`

- [ ] **Step 1: Replace the inline stub `Ps2RenderManager3D` with dedicated runtime classes**

Create `Ps2RuntimeModel.hpp`:

```cpp
#pragma once

#include <vector>

#include "RuntimeModel.hpp"

namespace helengine::ps2 {
    class Ps2RuntimeModel final : public ::RuntimeModel {
    public:
        Ps2RuntimeModel();

        void LoadFromRaw(::ModelAsset* modelAsset);

        const std::vector<::float3>& GetPositions() const;
        const std::vector<::float3>& GetNormals() const;
        const std::vector<std::uint16_t>& GetIndices() const;
    private:
        std::vector<::float3> Positions;
        std::vector<::float3> Normals;
        std::vector<std::uint16_t> Indices;
    };
}
```

Create `Ps2RuntimeMaterial.hpp`:

```cpp
#pragma once

#include "RuntimeMaterial.hpp"

namespace helengine {
    class Ps2MaterialAsset;
}

namespace helengine::ps2 {
    class Ps2RuntimeMaterial final : public ::RuntimeMaterial {
    public:
        Ps2RuntimeMaterial();

        void LoadFromCooked(::Ps2MaterialAsset* materialAsset);

        ::Ps2MaterialLightingMode GetLightingMode() const;
        ::Ps2MaterialAlphaMode GetAlphaMode() const;
        ::Ps2RenderClass GetRenderClass() const;
        bool UsesVertexColor() const;
    private:
        ::Ps2MaterialLightingMode LightingMode;
        ::Ps2MaterialAlphaMode AlphaMode;
        ::Ps2RenderClass RenderClass;
        bool UseVertexColor;
    };
}
```

Create `Ps2RenderProxy.hpp`:

```cpp
#pragma once

#include "Entity.hpp"

namespace helengine::ps2 {
    class Ps2RuntimeModel;
    class Ps2RuntimeMaterial;

    class Ps2RenderProxy {
    public:
        void Synchronize(::IDrawable3D* drawable);

        ::IDrawable3D* GetDrawable() const;
        Ps2RuntimeModel* GetModel() const;
        Ps2RuntimeMaterial* GetMaterial() const;
        bool IsStatic() const;
    private:
        ::IDrawable3D* Drawable;
        Ps2RuntimeModel* Model;
        Ps2RuntimeMaterial* Material;
        bool Static;
    };
}
```

Create `Ps2FramePassKind.hpp`:

```cpp
#pragma once

namespace helengine::ps2 {
    enum class Ps2FramePassKind {
        OpaqueWorld,
        OpaqueDynamic,
        Present
    };
}
```

Create `Ps2FramePlan.hpp`:

```cpp
#pragma once

#include <vector>

namespace helengine::ps2 {
    class Ps2RenderProxy;

    class Ps2FramePlan {
    public:
        std::vector<const Ps2RenderProxy*> OpaqueWorld;
        std::vector<const Ps2RenderProxy*> OpaqueDynamic;
    };
}
```

Create `Ps2FramePlanner.hpp`:

```cpp
#pragma once

#include <vector>

namespace helengine::ps2 {
    class Ps2FramePlan;
    class Ps2RenderProxy;

    class Ps2FramePlanner {
    public:
        Ps2FramePlan Build(const std::vector<Ps2RenderProxy>& proxies) const;
    };
}
```

- [ ] **Step 2: Move the 3D manager into `src/platform/ps2/rendering` and make it own proxies plus a frame planner**

Create `Ps2RenderManager3D.hpp`:

```cpp
#pragma once

#include <vector>

#include "RenderManager3D.hpp"
#include "platform/ps2/rendering/Ps2FramePlanner.hpp"

namespace helengine::ps2 {
    class Ps2RenderProxy;
    class Ps2RuntimeMaterial;
    class Ps2RuntimeModel;

    class Ps2RenderManager3D final : public ::RenderManager3D {
    public:
        Ps2RenderManager3D();

        ::RuntimeModel* BuildModelFromRaw(::ModelAsset* data) override;
        ::RuntimeMaterial* BuildMaterialFromAsset(::Asset* materialAsset) override;
        void Draw() override;

    private:
        void RebuildProxies();

        std::vector<Ps2RenderProxy> Proxies;
        Ps2FramePlanner FramePlanner;
    };
}
```

Implement `Ps2RenderManager3D.cpp` with this skeleton:

```cpp
#include "platform/ps2/rendering/Ps2RenderManager3D.hpp"

#include "Core.hpp"
#include "ObjectManager.hpp"
#include "model/interfaces/IDrawable3D.hpp"
#include "platform/ps2/rendering/Ps2FramePlan.hpp"
#include "platform/ps2/rendering/Ps2RenderProxy.hpp"
#include "platform/ps2/rendering/Ps2RuntimeMaterial.hpp"
#include "platform/ps2/rendering/Ps2RuntimeModel.hpp"

namespace helengine::ps2 {
    Ps2RenderManager3D::Ps2RenderManager3D() {
    }

    ::RuntimeModel* Ps2RenderManager3D::BuildModelFromRaw(::ModelAsset* data) {
        if (data == nullptr) {
            throw std::invalid_argument("PS2 raw model data is required.");
        }

        Ps2RuntimeModel* runtimeModel = new Ps2RuntimeModel();
        runtimeModel->LoadFromRaw(data);
        return runtimeModel;
    }

    ::RuntimeMaterial* Ps2RenderManager3D::BuildMaterialFromAsset(::Asset* materialAsset) {
        ::Ps2MaterialAsset* cookedAsset = static_cast<::Ps2MaterialAsset*>(materialAsset);
        Ps2RuntimeMaterial* runtimeMaterial = new Ps2RuntimeMaterial();
        runtimeMaterial->LoadFromCooked(cookedAsset);
        return runtimeMaterial;
    }

    void Ps2RenderManager3D::Draw() {
        RebuildProxies();
        Ps2FramePlan plan = FramePlanner.Build(Proxies);
        (void)plan;
    }
}
```

Modify `Ps2BootHost.cpp` to include `platform/ps2/rendering/Ps2RenderManager3D.hpp` and remove the inline stub `Ps2RenderManager3D` class. Update the `Makefile` source list to compile every new `rendering/*.cpp` file.

- [ ] **Step 3: Compile the PS2 host and verify the runtime skeleton builds**

Run:

```powershell
rtk proxy docker build -t helengine-ps2 C:\dev\helworks\helengine-ps2
rtk proxy docker run --rm -v C:\dev\helworks\helengine-ps2:/workspace -w /workspace -e HELENGINE_CORE_CPP_ROOT=/workspace/build/generated-core helengine-ps2 make
```

Expected: FAIL at first because the new runtime files are missing from the makefile and because the stub inline 3D manager is still embedded in `Ps2BootHost.cpp`.

- [ ] **Step 4: Re-run the PS2 Docker build and verify it passes**

Run:

```powershell
rtk proxy docker run --rm -v C:\dev\helworks\helengine-ps2:/workspace -w /workspace -e HELENGINE_CORE_CPP_ROOT=/workspace/build/generated-core helengine-ps2 make
```

Expected: PASS and `C:\dev\helworks\helengine-ps2\build\helengine_ps2.elf` exists.

- [ ] **Step 5: Commit the PS2 runtime skeleton**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-ps2 add src\platform\ps2\rendering src\platform\ps2\Ps2BootHost.hpp src\platform\ps2\Ps2BootHost.cpp Makefile
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: add PS2 render proxy and frame plan skeleton"
```

## Task 5: Implement the first custom opaque PS2 draw path and verify end-to-end export

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RenderManager3D.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2FramePlanner.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\rendering\Ps2RuntimeMaterial.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\README.md`

- [ ] **Step 1: Route static and dynamic proxies into separate opaque frame buckets**

Implement `Ps2FramePlanner.cpp`:

```cpp
#include "platform/ps2/rendering/Ps2FramePlanner.hpp"

#include "platform/ps2/rendering/Ps2FramePlan.hpp"
#include "platform/ps2/rendering/Ps2RenderProxy.hpp"
#include "platform/ps2/rendering/Ps2RuntimeMaterial.hpp"

namespace helengine::ps2 {
    Ps2FramePlan Ps2FramePlanner::Build(const std::vector<Ps2RenderProxy>& proxies) const {
        Ps2FramePlan plan;
        for (const Ps2RenderProxy& proxy : proxies) {
            Ps2RuntimeMaterial* material = proxy.GetMaterial();
            if (material == nullptr) {
                continue;
            }

            if (material->GetRenderClass() != ::Ps2RenderClass::Opaque) {
                continue;
            }

            if (proxy.IsStatic()) {
                plan.OpaqueWorld.push_back(&proxy);
            } else {
                plan.OpaqueDynamic.push_back(&proxy);
            }
        }

        return plan;
    }
}
```

- [ ] **Step 2: Add unlit and simple-lit CPU-side triangle submission inside the PS2 3D manager**

Update `Ps2RuntimeMaterial.cpp` to map cooked material enums into runtime state and add a helper:

```cpp
bool Ps2RuntimeMaterial::UsesVertexColor() const {
    return UseVertexColor;
}
```

Update `Ps2RenderManager3D.cpp` to draw the planned opaque proxies:

```cpp
void Ps2RenderManager3D::Draw() {
    RebuildProxies();
    Ps2FramePlan plan = FramePlanner.Build(Proxies);

    for (const Ps2RenderProxy* proxy : plan.OpaqueWorld) {
        DrawOpaqueProxy(*proxy);
    }

    for (const Ps2RenderProxy* proxy : plan.OpaqueDynamic) {
        DrawOpaqueProxy(*proxy);
    }
}
```

Add a private helper:

```cpp
void Ps2RenderManager3D::DrawOpaqueProxy(const Ps2RenderProxy& proxy) {
    const Ps2RuntimeModel* model = proxy.GetModel();
    const Ps2RuntimeMaterial* material = proxy.GetMaterial();
    if (model == nullptr || material == nullptr) {
        return;
    }

    for (std::size_t index = 0; index + 2 < model->GetIndices().size(); index += 3) {
        const ::float3& a = model->GetPositions()[model->GetIndices()[index + 0]];
        const ::float3& b = model->GetPositions()[model->GetIndices()[index + 1]];
        const ::float3& c = model->GetPositions()[model->GetIndices()[index + 2]];

        const u64 colorA = ResolveVertexColor(*material, a, model->GetNormals()[model->GetIndices()[index + 0]]);
        const u64 colorB = ResolveVertexColor(*material, b, model->GetNormals()[model->GetIndices()[index + 1]]);
        const u64 colorC = ResolveVertexColor(*material, c, model->GetNormals()[model->GetIndices()[index + 2]]);

        gsKit_prim_triangle_gouraud_3d(
            ActiveGsGlobal,
            ProjectX(a), ProjectY(a), 1.0f, colorA,
            ProjectX(b), ProjectY(b), 1.0f, colorB,
            ProjectX(c), ProjectY(c), 1.0f, colorC);
    }
}
```

Use one fixed light direction for `SimpleLit` at first:

```cpp
u64 Ps2RenderManager3D::ResolveVertexColor(
    const Ps2RuntimeMaterial& material,
    const ::float3& position,
    const ::float3& normal) const {
    (void)position;

    if (material.GetLightingMode() == ::Ps2MaterialLightingMode::Unlit) {
        return GS_SETREG_RGBAQ(0xC0, 0xC0, 0xC0, 0x80, 0x00);
    }

    const ::float3 lightDirection(0.0f, -0.70710678f, -0.70710678f);
    double ndotl = std::max(0.0, static_cast<double>(normal.X * lightDirection.X) + static_cast<double>(normal.Y * lightDirection.Y) + static_cast<double>(normal.Z * lightDirection.Z));
    std::uint8_t intensity = static_cast<std::uint8_t>(64 + static_cast<int>(ndotl * 191.0));
    return GS_SETREG_RGBAQ(intensity, intensity, intensity, 0x80, 0x00);
}
```

- [ ] **Step 3: Run the full PS2 export path and verify the player build still completes**

Run:

```powershell
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildSelectionModelTests|FullyQualifiedName~AssetImportSettingsMaterialSerializationTests" -v minimal
rtk proxy dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneAssetReferenceResolverTests" -v minimal
rtk proxy dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -v minimal
rtk proxy docker run --rm -v C:\dev\helworks\helengine-ps2:/workspace -w /workspace -e HELENGINE_CORE_CPP_ROOT=/workspace/build/generated-core helengine-ps2 make
```

Expected: PASS.

- [ ] **Step 4: Update the PS2 README with the renderer-family and cooked-material contract**

Add to `helengine-ps2/README.md`:

```markdown
## Renderer foundation

The PS2 build now exposes renderer families through platform graphics profiles:

- `ps2-standard-forward`
- `ps2-showcase-forward`

PS2 materials are cooked into `Ps2MaterialAsset` payloads rather than Windows shader-backed material assets. The PS2 runtime resolves those cooked assets through the player-owned render manager and routes renderables through PS2 render proxies plus a planner-owned opaque pass.
```

- [ ] **Step 5: Commit the first custom PS2 opaque path**

```powershell
rtk proxy git -C C:\dev\helworks\helengine-ps2 add src\platform\ps2\rendering\Ps2RenderManager3D.cpp src\platform\ps2\rendering\Ps2FramePlanner.cpp src\platform\ps2\rendering\Ps2RuntimeMaterial.cpp README.md
rtk proxy git -C C:\dev\helworks\helengine-ps2 commit -m "feat: add first custom PS2 opaque renderer path"
```

## Self-Review

- Spec coverage: this plan intentionally targets the approved first PS2 renderer slice rather than the full architecture. It covers renderer-family selection, schema gating, PS2 cooked material assets, platform-owned runtime cooked-material resolution, PS2 render proxies, PS2 frame planning, and the first opaque unlit/simple-lit path.
- Placeholder scan: no `TODO`, `TBD`, or deferred “implement later” instructions appear inside the tasks.
- Type consistency: the plan consistently uses `ps2-standard-forward`, `ps2-showcase-forward`, `ps2-unlit-textured`, `ps2-simple-lit-textured`, `ps2-showcase-lit-textured`, `Ps2MaterialAsset`, `Ps2MaterialLightingMode`, `Ps2MaterialAlphaMode`, and `Ps2RenderClass`.
