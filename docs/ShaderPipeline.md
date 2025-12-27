# Shared Shader Pipeline (HLSL Canonical)

This document proposes a shared shader format based on HLSL source that can
produce binaries for DirectX 9.0c, DirectX 11, DirectX 12, Vulkan, and Metal.
It defines the authoring subset, the build pipeline, and a unified reflection
schema used by the engine at runtime.

## Goals

- Single source of truth (HLSL) with controlled, portable features.
- Deterministic resource binding across APIs.
- Explicit metadata generated at build time with no runtime reflection.
- Variant/permutation support without cloning shader files.
- Clear error reporting and validation during build.

## High-Level Approach

- Author in HLSL.
- Compile to backend-specific binaries.
- Emit generated C# code per shader module so runtime never parses reflection.

Targets and compilers:

- DX9: HLSL -> fxc -> DX9 bytecode (SM2/SM3)
- DX11: HLSL -> fxc or dxc -> DXBC (SM4/SM5)
- DX12: HLSL -> dxc -> DXIL (SM6)
- Vulkan: HLSL -> dxc -> SPIR-V
- Metal: HLSL -> dxc -> SPIR-V -> SPIRV-Cross -> MSL

## No Runtime Reflection

The engine and editor never parse reflection data at runtime. The build tool
uses reflection only as an intermediate step to generate C# shader modules.

## Authoring Rules (Portable HLSL Subset)

These constraints keep a single source viable for all targets:

- Use `float`, `float2/3/4`, `int`, `uint`, `bool` only.
- Use `row_major` for matrices to match CPU layout and reduce ambiguity.
- No wave ops, mesh shaders, or ray tracing constructs.
- No UAVs on DX9. Guard UAV usage with `#if HEL_DX11_PLUS`.
- No compute shaders on DX9. Guard CS entry points with `#if HEL_DX11_PLUS`.
- Avoid unbounded loops; loops must have static bounds for DX9.
- Avoid dynamic indexing into arrays unless guarded for DX11+.
- Texture and sampler declarations must use macros (see below).
- Constant buffers must be declared with macros to support DX9.
- No HLSL `struct` packing assumptions; use explicit padding when needed.

Recommended file layout:

- `shaders/` for shader source files.
- `shaders/Includes/` for shared macro headers.
- `shaders/Compiled/` for build outputs (not committed).

## Binding Model

All resources are assigned a logical binding: `(set, slot, type)`.
Backends map these to native binding systems.

- `set` is the descriptor set / space.
- `slot` is the binding within the set.

Mapping:

- DX12: `space = set`, `register = slot`
- Vulkan: `[[vk::binding(slot, set)]]`
- DX11/DX9: `set` is ignored, `register = slot`

DX9 requires separate resource/sampler registers; use macros to keep names
consistent and avoid duplicate declarations.

## Required Shader Macros

Create `shaders/Includes/ShaderCommon.hlsl` and include it in every shader.
This header defines resource declaration macros that expand per target.

Example macro design:

```hlsl
// Target defines injected by the build pipeline:
// HEL_DX9, HEL_DX11, HEL_DX12, HEL_VULKAN, HEL_METAL

#if defined(HEL_VULKAN)
  #define HEL_VK_BINDING(set, slot) [[vk::binding(slot, set)]]
#else
  #define HEL_VK_BINDING(set, slot)
#endif

#if defined(HEL_DX12)
  #define HEL_DX_REGISTER(reg, set) register(reg, space##set)
#else
  #define HEL_DX_REGISTER(reg, set) register(reg)
#endif

#if defined(HEL_DX9)
  #define HEL_POSITION_SEMANTIC POSITION
  #define HEL_TARGET_SEMANTIC COLOR0
  #define HEL_CBUFFER_BEGIN(name, set, slot) struct name##_Type {
  #define HEL_CBUFFER_END(name, set, slot) } name : HEL_DX_REGISTER(c##slot, set)
  #define HEL_TEXTURE2D(name, set, slot) sampler2D name : HEL_DX_REGISTER(s##slot, set)
  #define HEL_SAMPLER(name, set, slot)
  #define HEL_TEXTURE2D_SAMPLE(tex, samp, uv) tex2D(tex, uv)
#else
  #define HEL_POSITION_SEMANTIC SV_POSITION
  #define HEL_TARGET_SEMANTIC SV_Target
  #define HEL_CBUFFER_BEGIN(name, set, slot) \
    HEL_VK_BINDING(set, slot) cbuffer name : HEL_DX_REGISTER(b##slot, set) {
  #define HEL_CBUFFER_END(name, set, slot) }
  #define HEL_TEXTURE2D(name, set, slot) \
    HEL_VK_BINDING(set, slot) Texture2D name : HEL_DX_REGISTER(t##slot, set)
  #define HEL_SAMPLER(name, set, slot) \
    HEL_VK_BINDING(set, slot) SamplerState name : HEL_DX_REGISTER(s##slot, set)
  #define HEL_TEXTURE2D_SAMPLE(tex, samp, uv) tex.Sample(samp, uv)
#endif
```

