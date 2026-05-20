# PS2 Demo Disc Startup And Scene Selection Design

## Goal

Deploy the `city` project to PS2 using the same high-level scene flow as Windows:

- boot through `GeneratedBootScene`
- enter `DemoDiscMainMenu`
- expose the playable rendering scenes from the demo-disc menu

The PS2 build must not redirect into Nintendo DS scene variants.

## Problem

The editor already supports generated startup-scene routing, but the current platform-specific behavior is asymmetric:

- `windows` forces `GeneratedBootScene` to the front of the staged scene list
- `ds` forces its generated startup scene and adds DS companion-scene behavior
- `ps2` is not wired into that startup-scene path

Separately, the generated boot-scene preparation service only emits mappings for `windows` and `ds`. DS mappings intentionally redirect default scene ids such as `DemoDiscMainMenu` and rendering-scene ids to `_ds` variants. That is correct for DS and incorrect for PS2.

## Scope

This design covers:

- editor startup-scene selection for PS2 builds
- generated boot-scene preparation for PS2 builds
- PS2 build verification for the `city` playable demo-disc rendering scenes

This design does not cover:

- new scene discovery rules
- PS2-specific menu content
- runtime scene-loading code changes
- DS behavior changes

## Selected Scene Set

The PS2 deployment should package the Windows-style playable rendering menu content:

- `GeneratedBootScene`
- `DemoDiscMainMenu`
- `cube_test`
- `scaled_cube`
- `colored_cube_grid`
- `textured_cube_grid`
- `axis_test`
- `axis_test2`
- `directional_shadow_plaza`
- `spotlight_street_slice`

DS companion scenes and `_ds` scene ids are excluded from the intended PS2 deployment.

## Design

### 1. Startup-scene ownership

PS2 should use the same generated startup-scene flow that Windows uses. The editor build queue should treat `ps2` as a platform that must stage `GeneratedBootScene` first whenever the selected scene set represents the demo-disc flow.

This is not a new boot-scene system. It is a platform enablement of the existing startup-scene mechanism.

### 2. Boot-scene mapping behavior

The generated boot-scene preparation service should treat PS2 like Windows, not like DS:

- If the selected scene set contains `DemoDiscMainMenu` or `GeneratedBootScene`, prepare `GeneratedBootScene`
- Emit an empty mapping table for PS2
- Do not emit DS companion-scene remaps
- Do not rewrite any selected scene ids to `_ds` variants

The resulting PS2 boot scene acts as a neutral startup hop into the normal demo-disc menu flow.

### 3. DS isolation

All DS-specific behavior remains DS-only:

- DS startup-scene ordering stays unchanged
- DS companion-scene expansion stays unchanged
- DS boot-scene remapping stays unchanged

The implementation must make the platform branching explicit so PS2 cannot accidentally inherit DS scene rewriting again.

### 4. Build verification

Verification should prove the editor emits the correct scene set and boot-scene behavior for PS2:

- a focused editor test for startup-scene ordering on PS2
- a focused editor test for generated boot-scene preparation on PS2
- an end-to-end build verification using the `city` project configuration for playable rendering scenes

The end-to-end verification should assert:

- `GeneratedBootScene` is staged
- `DemoDiscMainMenu` is staged
- playable rendering scenes are staged
- `_ds` scene ids are absent from the resulting PS2 scene manifest or staged scene set

## File Impact

Expected implementation touch points:

- `engine/helengine.editor/managers/project/EditorBuildQueueItemFactory.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs`
- existing editor tests covering startup-scene ordering and generated boot-scene preparation
- one build/integration test that verifies the `city` PS2 scene set end to end

## Error Handling

The implementation should preserve current failure behavior:

- if the selected scene set does not include the menu flow, do not invent fallback scene mappings
- if the boot-scene generator is not needed for the selected scene set, return `null` as it does today
- if tests reveal PS2 scene selection still includes `_ds` ids, treat that as a build-configuration failure rather than patching it at runtime

## Testing Strategy

Add or update focused tests for:

- PS2 startup-scene ordering
- PS2 generated boot-scene mapping generation
- DS regression coverage where existing DS behavior is near the changed branches

Run a narrow end-to-end verification against the `city` project to confirm the PS2 build output matches the intended playable rendering-scene menu set.

## Success Criteria

The work is complete when:

- PS2 builds follow `GeneratedBootScene -> DemoDiscMainMenu`
- the playable rendering scenes are present in the PS2 deployment
- no DS companion-scene remapping is applied to PS2
- DS builds still retain their existing remap behavior
