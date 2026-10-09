# Helengine Timeline authoring format

A timeline is one sequencing asset (`.htimeline`) that animates abstract **slots** over a fixed duration. A game binds
the slots to scene entities (cutscenes, scripted sequences); the video pipeline binds them to elements it creates
(texts, images, rectangles). People and planner models author timelines as JSON (`helengine.timeline.v1`); tools store
them in the HELE editor asset format and a cooker flattens them for target platforms.

Code: `engine/helengine.timeline` (tools only: model, JSON, validator, flattener, cooker) and
`engine/helengine.timeline.runtime` (ships to games: cooked asset, player). Design: `docs/superpowers/specs/2026-10-09-helengine-timeline-design.md`.

## Model in one paragraph

A timeline declares **slots** (what it animates) and **cues** (named instants). **Tracks** target one slot each and hold
**clips** (timed spans) or, for event tracks, **markers**. A clip starts at an absolute time or at an offset from a cue,
so moving the cue (for example to the moment a word is spoken) moves the clip without stretching it. Keyframes inside
a clip are in **clip time** (0 = clip start). Each keyframe names the curve used from it to the next keyframe.

## JSON reference

General rules:

- Property names are snake_case. Unknown or repeated properties are errors.
- Omitted optional properties, and properties set to `null`, take the defaults below.
- Times and durations are seconds (numbers). Documents are limited to 64 KB.
- Names of slots, cues and channels: 1-64 chars, lowercase letter first, then lowercase letters, digits or `_`.
  Timeline ids and event names may also use `.` and `-`, and may start with a digit.

### Timeline

| Field | Type | Default | Meaning |
|---|---|---|---|
| `schema` | string | `helengine.timeline.v1` | Must be this value when present. |
| `id` | string | required | Stable id, e.g. `contrast_three_terms`. |
| `version` | integer | `1` | Contract version (>= 1). |
| `display_name` | string | `""` | Human-readable name. |
| `description` | string | `""` | When to use it. |
| `duration` | number | required | Total length, > 0 and <= 3600. |
| `slots` | list of slot | `[]` | Targets the tracks animate. |
| `cues` | list of cue | `[]` | Named instants. |
| `tracks` | list of track | `[]` | Applied in order. |

### Slot and cue

| Field | Type | Default | Meaning |
|---|---|---|---|
| slot `name` | string | required | Unique slot name. |
| slot `kind` | `entity` \| `text` \| `media` \| `rect` | required | What the slot is bound to. |
| slot `description` | string | `""` | Guidance for whoever binds it. |
| cue `name` | string | required | Unique cue name. |
| cue `time` | number | required | 0 <= time <= duration. |

### Times: `start` and marker `time`

Either a number (absolute seconds) or `{"cue": "<cue name>", "offset": <seconds, default 0>}` (relative to the cue;
negative offsets lead it).

### Track

Common fields: `kind` (required), `name` (label, default `""`), `slot`.

| `kind` | `slot` | Extra fields | Clip fields besides the shared ones |
|---|---|---|---|
| `transform` | required, any kind | `mode`: `absolute` (default) or `offset` | `position`, `rotation`, `scale`: lists of vector keyframes (rotation in degrees). At least one keyframe overall. |
| `value` | required, any kind | `channel` (required) | `keyframes`: list of scalar keyframes, at least one. |
| `activation` | required, any kind | | none; the slot is active only inside clips. No blends, no `clip_in`. |
| `audio` | optional; `entity` (emitter) | | `audio`: asset reference (required); `gain`: 0..4, default `1`. |
| `animation` | required, `entity` | | `animation`: asset reference to an animation clip (required); `speed`: (0, 16], default `1`. |
| `event` | none | `markers`: list of markers | (no clips) |
| `timeline` | none | | `timeline` (asset reference) **or** `definition` (inline timeline); `speed` (0, 16], default `1`; `slots`: object mapping every nested slot to a slot of this timeline (same kind). |

Shared clip fields:

| Field | Type | Default | Meaning |
|---|---|---|---|
| `start` | time | required | Absolute or cue-anchored start. |
| `duration` | number | required | > 0; the clip must end within the timeline. |
| `clip_in` | number | `0` | Seconds skipped at the start of the source (audio, animation, nested timeline only). |
| `ease_in` | number | `0` | Blend-in ramp. |
| `ease_out` | number | `0` | Blend-out ramp; `ease_in + ease_out <= duration`. |

