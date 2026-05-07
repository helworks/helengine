# Directional Shadow Plaza Design

## Summary

Add one canonical user-side rendering showcase scene focused on lighting and shadows:

- the first generated scene is `Directional Shadow Plaza`
- it is authored once as a single canonical scene asset
- it uses simple reusable geometry that can scale from Windows to PS2, GameCube, and Wii
- it defaults to attract mode with deterministic motion
- runtime animation behavior owns motion, while the generator owns stable authored scene data

The first slice should produce a scene that looks dynamic on Windows today without becoming Windows-specific in structure or authored content.

## Problem

The current rendering-scene pipeline already writes committed static validation scenes for focused renderer coverage, including shadow labs and smoke scenes. That is useful for diagnostics, but it does not yet provide a user-side showcase scene that feels alive, loops automatically, and can later become part of cross-platform demo content.

The scene generator should avoid two failure modes:

- building a Windows-only spectacle scene that cannot be reused for lower-end platforms later
- adding a second parallel scene-authoring system instead of extending the existing committed scene-writer model

The new slice needs one clear first target:

- a canonical authored scene
- simple enough to scale across platforms
- dynamic enough to show moving shadows and motion immediately
- structured so later platform/profile visibility rules can hide or show authored objects without changing scene identity

## Goals

- Generate one canonical rendering showcase scene focused on directional-light shadows.
- Keep the scene authored once and reusable later for Windows, PS2, GameCube, and Wii.
- Default to attract mode with deterministic camera and object motion.
- Use simple primitive-based geometry and strong silhouette differences.
- Keep stable entity ids and scene structure so later platform/profile visibility can prune scene detail without forking the scene.
- Extend the existing scene-writer pipeline instead of inventing a separate generator architecture.
- Keep authored scene generation separate from runtime animation behavior ownership.
- Fail fast when required scene-generation inputs are invalid.

## Non-Goals

- No platform-specific scene variants in the first version.
- No platform/profile visibility system in this slice.
- No Windows-only post-process or material-effects layer in the authored baseline scene.
- No per-frame baked transform output from the generator.
- No attempt to replace the existing focused rendering lab scenes.
- No fully art-directed environment scene in the first slice.

## Scene Concept

### Canonical Scene

The first scene should be a single authored `Directional Shadow Plaza` scene.

It should be structured as a broad outdoor plaza with:

- one large ground plane
- mild elevation changes created by low stepped platforms or blocks
- three rotating tower groups placed across the plaza
- one orbiting hero prop moving around a central anchor
- a ring of passive receiver objects near the plaza perimeter
- one directional light acting as the scene sun
- one attract-mode camera rig that keeps the main composition visible

The scene should read clearly even with simple meshes and default materials. Visual strength should come from silhouette, spacing, height variation, and shadow motion rather than from high-end material complexity.

### Cross-Platform Shape Language

Geometry should be built from simple primitives and repeated forms.

Recommended shape language:

- tall stacked towers with distinct profiles
- wide receiver blocks and low walls
- one larger orbiting centerpiece with a profile different from the towers
- passive edge markers that make shadow travel easy to read from multiple angles

This keeps the canonical scene portable. Later platforms can hide some objects if needed, but the authored baseline should already be compatible with lower-complexity targets.

## Motion Model

### Attract Mode

The first version should always default to attract mode.

Motion should be slow, deliberate, and readable:

- the directional light sweeps through a narrow sun arc instead of rotating freely
- each tower group rotates at a different deterministic period
- the orbiting hero prop circles a central anchor on a slower loop
- the camera follows its own slow orbit around the plaza at a slightly elevated downward-looking angle

All loops must be deterministic and time-based so captures remain comparable across backends and future console targets.

### Motion Priority

Motion exists to make lighting and shadows legible.

Each animated element should satisfy at least one of these purposes:

- produce visible directional shadow change across large receivers
- create silhouette variation at different scene depths
- keep the plaza visually alive during automatic playback

