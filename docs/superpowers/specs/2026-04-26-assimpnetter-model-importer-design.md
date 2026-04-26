# AssimpNetter Model Importer Design

## Goal

Add a separate editor importer library that uses the Open Asset Import Library through AssimpNetter to import common 3D model formats into the engine's existing `ModelAsset` format.

The first implementation should make model files usable through the current asset import pipeline without changing the runtime renderer, scene format, material model, or mesh component contract.

## Scope

The importer will support the current single-buffer `ModelAsset` shape:

- `float3[] Positions`
- `float3[] Normals`
- `float2[] TexCoords`
- `ushort[] Indices16`

The importer will flatten all imported Assimp meshes into one `ModelAsset` when the total vertex and index counts fit the engine's current 16-bit index limit. It will reject files that cannot be represented correctly by this format.

Supported initial extensions will be:

- `.fbx`
- `.obj`
- `.gltf`
- `.glb`
- `.dae`
- `.3ds`

Out of scope for this pass:

- Materials
- Texture reference extraction
- Scene hierarchy
- Per-node transforms
- Animation
- Skeletons and skinning
- Multiple runtime mesh assets from one source file
- 32-bit index buffers

## Architecture

The existing `engine/helengine.editor.fbximporter` project will be treated as the starting point for the new importer library. It will be renamed at the project metadata and namespace level to represent a general Assimp importer rather than an FBX-only importer.

The library will reference AssimpNetter instead of the old `AssimpNet` package. It will still expose a normal editor `IModelImporter` implementation so the rest of the editor does not depend on Assimp APIs directly.

Primary classes:

- `HelengineAssimpImporter`: implements `IModelImporter` and converts streams to `ModelAsset`.
- `AssimpModelImporterRegistration`: supplies the importer id and supported extensions to `AssetImportManager`.
- Small converter classes if needed to keep Assimp scene traversal separate from validation and `ModelAsset` allocation.

The editor application will register this importer from `BuildImporters()` alongside the existing texture and text importers.

## Asset Pipeline Integration

`AssetImportManager` currently supports texture and text importer registration, default importer lookup, cache import, and lazy load. Model assets should follow the same pattern.

New manager behavior:

- Store model importers by id.
- Store default model importer ids by extension.
- Include model importers in `GetImporterIdsForExtension()`.
- Add `IsModelExtension()`.
- Add `SetDefaultModelImporter()`.
- Add `RegisterModelImporter()`.
- Add `ImportModel()`.
- Add `TryLoadModelAsset()`.
- Add cached model loading with `ModelAsset` payload validation.
- Add missing-cache model import scanning.

The editor session initialization should call the missing-cache model import pass after generating missing import settings, just as texture cache import is handled today.

## Import Behavior

The importer will use Assimp post-processing to produce data that fits the engine:

- Triangulate faces.
- Join identical vertices where Assimp can do so safely.
- Generate smooth normals when normals are missing.
- Generate UV coordinates where Assimp can provide them.
- Improve cache locality when available.
- Validate data after post-processing.

Conversion rules:

- Use mesh positions directly as `float3`.
- Use mesh normals directly when present.
- Use the first texture coordinate channel when present.
- Fill missing texture coordinates with `(0, 0)` only when positions and normals are otherwise valid. This preserves compatibility with renderers that require UV arrays matching positions.
- Build a single index buffer by offsetting each mesh's local indices by the accumulated vertex count.
- Throw when total vertices exceed `ushort.MaxValue + 1`.
- Throw when any face is not triangulated after post-processing.
- Throw when no meshes or no vertices are available.

## Error Handling

Import failures should be explicit. The importer should throw exceptions with actionable messages for:

- Null streams.
- Empty or invalid Assimp scenes.
- Missing mesh data.
- Unsupported vertex or index counts.
- Untriangulated faces after post-processing.
- Assimp native import failures.

The pipeline should not silently return placeholder meshes or default assets. A bad source asset should fail import so the editor can surface that problem instead of caching incorrect geometry.

## Testing

Tests should cover the editor-facing behavior first:

- Model importer registration rejects duplicate importer ids.
- Model extensions resolve to the model importer id list.
- Unsupported extensions return no importer ids.
- Import settings are generated for registered model extensions.
- `TryLoadModelAsset()` imports and caches a valid model.
- Cached model files are reloaded without reimporting when valid.
- Invalid cached payloads fail with an explicit exception.

Importer conversion tests should use small text fixtures where possible. OBJ is the best first fixture because it is readable and stable in source control. Binary FBX/GLB coverage can be added later when the asset model needs it.

## Open Decisions

No remaining open decisions are required for this implementation. The first pass intentionally targets the existing `ModelAsset` shape and defers richer scene import until the engine has asset types that can represent it.