Clips on one track are listed in start order. Two neighbouring clips may overlap only inside the previous clip's
`ease_out` and the next clip's `ease_in` (a cross-fade); three clips never overlap.

Keyframes:

| Field | Type | Default | Meaning |
|---|---|---|---|
| `time` | number | required | Clip time, 0..clip duration, strictly increasing. |
| `value` | number, or `[x, y, z]` for transform | required | Value at that instant. |
| `curve` | curve id | `linear.v1` | Curve from this keyframe to the next. |

Curves (`CurveCatalog`, shared with helengine.media): `linear.v1`, `smoothstep.v1`, `ease_out_cubic.v1`,
`ease_in_quad.v1`, `ease_out_back.v1` (soft overshoot, good for pops).

Known value channels and their ranges: `opacity` 0..1, `reveal` 0..1, `intensity` >= 0, `range` >= 0, `fov` 1..179.
Other channel names are allowed and passed to the target's receiver as they are.

Markers: `time` (time, required), `name` (required), `value` (string, default `""`). Listed in time order.

Asset references: `{"path": "assets/sfx/pop.wav"}`; optionally `asset_id` + `content_hash` (canonical file
reference), or `"source": "generated"` with `provider_id` and `asset_id`.

Nesting: at most 8 levels; a timeline cannot contain itself (checked through references when the caller supplies a
resolver).

## Diagnostics

Parsing (`TimelineJson.Parse`) and validation (`TimelineValidator.Validate`) report every problem with a JSON path, for
example:

```
tracks[2].clips[0].keyframes[1].time: Keyframe times must increase: 0.2 does not come after 0.3.
tracks[0].slot: Unknown slot 'term_z'; declared slots: term_a, term_b, term_c, sep_ab, sep_bc, strike.
tracks[4].clips[1].start: The clip overlaps clips[0] by 0.3 s; clips may only overlap inside the blend ramps (clips[0].ease_out = 0.2, this ease_in = 0.25).
```

## Complete example

Three terms appear on their spoken words, separated by "≠"; the last term pops through a nested timeline and a bar
strikes it. The host binds `term_a`, `term_b`, `term_c` to the texts `LEGALIZAR`, `DESCRIMINALIZAR` and `TRATAR`,
`sep_ab`/`sep_bc` to `≠`, `strike` to a rectangle, and moves cues `a`, `b`, `c` to the spoken words.

<!-- timeline-example -->
```json
{
  "schema": "helengine.timeline.v1",
  "id": "contrast_three_terms",
  "version": 1,
  "display_name": "Three-term contrast",
  "description": "Three terms appear on their words, separated by a not-equal sign; a bar strikes the last term.",
  "duration": 3.6,
  "slots": [
    { "name": "term_a", "kind": "text", "description": "First term, e.g. LEGALIZAR." },
    { "name": "term_b", "kind": "text", "description": "Second term, e.g. DESCRIMINALIZAR." },
    { "name": "term_c", "kind": "text", "description": "Third term, e.g. TRATAR." },
    { "name": "sep_ab", "kind": "text", "description": "Separator between the first two terms, e.g. ≠." },
    { "name": "sep_bc", "kind": "text", "description": "Separator between the last two terms, e.g. ≠." },
    { "name": "strike", "kind": "rect", "description": "Bar drawn across the third term." }
  ],
  "cues": [
    { "name": "a", "time": 0.15 },
    { "name": "b", "time": 1.2 },
    { "name": "c", "time": 2.4 }
  ],
  "tracks": [
    { "kind": "activation", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 3.45 } ] },
    { "kind": "transform", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 0.45,
      "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },

    { "kind": "activation", "slot": "sep_ab", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 2.6 } ] },
    { "kind": "value", "slot": "sep_ab", "channel": "opacity", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 0.2,
      "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },

    { "kind": "activation", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 2.4 } ] },
    { "kind": "transform", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 0.45,
      "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },

    { "kind": "activation", "slot": "sep_bc", "clips": [ { "start": { "cue": "c", "offset": -0.2 }, "duration": 1.4 } ] },
    { "kind": "value", "slot": "sep_bc", "channel": "opacity", "clips": [ { "start": { "cue": "c", "offset": -0.2 }, "duration": 0.2,
      "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },

    { "kind": "activation", "slot": "term_c", "clips": [ { "start": { "cue": "c" }, "duration": 1.2 } ] },
    { "kind": "timeline", "name": "pop term c", "clips": [ { "start": { "cue": "c" }, "duration": 0.45, "slots": { "target": "term_c" },
      "definition": {
        "id": "pop", "duration": 0.45,
        "slots": [ { "name": "target", "kind": "text" } ],
        "tracks": [ { "kind": "transform", "slot": "target", "clips": [ { "start": 0, "duration": 0.45,
          "scale": [ { "time": 0, "value": [0.5, 0.5, 1], "curve": "ease_out_back.v1" }, { "time": 0.35, "value": [1, 1, 1] } ],
          "rotation": [ { "time": 0, "value": [0, 0, -6], "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": [0, 0, 0] } ] } ] } ]
      } } ] },

    { "kind": "activation", "slot": "strike", "clips": [ { "start": 3, "duration": 0.6 } ] },
    { "kind": "value", "slot": "strike", "channel": "reveal", "clips": [ { "start": 3, "duration": 0.35,
      "keyframes": [ { "time": 0, "value": 0, "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": 1 } ] } ] },

    { "kind": "audio", "clips": [ { "start": { "cue": "c" }, "duration": 0.4, "ease_out": 0.1, "gain": 0.7,
      "audio": { "path": "assets/sfx/pop.wav" } } ] },
    { "kind": "event", "markers": [ { "time": { "cue": "c" }, "name": "emphasis", "value": "term_c" } ] }
  ]
}
```

