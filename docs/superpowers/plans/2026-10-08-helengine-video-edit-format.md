# helengine.video Edit Format Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `engine/helengine.video`: the scene-based video edit document (`helengine.video.edit.v1`), its JSON, schema, validator, moment resolution and a compiler to `helengine.media` compositions; then move Cortex and Flux Studio onto it.

**Architecture:** C# model classes are the source of truth; `VideoEditJson` (System.Text.Json, snake_case, strict) is the interchange; `VideoEditCompiler` turns a document into a `CompositionDocument` plus diagnostics. `helengine.media` gains `VisualLayer.HoldLastFrame` for transition overlaps without source handles.

**Tech Stack:** .NET 9 (engine), System.Text.Json, xUnit; Direct3D 11 WARP for GPU tests (`helengine.media.windows.tests`).

**Spec:** `docs/superpowers/specs/2026-10-08-helengine-video-edit-format-design.md`

## Global Constraints

- Schema id `helengine.video.edit.v1`; JSON is snake_case; unknown properties and duplicate keys are rejected.
- One class per file; XML comments on every member; PascalCase fields; no tuples; no nullable annotations; braces on the same line (engine AGENTS.md).
- Moments: `{sec}`, `{from_end}`, `{fraction}`, `{word, occurrence=1, offset_sec=0}`; track positions `{scene, at}`.
- Entry effects: `cut` (default), `appear` (opacity ramp), or any catalog effect whose category is `transition`; never on the first scene; never shortens the video.
- Lockable objects carry `"by": "human"`; `VideoEditMerge.Replan` keeps them from the current document.
- Final compilation refuses `estimate` durations and any `pending` diagnostic; previews compile with pending diagnostics reported.
- Build/test with `dotnet test <project> -nologo -v q` from `engine/`.

## Review Focus

- A word anchor whose word appears twice or not at all: `occurrence` picks the n-th; a missing word yields `anchor_unresolved` (pending) and resolves to scene start, never throws. → Task 3 tests.
- Moments that land outside the scene (`sec` > duration, `from_end` > duration): clamped into the scene with a `moment_clamped` warning, never negative time. → Task 3 tests.
- A transition entry longer than the scene, or on the first scene: `invalid_entry` error, document rejected by the validator. → Task 4 tests.
- A take whose `out_sec` exceeds the media duration or `in_sec >= out_sec`: `invalid_take` error. → Task 4 tests.
- Replanning a document where the AI deleted a locked layer: the locked layer is restored from the current document. → Task 5 tests.

---

## Stage 1 — model, JSON, schema, validator, moments

### Task 1: Project, model and JSON round trip

**Files:**
- Create: `engine/helengine.video/helengine.video.csproj` (net9.0, ProjectReference `../helengine.media/helengine.media.csproj`)
- Create model classes (one per file) in `engine/helengine.video/model/`: `VideoEdit`, `VideoFormat`, `VideoScriptReference`, `VideoCatalogReference`, `VideoMedia`, `VideoMediaAnalysis`, `VideoWord`, `VideoSilence`, `VideoScene`, `VideoSceneDuration`, `VideoTake`, `VideoEntry`, `VideoVoice`, `VideoLayer`, `VideoLayout`, `VideoViewport`, `VideoMotion`, `VideoFocus`, `VideoTransform`, `VideoAnimation`, `VideoKeyframe`, `VideoEffect`, `VideoMask`, `VideoOverlay`, `VideoMoment`, `VideoTrackMoment`, `VideoTracks`, `VideoAudioTrack`, `VideoEnvelope`, `VideoCaptionTrack`, `VideoCaptionOverride`, `VideoRational`
- Create: `engine/helengine.video/VideoEditJson.cs`
- Create: `engine/helengine.video.tests/helengine.video.tests.csproj` (xUnit like `helengine.media.tests`), `VideoEditJsonTests.cs`, `VideoEditSamples.cs` (shared sample document builder)

