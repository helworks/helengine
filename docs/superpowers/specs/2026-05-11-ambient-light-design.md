# Ambient Light Design

## Summary

Add an `AmbientLightComponent` to the engine as a global 3D lighting source.

Ambient light should:

- affect only 3D lit materials
- be authored as a scene component like the existing light families
- be runtime-loadable and exportable like the existing light families
- stack on Windows by summing all visible ambient lights into one final ambient contribution

Ambient light should not:

- consume one of the explicit forward light slots
- behave like a spatial light with range or direction
- affect sprites, text, or UI in this change

## Problem

The engine currently supports:

- directional lights
- point lights
- spot lights

Those are direct-light sources and already flow through the explicit forward-light shader contract. There is no shared scene light type for broad baseline illumination.

That leaves scenes without a clean way to author:

- subtle base fill light
- stacked mood lighting
- animated whole-scene lighting changes driven by scripts, triggers, or timeline behavior

Trying to fake ambient light as another forward-slot light type would be the wrong contract. Ambient light is not a positional or directional emitter, and it should not burn limited direct-light slots.

## Goals

- Add `AmbientLightComponent` as a first-class engine light component.
- Make ambient lights serializable in scenes and loadable at runtime.
- Keep the component generic and reusable across projects.
- Feed ambient light into the 3D forward shading path as a separate accumulated term.
- Stack multiple ambient lights on Windows by summing their contributions.
- Preserve the existing direct-light slot model for directional, point, and spot lights.

## Non-Goals

- No ambient effect for UI, text, sprites, or other 2D content.
- No spatial ambient volumes, probes, or falloff.
- No renderer-wide decision that all platforms must stack the same way forever.
- No scene-settings-only fallback instead of a real component.
- No physically based lighting overhaul in this change.

## Runtime Contract

### AmbientLightComponent

`AmbientLightComponent` should derive from `LightComponent`.

It should use the existing common light fields:

- `Color`
- `Intensity`
- `Enabled`

It should not add:

- direction
- range
- spot angles
- shadow settings beyond whatever is already inherited and explicitly disabled by policy

Ambient light is global and non-spatial in this design.

### Light Type

The core light model should recognize ambient as a distinct light family so tooling, serialization, and runtime code can identify it cleanly.

However, renderer behavior remains renderer-owned. The presence of an ambient light type in core does not mean every renderer must consume it the same way internally.

### Stacking Policy

Core should allow multiple ambient lights in the same scene.

Windows should stack them by summing their final contributions into one accumulated ambient term.

This is intentionally renderer policy, not a core single-instance rule. The engine should not reject multiple ambient lights or enforce “first one wins.”

## Serialization and Scene Authoring

Ambient light should follow the same scene pipeline shape as the other light families:

- editor persistence descriptor
- tagged-field scene save/load
- runtime binary payload serialization
- runtime component deserializer
- export packaging rewrite

That means authored scenes, runtime scene loading, and Windows export should all understand:

- `helengine.AmbientLightComponent`

The common light field encoding should be reused where possible so the new family does not invent a separate light serialization shape unnecessarily.

## Rendering Design

### Separate Ambient Channel

Ambient light should not enter the existing explicit forward-light slot array.

Instead, the renderer should:

1. collect visible ambient lights
2. accumulate their final color contribution
3. upload that accumulated ambient term through a dedicated shader input

This keeps the ambient contract clean and avoids wasting direct-light slots on something that is not a direct emitter.

### Windows / DirectX11

On Windows, the forward lighting path should sum all visible ambient lights into one ambient color term:

`AccumulatedAmbient = sum(light.Color * light.Intensity)`

That accumulated term should be uploaded to the standard forward shader and added to the final lit surface result independently of directional, point, and spot evaluation.

The direct-light buffer remains responsible only for explicit light slots.

### Shader Contract

The built-in standard forward shader should gain a dedicated ambient input buffer or equivalent explicit field in the current lighting constant-buffer contract.

The shader should:

- evaluate direct lights the same way it does today
- add the accumulated ambient contribution to the shaded result for 3D materials

This should be a clean additive ambient term, not a fake extra directional light path.

## Architecture

### Core

Core needs:

- `AmbientLightComponent`
- updated light-type recognition
- runtime scene payload serializer support
- runtime deserializer registration

The shared render-frame extraction path should continue to surface ambient lights as visible scene lights, while renderers decide how to consume that family.

### Editor

Editor needs:

- scene persistence descriptor for ambient light
- registration in editor session persistence
- packaging transform support for Windows export
- add-menu/editor creation path if light creation menus already expose other light families through the same surface

If there is an existing “Add Light” flow, ambient should appear there as a sibling to directional, point, and spot.

### DirectX11

DirectX11 needs:

- ambient accumulation logic
- shader-data structure updates
- shader binding updates
- built-in shader support

This should remain separate from shadow logic and separate from explicit direct-light slot packing.

## Error Handling and Compatibility

- Multiple ambient lights should be valid.
- Missing ambient-light support in older payload versions should fail the same way other unsupported component payload/version mismatches fail.
- Existing scenes without ambient lights should remain unchanged.
- Existing direct-light behavior should remain unchanged when no ambient lights are present.

## Testing

Add targeted regressions for:

- `AmbientLightComponent` default construction and light-type identity
- editor persistence round-trip
- runtime scene payload read/write
- runtime scene loading materializing ambient lights
- export packaging rewriting ambient light records correctly
- DirectX11 ambient accumulation stacking multiple ambient lights
- standard forward shader contract including the ambient input
- scenes without ambient lights continuing to render under the existing direct-light path

At minimum, there should be one renderer test proving Windows stacks:

- red ambient light
- blue ambient light

into the expected summed shader input.

## Success Criteria

The feature is complete when:

- users can add and save `AmbientLightComponent` in scenes
- runtime scene loading materializes ambient lights correctly
- Windows export preserves ambient lights
- Windows forward rendering applies ambient lighting to 3D materials
- multiple ambient lights stack on Windows
- no direct-light slot regressions are introduced
