# VideoTextureAsset Editor Design

## Goal

Add first-class `VideoTextureAsset` support to the HelEngine Windows editor so authored `.mp4` files can be referenced anywhere a texture is currently used, autoplay in a loop, and render through a GPU-resident DirectX11 path without CPU frame upload on every frame.

## Scope

### In scope

- New authored `VideoTextureAsset` type backed by `.mp4` source files.
- Windows editor support only.
- H.264 `.mp4` only for the first milestone.
- Video-only playback with autoplay and looping.
- Runtime resolution of video assets into `RuntimeTexture` instances that can be consumed by sprites, material texture bindings, preview panels, and scene loading paths.
- Native FFmpeg-backed decoder that interoperates with Direct3D 11 surfaces.

### Out of scope

- Audio decode, audio sync, mute controls, or transport UI.
- Build packaging, cooked asset output, or player/runtime shipping support outside the editor.
- Vulkan backend support.
- Arbitrary container or codec support beyond the initial `.mp4` target.
- Timeline scrubbing, pause/play, editor transport widgets, or timeline authoring.

## Constraints And Existing Repo Facts

- `helengine.directx11.video` already exists as a managed wrapper around a missing native library named `helengine.video.ffmpeg`.
- The current wrapper expects native `ID3D11Texture2D` handles and already models stream info, frame format, frame lifetime, and hardware mode.
- No native `helengine.video.ffmpeg` implementation is present in this checkout.
- Existing HelEngine rendering code samples engine-owned `RuntimeTexture` instances, with the DirectX11 backend expecting a `Texture2D` plus `ShaderResourceView`.
- Existing editor/runtime scene resolution flows already route texture references through `RuntimeTexture`, so video should fit into that seam rather than adding a bespoke component-only path.

## User-Facing Design

### Authored asset model

`VideoTextureAsset` is a new authored asset type that sits beside `TextureAsset`, not under it and not as a special scene component. A source `.mp4` file will be imported into lightweight asset metadata that preserves:

- Stable asset id.
- Source-relative path.
- Width and height.
- Frame rate.
- Duration.
- Looping/autoplay capability flags for the first milestone.
- Import diagnostics and compatibility metadata.

The authored source remains the `.mp4`; the generated asset record exists so the editor can treat video as a typed texture-like asset and persist references consistently.

### Authoring experience

- `.mp4` appears in the asset browser as a video texture asset.
- Any property picker or scene field that currently accepts a texture asset reference should also accept `VideoTextureAsset`.
- No dedicated video component is required for the initial milestone.
- Users assign video exactly where they would assign a normal texture.
- In the editor, resolved video assets begin playing automatically and loop indefinitely.

## Architecture

The feature is divided into five layers:

1. Asset model and import settings.
2. Editor asset import and browsing integration.
3. Scene and material resolution.
4. Managed DirectX11 video runtime objects.
5. Native FFmpeg + Direct3D11 decoder backend.

### 1. Asset model and import settings

Add a new asset type and sidecar settings model:

- `VideoTextureAsset`
- `VideoTextureAssetImportSettings`
- Optional `VideoTextureAssetProcessorSettings` only if later milestones need transcoding or validation knobs

The first milestone should keep the imported asset lightweight. It does not need to serialize decoded frames or packaged output. It only needs enough metadata to support editor browsing, diagnostics, and runtime opening of the source file.

### 2. Editor asset import and browsing integration

The asset importer layer should register a dedicated video importer for `.mp4`. The importer performs:

- Extension validation.
- Container/codec probing.
- Metadata extraction.
- Import diagnostics.
- Production of a `VideoTextureAsset` cache record.

Asset-browser and picker infrastructure should classify video assets as texture-compatible so that existing UI paths can show them anywhere texture references are allowed.

### 3. Scene and material resolution

Scene serialization should continue to persist a normal asset reference. The difference is in the resolver:

- If the reference resolves to a `TextureAsset`, build a normal static `RuntimeTexture`.
- If the reference resolves to a `VideoTextureAsset`, build a runtime video texture object.

This keeps scene persistence stable while allowing runtime behavior to differ behind the same texture-facing abstraction.

### 4. Managed DirectX11 video runtime objects

Add a DirectX11 runtime texture subtype that owns playback state for one video asset while still presenting as a normal `RuntimeTexture` to the rest of the engine.

Recommended responsibilities:

