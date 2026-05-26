## Summary

The current shared editor texture pipeline supports generic indexed output formats (`Indexed4` and `Indexed8`), but the conversion path is exact-palette only. If the source image uses more unique colors than the target palette capacity, the conversion throws and the platform build fails.

That behavior is too weak for real authored textures. A practical indexed pipeline needs a configurable indexing method that can reduce arbitrary source images into a bounded palette instead of only accepting images that already fit the target palette exactly.

This design adds a new generic indexed-texture setting on the shared editor side:

- `IndexingMethodId`

When a platform selects an indexed texture format, the editor UI will expose an indexing-method field. The first supported method is:

- `QuantizedIndexed`

`QuantizedIndexed` performs palette quantization instead of exact-color admission and uses alpha-aware prioritization so semi-transparent UI edge colors are preserved more aggressively than opaque color-only fidelity.

This is a shared engine/editor feature. It is not platform-specific. Any platform that publishes `Indexed4` or `Indexed8` automatically benefits from it.

## Goals

- Make indexed texture formats usable for real images instead of only exact-palette images.
- Keep the feature generic across all platforms that publish indexed formats.
- Expose indexing behavior explicitly in the editor when the user selects an indexed texture format.
- Preserve semi-transparent UI edges with higher priority during quantization.

## Non-Goals

- Add platform-specific indexed-quantization behavior.
- Add multiple quantization methods in the first pass beyond `QuantizedIndexed`.
- Add dithering in the first pass.
- Change the meaning of non-indexed texture formats.

## Current Problem

Today `TextureAssetProcessor.ConvertToIndexed(...)` builds a palette by accepting exact unique RGBA values until palette capacity is exhausted. Once more unique colors are encountered than the target palette supports, processing fails.

That causes two practical problems:

- real images often exceed 16 or 256 unique colors even after resolution reduction
- images with anti-aliased transparency, especially UI logos and edges, degrade badly unless alpha-sensitive colors are treated as important during palette selection

The result is that users can select `Indexed4` or `Indexed8`, but the shared indexed conversion path is not robust enough for normal authoring workflows.

## Architecture

### 1. Generic indexed-method setting

Add one new shared texture processor setting:

- `IndexingMethodId`

This remains part of the existing per-platform texture import settings model. It is not PS2-specific or platform-specific. It is only meaningful when:

- `ColorFormatId == Indexed4`
- `ColorFormatId == Indexed8`

For non-indexed formats, the setting is ignored by processing and hidden or disabled in the editor UI.

### 2. First supported method: `QuantizedIndexed`

`QuantizedIndexed` becomes the first generic indexing method. It replaces the current "exact palette only" assumption when the user has selected an indexed target format and this method.

Behavior:

- the source texture may contain more colors than the target palette capacity
- the processor reduces the image to the requested palette size
- palette generation is alpha-aware
- semi-transparent UI edge colors receive higher preservation priority than fully opaque colors when tradeoffs are required

This keeps the setting explicit while producing practical indexed output.

### 3. Default behavior

When a platform uses an indexed format and no explicit indexing method has been stored yet, the system will default to:

- `QuantizedIndexed`

That keeps the first indexed workflow usable without requiring legacy assets to be hand-edited before saving.

### 4. Editor UI behavior

The asset import settings UI should expose the indexing-method selector only when the selected texture format is indexed.

Expected UX:

- `Rgba32` / other non-indexed format:
  - no indexing-method option shown
- `Indexed4` or `Indexed8`:
  - show `IndexingMethod`
  - first available option: `QuantizedIndexed`

This preserves a clean UI surface while making the indexed pipeline explicit.

## Processing Rules

### Format gating

The quantization path is generic and available to any platform that selects:

- `Indexed4`
- `Indexed8`

No platform needs to opt into separate indexing-method metadata for the first pass.

### Alpha-aware prioritization

Quantization must treat semi-transparent pixels as higher priority than ordinary opaque color variance when palette decisions are forced.

Practical interpretation:

- anti-aliased transparent edges should remain visually stable
- alpha-bearing UI silhouette colors should survive palette reduction more reliably
- opaque body colors may absorb more approximation error than edge/transparency-sensitive colors

The exact scoring heuristic can be implementation-defined, but the outcome must reflect this priority.

### Exact-palette images

If an image already fits within the target palette capacity, `QuantizedIndexed` should still produce correct indexed output. It must not degrade exact-palette images unnecessarily.

## Data Model

Add one generic indexed-method identifier concept on the editor side.

Suggested first value:

- `QuantizedIndexed`

Possible future additions can be introduced later without changing the meaning of the indexed texture formats themselves.

## Error Handling

- Non-indexed formats ignore `IndexingMethodId`.
- Indexed formats without a supported indexing method fail clearly.
- Quantization should no longer fail merely because the source image exceeds palette capacity.
- Real failures should now be reserved for invalid input, unsupported method identifiers, or internal processing errors.

## Testing

Required shared-engine tests:

1. Serialization tests
- texture import settings round-trip `IndexingMethodId`

2. Texture processor tests
- `Indexed8 + QuantizedIndexed` succeeds on an image with more than 256 unique colors
- `Indexed4 + QuantizedIndexed` succeeds on an image with more than 16 unique colors
- alpha-aware prioritization preserves semi-transparent edge colors preferentially
- exact-palette-friendly images still convert correctly

3. UI tests
- indexing-method control is hidden or disabled for non-indexed formats
- indexing-method control is shown for indexed formats
- indexed format selection defaults the method to `QuantizedIndexed` when needed

4. Asset import workflow tests
- saving indexed platform settings persists the chosen indexing method
- re-importing a texture with indexed settings uses the stored method

## Boundaries

- This feature belongs in the shared editor and shared texture processor.
- Platform repos should only consume the resulting indexed textures.
- No PS2-specific or platform-specific engine branching is needed for quantization itself.

## Risks

- Poor quantization heuristics can produce visible artifacts even if builds no longer fail.
- Alpha prioritization that is too aggressive could distort large opaque regions.
- Defaulting behavior for legacy indexed settings must not break existing assets or UI expectations.

## Acceptance Criteria

- Selecting `Indexed4` or `Indexed8` exposes an indexing-method option in the editor.
- `QuantizedIndexed` is available as the first supported method.
- Shared indexed conversion no longer requires the source image to already fit palette capacity.
- Semi-transparent UI edges are preserved with higher priority during palette reduction.
- Any platform using indexed formats benefits automatically from the shared behavior.
