# Core Platform Manager Design

## Goal

Add a runtime-owned `PlatformManager` to `Core` so running game code can read the built platform name and builder-stamped version from `Core.Instance.PlatformManager`.

## Decision Summary

- `Core` will expose `PlatformManager` directly as a required shared runtime service.
- `PlatformManager` will be a lightweight concrete data class, not a per-platform subclass hierarchy.
- `PlatformManager` will expose:
  - `Name` as `string`
  - `Version` as `int`
- `Core.Initialize(...)` will require a `PlatformManager` argument and throw when it is missing.
- Packaged builders will stamp the selected platform name and builder version into generated startup/runtime metadata.
- Runtime bootstrap code will construct the runtime `PlatformManager` from the stamped metadata and inject it into `Core.Initialize(...)`.

## Why This Approach

This keeps the feature as small and explicit as possible. The user only needs two runtime-facing values right now: platform id and build version. A single concrete `PlatformManager` data object gives that shape without forcing a platform-specific class hierarchy that adds ceremony but no useful behavior.

Making the dependency required during `Core.Initialize(...)` prevents silent fallback to fake or placeholder platform values. That matters because the point of the version is stale-build verification. If the runtime can quietly default to `"unknown"` or `0`, the test signal becomes weak and easy to miss.

Stamping the values through generated startup/build metadata ensures the running player reports what was actually built, not whatever the editor or host happens to think the platform is.

## Public API

Add a new runtime type:

- `PlatformManager`

Expected shape:

```csharp
public class PlatformManager {
    public PlatformManager(string name, int version) { }

    public string Name { get; }
    public int Version { get; }
}
```

`Core` will expose:

- `public PlatformManager PlatformManager { get; private set; }`

`Core.Initialize(...)` and `EditorCore.Initialize(...)` will require one `PlatformManager` argument.

## Runtime Behavior

At runtime:

1. The platform/bootstrap layer resolves the built platform name and stamped build version.
2. It constructs `new PlatformManager(name, version)`.
3. It passes that instance into `Core.Initialize(...)`.
4. `Core` stores it on `Core.Instance.PlatformManager`.
5. Runtime code reads:
   - `Core.Instance.PlatformManager.Name`
   - `Core.Instance.PlatformManager.Version`

This is runtime-facing metadata only for now. No additional platform behavior is attached to the manager in this slice.

## Build And Packaging Flow

The selected builder will provide:

- platform name
- build version

Those values will be written into generated startup/runtime metadata alongside the existing startup manifest data used by packaged players.

The generated startup/bootstrap path will read those stamped values and create the `PlatformManager` from them before core initialization begins.

This gives a direct stale-build test loop:

1. bump builder version
2. rebuild platform output
3. run game
4. confirm runtime-reported `PlatformManager.Version` changed

## Editor And Test Harness Impact

All direct `Core.Initialize(...)` and `EditorCore.Initialize(...)` callers in tests, editor bootstraps, and lightweight runtime harnesses must now pass a concrete `PlatformManager`.

For tests and editor-only callers, a simple value like:

- `new PlatformManager("windows", 1)`

is sufficient unless a test is specifically validating stamped platform metadata behavior.

## Error Handling

`Core.Initialize(...)` throws `ArgumentNullException` when `platformManager` is missing.

`PlatformManager` construction should reject:

- blank or whitespace `name`
- negative `version`

Generated startup/bootstrap code should also fail fast if stamped platform name or version is missing or invalid, because that indicates a broken builder/runtime contract.

## Testing

Add focused coverage for:

1. `Core.Initialize(...)` stores the injected `PlatformManager`
2. `Core.Initialize(...)` throws when `PlatformManager` is missing
3. existing core/editor test harnesses are updated to supply a test platform manager
4. generated startup/runtime metadata includes the stamped platform name and version
5. packaged platform build tests can read the runtime values back to verify builder-version changes are observable in the built player

Tests should verify behavior, not implementation details like the exact codegen file names unless those tests already work at that level.

## Out Of Scope

This design does not include:

- per-platform runtime manager subclasses
- additional platform metadata beyond `Name` and `Version`
- moving existing builder-selection/editor platform discovery logic into `PlatformManager`
- a fallback/default platform manager
- runtime feature flags or capability queries on `PlatformManager`

## Files Expected To Change

- `engine/helengine.core/Core.cs`
- one new runtime file under `engine/helengine.core/`
- `engine/helengine.editor/EditorCore.cs`
- packaged startup/codegen files under editor build/runtime generation
- core/editor/runtime tests that initialize `Core`
- packaged build graph tests that validate generated startup metadata

## Acceptance Criteria

- Runtime code can read `Core.Instance.PlatformManager.Name`
- Runtime code can read `Core.Instance.PlatformManager.Version`
- `Core.Initialize(...)` requires a `PlatformManager`
- Missing `PlatformManager` causes initialization to throw
- Builders stamp platform name and version into generated startup/runtime metadata
- Packaged runtimes initialize `PlatformManager` from stamped build data
- Tests cover core injection and packaged runtime stamping behavior
