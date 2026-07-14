# Tilt Trial Cook-Time Platform UI Design

## Summary

Keep Tilt Trial gameplay as one level-design-owned scene for every target platform. Its world, gameplay entities, physics, and stage layout are authored once. Presentation is supplied by two reusable UI prefabs that the scene references:

- a console presentation prefab for desktop and conventional consoles
- a handheld presentation prefab for Nintendo DS and Nintendo 3DS

The active target platform is resolved while cooking the scene. The cooked runtime scene contains exactly one presentation prefab and only the assets reachable from that resolved scene. Runtime loading must not select, instantiate, retain, or even know about the other platform's UI.

The demo-disc main menu is intentionally separate from this model. Its handheld layout has different useful content and should be authored as a dedicated handheld menu scene rather than constrained into the shared Tilt Trial gameplay layout.

## Goals

- Preserve one authored Tilt Trial gameplay scene for level design on all platforms.
- Generate reusable UI presentation prefabs without rewriting or regenerating the gameplay scene.
- Keep desktop and console presentation separate from DS and 3DS presentation.
- Resolve platform UI before packaging so constrained handheld builds never ship desktop or console UI, fonts, textures, cameras, or layout data.
- Render Tilt Trial gameplay on the DS/3DS top screen and HUD plus touch controls on the bottom screen.
- Route touch and physical controls through one semantic gameplay-command path.
- Reuse the established per-entity platform override and cook-resolution system rather than add runtime platform branching.

## Non-Goals

- No runtime platform UI switcher.
- No scene generation for Tilt Trial gameplay or level geometry.
- No loading both UI prefabs and hiding one after the scene opens.
- No separate DS and 3DS gameplay scenes in the initial implementation.
- No attempt to force the main menu into the shared gameplay-scene model.

## Authoring Model

### Shared Gameplay Scene

The Tilt Trial gameplay `*.helen` scene is level-design-owned. It contains the complete shared game world:

- stage geometry and visual dressing
- player, goal, hazards, lights, and physics objects
- gameplay controller and session state
- two presentation-prefab reference roots

The scene must not contain handwritten platform-specific HUD subtrees. It only references reusable presentation prefabs. Regenerating UI must update the prefab assets, never rewrite the gameplay scene or its level-design entities.

### Presentation Prefabs

The UI generator produces two assets with stable identities:

- `TiltTrialConsolePresentationPrefab`
- `TiltTrialHandheldPresentationPrefab`

Each prefab is a static entity subtree materialized during cooking. This is not a runtime prefab-instantiation mechanism.

The console prefab owns:

- one gameplay camera for the conventional full-screen target
- the existing full-screen Tilt Trial HUD
- UI assets required only by that presentation

The handheld prefab owns:

- a top-screen camera that renders the shared 3D world
- a bottom-screen camera and viewport for the HUD
- bottom-screen touch controls for pause, restart, and back
- handheld-only layout, font, and texture assets

The DS and 3DS initially share this handheld prefab. A third prefab may be introduced later only when their presentation requirements genuinely diverge.

### Per-Platform Scene Overrides

The base scene uses the console presentation as its editor-default presentation. Per-platform existence overrides select the final presentation:

| Target family | Console prefab | Handheld prefab |
| --- | --- | --- |
| Windows and conventional consoles | enabled | disabled |
| Nintendo DS | disabled | enabled |
| Nintendo 3DS | disabled | enabled |

These are normal editor-owned scene platform overrides. The editor can preview each resolved target through its active-platform view. Both references remain available to the authored scene, but only the target-resolved subtree may reach the cooked result.

## Cook And Package Flow

1. The UI generator produces or refreshes the two presentation prefab assets.
2. The packager resolves the authored Tilt Trial base scene with the requested platform's entity overrides.
3. The packager removes disabled presentation roots before collecting scene asset references.
4. The selected prefab subtree is flattened into the cooked scene payload.
5. Asset collection runs only over the flattened resolved scene.
6. The final package contains the world plus its one selected presentation and reachable dependencies.

The order is mandatory. Collecting assets before disabled roots are removed would ship both presentation families and violates the handheld memory constraint.

Cook failures must be explicit. Packaging fails when a required target presentation reference is missing, both presentation roots resolve enabled, neither resolves enabled, or a selected prefab cannot be resolved. There is no fallback to a different UI or an empty runtime HUD.

## Gameplay Input Contract

Presentation maps physical and touch input onto shared Tilt Trial actions:

- `Pause`
- `Restart`
- `Back`

The gameplay/session controller consumes those actions without inspecting the platform or UI type. Desktop and console bind them to their normal physical input. The handheld prefab supplies equivalent bottom-screen touch buttons while the handheld input mapping continues to bind physical controls to the same actions.

Touch controls are presentation and input wiring only. They do not contain session logic, scene navigation logic, or platform-specific game rules.

## Main Menu Boundary

The main menu is a separate authored handheld implementation. It may use both screens for different content and is selected in the platform build configuration as its own scene asset. It does not share the Tilt Trial gameplay presentation prefabs or rely on gameplay-scene platform overrides.

This distinction is deliberate:

- Tilt Trial gameplay has one shared world with a platform-specific presentation shell.
- the main menu has materially different handheld content and composition.

## Validation

Add focused coverage around the cook boundary:

- the authored Tilt Trial scene references both stable presentation-prefab identities
- Windows/console cooking retains only the console presentation root and its dependencies
- DS cooking retains only the handheld presentation root and its dependencies
- 3DS cooking retains only the handheld presentation root and its dependencies
- cooked DS and 3DS scenes contain the top gameplay camera and bottom HUD viewport
- packaged handheld assets contain no console-presentation font, texture, or layout references
- packaged desktop/console assets contain no handheld-only touch-control or bottom-screen layout references
- invalid resolved presentation states fail packaging with a diagnostic that names the target platform and affected roots
- touch and physical bindings invoke the same Tilt Trial actions

Tests operate on cooked output and dependency manifests, not merely the authored scene. The product requirement is the absence of unused data from the final target package.

## Migration Direction

The current generated Tilt Trial UI construction moves into the two prefab generators. The gameplay-scene factory must stop owning level content or rewriting the `*.helen` scene. The authored scene receives stable presentation-prefab references once, after which level design remains independent from UI generator changes.

Existing level scenes remain gameplay content. This work must not overwrite stage transforms, entities, component values, or other user-authored scene data.
