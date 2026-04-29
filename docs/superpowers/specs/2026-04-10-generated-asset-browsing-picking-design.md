# Generated Asset Browsing And Picking Design

## Summary

This document defines the first editor-facing slice of generated assets: users can browse and pick engine-provided assets from the same asset browser and picker UI used for file-backed project assets. Generated assets are virtual entries supplied by providers and resolved from engine-managed caches instead of the filesystem.

The first implementation target is browse-and-pick only. Users will not create, configure, import, or persist generated assets from the editor UI in this phase.

## Goals

- Expose engine-provided assets such as built-in models in the asset browser and picker.
- Keep one unified browser and picker flow for file-backed and generated assets.
- Resolve generated assets by stable asset id through engine providers instead of disk paths.
- Reuse cached runtime assets when a generated asset provider marks an entry as cache-backed.
- Preserve the current file-backed behavior for import settings, preview, and content loading.

## Non-Goals

- No editor UI for creating or configuring generated assets.
- No import settings flow for generated assets.
- No filesystem mirroring or fake files for generated assets.
- No general plugin marketplace or external provider loading in this phase.
- No write-back serialization of generated asset references beyond what already exists for runtime object assignment.

## Current Problem

The editor currently assumes every selectable asset is a real file under the project `assets` folder:

- `AssetBrowserEntry` only carries file and directory path data.
- `AssetBrowserView` only renders entries loaded from `EditorAssetManager.LoadEntries`.
- `AssetPickerModal` and `EditorAssetPickerService` pass `AssetBrowserEntry` instances that imply disk-backed assets.
- `ComponentPropertiesView.LoadModel(...)` always resolves models through `entry.FullPath`.
- `EditorSession.HandleAssetSelected(...)` assumes selected assets participate in import settings and texture preview.

That model blocks engine-generated assets such as built-in cubes and planes, even when those assets already exist in engine caches and are intentionally shared runtime resources.

## Proposed Architecture

### 1. Source-Aware Asset Entries

`AssetBrowserEntry` will become a generic selectable asset item instead of a file-only record.

Each entry will explicitly describe:

- Display name.
- Logical relative path used by the browser tree.
- Source kind.
- Entry kind.
- Whether the entry is a directory.
- Backing identifier used by the consumer.
- Optional filesystem path when the source is disk-backed.

The important separation is that generated assets no longer need fake disk paths. A file-backed entry can still expose `FullPath`, while a generated entry exposes a stable provider asset id.

### 2. Generated Asset Provider Registry

The editor will introduce a registry responsible for discovering generated asset providers available to the current session.

Each provider is responsible for:

- Publishing browseable virtual entries.
- Reporting the asset kind for each entry.
- Resolving a selected entry into the requested runtime or raw asset form.
- Declaring whether results are cache-backed and stable across requests.

The registry owns provider lookup and gives the browser one merged view of all generated assets.

This first version can ship with one built-in provider for engine assets, but the editor-side architecture must be provider-based so additional generated asset kinds can be added without reworking the browser.

### 3. Built-In Engine Asset Provider

The first provider will expose engine-owned generated assets under a virtual root, for example:

- `Engine/Models/Cube`
- `Engine/Models/Plane`

The provider will use stable asset ids such as:

- `engine:model:cube`
- `engine:model:plane`

These ids are the contract between the picker/browser and the provider cache. The display path is only for browsing.

### 4. Unified Browser Tree

The asset browser will merge two entry sources:

- Filesystem entries from `EditorAssetManager`.
- Generated entries from the provider registry.

The browser must treat generated directories as virtual directories and generated leaves as selectable assets. Navigation behavior remains the same from the UI point of view, but the backing source differs.

The browser should present generated assets as a top-level virtual category so users can discover them in the normal asset browser, not only in the picker modal.

## Selection And Resolution Rules

### File-Backed Assets

File-backed assets keep the current behavior:

- Model picks load a `ModelAsset` from disk through the editor content manager and build a `RuntimeModel`.
- Asset browser selection continues to show import settings and preview when the asset type supports it.

### Generated Assets

Generated assets use provider resolution:

- A model pick resolves the generated asset id through the provider and returns the cached or newly created runtime model.
- The model property row stores the selected label for display exactly as it already does for file-backed picks.
- The browser selection path treats generated assets as read-only virtual assets, not importable project files.

Generated assets must not fall through to code paths that require disk paths, import settings sidecars, or importer registration.

## UI Behavior

### Asset Browser

The browser must show generated assets alongside normal project assets.

Generated assets:

- Are browseable through virtual directories.
- Use the same row interaction model as file-backed assets.
- Use icon classification based on generated asset kind.
- Do not expose import settings behavior.

### Asset Picker

The picker continues to use the same modal and callback model, but the picked entry may now come from either source.

Extension filters remain valid for file-backed entries. Generated entries should instead be filtered by asset kind or provider-declared compatibility when necessary.

For the immediate model-picker use case, generated model entries must remain visible even though they have no file extension.

### Properties Panel

When a generated asset is selected in the normal asset browser:

- The import-settings view must not open.
- Texture preview must stay empty unless a future generated texture preview flow is explicitly added.
- The panel should show a simple read-only asset summary rather than pretending the asset can be imported.

This keeps generated assets discoverable without misleading the user into import workflows that do not apply.

## Caching Expectations

Generated assets are explicitly cache-oriented in this design.

Providers may return assets that are:

- Created once and reused from an internal cache.
- Generated on first request and then stored by stable id.

The editor must not rebuild generated runtime assets on every pick if the provider says the asset is cache-backed. The provider owns cache policy, while the picker/browser owns only browsing and selection.

## Failure Behavior

The editor must fail clearly instead of fabricating fallback assets.

If generated asset resolution fails:

- The picker consumer logs the failure.
- The current property value remains unchanged.
- The picker or browser does not silently replace the broken selection with a default model.

If a generated asset is selected in the browser but cannot be resolved for summary display, the properties panel should show an error state rather than import settings.

## Implementation Plan Shape

The code changes should be split into the following boundaries:

1. Asset entry and source metadata expansion.
2. Generated asset provider contracts and registry.
3. Built-in engine provider for models.
4. Browser and picker integration for merged virtual and filesystem entries.
5. Model property picker resolution for generated entries.
6. Asset-browser selection behavior that bypasses import settings for generated entries.

This decomposition keeps browser rendering, provider resolution, and consumer behavior isolated enough to test independently.

## Testing Requirements

The first implementation must include coverage for:

1. Asset browser entry generation that merges filesystem and generated virtual entries.
2. Browser navigation into generated virtual folders.
3. Model picker resolution of generated model entries without disk loading.
4. Model property display labels after picking generated assets.
5. Asset browser selection bypassing import settings for generated entries.
6. Provider resolution failure leaving the current property value unchanged.

## Open Follow-Ups

These items are intentionally deferred:

- Generated asset creation UI.
- Editing provider parameters from the editor.
- Serialization format for persistent generated-asset references.
- Generated texture preview and other richer asset-specific inspectors.
- Non-engine external generated asset providers.

## Recommendation

Implement the generated asset system as a provider-backed virtual asset source integrated into the existing browser and picker, not as fake files and not as a model-only shortcut.

That gives the editor one consistent asset selection workflow now while preserving room for future generated asset kinds without reworking the browser again.
