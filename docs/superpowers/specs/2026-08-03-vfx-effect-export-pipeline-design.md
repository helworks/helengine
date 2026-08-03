# VFX Effect & Export Pipeline Design

## Goal

Add a batch VFX pipeline to the engine that takes a pair of synced EXR image sequences (source color + alpha mask), runs a GPU shader-based effect over every frame, and writes the result out as a new EXR sequence. The first effect, `RainbowExpand`, hue-cycles a greenscreen-keyed subject while scaling it up from frame center, compositing over a solid background. The pipeline is built so additional effects can be added later by writing a new HLSL shader plus a small registration class, and so the same effect shaders can eventually run live inside the engine's real-time post-process chain without rewriting them.

## Scope

### In scope

- A new float/HDR raw asset type (`FloatImageAsset`) so linear EXR color data survives the pipeline without being truncated to 8-bit.
- EXR sequence discovery, reading, and writing (source, mask, and output).
- A clip abstraction pairing a source sequence with a mask sequence, with validation that frame counts and resolutions match.
- An effect abstraction (`IVfxEffect`) backed by HLSL shaders compiled through the existing `helengine.shader.compilation` pipeline, with a small registry so new effects can be added by dropping in a shader + registration class.
- A headless DirectX11 execution path: a windowless `Device`, one shared full-screen-triangle vertex shader, per-effect pixel shaders, GPU readback to CPU per frame.
- The first effect, `RainbowExpand`: mask-alpha-driven compositing (no chroma-key math — the mask is supplied externally), hue rotation over time, scale-from-center expansion with a small fixed set of easing curves, solid background color.
- A thin CLI (`helengine.vfx.cli`) that drives an export end to end: two input EXR folders in, one EXR folder out.

### Out of scope

- Video muxing/encoding (h.264, mp4, or any container/codec work). Output is a frame sequence only.
- Chroma-keying / greenscreen matte generation. The mask is supplied by an external tool; this pipeline only consumes it.
- Any editor UI (asset browser entries, preview panels, property pickers). This is a library + CLI tool for now.
- Alpha-channel output / transparent compositing. Output frames are opaque, composited over a solid background.
- Audio of any kind.
- Live, real-time use of these effects inside `DirectX11PostProcessChain` (the shaders are written to make that possible later, but wiring them into the live renderer is not part of this work).
- A general-purpose curve/keyframe editor for effect parameters. Easing is a small fixed enum (Linear, EaseIn, EaseOut, EaseInOut).

## Constraints And Existing Repo Facts

- The engine's asset pipeline follows a consistent raw asset → `IContentProcessor` → runtime asset pattern (`engine/helengine.core/assets/raw/`, `engine/helengine.core/content/`). `TextureAsset` stores only 8-bit RGBA (`byte[] Colors`), so it cannot carry EXR's float data — a new raw asset type is needed rather than reusing `TextureAsset`.
- EXR import already exists for single stills via `MagickTextureImporter` (`engine/helengine.editor.windows/content/textures/MagickTextureImporter.cs`), but it uses `Magick.NET-Q8-AnyCPU`, an 8-bit-quantum build that cannot preserve float/HDR precision. This pipeline needs the **Q16-HDRI** package variant instead; the two are not interchangeable and this is a new dependency, not a reuse of the existing one.
- There is no existing concept of an image sequence (numbered frames as one clip) anywhere in the codebase — this is net new.
- `engine/helengine.shader` and `engine/helengine.shader.compilation` already form a real shader contract/compiler split (see `docs/superpowers/plans/2026-07-31-shader-runtime-compilation-project-boundary.md`). This pipeline should compile its effect HLSL through the existing `helengine.shader.compilation` entrypoint rather than adding a second shader-compilation path.
- `engine/helengine.directx11/DirectX11Renderer3D.cs` already creates its `Device` independently of any swap chain (swap chains are attached to surfaces afterward). This confirms a windowless device — created with no window handle and no swap chain, used purely to run compute/pixel passes and read back results — fits the existing device lifecycle model and needs no new device-creation pattern.
- `engine/helengine.directx11/rendering/DirectX11PostProcessChain.cs` is a real but currently fixed/hardcoded list of post-process passes (tonemap, bloom, fxaa). It is a plausible future home for these effects as live passes, but today it is not a pluggable registry — this design does not change that file.
- Video *decode* support (`engine/helengine.directx11.video`) exists as a managed wrapper around a native `helengine.video.ffmpeg` library that is not present in this checkout, and there is no video *encode* support anywhere. "We already have video support" refers to that in-progress decode/playback path, not export — this pipeline does not depend on it and does not need it, since muxing is out of scope here.

