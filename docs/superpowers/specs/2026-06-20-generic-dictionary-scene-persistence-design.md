# Generic Dictionary Scene Persistence Design

## Goal

Add engine-wide generic dictionary persistence support so components with safe dictionary-shaped data can flow through the shared automatic scene serialization and runtime deserialization systems without bespoke descriptor or deserializer classes.

The first consumer is `SceneMapComponent`, which should keep its runtime behavior while dropping its custom persistence stack.

## User Direction

- Keep `SceneMapComponent` behavior.
- Remove unnecessary bespoke persistence/deserialization classes.
- Add broader generic dictionary support instead of solving only `SceneMapComponent`.
- Keep dictionary keys restricted to a safe deterministic subset.
- Do not preserve backward compatibility with existing `SceneMapComponent` saved or cooked payloads.

## Problem

The current generic systems support:

- scalar leaf values
- enums
- arrays
- nested writable public objects
- a limited set of asset-backed member types

They do not support `Dictionary<TKey, TValue>`.

Because of that gap, `SceneMapComponent` requires:

- `SceneMapComponentPersistenceDescriptor`
- `RuntimeSceneMapComponentDeserializer`
- explicit editor registration
- explicit runtime registration

That is disproportionate to the actual data shape, which is just:

- `InitialSceneId`
- `Mappings`

The current system also has two runtime load environments:

- managed reflected runtime loading through `AutomaticScriptComponentRuntimeDeserializer`
- native C++ player builds through generated `GeneratedRuntime...Deserializer` classes and generated registration

Any dictionary feature must cover both paths or the engine will diverge across targets.

## Non-Goals

- Preserve old `SceneMapComponent` payload formats.
- Add arbitrary collection support beyond the scoped dictionary feature.
- Remove `SceneMapComponent` runtime singleton or startup-scene behavior.
- Generalize persistence for unsupported key types in the first pass.

## Current State

### `SceneMapComponent`

`SceneMapComponent` is simple data plus runtime behavior:

- `InitialSceneId : string`
- `Mappings : Dictionary<string, string>`
- singleton registration on add/remove/dispose
- startup-scene request on update
- scene-id remapping through `ResolveSceneId`

### Generic editor save path

`AutomaticScriptComponentPersistenceDescriptor` supports writable public members whose value types are already understood by the shared serializer.

It does not support dictionaries.

### Generic managed runtime load path

`AutomaticScriptComponentRuntimeDeserializer` supports the same reflected shape categories and also does not support dictionaries.

### Generic native runtime load path

The editor-generated C++ deserializer generator emits hard-coded deserializer classes for generic runtime loading in native player builds.

That generator also needs dictionary support.

## Supported Dictionary Scope

The feature will support:

- `Dictionary<TKey, TValue>`

with these constraints:

- `TKey` must be one of:
  - `string`
  - `byte`
  - `ushort`
  - `int`
  - `uint`
  - `long`
  - enums whose underlying type is one of the above supported integral types
- `TValue` must already be supported by the generic value system:
  - leaf scalar values
  - enums
  - arrays
  - nested writable public objects
  - supported asset-backed member types

Unsupported dictionary key types must fail fast with a clear exception.

Unsupported dictionary value types must fail through the same validation path already used by the generic serializer/deserializer.

## Data Format

Dictionary payloads will be encoded as:

1. entry count
2. ordered key/value entries

Ordering rules:

- entries must be serialized in deterministic key order
- key ordering must use ordinal string comparison for `string`
- integral keys use numeric ascending order
- enum keys use their underlying numeric value ordering

This keeps payloads stable across saves, codegen, and runtime targets.

## Design

### 1. Extend generic editor persistence

Update `AutomaticScriptComponentPersistenceDescriptor` so reflected members can serialize dictionary values.

Add shared dictionary detection and validation:

- detect `Dictionary<TKey, TValue>`
- validate supported key type
- validate supported value type through the existing generic value rules

Add dictionary writing:

- write entry count
- write sorted keys
- write each key then value using the existing generic supported-value helpers

Add dictionary reading:

- read count
- construct target dictionary
- read each key and value
- assign into dictionary

Duplicate keys in persisted payloads should throw, not silently overwrite.

