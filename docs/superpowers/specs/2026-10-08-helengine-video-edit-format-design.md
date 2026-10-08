# helengine.video — video edit format

Date: 2026-10-08. Status: approved direction (Helena), spec for implementation.

## Problem

Going from a script to a rendered video currently passes through many overlapping JSON formats spread over three
repositories: Cortex `cortex.edit.decisions.v1/v2` (per-scene recipe plus human patch overrides), the unused
`cortex.edit.plan.v1` / `cortex.edit.resolved.v1` / `recording_tasks.v1` chain, Flux Studio manifests
(`slogan.preview-images.v2`, `slogan.recording-shots.v1`, caption metadata blocks) and finally the engine's
`helengine.media.composition.v1`. The same idea is expressed several ways (scene entry: `entry` cut/fade, `editing.transition`
and the script's `visual.enter/exit`; zoom: `motion`, `editing.animations`, `transform.zoom`; voice fades: three places),
times mix estimated scene-local seconds with rational global time, naming styles mix snake/camel/Pascal case, product and
channel concepts leak into Cortex, and two compilers (Cortex `HelengineCompositionCompiler`, Flux Studio
`SloganCompositionBuilder`) each build part of the composition.

## Decisions

1. The edit format belongs to Helengine, in a new library `engine/helengine.video` above `helengine.media`.
   Flux Studio is the product that stores and edits it; Cortex is the graph/AI runtime that proposes it; channel projects
   (e.g. Depois do Slogan) are configuration, never format structure.
2. C# classes in `helengine.video` are the source of truth. JSON (`helengine.video.edit.v1`, snake_case) is the
   interchange format for the AI and the product, with a JSON Schema generated from the model. Nobody hand-writes it.
   A binary `.hvideo` asset may come later without changing the model.
3. The unit is the **scene**: an ordered sequence of scenes, plus a few **global tracks** for things that cross scene
   boundaries (music/sfx audio, captions).
4. **Durations come from their source**; moments inside a scene are **anchors** (seconds from start or end, fraction of the
   scene, or a spoken word), so nothing has to be re-planned when a take replaces an estimate.
5. **Intent presets** (layout, motion, entry) are stored as presets and expanded only at compile time into one low-level
   vocabulary (viewport, transform, keyframes with catalog curve ids, opacity). Raw keyframes are allowed on the same layer.
6. **One document** is always the current state. Any editable object can carry `"by": "human"`, which locks it: AI
   re-planning never rewrites a locked object. Unlocking returns it to the AI. Revision history lives in the product.

## Document (`helengine.video.edit.v1`)

```json
{
  "schema": "helengine.video.edit.v1",
  "id": "video-7f3a",
  "revision": 12,
  "format": { "width": 1080, "height": 1920, "frame_rate": { "numerator": 24, "denominator": 1 }, "background_color": "#F7F5FAFF" },
  "script_ref": { "id": "script-42", "revision": 7, "sha256": "…" },
  "catalog": { "schema": "helengine.media.capabilities.v1", "fingerprint": "…" },
  "project_profile": "depois-do-slogan",
  "media": [ … ],
  "scenes": [ … ],
  "tracks": { "audio": [ … ], "captions": { … } }
}
```

`script_ref` and `project_profile` are provenance only; the document is self-contained and compiles without them.

### Media

```json
{ "id": "take-03", "kind": "video", "path": "media/ab12….mp4", "sha256": "ab12…", "width": 1080, "height": 1920,
  "duration_sec": 41.2,
  "analysis": {
    "method": "whisper_cpp_token_timestamps",
    "words": [ { "text": "Porque", "start_sec": 12.40, "end_sec": 12.71 } ],
    "silences": [ { "start_sec": 15.10, "end_sec": 15.62 } ]
  } }
```

`kind` is `video | audio | image | font`. Paths are relative to the render assets root and pinned by SHA-256.
`analysis` belongs to the media file (source time), so every scene that uses the file can anchor to its words.

### Scene

```json
{
  "id": "pergunta",
  "script_beat": "question",
  "section": "pergunta",
  "purpose": "Perguntar pela lei concreta.",
  "duration": { "mode": "from_take" },
  "take": { "media": "take-03", "in_sec": 12.30, "out_sec": 16.05 },
  "entry": { "effect": "push", "version": 1, "duration_sec": 0.4, "parameters": { "Direction": "Left" }, "audio_fade_sec": 0.15, "by": "human" },
  "voice": { "gain": 1.0, "muted": false, "envelopes": [] },
  "layers": [ … ],
  "overlays": [ { "id": "q", "text": "QUAL LEI?", "style": "graphic", "at": { "word": "lei" }, "until": { "from_end": 0 } } ]
}
```

- `duration.mode`: `from_take` (take `out_sec - in_sec`), `fixed` (`sec`), or `estimate` (`sec`, marked as an estimate until a
  take is chosen; final rendering refuses estimates).
- `take` is optional (image or title scenes). The take supplies the scene's video and voice.
- `entry` is the only way into a scene: `{ "effect": "cut" }` (default), any catalog transition (`crossfade`, `dissolve`,
  `push`, …), or `{ "effect": "appear", "duration_sec": 0.25 }`, the former opacity "fade" entry (an opacity ramp from
  nothing, not a two-scene transition). A transition never applies to the first scene and never shortens the video: the
  previous scene is extended over the overlap; when its take has no source handle, the last frame is held instead of
  raising a pending issue. `audio_fade_sec` adds equal-power voice fades on both sides of the cut.
- `voice` adjusts the take's audio; envelopes use moments.

### Layer

```json
{
  "id": "picture", "kind": "media", "media": "ref-image-2", "order": 10,
  "layout": { "preset": "inset" },
  "fit": "contain",
  "transform": { "position_x": 0, "position_y": 0, "scale_x": 1, "scale_y": 1, "rotation_deg": 0, "opacity": 1 },
  "motion": { "preset": "zoom_to_focus", "from_scale": 1.0, "to_scale": 1.35, "start": { "fraction": 0.2 }, "end": { "from_end": 0 },
              "curve": "smoothstep.v1", "focus": { "type": "text_region", "text": "salário mínimo" } },
  "animations": [ { "property": "opacity", "keyframes": [ { "at": { "sec": 0 }, "value": 0, "curve": "linear.v1" } ] } ],
  "effects": [ { "id": "rainbow-expand", "version": 1, "parameters": {}, "inputs": {} } ],
  "mask": null,
  "by": "human"
}
```

- `kind`: `take` (the scene's take video, implicit when a take exists and no take layer is given), `media` (image or video
  media), `text` (free text with a style id from the caption/graphic styles).
- `layout`: `{ "preset": "full_frame" | "inset" | "side_by_side" }` or `{ "viewport": { "x", "y", "width", "height" } }` (0–1).
- `motion` presets: `none`, `zoom_to_focus` (`focus` = `center`, `point {x,y}`, or `text_region {text}` resolved by the product
  into a region on the image before compilation and stored as `point`/`region`).
- `animations` and `effects` use the engine catalog ids, versions, properties and curves directly.

### Moments

A moment is one of: `{ "sec": 1.2 }` (from scene start), `{ "from_end": 0.3 }`, `{ "fraction": 0.5 }`,
`{ "word": "política", "occurrence": 1, "offset_sec": 0 }` (start of the n-th matching word of the scene take, normalized
comparison). Global track positions use `{ "scene": "intro", "at": <moment> }`. The compiler resolves every moment to
rational global time; an unresolvable word raises `anchor_unresolved` and falls back to the scene start in previews.

### Global tracks

- `tracks.audio[]`: `{ "id", "media", "start": <scene moment>, "end": <scene moment> | null, "in_sec", "gain", "muted",
  "envelopes": [ { "type": "linear|equal_power_in|equal_power_out", "at": <scene moment>, "duration_sec", "from", "to" } ] }`.
- `tracks.captions`: `{ "source": "voice", "words_per_cue": 8, "words_per_line": 4, "style": <caption style>,
  "overrides": [ { "scene": "intro", "cue": 2, "text": "…", "by": "human" } ] }`. Cues are derived from take words at
  compile time; `style` is the resolved caption style snapshot (font, size, colors, outline, animation and caption
  effects such as the project bubble and soft shadow) copied from the project profile when the document is created.

### Origin and locks

Objects that can carry `"by": "human"`: `take`, `duration`, `entry`, `voice`, each layer, `motion`, each overlay, each
audio track, caption overrides. Replanning APIs receive the document, return a document, and `VideoEditMerge.Replan`
copies every locked object from the current document into the proposal before validation.

## Library layout (`engine/helengine.video`)

One class per file, XML-documented, following AGENTS.md.

- Model: `VideoEdit`, `VideoFormat`, `VideoScriptReference`, `VideoCatalogReference`, `VideoMedia`, `VideoMediaAnalysis`,
  `VideoWord`, `VideoSilence`, `VideoScene`, `VideoSceneDuration`, `VideoTake`, `VideoEntry`, `VideoVoice`, `VideoLayer`,
  `VideoLayout`, `VideoMotion`, `VideoFocus`, `VideoAnimation`, `VideoKeyframe`, `VideoEffect`, `VideoMask`,
  `VideoOverlay`, `VideoMoment`, `VideoTrackMoment`, `VideoTracks`, `VideoAudioTrack`, `VideoEnvelope`,
  `VideoCaptionTrack`, `VideoCaptionOverride`, `VideoCaptionStyle`.
- `VideoEditJson` (System.Text.Json, snake_case, strict: unknown properties and duplicate keys rejected) and
  `VideoEditSchema` (JSON Schema generated from the model, used for AI structured output).
- `VideoEditValidator` → `VideoDiagnostic { code, scene, path, message, severity }`, structural rules only
  (ids unique, references exist, moments well-formed, presets known, catalog ids/parameters valid against
  `MediaCapabilities`).
- `VideoMomentResolver`, `VideoSceneTimeline` (scene start/end in rational global time after durations and entries).
- `VideoEditCompiler.Compile(VideoEdit, VideoCompileContext)` → `VideoCompileResult { CompositionDocument, Diagnostics }`.
  Context: assets root, `MediaCapabilities`, preview vs final. Produces scene groups, take/media/text layers, caption and
  overlay text layers, voice and track audio clips, transitions (with last-frame hold), presets expanded into
  animations. Pending conditions are diagnostics with `severity: pending` (final rendering refuses them).
- `VideoEditMerge`: `Replan(current, proposal)` keeping locked objects.

Required `helengine.media` addition: `VisualLayer.hold_last_frame` (bool, default false). When true a video layer may end
after its source interval; the evaluator clamps the source time to `source_out` minus one frame, so the last frame is held.
The validator then skips the `source_interval` end check for that layer. This is what transition overlaps use when the
outgoing take has no handle.

## Migration

A one-time converter (in Flux Studio, since it owns the old data): script + `cortex.edit.decisions.v2` + recording
analysis + caption settings → `VideoEdit`. Afterwards Flux Studio stores `video_edits(video_id, revision, document_json)`
plus history, Cortex AI nodes read/write `VideoEdit`, and the old formats (`decisions` v1/v2, `plan`, `resolved`,
`recording_tasks`, preview manifests, `HelengineCompositionCompiler`, `SloganCompositionBuilder`) are removed.

## Build order

1. `helengine.video` model, JSON, schema, validator, moments — with unit tests.
2. Compiler to `helengine.media` composition — unit tests on the produced composition, GPU tests on rendered frames
   (take scene, image inset with zoom, overlay at a word, captions from words, push entry with last-frame hold, voice fades).
3. Cortex: AI planning/replanning nodes produce `VideoEdit` (schema-derived output type, catalog injected, locks kept).
4. Flux Studio: storage, migration, preview/final rendering through the new compiler, editing UI on the new document.
5. Remove the old formats and compilers.

## Out of scope

Binary `.hvideo` asset and editor UI in Helengine; multi-video projects; non-voice caption sources.
