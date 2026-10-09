# Helengine Timeline

Date: 2026-10-09. Status: approved (Helena: scope "B" — general engine feature for in-game sequences and the video
pipeline; no editor UI for now; property binding "B" — fixed track kinds plus named value channels; architecture "3" —
optional modules with a rich authoring form and a flat cooked form; video bridge as below).

## Goals and constraints

- One sequencing asset that a game plays in a scene (cutscenes, scripted sequences) and the video pipeline compiles into a
  media composition (motion graphics), authored by people or by a planner model (Claude Opus 5.5) as JSON.
- The engine also targets PlayStation 1. Everything is opt-in: core never references the timeline modules; a project that
  does not reference them ships no timeline code. What reaches a console is flat: integer ticks, indices instead of names,
  no nesting, no per-frame lookups.
- No editor UI in this phase. The runtime exposes deterministic evaluation so tests (and a future editor) can scrub.

## Modules

- `helengine.timeline` (tools only: editor, cooker, video): authoring model, JSON form, validator, cooker.
- `helengine.timeline.runtime` (ships to targets): cooked asset, its packaged reader, `TimelinePlayerComponent`,
  `ITimelineReceiver`, `ITimelineEventListener`. Must follow the transpilable subset core uses (see core code).
- Core gains only two value-kind numbers in `EditorAssetBinaryValueKind` (authoring timeline, cooked timeline) and an
  optional start offset on `AudioPlaybackRequest`. Serializers/processors are registered by the timeline modules.
- The media curve math (`linear.v1`, `smoothstep.v1`, `ease_out_cubic.v1`, `ease_in_quad.v1`, `ease_out_back.v1`) moves to a
  neutral place both `helengine.media` and the timeline modules use (ids unchanged; `MediaCurve` keeps its API).

## Authoring model (`TimelineAsset`, `.htimeline`)

- Identity: id, version, display name, description, duration (seconds, double).
- Binding slots: `{name, kind: entity|text|media|rect, description}`. The timeline never names concrete targets; whoever
  plays it binds slots (game: `SceneEntityReference`; video: an element of the edit).
- Cues: `{name, time}` named points. Clips may anchor on a cue (their start is relative to it).
- Tracks (`slot` except events):
  - `transform`: keyframes for position (x, y, z), rotation (degrees), scale; mode absolute or offset.
  - `value`: one named channel (`intensity`, `fov`, `opacity`, `reveal`, ...) with keyframes.
  - `activation`: intervals where the target is active.
  - `audio`: audio asset reference, start, clip-in, gain.
  - `animation`: an `AnimationClipAsset` with start and speed.
  - `event`: markers `{time, name, value?}`.
  - `timeline`: a nested timeline (asset reference or inline), with offset, speed and a slot mapping.
- Clips: start (or cue + offset), duration, clip-in, ease-in/ease-out blend durations, keyframes in clip time. Keyframes:
  time, value, curve id from the catalog.
- JSON form (snake_case) round-trips with the binary HELE form; omitted optional fields take defaults; bounded size.
- `TimelineValidator`: increasing times, no overlapping clips on a track except within blend ramps, known slots and cues,
  catalog curves, value ranges per channel where known, nesting without cycles and bounded depth (e.g. 8), readable
  messages with JSON paths (a planner corrects from them).
- Receivers: a component that accepts value channels implements `ITimelineReceiver` and declares its channels with
  `[TimelineChannels("intensity", "range")]` (read by tools only).

## Cooked form and game runtime

- Cooker, per platform profile: flattens nested timelines (slot remap, offset, speed), converts time to integer ticks at the
  platform tick rate (e.g. 60/50 Hz), turns keyframes into segments `{start tick, end tick, from, to, curve code (byte)}`,
  resolves slot/channel names to indices and receiver component ids to integers, keeps event names (rare). Curve mode per
  profile: native (runtime evaluates the five catalog curves) or linearized (cooker splits curves into linear segments
  within a tolerance; PS1 profile).
- `TimelinePlayerComponent`: cooked timeline reference, one `SceneEntityReference` per slot, play on start, end mode
  (stop, hold, loop), speed, `Play/Pause/Stop/Seek(seconds)`, `Time`. Slots and receivers are resolved once at start. Each
  frame: advance ticks; per track a forward-only cursor finds the active segment; apply: transform → local transform;
  value → `ITimelineReceiver.SetTimelineValue(channelIndex, value)`; activation → entity active; animation →
  `AnimationPlayerComponent` play/seek; audio → `AudioManager.Play` at clip start (with start offset after a seek when the
  backend supports it, otherwise skipped); event → `ITimelineEventListener.OnTimelineEvent(name, value)` on the player
  entity's components. Seeking backwards recomputes cursors.
- `Evaluate(tick)` is deterministic and side-effect free apart from the applied values (tests scrub with it).

## Video bridge

- `overlays[].timeline = { definition | library id + version, bindings: { slot: {text} | {media} | {rect: {color}} },
  cues: { name: <moment> } }`, alongside `region` and `by` as today.
- Bindings create elements: `text` → text layer in the edit's text style, `media` → owned image/video, `rect` → solid
  color rectangle (the technique the strike bar uses). `transform`, `value` (`opacity`, `reveal`), `activation` and nested
  `timeline` tracks compile to composition layers and `PropertyAnimation`s; `audio` to composition audio clips.
- Layout: positions are relative to a box — the overlay region, or the free area as graphics use today — in -0.5..0.5;
  text size is a fraction of the box height. The compiler measures every bound text and scales the whole timeline
  uniformly so it fits the box; safe area and caption band are kept.
- Cues: each cue maps to a moment (usually a spoken word); clips anchored on a cue move with it (shifted, never stretched).
- Diagnostics follow the video validator conventions (`invalid_timeline`, paths into the definition).
- The four `.hgraphic` templates stay as they are; migrating them to timelines is later work.

## Flux Studio

- Planner overlay items gain `timeline` (strict JSON schema derived from the format, bindings, cues as moments). Guidance:
  a catalog template when it fits, an authored timeline when the scene asks for its own motion.
- Authored timelines are kept in a project library (id, version, description, slots, cues, definition) and offered to
  later planning runs; a reference by id is inlined by the host before validation.
- Scene editor shows "timeline criada pela IA" with its bindings (texts) editable like graphic items.

## Delivery stages

1. `helengine.timeline`: model, JSON, binary serializer, validator, curve catalog move.
2. Cooker + `helengine.timeline.runtime`: cooked asset, player, interfaces, audio start offset; tick-by-tick tests; a
   project without timeline modules still builds.
3. Video bridge in `helengine.video`: overlay `timeline`, bindings, cues, fitting, compile + GPU frame test.
4. Flux Studio: planner schema and prompt, library, editor label.
