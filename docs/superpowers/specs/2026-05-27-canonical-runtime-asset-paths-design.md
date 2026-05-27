# Canonical Runtime Asset Paths

## Goal

Define one strict packaged/runtime asset path contract for the whole engine so scene data, generated core, build manifests, and runtime loaders all speak the same logical path format regardless of platform filesystem rules.

This fixes the current class of bugs where:

- shared engine code emits lowercase logical paths such as `cooked/fonts/default.hefont`
- other packagers emit mixed-case payloads such as `cooked/Fonts/default.hefont`
- permissive platforms hide the mismatch
- strict packaged filesystems such as RomFS fail at runtime

## Decision

The engine will use one canonical packaged/runtime path format:

- lowercase only
- forward slashes only
- relative only
- no leading slash
- no drive/rooted path
- no `.` segments
- no `..` segments

Example:

- valid: `cooked/fonts/default.hefont`
- invalid: `cooked/Fonts/default.hefont`
- invalid: `cooked\\fonts\\default.hefont`
- invalid: `/cooked/fonts/default.hefont`

This contract is strict immediately. Mixed-case packaged/runtime paths are invalid and must fail validation during packaging or build preparation. There is no compatibility fallback.

## Boundary

The shared engine owns canonical logical paths.

Platforms may still have different physical storage paths, but those mappings must live only at the platform export/runtime media boundary.

Examples:

- 3DS runtime uses canonical paths directly inside RomFS
- Windows loose-file builds use canonical paths directly on disk
- PS2 keeps canonical logical paths in shared manifests, then maps them to uppercase disc-native physical paths in the PS2 export/runtime lookup layer

The shared runtime must never depend on PS2-style physical paths such as `\\COOKED\\FONTS\\DEFAULT.HEF;1`.

## Affected Surfaces

The contract must be enforced anywhere a packaged/runtime-facing path is created, persisted, or consumed as a logical key.

### Shared packaging and serialization

- `PlatformPackagedAssetPathResolver`
- scene/component packaging transforms
- packaged scene asset references
- runtime scene catalog entries
- platform build manifests and cooked artifact records
- builder cook work-item output relative paths

### Shared runtime identity

- `RuntimeAssetIdGenerator`
- any runtime content key that derives from packaged relative paths

### Platform-specific mapping layers

- PS2 logical-path-to-physical-disc-path export
- any platform media manifest that translates canonical logical paths into platform-native physical paths

These mapping layers may transform casing and separators physically, but only after the canonical logical path has already been validated.

## Normalization And Validation

Add one shared path-normalization and validation utility for packaged/runtime logical paths.

Responsibilities:

- convert `\\` to `/`
- reject rooted paths
- reject empty paths
- reject `.` and `..` traversal
- require lowercase output
- provide one validation API that throws on non-canonical input

The contract should prefer explicit validation over silent correction for packaged/runtime references that already claim to be final logical paths. Build-time producers may normalize intermediate values before final validation, but the emitted logical path must always be canonical.

## Enforcement Strategy

### Producers

Any code that emits packaged/runtime paths must emit canonical lowercase paths before those paths enter manifests, scene payloads, or generated runtime data.

This includes:

- default generated assets such as `default.hefont`
- cooked source-asset outputs such as fonts, textures, materials, and scene assets
- generated scene companion references
- runtime scene catalog records

### Consumers

Runtime and build consumers should validate canonical logical paths at trust boundaries. If a mixed-case packaged/runtime path arrives, the operation should fail with a clear exception naming the invalid path.

### PS2

PS2 remains the one platform that intentionally diverges physically. Its export/runtime mapping layer should:

1. accept canonical lowercase logical paths
2. transform them to uppercase disc-native physical paths
3. preserve the logical canonical path as the shared-facing identity

No other shared system should know about the PS2 physical path form.

## Migration

This is not a compatibility rollout. The migration is immediate and mechanical.

Required cleanup:

- convert mixed-case cooked references such as `cooked/Fonts/...` to lowercase `cooked/fonts/...`
- update any tests that currently assert mixed-case packaged/runtime paths
- update any builder logic that preserves source-folder casing into final cooked/runtime references
- keep authored asset source paths unchanged where appropriate; only packaged/runtime logical paths are being canonicalized

Authored source paths may still live under folders such as `assets/Fonts`, but once they become packaged/runtime references they must be lowered to `cooked/fonts/...`.

## Failure Behavior

Failures should happen as early as possible and identify the exact invalid logical path.

Examples:

- scene packaging should fail if it is about to emit `cooked/Fonts/DemoDiscBody.hefont`
- builder staging should fail if a cooked artifact relative path contains uppercase characters
- runtime catalog creation should fail if a generated entry is not canonical

The system should not auto-recover at runtime through case-insensitive probes or duplicate alias entries.

## Testing

### Shared engine tests

- canonical path normalizer returns lowercase forward-slash paths
- non-canonical packaged/runtime paths fail validation
- runtime asset id generation remains stable across slash and case variations by normalizing to the same canonical key

### Packaging tests

- default editor/debug font emits `cooked/fonts/default.hefont`
- cooked source fonts emit lowercase logical paths regardless of authored source folder casing
- scene packaging fails when a non-canonical packaged/runtime path would be emitted

### Platform tests

- 3DS packaged builds contain lowercase logical paths that match runtime lookup exactly
- PS2 export maps canonical logical paths to uppercase disc-native physical paths without changing the shared logical identity

## Success Criteria

This work is complete when:

- every packaged/runtime logical path emitted by shared engine and platform builders is canonical lowercase
- mixed-case packaged/runtime logical paths fail during build/package validation
- PS2 still boots using its physical path mapping layer
- 3DS no longer hits case-sensitive missing-file faults caused by mixed-case cooked references
