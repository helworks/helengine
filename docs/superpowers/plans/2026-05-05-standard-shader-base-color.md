# Standard Shader Base Color Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the built-in standard mesh shader to `StandardShader.hlsl` and add one schema-driven `base-color` input that flows into a `BaseColorBuffer` constant buffer with a white default.

**Architecture:** Keep one built-in standard shader path. The shader file rename updates the engine-generated standard material, the packaged generated standard material, and the built-in shader compile tests. The new `base-color` field stays schema-driven and is translated into one `MaterialConstantBufferAsset` named `BaseColorBuffer`, with white used whenever settings do not author a color.

**Tech Stack:** C#, HLSL, xUnit, HelEngine material schema metadata, DirectX11/Vulkan built-in shader compilation.

---

## File Structure

### Production Files

- Create: `engine/helengine.editor/shaders/builtin/StandardShader.hlsl`
- Delete: `engine/helengine.editor/shaders/builtin/EditorDefaultMesh.hlsl`
- Modify: `engine/helengine.editor/shaders/EditorBuiltInShaderAssetLibrary.cs`
- Modify: `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs`

### Test Files

- Modify: `engine/helengine.editor.tests/rendering/EditorBuiltInStandardShaderTests.cs`
- Delete: `engine/helengine.editor.tests/shaders/EditorDefaultMeshShaderTests.cs`
- Create: `engine/helengine.editor.tests/shaders/StandardShaderTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildSelectionModelTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/AssetImportSettingsMaterialSerializationTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs`

### Verification Commands

Run from: `C:\dev\helworks\helengine\.sdk9`

- `dotnet test ..\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "EditorBuiltInStandardShaderTests|StandardShaderTests|EngineGeneratedAssetProviderTests"`
- `dotnet test ..\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "PlatformDefinitionTests|EditorPlatformBuildSelectionModelTests|AssetImportSettingsMaterialSerializationTests|EditorPlatformBuildScenePackagerMaterialCookTests|IPlatformAssetBuilderMetadataTests"`

---

### Task 1: Rename The Built-In Standard Shader Contract

**Files:**
- Create: `engine/helengine.editor/shaders/builtin/StandardShader.hlsl`
- Delete: `engine/helengine.editor/shaders/builtin/EditorDefaultMesh.hlsl`
- Modify: `engine/helengine.editor/shaders/EditorBuiltInShaderAssetLibrary.cs`
- Modify: `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor.tests/rendering/EditorBuiltInStandardShaderTests.cs`
- Delete: `engine/helengine.editor.tests/shaders/EditorDefaultMeshShaderTests.cs`
- Create: `engine/helengine.editor.tests/shaders/StandardShaderTests.cs`

- [ ] **Step 1: Write the failing rename tests**

```csharp
// engine/helengine.editor.tests/rendering/EditorBuiltInStandardShaderTests.cs
[Fact]
public void LoadShaderAsset_WhenUsingBuiltInStandardShader_CompilesForDirectX11() {
    ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "StandardShader.hlsl");

    Assert.NotNull(shaderAsset);
    Assert.Equal("StandardShader", shaderAsset.Id);
    Assert.NotNull(shaderAsset.Binaries);
    Assert.NotEmpty(shaderAsset.Binaries);
}
```

```csharp
// engine/helengine.editor.tests/shaders/StandardShaderTests.cs
[Fact]
public void LoadShaderAsset_WhenCompilingForDirectX11_ProducesTheStandardShaderPrograms() {
    ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "StandardShader.hlsl");

    Assert.Equal("StandardShader", shaderAsset.Id);
    Assert.Equal(2, shaderAsset.Binaries.Length);
    Assert.Contains(shaderAsset.Programs, program => program.Name == "StandardShader.vs");
    Assert.Contains(shaderAsset.Programs, program => program.Name == "StandardShader.ps");
}
```

- [ ] **Step 2: Run the focused shader tests to verify they fail**

Run:

```powershell
dotnet test ..\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "EditorBuiltInStandardShaderTests|StandardShaderTests"
```

Expected: FAIL because `StandardShader.hlsl` and the renamed test file/class do not exist yet.

- [ ] **Step 3: Write the minimal rename implementation**

```csharp
// engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs
const string StandardShaderFileName = "StandardShader.hlsl";
const string StandardVertexProgramName = "StandardShader.vs";
const string StandardPixelProgramName = "StandardShader.ps";
```

```csharp
// engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs
const string StandardShaderFileName = "StandardShader.hlsl";
const string StandardVertexProgramName = "StandardShader.vs";
const string StandardPixelProgramName = "StandardShader.ps";
```

