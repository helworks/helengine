# Spotlight Street Slice Design

## Summary

The second canonical rendering scene should be a spotlight-focused showcase built from the existing Riemers content already present in the city project. It should contrast clearly with the first directional-light scene by presenting a smaller, darker, more localized pool of light with a parked car and a lamppost as the visible source.

## Goals

- Showcase spot light rendering, falloff, and cone shape.
- Use existing authored Riemers models and textures instead of primitive-only placeholders.
- Keep the scene reusable later for other platform demos by preserving a simple, authored layout.
- Keep scene motion controlled and readable by using camera motion only.

## Scene Concept

The scene should read as a `night street slice`:

- one street-ground slice
- one static lamppost
- one parked racer
- one spotlight aligned with the lamppost
- sparse surroundings with mostly dark negative space
- camera motion only

This should feel like a small real scene, not a debug rig, while still making lighting behavior easy to judge.

## Asset Use

The scene should use the existing city project Riemers content for the imported geometry:

- `assets/models/Riemers/lamppost.x`
- `assets/models/Riemers/racer.x`

The implementation should prefer file-backed scene asset references for the Riemers models instead of generated primitives wherever the authored assets already exist.

For this pass, the scene should stay on the current standard material path rather than expanding the renderer/material system just to bind authored albedo textures. The existing Riemers texture files should remain available for later material-system work, but they are not a blocker for the spotlight scene composition pass.

## Lighting

The hero light should be one spotlight:

- aligned with or attached to the lamppost
- aimed downward across the street slice
- hitting both the ground and the racer body
- authored to produce a clear cone and readable falloff

The rest of the scene should stay dark and simple so the spotlight remains the obvious focal point.

## Motion

Motion should stay controlled:

- lamppost stays static
- racer stays parked
- spotlight stays static
- camera provides the only deliberate motion

This keeps spotlight behavior easy to inspect:

- cone shape
- falloff
- shadow response
- texture response under concentrated light

## Generator Structure

This scene should be added as a second canonical rendering scene in the same city-owned generation suite:

- extend `RenderingSceneGenerator` so it generates both the directional-light scene and the new spotlight scene
- add a new dedicated factory, such as `SpotlightStreetSliceSceneFactory`
- keep the existing first scene intact

The second scene should be authored as its own canonical scene asset rather than being folded into the first scene.

## Constraints

- Do not replace the first rendering scene.
- Keep the second scene city-owned, not engine-owned.
- Use the Riemers assets and textures that already exist in the city project.
- Keep the car parked.
- Keep the lamppost static.
- Keep the spotlight static.
- Keep camera motion only.

## Expected Outcome

The city rendering scene generator should now produce:

1. the existing directional-light city-block scene
2. a new spotlight-focused street slice using the Riemers lamppost and racer models with the existing standard material path

Together, these form the first two canonical rendering showcase scenes for the demo flow.
