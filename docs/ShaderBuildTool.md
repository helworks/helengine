# Shader Build Tooling (Design)

This document defines the manifest format and CLI contract for an HLSL-first
shader build tool that emits backend binaries plus a unified reflection blob.

## Manifest Overview

The manifest is a single JSON file that describes input shaders, entry points,
targets, and permutations. The tool injects target macros (HEL_DX9, HEL_DX11,
HEL_DX12, HEL_VULKAN, HEL_METAL) automatically per target.

### Minimal Example

```json
{
  "version": 1,
  "root": "shaders",
  "includeDirs": ["Includes"],
  "output": {
    "binaryDir": "Compiled/Bin",
    "reflectionDir": "Compiled/Reflect",
    "codegenDir": "Compiled/Codegen",
    "moduleDir": "Compiled/Modules",
    "mslDir": "Compiled/MSL",
    "debugDir": "Compiled/Debug"
  },
  "targets": ["dx9", "dx11", "dx12", "vulkan", "metal"],
  "profiles": {
    "dx9": { "vertex": "vs_3_0", "pixel": "ps_3_0" },
    "dx11": { "vertex": "vs_5_0", "pixel": "ps_5_0" },
    "dx12": { "vertex": "vs_6_0", "pixel": "ps_6_0" },
    "vulkan": { "vertex": "vs_6_0", "pixel": "ps_6_0" },
    "metal": { "vertex": "vs_6_0", "pixel": "ps_6_0" }
  },
  "tools": {
    "fxc": "C:/Program Files (x86)/Windows Kits/10/bin/x64/fxc.exe",
    "dxc": "C:/Program Files/dxc/bin/dxc.exe",
    "spirvCross": "C:/Program Files/spirv-cross/spirv-cross.exe"
  },
  "shaders": [
    {
      "name": "BasicTextured",
      "file": "Samples/BasicTextured.hlsl",
      "entries": [
        { "stage": "vertex", "entry": "VSMain" },
        { "stage": "pixel", "entry": "PSMain" }
      ],
      "variants": [
        { "name": "Base", "defines": [] },
        { "name": "UseTint", "defines": ["USE_TINT=1"] }
      ]
    }
  ]
}
```

### Manifest Fields

- `version`: Integer schema version.
- `root`: Root folder for shader paths.
- `includeDirs`: Additional include directories, relative to `root`.
- `output`: Output directories for binaries/reflection/codegen/modules/metal/debug.
- `targets`: Target backends to build.
- `profiles`: Shader model/profile per target and stage.
- `tools`: Paths to external compiler tools.
- `shaders`: List of shader files and entry points.
- `variants`: Per-shader macro permutations.

## CLI Contract

The CLI supports a single binary named `helshader` with subcommands.

### Build

```
helshader build --manifest shaders/shader-manifest.json --all-targets
helshader build --manifest shaders/shader-manifest.json --target dx12
helshader build --manifest shaders/shader-manifest.json --target vulkan --variant Base
helshader build --manifest shaders/shader-manifest.json --file Samples/BasicTextured.hlsl --emit-modules
```

Flags:

- `--manifest`: Path to the JSON manifest.
- `--target`: Single target backend to build.
- `--all-targets`: Build all targets from the manifest.
- `--variant`: Build only a named variant.
- `--file`: Build only the shader that matches a manifest file path (relative to `root`).
- `--clean`: Rebuild all outputs.
- `--define`: Add a global define (e.g. `--define USE_FOG=1`).
- `--verbose`: Emit compiler command lines.
- `--emit-modules`: Generate C# shader modules and compile per-shader DLLs.

### Validate

```
helshader validate --manifest shaders/shader-manifest.json
```

Validation rules:

- Missing files, entry points, or include dirs fail validation.
- Duplicate bindings within a set/slot fail validation.
- DX9 builds reject unsupported features (compute, UAVs, SV_Position).

### Codegen Only

```
helshader codegen --manifest shaders/shader-manifest.json
```

The codegen command reads the intermediate reflection data and emits C# shader
modules that implement `IShaderModule`, without compiling binaries.

Note: `helshader build --emit-modules` expects reflection JSON files to be
available in the reflection output directory. If you use a separate reflection
step, ensure those files are present before emitting modules.

Example generated structure:

```csharp
public sealed class BasicTexturedModule : IShaderModule {
    public ShaderModuleDefinition BuildDefinition(string moduleRoot) {
        var programs = new[] {
            new ShaderProgramDefinition(
                "BasicTextured.VS",
                ShaderStage.Vertex,
                "VSMain",
                Array.Empty<ShaderBinding>(),
                Array.Empty<ShaderVertexElement>(),
                Array.Empty<ShaderVertexElement>(),
                Array.Empty<ShaderVariant>())
        };

        var binaries = new[] {
            new ShaderProgramBinary(
                "BasicTextured.VS",
                ShaderStage.Vertex,
                "dx12",
                "Base",
                Path.Combine(moduleRoot, "BasicTextured.VS.dx12.Base.bin"))
        };

        return new ShaderModuleDefinition("BasicTextured", programs, binaries);
    }
}
```

The module root passed to `BuildDefinition` is the directory containing the
module DLL, so generated binaries can be located with `Path.Combine`.

Generated module requirements:

- The module type is public, non-abstract, and has a public parameterless constructor.
- The module type name is `helengine.HelengineShaderModule`.
- Exactly one `IShaderModule` implementation is emitted per module DLL.

### Dump Reflection

```
helshader dump-reflect --input Compiled/Reflect/BasicTextured.VS.json
```

The dump command prints the unified reflection format for quick inspection.

## Output Layout

For each shader entry point and variant:

- `Compiled/Bin/<Name>.<Stage>.<Target>.<Variant>.bin`
- `Compiled/Reflect/<Name>.<Stage>.<Variant>.json`
- `Compiled/MSL/<Name>.<Stage>.<Variant>.msl` (Metal only)
- `Compiled/Debug/<Name>.<Stage>.<Target>.<Variant>.pdb` (optional)

For each shader module:

- `Compiled/Codegen/<Name>.shader.cs`
- `Compiled/Modules/<Name>.shader.dll`

## Build Notes

- DX9/DX11 use fxc; DX12/Vulkan use dxc; Metal uses SPIR-V + SPIRV-Cross.
- The tool emits reflection once per entry point and variant to keep runtime
  lookup simple and deterministic.
- Module DLLs contain `IShaderModule` implementations so the editor can load
  and unload shader metadata without runtime reflection.
- Each shader module is compiled as a separate DLL to enable editor hot-reload.