```csharp
// engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs
const string StandardShaderFileName = "StandardShader.hlsl";
const string StandardVertexProgramName = "StandardShader.vs";
const string StandardPixelProgramName = "StandardShader.ps";
```

```hlsl
// engine/helengine.editor/shaders/builtin/StandardShader.hlsl
// Copy the current EditorDefaultMesh.hlsl contents verbatim into the new file first.
```

- [ ] **Step 4: Run the focused shader tests to verify they pass**

Run:

```powershell
dotnet test ..\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "EditorBuiltInStandardShaderTests|StandardShaderTests"
```

Expected: PASS, with the built-in standard shader compiling from `StandardShader.hlsl` and reporting `StandardShader` program names.

- [ ] **Step 5: Commit the rename slice**

```bash
git add engine/helengine.editor/shaders/builtin/StandardShader.hlsl engine/helengine.editor/shaders/EditorBuiltInShaderAssetLibrary.cs engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor.tests/rendering/EditorBuiltInStandardShaderTests.cs engine/helengine.editor.tests/shaders/StandardShaderTests.cs
git commit -m "refactor: rename built-in standard shader"
```

### Task 2: Add `BaseColorBuffer` To The Standard Shader And Generated Standard Material

**Files:**
- Modify: `engine/helengine.editor/shaders/builtin/StandardShader.hlsl`
- Modify: `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor.tests/shaders/StandardShaderTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs`

- [ ] **Step 1: Write the failing shader-layout and generated-material tests**

```csharp
// engine/helengine.editor.tests/shaders/StandardShaderTests.cs
[Fact]
public void LoadShaderAsset_WhenCompilingForDirectX11_ExposesBaseColorConstantBufferBinding() {
    ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "StandardShader.hlsl");
    MaterialLayout layout = MaterialLayoutBuilder.Build(CreateMaterialAsset(shaderAsset.Id), shaderAsset);

    Assert.Empty(layout.TextureBindings);
    Assert.Single(layout.ConstantBufferBindings);
    Assert.Equal("BaseColorBuffer", layout.ConstantBufferBindings[0].Name);
    Assert.Equal(16, layout.ConstantBufferBindings[0].Size);
}
```

```csharp
// engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs
[Fact]
public void TryResolveRuntimeMaterial_WhenBuildingGeneratedStandardMaterial_WritesWhiteBaseColorBuffer() {
    EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
    AssetBrowserEntry standardEntry = AssetBrowserEntry.CreateGeneratedAsset(
        "Standard",
        EngineGeneratedAssetProvider.StandardMaterialRelativePath,
        AssetEntryKind.Material,
        EngineGeneratedAssetProvider.ProviderIdValue,
        EngineGeneratedMaterialCache.StandardAssetId);

    Assert.True(provider.TryResolveRuntimeMaterial(standardEntry, out RuntimeMaterial _));

    MaterialAsset builtMaterial = Assert.Single(RenderManager3D.BuiltMaterialAssets);
    MaterialConstantBufferAsset baseColorBuffer = Assert.Single(builtMaterial.ConstantBuffers);
    Assert.Equal("BaseColorBuffer", baseColorBuffer.Name);
    Assert.Equal(new byte[] { 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63, 0, 0, 128, 63 }, baseColorBuffer.Data);
}
```

- [ ] **Step 2: Run the focused editor tests to verify they fail**

Run:

```powershell
dotnet test ..\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "StandardShaderTests|EngineGeneratedAssetProviderTests"
```

Expected: FAIL because the shader still exposes no material constant buffer and the generated standard material still writes no constant buffers.

- [ ] **Step 3: Write the minimal base-color implementation**

```hlsl
// engine/helengine.editor/shaders/builtin/StandardShader.hlsl
cbuffer BaseColorBuffer : register(b3)
{
    float4 baseColor;
};

float4 PS(PS_IN input) : SV_Target
{
    float3 neutralSurfaceColor = float3(0.78f, 0.80f, 0.84f);
    float3 surfaceColor = neutralSurfaceColor * baseColor.rgb;
    // keep the rest of the existing lighting code unchanged
}
```

```csharp
// engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs
static readonly byte[] WhiteBaseColorBufferData = new byte[] {
    0, 0, 128, 63,
    0, 0, 128, 63,
    0, 0, 128, 63,
    0, 0, 128, 63
};

var materialAsset = new MaterialAsset {
    Id = StandardMaterialAssetId,
    ShaderAssetId = shaderAsset.Id,
    VertexProgram = StandardVertexProgramName,
    PixelProgram = StandardPixelProgramName,
    Variant = DefaultVariantName,
    ConstantBuffers = new[] {
        new MaterialConstantBufferAsset {
            Name = "BaseColorBuffer",
            Data = WhiteBaseColorBufferData
        }
    }
};
```

