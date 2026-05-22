# Windows Demo Disc Boot Alignment Design

## Goal

Make Windows demo-disc exports boot through the same startup flow the user wants to validate: `GeneratedBootScene` first, then `DemoDiscMainMenu`, then the authored demo scenes selected from the menu.

## Current State

- Windows builds currently force `GeneratedBootScene` as startup in `EditorBuildQueueItemFactory`.
- PS2 builds currently force `DemoDiscMainMenu` directly as startup in `EditorBuildQueueItemFactory`.
- `EditorGeneratedBootScenePreparationService` already knows how to generate `GeneratedBootScene` that targets `DemoDiscMainMenu`.
- The city project now owns the demo-disc menu scene and menu runtime components.

The practical problem is not scene generation. The practical problem is making sure the Windows export path is the validation target for the same boot chain the user cares about.

## Design

### Startup Contract

Windows builds will explicitly preserve the demo-disc boot chain:

1. `GeneratedBootScene`
2. `DemoDiscMainMenu`
3. Demo scenes selected from the menu

This means the Windows build must continue to stage `GeneratedBootScene` first whenever the selected scene set contains the demo-disc main menu.

### Scope

The change stays inside editor build-selection and startup-scene routing:

- `EditorBuildQueueItemFactory` remains responsible for forcing the Windows startup scene.
- `EditorGeneratedBootScenePreparationService` remains responsible for generating the boot scene that forwards to `DemoDiscMainMenu`.
- No city menu content changes are required.
- No PS2 startup behavior changes are included in this change.

### Expected Behavior

For a Windows export that includes the demo-disc menu:

- `GeneratedBootScene` is first in the selected/cooked scene list.
- The packaged startup scene resolves to `GeneratedBootScene`.
- `GeneratedBootScene` loads `DemoDiscMainMenu`.
- The user can then navigate from the main menu into the demo scenes.

## Alternatives Considered

### Keep Windows Direct-To-Menu

This would reduce one layer of startup behavior, but it would not validate the boot chain the user asked to test.

### Mirror PS2's Current Direct-To-Menu Override

This would make Windows and PS2 match each other as currently implemented, but it would move Windows away from the desired `GeneratedBoot -> MainMenu` path.

### Add New Project Configuration

This would make startup routing configurable per platform, but it adds unnecessary configuration and complexity for a single clear requirement.

## Recommendation

Keep Windows on the explicit `GeneratedBootScene` startup path and validate the city demo-disc flow through that boot scene. This is the smallest change that matches the desired runtime behavior and exercises the intended boot pipeline.

## Verification

The smallest useful verification is:

- build a Windows export for `C:\dev\helprojs\city`
- confirm the export succeeds
- launch the exported Windows executable
- confirm runtime reaches the main menu through boot rather than requiring a direct main-menu startup
