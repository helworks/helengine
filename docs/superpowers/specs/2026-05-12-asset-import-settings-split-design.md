# Asset Import Settings Split Design

## Goal

Replace the current mixed `AssetImportSettings` sidecar schema with separate asset-kind-specific sidecar models so each source asset stores only the importer and processor settings that actually apply to that asset kind.

This is an intentional schema break. Existing `.hasset` import-settings payloads do not need to remain readable.

## Problem

The current editor import-settings model stores one generalized processor container per asset:

- `AssetImportSettings`
- `AssetProcessorSettings`
- `AssetPlatformProcessorSettings`
  - `Texture`
  - `Model`
  - `Material`

That shape has three problems:

1. Texture assets carry model and material settings slots that are meaningless for textures.
2. Model assets carry texture and material settings slots that are meaningless for models.
3. The editor, serializer, and import manager all need to understand a schema that is broader than the actual asset being edited.

The result is unnecessary branching, extra validation noise, and a sidecar schema that does not describe the asset clearly.

## Requirements

- Keep one sidecar file per source asset using the existing `<source>.hasset` convention.
- Split the sidecar payloads by asset kind.
- Keep importer settings in each sidecar because importer selection is still asset-specific.
- Keep per-platform processor settings, but only for the relevant asset kind.
- Allow hard schema breakage with no backward-compatibility reader.
- Preserve current behavior for:
  - model flip-winding settings
  - texture max-resolution settings
  - material schema and field-value settings
- Keep asset-id generation deterministic and tied to the relevant processor settings only.

## Proposed Shape

### Shared Concepts

The file path convention stays the same, but the payload type depends on the asset kind:

- texture source asset -> `TextureAssetImportSettings`
- model source asset -> `ModelAssetImportSettings`
- material source asset -> `MaterialAssetImportSettings`

Each asset-kind-specific model contains:

- one importer settings object
- one platform settings map for the relevant processor settings type

### Texture Settings Model

`TextureAssetImportSettings`

- `Importer: AssetImporterSettings`
- `Processor: TextureAssetProcessorPlatformSettings`

`TextureAssetProcessorPlatformSettings`

- `Platforms: Dictionary<string, TextureAssetProcessorSettings>`

### Model Settings Model

`ModelAssetImportSettings`

- `Importer: AssetImporterSettings`
- `Processor: ModelAssetProcessorPlatformSettings`

`ModelAssetProcessorPlatformSettings`

- `Platforms: Dictionary<string, ModelAssetProcessorSettings>`

### Material Settings Model

`MaterialAssetImportSettings`

- `Importer: AssetImporterSettings`
- `Processor: MaterialAssetProcessorPlatformSettings`

`MaterialAssetProcessorPlatformSettings`

- `Platforms: Dictionary<string, MaterialAssetProcessorSettings>`

## Serialization

The current binary serializer should be replaced with asset-kind-specific serializers:

- `TextureAssetImportSettingsBinarySerializer`
- `ModelAssetImportSettingsBinarySerializer`
- `MaterialAssetImportSettingsBinarySerializer`

Each serializer should:

- own its own binary version
- write only the fields relevant to its asset kind
- validate that required settings exist
- reject invalid platform ids and invalid processor values

The old generalized serializer should be removed from active use rather than preserved as a compatibility path.

## Asset Import Manager Changes

`AssetImportManager` should stop treating import settings as one generic payload.

Instead it should:

1. Resolve the source asset kind.
2. Load or create the matching typed sidecar model.
3. Apply importer validation and default-importer selection for that asset kind.
4. Build asset ids from only the relevant processor settings.
5. Apply processing through asset-kind-specific code paths.

This means:

- texture import flow reads texture settings only
- model import flow reads model settings only
- material settings flow reads material settings only

The current helper methods that resolve mixed `AssetPlatformProcessorSettings` objects should be removed or replaced by typed helpers.

## Editor UI Changes

`AssetImportSettingsView` should stop editing one generalized processor object.

Instead it should work against an asset-kind-specific editing model:

- texture assets show texture processor controls only
- model assets show model processor controls only
- material assets show material processor controls only

This keeps the panel honest to the selected asset and eliminates dead UI state. The current texture max-resolution feature naturally belongs only in the texture view path.

The apply request flow can remain structurally similar, but the payload it carries should be typed to the current asset kind rather than always carrying generalized processor settings.

## Testing

Tests should be rewritten around typed sidecars rather than upgraded compatibility behavior.

Required coverage:

- binary round-trip tests for each asset-kind-specific serializer
- validation tests for invalid platform ids and invalid processor values
- asset import manager tests covering:
  - texture max-resolution hashing and processing
  - model flip-winding hashing and processing
  - material settings persistence
- editor view tests proving the correct controls appear for each asset kind
- editor session tests proving typed settings save, reload, and apply correctly

No test coverage is needed for reading the old mixed-schema payloads.

## Migration Strategy

There is no binary migration.

When old sidecars are encountered after this change:

- deserialization failure is acceptable
- users can regenerate settings by reopening and reapplying settings for the asset
- assets with no valid sidecar should fall back to newly created default typed settings

This keeps the implementation simple and avoids carrying two schemas.

## Risks

### Settings File Invalidations

All existing import-settings sidecars become stale immediately. This is intentional, but it will force recreation for assets that relied on non-default settings.

### Editor Flow Coupling

The current editor apply flow assumes generalized processor settings. Refactoring that flow cleanly is required to avoid replacing one awkward abstraction with another disguised wrapper.

### Test Churn

Serializer, manager, and UI tests will all move together because they currently share the mixed-schema assumptions.

## Recommendation

Implement a hard split into `TextureAssetImportSettings`, `ModelAssetImportSettings`, and `MaterialAssetImportSettings`, with dedicated serializers and typed editor/import-manager flows.

That is the cleanest shape, matches the user expectation that each asset sidecar should describe only that asset, and removes the dead mixed-schema container entirely.
