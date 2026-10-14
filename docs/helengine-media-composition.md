# Helengine audiovisual composition

Helengine owns the exact rational timeline, image/video decoding, transforms and keyframes, text/captions, masks, scene groups, visual crossfades and sample-accurate PCM mixing. The FFmpeg executable receives only final RGBA frames and mixed float PCM, then encodes/muxes them. FFmpeg decoding libraries are internal to the engine; ffprobe is used only for measured host metadata.

Publish an immutable package from Helengine with:

```powershell
./tools/publish-media-composition.ps1 -OutputDirectory C:/dev/helworks/builds/helengine/media-composition/packages/<generation>
```

The package includes native FFmpeg DLLs, shaders, executable schema, `media-capabilities.json` and a checksum receipt. Install Depois do Slogan using `tools/install-cortex-plugin.ps1 -CortexRoot <cortex> -ProfileDirectory <profile> -HelengineDirectory <package> -Enable`. Existing keys, model settings, footage/library and older live DLL folders remain intact.

```text
dotnet helengine.vfx.cli.dll composition capabilities --json
dotnet helengine.vfx.cli.dll composition validate --input composition.json --assets-root assets
dotnet helengine.vfx.cli.dll composition render --input composition.json --assets-root assets --out preview.mp4 --profile mp4-h264-aac.v1 --ffmpeg <trusted encoder>
dotnet helengine.vfx.cli.dll composition render --input composition.json --assets-root assets --out master.mov --profile mov-prores4444-pcm.v1 --ffmpeg <trusted encoder>
dotnet helengine.vfx.cli.dll composition frame --input composition.json --assets-root assets --time 24/24 --out frame.png
```

Initial output is SDR RGBA8; MP4 H.264/AAC flattens alpha in the engine, MOV ProRes4444/float PCM preserves it. Mixing is stereo, normally48kHz, with explicit gain/mute and linear/equal-power envelopes, no automatic normalization. Output blocks are bounded to32768 sample frames; export streams one video frame and2048 PCM frames with backpressure. Composition/frame/export use the same evaluator and GPU compositor. A picture transition does not imply an audio fade or shorten the timeline. Video endpoints require real source handles through the entire overlap.

All media paths are relative to the host-owned root and SHA256 pinned. Fonts used by product previews are staged with their hashes. Caption words are actual supplied alignments; planning previews may use explicit estimated caption cue timing. Missing source/focus and stale catalog decisions remain pending. Native backward audio reads restart the resampler from source origin for reproducibility, which can cost seek time on long footage.

Editorial `cortex.edit.decisions.v2` remains the AI proposal and reference-image plan; since the `helengine.video` edit format (below) the stored video edit is the source of truth for editing. Decisions v2 is additive: catalog pin, transforms/keyframes, registered effects, masks, transitions and audio. V1 remains readable and is not automatically converted; an explicit advanced manual edit upgrades it. Human overrides survive replanning. Final compilation requires current resolved speech and intervals. When the script file has different whitespace/newlines than canonical semantic JSON, pass `LoadedEditScript.Reference` to the compiler overload as the actual current byte fingerprint. The host must validate current edit-plan/recording-task references before final compilation.

Flux Studio (here for the Depois do Slogan channel project) renders available selected recordings and owned images; absent recordings stay silent. Optional audio material uploads accept up to128MiB and use the existing authenticated project access. Rendering/manual editing do not invoke a model; drafting/replanning keep existing spending confirmations. A completed preview is published only after transactional comparison of script/version, editorial plan/focus, selected recording state and media byte hashes. Stale completion is discarded and reported as requiring a new preview. Old saved previews default to `has_audio=false`.

Validation rejects unknown versions/parameters, source/hash/path changes, invalid intervals and cyclic/shared group ownership before source/GPU allocation. A failed/canceled encoder never publishes a final output or receipt. Package generations and old plugin DLL directories are retained so existing processes are not overwritten.

## Effects (`.heffect`)