If an animated element adds spectacle but reduces readability, it does not belong in the first scene.

## Generator Architecture

### Extend Existing Writer Infrastructure

The new scene should be generated through the existing committed scene-writer pipeline rather than a new authoring path.

Recommended architecture:

- a top-level rendering scene writer owns the stable scene id and file output
- a plaza layout builder creates the static entity hierarchy
- a motion layout builder authors the runtime behavior inputs for motion
- a shared scene reference builder owns stable primitive mesh and material references
- a catalog integration step exposes the scene to existing rendering or demo-scene listings

This keeps scene generation aligned with the current `RenderingSceneWriter` pattern and avoids a second generator stack.

### Scene Ownership Boundaries

The generator owns authored scene data:

- stable entity ids
- stable hierarchy
- light, camera, and mesh placement
- authored motion parameters
- asset references

Runtime components own scene motion:

- sun sweep behavior
- tower rotation behavior
- orbiting hero behavior
- camera orbit behavior

The generator must not bake animation frame-by-frame. It should author a valid scene that contains the entities and runtime behavior configuration needed to animate after load.

## Runtime Composition

### Required Authored Elements

The first generated scene should contain:

- exactly one directional light
- exactly one attract-mode camera rig
- exactly three rotating tower groups
- exactly one orbiting hero prop
- passive receiver objects around the plaza edges
- floor and elevation geometry sufficient to create readable long shadows

Tower groups should differ in silhouette and height so their shadows are easy to distinguish during motion.

### Future Scalability

The canonical scene should be authored with later platform/profile filtering in mind.

That means:

- stable entity ids should remain predictable
- optional detail props should be separable from core composition elements
- platform simplification should later be able to hide objects without breaking motion anchors or overall scene readability

The first version does not implement visibility filtering, but it should not block that future addition.

## Validation Rules

Fail fast when:

- the project root path is missing or invalid
- the assets root path is missing
- required built-in asset references cannot be authored
- a required loop parameter is invalid
- a required attract-mode element cannot be generated

Examples of invalid authored parameters include:

- zero or negative animation periods when continuous motion is required
- zero orbit radius when an orbiting prop is required
- missing stable scene id or invalid output path

The generator should not substitute hidden defaults for invalid required values.

## Testing

Add focused coverage for the canonical scene generator.

### Scene Output

- the generated scene is written at the expected stable scene id
- the generated scene can be deserialized successfully
- the generated scene uses only the expected primitive mesh and baseline material references

### Scene Structure

- the scene contains exactly one directional light
- the scene contains exactly one attract-mode camera rig
- the scene contains exactly three rotating tower groups
- the scene contains exactly one orbiting hero prop
- the scene contains passive receivers and floor geometry in the expected hierarchy

### Motion Authoring

- the scene includes the authored runtime motion inputs needed for sun sweep
- the scene includes the authored runtime motion inputs needed for tower rotation
- the scene includes the authored runtime motion inputs needed for hero orbit
- the scene includes the authored runtime motion inputs needed for camera orbit

### Rendering Compatibility

- the camera render settings remain compatible with current rendering-scene expectations
- catalog integration exposes the new scene without regressing current rendering or demo-scene listing behavior

## Implementation Notes

- Reuse current rendering scene serialization helpers where possible instead of building a bespoke authoring format.
- Keep the authored baseline scene intentionally simple so later enhancements on Windows remain additive.
- Prefer one focused scene with strong motion readability over a mixed-feature showcase that tries to demonstrate everything at once.
- Keep motion parameters explicit and deterministic to support future backend comparisons and console demo reuse.

## Migration Direction

This scene complements existing static rendering validation scenes rather than replacing them.

Implementation should proceed in layers:

1. add the canonical directional-shadow plaza scene writer path
2. add or wire the required runtime motion behaviors for attract mode
3. add generator and catalog tests for scene structure and motion authoring
4. later add platform/profile visibility filtering without changing the canonical scene identity
