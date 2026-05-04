# Multiplatform Materials Design

## Summary

This document defines a material-authoring architecture for platforms with fundamentally different rendering models.

HelEngine should not treat materials as one shared shader-centric asset that every platform must reinterpret. Instead, materials should follow the same per-platform settings model used elsewhere in the engine. The material asset becomes a thin shared shell, while the real authored material payload lives in per-platform settings and is defined by the active platform builder through dynamic schemas.

This allows shader-driven platforms to expose shader-oriented material fields and fixed-pipeline platforms to expose completely different render-mode and state fields without forcing fake shared semantics like universal color, shader, or texture concepts.

## Goals

- Make material authoring explicitly per-platform.
- Keep materials aligned with the engine-wide rule that every asset has per-platform settings.
- Let platform builders define material schemas dynamically.
- Allow shader platforms and fixed-pipeline platforms to expose different material concepts.
- Keep the editor generic and schema-driven instead of hardcoding platform material logic.
- Ensure cooking reads only the target platform's material settings and emits platform-native runtime material outputs.

## Non-Goals

- No universal cross-platform material property model.
- No required shared concepts like `Color`, `Shader`, `TextureMode`, or `LightingMode`.
- No material-only settings architecture separate from the existing per-platform asset settings model.
- No best-effort fallback when a platform material schema is missing or invalid.
- No requirement that the first PS2 slice fully solve final production material coverage for every render mode.

## Current Problem

`MaterialAsset` is currently shader-centric. It directly stores fields such as:

- `ShaderAssetId`
- `VertexProgram`
- `PixelProgram`
- `Variant`
- `ConstantBuffers`

That shape assumes materials fundamentally mean "choose a shader and configure shader data." That can work for shader platforms, but it does not describe fixed-pipeline targets well.

At the same time, the engine already has a pattern where assets carry per-platform processor settings through `AssetImportSettings` and `AssetPlatformProcessorSettings`. Materials are the first asset type that needs to lean heavily on that pattern instead of treating it as a small override layer.

The current architecture therefore has two problems:

- it bakes shader semantics into the material asset itself
- it bypasses the engine's broader per-platform settings model

## Proposed Architecture

### 1. Materials Follow The Existing Per-Platform Asset Settings Model

Materials should not invent their own variant container separate from the rest of the asset pipeline.

`MaterialAsset` should be treated as an asset whose authored meaning is effectively per-platform. The canonical authored payload should live in that asset's per-platform settings entry, using the same `AssetImportSettings` and `AssetPlatformProcessorSettings` model already used for other assets.

This means the engine-wide pattern stays consistent:

- every asset has per-platform settings
- materials use that mechanism much more heavily than models do
- the base asset remains thin and shared

### 2. Platform Builders Publish Material Schemas

The platform builder should define what a valid material looks like for that platform.

This should not be represented by a small fixed list of engine-defined archetypes. It should be a fully dynamic schema published by the builder and consumed by the editor.

Shader platforms may expose schemas with fields for:

- shader asset selection
- vertex program
- pixel program
- shader variant
- texture bindings
- constant-buffer or parameter payloads

Fixed-pipeline platforms may expose schemas with fields for:

- render mode
- lighting flags
- texture usage
- combine behavior
- fog mode
- vertex color behavior

The editor should not assume those are equivalent concepts.

### 3. Dedicated Material Schema Metadata

The existing `PlatformSettingDefinition` type is too narrow for material authoring. It is designed for simple build/profile settings and only supports:

- `Boolean`
- `Text`
- `Choice`

Material authoring will need richer field kinds. The platform metadata model should therefore grow dedicated material-schema types rather than overloading build/profile settings.

Expected material-schema metadata should support at least:

- schema identity
- display name
- field identity
- field display name
- field kind
- required flag
- default value
- allowed values when the field is closed-choice

The field-kind model should be open to richer material types such as:

- boolean
- text
- choice
- numeric scalar
- asset reference
- color or vector payload

Grouped fields or conditional visibility can remain future extensions, but the schema model must not block them.

### 4. Material Data Lives In Per-Platform Processor Settings

`AssetPlatformProcessorSettings` should gain a material payload branch alongside `Model`.

That material payload should store:

- selected material schema id
- material field values keyed by field id
- any builder-required auxiliary metadata needed for validation or cooking

The editor should persist these values as asset settings, not as hardcoded top-level `MaterialAsset` shader fields.

### 5. Editor Is Generic, Builders Own Meaning

The editor should:

- load the builder-published material schemas for the active platform
- render schema-driven controls
- validate field presence and type shape
- persist per-platform material settings

The editor should not:

- decide what a PS2 material means
- translate fixed-pipeline modes into shader assumptions
- invent fallback schemas when a builder provides none

The builder should:

