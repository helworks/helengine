# Editor Play button with a separate C# player — design

Date: 2026-10-13
Status: approved in brainstorming (revision 2 — desktop C# core, content-processor decoding)

## Goal

Add a **Play** button to the editor that runs the game in a **separate C# process** (`helengine.player.exe`), so that:

- game C# scripts can be debugged with a normal .NET debugger (Visual Studio attach / launch), with `.pdb` breakpoints;
- resetting is trivial: Play kills the previous player process and starts a fresh one; editor state is never touched by gameplay;
- the player loads **content cooked for the selected console platform** — the same cooked assets the platform build ships — decoded on PC and rendered with DX11.

The first platform is **PS2**. Other platforms migrate one at a time by shipping their own adapter; the engine-side player stays platform-agnostic.

## Decisions taken

| Question | Decision |
|---|---|
| Player process | New engine-owned WinExe `helengine.player` (net9.0-windows, DX11 from `helengine.directx11`), built once alongside the editor. |
| Engine build | The **desktop** `helengine.core` the editor uses. The engine is **never recompiled per platform**; the player uses the core's raw (non-`*_COOKED_PLATFORM_OWNED`) resolution paths. |
| Content source | Cooked files on disk, produced by the **same cook as the platform build**. No IPC/streaming of content. |
| Which cook | The **platform selected in the editor**, starting with PS2. |
| Cook cost | New **assets-only** build mode that stops after staging (no native build, no ISO). Unchanged assets are **not re-cooked**. |
| Platform-only C# | Game script modules are compiled for Play with the platform's **gameplay symbols** (`EditorPlatformPreprocessorSymbolService.ResolveGameplaySymbols(platformId, environmentId)`) into a per-platform output folder, so `#if`-gated platform code is included. Only script DLLs are rebuilt, never the engine. |
| Platform knowledge | Lives in the platform repo. The **player adapter** ships **inside the platform builder plugin** (`helengine.ps2.builder`). |
| How cooked assets are decoded | The adapter supplies **content processors** that turn platform-cooked texture/model files into the engine's generic `TextureAsset` / `ModelAsset`, plus a material decoder and a content alias map. The DX11 raw paths (`BuildTextureFromRaw`, `BuildModelFromRaw`) do the GPU work unchanged. |
| Materials | First cut: every platform material maps to **one standard DX11 built-in material** (base color + diffuse texture, alpha-test/transparent, double-sided). Platform-specific DX11 materials (e.g. a PS2 VU-style material) come later. |
| Unsaved scene | Play saves the open scene first; save failure aborts Play. |

## Architecture

```
Editor (helengine.editor)                         helengine.player.exe (separate process, desktop core)
┌───────────────────────────────┐                 ┌───────────────────────────────────────────┐
│ EditorViewport toolbar        │                 │ PlayerLaunchOptions (args)                │
│   Play / Stop buttons         │                 │ PlayerScriptModuleLoader (ALC + resolver) │
│        │                      │                 │ platform adapter (from builder dll)       │
│ EditorPlaySessionService      │  Process.Start  │   → content processors (tex/model)        │
│   1. save scene               │ ──────────────► │   → material decoder, alias map           │
│   2. assets-only cook         │  stdout/stderr  │ PlayerRenderManager3D (DX11)              │
│   3. build scripts w/ platform│ ◄────────────── │   BuildMaterialFromRawAsset → standard    │
│      gameplay symbols         │                 │ Core + AliasedContentStreamSource(        │
│   4. launch / track / kill    │                 │   HostFileSystemContentStreamSource)      │
└───────────────────────────────┘                 └───────────────────────────────────────────┘
          │ assets-only build mode                          ▲ reads
          ▼                                                 │
 Platform builder plugin (helengine-ps2/builder) ──► <working root>/ps2-staging/...
   Ps2PlatformAssetBuilder (stops after staging, skips unchanged)
   Ps2PlayerAdapter : IPlatformPlayerAdapter
```

## Components

### Engine (`helengine`)

**`IAssetContentManagerConfigurator`** + **`CoreInitializationOptions.AssetContentManagerConfigurator`** — `helengine.core`. Optional hook invoked by `Core.Initialize` immediately **before** `RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager`, so a host can register its own processors for the shared processor ids (`RegisterProcessorIfMissing` then keeps them). Null means no change in behavior.

**`IPlatformPlayerAdapter`** — `helengine.baseplatform`. Contract a platform builder plugin implements for Play:

- `void RegisterContentProcessors(ContentManager contentManager)` — registers processors for `RuntimeContentProcessorIds.TextureAsset` and `RuntimeContentProcessorIds.ModelAsset` that read the platform's cooked files and return generic `TextureAsset` (RGBA32) / `ModelAsset`.
- `PlayerMaterialDescription DecodeMaterial(Stream cookedMaterial)` — neutral material description (base color RGBA, logical diffuse texture path, render class, double-sided).
- `IReadOnlyDictionary<string, string> BuildContentAliases(string contentRootPath)` — logical path → staged path aliases (e.g. a model referenced as `.hasset` but staged as `.phm`).

**`PlatformPlayerCapability`** — `helengine.baseplatform`, declared on `PlatformDefinition`. Names the adapter type inside the builder assembly. Separate from `PlatformHostDebugCapability`.

**Assets-only build mode** — new `EditorBuildExecutionMode.CookAssetsOnly`, carried to the builder on `PlatformBuildRequest`. The builder runs cook/staging and returns before native build, packaging and ISO.

**Play script build** — compiles the project's runtime script modules with `ResolveGameplaySymbols(platformId, environmentId)` into `<project>/cache/generated_code/play/<platformId>/bin/<moduleId>/`, separate from the editor's hot-reload output.

**`helengine.player`** — new WinExe project in `helengine.ui/helengine.sln`.

- `PlayerLaunchOptions` — `--content-root`, `--scene`, `--platform-adapter-assembly`, `--platform-adapter-type`, `--script-modules`, `--wait-for-debugger`. Missing required options throw.
- `PlayerScriptModuleLoader` — loads runtime module assemblies and registers them on a `ScriptTypeResolver`.
- `PlayerRenderManager3D : DirectX11Renderer3D` — overrides `BuildMaterialFromRawAsset` to decode the platform material through the adapter and build the standard built-in material.
- `PlayerHost` — window, `Core` with `AliasedContentStreamSource(HostFileSystemContentStreamSource(contentRoot), adapterAliases)`, the adapter's processors through the configurator, no audio backend (Core tolerates a null `AudioManager`), loads the requested scene.

**`EditorPlaySessionService`** — `helengine.editor`, logic only (MVC): availability by `PlatformPlayerCapability`, save → assets-only cook → Play script build → resolve adapter from the loaded builder plugin → kill previous / launch / redirect output → Stop / editor shutdown kill → exit reporting. Launcher, cook and script build sit behind small interfaces for tests.

**Viewport toolbar** — Play and Stop buttons on `EditorViewport` wired to `EditorPlaySessionService`.

### PS2 (`helengine-ps2`)

**`Ps2PlayerAdapter : IPlatformPlayerAdapter`** in `helengine.ps2.builder`:

- Texture processor: `Ps2AssetSerializer.Deserialize` → `Ps2TextureAsset` → `Ps2NativeTextureCodec.Decode` → `TextureAsset { Colors, Width, Height, ColorFormat = Rgba32 }` (GS alpha 0..128 rescaled to 0..255 if the codec does not already).
- Model processor: `Ps2ModelAsset` indexed arrays → `ModelAsset` (Positions, Normals, TexCoords, Indices16, bounds, one submesh). `PackedMeshBytes` ignored.
- Material: `Ps2MaterialAsset` → `PlayerMaterialDescription`; the 8.3 disc texture path written by `Ps2MaterialCooker` is mapped back to the logical staged path.
- Aliases: `.hasset` model references → staged `.phm`.

**`Ps2PlatformDefinitionFactory`** declares `PlatformPlayerCapability`. **`Ps2PlatformAssetBuilder.BuildAsync`** honors assets-only mode and skips re-cooking unchanged items (the PS2 cook currently re-cooks everything; a fingerprint store under the staging root is added).

## Error handling

No silent fallbacks.

- Platform without `PlatformPlayerCapability`: Play disabled with a tooltip naming the platform.
- Scene save, cook or script build failure: reported in the console; player not launched.
- Unsupported cooked format: adapter throws naming asset path and format; player exits non-zero; editor shows exit code and stderr.
- Audio: none in the first cut.

## Testing

Host-runnable logic only; no assertions on source text.

- PS2 builder tests: cook → adapter decode round-trips for T4, T8, CT32 textures, a model, a material; unsupported format throws; assets-only mode stops before native; unchanged second cook re-cooks nothing.
- Engine: configurator runs before shared registration and its processors win; `PlayerLaunchOptions` parsing.
- Editor: `EditorPlaySessionService` with fakes (launch, relaunch kills previous, Stop, non-zero exit, unavailable platform, failures don't launch); Play script build passes the platform gameplay symbols and per-platform output path.
- Manual: Play on demodisc `cube_test` with PS2 selected — ask Helena before launching the editor or the player.

## Out of scope (first cut)

- Platform-specific DX11 materials (PS2 VU-style), audio, embedding the player window in the viewport, editor↔player IPC, platforms other than PS2.