- `DirectX11VideoTextureResource`
  - Inherits from `RuntimeTexture`.
  - Owns the shader-readable destination texture and SRV sampled by the renderer.
  - Owns or references one decoder session.
  - Exposes a per-frame update method that advances playback and refreshes the destination texture.

- `EditorVideoPlaybackService`
  - Tracks active runtime video textures for the editor session.
  - Updates them once per editor frame before render submission.
  - Removes disposed or unreachable entries.

The engine-facing texture object must keep stable identity for the life of the scene binding. Materials and sprites should not need a different texture object every frame.

### 5. Native FFmpeg + Direct3D11 decoder backend

The missing native library should be implemented as `helengine.video.ffmpeg` and should own:

- FFmpeg demuxing.
- Codec setup.
- D3D11 hardware device binding.
- Frame decode.
- Loop-on-end behavior.
- Native frame lifetime and release.

The managed wrapper in `helengine.directx11.video` should be preserved and extended only where its current API is insufficient.

## Rendering Path

### Recommended path

Use native FFmpeg decode with Direct3D11-backed frames, then copy the decoded GPU surface into one persistent engine-owned shader-readable texture.

This is the recommended first version because it preserves renderer assumptions:

- The renderer continues sampling one engine-owned `RuntimeTexture`.
- The engine does not need to swap texture object identity per frame.
- The copy remains GPU-local.
- Decoder-surface lifetime stays contained inside the video runtime object.

### Why not CPU upload

CPU decode plus RGBA upload is intentionally rejected for the first milestone because it adds:

- CPU colorspace conversion cost.
- CPU-to-GPU upload every frame.
- Worse scrubbing and playback headroom later.
- A second implementation that would be replaced when GPU decode lands.

### Why not direct SRV swap as the first cut

Rebinding a new native texture directly into the engine resource every frame is possible, but it complicates:

- Shader resource lifetime.
- Decoder frame release timing.
- Synchronization between playback updates and draw submission.
- Reuse assumptions in existing DirectX11 renderer code.

The persistent destination texture plus GPU copy path is a safer first milestone while still meeting the performance goal.

## Data Flow

### Import-time flow

1. User drops or references an `.mp4` under project assets.
2. Video importer probes the file.
3. Importer rejects unsupported files with clear diagnostics.
4. Importer writes `VideoTextureAsset` metadata into the project cache.
5. Asset browser exposes the resulting item as a texture-compatible asset.

### Editor runtime flow

1. A scene field or material requests a texture reference.
2. Resolver loads either `TextureAsset` or `VideoTextureAsset`.
3. For video assets, the resolver opens a DirectX11 decoder session and creates a `DirectX11VideoTextureResource`.
4. The editor playback service registers that runtime texture.
5. Each editor frame, the playback service advances active video textures.
6. When a new decoded frame is available, the runtime video texture copies it into its persistent shader-readable texture.
7. Existing rendering code samples the texture normally.
8. On scene unload or texture disposal, decoder and GPU resources are released.

## Detailed Component Boundaries

### Core and asset layer

Expected additions:

- `engine/helengine.core/assets/raw/VideoTextureAsset.cs`
- `engine/helengine.core/assets/RuntimeVideoTexture.cs` only if a shared non-backend-specific base is needed

`VideoTextureAsset` should contain metadata only. It should not own decoder state or native resources.

### Editor asset layer

Expected additions or changes:

