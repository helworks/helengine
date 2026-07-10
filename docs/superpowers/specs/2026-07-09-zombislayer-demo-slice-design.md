# Zombislayer Demo Slice Design

## Goal

Add Zombislayer as a second launchable game on the city demo disc and deliver one Windows-first playable slice that goes straight into gameplay with:

- one static legacy level environment
- one mouse-and-keyboard FPS controller
- one camera-attached M4 viewmodel
- one FSM-driven pause menu with `Resume` and `Return to Demo Disc`

This slice is intentionally narrow. It proves launch flow, scene generation, input feel, and pause/return behavior without taking on combat, enemies, or long-term environment authoring.

## Scope

### In Scope

- Add `Zombislayer` to the demo-disc `Games` panel.
- Generate one authored Zombislayer gameplay scene through the existing city gameplay scene generation workflow.
- Stage one legacy environment model from `C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level`.
- Stage one legacy M4 weapon model from `C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\weapons`.
- Add one runtime FPS controller for Windows using `WASD`, mouse look, and `Esc` pause.
- Add one session component backed by `FiniteStateMachine<T>` with `Playing` and `Paused` states.
- Add one pause overlay with `Resume` and `Return to Demo Disc`.
- Validate the full loop on Windows: demo disc menu -> Zombislayer -> gameplay -> pause -> resume -> return.

### Out of Scope

- Enemies
- Shooting
- Damage, health, fail state, or wave logic
- Weapon animation, recoil, reload, or muzzle effects
- Long-term modular environment pipeline
- Non-Windows gameplay-target validation in this slice

## Recommended Approach

Use the existing generated-scene pattern already established in `city.game.tools`, and add a narrow Zombislayer branch beside Tilt Trial rather than inventing a second content-authoring path.

This is the right fit for the current codebase because:

- city already generates authored gameplay scenes from C# source
- demo-disc games are already catalog-driven
- FSM support already exists and is already used in gameplay code
- subsequent Zombislayer work can grow from this slice without rewriting launch or scene-generation flow

Two alternatives were considered and rejected for this slice:

- Direct hand-authored scene work would be faster once, but it would diverge from the current city generation pattern and be harder to maintain.
- A fuller content-pipeline design would give cleaner long-term asset boundaries, but it is too much for a first walk-only milestone.

## Launch And Scene Flow

Zombislayer should appear as a second item inside the existing demo-disc `Games` panel, alongside Tilt Trial.

Selecting `Zombislayer` should load one gameplay scene directly. There is no Zombislayer title scene, mode select, or per-game front-end in this milestone.

The gameplay scene should contain:

- one static environment entity built from the legacy level model
- one player root entity
- one FPS camera rig
- one camera-attached M4 weapon viewmodel
- one pause UI root

Returning from pause should reuse the existing demo-disc return behavior instead of introducing a second menu-return mechanism.

## Runtime Architecture

### Scene Generation

Add one Zombislayer generation path under `C:\dev\helprojs\city\assets\codebase\game.tools`.

That path should emit one generated authored scene definition for the first gameplay slice. It should be parallel to Tilt Trial generation conceptually, but much smaller in scope.

The scene generator owns static scene structure only:

- environment entity placement
- player root creation
- camera/viewmodel hierarchy
- pause overlay hierarchy
- component wiring

It should not contain gameplay state logic beyond authored references and component attachment.

### Menu Integration

Add one new scene/catalog entry under the existing city menu/game catalog layer so the demo-disc `Games` panel exposes `Zombislayer`.

The entry should point directly at the generated gameplay scene id.

### Session State

Add:

- `ZombislayerSessionState`
- `ZombislayerSessionComponent`

The session component owns the high-level runtime state machine:

- `Playing`
- `Paused`

Responsibilities:

- initialize the FSM once
- react to pause/unpause requests
- show or hide the pause overlay
- suppress gameplay input while paused
- route `Return to Demo Disc`

The session component should own state. The pause UI should reflect state, not decide it independently.

### FPS Control

Add one `ZombislayerFpsControllerComponent`.

Responsibilities:

- read Windows movement input
- read mouse-look input
- update player yaw
- update camera pitch
- move the player on the horizontal plane
- stop applying movement/look while the session is paused

