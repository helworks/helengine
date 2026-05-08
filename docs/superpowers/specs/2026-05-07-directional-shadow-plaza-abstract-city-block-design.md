# Directional Shadow Plaza Abstract City Block Design

## Goal

Re-author the city-owned `Directional Shadow Plaza` scene so it reads as an abstract city block instead of a generic test yard, while keeping the canonical scene identity and avoiding the earlier readability hacks that made the cubes feel visually wrong.

This pass should preserve the stable city scene-generation path and keep the scene suitable for both editor and Windows player rendering demos.

## Scope

This work updates the city rendering scene generator path for the canonical plaza scene:

- `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`

This work also assumes the existing camera far-plane persistence support remains in place so the authored `FarPlaneDistance = 200` value is preserved through scene loading and player packaging.

This work does not:

- modify the main editor renderer
- add new runtime systems
- add debug-only geometry or cooked artifact patches
- change the canonical scene id or stable entity ids unless strictly required by a missing sphere asset path

## Constraints

- Buildings must remain plain upright cubes.
- No diagonal-looking readability hacks should be introduced.
- Building entities should not rotate.
- Camera orbit remains active.
- Sun sweep remains active but subtle.
- The moving focal object becomes a sphere.
- The overall scene should read like an abstract city block rather than a literal detailed street set.

## Recommended Approach

Use a `skyline block` composition.

Keep a small number of strong building masses, leave broad negative-space lanes that read like streets, and use one moving sphere as the focal contrast object. This keeps the scene readable and city-like without overcomplicating the authored layout.

This is preferred over a literal intersection or courtyard layout because it preserves simplicity while still improving scene identity.

## Scene Layout

### 1. Abstract city block composition

The scene should be composed from:

- one broad ground plane
- three main upright cube towers that read as stylized buildings
- a few shorter supporting cube masses to make the block feel layered
- deliberate open lanes between building groups so the ground reads like streets
- one sphere that acts as the only clearly non-building form

The building masses should stay axis-aligned and should not rely on stretched diagonal silhouettes or marker geometry to read well.

### 2. Motion

The motion model should be simplified:

- buildings stay static
- camera orbit remains the main framing motion
- sun sweep remains subtle
- the sphere moves slowly through the block on an orbit path

This keeps the scene dynamic without making the built environment itself feel unstable.

### 3. Lighting and camera defaults

Keep the current approved authored defaults:

- directional light intensity `= 1`
- shadows enabled
- subtle sun sweep
- camera far plane `= 200`

These settings should remain part of the canonical source scene and not be reintroduced through temporary patches.

## Asset Requirements

The hero prop should use a sphere primitive rather than a cube.

If the engine-generated asset set already exposes a stable built-in sphere model reference, the city scene generator should use that reference directly.

If a generated sphere reference is not currently available, that becomes a follow-up dependency decision before implementation can complete cleanly.

## Implementation Boundary

This pass should be implemented by reauthoring the city scene factory only:

- remove tower spin components from the building entities
- keep camera orbit
- keep subtle sun sweep
- replace the moving hero cube with a sphere
- recompose the placement into an abstract city-block layout
- keep plain upright cube buildings

No renderer changes should be part of this scene redesign.

## Generation Flow

After the source redesign:

1. regenerate the city rendering scenes through `menu.generate-rendering-scenes`
2. update the generated `directional_shadow_plaza.helen`
3. rebuild the Windows export
4. verify manually in editor and player

## Verification

This pass is iterative and uses manual verification only.

Expected checks:

- the plaza reads like an abstract city block
- buildings stay visually upright and static
- camera orbit provides the primary scene motion
- sun sweep remains subtle
- the sphere reads clearly as the moving focal object
- the scene remains stable in both editor and Windows player

## Failure Behavior

- Missing required authored references should still fail loudly during generation.
- If a built-in sphere primitive is not actually available, implementation should stop and resolve that dependency explicitly rather than silently substituting another shape.
