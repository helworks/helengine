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

Editorial `cortex.edit.decisions.v2` is additive: catalog pin, transforms/keyframes, registered effects, masks, transitions and audio. V1 remains readable and is not automatically converted; an explicit advanced manual edit upgrades it. Human overrides survive replanning. Final compilation requires current resolved speech and intervals. When the script file has different whitespace/newlines than canonical semantic JSON, pass `LoadedEditScript.Reference` to the compiler overload as the actual current byte fingerprint. The host must validate current edit-plan/recording-task references before final compilation.

Depois do Slogan renders available selected recordings and owned images; absent recordings stay silent. Optional audio material uploads accept up to128MiB and use the existing authenticated project access. Rendering/manual editing do not invoke a model; drafting/replanning keep existing spending confirmations. A completed preview is published only after transactional comparison of script/version, editorial plan/focus, selected recording state and media byte hashes. Stale completion is discarded and reported as requiring a new preview. Old saved previews default to `has_audio=false`.

Validation rejects unknown versions/parameters, source/hash/path changes, invalid intervals and cyclic/shared group ownership before source/GPU allocation. A failed/canceled encoder never publishes a final output or receipt. Package generations and old plugin DLL directories are retained so existing processes are not overwritten.
