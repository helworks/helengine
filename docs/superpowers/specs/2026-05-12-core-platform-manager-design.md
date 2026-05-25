# Core Platform Info Design

## Goal

Add runtime-owned `PlatformInfo` to `Core` so running game code can read the built platform id and builder-stamped version from `Core.Instance.PlatformInfo`.

## Decision Summary

- `Core` will expose `PlatformInfo` directly as a required shared runtime service.
- `PlatformInfo` will be a lightweight concrete data class, not a per-platform subclass hierarchy.
- `PlatformInfo` will expose:
  - `Name` as `string`
  - `Version` as `string`
- `Name` will use the stable platform id, like `windows` or an external package-owned platform id, not a human-readable display name.
- `Core.Initialize(...)` will require a `PlatformInfo` argument and throw when it is missing.
- Packaged builders will stamp the selected stable platform id and builder version into generated startup/runtime metadata.
- Runtime bootstrap code will construct the runtime `PlatformInfo` from the stamped metadata and inject it into `Core.Initialize(...)`.

## Why This Approach

This keeps the feature as small and explicit as possible. The user only needs two runtime-facing values right now: platform id and build version. A single concrete `PlatformInfo` data object gives that shape without forcing a platform-specific class hierarchy that adds ceremony but no useful behavior.

Making the dependency required during `Core.Initialize(...)` prevents silent fallback to fake or placeholder platform values. That matters because the point of the version is stale-build verification. If the runtime can quietly default to `"unknown"` or `0`, the test signal becomes weak and easy to miss.

Using a string version keeps the runtime shape aligned with existing builder metadata. Builders already publish `PlatformBuilderDescriptor.BuilderVersion` as a string, so keeping that type avoids inventing a second integer-only version field or introducing parsing constraints that do not help the runtime.

Stamping the values through generated startup/build metadata ensures the running player reports what was actually built, not whatever the editor or host happens to think the platform is.

## Public API

Add a new runtime type:

- `PlatformInfo`

Expected shape:

```csharp
public class PlatformInfo {
    public PlatformInfo(string name, string version) { }

    public string Name { get; }
    public string Version { get; }
}
```

`Core` will expose:

- `public PlatformInfo PlatformInfo { get; private set; }`

`Core.Initialize(...)` and `EditorCore.Initialize(...)` will require one `PlatformInfo` argument.

## Runtime Behavior

At runtime:

1. The platform/bootstrap layer resolves the built stable platform id and stamped builder version.
2. It constructs `new PlatformInfo(name, version)`.
3. It passes that instance into `Core.Initialize(...)`.
4. `Core` stores it on `Core.Instance.PlatformInfo`.
5. Runtime code reads:
   - `Core.Instance.PlatformInfo.Name`
   - `Core.Instance.PlatformInfo.Version`

This is runtime-facing metadata only for now. No additional platform behavior is attached to the type in this slice.

## Build And Packaging Flow

The selected builder will provide:

- stable platform id
- build version

Those values will be written into generated startup/runtime metadata alongside the existing startup manifest data used by packaged players.

The generated startup/bootstrap path will read those stamped values and create the `PlatformInfo` from them before core initialization begins.

This gives a direct stale-build test loop:

1. bump builder version
2. rebuild platform output
3. run game
4. confirm runtime-reported `PlatformInfo.Version` changed

## Editor And Test Harness Impact

All direct `Core.Initialize(...)` and `EditorCore.Initialize(...)` callers in tests, editor bootstraps, and lightweight runtime harnesses must now pass concrete `PlatformInfo`.

For tests and editor-only callers, a simple value like:

- `new PlatformInfo("windows", "1")`

is sufficient unless a test is specifically validating stamped platform metadata behavior.

## Error Handling

`Core.Initialize(...)` throws `ArgumentNullException` when `platformInfo` is missing.

`PlatformInfo` construction should reject:

- blank or whitespace `name`
- blank or whitespace `version`

Generated startup/bootstrap code should also fail fast if stamped platform name or version is missing or invalid, because that indicates a broken builder/runtime contract.

## Testing

Add focused coverage for:

1. `Core.Initialize(...)` stores the injected `PlatformInfo`
2. `Core.Initialize(...)` throws when `PlatformInfo` is missing
3. existing core/editor test harnesses are updated to supply test platform info
4. generated startup/runtime metadata includes the stamped stable platform id and builder version
5. packaged platform build tests can read the runtime values back to verify builder-version changes are observable in the built player

Tests should verify behavior, not implementation details like the exact codegen file names unless those tests already work at that level.

## Out Of Scope

This design does not include:

- per-platform runtime manager subclasses
- additional platform metadata beyond `Name` and `Version`
- moving existing builder-selection/editor platform discovery logic into `PlatformInfo`
- a fallback/default platform info object
- runtime feature flags or capability queries on `PlatformInfo`

## Files Expected To Change

- `engine/helengine.core/Core.cs`
- one new runtime file under `engine/helengine.core/`
- `engine/helengine.editor/EditorCore.cs`
- packaged startup/codegen files under editor build/runtime generation
- core/editor/runtime tests that initialize `Core`
- packaged build graph tests that validate generated startup metadata

## Acceptance Criteria

- Runtime code can read `Core.Instance.PlatformInfo.Name`
- Runtime code can read `Core.Instance.PlatformInfo.Version`
- `Core.Initialize(...)` requires `PlatformInfo`
- Missing `PlatformInfo` causes initialization to throw
- Builders stamp stable platform id and builder version into generated startup/runtime metadata
- Packaged runtimes initialize `PlatformInfo` from stamped build data
- Tests cover core injection and packaged runtime stamping behavior
