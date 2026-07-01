# Generic Endian-Aware Binary Enforcement Design

## Goal

Make endian-aware Helengine serialization mandatory for every binary payload boundary so plugins cannot inject host-native binary blobs directly into cooked output.

## Problem

Helengine already exposes generic endian-aware primitives such as `EngineBinaryWriter`, `EngineBinaryReader`, and `EngineBinaryHeaderSerializer`.

That is not enough today because plugin and subsystem code can still bypass those primitives by:

- building opaque `byte[]` payloads in host-native memory layout
- storing those bytes in cooked runtime data types
- reading those bytes back later through runtime code that assumes native host endianness

The static-mesh BEPU failure on GameCube is only the first visible symptom. The underlying architectural problem is broader:

- Helengine permits cooked binary payloads that are not serialized through Helengine-owned endian-aware contracts
- platform endianness metadata exists in `helengine.baseplatform`, but that metadata is not enforced at every binary serialization boundary
- plugins are able to decide their own binary layout implicitly by writing raw bytes instead of using Helengine readers and writers

## Required Outcome

Helengine must own binary serialization everywhere.

That means:

- all cooked binary payloads must be written through Helengine-owned endian-aware writers
- all cooked binary payloads must be read through Helengine-owned endian-aware readers
- plugins must provide field-level serialization logic, not prepacked opaque binary blobs
- payload endianness must be chosen by Helengine from platform metadata, not by plugin code
- old cooked payload compatibility is out of scope and may be abandoned

## Non-Goals

- No backward-compatibility layer for already cooked opaque payloads.
- No GameCube-specific payload workaround.
- No BEPU-specific byte-order workaround.
- No runtime "best effort" auto-detection of broken payloads.

## Existing Context

The current engine already contains the core building blocks needed for a generic solution:

- `EngineBinaryWriter`
- `EngineBinaryReader`
- `EngineBinaryHeaderSerializer`
- `PlatformSerializationEndianness`
- `PlatformCookProfileCapabilities`

The missing piece is enforcement.

Today, opaque `byte[]` payloads can cross engine boundaries without proving that they were serialized through Helengine's binary abstractions.

## Approaches

### Approach 1: Wrap Existing Opaque `byte[]` Payloads With More Validation

This would keep runtime payload fields as opaque byte arrays and only add validation that the payload appears to contain a Helengine binary header or a plausible byte-order marker.

Pros:

- Smallest code change.
- Minimal disruption to current payload types.

Cons:

- Still preserves the architectural escape hatch.
- Still allows plugins to manufacture arbitrary binary blobs.
- Validation happens after the wrong abstraction already crossed the boundary.

Recommendation: reject.

### Approach 2: Keep Opaque `byte[]`, But Require Serializer Registrations

This would keep `byte[]` storage in runtime payload types but add a required serializer and deserializer registration layer in Helengine. Plugins would register serializer implementations while the stored payload would remain opaque bytes.

Pros:

- Better than ad hoc raw blobs.
- Gives Helengine more control over read and write paths.

Cons:

- Still encodes "opaque blob" as an acceptable engine contract.
- Makes it too easy for callers to continue building the byte arrays outside Helengine and only attach them later.
- Enforcement remains weaker than the requirement.

Recommendation: reject.

### Approach 3: Remove Raw Blob Boundaries And Make Helengine Serialization The Only Binary Contract

This makes Helengine-owned serialization mandatory at the engine boundary. Plugins contribute structured serialization logic through Helengine interfaces, while Helengine owns:

- writer creation
- reader creation
- endianness selection
- headers
- field encoding
- payload decoding

Pros:

- Matches the required architecture exactly.
- Prevents recurrence of the same class of bugs across all binary payloads.
- Makes endianness generic and platform-driven.
- Keeps plugins unaware of byte order.

Cons:

- Broad change touching multiple cook and load seams.
- Requires migration of existing opaque cooked payload shapes.

Recommendation: adopt.

## Recommended Design

### 1. Promote Endian-Aware Serialization To A Mandatory Engine Boundary

Helengine should treat binary payload serialization the same way it already treats scene and asset serialization:

- the engine defines the binary contract
- callers provide data, not final bytes

Any API that currently accepts or returns cooked opaque binary payloads directly should be reshaped so the engine owns the serialization act.

### 2. Replace Opaque Binary Payload Contracts With Structured Runtime Payload Contracts

Runtime payload container types should stop exposing "opaque cooked bytes from anywhere" as the primary contract.

Instead, cooked payload types should represent one of these patterns:

- explicit typed fields
- engine-owned structured records
- engine-owned payload objects that know how to serialize and deserialize themselves through `EngineBinaryWriter` and `EngineBinaryReader`

The important rule is:

- plugin code may describe payload contents
- plugin code may not provide already-packed binary blobs as the authoritative cooked result

### 3. Introduce Generic Binary Payload Serialization Interfaces In Helengine

