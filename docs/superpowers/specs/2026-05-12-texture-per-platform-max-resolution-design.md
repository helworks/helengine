# Texture Per-Platform Max Resolution Design

## Goal

Add a per-asset, per-platform texture max-resolution override to the existing asset import settings pipeline so that one texture source can cook to different capped dimensions per platform.

The cap must apply at the texture asset level. Any direct or indirect consumer that resolves the same imported texture asset must receive the capped result automatically.

## Requirements

- Texture assets expose a per-platform max-resolution override in their import settings.
- The setting is stored on the texture asset sidecar, not in global build-profile settings.
- The setting affects the imported cached texture asset, so every downstream consumer inherits it.
- The cap preserves aspect ratio and only downsizes. It never upscales.
- `0` means uncapped.
- Changing the cap invalidates the imported texture cache identity and forces reimport.
- Existing assets without explicit texture processor settings keep current behavior.

## Recommended Approach

Add a dedicated `TextureAssetProcessorSettings` object under the existing `AssetPlatformProcessorSettings` tree and use it as the one texture-specific processor payload for each platform.

This keeps the feature aligned with the current asset import settings model:

- importer settings remain shared per source asset
- processor settings remain sparse and platform-specific
- texture, model, and material processor state remain grouped under one sidecar document

This avoids creating a second texture-settings sidecar system while still leaving room for future texture-specific processor settings.

## Data Model

Extend the current asset import settings classes with a texture branch:

- `AssetImportSettings`
  - `Processor`
    - `Platforms[platformId]`
      - `Texture.MaxResolution`

### New Type

`TextureAssetProcessorSettings`

- `MaxResolution: int`
- default value: `0`

Behavior:

- `0` means no cap
- values below `0` are invalid and should be rejected by UI validation and serializer/runtime guards
- positive values are interpreted as the maximum allowed width or height in pixels, whichever axis is larger

### Existing Type Changes

`AssetPlatformProcessorSettings`

- add `Texture` property
- initialize it in the constructor alongside `Model` and `Material`

## Serialization

The asset import settings binary serializer must version forward to include texture processor data.

### Write Path

For each platform entry, write:

1. platform id
2. model flip-winding
3. texture max-resolution
4. material schema id
5. material field values

### Read Path

- bump `AssetImportSettingsBinarySerializer.CurrentVersion`
- read the texture max-resolution field into `platformSettings.Texture.MaxResolution`
- reject negative values during deserialize

Backward compatibility is not required beyond the repo's current-format-only direction. The serializer should read and write only the new current format.

## Import Pipeline

Texture max resolution should be applied during texture import caching, not during packaging.

### Why This Layer

- direct asset preview and downstream material/model use will all observe the same processed asset
- the texture asset id can encode the setting naturally
- platform-specific reimport behavior already exists conceptually in the asset import manager

### Processing Rule

After the selected texture importer returns a `TextureAsset`:

- read the active platform's `Texture.MaxResolution`
- if the cap is `0`, leave the texture unchanged
- if both dimensions are already within the cap, leave the texture unchanged
- otherwise compute a uniform downscale factor from the larger source dimension
- generate a resized `TextureAsset` with preserved aspect ratio
- clamp the result so both output dimensions are at least `1`

The resize should be deterministic and contained in a dedicated texture processor/helper rather than embedded inline in unrelated import-manager logic.

## Cache Identity

Texture asset ids must change when the selected texture importer or active platform texture cap changes.

### Identity Inputs

For texture assets, the cache identity should include:

- source checksum
- importer id when importer-qualified identity is already required
- active platform id when texture processor settings are being applied
- texture max-resolution for that active platform

This makes changing the per-platform cap behave like a real processor-state change instead of leaving stale imported cache entries behind.

If no active platform id is set, the import manager should resolve the texture processor platform using the same deterministic platform-selection strategy used for model processor settings.

## Editor UI

Extend `AssetImportSettingsView` so texture assets expose a platform-specific max-resolution field.

### UI Behavior

- show the existing platform tabs for texture assets
- show a numeric field labeled `Max Resolution`
- bind the value to the selected platform's `Texture.MaxResolution`
- preserve the existing importer picker behavior
- emit the updated `AssetImportSettingsApplyRequest` with the modified processor settings payload

### Validation

- blank input is invalid
- non-numeric input is invalid
- negative input is invalid
- `0` is allowed and means uncapped

Validation should fail explicitly in the editor flow rather than silently coercing invalid input to defaults.

## Downstream Behavior

Indirect texture users do not need special-case logic.

If a model or material references a texture asset id that resolves through the same texture source sidecar, that consumer automatically receives the capped imported texture asset because the cap is applied before packaging, at cache generation time.

No material-side or model-side duplicate max-resolution setting should be introduced.

## Error Handling

- Deserialization throws on negative max-resolution values.
- Import logic throws if required processor containers are missing instead of fabricating replacements silently.
- UI validation blocks apply requests for invalid values.
- Resizing code throws on invalid source texture dimensions rather than masking malformed imported data.

## Testing

Implementation should start with failing tests.

### Serialization Tests

- round-trip asset import settings containing per-platform texture max-resolution values
- verify distinct platform values survive deserialize
- verify negative serialized values are rejected

### UI Tests

- texture asset settings view shows the max-resolution control for texture assets
- changing the selected platform updates the bound max-resolution value
- applying a new max-resolution emits the expected processor settings payload
- invalid input is rejected

### Import Manager Tests

- texture import with uncapped settings preserves original dimensions
- texture import with capped settings downsizes while preserving aspect ratio
- changing the texture cap changes the generated asset id
- changing the texture cap causes reimport instead of stale cache reuse

### Integration Tests

- one indirect consumer path should prove that a referenced texture asset inherits the capped imported dimensions without extra consumer-specific overrides

## Files Likely Affected

- `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
- `engine/helengine.editor/managers/asset/AssetImportSettings.cs`
- `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`
- `engine/helengine.editor.tests/BinarySerializationTests.cs`
- `engine/helengine.editor.tests/AssetImportManagerTests.cs`

Additional helper files may be warranted for texture resizing if the current import manager has no appropriate home for that logic.

## Non-Goals

- no global build-profile replacement for `TextureScalePercent`
- no material-side duplicate texture cap settings
- no platform-agnostic texture cap field
- no upscaling behavior
- no generalized mip-generation or texture compression feature in this change

## Rollout Notes

This design is intentionally narrow. It adds one per-asset texture processor setting and routes it through the existing import sidecar pipeline. That keeps the feature predictable, cache-safe, and inherited automatically by every downstream texture consumer.