Effects are data-driven `EffectAsset`s stored as binary `.heffect` files (HELE editor asset format): named inputs, scaled
intermediate targets, ordered fullscreen HLSL passes with per-pass constants, and typed parameters (float, integer,
float2/float4, RGBA color, bool, enum) packed into 16 constant slots. `VfxEffectCatalog` holds the engine built-ins plus,
with `--project <dir>` on the `vfx` and `composition` CLI commands, every `assets/**/*.heffect` of a Helengine project.
Project shaders resolve inside the project's `assets` and may `#include "shaders/common/VfxCommon.hlsli"`. Scene frames
reach effects in linear light with straight alpha. Effects are authored in C# (`VfxEffectFile.Save`); Flux Studio ships its
caption effects (`slogan-soft-shadow`, `slogan-bubble`) in `src/DepoisDoSlogan.CortexPlugin/HelengineProject`, generated by
`tools/DepoisDoSlogan.EffectBuilder`.

## Transitions

A transition is an `EffectAsset` of category `transition` reading `From` and `To` scene frames, with the transition
progress as its normalized time. Built-ins: `dissolve`, `dip-to-color`, `blur-dissolve`, `push`, `wipe`, `zoom`, `whip`,
`shape-reveal`, `flash`, `glitch` (shared Easing/Direction/Shape/Amount/Color parameters); `crossfade` v1 keeps its dedicated
pass. Transition parameters are validated against the catalog. A video layer may set `hold_last_frame` so an outgoing take
without source handles holds its last frame under the overlap; the field is omitted when false.

## Playback rate

Visual layers and audio clips may set `rate` (source seconds per timeline second, 0.25..4, omitted when 1): a layer reads
`source_in + local * rate`, and validators require `source_out - source_in >= duration * rate`. The mixer time-stretches a
clip whose rate is not 1 with the pitch preserved (WSOLA: 30 ms Hann frames, 50% overlap, 10 ms cross-correlation search).
Each clip's whole source span is stretched once and cached, so the samples are deterministic and independent of the block
size, offline and in realtime playback alike; the first block of a long rate-changed clip pays for its stretch.

## Video edit format (`helengine.video`)

`helengine.video.edit.v1` is the scene-based editing document compiled to compositions by `VideoEditCompiler`. Scenes take
their length from a take (`from_take`), a fixed length or an estimate; moments are seconds, seconds before the end, a
fraction, or a spoken word/phrase of the take. Layout and motion presets expand at compile time; entries are `cut`, `appear`
or a catalog transition. Global tracks carry music and caption settings; captions derive from take words (estimated from the
planned speech in previews). Any lockable object may carry `"by": "human"`; `VideoEditMerge.Replan` keeps locked objects when
the AI proposes again. Spec: `helengine/docs/superpowers/specs/2026-10-08-helengine-video-edit-format-design.md`.

## Take speed and jump cuts

A take may set `speed` (0.5..2, default 1; the voice keeps its pitch) and `segments: [{in_sec, out_sec}, ...]`: intervals of
the same media in source order, without overlap, each at least 0.1 s and inside the media, played back to back. With segments,
`in_sec`/`out_sec` must equal the first segment start and the last segment end. Diagnostics: `invalid_take_speed`,
`invalid_take_segment`, `take_segment_too_short`, `take_segment_order`, `take_segment_envelope`. A `from_take` scene lasts the
sum of the segment lengths divided by the speed (each piece rounded down to the nanosecond so it never needs more source than
its segment). Everything keyed to source seconds maps through the segment table and the speed: caption words (only words
starting inside a kept segment; a word ends no later than its segment), word anchors, and `VideoSceneFacts` (words, silences,
`speed`, `segments` with scene and source times, `voiced` and `look_away` spans in scene time; handles come from the first and
last segment). `media[].analysis` may carry `voiced` and `look_away` source spans (`[[start, end], ...]`) for planners; they do
not change the compiled video.

Each segment of a take layer compiles to its own member (`{scene}-{layer}`, then `{scene}-{layer}-segment-N`) with the same
viewport, fit, transform, effects and mask, spanning its slice of the scene at `rate = speed`; the scene-long motion and
animations are cut into each piece (curved spans sampled per frame) so they run continuously across the cuts. A blurred
speaker background is split the same way; masks follow because they sample the layer source time. The voice becomes one clip
per segment (`voice-{scene}`, then `voice-{scene}-segment-N`) at the take speed; at each cut the clips overlap by up to 10 ms
with equal-power fades (less when the media has no source to spare), and voice gain, mute and envelopes still apply across the
scene (an envelope a cut splits is divided between the clips). Transition handles extend the last segment.

## Graphic templates (`.hgraphic`)

