# Source Font Import And Cooked Hefont Design

## Goal

Replace project-authored `.hefont` files with source font assets while preserving the existing runtime contract.

After this change:

- Projects author source font files such as `.ttf` or `.otf` directly in `assets`.
- The editor imports those files into `FontAsset` instances through the existing GDI-backed font generation path.
- Builds still emit runtime `.hefont` files for the player.
- Packaged scene/component references still resolve to cooked `.hefont` files.
- Committed cooked `.hefont` files are no longer the canonical source assets in projects like `city`.

## Current Problem

The current `city` project authors cooked font blobs directly:

- `assets/Fonts/DemoDiscTitle.hefont`
- `assets/Fonts/DemoDiscBody.hefont`

The demo menu theme references those cooked blobs directly from source code.

This is the wrong asset boundary:

- `.hefont` is a runtime/cooked representation, not a good authored source format.
- Source control ends up storing generated binary blobs as the canonical editable asset.
- The editor already has a font generation primitive in `GDIFontProcessor`, but it is not wired into the normal project asset pipeline.

## Requirements

- Projects must author source fonts, not cooked `.hefont` blobs.
- The first implementation slice must support exactly the formats the current `GDIFontProcessor` path can consume successfully.
- Editor-side tooling must be able to resolve source font assets into `FontAsset` instances.
- Scene persistence and component save state must keep referencing the authored source font path.
- Platform cooking and scene packaging must translate source font references into cooked `.hefont` runtime artifacts.
- Cooked output paths for fonts must remain predictable and stable.
- The cooked font path shape must remain `cooked/<source-relative-path-with-hefont-extension>`, for example `cooked/Fonts/DemoDiscTitle.hefont`.
- Runtime player loading remains unchanged and continues to load `.hefont` files only.

## Recommended Approach

Use source-font assets with normal asset import behavior.

The editor already owns the font rasterization/generation backend through `GDIFontProcessor`. The missing piece is pipeline integration.

This design keeps the runtime contract intact:

- authored asset: `.ttf` or `.otf`
- editor/import representation: `FontAsset`
- cooked runtime artifact: `.hefont`

That avoids introducing a new descriptor asset type and avoids keeping generated `.hefont` files in the project assets folder.

## Asset Model

Authored project assets:

- `assets/Fonts/DemoDiscTitle.ttf`
- `assets/Fonts/DemoDiscBody.ttf`

Authored references in code and scene data:

- `Fonts/DemoDiscTitle.ttf`
- `Fonts/DemoDiscBody.ttf`

Cooked runtime output:

- `cooked/Fonts/DemoDiscTitle.hefont`
- `cooked/Fonts/DemoDiscBody.hefont`

The source font path is the stable authored identity. The cooked `.hefont` path is derived from it during packaging.

## Editor Import Flow

### Discovery

The editor host registers source font extensions as importable assets.

Source font files must appear in the asset browser as font assets rather than unknown binary files.

### Loading

When editor tooling requests a `FontAsset` from a source font path:

- the import pipeline opens the source font file
- the current GDI-backed generation path builds a `FontAsset`
- editor consumers receive the `FontAsset` just as they do today when loading `.hefont`

This allows existing property pickers, component editors, and scene resolvers to continue working with `FontAsset` at runtime in the editor.

### Settings

Font import settings should use the existing asset import settings model.

This first slice does not introduce a new dedicated font-asset descriptor format.

## Scene And Component Persistence

Scene persistence continues to store file-backed font references, but the stored relative path becomes the source asset path.

Example:

- before: `Fonts/DemoDiscTitle.hefont`
- after: `Fonts/DemoDiscTitle.ttf`

This applies to:

- text components
- FPS overlays
- baked menu scene generation
- any other persistence path that stores file-backed font references

Editor scene loading resolves those source font references through the editor import path.

## Build And Packaging Flow

When the cooker or scene packager encounters a file-backed font reference:

1. Resolve the referenced source font asset path inside the project.
2. Import the source font into a `FontAsset`.
3. Derive the cooked runtime path by keeping the relative path and changing the extension to `.hefont`, prefixed under `cooked/`.
4. Write the serialized runtime `FontAsset` bytes to that cooked path.
5. Rewrite packaged scene/component font references to the cooked `.hefont` path.

Example:

- source reference: `Fonts/DemoDiscTitle.ttf`
- cooked runtime path: `cooked/Fonts/DemoDiscTitle.hefont`
- packaged scene/component reference: `cooked/Fonts/DemoDiscTitle.hefont`

This keeps the player-side contract simple and stable.

## Runtime Contract

No runtime support is added for raw `.ttf` or `.otf` loading.

The player runtime continues to:

- receive packaged scene references that point at `.hefont`
- load those `.hefont` files as `FontAsset`

The only change is where those `.hefont` files come from.

## City Project Migration

The first client migration is the `city` demo menu.

Changes:

- replace committed `assets/Fonts/DemoDiscTitle.hefont` and `DemoDiscBody.hefont` with source font files
- update `DemoDiscMenuTheme` to reference the source font filenames
- regenerate the demo menu scene so baked references point at source font paths
- let Windows build cooking emit the cooked `.hefont` files automatically

After migration, the project no longer depends on authored cooked font blobs.

## Error Handling

The pipeline should fail clearly when:

- a referenced source font file does not exist
- the current font importer cannot load the source font format
- cooking cannot write the derived cooked `.hefont` output

Failures must not silently fall back to defaults or skip cooking.

The build error should identify the source font path that failed.

## Non-Goals

- No new font descriptor asset type in this slice.
- No runtime direct loading of `.ttf` or `.otf`.
- No broad redesign of all asset importers.
- No silent backward compatibility layer for authored `.hefont` project assets unless needed later for migration tooling.

## Testing

Add coverage for:

- editor asset resolution of source font files into `FontAsset`
- source font references persisting correctly in scene/component save data
- cooking source font references into `cooked/.../*.hefont`
- packaged scene/component payloads rewriting to cooked `.hefont` paths
- runtime scene loading succeeding with a source-authored font reference after packaging
- demo menu regeneration/build succeeding without committed `.hefont` source assets

## Result

This change restores the correct asset boundary:

- source fonts are authored assets
- `FontAsset` is the editor/runtime in-memory representation
- `.hefont` is a cooked runtime artifact

That gives the project a sane workflow without changing the player runtime contract.
