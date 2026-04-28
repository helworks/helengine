# Shared `.heproj` Format Library Design

## Summary

This change establishes a shared library that owns the canonical `.heproj` project-file format used by both the launcher and the editor.

Today the launcher parses `.heproj` directly in its own code, while other project metadata still leaks into separate local settings files and older editor-side project-management paths. That creates duplication, version coupling, and unclear ownership of the project contract. The launcher must understand project metadata without depending on any specific editor version, and the project file must be able to describe engine compatibility and supported platforms in a stable way.

The new design makes `.heproj` the authoritative shared project contract and moves its schema, parsing, validation, and writing into a dedicated shared library with no launcher-UI or editor-runtime dependencies.

## Goals

- Create one shared library that defines and owns the `.heproj` file format.
- Make `.heproj` the canonical source of shared project metadata.
- Let the launcher understand projects without depending on editor implementation details.
- Store the exact required engine version in `.heproj`.
- Store supported platforms in `.heproj`.
- Support arbitrary platform identifiers without crashing older launchers or editors.
- Add a project format version so the schema can evolve safely over time.
- Keep local-only state out of `.heproj`.
- Let the launcher display project engine version and supported platforms from the shared project contract.

## Non-Goals

- No launcher visual redesign beyond consuming the new shared metadata.
- No attempt to turn local editor settings into shared project contract data.
- No broad “project system” framework that owns editor workflows, launcher workflows, and local settings together.
- No baked-in enum that must be updated every time a new target platform appears.

## Current Problem

The repository currently splits project-format knowledge across multiple places:

- the launcher parses `.heproj` in its own service code,
- project creation writes part of the project contract into `.heproj`,
- engine version currently lives in `settings/project.json`,
- older editor-side project-management code still uses separate assumptions about what a project is,
- local settings and shared project metadata are not cleanly separated.

That causes several problems:

- the launcher can drift from the editor’s understanding of the project file,
- the launcher risks depending on editor-specific behavior instead of a stable contract,
- new shared metadata such as supported platforms has no clear home,
- future format changes would require duplicated parser updates.

## Proposed Design

### 1. Add A Dedicated Shared Project-Format Library

Introduce a new shared library that has one responsibility: define the `.heproj` contract and provide read/write access to it.

The library should:

- have no dependency on launcher UI code,
- have no dependency on editor runtime code,
- expose strongly modeled project-file types,
- expose parsing, validation, and serialization APIs,
- return structured failures instead of silently inventing defaults for invalid files.

This makes the project file a proper shared boundary instead of an informal convention.

### 2. Make `.heproj` The Canonical Shared Project Contract

The `.heproj` file becomes the authoritative source of project metadata that must be shared across tools and platforms.

Canonical `.heproj` fields should include:

- `projectFormatVersion`
- `name`
- `version`
- `requiredEngineVersion`
- `supportedPlatforms`
- `created`
- `lastOpened`
- `description`

This means:

- exact engine compatibility is defined by `requiredEngineVersion`,
- launcher-visible project capabilities such as supported platforms come from `.heproj`,
- the launcher no longer needs to reconstruct canonical metadata from sibling local settings files.

### 3. Keep Local Settings Out Of `.heproj`

Local state should remain outside the shared project contract.

Examples of local-only data:

- currently selected platform,
- editor-only preferences,
- machine-local workflow state.

Files such as `settings/project.json` should continue to exist only for local settings concerns. They should not define project identity, engine compatibility, or platform support.

### 4. Represent Supported Platforms As Arbitrary String Identifiers

The shared format should store supported platforms as a list of strings.

Example shape:

```json
{
  "supportedPlatforms": [
    "windows",
    "linux",
    "android"
  ]
}
```

Important behavior:

- the shared library must not crash on unknown platform ids,
- unknown ids must be preserved when reading and writing,
- the launcher should display unknown ids rather than hiding or rejecting them.

This allows newer engine versions to introduce new platforms without breaking older launcher builds.

### 5. Require One Exact Engine Version

`requiredEngineVersion` should represent one exact required engine version, not a version range.

That keeps the meaning simple:

- the project declares exactly which engine version it targets,
- the launcher can compare the project against installed engines directly,
- the editor can enforce the same compatibility rule without needing separate interpretation logic.

### 6. Add Explicit Format Versioning

The shared format must include a project format version field, for example `projectFormatVersion`.

This field exists to:

- allow future schema changes,
- let older tools fail clearly on unsupported newer formats,
- avoid silent misreads when the project shape evolves.

Behavior:

- known supported format versions should parse normally,
- newer unsupported versions should fail with a structured “unsupported project format version” result,
- unknown extra JSON properties should not crash parsing unless they conflict with required structure.

### 7. Centralize JSON Shape And Validation In The Shared Library

The shared library should own:

- property naming,
- required/optional field validation,
- serializer options,
- read/write stability,
- date parsing rules,
- forward-compatible tolerance for extra fields.

Both launcher and editor should stop carrying their own independent knowledge of the `.heproj` JSON shape.

This avoids a repeat of the current split where each consumer partly redefines the format.

### 8. Make The Launcher Read Shared Metadata Directly

The launcher should consume the shared library instead of its current ad hoc `.heproj` parser.

Launcher responsibilities after the change:

- load project metadata through the shared library,
- render required engine version from `.heproj`,
- render supported platforms from `.heproj`,
- treat shared-library validation errors as user-facing project-file errors,
- remain independent from any specific editor assembly version.

The launcher should understand the project file because it understands the shared contract, not because it knows editor internals.

### 9. Make The Editor Read And Write The Same Shared Contract

The editor should also consume the shared library for project-file loading and saving.

Editor responsibilities after the change:

- create new `.heproj` files through the shared writer,
- load project metadata through the shared reader,
- persist canonical project metadata back through the shared contract,
- keep local settings in local settings files.

This gives both launcher and editor the same interpretation of the project file.

## Error Handling

The shared library should return structured failures for invalid project files.

Expected failure categories include:

- invalid JSON,
- missing required fields,
- unsupported `projectFormatVersion`,
- semantically invalid values for required fields.

The library should not silently fabricate a valid project model from an invalid canonical file.

Tolerance rules:

- unknown extra properties are allowed,
- unknown platform ids are allowed,
- unsupported future format versions fail clearly,
- invalid required structure fails clearly.

## Testing

### Shared Library Tests

Add focused tests for:

- valid `.heproj` round-trip read/write,
- missing required fields,
- invalid JSON,
- unsupported `projectFormatVersion`,
- unknown extra properties,
- unknown platform ids surviving parse/write without failure,
- exact required engine version serialization.

### Launcher Tests

Add or update launcher tests to verify:

- recent-project metadata is loaded through the shared library,
- required engine version is available for display,
- supported platforms are available for display,
- invalid project files surface clean launcher errors,
- unknown platform ids do not crash project display.

### Editor Tests

Add or update editor tests to verify:

- new projects write the canonical `.heproj` shape,
- project load/save uses the shared library,
- local settings remain separate from canonical project metadata,
- active-platform local settings do not redefine supported platforms in `.heproj`.

## Migration Direction

New work should move toward this ownership model:

- `.heproj` owns shared project contract data,
- local settings files own machine-local state,
- launcher and editor both depend on the shared project-format library.

As this rolls out, duplicated project-file parsing code in launcher and editor should be removed rather than kept in parallel.