Kinetic typography is authored as `GraphicTemplateAsset`s (HELE `.hgraphic`, saved with `GraphicTemplateFile.Save`): slots
(`items`, optional `separator`, optional `accent_item`), typed parameters (effect parameter shapes), `vertical`/`horizontal`
layouts and elements whose tracks (opacity, scale, offset, bar reveal) are timed relative to their item's moment or the next
item's moment, with catalog curves (`ease_out_back.v1` gives pops a soft overshoot). `GraphicTemplateCatalog` holds the
built-ins `contrast_chain`, `list_build`, `highlight_word` and `strike_replace` plus, with `--project <dir>`, every
`assets/**/*.hgraphic`; `composition capabilities` publishes them as `graphic_templates`. An overlay sets
`graphic: {template, version, items, at[], separator?, accent_item?, layout?, parameters}` (one moment per item, usually the
spoken word; `text` stays the fallback). The compiler measures items with `VideoCompileContext.TextMeasurer`
(`WindowsVideoTextMeasurer` uses the renderer's fonts; without one a deterministic estimate is used and an Info
`text_measure_estimated` diagnostic is raised), lays them out in an 84 % safe area above the caption band minus the
parts the scene's take/media layers cover (as presented: fit, clip, padding, static transform; the free box keeping the
text largest wins, centered in it, and when none allows 40 % of the style size, e.g. a full-frame picture, the whole safe
area is used as before), scales them uniformly to fit, and emits one group (exit fade, caption lift) owning one text layer per element instance; it needs
`VideoCompileContext.GraphicTemplates`. `composition compile-edit --input <edit.json> --assets-root <root> --out <json>
[--project <dir>]` compiles an edit with both. Spec: `docs/superpowers/specs/2026-10-08-helengine-graphic-templates-design.md`.

The caption band is estimated by `VideoCaptionBand` from the caption style (center, font size, `MaxWidth`) and the track's
words per cue/line: a line holds no more words than fit the wrapping width at an average word width (6.5 characters of
0.6 em plus a 0.3 em space), up to 4 lines, so large caption fonts on narrow frames reserve the lines they will wrap into.
Graphics and arrangement regions keep 1.5 % of the frame height away from it.

## Scene arrangements

A scene may declare `"arrangement": {"preset": "<id>", "by"?: "human"|"ai"}` (lockable; `VideoEditMerge` keeps it like
the other scene objects). Layers and overlays (plain, graphic or timeline) claim a region with `"region": "<name>"`:

```json
{"id": "difference", "arrangement": {"preset": "stack"},
 "layers": [{"id": "picture", "kind": "media", "media": "m1", "fit": "cover", "region": "main"}],
 "overlays": [{"id": "chain", "text": "A ≠ B", "region": "top", "graphic": {"template": "contrast_chain", "items": ["A", "B"], "at": [...]}}]}
```

Presets are built-in data (`VideoArrangementPresets`), published by `composition capabilities` as `arrangements[]`
(`{id, description, regions: [{name, role, description}]}`, roles `picture`, `graphic`, `background`):

| preset | regions | portrait / square | landscape |
| --- | --- | --- | --- |
| `full` (default) | `main` | whole frame | whole frame |
| `stack` | `top` (graphic), `main` (picture) | `top` x .05 w .90, safe y 0–.33; `main` x .04 w .92 (square .05/.90), safe y .36–1 | `main` left column, `top` right column (as `split`) |
| `split` | `left` (picture), `right` (graphic) | as `stack` (`right` on top, `left` below) | `left` x .03 w .50, safe y .02–.98; `right` x .56 w .40, safe y .06–.94 |
| `take_with_graphic` | `background` (take), `band` (graphic) | `background` whole frame; `band` x .05 w .90 (square .06/.88), safe y 0–.22 (square .24) | `band` x .10 w .80, safe y 0–.26 |
| `take_behind_graphic` | `background` (take), `front` (graphic) | `background` whole frame; `front` x .06 w .88, safe y .04–.76 (square x .08 w .84, y .04–.84) | `front` x .10 w .80, safe y .05–.90 |
| `graphic_only` | `main` (graphic) | x .06 w .88, safe y .06–.94 (square x .08 w .84, y .05–.95) | x .10 w .80, safe y .05–.95 |