## Using it from code

- `TimelineJson.Parse(string|JsonElement, ITimelineAssetResolver)` returns a validated `TimelineAsset` or throws
  `TimelineFormatException` (with `Diagnostics`); `TimelineJson.ReadUnvalidated` reads structure only;
  `TimelineJson.Write`/`Serialize` write the canonical form (defaults omitted).
- `TimelineValidator.Validate(timeline, resolver)` returns `TimelineDiagnostic`s (`Path`, `Message`); a null resolver
  leaves asset references unfollowed.
- `TimelineSerialization.Register()` adds the HELE serializer (value kind 14) to `EditorAssetPayloadSerializerRegistry`;
  `TimelineFile.Load/Save` read and write validated `.htimeline` files. Core only reserves value kinds 14 (authoring
  timeline) and 15 (cooked timeline) and never references the timeline modules.

## Cooking and runtime

A game never plays the authoring form. The tools cook it into a flat `CookedTimelineAsset` (value kind 15, file
extension `.hctimeline`) that the shipping module `helengine.timeline.runtime` plays with `TimelinePlayerComponent`.
The runtime depends only on core and the curve catalog and is written in the C# subset core uses, so it transpiles to
C++ like core; it never references the authoring module.

### Cooking

```csharp
TimelineSerialization.Register();                       // HELE serializers for kinds 14 and 15
TimelineReceiverCatalog receivers = new TimelineReceiverCatalog();
receivers.AddAssembly(typeof(LampComponent).Assembly);  // reads [TimelineChannels] declarations
TimelineCookOptions ps1 = new TimelineCookOptions { TickRate = 50, CurveMode = TimelineCurveMode.Linearized, Tolerance = 0.001 };
CookedTimelineAsset cooked = TimelineCooker.Cook(timeline, ps1, resolver, receivers);
using FileStream file = File.Create("cutscenes/intro.hctimeline");
EditorAssetBinarySerializer.Serialize(file, cooked);
```

| Option | Default | Meaning |
|---|---|---|
| `TickRate` | `60` | Ticks per second of the target, 1..1000 (50 for PAL consoles). |
| `CurveMode` | `Native` | `Native` keeps the five catalog curves as byte codes; `Linearized` writes only straight segments (code 0). |
| `Tolerance` | `0.001` | Largest error, in value units, when curves or cross-fades become straight segments. |

What the cooker does (`TimelineFlattener` does the first steps in seconds; `TimelineCooker` the rest):

- **Cues** become times. **Nested timelines** (inline or through the resolver) are expanded: the child time
  `clip_in + (t - start) * speed` plays while the parent is inside the clip, slots go through the mapping, and
  everything is cut to the clip window (and to every enclosing window). Nesting is bounded by the validator (8 levels).
- **Keyframes** become segments `{start, end, from, to, curve}`. A clip holds its first value before its first keyframe
  and its last value after the last one, up to the clip edges. After a segment ends the channel holds its end value
  until its next segment; before its first segment it has no value.