## Architecture

The feature is divided into four projects, each with one clear responsibility:

1. **`engine/helengine.vfx`** — engine-agnostic contracts. No GPU dependency, no image-library dependency.
2. **`engine/helengine.vfx.io`** — EXR sequence discovery and read/write, isolated so the Magick.NET dependency doesn't leak into the contracts or GPU projects.
3. **`engine/helengine.vfx.directx11`** — headless GPU execution of effects.
4. **`engine/helengine.vfx.cli`** — thin console app wiring the above together.

Plus one addition to the core asset layer:

- **`engine/helengine.core/assets/raw/FloatImageAsset.cs`** — a float-precision RGBA image (`Width`, `Height`, `float[] Pixels`), mirroring the shape of `TextureAsset` but at HDR precision. It derives from the same `Asset` base as other raw assets, following the existing convention, even though no `IContentProcessor`/serialization registration is added for it in this milestone — that seam is left ready for a future editor importer rather than built out now.

### 1. `helengine.vfx` (contracts)

- `ImageSequence` — an ordered, sorted list of frame file paths, plus resolution and optional frame-rate metadata (frame rate is carried through only as informational metadata for a future muxing step; it is not needed to compute effect timing, since timing is normalized frame-index progress through the clip).
- `VfxClip` — pairs a source `ImageSequence` and a mask `ImageSequence`. Validates at construction that both have the same frame count and resolution, throwing a clear, specific exception naming the mismatch if not (this is a real, expected failure mode: the two sequences come from independent external tools, not from a single trusted internal source).
- `VfxEffectParameterDescriptor` — name, value type (Float, Color, Int), default value, and (for Float) min/max, used both to validate CLI `--param` input and to map named parameters onto shared constant-buffer slots.
- `IVfxEffect` — id, display name, parameter descriptor list, and a path to its HLSL source.
- `VfxEffectRegistry` — a static id → `IVfxEffect` map, populated by built-in effect registrations at startup. Mirrors the existing `ContentProcessorRegistration` pattern: adding a new effect means adding one registration, not touching the registry's own code.
- `VfxExportRequest` — a clip, an effect id, resolved parameter values, and an output folder/filename pattern. The single value the CLI builds and hands to the runner.