x and width are frame fractions; y is a fraction of the caption-free safe frame, which runs from 4 % of the frame height
to the top of the estimated caption band (or 96 % without captions; captions centered in the top half flip it). Frames
are portrait when height/width > 1.15, landscape when width/height > 1.15, square otherwise. `stack` and `split` keep
their region names on every frame and swap geometry by orientation. `VideoArrangementPresets.Resolve(preset, edit)` (or
`Resolve(preset, format)`, which assumes the safe frame ends at 70 %) returns every region's normalized rectangle and pixel
size so products generate media at the region's aspect.

Compilation: a layer in a region takes the region as its viewport, its layout preset applying inside the region and an
explicit `layout.viewport` still winning; region layers are always clipped to their viewport. A graphic in a region is
measured, fitted (allowed to grow up to 2x the style size) and centered in the region, cut away from the caption band only
when they overlap; the free-area search is skipped. A plain overlay in a region is wrapped like captions at the region
width, shrunk to fit and centered (`CenterX`, `CenterY`, `MaxWidth`, `FontSize` are set on its style). Without a region
everything behaves as before; captions always keep their band.

Validation: an unknown preset is `unknown_arrangement`; a region the scene's preset does not have is `unknown_region` (a
scene without an arrangement is `full`, so only `main` is accepted there); two picture layers (take, image or video) in
one region raise the warning `region_shared`. The schema lists the presets and every region name as enums. Spec:
`docs/superpowers/specs/2026-10-08-helengine-scene-arrangements-design.md`.

## Overlay reading time (`hold_sec`)

Any overlay (plain, graphic or timeline) may set `"hold_sec": <0..10>`: it stays visible at least that long after its
last item appears (the last graphic item `at`, the first activation of the last timeline slot, or the overlay start for
plain text), even when `until` or the scene ends sooner; without it nothing changes. A held overlay that outlives its
scene is emitted as a document-level layer (not a member of its scene group, absolute times, overlay order, caption lift)
so the scene group does not cut it: it keeps the box laid out in its own scene and draws above the following scenes, up
to the end of the video at most. A timeline never runs past its own end, and its cut-short fade happens at the held end;
a graphic's exit fade likewise. Simultaneous plain overlays are stacked with their held ends. Out of range is
`invalid_overlay` (path `...overlays[i].hold_sec`); the schema bounds it; `VideoEditMerge` keeps it with a locked
overlay. When a held overlay is on screen together with an overlay of a later scene whose box intersects its own, the
later overlay gets the warning `overlay_hold_overlap`.

## Speaker background (blur and replacement)

A scene with a take may declare `"background": {"mode": "original"|"blur"|"replace", "radius"?: 24, "media"?: "<id>", "by"?: "human"|"ai"}`
(lockable; `VideoEditMerge` keeps a `by: "human"` background through replans). `radius` is the blur radius in output pixels
(0 to 96); `media` is the image or video shown behind the speaker for `replace`. A non-original background on a scene without
a take is `background_without_take`; an unknown mode, a radius outside 0..96 or a `replace` without image/video media is
`invalid_background`.

The speaker is cut out with a matte declared on the take media: `media[].matte` is the id of a grayscale video aligned frame
by frame with the media (it may be smaller, same aspect; the mask shader samples it bilinearly), and `media[].foreground` is
an optional despilled video, also aligned, drawn inside the matte instead of the media. Both must name existing video media
(`invalid_matte`). For every `take` layer of such a scene the compiler emits two members, background first so it draws under
the speaker (members of equal `order` keep list order):

- `{scene}-{layer}-background`: for `blur`, a copy of the take layer (same viewport, transform, motion, animations, timing and
  effects, fit forced to `cover`, no mask) plus the `gaussian-blur` effect; for `replace`, a `cover` media layer of
  `background.media` over the scene (a video shorter than the scene holds its last frame).
- `{scene}-{layer}`: the normal take layer with `mask = {media: matte, channel: "luma"}` and `foreground ?? take` as media.

The copy is silent: voice audio comes only from the take's `voice-{scene}` clips. Without a matte the layer renders with
its original background and the compiler reports the pending `background_matte_pending`. A take layer that already has its own
mask keeps it, skips the background and reports the warning `background_mask_conflict`.

## Gaussian blur effect

`gaussian-blur` v1 is a built-in layer effect (one input, `Source`) with parameter `Radius` (float, output pixels, default 24,
0 to 96). It is a separable gaussian: a horizontal pass into a half-resolution RGBA16F target, then a vertical pass to the
output; color is blurred premultiplied, edges clamp, and a radius under half a pixel returns the source unchanged.
Shader: `helengine.vfx.directx11/shaders/effects/GaussianBlur.hlsl`.