- `engine/helengine.editor/managers/asset/VideoTextureAssetImportSettings.cs`
- `engine/helengine.editor/content/video/IVideoImporter.cs`
- `engine/helengine.editor/content/video/VideoImporterContentProcessor.cs`
- `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Asset browser entry typing and picker filters

The editor should reuse the current importer registration model rather than introduce a parallel asset-loading mechanism.

### Scene persistence and resolution

Expected changes:

- `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs` later, but out of first-milestone implementation scope for packaged builds
- Texture-compatible persistence helpers and automatic component asset-reference support

The first milestone only needs editor-scene resolution. Player/runtime packaged resolution should remain explicitly unsupported for video.

### DirectX11 managed runtime

Expected additions:

- `engine/helengine.directx11/` additions for a video texture runtime resource
- `engine/helengine.directx11.video/` completion of managed decoder APIs
- Editor service to update active video textures each frame

The renderer should not get a separate "draw video" code path. It should continue consuming `RuntimeTexture`.

### Native backend

Recommended placement:

- `helengine-windows/native/helengine.video.ffmpeg/` or equivalent Windows-native folder with explicit build instructions and output path matching the managed P/Invoke name

The native backend should emit:

- Stream metadata on open.
- Decoder handle lifetime functions.
- Try-get-next-frame semantics.
- Seek and flush entry points, even if the editor UI does not expose them yet.

Retaining seek now avoids painting the API into a corner when scrubbing arrives later.

## Playback Semantics

For the initial milestone:

- Playback begins automatically when the video runtime texture is first resolved.
- Playback loops automatically at end-of-stream.
- Audio streams are ignored entirely.
- If the editor is paused in a future sense, that can become a later playback-service concern; it is not part of this milestone.

Frame advancement should use wall-clock editor time rather than "decode every update blindly." The playback service should advance toward the presentation timestamp of the next frame and avoid unnecessary decode churn when frames are still current.

## Failure Handling

Failures should surface at the asset boundary, not deep inside rendering.

### Import-time failures

Reject and report:

- Non-`.mp4` input for the first milestone.
- Unsupported codecs or profiles.
- Probe failures.
- Missing native decoder library during validation, if the importer depends on the same native backend.

### Runtime resolution failures

If video runtime creation fails:

- Emit one bounded error tied to the asset path/id.
- Return a stable placeholder texture instead of throwing from the render loop.
- Keep scene loading alive unless the caller explicitly requested fail-fast behavior.

### Mid-playback failures

If decode fails after startup:

- Preserve the last successfully copied frame when possible.
- Otherwise bind a placeholder texture.
- Log once and suppress per-frame spam.

## Performance Notes

The first milestone should optimize for stable GPU-local playback, not for universal codec coverage.

Performance-critical rules:

- No CPU frame upload path in the normal decode loop.
- No per-frame recreation of `RuntimeTexture`.
- No per-frame recreation of SRVs unless a device-lost or format-reset path requires it.
- Bounded frame queue depth to avoid unbounded latency.
- Prefer one decoder session per active runtime video texture.

The first milestone does not need multi-video global scheduling sophistication unless profiling proves it necessary.

## Testing Strategy

### Managed/editor tests

Add tests for:

- `VideoTextureAsset` serialization and metadata defaults.
- Import settings persistence.
- Asset-browser classification as texture-compatible.
- Resolver branch selection between static texture and video texture assets.
- Scene/component/material persistence compatibility for video asset references.
- Runtime video texture disposal and ownership behavior.

### Native integration tests

Add tests or harness coverage for:

- Open valid H.264 `.mp4`.
- Reject unsupported input cleanly.
- Decode first frame successfully.
- Loop at end-of-stream.
- Release frame and decoder resources correctly.
- Validate GPU-copy path into a shader-readable D3D11 texture.

### End-to-end editor verification

Manual or harness-driven verification should confirm:

1. Import one `.mp4`.
2. Assign it where a normal texture is accepted.
3. Load the scene in the Windows editor.
4. Observe autoplay looping without special-case scene logic.
5. Confirm scene unload/disposal releases the decoder and texture resources cleanly.

## Rollout Plan

### Milestone 1

- Windows editor only.
- `.mp4` only.
- H.264 only.
- Autoplay loop only.
- No build packaging.
- No audio.

### Later milestones

- Packaging/runtime player support.
- Additional containers and codecs.
- Scrubbing and transport controls.
- Audio pipeline.
- Vulkan or cross-backend support.
- Smarter shared decode/session policies.

## Open Design Decisions Resolved Here

- Asset type is `VideoTextureAsset`, not `MediaTextureAsset`.
- Video behaves like a texture source, not a dedicated scene-only component.
- First target format is `.mp4`.
- First playback behavior is autoplay looping.
- Audio is ignored.
- Initial backend is Windows DirectX11 editor only.
- Decode path is native FFmpeg with Direct3D11 surfaces and GPU-local copy into a persistent engine-owned texture.

## Implementation Guidance

When implementation begins, it should preserve these rules:

- Extend current texture-facing seams instead of introducing parallel rendering concepts.
- Keep native decoder concerns behind `helengine.directx11.video` and the native DLL boundary.
- Keep renderer sampling unchanged wherever possible.
- Fail clearly and locally.
- Do not add build/publishing scope until editor playback is solid.