```csharp
// engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs
MaterialAsset materialAsset = new MaterialAsset {
    Id = "Engine.Materials.Standard.material",
    ShaderAssetId = shaderAsset.Id,
    VertexProgram = StandardVertexProgramName,
    PixelProgram = StandardPixelProgramName,
    Variant = StandardShaderVariantName,
    ConstantBuffers = new[] {
        new MaterialConstantBufferAsset {
            Name = "BaseColorBuffer",
            Data = WhiteBaseColorBufferData
        }
    }
};
```

```csharp
// engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs
MaterialAsset materialAsset = new MaterialAsset {
    Id = "Engine.Materials.Standard.material",
    ShaderAssetId = shaderAsset.Id,
    VertexProgram = StandardVertexProgramName,
    PixelProgram = StandardPixelProgramName,
    Variant = StandardShaderVariantName,
    ConstantBuffers = new[] {
        new MaterialConstantBufferAsset {
            Name = "BaseColorBuffer",
            Data = WhiteBaseColorBufferData
        }
    }
};
```

- [ ] **Step 4: Run the focused editor tests to verify they pass**

Run:

```powershell
dotnet test ..\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "EditorBuiltInStandardShaderTests|StandardShaderTests|EngineGeneratedAssetProviderTests"
```

Expected: PASS, with one `BaseColorBuffer` binding visible to the material layout and the generated standard material carrying white default data.

- [ ] **Step 5: Commit the shader/material slice**

```bash
git add engine/helengine.editor/shaders/builtin/StandardShader.hlsl engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor.tests/shaders/StandardShaderTests.cs engine/helengine.editor.tests/managers/asset/EngineGeneratedAssetProviderTests.cs
git commit -m "feat: add base color to standard shader"
```

### Task 3: Extend Standard Material Schema Coverage And Cook `base-color`

**Files:**
- Modify: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildSelectionModelTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/AssetImportSettingsMaterialSerializationTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs`
- Modify: `engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs`

- [ ] **Step 1: Write the failing schema and cook tests**

```csharp
// engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildSelectionModelTests.cs
Assert.Equal("variant", schemas[0].Fields[0].FieldId);
Assert.Equal("base-color", schemas[0].Fields[1].FieldId);
Assert.Equal(PlatformMaterialFieldKind.Color, schemas[0].Fields[1].FieldKind);
Assert.Equal("#ffffff", schemas[0].Fields[1].DefaultValue);
```

```csharp
// engine/helengine.baseplatform.tests/Definitions/AssetImportSettingsMaterialSerializationTests.cs
Assert.Equal("#ffffff", settings.Processor.Platforms["windows"].Material.FieldValues["base-color"]);
```

```csharp
// engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs
PlatformMaterialCookResult result = builder.CookMaterial(new PlatformMaterialCookRequest(
    "Materials/Test.helmat",
    "Materials/Test.helmat",
    "windows",
    "debug",
    "directx11",
    "standard-shader",
    new Dictionary<string, string> {
        ["shader-asset-id"] = "StandardShader",
        ["vertex-program"] = "StandardShader.vs",
        ["pixel-program"] = "StandardShader.ps",
        ["variant"] = "default",
        ["base-color"] = "#336699"
    }));

MaterialAsset materialAsset = Assert.IsType<MaterialAsset>(AssetSerializer.DeserializeFromBytes(result.CookedMaterialBytes));
MaterialConstantBufferAsset baseColorBuffer = Assert.Single(materialAsset.ConstantBuffers);
Assert.Equal("BaseColorBuffer", baseColorBuffer.Name);
Assert.Equal(16, baseColorBuffer.Data.Length);
```

```csharp
// engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs
Assert.Single(packagedMaterial.ConstantBuffers);
Assert.Equal("BaseColorBuffer", packagedMaterial.ConstantBuffers[0].Name);
Assert.Equal(16, packagedMaterial.ConstantBuffers[0].Data.Length);
```

- [ ] **Step 2: Run the focused baseplatform tests to verify they fail**

Run:

```powershell
dotnet test ..\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "PlatformDefinitionTests|EditorPlatformBuildSelectionModelTests|AssetImportSettingsMaterialSerializationTests|EditorPlatformBuildScenePackagerMaterialCookTests|IPlatformAssetBuilderMetadataTests"
```

Expected: FAIL because the standard schema fixtures do not publish `base-color` and the test builder still emits empty `ConstantBuffers`.

- [ ] **Step 3: Write the minimal schema and cook implementation**

```csharp
// engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs
const string BaseColorFieldId = "base-color";
const string BaseColorBufferName = "BaseColorBuffer";