Add a small generic contract in Helengine for binary payload participants.

The engine-level shape should be conceptually similar to:

- one writer-facing interface for serialization
- one reader-facing factory or static deserializer path for reconstruction

The contract should require:

- writing through `EngineBinaryWriter`
- reading through `EngineBinaryReader`
- no direct native-memory reinterpret casts across the engine boundary

The interface names can follow current engine naming patterns, but the architectural rule matters more than the exact names.

### 4. Move Platform Endianness Selection Into Helengine Build And Load Services

Platform selection should come from existing platform metadata, not from plugins.

At cook time:

- Helengine resolves `PlatformSerializationEndianness` from the selected platform profile
- Helengine creates the correct `EngineBinaryWriter`
- Helengine passes that writer into the payload serializer

At runtime:

- Helengine reads the payload header
- Helengine creates the correct `EngineBinaryReader`
- Helengine reconstructs the payload through the engine-owned deserializer

Plugins should never branch on little-endian vs big-endian.

### 5. Require Helengine Binary Headers For Custom Binary Payloads

Every custom cooked binary payload should carry a Helengine binary header, not only top-level asset files.

That header should identify:

- payload format id
- payload version
- payload endianness
- record and value kind metadata needed by the existing engine conventions

This keeps payload validation uniform and gives Helengine one generic way to reject malformed or wrong-version binary payloads.

### 6. Make Raw `byte[]` Insertion Impossible Or Clearly Internal-Only

APIs that currently allow callers to supply raw cooked bytes directly should be removed or narrowed so they cannot be used by plugin authors as a primary extension path.

If some `byte[]` storage remains internally for transport or persistence reasons, it should only be populated by Helengine-owned serializers after the engine has:

- selected endianness
- written the header
- encoded fields through engine-owned primitives

### 7. First Migration: Static Mesh Collision Runtime Data

Use the BEPU static-mesh payload as the first migration because it already demonstrates the failure mode.

The migration should:

- stop storing raw `Mesh.Serialize(...)` output as the cooked contract
- define a Helengine-owned binary representation for static mesh collision runtime data
- serialize triangle data and any required metadata through `EngineBinaryWriter`
- reconstruct the BEPU mesh through Helengine-owned deserialization before handing the result to BEPU

The BEPU runtime should receive reconstructed BEPU data structures, not BEPU-authored serialized bytes from disk.

### 8. Broader Migration Rule

After the first migration, the same rule should be applied to every binary payload seam:

- cooked runtime data objects
- plugin-owned binary caches
- runtime component binary payloads
- any asset or sidecar format that currently bypasses Helengine binary readers and writers

The policy should be simple:

- if bytes are persisted or loaded across platform boundaries, Helengine serialization owns them

## Enforcement Strategy

The engine must make the correct path the easy path and the incorrect path unavailable.

Recommended enforcement:

- new binary payload abstractions require `EngineBinaryWriter` and `EngineBinaryReader`
- code review and tests should treat raw host-native binary blob insertion as a bug
- existing APIs that expose raw cooked `byte[]` authoring should be deprecated and then removed during migration

## Validation Strategy

Validation should be generic and engine-level.

### Source-Level Coverage

Add tests that prove:

- payload serializers can write both little-endian and big-endian output through the same serializer logic
- payload deserializers reconstruct identical semantic data from both byte orders
- wrong or missing headers fail hard
- runtime build and load services select writer and reader endianness from platform metadata

### Regression Coverage

Add one regression test that would have caught the current bug:

- cook one static-mesh collision payload through Helengine-owned big-endian serialization
- load it back through the runtime deserializer
- build the BEPU mesh successfully without platform-specific conditional logic in plugin code

### Policy Coverage

Add tests or guardrails that verify engine-managed binary payload seams do not accept arbitrary prepacked host-native blobs from plugin code.

## Risks

### Migration Surface

The change is cross-cutting. Multiple runtime payload types may depend on implicit raw-byte behavior today.

Mitigation:

- introduce the generic serializer contract first
- migrate one payload family at a time
- keep validation focused on engine boundaries

### Partial Adoption

If only the BEPU path is migrated and other opaque payload seams remain, the architecture stays vulnerable.

Mitigation:

- document raw opaque cooked bytes as unsupported
- audit all binary payload seams after the first migration

## Implementation Outline

1. Add generic engine-owned binary payload serialization interfaces and helpers.
2. Update cook and load services to own endianness selection from platform metadata.
3. Remove or narrow raw opaque cooked-byte authoring APIs.
4. Migrate static-mesh collision runtime data to the new contract.
5. Add endian-specific regression tests.
6. Audit and migrate remaining binary payload seams.

## Recommendation

Adopt Approach 3.

Helengine already has the primitives needed for endian-aware serialization. The missing move is to make those primitives mandatory everywhere and remove the opaque raw-byte escape hatch from plugin-facing cooked payload flows.