To keep the constant-buffer layout simple for v1 (one effect) without over-building a dynamic layout system, every effect shares one fixed cbuffer shape: normalized time, resolution, and a small fixed array of parameter floats (enough slots for `RainbowExpand`'s five parameters plus headroom). Effects read whichever slots their own parameter descriptors map to, by index.

### 2. `helengine.vfx.io`

- `ExrSequenceReader` — given a folder, finds `*.exr` files, sorts them numerically by the frame index in the filename (e.g. `frame.0001.exr`), and builds an `ImageSequence` from the paths without loading pixel data up front (frames are loaded lazily, one at a time, during the export run — a clip's frames are never all held in memory at once).
- `ExrFrameReader` / `ExrFrameWriter` — read/write a single `FloatImageAsset` to/from an `.exr` file, backed by `Magick.NET-Q16-HDRI-AnyCPU`.

**Open risk, called out explicitly:** this is the one piece of the design not yet validated against real EXR files. Magick.NET's Q16-HDRI build stores samples as 16-bit half-floats internally, which matches EXR's common "half" channel format, so it's expected to round-trip cleanly for this pipeline's needs (single-layer RGBA float frames, no multilayer/cryptomatte channels). This should be confirmed against real exported EXR frames early in implementation, before building the rest of the pipeline on top of it.

### 3. `helengine.vfx.directx11`

- `DirectX11VfxDevice` — creates a `SharpDX.Direct3D11.Device` with no window handle and no swap chain, exposing the immediate context. One instance per export run.
- `DirectX11VfxEffectRunner` — given a `VfxClip`, an `IVfxEffect`, and resolved parameters:
  1. Compiles the effect's pixel shader once via `helengine.shader.compilation`, alongside one shared built-in full-screen-triangle vertex shader (all effects here are full-frame image effects; no effect needs its own vertex shader).
  2. For each frame index `i` in `[0, clip.FrameCount)`:
     - Loads source frame `i` and mask frame `i` (via `helengine.vfx.io`), uploads each as an `R32G32B32A32_Float` texture + SRV.
     - Computes `normalizedTime = i / (frameCount - 1)` and updates the shared constant buffer with time, resolution, and the effect's resolved parameter values.
     - Binds source + mask SRVs and a linear-clamp sampler, sets an offscreen float render target sized to the source resolution, draws the full-screen triangle.
     - Copies the render target to a staging texture, maps it, and reads the float pixels back into a `FloatImageAsset`.
     - Hands the result to `ExrFrameWriter` to write `frame.{i:D4}.exr` into the output folder.
  3. Releases all GPU resources at the end of the run.

### 4. `helengine.vfx.cli`

Parses arguments, builds a `VfxClip` from two input folders, resolves the effect and parameters from the registry, runs `DirectX11VfxEffectRunner`, and reports progress/errors to the console.

```
helengine.vfx.cli
  --source <folder>       EXR sequence folder (source color)
  --mask <folder>         EXR sequence folder (alpha mask; A channel is used)
  --effect <id>           e.g. rainbow-expand
  --out <folder>          output EXR sequence folder
  --param <name>=<value>  repeatable; sets one effect parameter
```

## The First Effect: RainbowExpand

Parameters (`VfxEffectParameterDescriptor` list):

| Name | Type | Meaning |
|---|---|---|
| `HueCyclesPerClip` | Float | Number of full 360° hue rotations applied across the whole clip |
| `StartScale` | Float | Uniform scale factor at the start of the clip |
| `EndScale` | Float | Uniform scale factor at the end of the clip |
| `Easing` | Int (enum) | `Linear`, `EaseIn`, `EaseOut`, `EaseInOut` |
| `BackgroundColor` | Color | Solid background the subject is composited over |

Per-pixel shader logic:

1. Compute eased `t` from `normalizedTime` and `Easing`.
2. `scale = lerp(StartScale, EndScale, t)`.
3. Sample source/mask at the inverse-scaled UV, centered on frame middle: `uv' = (uv - 0.5) / scale + 0.5`. If `uv'` falls outside `[0,1]`, treat as fully transparent (background shows through) rather than sampling.
4. `alpha = mask.a` sampled at `uv'` (0 if out of bounds).
5. Sample `source.rgb` at `uv'`, convert to HSV, rotate hue by `360° * HueCyclesPerClip * t` (wrapping), convert back to RGB.
6. `output.rgb = lerp(BackgroundColor, huedRgb, alpha)`, `output.a = 1` (opaque — no alpha in the exported frame, per scope).

No chroma-key math is needed anywhere in this shader: the mask sequence already supplies alpha directly, since it comes from an external keying tool.

## Data Flow

1. User exports/produces two EXR sequences externally: source color and an alpha mask (from their keying tool of choice).
2. CLI builds an `ImageSequence` for each folder via `ExrSequenceReader`, then a `VfxClip` pairing them — failing immediately with a specific error if frame counts or resolutions don't match.
3. CLI resolves the requested effect id from `VfxEffectRegistry` and parses `--param` values against that effect's parameter descriptors, failing with a listing of valid names/ranges on any unknown or invalid value.
4. `DirectX11VfxEffectRunner` compiles the effect shader once, then iterates every frame: load → upload → run pass → read back → write EXR frame.
5. CLI reports the output folder and frame count on success.

## Failure Handling

Failures should surface clearly at the two real system boundaries: the user-supplied input sequences, and the user-supplied effect/parameter selection. Everything else is internal engine machinery and is allowed to fail with a raw exception/stack trace for this CLI-only milestone.

- **Clip construction:** mismatched frame counts or resolutions between source and mask → immediate, specific error identifying which sequence and what mismatched.
- **Effect/parameter resolution:** unknown effect id, unknown parameter name, or out-of-range parameter value → immediate error listing the valid effect ids or that effect's valid parameters/ranges.
- **GPU/device/shader-compile failures:** allowed to surface as an unhandled exception. This is a CLI tool run by a developer, not a live render loop that needs to degrade gracefully — there is no placeholder-texture concern here the way there is for the video-texture live-playback design.

## Testing Strategy

- **Unit tests** (new `helengine.vfx.tests`, following the existing per-project test-project convention): `VfxClip` validation (mismatched frame counts/resolutions throw with the right message), `VfxEffectRegistry` lookup (known/unknown ids), easing-curve math (pure functions, no GPU involved), parameter parsing/validation (`--param` string → typed value, range checks).
- **EXR I/O smoke test:** round-trip a small synthetic `FloatImageAsset` through `ExrFrameWriter` → `ExrFrameReader` and assert pixel values survive within a small float tolerance. This directly exercises the open Magick.NET Q16-HDRI risk called out above.
- **End-to-end structural test:** a small checked-in fixture (2-3 frame synthetic source + mask EXR sequences) run through the full CLI pipeline, asserting the right number of output files exist with the right resolution and non-degenerate pixel data (not asserting exact pixel values, which would be fragile). Run manually or in CI if a GPU device is available in that environment.

## Rollout Plan

### Milestone 1 (this design)

- `FloatImageAsset` raw asset type.
- `helengine.vfx`, `helengine.vfx.io`, `helengine.vfx.directx11`, `helengine.vfx.cli` projects.
- `RainbowExpand` effect, driven entirely by CLI.
- EXR sequence in, EXR sequence out. No muxing.

### Later milestones (explicitly out of scope here)

- Video muxing/encoding of the output frame sequence (e.g. via ffmpeg).
- Editor UI: asset browser entries, live preview, property pickers for effect parameters.
- Wiring effect shaders into `DirectX11PostProcessChain` as live, real-time passes.
- Additional effects beyond `RainbowExpand`.
- Chroma-key/matte generation inside the engine (currently done externally).

## Open Design Decisions Resolved Here

- Input is two independently-produced EXR sequences (source + mask); this pipeline does no chroma-keying itself.
- A new `FloatImageAsset` raw type is added rather than extending `TextureAsset`, to avoid disturbing existing 8-bit texture consumers.
- Effects are authored as HLSL compiled through the existing `helengine.shader.compilation` pipeline, not as C# per-pixel processors, so they can later run live in the engine's post-process chain unchanged.
- Output is opaque (composited over a solid background), not alpha-carrying.
- Easing is a small fixed enum, not a general keyframe/curve system.
- Muxing to a video container is fully out of scope for this milestone; output is a frame sequence only.
- GPU execution is a windowless `Device` reused from the existing device-creation pattern in `helengine.directx11`, not a new device abstraction.

## Implementation Guidance

When implementation begins, it should preserve these rules:

- Keep the four `helengine.vfx*` projects' boundaries clean: contracts have no GPU/image-library dependency, I/O has no GPU dependency, GPU execution has no CLI-argument-parsing concerns.
- Confirm the Magick.NET Q16-HDRI EXR round-trip early (see Open Risk above) before building the rest of the pipeline on top of it — if it doesn't hold up, that's a design-affecting finding, not a minor bug.
- Do not add editor UI, muxing, or chroma-key logic in this milestone — those are explicitly deferred.
- Reuse `helengine.shader.compilation`'s existing HLSL compile entrypoint rather than adding a second compilation path.
