# Helengine Timeline authoring format

A timeline is one sequencing asset (`.htimeline`) that animates abstract **slots** over a fixed duration. A game binds
the slots to scene entities (cutscenes, scripted sequences); the video pipeline binds them to elements it creates
(texts, images, rectangles). People and planner models author timelines as JSON (`helengine.timeline.v1`); tools store
them in the HELE editor asset format and a cooker flattens them for target platforms.

Code: `engine/helengine.timeline` (tools only). Design: `docs/superpowers/specs/2026-10-09-helengine-timeline-design.md`.

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
