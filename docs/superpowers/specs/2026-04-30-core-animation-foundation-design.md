# Core Animation Foundation Design

## Overview

This design introduces the first reusable animation foundation in `helengine.core`.

The goal is to create a compact, high-performance, C++-friendly runtime animation path that can be reused for editor feedback, runtime transform animation, and later controller-driven playback. The first slice is intentionally narrow:

- keyframe-based `AnimationClip` asset data
- a low-level `AnimationPlayerComponent`
- transform-track playback only

This slice does not include animation state machines, controller graphs, blend trees, or skeletal animation support. Those systems can be layered later on top of the playback surface introduced here.

## Goals

- add a reusable animation asset format that can hold multiple tracks in one clip
- add a low-level player component in `helengine.core`
- keep runtime memory usage predictable and compact
- avoid reflection, string-based property lookup, and dynamic property binding
- keep the data layout straightforward for later C++ conversion
- support both absolute and additive transform animation

## Non-Goals

- no animation controller or FSM layer
- no clip blending graph
- no skeletal animation runtime yet
- no editor authoring UI in this slice
- no generalized callback-driven tween framework

## Recommended Approach

Use one keyframe-based `AnimationClip` asset that can contain multiple typed transform tracks, and one low-level `AnimationPlayerComponent` that plays a single clip at a time.

This keeps authoring compact while keeping runtime memory usage controlled:

- one asset can hold position, offset-position, scale, and rotation tracks together
- the runtime player only keeps a small fixed set of active channel states
- no dynamic track binding or runtime property discovery is required

## Architecture

### AnimationClip Asset

`AnimationClip` should be a core asset type that stores keyframe-based animation data.

It should contain:

- clip name or identifier
- clip duration
- typed transform track collections

Initial supported track types:

- absolute position
- offset position
- absolute scale
- absolute rotation

Each track should use a plain keyframe list with:

- time
- value
- interpolation mode

Initial interpolation modes:

- step
- linear

The asset boundary and the runtime boundary are intentionally different:

- one `AnimationClip` can contain many tracks
- one runtime player still uses a compact fixed internal channel model

### AnimationPlayerComponent

`AnimationPlayerComponent` should live in `helengine.core` and be a low-level playback engine only.

It should expose an explicit API similar to:

- `Play(clip, loop)`
- `Stop()`
- `Pause()`
- `Resume()`
- `Seek(time)`

The component should:

- store the current clip
- store current playback time
- store playing/paused/looping state
- evaluate only the tracks present in the active clip
- update the entity transform deterministically each frame

The player should be intentionally dumb:

- no automatic transitions
- no hidden controller logic
- no clip stacks

That keeps the runtime surface stable for future higher-level animation systems.

## Runtime Model

The runtime should use one active clip at a time and one fixed internal state per transform channel.

That means:

- one absolute position channel
- one offset position channel
- one absolute scale channel
- one absolute rotation channel

Starting a new clip replaces the active playback state rather than layering many clips together.

This is the memory-friendly baseline. It avoids building a general blending stack before the requirements are clear.

### Absolute And Offset Behavior

Both absolute and offset animation should be supported from the start.

Absolute tracks are for general animation:

- move from A to B
- scale from A to B
- rotate from A to B

Offset tracks are for transient effects:

- shake
- punch
- recoil
- hover feedback

This matters because offset animation can later be used for UI feedback, including textbox shake, without overwriting the layout transform authored elsewhere.

## Data Design

The data should stay simple and explicit so it transpiles cleanly to C++ later.

Preferred characteristics:

- enums over dynamic type lookup
- plain data objects over reflection-driven dispatch
- typed track classes over generic property bags
- no runtime string-based property resolution

Optional lifecycle callbacks are acceptable later, but the main runtime path should stay data-driven and predictable.

## Extensibility Direction

This foundation should not try to solve future skeletal animation now, but it should avoid blocking it.

The future layering should be:

1. low-level transform animation playback from this design
2. later controller/FSM layer that selects and drives clips
3. later skeletal animation playback with a similar operational surface
4. later platform-specific substitutions where controllers can still speak in terms of clip playback

The key decision is to keep the player low-level and reusable, not to force transform and skeletal systems into one implementation prematurely.

## Testing

Focused tests should cover:

- linear interpolation between position keyframes
- step interpolation preserving the previous keyframe value
- offset position adding on top of the base entity transform
- looping correctly wrapping current time
- stopping clearing active playback state
- seeking updating evaluated output deterministically

The first user-facing consumer, such as shaking the build-dialog output-folder textbox on validation failure, should be built on top of this foundation later rather than embedded into the core animation system itself.

## Impact

This introduces a reusable animation base in `helengine.core` that is:

- compact at runtime
- reusable across editor and runtime code
- suitable for later C++ conversion
- broad enough for general transform animation
- narrow enough to avoid premature controller and skeletal complexity
