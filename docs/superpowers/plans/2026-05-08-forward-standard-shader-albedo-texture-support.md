# Forward Standard Shader Albedo Texture Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the built-in standard forward shader sample one albedo texture through the existing material texture-binding path while keeping untextured standard content working.

**Architecture:** The change stays narrow. Update `ForwardStandardShader` to declare one `DiffuseTexture` binding and consume UVs in the pixel shader, update the built-in shader contract tests and generated standard material path to match that binding, and verify the existing DirectX11 runtime texture-binding path still resolves the first material texture cleanly. No material-system redesign, no normal/emissive support, and no shadow-pass changes in this plan.

**Tech Stack:** C#, HLSL, xUnit, DirectX11 runtime material binding, built-in shader asset library

---

## File Structure

- `engine/helengine.editor/shaders/builtin/ForwardStandardShader.hlsl`
  - Built-in standard forward shader. This is where the albedo texture binding, UV passthrough, and pixel-shader sampling will be added.
- `engine/helengine.editor.tests/shaders/ForwardStandardShaderTests.cs`
  - Shader contract tests. This is where the standard shader layout assertions will be updated from “no texture bindings” to “one DiffuseTexture binding”.
- `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`
  - Built-in generated standard material builder. This is where the generated standard material asset should explicitly align with the updated shader contract if any authored texture-related defaults are required.
- `engine/helengine.editor.tests/rendering/DirectX11MaterialFeatureBindingTests.cs`
  - DirectX11 runtime material binding tests. This is where one focused regression should verify that a standard-material-shaped layout with `DiffuseTexture` still resolves the bound texture resource through the existing DX11 path.

### Task 1: Lock the Shader Contract in Tests First

**Files:**
- Modify: `engine/helengine.editor.tests/shaders/ForwardStandardShaderTests.cs`

- [ ] **Step 1: Write the failing shader-layout assertions**

Update the existing test helper assertions so the built-in shader is expected to expose one `DiffuseTexture` texture binding instead of an empty texture-binding list:

```csharp
Assert.Single(layout.TextureBindings);
Assert.Equal("DiffuseTexture", layout.TextureBindings[0].Name);
Assert.Equal(ShaderResourceType.Texture2D, layout.TextureBindings[0].Type);
Assert.Equal(0, layout.TextureBindings[0].Set);
Assert.Equal(0, layout.TextureBindings[0].Slot);
Assert.Empty(layout.ConstantBufferBindings);
Assert.Empty(layout.SamplerBindings);
```

- [ ] **Step 2: Run the shader test to verify it fails**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.shaders.ForwardStandardShaderTests"
```

Expected: FAIL because `ForwardStandardShader` currently exposes zero texture bindings.

- [ ] **Step 3: Commit the failing test change**

```powershell
git add engine/helengine.editor.tests/shaders/ForwardStandardShaderTests.cs
git commit -m "test: require forward standard shader diffuse texture binding"
```

### Task 2: Add Albedo Sampling to the Built-In Standard Shader

**Files:**
- Modify: `engine/helengine.editor/shaders/builtin/ForwardStandardShader.hlsl`
- Review: `engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs`

- [ ] **Step 1: Update the shader declaration and vertex output**

Add the diffuse texture and sampler declarations and pass UVs through the shader stages:

```hlsl
Texture2D DiffuseTexture : register(t0);
SamplerState DiffuseTextureSampler : register(s0);

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float2 texCoord : TEXCOORD2;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    float4 worldPosition = mul(float4(input.pos, 1.0f), world);
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.worldPos = worldPosition.xyz;
    output.normal = mul(float4(input.normal, 0.0f), normalMatrix).xyz;
    output.texCoord = input.texCoord;
    return output;
}
```

- [ ] **Step 2: Run the shader test to confirm it still fails on layout only**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.shaders.ForwardStandardShaderTests"
```

Expected: still FAIL until the layout built from the compiled shader now reports the new `DiffuseTexture` binding correctly.

- [ ] **Step 3: Replace the hardcoded surface color with sampled albedo**

Change the pixel shader surface-color setup from a fixed constant to a sampled texture value:

```hlsl
float4 PS(PS_IN input) : SV_Target
{
    float3 sampledAlbedo = DiffuseTexture.Sample(DiffuseTextureSampler, input.texCoord).rgb;
    float3 surfaceColor = sampledAlbedo;
    float3 ambientColor = float3(0.12f, 0.13f, 0.15f);
    float3 normal = normalize(input.normal);
    float3 viewDirection = normalize(cameraPosition.xyz - input.worldPos);
    float3 color = surfaceColor * ambientColor;
```

Do not change the rest of the light evaluation structure in this step.

