# Directional Shadow Plaza Readability Design

## Goal

Retune the city-owned `Directional Shadow Plaza` authored scene so it reads clearly as a showcase scene in both the editor and the Windows player without relying on cooked artifact patches or renderer changes.

This pass is intentionally iterative. It changes scene generation only and does not add new automated test coverage.

## Scope

This work updates the city rendering scene generator path for the canonical plaza scene:

- `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`

This work does not:

- modify the main editor renderer
- add runtime fallback logic
- add debug-only materials or cooked asset patches to the canonical source scene
- change the scene id, entity ids, or the general canonical scene ownership model

## Constraints

- The scene remains city-owned and generated from the existing city command path.
- The plaza remains a showcase scene, not a pure diagnostic rig.
- The existing canonical entity set remains in place:
  - one camera
  - one directional light
  - one ground plane
  - three rotating towers
  - one orbiting hero prop
  - four passive receiver props
- The implementation should stay iterative and avoid adding test burden for this pass.

## Recommended Approach

Use a showcase-first retune.

Keep the same entity count and motion types, but retune authored transforms, proportions, and motion parameters so the scene is readable without manual cooked-scene edits.

This is preferred over adding marker geometry or rewriting the whole plaza layout because it keeps the original showcase intent, limits churn, and preserves stable authored ids.

## Authored Scene Changes

### 1. Silhouette readability

Retune the three rotating towers and the hero prop so their silhouettes are visibly asymmetric from a distance.

The current tower and hero scales are too close to square prisms, which hides Y-axis rotation. The new authored proportions should make rotation visible without relying on debug colors.

The center tower should remain at world center, but its shape should still read as rotating even while the orbit camera keeps it near the center of the frame.

### 2. Motion readability

Keep all existing motion classes active:

- camera orbit
- tower spin
- hero orbit
- sun sweep

Retune the authored motion values so they read independently:

- tower angular speeds should differ more clearly from one another
- hero orbit motion should no longer visually cancel against the camera orbit
- sun sweep should stay active, but it should be narrowed or slowed so it supports the showcase instead of overwhelming it

### 3. Camera readability

Keep the authored orbit camera, but retune framing if needed so the scene remains legible and the tallest tower does not crowd the shot.

The camera render settings should also be updated so the far plane is `200`.

### 4. Light defaults

Keep directional lighting and shadows enabled in the canonical source scene.

Update the authored directional light defaults so:

- `Intensity = 1`
- shadows stay enabled
- the existing authored shadow distance remains unchanged unless future iteration proves otherwise

## Implementation Plan Boundary

This work should be implemented entirely in the plaza scene factory by retuning:

- entity transforms
- entity scales
- tower spin parameters
- hero orbit parameters
- sun sweep parameters
- camera framing parameters
- camera render settings
- directional light defaults

No renderer changes are part of this pass.

## Generation Flow

After the source retune:

1. regenerate the city rendering scenes through the normal city generation path
2. produce an updated `directional_shadow_plaza.helen`
3. verify manually in editor and Windows player

## Verification

This pass uses manual verification only.

Expected checks:

- the source scene opens correctly in the editor
- the plaza reads clearly without cooked artifact hacks
- the player build still renders the plaza correctly
- camera motion, tower spins, hero orbit, and sun sweep all remain active and legible

## Failure Behavior

- Missing required authored references should continue to fail loudly during generation.
- This pass should not introduce silent fallback values or debug-only authored behavior into the canonical scene.
