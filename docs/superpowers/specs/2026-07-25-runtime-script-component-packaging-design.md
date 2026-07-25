# Runtime Script Component Packaging Design

## Problem

Platform asset cooking accepts only component types declared by a platform support table or resolved through the editor persistence registry. A project runtime component can already be selected for native generated-core deserialization but still fail this packaging gate when its automatic persistence descriptor is not present in the registry. This creates an invalid contradiction: native code exists for the component but its authored scene cannot be packaged.

## Decision

Make platform packaging treat the component types selected for generated native runtime code as supported automatic runtime components. The set must be supplied explicitly by the build graph, not inferred from a platform-specific hard-coded list.

The existing support table remains authoritative for built-in transforms and explicitly unsupported types. Components absent from both the support table and the generated-runtime set continue to fail with the existing actionable error.

## Data Flow

1. Generated-core discovery selects eligible script component types referenced by the cooked scenes.
2. The build graph passes that exact type-id set to the scene packager.
3. When normal persistence-descriptor lookup fails, the packager accepts a type only when it belongs to that generated-runtime set and emits the automatic packaged payload.
4. All other unknown types remain rejected.

## Validation

Add a focused editor test that packages a scene containing an automatic script component whose persistence descriptor is unavailable but whose type id is declared generated-runtime. The test fails on current behavior and passes after the change. A companion test verifies an unknown component remains rejected.