## Overlay timelines

An overlay may carry motion graphics authored as a Helengine Timeline (`helengine.timeline.v1`, format in
`docs/helengine-timeline.md`) instead of a catalog graphic: `overlays[].timeline` (an overlay has at most one of `graphic`
and `timeline`; `text` is then an optional label). `region`, `by`, `at`, `until` and `style` keep their meaning.

```text
timeline: {
  definition: <helengine.timeline.v1 object>        exactly one of definition / library
  library:    {id, version = 1}                       host placeholder; must be inlined before validation
  bindings:   {<slot>: {text, size?, color?} | {media, size?} | {rect: {color, width?, height?, match?}}}
  cues:       {<cue>: <moment>}                       optional; usually {"word": ...}
}
```

- **Bindings**: every `text`, `media` and `rect` slot of the definition is bound exactly once to an element of its kind
  (`entity` slots are games-only). `text` is drawn on one line in the overlay style (`color` overrides its text color);
  `media` is an image or video id of the edit; `rect` is a solid rectangle (drawn like the strike bar, as a text panel).
  Colors are `"#RRGGBB"`, `"#RRGGBBAA"` or `[r, g, b(, a)]` in 0..1.
- **Box and coordinates**: positions are box units, x right and y down, the box spanning -0.5..0.5 on both axes (0, 0 is
  its center; a slot without a position track sits there). The box is the overlay's region (kept off the caption band)
  or, without a region, the largest part of the safe area the scene's pictures leave free (as graphics do), at most as
  tall as it is wide. Rotation is degrees clockwise (Z only); scale X/Y multiply the element size (zero or negative
  scales fade the element out instead of flipping it). Position Z, rotation X/Y and scale Z are ignored.
- **Sizes**: a text's font size is `size` x box height (default `0.18`); a media's height is `size` x box height
  (default `0.5`, width from its aspect; the media must declare `width` and `height`); a rect is `width` x box width (default `0.5`) by `height` x box height
  (default `0.05`), or as wide as the measured text of the text slot named in `match` (strikes, underlines; the measured
  advance is slightly wider than the ink on the right).
- **Fit**: every bound text is measured (`VideoCompileContext.TextMeasurer`, else the estimate with
  `text_measure_estimated`), each element's rotated bounds are taken at every instant its motion changes (and halfway
  between) while it is visible (opacity >= 0.05; elements may enter from outside the box only while transparent), and the
  whole timeline is scaled uniformly about the box center so all of it stays inside the box. The timeline is never
  enlarged, and texts never exceed twice the style font size. Overlaps between elements are the author's choice.
- **Tracks and channels**: transform tracks (absolute and offset), `opacity` (any slot) and `reveal` (rect only; grows the
  rect from its left edge), activation (a slot is visible inside its intervals; without an activation track it is
  visible for the whole timeline), inline nested timelines and event tracks (events are ignored). Audio tracks (engine
  audio assets), animation tracks, other channels and referenced nested timelines are rejected; add sounds as edit audio
  tracks.
- **Timing**: timeline time 0 is the overlay `at` when given, otherwise the instant that puts the earliest mapped cue
  (by moment) on its moment (the scene start when no cue is mapped). Every mapped cue moves to its moment; clips anchored
  on it shift with it and keep their length (never stretched); the timeline grows by the largest later shift, and
  activation clips that lasted until the authored end last until the new end. The overlay ends when the timeline ends,
  at `until` or at the scene end, whichever is first (`hold_sec` may push the last two later); when `until` or the
  scene end cuts the timeline short the group fades out over 0.25 s.
- **Output**: one group layer per overlay (`<scene>-overlay-<id>`, overlay order, caption lift) owning one layer per bound
  slot (`<scene>-overlay-<id>-<slot>`, drawn in slot order, first slot at the back) that exists while the slot is active,
  with `position_x`, `position_y`, `scale_x`, `scale_y`, `rotation_deg` and `opacity` animations. Segments driving one
  property alone keep their catalog curve; partial curves and combined channels are split into linear keyframes within a
  small tolerance; activation gaps become opacity steps.
