# Camera Component Generic Persistence Design

## Goal

Make `CameraComponent` persist through the generic reflected scene-component pipeline instead of the dedicated camera serializer/deserializer path.

The result should remove camera-specific scene persistence code without changing authored scene behavior, packaged runtime behavior, or editor camera workflows.

## Scope

In scope:

- `CameraComponent` scene persistence
- editor scene save/load behavior for cameras
- packaged scene transformation for cameras
- runtime scene loading for packaged cameras
- editor camera suppression behavior where it intersects persistence

Out of scope:

- redesigning the full camera/render architecture
- changing how editor viewport cameras work
- changing camera authoring UX beyond what is required to stop using suppression as authored storage
- removing the remaining explicit mesh persistence path

## Current Problem

`CameraComponent` is not currently a clean generic persistence candidate because the live component mixes authored scene state with runtime/editor-only state.

Today the engine solves that by hardcoding camera persistence in three places:

- editor scene persistence through `CameraComponentPersistenceDescriptor`
- build-time scene packaging through `SceneComponentPackagingTransformService`
- packaged runtime loading through `RuntimeCameraComponentDeserializer`

The dedicated editor descriptor also reads authored values from `EditorSceneCameraSuppressionComponent` instead of from the live `CameraComponent`. That is the main architectural blocker. If the explicit camera descriptor is removed without changing suppression, the generic serializer will save the suppressed live values instead of the authored values.

## Constraints

- `CameraComponent` authored values must remain the source of truth for scene persistence.
- runtime-only objects such as `RenderTarget` must not become persisted scene data.
- packaged scenes must still load on targets that disable runtime script reflection.
- the refactor should be minimal and local; it should not require a new generalized authoring-data framework.

## Design

### 1. Make `CameraComponent` persistence-friendly

`CameraComponent` should expose only authored scene state to the generic persistence surface.

The component already exposes most authored values in a generic-friendly shape:

- `CameraDrawOrder`
- `LayerMask`
- `Viewport`
- `NearPlaneDistance`
- `FarPlaneDistance`
- `ClearSettings`
- `RenderSettings`

Those members are compatible with the reflected serializer and runtime automatic deserializer. `CameraClearSettings` and `CameraRenderSettings` are already persistence-friendly value/object types.

The problem member is `RenderTarget`. It is runtime GPU state and must be excluded from scene persistence. The minimal fix is to add `[ScenePersistenceIgnore]` to `RenderTarget`.

### 2. Stop storing authored camera values in suppression metadata

`EditorSceneCameraSuppressionComponent` currently acts as hidden authored storage for camera values while the editor suppresses the live scene camera. That makes persistence camera-specific because save/load must know to read the hidden component instead of the real camera.

This design removes that responsibility from suppression metadata.

After the refactor:

- `CameraComponent` always owns the authored values
- `EditorSceneCameraSuppressionComponent` becomes editor runtime metadata only, or a pure marker if no additional data is needed
- the properties panel reads and writes camera values directly on `CameraComponent`

### 3. Make suppression non-destructive

The editor should stop mutating persisted camera fields to suppress runtime rendering.

Today suppression forces inert live state by overwriting:

- `LayerMask`
- `ClearSettings`

That mutation is exactly what makes generic persistence unsafe.

Instead, suppression should be enforced by editor runtime behavior:

- editor viewport/render selection should explicitly ignore suppressed scene cameras
- authored camera values remain unchanged on the live component

This keeps authoring state and runtime suppression separate.

### 4. Let camera fall through the generic editor persistence path

Once authored values remain on `CameraComponent` and `RenderTarget` is ignored, the explicit editor registration of `CameraComponentPersistenceDescriptor` can be removed.

`ComponentPersistenceRegistry` will then route camera through `AutomaticScriptComponentPersistenceDescriptor`, which already handles:

- primitive values
- `ushort`
- `float4`
- enums
- arrays/dictionaries where supported
- nested authored objects and classes with public writable members

### 5. Let camera fall through the generic packaging/runtime path

After editor persistence is generic, build packaging should stop rewriting camera records through a dedicated binary contract.

Instead:

- camera scene records remain generic reflected payloads during editor save
- build packaging rewrites them through the existing automatic component packaging path
- runtime loading uses automatic runtime deserialization or generated runtime deserializers when reflection is disabled

This removes the dedicated `RuntimeCameraComponentDeserializer` path and aligns camera with other generic components.

## Recommended Implementation Shape

### Core

Modify `CameraComponent` so the reflected persistence surface is explicit and safe:

- add `[ScenePersistenceIgnore]` to `RenderTarget`
- keep authored camera members public and writable
- do not add camera-specific persistence helpers back into the core component

### Editor suppression

Refactor editor suppression so it no longer rewrites authored camera values:

- remove or shrink `EditorSceneCameraSuppressionComponent` data ownership
- remove `TryGetAuthoredPropertyValue` and `TrySetAuthoredPropertyValue` proxy usage
- update the editor to treat suppressed cameras as render-ineligible without mutating their persisted fields

### Editor persistence

Remove explicit camera descriptor registration from editor services once suppression no longer changes authored state.

`CameraComponentPersistenceDescriptor` should then be deleted.

### Packaging/runtime

Remove:

- camera-specific packaging transform logic
- camera-specific runtime deserializer registration
- the dedicated `RuntimeCameraComponentDeserializer`

Camera loading should use the same reflected/generated infrastructure used by generic persisted components.

## Risks

### Editor viewport suppression regressions

If suppression is changed incompletely, scene cameras may start rendering in editor views again. This is the main behavioral risk and should be verified early.

### Reflection-disabled runtime targets

Targets that disable runtime reflection depend on generated runtime deserializer output. Camera must still be emitted by the generation pipeline after the dedicated runtime deserializer is removed.

### Save/load compatibility

Existing camera scene files may contain the current dedicated payload shape. The migration path needs to be decided explicitly:

- either preserve compatibility during transition
- or accept a clean break if legacy camera scene payloads are no longer supported

This should be decided before deleting the legacy camera payload reader paths.

## Verification

Add or update focused tests for:

- generic save of a camera with authored values
- load of that saved camera without explicit camera descriptor registration
- editor camera suppression preserving authored values while preventing editor rendering
- packaged scene build for a camera component through the automatic path
- runtime load of packaged camera data on both reflection-enabled and generated-deserializer paths
- `RenderTarget` exclusion from persistence

## Recommendation

Take the minimal structural refactor, not a serializer-only deletion.

The correct cut is:

1. exclude runtime-only camera members from persistence
2. stop using suppression metadata as authored storage
3. make suppression non-destructive
4. remove the explicit camera serializer/deserializer path

That is the smallest path that makes camera genuinely generic instead of only moving the special case somewhere else.