- [ ] **Step 4: Review the generated standard material builder and keep it minimal**

Open `EngineGeneratedMaterialCache.cs` and confirm the built-in standard material still builds via:

```csharp
var materialAsset = new MaterialAsset {
    Id = StandardMaterialAssetId,
    ShaderAssetId = shaderAsset.Id,
    VertexProgram = StandardVertexProgramName,
    PixelProgram = StandardPixelProgramName,
    Variant = DefaultVariantName
};
```

Only change this file if the updated shader contract requires an explicit material-asset field for the generated standard material. Do not add unrelated material-system logic.

- [ ] **Step 5: Run the shader-layout tests to verify they pass**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.shaders.ForwardStandardShaderTests"
```

Expected: PASS with the shader now exposing one `DiffuseTexture` texture binding.

- [ ] **Step 6: Commit the shader contract and albedo-sampling change**

```powershell
git add engine/helengine.editor/shaders/builtin/ForwardStandardShader.hlsl engine/helengine.editor/managers/asset/EngineGeneratedMaterialCache.cs engine/helengine.editor.tests/shaders/ForwardStandardShaderTests.cs
git commit -m "feat: add albedo texture support to forward standard shader"
```

### Task 3: Verify the Existing DX11 Material Texture Path Still Supports the Standard Shader

**Files:**
- Modify: `engine/helengine.editor.tests/rendering/DirectX11MaterialFeatureBindingTests.cs`

- [ ] **Step 1: Write the failing DirectX11 texture-binding regression**

Add a focused regression that uses the standard shader binding name instead of the canvas-preview binding name:

```csharp
[Fact]
public void ResolveMaterialTextureResourceView_WhenMaterialUsesDiffuseTextureBinding_ReturnsRenderTargetShaderResourceView() {
    RuntimeMaterial material = CreateDiffuseTexturedMaterial();
    DirectX11RenderTargetResource renderTarget = (DirectX11RenderTargetResource)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11RenderTargetResource));
    ShaderResourceView expectedResourceView = (ShaderResourceView)RuntimeHelpers.GetUninitializedObject(typeof(ShaderResourceView));
    TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
    MethodInfo method = typeof(DirectX11Renderer3D).GetMethod("ResolveMaterialTextureResourceView", BindingFlags.Instance | BindingFlags.NonPublic);

    Assert.NotNull(method);

    SetAutoPropertyBackingField(renderTarget, "ShaderResourceView", expectedResourceView);
    material.Properties.SetTexture("DiffuseTexture", renderTarget);

    ShaderResourceView resourceView = Assert.IsType<ShaderResourceView>(method.Invoke(renderer, new object[] { material }));

    Assert.Same(expectedResourceView, resourceView);
}
```

Add the helper beside the existing one:

```csharp
static RuntimeMaterial CreateDiffuseTexturedMaterial() {
    RuntimeMaterial material = new RuntimeMaterial();
    material.SetLayout(new MaterialLayout(
        "shader/test",
        "VS",
        "PS",
        "default",
        new MaterialRenderState(),
        new[] {
            new MaterialLayoutBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 0, 0)
        },
        Array.Empty<MaterialLayoutBinding>(),
        Array.Empty<MaterialLayoutBinding>()));
    return material;
}
```

- [ ] **Step 2: Run the focused DX11 material-binding tests**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.rendering.DirectX11MaterialFeatureBindingTests"
```

Expected: PASS if the existing DX11 material texture path already handles the standard shader’s first texture binding cleanly. If it fails, use that failure to make the smallest DX11 runtime fix necessary.

- [ ] **Step 3: Run the combined focused verification slice**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.shaders.ForwardStandardShaderTests|FullyQualifiedName~helengine.editor.tests.rendering.DirectX11MaterialFeatureBindingTests"
```

Expected: PASS for both the shader contract and the DX11 material texture binding path.

- [ ] **Step 4: Commit the DX11 verification coverage**

```powershell
git add engine/helengine.editor.tests/rendering/DirectX11MaterialFeatureBindingTests.cs
git commit -m "test: cover forward standard shader diffuse texture binding"
```

## Self-Review

- Spec coverage:
  - albedo-only texture support in the built-in shader: covered by Task 2
  - generated standard material alignment: covered by Task 2
  - DirectX11 texture binding verification: covered by Task 3
  - focused verification only: covered by Tasks 1 and 3
- Placeholder scan:
  - no `TODO`, `TBD`, or “implement later” markers remain
  - each code-changing step contains concrete code
  - each verification step contains an exact command
- Type consistency:
  - binding name is consistently `DiffuseTexture`
  - shader resource type is consistently `Texture2D`
  - DirectX11 verification uses the existing `ResolveMaterialTextureResourceView` seam