### 2. Extend managed runtime reflected deserialization

Update `AutomaticScriptComponentRuntimeDeserializer` with the same dictionary support rules and binary layout.

Managed runtime behavior must mirror the editor reflected format exactly:

- same supported key constraints
- same deterministic key ordering assumptions
- same duplicate-key failure behavior
- same nested-value recursion rules

### 3. Extend native generated C++ runtime deserializers

Update the native generated runtime deserializer generator so reflected members typed as supported dictionaries emit native read logic.

The generator must:

- recognize supported dictionary shapes during schema/code generation
- emit dictionary construction code
- emit entry-count reading and looped key/value decoding
- delegate value decoding through the same generated logic already used for supported reflected member value categories

The native generated format must match the managed reflected runtime format byte-for-byte.

### 4. Remove bespoke `SceneMapComponent` persistence

Once generic dictionary support exists in all three places:

- remove `SceneMapComponentPersistenceDescriptor`
- remove its registration from `EditorSession.CreateComponentPersistenceRegistry`
- remove `RuntimeSceneMapComponentDeserializer`
- remove its explicit registration from `RuntimeComponentRegistry`

`SceneMapComponent` should then serialize through the shared automatic systems using:

- `InitialSceneId`
- `Mappings`

with no scene-map-specific persistence code.

### 5. Preserve `SceneMapComponent` runtime behavior

This design does not change:

- `Instance`
- `StartupSceneWasRequested`
- `ResolveSceneId`
- startup-scene loading behavior

Only persistence plumbing changes.

## Validation Rules

The feature must reject:

- unsupported dictionary key types
- dictionary keys that deserialize to null when the key type is non-nullable
- negative entry counts
- duplicate keys in serialized payloads

The feature must preserve:

- empty dictionaries
- null dictionary values when `TValue` already supports nulls
- deterministic save output for the same logical data

## Testing

### Editor persistence tests

Add tests proving:

- `Dictionary<string, string>` round-trips through the automatic descriptor
- supported integral-key dictionaries round-trip
- enum-key dictionaries round-trip
- nested-object and array dictionary values round-trip when already supported
- unsupported key types fail with the expected exception
- duplicate keys in payload fail on read
- save output is deterministic regardless of insertion order

### Managed runtime tests

Add tests proving:

- `AutomaticScriptComponentRuntimeDeserializer` restores supported dictionaries
- runtime and editor binary formats match
- `SceneMapComponent` round-trips through the generic path without bespoke classes

### Native generated runtime tests

Add codegen tests proving:

- generated native deserializers include dictionary decoding for supported shapes
- generated registration includes `SceneMapComponent` once bespoke exclusion is removed
- native generated outputs match the managed reflected format contract

### Regression tests

Add explicit tests that:

- `SceneMapComponent` no longer requires explicit descriptor/deserializer registration
- generic dictionary support does not regress existing generic array/nested-object handling

## Risks

### Native parity risk

The biggest risk is format drift between:

- editor reflected save
- managed reflected load
- native generated load

Mitigation:

- keep one binary shape
- add parity tests around representative dictionary shapes

### Key-ordering drift risk

Different ordering policies across managed and native targets would create nondeterministic payloads.

Mitigation:

- specify exact ordering rules in code and tests

### Over-broad scope risk

Unbounded dictionary support would create complex edge cases quickly.

Mitigation:

- constrain keys to the safe subset in this design
- reuse existing supported value rules for values instead of inventing new ones

## Recommended Implementation Order

1. Add failing editor persistence tests for supported and unsupported dictionaries.
2. Implement editor reflected dictionary save/load support.
3. Add failing managed runtime reflected deserialization tests.
4. Implement managed runtime reflected dictionary support.
5. Add failing native generated codegen tests.
6. Implement native generated dictionary deserialization support.
7. Remove bespoke `SceneMapComponent` persistence/deserialization classes and registrations.
8. Run focused regression suites for generic persistence, generated runtime deserializers, and scene-map loading.

## Result

After this change:

- `SceneMapComponent` keeps its behavior
- dictionary-shaped component data becomes a first-class generic persistence shape
- the engine loses one bespoke persistence stack
- future components with safe dictionary data can avoid custom serializer/deserializer boilerplate