string serializedBaseColor = request.FieldValues.TryGetValue(BaseColorFieldId, out string authoredBaseColor)
    ? authoredBaseColor
    : "#ffffff";

MaterialAsset materialAsset = new MaterialAsset {
    Id = request.MaterialAssetId,
    ShaderAssetId = shaderAssetId,
    VertexProgram = vertexProgram,
    PixelProgram = pixelProgram,
    Variant = variant,
    RenderState = new MaterialRenderState(),
    ConstantBuffers = new[] {
        new MaterialConstantBufferAsset {
            Name = BaseColorBufferName,
            Data = EncodeColor(serializedBaseColor)
        }
    }
};
```

```csharp
// engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs
static byte[] EncodeColor(string serializedColor) {
    string normalized = serializedColor.TrimStart('#');
    if (normalized.Length != 6 && normalized.Length != 8) {
        throw new InvalidOperationException("Base color must use #RRGGBB or #RRGGBBAA.");
    }

    int offset = 0;
    byte alpha = 255;
    if (normalized.Length == 8) {
        alpha = Convert.ToByte(normalized.Substring(0, 2), 16);
        offset = 2;
    }

    byte red = Convert.ToByte(normalized.Substring(offset, 2), 16);
    byte green = Convert.ToByte(normalized.Substring(offset + 2, 2), 16);
    byte blue = Convert.ToByte(normalized.Substring(offset + 4, 2), 16);

    float r = red / 255f;
    float g = green / 255f;
    float b = blue / 255f;
    float a = alpha / 255f;

    return [
        .. BitConverter.GetBytes(r),
        .. BitConverter.GetBytes(g),
        .. BitConverter.GetBytes(b),
        .. BitConverter.GetBytes(a)
    ];
}
```

```csharp
// schema fixtures in the test definitions
new PlatformMaterialFieldDefinition(
    "base-color",
    "Base Color",
    PlatformMaterialFieldKind.Color,
    "#ffffff",
    false,
    [])
```

- [ ] **Step 4: Run the focused baseplatform tests to verify they pass**

Run:

```powershell
dotnet test ..\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "PlatformDefinitionTests|EditorPlatformBuildSelectionModelTests|AssetImportSettingsMaterialSerializationTests|EditorPlatformBuildScenePackagerMaterialCookTests|IPlatformAssetBuilderMetadataTests"
```

Expected: PASS, with schema metadata exposing `base-color`, settings seeding white by default, and cooked materials emitting `BaseColorBuffer`.

- [ ] **Step 5: Commit the schema/cook slice**

```bash
git add engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionTests.cs engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildSelectionModelTests.cs engine/helengine.baseplatform.tests/Definitions/AssetImportSettingsMaterialSerializationTests.cs engine/helengine.baseplatform.tests/Definitions/EditorPlatformBuildScenePackagerMaterialCookTests.cs engine/helengine.baseplatform.tests/Builders/TestPlatformAssetBuilder.cs engine/helengine.baseplatform.tests/Builders/IPlatformAssetBuilderMetadataTests.cs
git commit -m "feat: cook standard shader base color"
```

### Task 4: Final Verification

**Files:**
- Modify: none unless one of the verification runs exposes a regression
- Test: `engine/helengine.editor.tests`
- Test: `engine/helengine.baseplatform.tests`

- [ ] **Step 1: Run the editor-side verification suite**

Run:

```powershell
dotnet test ..\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "EditorBuiltInStandardShaderTests|StandardShaderTests|EngineGeneratedAssetProviderTests"
```

Expected: PASS.

- [ ] **Step 2: Run the baseplatform-side verification suite**

Run:

```powershell
dotnet test ..\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "PlatformDefinitionTests|EditorPlatformBuildSelectionModelTests|AssetImportSettingsMaterialSerializationTests|EditorPlatformBuildScenePackagerMaterialCookTests|IPlatformAssetBuilderMetadataTests"
```

Expected: PASS.

- [ ] **Step 3: Run one final git diff review**

Run:

```bash
git diff --stat HEAD~3..HEAD
```

Expected: only the shader rename, the base-color shader/material changes, and the matching test/schema updates.

- [ ] **Step 4: Commit any verification-only cleanup if needed**

```bash
git add -A
git commit -m "test: finalize standard shader base color coverage"
```
