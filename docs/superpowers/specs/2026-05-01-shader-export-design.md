# Referenced Shader Export Design

**Goal:** Export only the shaders referenced by the selected scenes into the Windows build output, using the existing shader package format and runtime lookup path.

**Why this exists:** The Windows player already knows how to load shader packages from `shaders/{shaderId}.{target}.shader.asset`. The editor already compiles and caches shader packages. The missing piece is staging only the referenced shader packages into the final build root so the player can load them without carrying the whole shader cache.

## Scope

This change adds a shader export stage to the editor Windows build pipeline. It does not change shader compilation, shader authoring, runtime shader loading, or the Windows renderer backend. It only stages shader packages that are actually referenced by the packaged scene graph.

The exported build must remain target-specific. Windows builds export Windows shader packages, and future platform builds can export their own target packages through the same mechanism.

## Design

### 1. Shader export is a separate build stage

The Windows build flow will keep the existing responsibilities split:

- `EditorWindowsBuildScenePackager` rewrites selected scenes and resolves runtime asset references.
- A new shader export service stages the required shader packages into the final build root.
- `EditorWindowsBuildExecutor` orchestrates the order of those steps.

The new shader export service should not recompile shaders. It should copy or stage the already compiled shader packages from the editor shader cache into the deployment root.

Recommended service name:
- `EditorShaderPackageExportService`

### 2. Referenced shader discovery comes from packaged materials

The build pipeline already walks scene assets and rewrites file-backed material references. That is the right place to discover which shader assets are required.

Discovery rules:

- Every packaged `MaterialAsset` contributes its `ShaderAssetId`.
- Duplicate shader ids are deduplicated before export.
- Engine-generated standard materials are always included when the scene packager emits them.
- A missing or invalid `ShaderAssetId` on a material used by a packaged scene is a hard failure.

This keeps shader export tied to the actual scene dependency graph instead of the entire shader cache.

### 3. Export uses the existing package naming convention

The runtime loader already resolves shader packages by target and shader id. The export stage must write files into the final build root using that same convention:

- `shaders/{shaderId}.{target}.shader.asset`

The export stage should read from the editor shader cache using the same target-specific package path helper already used by the editor:

- `EditorProjectPaths.ShaderCache`
- `ShaderPackagePaths.GetPackagePath(...)`

If the compiled package does not exist in the cache, the build should fail immediately instead of skipping the shader.

### 4. Build orchestration order

The Windows build executor should follow this order:

1. Clean the deployment root.
2. Regenerate generated core C++.
3. Package selected scenes and rewrite their asset references.
4. Collect the shader ids referenced by packaged materials.
5. Export the referenced shader packages into `Build/shaders`.
6. Build the Windows host.
7. Copy the native build artifacts into the final build root.

This order keeps shader export aligned with the actual final asset tree and ensures the runtime player can see the exported shader packages when it boots.

### 5. Error handling

Shader export should fail fast and clearly.

Rules:

- A referenced shader package missing from the cache is a build failure.
- A referenced shader package with the wrong target is a build failure.
- A referenced material with no shader id is a build failure.
- Export should not silently skip a shader because that would produce a player that boots into runtime failures instead of surfacing the real asset problem during build.

### 6. Platform readiness

The export service should accept the active `ShaderCompileTarget` so the same mechanism can be reused by future platform builders.

That means:

- Windows uses `ShaderCompileTarget.DirectX11` today.
- Future Vulkan or console builds can reuse the same service with their own target.
- The service should not assume Windows-specific shader filenames beyond the existing target naming helper.

## Implementation Boundaries

The main files are expected to be:

- `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- `engine/helengine.editor/shaders/EditorShaderPackageExportService.cs`
- `engine/helengine.editor/shaders/ShaderPackagePaths.cs`
- `engine/helengine.editor/shaders/ShaderPackageBuilder.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

## Testing

Add or update tests to prove:

- one referenced material exports one shader package
- duplicate material references export one package only
- the exported package lands at the runtime path expected by the player
- a missing cached shader package fails the build
- the shader export stage is invoked as part of the Windows build pipeline

The tests should assert the exported file paths and failure modes, not internal implementation details.