- **Diagnostics**: `unresolved_timeline` (a `library` reference was not inlined), `invalid_timeline` (the definition's
  own diagnostics with paths such as `scenes[0].overlays[1].timeline.definition.tracks[2].clips[0].start`, video
  restrictions, bindings, unknown cues; also at compile time when the moved cues make two clips of one track overlap,
  prefixed "With the cues moved to their moments"), `invalid_moment` for cue moments and `invalid_overlay` for an overlay
  with both a graphic and a timeline. Messages name the fix (for example the binding an unbound slot needs).
- **Capabilities**: `composition capabilities` publishes `timeline: {format, slot_kinds, track_kinds, value_channels,
  curves, default_text_size, default_media_size, doc}`.

<!-- overlay-timeline-example -->
```json
{
  "id": "contrast", "region": "top", "text": "LEGALIZAR ≠ TRATAR",
  "timeline": {
    "definition": {
      "id": "two_terms", "duration": 2.6,
      "slots": [
        { "name": "term_a", "kind": "text" }, { "name": "sep", "kind": "text" },
        { "name": "term_b", "kind": "text" }, { "name": "strike", "kind": "rect" }
      ],
      "cues": [ { "name": "a", "time": 0.1 }, { "name": "b", "time": 1.2 } ],
      "tracks": [
        { "kind": "activation", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 2.5 } ] },
        { "kind": "transform", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 2.5,
          "position": [ { "time": 0, "value": [0, -0.3, 0] } ],
          "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },
        { "kind": "activation", "slot": "sep", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 1.6 } ] },
        { "kind": "value", "slot": "sep", "channel": "opacity", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 0.2,
          "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },
        { "kind": "activation", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 1.4 } ] },
        { "kind": "transform", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 1.4,
          "position": [ { "time": 0, "value": [0, 0.3, 0] } ],
          "rotation": [ { "time": 0, "value": [0, 0, -6], "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": [0, 0, 0] } ] } ] },
        { "kind": "activation", "slot": "strike", "clips": [ { "start": { "cue": "b", "offset": 0.6 }, "duration": 0.8 } ] },
        { "kind": "transform", "slot": "strike", "clips": [ { "start": { "cue": "b", "offset": 0.6 }, "duration": 0.8,
          "position": [ { "time": 0, "value": [0, 0.3, 0] } ] } ] },
        { "kind": "value", "slot": "strike", "channel": "reveal", "clips": [ { "start": { "cue": "b", "offset": 0.6 }, "duration": 0.3,
          "keyframes": [ { "time": 0, "value": 0, "curve": "ease_out_cubic.v1" }, { "time": 0.3, "value": 1 } ] } ] }
      ]
    },
    "bindings": {
      "term_a": { "text": "LEGALIZAR" },
      "sep": { "text": "≠", "size": 0.12, "color": "#FFD400" },
      "term_b": { "text": "TRATAR" },
      "strike": { "rect": { "color": "#FF3030", "height": 0.035, "match": "term_b" } }
    },
    "cues": { "a": { "word": "legalizar" }, "b": { "word": "tratar" } }
  }
}
```

`LEGALIZAR` pops in on its word at the top of the box, the `≠` fades in at the center just before `TRATAR`, which tilts
into place below it, and a red bar strikes `TRATAR` 0.6 s after it is spoken. Code: `VideoTimelineValidator`,
`VideoTimelineCompiler` (`helengine.video`, which references the tools-only `helengine.timeline`); the cue move is
`TimelineFlattener.Flatten(timeline, resolver, cueTimes, tolerance)`. Spec:
`docs/superpowers/specs/2026-10-09-helengine-timeline-design.md`.

## Flux Studio editing flow

Flux Studio stores one video edit per video (`video_edits` plus `video_edit_versions`). Every read and render composes a fresh
edit from the script, takes, images, caption style and the AI proposal (plugin node `sloganVideoEditCompose`) and applies the
stored human locks (`SloganVideoEditMerge`); the first read migrates old editorial-plan human overrides into locks.
`GET/PATCH /api/videos/{id}/video-edit` read the edit and lock (object value) or unlock (`null`) whole objects: `take`,
`duration`, `entry`, `voice`, `layer:<id>`, `motion:<id>`, `overlay:<id>`, `track:<id>`, `caption:<scene>:<cue>`. Render
manifests carry the stored edit (up to 16 MiB) and each render writes `video-edit.json` beside `composition.json`. The plugin
option `editFormat` (`video` by default, `legacy`) can switch back to the previous composition builder while edits are checked.