**Interfaces:**
- Produces: `VideoEditJson.Parse(string json) : VideoEdit` (throws `JsonException`/`InvalidDataException`), `VideoEditJson.Serialize(VideoEdit edit) : string`, `VideoEditJson.Options : JsonSerializerOptions`.
- Model property names map 1:1 to the spec JSON via snake_case naming policy. Lockable objects (`VideoTake`, `VideoSceneDuration`, `VideoEntry`, `VideoVoice`, `VideoLayer`, `VideoMotion`, `VideoOverlay`, `VideoAudioTrack`, `VideoCaptionOverride`) expose `string By`.
- `VideoMoment { double? Sec; double? FromEnd; double? Fraction; string Word; int Occurrence = 1; double OffsetSec; }` — exactly one of sec/from_end/fraction/word set (validated in Task 4). (`double?` is a value-type optional, allowed.)
- `VideoCaptionTrack.Style` is a `JsonElement` (caption style snapshot passed through to text layers).

- [ ] **Step 1: Write failing round-trip test** — `VideoEditSamples.TwoScenes()` builds a document with an image scene (fixed 3 s, inset layout, zoom motion), a take scene (`from_take`, push entry with `by: "human"`, overlay at word "lei"), a music track and a caption track. Test: `Serialize` → `Parse` → `Serialize` yields identical JSON; the JSON contains `"from_take"`, `"by":"human"`, `"word":"lei"`; parsing JSON with an unknown property or a duplicate key throws.
- [ ] **Step 2: Run** `dotnet test helengine.video.tests -nologo -v q` → FAIL (project missing).
- [ ] **Step 3: Implement** model + `VideoEditJson` with `JsonSerializerOptions { PropertyNamingPolicy = SnakeCaseLower, UnmappedMemberHandling = Disallow, AllowDuplicateProperties = false, DefaultIgnoreCondition = WhenWritingNull }`; `Parse` rejects a `schema` other than `helengine.video.edit.v1`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat: add helengine.video edit document model and JSON`.

### Task 2: JSON Schema generation

**Files:** Create `engine/helengine.video/VideoEditSchema.cs`; Test `VideoEditSchemaTests.cs`.

**Interfaces:** Produces `VideoEditSchema.Create() : JsonElement` (draft 2020-12, `additionalProperties:false` on every object, enums for `duration.mode`, `layout.preset`, `motion.preset`, `layer.kind`, `media.kind`, `envelope.type`, `"by"` enum `["human","ai"]`) built from `JsonSchemaExporter.GetJsonSchemaAsNode(VideoEditJson.Options, typeof(VideoEdit))` plus a post-pass that closes objects and injects the enums.

- [ ] **Step 1: Failing test** — schema has `properties.scenes.items.properties.duration.properties.mode.enum == ["from_take","fixed","estimate"]` and `additionalProperties == false` at the root.
- [ ] **Step 2-4:** implement, run, pass.
- [ ] **Step 5: Commit** `feat: generate the helengine.video edit JSON schema`.

### Task 3: Scene timeline and moment resolution

**Files:** Create `engine/helengine.video/VideoSceneTimeline.cs`, `VideoSceneSpan.cs`, `VideoMomentResolver.cs`, `VideoDiagnostic.cs`, `VideoDiagnosticSeverity.cs`; Test `VideoMomentResolverTests.cs`.

**Interfaces:**
- `VideoDiagnostic { string Code; string Scene; string Path; string Message; VideoDiagnosticSeverity Severity; }`, severities `Error | Pending | Warning`.
- `VideoSceneTimeline.Build(VideoEdit edit, List<VideoDiagnostic> diagnostics) : IReadOnlyList<VideoSceneSpan>`; `VideoSceneSpan { VideoScene Scene; MediaTime Start; MediaTime End; MediaTime Duration; bool Estimated; }`. Durations: `from_take` → `out - in`; `fixed`/`estimate` → `sec`; spans are contiguous (entries overlap the previous scene, they do not move starts).
- `VideoMomentResolver.Resolve(VideoEdit edit, VideoSceneSpan span, VideoMoment moment, string path, List<VideoDiagnostic> diagnostics) : MediaTime` (scene-local). Word: normalized (lowercase, strip punctuation/accents) match against the take media's `analysis.words` with `start_sec` inside `[in_sec, out_sec)`, n-th occurrence, local time = `word.start - in_sec + offset_sec`.

- [ ] **Step 1: Failing tests** — `sec`, `from_end`, `fraction` on a 4 s scene → 1.2/3.7/2.0; word "política" (analysis has "Política," at 13.0 with take in 12.3) → 0.7; `occurrence: 2`; missing word → `anchor_unresolved` pending and 0; `sec: 9` on a 4 s scene → clamped to 4 with `moment_clamped`; spans of `[fixed 3, from_take 3.75, estimate 2]` start at 0, 3, 6.75 and the last is `Estimated`.
- [ ] **Step 2-4:** implement, run, pass.
- [ ] **Step 5: Commit** `feat: resolve helengine.video scene spans and moments`.

### Task 4: Validator

**Files:** Create `engine/helengine.video/VideoEditValidator.cs`; Test `VideoEditValidatorTests.cs`.

**Interfaces:** `VideoEditValidator.Validate(VideoEdit edit, MediaCapabilities capabilities) : IReadOnlyList<VideoDiagnostic>` (structural `Error`s only).

Rules: schema id; unique ids (media, scenes, layers per scene, overlays, tracks); media references exist and have the right kind (take → video/audio, layer media → image/video); `invalid_take` (`in_sec < 0`, `out_sec <= in_sec`, `out_sec > duration_sec`); `invalid_duration` (mode/sec mismatch, `from_take` without take, sec ≤ 0); each moment has exactly one form and finite values (`invalid_moment`); `invalid_entry` (transition on the first scene, effect not `cut`/`appear`/catalog transition, parameters rejected by the catalog descriptor `Accepts`, `duration_sec` ≤ 0 or > scene duration when known, `audio_fade_sec` outside 0–2); layer presets known; viewport inside 0–1; layer effects must be catalog `layer` effects; animation properties/curves must be in the catalog; `by` is `human`, `ai` or absent; caption `words_per_cue` 1–16 and `words_per_line` 1–8.

- [ ] **Step 1: Failing tests** — the sample is valid; each rule above has a one-mutation test asserting its code.
- [ ] **Step 2-4:** implement, run, pass.
- [ ] **Step 5: Commit** `feat: validate helengine.video edit documents`.

### Task 5: Locks and replanning

**Files:** Create `engine/helengine.video/VideoEditMerge.cs`; Test `VideoEditMergeTests.cs`.

**Interfaces:** `VideoEditMerge.Replan(VideoEdit current, VideoEdit proposal) : VideoEdit` — returns a copy of `proposal` where every object locked (`by == "human"`) in `current` replaces the proposal's object with the same identity (scene id; layer/overlay/track id; scene-level objects take, duration, entry, voice; motion inside a locked-or-not layer; caption overrides by scene+cue). A locked layer missing from the proposal is re-inserted at its original index. Scenes missing from the proposal are kept from `current` only if they contain locked objects.

- [ ] **Step 1: Failing tests** — locked entry survives a proposal changing it; unlocked entry is replaced; locked layer deleted by the proposal is restored; locked caption override survives.
- [ ] **Step 2-4:** implement, run, pass.
- [ ] **Step 5: Commit** `feat: keep human-locked objects when replanning video edits`.

## Stage 2 — compiler

### Task 6: `helengine.media` last-frame hold

**Files:** Modify `engine/helengine.media/VisualLayer.cs` (add `public bool HoldLastFrame { get; set; }`), `engine/helengine.media/Schemas/helengine.media.composition.v1.schema.json` (`hold_last_frame` boolean), `TransitionValidator.cs` (skip the `source_interval` end check when `HoldLastFrame`), `CompositionEvaluator.cs` (clamp source time to `SourceOut - 1/frame_rate` when held); Test `helengine.media.tests/HoldLastFrameTests.cs`.

- [ ] **Step 1: Failing tests** — a video layer `[0,3)` with source `[0,2)` and `hold_last_frame` validates and evaluates source time `2 - 1/24` at t=2.5; without the flag it reports `source_interval`.
- [ ] **Step 2-4:** implement, run, pass (also run `helengine.media.windows.tests`).
- [ ] **Step 5: Commit** `feat: let composition video layers hold their last frame`.

### Task 7: Compiler — scenes, takes, media layers, presets

**Files:** Create `engine/helengine.video/VideoEditCompiler.cs`, `VideoCompileContext.cs`, `VideoCompileResult.cs`, `VideoLayoutPresets.cs`, `VideoMotionPresets.cs`; Test `VideoEditCompilerTests.cs`.

**Interfaces:**
- `VideoCompileContext { MediaCapabilities Capabilities; bool Final; }`.
- `VideoEditCompiler.Compile(VideoEdit edit, VideoCompileContext context) : VideoCompileResult { CompositionDocument Composition; IReadOnlyList<VideoDiagnostic> Diagnostics; }` — runs the validator first (errors → no composition).
- Output ids: group `scene-<scene>`, take layer `take-<scene>`, media layers `<scene>-<layer>`, voice clip `voice-<scene>`.
- Layout presets (`VideoLayoutPresets.Viewport(preset, width, height)`): `full_frame` (0,0,1,1), `inset` (0.08,0.2,0.84,0.6 vertical; 0.15,0.1,0.7,0.8 horizontal), `side_by_side` (0.52,0.1,0.44,0.8).
- Motion preset `zoom_to_focus` → `zoom` animation from `from_scale` to `to_scale` between resolved `start`/`end`, `focus_x/focus_y` constants from `focus` (`center` → 0.5).
- `appear` entry → opacity keyframes 0→1 over `duration_sec`.
- Final: `estimate` durations → `estimated_duration` pending.

- [ ] **Step 1: Failing tests** — compiling the sample yields contiguous groups, an inset viewport, a zoom animation with the resolved times, a voice clip with the take interval, and `CompositionValidator.Validate(result.Composition, caps)` is empty.
- [ ] **Step 2-4:** implement, run, pass.
- [ ] **Step 5: Commit** `feat: compile helengine.video scenes to compositions`.

### Task 8: Compiler — entries, overlays, captions, audio tracks

**Files:** Modify `VideoEditCompiler.cs`; Create `VideoCaptionBuilder.cs`, `VideoAudioBuilder.cs`; Test additions in `VideoEditCompilerTests.cs` and a GPU test `helengine.media.windows.tests/VideoEditGpuTests.cs` (references `helengine.video`).

Behaviour: transition entries extend the previous group and its members by `duration_sec`; a take layer without source handle gets `hold_last_frame` (no pending); `audio_fade_sec` adds equal-power envelopes to both voice clips. Overlays become text layers (`style` "graphic" uses the caption track style with graphic sizing) from `at` to `until`. Captions: words of each take inside its interval, grouped `words_per_cue`, cues as text layers `caption-<scene>-<n>` with word timings, overrides replace cue text; style = `tracks.captions.style`. Audio tracks resolve `start`/`end` track moments to global time, envelopes likewise. Final + take scene without analysis words → `caption_alignment` pending.

- [ ] **Step 1: Failing tests** — push entry produces a transition row and an extended previous group; previous take shorter than the overlap gets `hold_last_frame`; overlay at "lei" starts at the word; captions split 10 words into cues of 8+2; music track starting at scene 2 + 0.5 s lands at the right global time; GPU test renders the sample at mid-transition and checks a non-background pixel.
- [ ] **Step 2-4:** implement, run, pass.
- [ ] **Step 5: Commit** `feat: compile entries, overlays, captions and audio tracks`.

## Stage 3 — Cortex (outline)

### Task 9: AI nodes produce `VideoEdit`
Cortex references `helengine.video` (package/project reference via `HelengineRoot`), planner output type derived from `VideoEditSchema` with catalog effect ids injected, replanning goes through `VideoEditMerge.Replan`. Tests: a fake completion returning a proposal round-trips; locked objects kept.

## Stage 4 — Flux Studio (outline)

### Task 10: Storage and migration
`video_edits(video_id, revision, document_json)` + history; converter script + decisions v2 + recording analysis + caption settings → `VideoEdit`; previews and finals compile with `VideoEditCompiler`.

### Task 11: Editing UI on the new document
Editing plan UI reads/writes `VideoEdit` (locks via `by`), transition/caption/audio controls move to the new paths.

## Stage 5 — removal (outline)

### Task 12: Remove old formats
Delete `cortex.edit.decisions` v1/v2, `plan`, `resolved`, `recording_tasks`, `HelengineCompositionCompiler`, `SloganCompositionBuilder`, preview manifests and their tests once Flux Studio runs on `VideoEdit`.
