# Spotlight Street Slice Plan

## Goal

Add a second canonical city-owned rendering scene that showcases spotlight rendering using the existing Riemers lamppost, racer, and texture assets.

## Constraints

- Keep the existing directional-light city-block scene intact.
- Add the new scene through the city rendering scene generator, not through engine-owned demo content.
- Use the existing Riemers models and textures already present in the city project.
- Keep the car parked.
- Keep the lamppost static.
- Keep the spotlight static.
- Keep camera motion only.
- Preserve the existing project/menu generation path.

## Implementation Steps

1. Inspect the existing Riemers assets and their processed asset paths.
   - Confirm the file-backed model references for:
   - `assets/models/Riemers/lamppost.x`
   - `assets/models/Riemers/racer.x`
   - Confirm whether any existing imported material path is actually usable as-is.
   - If not, keep the spotlight scene on the current standard material path for this pass and treat real textured-material work as out of scope.

2. Extend the city rendering scene generator to output a second scene.
   - Update `C:/dev/helprojs/city/assets/codebase/rendering.tools/RenderingSceneGenerator.cs`.
   - Add a second stable scene id for the spotlight scene.
   - Generate both:
   - `scenes/rendering/directional_shadow_plaza.helen`
   - the new spotlight scene asset

3. Add a dedicated spotlight-scene factory.
   - Create `C:/dev/helprojs/city/assets/codebase/rendering.tools/SpotlightStreetSliceSceneFactory.cs`.
   - Keep the file focused on one canonical scene only.
   - Author:
   - one parked racer
   - one static lamppost
   - one street-ground slice using the current standard material path
   - one spotlight aligned with the lamppost
   - one camera rig with camera motion only
   - dark, sparse surroundings so the spotlight remains the focal point

4. Author the spotlight-scene persistence payloads.
   - Reuse the existing scene-asset authoring pattern already used by `DirectionalShadowPlazaSceneFactory`.
   - Add any required mesh, camera, and spot-light payload writing inside the new spotlight factory.
   - Use file-backed scene asset references for the Riemers models instead of generated primitives where authored assets already exist.
   - Keep component ids and authored payloads explicit and stable.

5. Regenerate the city rendering scenes.
   - Run:
   - `menu.generate-rendering-scenes`
   - Ensure both canonical rendering scenes are written successfully into the city project assets tree.

6. Rebuild the Windows export.
   - Run the normal Windows build path and confirm the second scene packages successfully with its file-backed Riemers content.

7. Manual verification only.
   - Open the new spotlight scene in the editor.
   - Check that:
   - the lamppost and racer load correctly
   - the spotlight cone is readable
   - the car remains parked
   - the camera is the only deliberate motion
   - the street slice reads correctly in the Windows player too

## Risks To Watch

- The imported Riemers models may not expose a ready-to-use textured material path in the current standard renderer flow, so this pass should not try to invent one mid-scene.
- The street surface may need a small authored helper mesh or plane if no reusable street model already exists.
- The scene can become noisy if too many extra props are added; keep it narrow and spotlight-led.
- If the spotlight alignment is off relative to the lamppost, the scene will feel fake immediately even if the rendering path is technically correct.
