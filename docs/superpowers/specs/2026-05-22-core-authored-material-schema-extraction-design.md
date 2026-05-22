# Core Authored Material Schema Extraction

## Goal

Keep `helengine.core` responsible only for generic authored material state while moving all platform-specific and shader-specific material meaning into the existing platform schema system.

## Problem

`MaterialAsset` in `helengine.core` still behaves like a shader-authored material:

- `ShaderAssetId`
- `VertexProgram`
- `PixelProgram`
- `Variant`
- `DiffuseTextureAssetId`
- `NormalTextureAssetId`
- `EmissiveTextureAssetId`
- `MaterialConstantBufferAsset[]`

These fields force one shader-shaped authored material model into core even though the repository already supports platform-owned material authoring and material cooking through generic platform schema metadata.

This creates the wrong ownership boundary:

- shader-capable platforms keep leaking authoring concepts into core
- fixed-function or cooked platforms still have to route around a shader-first material shape
- editor and packaging code mirror schema values back into core fields instead of treating schema data as the source of truth

## Existing generic seam

The repository already contains the right generic authoring seam outside `helengine.core`:

- `PlatformMaterialSchemaDefinition`
- `PlatformMaterialFieldDefinition`
- `PlatformMaterialCookRequest.SchemaId`
- `PlatformMaterialCookRequest.FieldValues`
- editor-side material schema settings documents and UI

That means the needed migration is not inventing a new abstraction. The needed migration is making the existing schema system the only owner of authored platform material meaning.

## Design

### Core ownership

`helengine.core` keeps `MaterialAsset` only as a generic authored material shell.

Core-owned material data should be limited to values that are genuinely cross-platform render state, such as:

- render-state settings
- generic shadow participation flags
- generic asset/base metadata inherited from `Asset`

`helengine.core` should not own any first-class authored texture slot, shader selection, program selection, variant selection, or shader constant-buffer payload fields.

### Schema-owned authoring

All platform-authored material meaning moves to the existing schema path.

That includes:

- diffuse/albedo texture references
- normal texture references
- emissive texture references
- shader asset selection
- vertex/pixel program selection
- variant selection
- colors and booleans that are only meaningful to one platform family
- packed constant/default values used by shader-capable platforms

These values are authored only through:

- selected schema id
- schema field values

The editor should treat platform schema settings as the source of truth rather than mirroring them into `MaterialAsset` fields.

### Storage model

The current editor-side material settings documents already persist:

- `SchemaId`
- `FieldValues`

This migration standardizes that path instead of introducing a second authored-material storage model.

The intended rule is:

- authored material semantic data lives in material schema settings
- core `MaterialAsset` does not duplicate that data
- builders consume schema data directly when creating cooked or raw runtime material outputs

### Builder ownership

Platform builders already consume `PlatformMaterialCookRequest`.

After the migration:

- all material cooking decisions come from `SchemaId` and `FieldValues`
- builders no longer depend on shader/program/texture fields stored directly on `MaterialAsset`
- shader-capable builders may still publish a `standard-shader` schema, but that schema belongs to the platform/shader layer rather than core

### Runtime impact

This migration does not add a runtime abstraction layer.

The schema path is editor/build-time only. Runtime performance stays on the current optimized seams:

- raw shader-backed material resolution for shader-capable platforms
- cooked platform-owned material resolution for fixed-function or opaque platform runtimes

The hot path still consumes runtime materials and cooked outputs, not schema dictionaries.

## Resulting boundary

After the migration:

- `helengine.core`
  - owns generic `MaterialAsset` shell state
  - owns generic render-state concepts
  - does not own shader-authored or platform-authored material semantics
- `helengine.shader`
  - owns shader-specific authored material expectations and translation logic where needed
- `helengine.baseplatform`
  - continues to own generic schema definitions and cook request contracts
- `helengine.editor`
  - owns schema-driven material authoring UI and settings persistence
- platform builders
  - own translation from schema field values into cooked/raw runtime material outputs

## Migration scope

The implementation should:

- remove shader/program/variant/texture/constant-buffer fields from `MaterialAsset`
- remove `MaterialConstantBufferAsset` from core if nothing generic still depends on it afterward
- stop mirroring schema values into core material fields in editor services
- update import, packaging, and build pipelines to read schema settings directly
- rewrite tests that currently assert shader-authored fields on `MaterialAsset`

## Testing

Focused validation should cover:

- material schema settings remain serializable and editable
- material cooking reads schema values without requiring old `MaterialAsset` shader fields
- shader-capable platforms still build runtime materials correctly from schema-driven authored data
- fixed-function or cooked platforms still cook materials correctly from schema-driven authored data
- `MaterialAsset` binary serialization still works after the field reduction

## Non-goals

- redesigning runtime material classes
- changing cooked runtime material performance characteristics
- introducing a new generic material technique abstraction in core
- removing generic render-state concepts from `MaterialAsset`