Notes:

- DX9 uses `sampler2D` and samples with `tex2D`.
- DX11+ uses `Texture2D` and `SamplerState`.
- `HEL_POSITION_SEMANTIC` and `HEL_TARGET_SEMANTIC` keep semantics portable.
- The build pipeline injects the target defines.

## Vertex Input and Semantics

- Standardize semantics across all shaders:
  - POSITION, NORMAL, TANGENT, COLOR0, TEXCOORD0..7
- Avoid `SV_VertexID` on DX9; guard any use with `#if HEL_DX11_PLUS`.
- When required, provide a DX9-compatible path for shader features.

## Build Pipeline

Inputs:

- HLSL source file path
- Entry point(s) and stage(s)
- Target list (DX9, DX11, DX12, Vulkan, Metal)
- Variant definitions (macro sets)

Steps:

1. Preprocess HLSL with target defines and variant macros.
2. Compile to backend-specific bytecode.
3. Reflect compiled output to extract:
   - Resource bindings
   - Constant buffer layouts
   - Input/output signatures
4. Generate C# source that encodes the metadata into `IShaderModule`.
5. Emit:
   - Backend binary
   - Generated C# source
   - Per-shader module DLL (editor workflow)
   - Optional debug symbols

Outputs per shader entry point:

- `*.dx9.bin`
- `*.dx11.bin`
- `*.dx12.bin`
- `*.spirv`
- `*.msl`

Outputs per shader module:

- `*.shader.cs` (generated module code)
- `*.shader.dll` (editor workflow)

## Editor Workflow (Dynamic Modules)

1. Watch shader source files for changes.
2. Run `helshader build --emit-modules` on the touched shader.
3. Load the new module DLL and read `ShaderModuleDefinition`.
4. Unload the previous module DLL when it is no longer needed.
5. Editor resolves the helshader tool path from `HELENGINE_SHADER_TOOL`.

## Intermediate Reflection Schema (Tooling Only)

This schema is used only by the build tool to generate C# code. It is not
consumed by the runtime or editor at execution time.

```json
{
  "name": "LightingPS",
  "stage": "pixel",
  "entryPoint": "PSMain",
  "targets": ["dx9", "dx11", "dx12", "vulkan", "metal"],
  "bindings": [
    {
      "name": "Frame",
      "type": "cbuffer",
      "set": 0,
      "slot": 0,
      "size": 256,
      "members": [
        { "name": "ViewProj", "type": "float4x4", "offset": 0 },
        { "name": "CameraPos", "type": "float3", "offset": 64 }
      ]
    },
    {
      "name": "Albedo",
      "type": "texture2d",
      "set": 0,
      "slot": 1
    },
    {
      "name": "LinearSampler",
      "type": "sampler",
      "set": 0,
      "slot": 1
    }
  ],
  "inputs": [
    { "semantic": "POSITION", "index": 0, "format": "float3" },
    { "semantic": "TEXCOORD", "index": 0, "format": "float2" }
  ],
  "outputs": [
    { "semantic": "SV_Target", "index": 0, "format": "float4" }
  ],
  "variants": [
    { "name": "UseNormalMap", "defines": ["USE_NORMAL_MAP=1"] }
  ]
}
```

## Variant System

- Variants are driven by compile-time `#define` sets.
- The build tool should generate a permutation matrix and enforce limits.
- Each permutation produces its own binaries and reflection.

## Engine Integration Notes

- The engine consumes generated `IShaderModule` implementations to build:
  - Root signature (DX12)
  - Descriptor set layouts (Vulkan)
  - Shader resource binding tables (DX9/DX11)
- Editor loads per-shader DLLs via an unloadable load context.
- Editor should read `ShaderModuleDefinition` from the loaded module and then
  release the module instance to allow unloading.
- Runtime builds can compile generated C# into the game assembly (no reflection).
- Validate runtime bindings against the generated metadata and fail fast when a
  required resource is missing or mismatched.

## Suggested Next Steps

- Implement `ShaderCommon.hlsl` in `shaders/Includes/`.
- Build a shader compiler tool that:
  - Accepts a JSON manifest of shaders and permutations.
  - Emits binaries + generated C# modules.
- Add an editor-only loader that dynamically loads per-shader module DLLs.
