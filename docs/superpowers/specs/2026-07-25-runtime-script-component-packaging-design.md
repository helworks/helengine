# Runtime Script Component Packaging Design

## Problem

Persisted scenes identify automatic script components with `namespace.Type, moduleId`. Declaring a source subdirectory as a new runtime module changes that module id. Existing scenes retain the previous module id, so the script resolver cannot locate the moved type; the packager then reaches its generic unsupported-component error.

## Decision

Preserve authored scenes across runtime module moves. When the recorded module does not contain the requested type, resolve the type name across loaded runtime module assemblies only when there is exactly one match. The packager must emit that type's current canonical component id, so generated native deserializers and cooked scene records agree.

The existing support table remains authoritative for built-in transforms and explicitly unsupported types. An unknown or ambiguous type name remains rejected with an actionable error; the fallback never guesses between multiple script types.

## Data Flow

1. The script resolver receives a persisted component type id.
2. It first performs the exact module-id lookup used today.
3. On a missing module or missing type, it searches the registered runtime assemblies for the type's full name and accepts exactly one match.
4. Scene packaging deserializes through that resolved type and writes its current canonical type id into the cooked record.
5. Generated-core discovery receives the canonical cooked record and emits the matching native deserializer.

## Validation

Add a resolver test for a uniquely moved type and a rejection test for ambiguous type names. Add a packaging regression test that starts with a legacy `, gameplay` record, resolves a component in another runtime module, and verifies the cooked record uses the new canonical module id. The test must fail on current behavior.
