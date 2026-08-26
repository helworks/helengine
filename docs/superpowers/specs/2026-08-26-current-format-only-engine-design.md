# Current-Format-Only Engine Design

## Summary

Helengine will read and write only the current version of every engine-owned persisted format. All production migration, upgrade, compatibility, deprecated-alias, and legacy-constructor code will be deleted. Older authored files, packages, caches, settings, component payloads, and build documents are intentionally unsupported and must be regenerated.

This is a source simplification project, not a migration project. It does not convert old data and does not provide a transitional compatibility period.

## Goals

- Make every binary and JSON persistence boundary accept exactly one current schema.
- Delete branches that infer defaults for fields absent from older versions.
- Delete readers that discard obsolete tails or translate older value layouts.
- Delete import-settings conversion and rewrite paths.
- Delete runtime type-ID aliases and obsolete constructor overloads retained for old callers.
- Delete build-settings and local-settings normalization for obsolete values.
- Delete physics and component-payload compatibility analysis.
- Replace compatibility tests with exact-version rejection and current-format round-trip tests.
- Add an automated repository guard that prevents new production compatibility code from being introduced casually.

## Non-Goals

- Preserving any project or package generated before this workstream.
- Shipping an external migration tool.
- Inferring an old schema from payload length or missing fields.
- Keeping deprecated APIs for downstream source compatibility.
- Changing the current data model merely because old readers are being removed.
- Removing historical design documents or Git history.

## Current Problem

The repository states that breaking changes are acceptable, but production code still carries many generations of compatibility behavior. Representative examples include:

- `EditorAssetBinarySerializer` accepts a version range and contains old model, material, scene, and identity readers;
- `PackagedAssetBinarySerializer` accepts old runtime packages and reconstructs old scene layouts;
- import-settings serializers accept ranges and `AssetImportManager` converts generalized old settings into typed settings;
- runtime component resolution normalizes obsolete type identifiers;
- physics feature analysis recognizes old component payload versions;
- build and local-settings services rewrite obsolete profile identifiers and fields; and
- constructors and request types retain overloads whose only purpose is source compatibility.

These branches increase every serializer's test matrix and make current behavior harder to reason about. They also allow stale generated files to survive unnoticed instead of failing at the boundary that owns them.

## Current-Version Contract

Every persisted format exposes one `CurrentVersion` constant. A reader validates:

```text
header.Version == CurrentVersion
```

Any other value throws an `InvalidOperationException` containing:

- the format or record name;
- the received version;
- the required current version; and
- a direct instruction to regenerate the authored asset, settings file, cache, or package.

Readers do not accept version ranges. They do not conditionally read fields by version. They do not fabricate defaults for missing old fields. Writers always emit the current version.

Nested payloads that have their own version byte follow the same rule. Component tagged-field formats may continue to support optional current-schema fields, but may not retain alternate historical field names or payload layouts.

## Scope

### Authored native assets

`helengine.files` keeps only the current `EditorAssetBinarySerializer` layout. Old scene entity records, old material fields, old packed-model tails, and pre-identity payloads are deleted. Native authored files without current embedded identity fail and must be regenerated.

### Packaged runtime assets

`helengine.core` keeps only the current `PackagedAssetBinarySerializer` layout. Old scene entity records, material constants, texture metadata defaults, and other version-conditioned readers are deleted. Runtime packages are always regenerated with the matching engine revision.

### Import and material settings

Each typed import-settings serializer accepts only its current schema. The generalized `AssetImportSettings` compatibility route, conversion helpers, preservation flags, and rewrite-on-load behavior are deleted. Invalid or stale settings files fail with regeneration guidance rather than being converted.

### Scene and component persistence

Automatic component persistence reads only current tagged member names and current payload versions. Historical member aliases are deleted. Runtime component registries resolve only current component type identifiers. Feature analyzers inspect only current payload shapes.

### Build, platform, and local settings

Obsolete build-profile identifiers, removed JSON properties, compatibility constructor overloads, and rewrite-on-save behavior are deleted. Current documents must contain the required current fields and exact identifiers.

### APIs

An overload or type exists only when it serves a current caller. Downstream engine repositories and demodisc are changed together with the removal. No obsolete facade forwards to a current implementation.

## Error Policy

Unsupported data fails at the first owning boundary. The engine must not catch an unsupported-version failure and silently create defaults. User-facing editor and CLI layers may add context, but must preserve the original file path and version diagnostic.

Missing disposable caches may be recreated. A cache with an unsupported schema is deleted and rebuilt only when the cache is explicitly documented as disposable. This is cache invalidation, not persisted-project migration. Source-controlled authored assets and settings are never silently replaced.

## Repository Enforcement

A source-contract test scans production engine sources and fails on compatibility constructs. Its allowlist is intentionally narrow and covers words that describe unrelated concepts rather than compatibility behavior. The guard detects at least:

- symbols or methods containing `Legacy`;
- methods containing `Migrate`, `Upgrade`, `ConvertLegacy`, or `NormalizeLegacy`;
- comments claiming backward compatibility or a compatibility cycle;
- version checks that accept ranges at current persistence boundaries; and
- obsolete API attributes or parameters retained only for compatibility.

The test excludes historical documentation, vendor sources, test fixture names that assert rejection, and native code migration markers whose meaning is implementation-language ownership rather than data compatibility.

## Test Strategy

### Serializer tests

For every engine-owned serializer:

- current payload round-trips;
- `CurrentVersion - 1` fails;
- `CurrentVersion + 1` fails;
- truncated current payload fails rather than receiving defaults; and
- the error identifies the format, actual version, and required version.

Tests that construct historical payloads for successful loading are deleted.

### Import and settings tests

- typed current settings load and save;
- generalized or older settings fail;
- no conversion or rewrite occurs;
- missing disposable settings follow the explicit current default-creation contract only where already supported; and
- current non-default settings retain their values.

### API and source tests

- removed overloads and aliases are absent;
- all in-repository callers use current APIs;
- production-source compatibility scan passes; and
- the complete engine solution compiles without obsolete warnings.

### Project fixtures

- regenerate demodisc native authored assets and settings with the current writer;
- regenerate required packaged fixtures;
- run editor load/save and platform cook smoke tests; and
- verify no compatibility helper is reintroduced to make a stale fixture pass.

## Implementation Boundaries

The removal proceeds by format ownership, with a green test suite after each boundary:

1. authored asset serializers;
2. packaged runtime serializers;
3. import and material settings;
4. scene, component, and physics payloads;
5. build, platform, and local settings;
6. obsolete public overloads and aliases; and
7. repository guard plus fixture regeneration.

Deleting a reader and regenerating its fixtures belong to the same change. A task must not leave a current writer paired with a historical reader or vice versa.

## Risks

The main risk is confusing a current optional field with historical compatibility. Optionality that is part of the current schema remains supported; alternate historical names and layouts do not.

Another risk is deleting a path still used by a separate platform repository. Before removing a public surface, the implementation searches all engine and platform repositories available under `C:\dev\helworks` and updates current callers in the same task.

Large fixture churn is expected. Generated binary changes are isolated from unrelated source edits and committed with the serializer task that requires them.

## Success Criteria

- Every persisted format accepts exactly its current version.
- No production migration, upgrade, compatibility alias, old-layout reader, or deprecated forwarding overload remains.
- Current files fail clearly when their embedded identity or required fields are absent.
- All supported repositories compile against current APIs.
- Demodisc and platform smoke tests use only regenerated current files.
- The production-source compatibility guard passes.
