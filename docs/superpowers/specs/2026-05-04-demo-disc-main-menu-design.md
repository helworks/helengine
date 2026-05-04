# Demo Disc Main Menu Design

## Summary

Build a reusable runtime menu framework for HelEngine and use it in the `city` project to create a demo-disc style main menu scene. The first version should ship one scene with switchable panels for `Main`, `Select Scene`, and `Options`.

The visual direction is lilac, colorful, friendly, and slightly gritty. The first pass should support imported fonts and placeholder icons, but the core deliverable is the reusable menu behavior rather than final art production.

## Problem

The demo disc needs a navigable front end that can grow to many scenes without turning scene selection into one-off project glue.

The current runtime already has:

- `ButtonComponent`
- `TextComponent`
- `InputSystem`
- runtime scene loading

What it does not have is a reusable menu layer that:

- groups buttons into navigable panels
- supports keyboard, mouse, and gamepad together
- switches between menu screens inside one scene
- launches curated scenes from user-side project code

If the first pass is built as city-only glue, later projects will need to re-solve the same focus, navigation, and panel-switching problems.

## Goals

- Add a reusable runtime menu framework in the engine.
- Reuse existing runtime UI primitives instead of building a separate UI stack.
- Support mouse, keyboard, and gamepad navigation in the same menu flow.
- Build one menu scene in the `city` project with switchable `Main`, `Select Scene`, and `Options` panels.
- Source the selectable demo scenes from a curated ordered C# config in the `city` project.
- Keep `Options` as a polished shell in the first pass.
- Make first-pass transitions programmatic so they can later be replaced or augmented by the animation system.

## Non-Goals

- No separate scene per menu screen.
- No editor-only menu authoring workflow in this slice.
- No data-driven scene-list asset format in this slice.
- No fully functional options/settings persistence in the first pass.
- No silent fallback behavior when menu config or target scenes are invalid.

## Proposed Architecture

### Runtime Menu Layer

Add a small runtime menu layer above the existing 2D UI components.

Recommended pieces:

- `MenuControllerComponent`
- `MenuPanelComponent`
- `MenuItemComponent` or a small adapter around `ButtonComponent`
- menu action types or action components for `OpenPanel`, `LoadScene`, and `Back`

This layer should own menu behavior only. Rendering should remain the responsibility of existing components such as `ButtonComponent`, `RoundedRectComponent`, and `TextComponent`.

### One Scene, Multiple Panels

The demo disc menu should be a single scene containing multiple panel roots:

- `Main`
- `Select Scene`
- `Options`

Each panel is one root entity or one rooted subtree. The controller activates one panel at a time and hides the others. First-pass hiding can be implemented through enable state and root transforms. This keeps the transition mechanism simple while leaving room for later animation-driven motion.

### Project-Specific Content

The reusable engine code should not know about the `city` project's scene list.

The `city` project should provide one curated ordered C# config that defines the selectable scenes. Each entry should contain:

- display title
- scene asset id
- optional subtitle or short description
- optional enabled flag

That allows the demo disc to hide unfinished scenes later without changing the reusable menu framework.

## Components

### `MenuControllerComponent`

`MenuControllerComponent` should:

- register available panels
- track the active panel id
- track panel history for `Back`
- resolve the currently selected or focused menu item
- normalize interaction between mouse hover and keyboard/gamepad navigation
- dispatch high-level menu actions

The controller is the only part that should understand panel switching and high-level menu navigation state.

### `MenuPanelComponent`

`MenuPanelComponent` should represent one logical screen inside the menu scene.

It should expose:

- panel id
- optional default target
- visible or hidden state

The panel owns its child visuals and buttons, but it should not own global navigation or scene-loading policy.

### Menu Items

Menu items should remain real buttons visually. The reusable layer should extend them with menu semantics instead of replacing them.

Expected menu-item behavior:

- participate in directional traversal
- expose a confirm action
- report hover so mouse input can update the active selection
- support a selected or focused visual state shared across keyboard, gamepad, and mouse usage

For the first pass, the most practical shape is a small component or adapter that binds existing `ButtonComponent` instances into menu navigation.

