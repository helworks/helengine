# Editor Play button with a separate C# player — design

Date: 2026-10-13
Status: approved in brainstorming, pending spec review

## Goal

Add a **Play** button to the editor that runs the game in a **separate C# process** (`helengine.player.exe`), so that:

- game C# scripts can be debugged with a normal .NET debugger (Visual Studio attach / launch), with `.pdb` breakpoints;
- resetting is trivial: Play kills the previous player process and starts a fresh one; editor state is never touched by gameplay;
- the player loads **content cooked for the selected console platform** — the same cooked assets the platform build ships — decoded on PC and rendered with DX11.

The first platform is **PS2**. Other platforms migrate one at a time by shipping their own decoder; the engine-side player stays platform-agnostic.

## Decisions taken

| Question | Decision |
|---|---|
| Player process | New engine-owned WinExe `helengine.player` (net9, DX11 from `helengine.directx11`), separate from the editor. |
| Content source | Cooked files on disk, produced by the **same cook as the platform build**. No IPC/streaming of content. |
| Which cook | The **platform selected in the editor** (option "2"), starting with PS2. |
| Cook cost | New **assets-only** build mode that stops after staging (no ELF, no ISO). Already-cooked, unchanged assets are **not re-cooked**. |
| Platform knowledge | Lives in the platform repo. The decoder ships **inside the platform builder plugin** (`helengine.ps2.builder`). |
| Decoder output | Neutral CPU data (RGBA32 pixels, mesh arrays, material description). The player owns all GPU/DX11 work. |
| Unsaved scene | Play saves the open scene first; save failure aborts Play. |
| Script assemblies | Loaded from disk (`<project>/cache/generated_code/bin/<moduleId>`), runtime modules only, so the debugger finds `.pdb` files. |

## Architecture

```
Editor (helengine.editor)                         helengine.player.exe (separate process)
┌───────────────────────────────┐                 ┌──────────────────────────────────────┐
│ EditorViewport toolbar        │                 │ PlayerLaunchOptions (args)           │
│   Play / Stop buttons         │                 │ PlayerScriptModuleLoader (ALC)       │
│        │                      │                 │ platform decoder (from builder dll)  │
│ EditorPlaySessionService      │  Process.Start  │ PlayerRenderManager2D/3D             │
│   1. save scene               │ ──────────────► │   Build*FromCooked → decoder → DX11  │
│   2. assets-only cook         │  stdout/stderr  │ Core + HostFileSystemContentStream-  │
│   3. resolve decoder + launch │ ◄────────────── │   Source(<staging>/cooked)           │
│   4. track / kill process     │                 │ loads startup scene                  │
└───────────────────────────────┘                 └──────────────────────────────────────┘
          │ assets-only build mode                          ▲ reads
          ▼                                                 │
 Platform builder plugin (helengine-ps2/builder) ──► <working root>/ps2-staging/cooked/...
   Ps2PlatformAssetBuilder (stops after staging)
   Ps2PlayerAssetDecoder : IPlatformPlayerAssetDecoder
```

## Components

### Engine (`helengine`)

**`IPlatformPlayerAssetDecoder`** — `helengine.baseplatform`. Contract a platform implements to turn its cooked assets into neutral CPU data. It never touches GPU APIs.

- `DecodeTexture(string cookedAssetPath, IContentStreamSource source)` → RGBA32 pixels + width + height.
- `DecodeModel(string cookedAssetPath, IContentStreamSource source)` → positions, normals, texcoords, indices.
- `DecodeMaterial(string cookedAssetPath, IContentStreamSource source)` → base color, texture path, render class (opaque / alpha-test / transparent), double-sided, lighting mode.

The exact result types are named classes (no tuples), defined next to the interface.

**`PlatformPlayerCapability`** — `helengine.baseplatform`, declared on `PlatformDefinition`. Names the decoder type inside the platform builder assembly. Separate from `PlatformHostDebugCapability`, which keeps describing the native `ps2-host-debugger` runner.

**Assets-only build mode** — new `EditorBuildExecutionMode` value (working name `CookAssetsOnly`) passed to the builder. The builder runs cook/staging and returns before native code build, packaging and ISO. The result reports the staging cooked root (logical asset names, not the 8.3 `disc/` tree).

**`helengine.player`** — new WinExe project.

