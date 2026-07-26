# Platform Material and Shader Artifact Contract

## Problem

Platform material cooking already returns two distinct values: cooked material bytes and referenced shader asset identifiers. The editor packaging pipeline writes the material bytes to disk but does not preserve their identity. It then scans the cooked directory and tries to infer each artifact kind from a path or serialized payload.

This loses the material/shader boundary. A platform material can be represented by arbitrary platform-owned data, including fixed-function state, VU configuration, or a reference to a separately compiled shader. It must never be interpreted as a shader simply because of its location or binary header.

The current PS2 failure demonstrates the problem: a PS2 material payload uses a header format identifier that the generic shader-material serializer also recognizes, so payload classification tries to deserialize the PS2 material as a generic shader material.

## Goals

- Materials and shaders are independent manifest artifacts with separate logical identities and output paths.
- A material cook declares its material output explicitly.
- A platform declares shader outputs explicitly only when that platform uses shaders.
- PSP and PS2 can declare material outputs without shader outputs.
- Artifact collection does not inspect material or shader payloads, and does not use paths to decide whether an artifact is a material or shader.
- Existing non-material, non-shader artifact discovery remains unchanged.

## Non-goals

- Defining a universal shader format for all platforms.
- Replacing platform-specific material serializers.
- Requiring a shader association for every material.
- Reinterpreting VU or fixed-function settings as shader files.

## Architecture

### Explicit output declarations

Introduce one immutable output declaration type for cooked runtime artifacts. It records:

- runtime-relative output path;
- stable logical artifact identifier;
- artifact kind (`material` or `shader`);
- optional variant identifier when the platform needs one.

The declaration describes an already-written artifact. It does not describe compilation work and does not require the artifact bytes to share a common serialization format.

`EditorPlatformBuildScenePackagerResult` will carry these declarations separately from deferred `PlatformCookWorkItem` entries. The scene packager and its component-transform service will report every material file they write. Shader staging/compilation will report every shader file it writes.

### Material cooking

`PlatformMaterialCookResult` remains the platform boundary for material bytes and shader dependencies. Its caller is responsible for writing the returned material bytes and immediately recording the matching material output declaration.

The returned shader asset identifiers are dependencies, not artifact declarations. They identify shader inputs that a shader staging/compilation step may resolve for a platform. A platform that has no shader implementation returns an empty dependency list and still emits its material declaration.

### Shader staging and compilation

The Vita shader compiler resolves the complete requested dependency set in one batch and emits one versioned shader bundle file. The bundle is a separate `shader` artifact. Its internal index maps each shader asset ID, program, variant, and source hash to the compiled Vita vertex and fragment program bytes plus compiler metadata.

The generated shader `.hasset` is the authoritative shader-asset identity. Materials persist that shader asset ID. For project shaders, the editor scans `.hlsl` files anywhere beneath the project assets root and uses the existing shader-ID derivation rule to find the source whose generated `.hasset` has the requested ID. Cache metadata validates the source hash; built-in shaders use the built-in shader source registry. This does not require a project folder convention. The compiler validates that every material-reported ID received a bundle entry. A dependency ID alone is not a manifest artifact; the bundle is the artifact. PSP and PS2 do not produce a bundle.

Materials retain shader asset IDs, program names, variants, and parameter bindings only. Vita material payloads must not make compiled stage hashes authoritative; the runtime resolves the material's shader ID through the bundle index.

### Manifest collection

`EditorPlatformAssetCookService.BuildCookedArtifacts` receives the declared material and shader outputs. It adds those entries to the artifact pool with their declared kind and skips their paths during directory scanning.

The scanner remains responsible only for files not declared by the platform/material/shader pipeline. It may retain generic serialized-asset classification for those other editor-owned assets, but it must not be invoked for declared materials or shaders.

## Data flow

```text
authored material + generated shader assets
  -> platform material cook
     -> cooked material bytes -> write material file -> material output declaration
     -> shader dependency IDs -> resolve shader hasset/cache metadata -> Vita batch compiler
                              -> one shader bundle -> shader output declaration
  -> declared outputs + other cooked files
     -> manifest artifact collection
```

For PSP and PS2, the shader-dependency branch is empty. Their material artifact may contain fixed-function or VU-specific data and is still declared as `material`.

## Error handling

- A declared path must be non-empty, normalized, unique, and exist beneath the cook root when the manifest is collected.
- Duplicate declared paths with different kind, logical ID, or variant are build errors.
- A shader dependency that the selected platform declares as supported but cannot compile into the bundle is a build error.
- A bundle entry must retain shader asset ID, source hash, stage-program metadata, compiler version, and compiled program bytes.
- A referenced shader ID with no generated shader asset/cache metadata source mapping is a build error that names the unresolved ID.
- A platform without shaders does not fail for an empty shader dependency list.
- Generic payload classification errors remain failures only for undeclared assets that actually use generic serialization.

## Tests

- A platform-owned material with arbitrary bytes is listed as a `material` solely from its declaration.
- A platform-owned material whose binary header collides with the generic shader-material format is still listed as `material` and is never deserialized by generic asset code.
- A declared Vita shader bundle is listed as `shader` independently from its referencing materials.
- A user-authored shader source compiles into an entry keyed by its shader asset ID and source hash in the Vita shader bundle.
- A PSP/PS2-style material declaration with no shader outputs succeeds.
- Conflicting duplicate declarations fail with an actionable error.
- Existing generic model, audio, scene, font, and undeclared editor-asset classification tests continue to pass.
