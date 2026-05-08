# Directional Shadow Plaza Abstract City Block Plan

## Goal

Re-author the city-owned `Directional Shadow Plaza` as an abstract city block while preserving the existing canonical scene identity, keeping the main motion in the orbit camera, and using a moving sphere as the focal object.

## Constraints

- Do not touch the main editor renderer for this pass.
- Keep the scene generator owned by `city`.
- Keep buildings as plain upright cubes; no diagonal readability hacks.
- Buildings remain static.
- Keep camera orbit.
- Keep subtle sun sweep.
- Use manual verification only for this pass.

## Dependency

The current engine-generated asset set does not expose a sphere primitive. Before the city scene can switch the hero object from cube to sphere, the generated model pipeline must gain a built-in sphere reference.

## Implementation Steps

1. Add a generated sphere primitive to the engine asset pipeline.
   - Update `engine/helengine.core/utils/ModelUtils.cs`.
   - Add sphere mesh generation alongside the existing cube and plane mesh generation.
   - Update `engine/helengine.editor/managers/asset/EngineGeneratedModelCache.cs`.
   - Register a stable generated model asset id for the sphere and route it through the cache writer.
   - Update `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`.
   - Expose the sphere in the generated asset tree so city tooling can reference it.

2. Thread the sphere reference into the city rendering scene generator.
   - Update `C:/dev/helprojs/city/assets/codebase/rendering.tools/RenderingSceneGenerator.cs`.
   - Pass the generated sphere model reference into the directional shadow plaza factory together with the plane, cube, and standard material references.

3. Re-author the plaza layout as an abstract city block.
   - Update `C:/dev/helprojs/city/assets/codebase/rendering.tools/DirectionalShadowPlazaSceneFactory.cs`.
   - Remove the tower spin components from the building entities.
   - Keep the building meshes as upright cubes with plain axis-aligned scales.
   - Recompose the scene into a small skyline-like block with supporting low-rise masses and street-like negative space.
   - Replace the moving hero cube with the sphere model reference.
   - Keep the sphere as the only moving focal object.
   - Keep the camera orbit.
   - Keep a subtle sun sweep.
   - Preserve authored directional light intensity at `1`.
   - Preserve authored camera far plane at `200`.

4. Regenerate the city rendering scene from the normal command path.
   - Run:
   - `rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --editor-command menu.generate-rendering-scenes`

5. Rebuild the Windows export.
   - Run:
   - `rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows`

6. Do manual verification only.
   - Open the scene in the editor and verify the city-block composition reads clearly.
   - Run the rebuilt Windows player and verify the same scene behavior there.
   - Check that only the camera and sphere provide obvious motion while the buildings stay fixed.

## Risks To Watch

- Sphere generation may require choosing a tessellation level that is visually acceptable without bloating the generated asset.
- The city worktree is already dirty; do not revert unrelated project changes while editing the generator files.
- Scene readability can regress if the sphere motion visually cancels with the camera orbit again.
