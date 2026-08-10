# Nested Environment Overrides

## Problem

HelEngine currently supports per-platform cooking and platform-specific authored overrides. Projects also need optional build-environment variants such as `debug`, `release`, `staging`, or project-defined environments. These variants must not make ordinary authoring more complex or duplicate platform payloads.

The first consumer is a Tilt Trial debug level label that is emitted for a debug environment on selected platforms, but the mechanism must apply consistently to entities and every cookable asset type.

## Decision

Environment overrides are an optional second layer nested inside an existing platform override. They are not a global parallel override dimension.

Resolution is ordered as:

```text
base payload -> platform payload -> optional environment payload for that platform
```

An environment payload inherits the selected platform payload. A platform with no environment overrides continues to cook exactly as it does today, regardless of the selected build environment.

## Environment Registry

Each project owns `settings/environments.json`. It contains stable environment identifiers and display names.

The registry is seeded on first use with two protected environments:

- `debug`
- `release`

Protected environments are visible but cannot be renamed or deleted. Custom environments can be created, renamed, and deleted. Identifiers are unique, non-empty, and stable after creation; renaming changes only the display name.

Deleting a custom environment requires confirmation. It removes nested overrides that reference that environment and resets persisted build selections that reference it to `release`.

## Editor Experience

`Tool -> Environments` opens the project environment manager. It lists the protected entries and custom entries, supports adding custom environments, and provides rename/delete actions only for custom entries.

Existing platform override controls remain the default experience. On the right side of a selected platform override, a `+` action creates the optional environment-override layer for that platform. Until the action is used, no environment controls are displayed for that platform or asset.

Once enabled, the environment area uses the same row, inheritance, add, remove, and property-editing pattern as platform overrides. Authors select an environment from the project registry, then edit only values that differ from the platform payload. Removing the final environment override removes the optional layer and restores the normal platform-only view.

This pattern is implemented by the shared authoring override infrastructure and exposed for all cookable payloads: entities and scene components, textures, materials, models, audio, animations, and future asset types that already support platform overrides.

## Build and Cook Contract

Build selection remains platform-first. After a platform is selected, a build environment is selected; the persisted default is `release`. Canonical `debug` and `release` build profiles preselect their matching protected environment, while other build profiles retain the user’s chosen environment.

The selected `environmentId` is carried through local build settings, queued builds, CLI invocations, prebuild context, build manifests, and cook requests. The cooker resolves every payload with the selected platform and then its environment override, when present.

Environment overrides may change every payload field available in the corresponding platform override, including entity existence. The final cooked artifact contains only the resolved result; it does not carry environment-override metadata into runtime.

## Compatibility and Migration

Existing projects without `settings/environments.json` load successfully and receive the protected default registry on first save. Existing scenes and assets have no environment overrides and therefore retain their present platform-only cooked output.

The build pipeline defaults absent or invalid persisted environment selections to `release`, while reporting invalid explicit CLI environment identifiers as errors.

## Validation

Tests will prove:

1. Registry seeding, validation, persistence, and protected-entry behavior.
2. Tool-menu command and environment-manager dialog behavior.
3. Nested environment override creation, inheritance, edit, and removal behavior in the shared override UI.
4. Deleting a custom environment removes its references and resets build selections to `release`.
5. Build dialog, queue, CLI, prebuild context, manifest, and cook requests preserve the selected environment.
6. Cook resolution applies base, platform, and environment values in that order for entities and representative asset types.
7. Existing platform-only assets cook identically when no environment override is present.
8. Tilt Trial’s debug-level label can use `debug` plus platform overrides without appearing in release or excluded-platform artifacts.

## Out of Scope

- Runtime switching between environments.
- Environment-specific project source trees or asset directories.
- Replacing platform build profiles with environments.
- A global environment override that bypasses platform selection.