For this slice, the FPS controller should own the runtime camera pose directly instead of being split across several smaller motion components. This keeps the first implementation understandable and easy to validate.

### Pause UI

Add one lightweight pause overlay controller or session-owned overlay wiring with exactly two options:

- `Resume`
- `Return to Demo Disc`

`Resume` returns to `Playing`.

`Return to Demo Disc` loads the resolved runtime main-menu scene using the same general return pattern already used elsewhere in the project.

## Input And Camera Behavior

### Controls

Windows controls for the first slice:

- `W` `A` `S` `D` moves the player
- mouse controls look
- `Esc` toggles pause

### Camera Rules

The first slice should feel like a conventional desktop FPS:

- yaw rotates the player root
- pitch rotates the camera/viewmodel pivot
- pitch is clamped to prevent inversion
- movement is camera-relative on the horizontal plane

### Weapon Viewmodel

The M4 is visual only in this milestone.

It should:

- be attached to the camera rig
- move with the camera every frame
- stay visible in first-person framing

It should not:

- fire
- animate
- recoil
- reload

### Cursor Behavior

When the runtime/input layer supports it cleanly on Windows:

- gameplay should capture and hide the cursor while playing
- pause should release and show the cursor

If existing Windows cursor-lock support is incomplete, functional mouse look is still the requirement. Cursor-lock polish is desirable but not allowed to block the slice.

## Asset Strategy

The legacy level is explicitly treated as an old test asset, not the beginning of a final environment pipeline.

For this milestone:

- import or stage the level as one static environment
- do not spend time decomposing it into reusable authored environment modules
- treat it as a temporary but playable test space

The same rule applies to the weapon model: get it visible and correctly attached, not production-ready.

This keeps asset work aligned with the real goal of the slice: proving gameplay entry, FPS movement, and pause flow.

## Testing Strategy

### Automated Tests

Focus tests on durable logic seams rather than renderer-dependent output.

Required coverage:

- demo-disc game catalog test proving `Zombislayer` appears in the `Games` panel
- game-scene generation source tests proving the Zombislayer generation path is wired into the generator
- session FSM tests proving `Playing` and `Paused` are registered and transition correctly
- pause behavior tests proving `Resume` returns to gameplay state
- return-flow tests proving `Return to Demo Disc` targets the resolved menu scene
- FPS controller math tests if the movement/look helpers expose a clean deterministic seam

### Manual Validation

Manual Windows validation is required for this slice.

Validation checklist:

1. Regenerate city gameplay scenes/assets.
2. Build the Windows target.
3. Launch the demo disc.
4. Enter `Games`.
5. Select `Zombislayer`.
6. Verify gameplay scene loads directly.
7. Verify `WASD` movement works.
8. Verify mouse look works.
9. Verify the M4 viewmodel is attached and visible.
10. Press `Esc` and verify pause overlay appears.
11. Verify `Resume` returns to gameplay.
12. Verify `Return to Demo Disc` returns to the menu.

## Deliverables

The slice is complete when the following are true:

- `Zombislayer` is present in the demo-disc `Games` panel
- selecting it loads directly into one gameplay scene
- the player can move and look on Windows with standard FPS controls
- the M4 viewmodel is visible from the camera
- pausing works with FSM-backed `Playing` and `Paused` states
- `Resume` and `Return to Demo Disc` both work
- automated tests cover menu integration and core runtime seams

## Risks And Constraints

### Legacy Asset Risk

The old models may import with awkward scale, orientation, or material behavior. The milestone should tolerate small presentational imperfections as long as the environment is playable and the weapon is visible.

### Cursor-Lock Risk

Windows mouse capture may need small platform-specific adjustments. Functional look input matters more than perfect cursor behavior for the first slice.

### Scope Risk

The easiest way to derail this slice is to let it absorb combat, zombie behavior, or environment cleanup. Those should stay explicitly deferred.

## Implementation Shape

At a high level, the implementation should break down into:

- menu/catalog wiring
- asset staging/preparation for level and weapon models
- Zombislayer scene generation
- FPS/session/pause runtime components
- Windows validation and targeted tests

That sequence preserves fast feedback: launchability first, then movement feel, then pause flow polish.
