## Summary

Asset settings need to separate source-import concerns from platform-specific processing concerns without multiplying files. The editor should keep using one `*.hasset` file per source asset, but that file should contain two distinct sections:

- importer settings shared by every platform
- processor settings keyed by project-supported platform id

This change is driven by the immediate `Flip Winding` need for Sponza, but the design needs to generalize to any model processor and to future per-platform asset processing.

## Goals

- Keep a single `*.hasset` file per source asset.
- Separate importer configuration from processor configuration in code and UI.
- Support processor settings per platform using the current project's `supportedPlatforms`.
- Persist the editor's active platform per project in `settings/project.json`.
- Add a model processor boolean setting named `Flip Winding`.
- Apply `Flip Winding` during processing, not rendering.
- Silently invalidate and rewrite old `*.hasset` versions instead of supporting prior layouts.

## Non-Goals

- Support old `*.hasset` versions beyond silent regeneration.
- Build a general plugin system for arbitrary processor UIs in this change.
- Add more than the currently needed model processor settings.
- Introduce multiple asset settings files for one source asset.

## Current Problems

Today `AssetImportSettings` only stores:

- `ImporterId`
- `SourceChecksum`
- `AssetId`

That is enough to pick an importer, but it does not model processor behavior at all, and it cannot vary by platform. The current asset settings UI also only exposes importer selection, so there is no place to edit model-processing behavior such as winding correction.

## Proposed Data Model

`*.hasset` remains the single source of truth for editor-owned asset settings. Its schema becomes versioned editor data with two top-level sections.

### Importer Settings

Importer settings are source-reading choices shared across all platforms.

Fields:

- `ImporterId`
- `SourceChecksum`
- `AssetId`

These continue to answer:

- which importer should read the source file
- whether cached output is still valid for the current source content
- which processed asset identity belongs to the source file

### Processor Settings

Processor settings are platform-specific build choices.

For model assets, processor settings will be keyed by platform id and initially contain:

- `FlipWinding`

Conceptual shape:

```text
AssetImportSettings
  Importer
    ImporterId
    SourceChecksum
    AssetId
  Processor
    Platforms
      windows
        Model
          FlipWinding
      android
        Model
          FlipWinding
```

The exact C# type names can differ, but the structure should preserve these boundaries:

- importer settings shared across all platforms
- processor settings per platform
- model processor settings isolated from unrelated asset kinds

## Versioning And Migration

The asset settings binary version should be bumped.

Old `*.hasset` files are not supported for reading as a compatibility format. When the editor encounters an unsupported asset settings version, it should:

1. discard the old settings object
2. create new settings using current defaults
3. persist the new schema back to the same `*.hasset` path

This replacement should happen silently with no warning UI.

That keeps the editor logic simple and avoids carrying partial old-format behavior while there are no released engine versions to preserve.

## Platform Source Of Truth

Platform tabs must come from the current project's `.heproj` `supportedPlatforms` list.

The editor also needs an active platform concept. That active platform should be stored per project in `settings/project.json`, not in `.heproj`, because it is local workspace state rather than canonical project metadata.

Rules:

- if the project has one supported platform, show one tab
- if the project has multiple supported platforms, show one tab per supported platform
- the selected tab should default to the saved active platform from `settings/project.json`
- changing the selected tab should update `settings/project.json`
- if `settings/project.json` is missing or the saved platform is no longer supported, default to the first supported platform and persist that choice

## UI Design

The asset settings area in the properties panel should evolve from a single importer picker into a split editor with two sections:

- `Importer`
- `Processor`

### Importer Section

The importer section keeps the existing importer combo box behavior:

- show the currently applied importer
- allow the user to switch importer
- apply importer changes explicitly

### Processor Section

The processor section should appear for assets whose processor settings are supported by the editor. For models, this section should:

- render platform tabs at the top using the current project's supported platforms
- render the current platform's processor controls below the tabs
- expose `Flip Winding` as a boolean model processor control

For now, a Windows-only project will show a single `windows` tab. That is still the correct UI because it keeps the model aligned with future multi-platform projects.

The processor section should use the project's active platform as its initial selected tab.

## Processing Behavior

`Flip Winding` is a processor setting, not an importer setting and not a renderer behavior.

That means:

- importers continue to read source files into raw model asset data
- processor settings modify how the processed model asset is built
- runtime renderers consume the already processed output and should not need special winding logic for this feature

For model processing:

- if `Flip Winding` is `false`, triangle order is emitted normally
- if `Flip Winding` is `true`, every emitted triangle index order is reversed during processed model generation

This should work for any model pipeline that produces `ModelAsset`, not only Assimp-backed imports.

## Cache Invalidation

Changing processor settings must invalidate processed model cache reuse.

At minimum, cache validity for models must incorporate:

- source checksum
- importer choice
- relevant processor settings for the target platform

Changing `Flip Winding` must cause the model asset to be regenerated instead of reusing stale processed output.

## Editor Architecture Changes

The design should preserve MVC boundaries:

- UI classes render tabs, checkboxes, and apply interactions only
- settings serialization stays in editor serialization code
- model processing behavior stays in asset-management and conversion code
- project-local active-platform persistence stays in editor-side project/local-settings services

Expected structural changes:

- expand `AssetImportSettings` into importer and processor sections
- update `AssetImportSettingsBinarySerializer` to the new schema version
- add editor-side loading and saving for project-local active platform in `settings/project.json`
- extend `AssetImportSettingsView` to render importer and processor sections with platform tabs
- extend `AssetImportManager` cache validation so processor settings participate in model rebuild decisions
- extend model conversion code so `Flip Winding` actually changes emitted triangle order

## Error Handling

- Unsupported `*.hasset` versions should silently regenerate with current defaults.
- Missing or invalid `settings/project.json` active platform should fall back to the first supported platform.
- Missing `supportedPlatforms` should be treated as a project metadata error, because the shared project file contract already requires it.
- If a processor setting is unavailable for a given asset kind, the processor section should omit that control rather than render a disabled placeholder.

## Testing Strategy

Add focused coverage for:

- binary serialization round-trip of the expanded `*.hasset` schema
- unsupported old `*.hasset` version causing silent replacement with current defaults
- model processor settings per platform loading and saving correctly
- project active platform loading from and saving to `settings/project.json`
- asset settings UI rendering platform tabs from project `supportedPlatforms`
- `Flip Winding` changing processed model triangle order
- cache invalidation when `Flip Winding` changes

## Recommended Implementation Order

1. Add the new `*.hasset` schema and serializer version bump.
2. Add editor-side project local settings support for active platform.
3. Extend asset settings UI to render importer and processor sections with platform tabs.
4. Add model processor settings with `Flip Winding`.
5. Make model processing and cache validation honor the new processor settings.
6. Add focused regression tests around serialization, UI tab selection, and winding changes.
