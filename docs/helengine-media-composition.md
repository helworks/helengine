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

## Video edit format (`helengine.video`)

`helengine.video.edit.v1` is the scene-based editing document compiled to compositions by `VideoEditCompiler`. Scenes take
their length from a take (`from_take`), a fixed length or an estimate; moments are seconds, seconds before the end, a
fraction, or a spoken word/phrase of the take. Layout and motion presets expand at compile time; entries are `cut`, `appear`
or a catalog transition. Global tracks carry music and caption settings; captions derive from take words (estimated from the
planned speech in previews). Any lockable object may carry `"by": "human"`; `VideoEditMerge.Replan` keeps locked objects when
the AI proposes again. Spec: `helengine/docs/superpowers/specs/2026-10-08-helengine-video-edit-format-design.md`.

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

## Flux Studio editing flow

Flux Studio stores one video edit per video (`video_edits` plus `video_edit_versions`). Every read and render composes a fresh
edit from the script, takes, images, caption style and the AI proposal (plugin node `sloganVideoEditCompose`) and applies the
stored human locks (`SloganVideoEditMerge`); the first read migrates old editorial-plan human overrides into locks.
`GET/PATCH /api/videos/{id}/video-edit` read the edit and lock (object value) or unlock (`null`) whole objects: `take`,
`duration`, `entry`, `voice`, `layer:<id>`, `motion:<id>`, `overlay:<id>`, `track:<id>`, `caption:<scene>:<cue>`. Render
manifests carry the stored edit (up to 16 MiB) and each render writes `video-edit.json` beside `composition.json`. The plugin
option `editFormat` (`video` by default, `legacy`) can switch back to the previous composition builder while edits are checked.