- **Blends**: where two neighbouring clips of one track overlap (inside the previous clip's `ease_out` and the next
  clip's `ease_in`) and both drive a channel, the overlap becomes a linear cross-fade, `(1 - w) * previous + w * next`
  with `w` rising from 0 to 1 across the overlap, written as straight segments within the tolerance. A ramp with no
  overlapping neighbour is ignored for transform and value channels (there is no other value to fade from). Animation
  clips that overlap are cut at the next start (one animation player shows one clip). Audio ramps are kept in the
  flattened form for consumers that can fade, but the game runtime does not fade sounds.
- **Several tracks on one target** (for example an outer track and a nested timeline on the same slot and channel) are
  merged into one: at every moment the segment that started last wins (later tracks win ties). Cut segments keep their
  exact curve in the flattened form; activation tracks of a slot are unioned.
- **Nested speed** compresses keyframes, activation and animation (animation speed is multiplied); sounds keep their
  natural rate, so a nested sound is only moved and, if needed, cut.
- **Ticks**: times round to the nearest tick (exact halves round up), so boundaries move at most half a tick and shared
  boundaries stay shared. A segment whose rounded span is empty becomes a step to its end value. Values are stored as
  32-bit floats.
- **Curves**: in `Native` mode whole segments keep their curve code; a segment cut mid-curve (by a window or a
  higher-priority track) is split into straight pieces. In `Linearized` mode every curve is split; the pieces are cut at
  ticks so that every tick, which is all the runtime samples, is within `Tolerance` of the native curve.
- **Names**: slots become indices (the slot names stay in a table for binding tools), value channels become a receiver
  id and a channel index through the `ITimelineChannelResolver`, and event names and values go to a string table
  (index 0 is the empty string).
- Track order in the cooked asset: transform components and value channels (first appearance), activation per slot,
  animation per slot, one audio track, one event track.

Other compilers (the video bridge) can start from `TimelineFlattener.Flatten(timeline, resolver)` and read
`FlattenedTimeline` directly: curve tracks per slot and channel in seconds with catalog curve ids
(`FlattenedCurveTrack.TryEvaluate` samples one), activation intervals, animation and audio clips, and events; the
overload with `cueTimes` moves root cues first (see Video overlays).

### Making a component a receiver

```csharp
[TimelineChannels("intensity", "range", ReceiverId = 12)]
public sealed class LampComponent : Component, ITimelineReceiver {
    public int TimelineReceiverId { get { return 12; } }

    public void SetTimelineValue(int channel, double value) {
        if (channel == 0) { Intensity = (float)value; } else { Range = (float)value; }
    }
}
```

The attribute is read only by the tools; the channel index is the position in its list. The receiver id must be
positive, unique among receiver types, equal to `TimelineReceiverId`, and never change once timelines are cooked against
it (the runtime finds the receiver on the slot entity by this id, without reflection). When two receiver types declare
the same channel name, tell the catalog which one a slot carries with `receivers.BindSlot("lamp", typeof(LampComponent))`.

### Playing

```csharp
TimelineRuntimeRegistration.Register(core);  // content processor for .hctimeline (generated cores call it for you)
TimelinePlayerComponent player = new TimelinePlayerComponent {
    TimelinePath = "cutscenes/intro.hctimeline",  // or Timeline = cookedAsset
    Slots = slotReferences,                         // one SceneEntityReference per slot, in slot order
    PlayOnStart = true,
    EndMode = TimelineEndMode.Hold,
    Speed = 1f
};
director.AddComponent(player);
```

- The player binds once, on the first `Play`, `Seek` or `Evaluate`: it loads the timeline, resolves every slot used by
  a track (an unresolved one throws, naming the slot), finds the receivers (by id) and animation players, resolves
  audio and animation assets through `AssetSource` (default: the core's scene asset resolver), and collects the
  `ITimelineEventListener` components on its own entity. Nothing is looked up, allocated or formatted per frame.
- **Transform** tracks write local position, rotation (Euler degrees, X pitch, Y yaw, Z roll) and scale. Absolute tracks
  replace a component; offset tracks add to the transform captured at bind (scale multiplies). A group (position,
  rotation, scale) a timeline drives is rewritten every frame from the bound transform, so the result depends only on
  the tick. **Value** tracks call `SetTimelineValue(channel, value)` while the channel has a value. **Activation**
  enables the slot entity inside its intervals and disables it outside; at the final tick activation reads the last
  frame, so slots active until the end stay visible. Do not put the player's own entity (or an ancestor) in an
  activation slot: disabling it would stop the player. **Animation** tracks switch the slot's
  `AnimationPlayerComponent` to the clip (paused) and seek it to `clip_in + elapsed * speed`, holding the last pose
  after the clip. **Audio** starts each sound through `AudioManager.Play` (bus `master`, the clip's gain) when playback
  crosses its start and stops it at its end; the audio slot (emitter) is not used yet. **Events** call
  `OnTimelineEvent(name, value)` on the listeners when playback crosses their tick.
- `Play` starts from the beginning (firing tick-0 events) or resumes after `Pause`/`Seek`; `Pause` silences sounds and
  resuming restarts them at the paused position; `Stop` rewinds without touching applied values. End modes: `Stop`
  applies the final frame and stops, `Hold` keeps playing and re-applies the final frame every update, `Loop` wraps and
  fires the start events again.
- `Seek(seconds)` applies the target immediately (a stopped player becomes paused there). Events in the skipped span
  never fire, and a marker exactly at the target does not fire either; seeking backwards and playing over a marker
  again fires it again. Sounds that span the target restart with `AudioPlaybackRequest.StartOffsetSeconds` when the
  backend implements `ISeekableAudioBackend` (`AudioManager.SupportsStartOffset`); otherwise they are skipped until
  their next start.
- `Evaluate(tick)` applies the state of a tick (transforms, values, activation, animation) without firing events,
  starting sounds or moving `Time`: tests and tools scrub with it, and it is deterministic.

### PlayStation 1 notes

- Cook with `TimelineCurveMode.Linearized` and the console's tick rate (50 or 60): the runtime then only interpolates
  linearly. Ticks are 32-bit integers; values are 32-bit floats (soft-float on PS1, so keep timelines lean).
- Rotation tracks convert Euler angles to a quaternion each frame for the slots they drive (trigonometry); prefer
  position, scale, value and activation tracks for console cutscenes.
- Backends that cannot seek play sounds from their start: bake the authored `clip_in` into the sound, and expect sounds
  that a seek lands inside to be skipped.

### Leaving timelines out of a build

Core only reserves value kinds 14 and 15 and knows the optional audio start offset; it never references the timeline
modules (an architecture test enforces it, and that only the timeline projects and the tools-only video compiler
reference them). A game that does not
reference `helengine.timeline.runtime` ships no timeline code. A game that does gets it through the module's
`GeneratedRuntimeModuleManifest` (`timeline-runtime-module`), whose bootstrap `TimelineRuntimeRegistration.Register` is
emitted only when cooked content uses `TimelinePlayerComponent`. The authoring module `helengine.timeline` is tools-only
and is never part of a game build.

## Video overlays

The video edit format (`helengine.video.edit.v1`) plays timelines as overlay motion graphics:
`overlays[].timeline = {definition | library, bindings, cues}`. The full contract (box coordinates, sizes, fit, timing,
diagnostics and a complete example) is in `docs/helengine-media-composition.md#overlay-timelines`; in short:

- Slots are bound to edit elements: `text` slots to `{"text": "...", "size"?, "color"?}`, `media` slots to
  `{"media": "<image or video id>", "size"?}`, `rect` slots to `{"rect": {"color", "width"?, "height"?, "match"?}}`.
  `entity` slots, audio and animation tracks, value channels other than `opacity` and `reveal` (rect only) and nested
  timelines given by reference are rejected; event tracks are ignored.
- Positions are box units (x right, y down, -0.5..0.5 spans the overlay box), rotation is degrees clockwise around Z,
  sizes are fractions of the box height, and the whole timeline shrinks uniformly to fit the box.
- Cues are mapped to scene moments (usually spoken words). The compiler flattens with
  `TimelineFlattener.Flatten(timeline, resolver, cueTimes, blendTolerance)`: root cues move to the given times, clips
  anchored on a moved cue shift and keep their length, the timeline grows by the largest later shift, root activation
  clips that lasted until the authored end last until the new end, and clips and markers are re-sorted; overlaps the
  move creates fail validation like any other overlap. Cues of nested timelines never move.
- `composition capabilities` advertises the support as `timeline: {format, slot_kinds, track_kinds, value_channels,
  curves, default_text_size, default_media_size, doc}`.
