# Explicit Project Module Build Graph

## Problem

Platform builds currently start through the editor command-line host. The host uses the same script build-and-reload path as an interactive editor session, which discovers test folders and editor tooling before cooking or native packaging begins. The platform wrapper also invokes Demo Disc menu and scene generation commands unconditionally.

This makes a clean isolated platform build depend on unrelated editor modules, test projects, and project-specific authoring commands. It also hides failures when an older generated-code cache happens to exist.

## Goals

- Every authored code surface declares whether it is runtime or editor-only.
- Platform builds compile only runtime modules needed for the selected target and scenes.
- Opening a project in the editor and explicitly regenerating content compile the complete editor graph, including test projects.
- Test projects bind to declared production modules by stable module id.
- Project-specific generation runs only when named by the selected build profile.
- Each platform build keeps its own isolated project, generated-code workspace, editor publish, and output directory.

## Module Layout

Demo Disc will declare these production modules through `code.module.json` files:

- `gameplay`: composition module rooted at `assets/codebase`; it owns loose shared runtime code and references the runtime modules below.
- `game`: runtime gameplay components and systems.
- `menu`: runtime menu components and menu state.
- `rendering`: runtime rendering-facing components and input-driven render controls.
- `game.tools`: editor-only game and scene authoring tools.
- `menu.tools`: editor-only menu authoring and menu regeneration tools.
- `physics.tools`: editor-only physics authoring tools.
- `rendering.tools`: editor-only rendering authoring tools.
- `scene.tools`: editor-only scene authoring tools.

Each runtime module declares only the runtime modules it references. Each editor module may reference runtime modules but no runtime module may reference an editor module. The composition module gives `gameplay.tests` a declared production target.

Test folders remain sibling folders named `<module-id>.tests`. Test discovery resolves only declared module ids; an undeclared sibling is a configuration error shown in the editor, not a platform-build concern.

## Script Compilation Modes

The editor exposes a compilation-mode argument to the generated-code solution and hot-reload services.

`EditorFull` is used by interactive editor startup, explicit script reload, and manual regeneration commands. It generates runtime and editor production projects, discovers test projects, builds all applicable projects, and loads editor command/menu contributions.

`RuntimeOnly` is used by platform builds. It generates and builds runtime production projects only. It never enumerates `.tests` folders, emits test projects, loads editor modules, or requires editor command registrations.

Both modes write their project files and compiler outputs into the current build invocation workspace. Neither mode reads or relies on `user_settings/generated_code` from the authored project.

## Build Profile Prebuild Steps

The generic platform wrapper stops hard-coding Demo Disc commands such as `menu.generate-game-scenes`, `menu.regenerate-demo-disc-main-menu`, and Tilt Trial presentation attachment.

A build profile can instead declare ordered editor prebuild command identifiers. The platform build executes those commands only when the selected profile declares them. Each command runs under `EditorFull` because it is authoring work. The final cook and package stage always runs under `RuntimeOnly`.

A minimal colored-cubes diagnostic profile declares no editor prebuild commands. A full Demo Disc profile can declare its generation commands explicitly.

## Failure Behavior

- A missing or invalid module manifest fails `EditorFull` with the module id, manifest path, and invalid dependency.
- `RuntimeOnly` ignores editor modules and tests completely.
- A selected profile that names an unavailable prebuild command fails before cooking with that command id.
- A platform build never silently falls back to shared generated-code output or another platform's invocation.

## Validation

- Unit-test manifest discovery for declared runtime, editor, and composition modules.
- Unit-test that `RuntimeOnly` does not enumerate `.tests` folders or create test project descriptions.
- Unit-test that `EditorFull` creates one test project for every declared sibling test surface and binds it to the declared production module.
- Unit-test that runtime modules cannot reference editor modules.
- Integration-test a clean isolated PS2 build using a runtime-only colored-cubes profile while test folders are present.
- Integration-test a full project profile whose declared prebuild commands run before its runtime-only cook.
- Run concurrent isolated builds for at least PS2 and PS Vita to verify no generated-code or editor-publish paths overlap.