### Menu Actions

The framework should separate visual selection from behavior dispatch.

First-pass action types:

- open another panel
- go back to the previous panel
- load one scene

This keeps the framework reusable for non-scene menus later.

## Input Model

### Shared Navigation Commands

Keyboard and gamepad should be normalized into shared menu commands:

- `Up`
- `Down`
- `Left`
- `Right`
- `Confirm`
- `Back`

Mouse should continue using existing button hover and click behavior.

### Active Input Family

The controller should track the last active input family:

- mouse
- keyboard
- gamepad

When the player uses the mouse, hover should update selection.
When the player uses keyboard or gamepad, traversal should move the selected target directly.

This avoids separate visual states fighting each other. There should be one authoritative selected target at a time.

### Focus Rules

Each panel should declare or infer a default target. When a panel opens:

- the controller resolves the panel
- the controller resolves the default target
- the target becomes selected immediately

If the panel or target configuration is invalid, setup should fail with a clear exception.

## City Demo Disc Composition

### Main Panel

The `Main` panel should include:

- `Select Scene`
- `Options`

An exit action is optional and should only be included if the runtime has a clean cross-platform way to support it. It is not required for the first pass.

### Select Scene Panel

The `Select Scene` panel should:

- build its rows from the curated city config
- show the entries in config order
- allow confirm to load the selected scene
- allow `Back` to return to `Main`

The reusable framework should not enumerate scenes from the project automatically in this slice.

### Options Panel

The `Options` panel should be a polished shell only.

It should:

- use the same menu framework and styling
- present plausible option rows or headings
- include a back action

It should not yet persist or apply real settings.

## Scene Loading

Scene launching should stay strict and explicit.

When a scene menu item activates:

1. The action resolves the configured scene id.
2. The runtime attempts to load that scene through the existing runtime scene-loading path.
3. Invalid scene ids should fail fast with a clear error.

The framework should not silently skip bad entries or fall back to another scene.

## Visual Direction

The first implementation should support this design language:

- lilac-forward palette
- colorful accents
- friendly shapes and labels
- slightly gritty texture or presentation details

The framework should not hardcode city-specific art assets, but the city menu scene can configure:

- imported fonts
- placeholder icons
- panel-specific text
- background decoration

Programmatic panel motion is acceptable in the first pass. The design should leave panel roots easy to animate later.

## Failure Handling

The menu framework should fail fast when required state is invalid.

Examples:

- missing panel id
- duplicate panel id
- missing default target
- unresolved scene id
- invalid curated config entry
- required font or button binding missing during setup

Do not add best-effort fallback menus, empty placeholder targets, or automatic replacement config values.

## Testing

Add focused automated tests around the reusable engine-side behavior:

1. Panel registration and switching
- panel ids register once
- active panel changes correctly
- hidden panels do not remain active

2. Default-target resolution
- panel open selects the declared default target
- invalid defaults fail clearly

3. Navigation behavior
- keyboard navigation moves between registered menu items
- gamepad navigation uses the same traversal logic
- mouse hover updates the same selected target

4. Back-stack behavior
- `Back` returns to the previous panel
- empty history behaves explicitly and predictably

5. Scene-launch validation
- valid scene actions dispatch correctly
- invalid scene ids fail fast

6. City config validation
- curated entries preserve declared order
- disabled entries can be excluded cleanly

## Files In Scope

Engine-side scope:

- `engine/helengine.core/components/2d/interactable/*`
- new runtime menu components under `engine/helengine.core`
- runtime input integration points in `engine/helengine.input`
- runtime scene loading integration in `engine/helengine.core/scene/runtime/*`

Project-side scope:

- `C:\dev\helprojs\city\assets\**\*.cs`
- curated menu scene-list config in the `city` project
- the city demo-disc menu scene composition

## Implementation Notes

- Reuse existing `ButtonComponent` and `TextComponent` primitives.
- Keep logic in separate controller and action classes so UI entities stay focused on presentation and wiring.
- Prefer strict setup validation over auto-healing behavior.
- Keep the first-pass public API small so later projects can assemble their own menus without inheriting city-specific assumptions.