- define schemas
- define what field values mean
- validate final semantic correctness
- translate authored data into cooked runtime material outputs

### 6. Cooking Reads Only The Target Platform Variant

When building for platform `X`, the cook/build path must read only platform `X`'s material settings.

The build should not merge concepts from other platforms and should not attempt cross-platform value sharing.

This keeps ownership correct:

- authoring storage is per-platform
- validation is per-platform
- cooked output is per-platform
- runtime only consumes cooked platform-native material assets

## Components And Data Flow

The expected component split is:

- `AssetImportSettings` remains the canonical per-platform settings container for all assets.
- `AssetPlatformProcessorSettings` gains a material payload branch alongside `Model`.
- `PlatformDefinition` gains builder-published material schema metadata.
- `EditorPlatformBuildSelectionModel` exposes material schemas to editor UI in the same way it already exposes build and graphics profile metadata.
- `MaterialAssetView` becomes schema-driven instead of being a hardcoded shader picker.
- the build/cook path translates the target platform's material settings into runtime material outputs.

### Authoring Flow

1. The platform builder loads and exposes material schemas for its platform.
2. The editor selects an active platform tab.
3. `MaterialAssetView` resolves the material schemas for that platform.
4. The user selects a schema and edits its fields.
5. The editor stores the selected schema id and field values into that asset's per-platform settings entry.

### Build Flow

1. The editor selects a target platform for the build.
2. The build pipeline reads that material asset's per-platform settings for the target platform only.
3. The builder validates the selected schema id and field payload.
4. The builder cooks that data into platform-native runtime material output.
5. Scene packaging and runtime loading consume the cooked output rather than the original authoring schema.

## Editor Behavior

`MaterialAssetView` should stop assuming a material is edited through one shader field plus derived vertex/pixel program names.

The view should instead:

- show platform tabs using the same broad editor pattern as other per-platform settings
- show builder-published material schemas for the selected platform
- let the user choose one schema for that platform
- render field editors dynamically from the schema metadata
- preserve independent values per platform tab

If a platform does not publish any material schemas, the editor should say so clearly instead of showing fake default controls.

## Build And Packaging Behavior

Scene and asset packaging should stop assuming that file-backed material assets are copied forward as universal runtime-ready payloads.

The packaging/build path should:

- load the authored material plus its per-platform settings
- select the target platform's material payload
- validate it through the builder contract
- emit cooked material output in the platform's native runtime shape

Shader dependency tracking should also move away from assuming every material always exposes top-level shader ids. Shader platforms can still emit shader dependencies, but fixed-pipeline platforms should not be forced into the same dependency model.

## Validation Rules

Validation should be strict.

Authoring-time validation:

- a material edited for platform `X` must reference a schema published by platform `X`
- required fields must be present
- field values must match the declared field kind
- blank or duplicate field ids must be rejected in schema metadata

Build-time validation:

- building platform `X` must fail if platform `X` has no usable material settings for a required material
- stale schema ids must fail with clear diagnostics
- removed required fields must fail with clear diagnostics
- final semantic validation belongs to the builder because only the builder knows what the schema means

The system must not silently construct defaults for required material data.

## Migration Strategy

The current shader-centric `MaterialAsset` fields should not remain the long-term canonical representation.

The first migration slice should:

- introduce builder-backed shader-material schemas for current shader platforms
- map existing shader-centric material data into those schemas
- persist material authoring through per-platform settings
- leave runtime/build code able to consume migrated shader-platform material data without depending on top-level shader fields as the permanent source of truth

Migration should be explicit and versioned. Asset-settings serialization should be updated so per-platform material data round-trips cleanly and old payloads fail clearly when unsupported.

## Testing Requirements

Implementation must include coverage for three layers.

### 1. Schema Metadata

- builders expose material schemas correctly
- schema ids are stable and unique per platform
- invalid schema definitions fail fast

### 2. Editor Authoring

- `AssetImportSettings` round-trips per-platform material settings
- `MaterialAssetView` renders builder-published schemas for the active platform
- switching platform tabs preserves independent material values
- invalid schema references surface clear errors instead of silent resets

### 3. Build And Cook Integration

- the build reads only the target platform's material settings
- shader platforms cook shader-backed materials from schema data
- fixed-pipeline platforms cook non-shader material outputs from schema data
- missing target-platform material settings fail the build clearly

## Recommendation

Implement multiplatform materials as an extension of the existing per-platform asset settings architecture, not as a new material-only variant system.

Platform builders should publish dedicated material schemas. The editor should render and persist those schemas generically. Cooking should consume only the selected target platform's material payload and emit platform-native runtime material output.

This keeps the engine architecture honest:

- materials are truly per-platform
- the editor stays generic
- builders own rendering semantics
- fixed-pipeline targets are not forced into shader-shaped abstractions