- `PlayerLaunchOptions` — parses `--content-root <dir>`, `--scene <logical path>`, `--platform-decoder <assembly path>` + `--platform-decoder-type <type name>`, `--script-modules <dir>`, `--wait-for-debugger`. Missing required options throw; no defaults are invented.
- `PlayerScriptModuleLoader` — loads the runtime script module assemblies into an `AssemblyLoadContext` and provides the `ScriptTypeResolver`.
- `PlayerRenderManager2D` / `PlayerRenderManager3D` — derive from the DX11 render managers and override `BuildTextureFromCooked`, `BuildModelFromCooked`, `BuildMaterialFromCooked` to call the decoder and build DX11 resources from its output.
- `PlayerHost` — creates the window, boots `Core` with `HostFileSystemContentStreamSource(contentRoot)`, the player render managers and an explicitly silent audio backend, then loads the requested scene.
- `--wait-for-debugger` blocks startup until a debugger is attached.

**`EditorPlaySessionService`** — `helengine.editor`, logic only (MVC).

1. Resolves the selected platform; Play is unavailable when it has no `PlatformPlayerCapability`.
2. Saves the open scene.
3. Runs the assets-only cook for that platform.
4. Resolves the decoder assembly path from the already-loaded platform builder plugin.
5. Kills a still-running previous player, launches the new one, redirects stdout/stderr into the editor console.
6. Stop kills the process; editor shutdown kills the process; an exited player returns the session to "stopped" and reports the exit code.

Process launching and the cook call sit behind small interfaces so the service is testable with fakes.

**Viewport toolbar** — Play and Stop buttons on `EditorViewport` (icons in `helengine.editor.app/content/icons/toolbar/`). The buttons only wire input to `EditorPlaySessionService` and reflect its state.

### PS2 (`helengine-ps2`)

**`Ps2PlayerAssetDecoder : IPlatformPlayerAssetDecoder`** — in `helengine.ps2.builder`.

- Texture: deserialize `Ps2TextureAsset` (`.ps2tex`) with `Ps2AssetSerializer`, decode with `Ps2NativeTextureCodec.Decode` (→ `Ps2GsTexturePixelCodec.DecodeToRgba32`). Supports the formats the PS2 cook emits: CT32, CT24, CT16(S), T8, T4 with CLUT16/CLUT16S palettes (and the T8H/T4HL/T4HH variants the codec handles). Depth formats throw.
- Model: deserialize `Ps2ModelAsset` (`.phm`) and return its indexed arrays (positions, normals, texcoords, 16-bit indices). The VU `PackedMeshBytes` blob is ignored.
- Material: deserialize `Ps2MaterialAsset` and map to the neutral description.

**`Ps2PlatformDefinitionFactory`** declares `PlatformPlayerCapability` pointing at `Ps2PlayerAssetDecoder`.

**`Ps2PlatformAssetBuilder.BuildAsync`** honors the assets-only mode by returning after texture/model/material staging (before workspace, native build and ISO steps).

**Incremental cook** — a second assets-only cook with no source changes must not re-cook or re-publish anything. If the existing `Ps2PlatformCookWorkItemExecutor` Prepare/Publish flow does not already skip unchanged work, add a content-hash skip there (verify first during implementation).

## Error handling

No silent fallbacks (no placeholder textures, no swallowed exceptions).

- Platform without `PlatformPlayerCapability`: Play button disabled with a tooltip naming the platform. Normal state, no exception.
- Scene save failure: Play aborts and reports it.
- Assets-only cook failure: reported in the console like any build failure; the player is not launched.
- Decoder meets an unsupported format: throws naming the asset path and format; the player exits non-zero; the editor shows exit code and stderr.
- Audio: explicitly silent backend in the first cut (PS2 audio cook format is still unknown).

## Testing

Host-runnable logic only; no assertions on source-file text.

- `helengine-ps2/builder.tests`: cook a T4, T8 and CT32 texture, a model and a material with the real cookers, decode through `Ps2PlayerAssetDecoder`, compare against the source data (RGBA pixels after quantization, mesh arrays, material fields). Unsupported-format case throws.
- `helengine-ps2/builder.tests`: assets-only mode returns before the native step; a second unchanged cook publishes nothing.
- Engine: `PlayerLaunchOptions` parsing (including missing-option failures).
- Editor: `EditorPlaySessionService` with fake launcher and cook — Play launches; Play while running kills then relaunches; Stop kills; non-zero exit is reported; platform without capability is unavailable; save/cook failures do not launch.
- Manual: Play on the demodisc `cube_test` scene with PS2 selected. Ask Helena before launching the editor or the player.

## Out of scope (first cut)

- PS2 audio decoding.
- Material fidelity beyond the neutral description (GS blending modes, VU lighting).
- Embedding the player window inside the editor viewport.
- Editor↔player IPC (live scene reload, remote commands).
- Platforms other than PS2.
