# Asset & Content Pipeline

Status: living document — reflects `engine/helengine.core/assets`, `engine/helengine.core/content`, and the corresponding `engine/helengine.files` write-side as built. Update this file in the same change that alters the behavior it describes.

Scope: how a serialized asset payload gets from disk into a typed in-memory object at runtime, and the editor/cook-time write path that produces those payloads. Backend-specific materialization of raw assets into GPU/platform resources (e.g. how a `ModelAsset` becomes a DirectX/Vulkan vertex buffer) and the platform build/cook orchestration that produces the packaged output directory are out of scope — each belongs in its own spec (rendering backend, platform/build system).

## 1. Asset identity & the raw/runtime split

Two unrelated base types both get called "asset" in this codebase; keeping them distinct matters.

- **`Asset`** (`engine/helengine.core/assets/Asset.cs`) is the base of every *serialized* payload type: `ModelAsset`, `TextureAsset`, `MaterialAsset`, `PlatformMaterialAsset`, `FontAsset`, `AudioAsset`, `AnimationClipAsset`, `TextAsset`, `SceneAsset`, `BlueprintAsset`. It carries only `Id` (string) and `RuntimeAssetId` (a `ulong`, zero meaning "ephemeral, not cached by player"). These are plain data POCOs produced by deserialization and consumed by content processors — they never touch a graphics API.
- **`RuntimeData`** (`engine/helengine.core/assets/RuntimeData.cs`) is the unrelated base of GPU/backend-resident resources: `RuntimeModel`, `RuntimeMaterial`, `RuntimeTexture`. These carry a string `Id` assigned once via `SetId` (throws if called with a blank id; there is no re-assignment guard beyond that). Backend-specific code (rendering-backend projects, out of scope here) is responsible for turning a raw `Asset` into a `RuntimeData` instance — e.g. uploading `TextureAsset.Colors` to a GPU texture, or building vertex/index buffers from `ModelAsset.Positions`/`Indices16`/`Indices32`.
- **`RuntimeModel` can retain its source `ModelAsset`.** `RuntimeModel.SetRawModelAsset`/`RawModelAsset` lets a renderer keep the raw geometry payload alongside the GPU resource specifically to support **load-time mesh preparation** (see `RuntimeMeshPreparationService`, `ModelTessellationProcessor` — out of scope for this spec but the retention hook lives here). Most renderers do not need this and leave it null.
- **Submeshes are a draw-range concept, not a data concept.** `ModelSubmeshAsset` (raw, serialized: material slot name + index start/count) and `RuntimeSubmesh` (runtime, GPU-facing draw range) are parallel but distinct types — the raw asset stores the authoring intent, the runtime submesh stores what the renderer actually draws.

### Invariants — do not break

- `Asset`-derived types must stay plain data with no engine/graphics dependencies; anything that needs a rendering backend belongs on the `RuntimeData` side.
- `RuntimeData.Id` is set-once by contract (`SetId` has no explicit re-entry guard today, but callers must not rely on being able to reassign it — treat it as write-once).

## 2. The HELE binary format and the read/write split

Every serialized payload starts with a shared header (`EngineBinaryHeader`/`EngineBinaryHeaderSerializer`, `engine/helengine.core/serialization/` and `engine/helengine.files/serialization/`) carrying: endianness, a format version byte, a format id (`ushort`), a record kind (`ushort`), and a value kind (`ushort`) identifying which concrete asset type follows.

There are **two independent implementations of the same format**, split by read/write direction and by which project may depend on which:

- **`helengine.core.PackagedAssetBinarySerializer`** — read-only. This is what packaged/player builds use through `ContentManager`/`AssetContentProcessor<T>` at runtime. It has no `Write*` methods at all.
- **`helengine.files.EditorAssetBinarySerializer`** — read *and* write. This is the editor/cook-time serializer: it writes every asset type out to the HELE format (`Write*` methods) and can also read them back (for round-tripping authored assets during editing).
- Each project also exposes a thin `AssetSerializer` facade (`helengine.core.AssetSerializer` read-only; `helengine.files.AssetSerializer` read+write) that reads the header first and dispatches to the matching format serializer — `helengine.files.AssetSerializer.Serialize` additionally special-cases `ShaderMaterialAsset` to a separate `ShaderMaterialAssetBinarySerializer` before falling back to `EditorAssetBinarySerializer`.

This split is deliberate, not incidental: it is the concrete embodiment of the FEATURES.md architectural goal *"Core remains runtime/read-side only"* — packaged/player code (`helengine.core`) must never need write-side serialization, so the write path (and everything upstream of it, like the editor and cook pipeline) lives only in `helengine.files`.

**The two readers must stay byte-compatible.** `PackagedAssetBinarySerializer` and `EditorAssetBinarySerializer` currently duplicate the same version ladder (`CurrentVersion`, `LegacyVersion`, `PreviousVersionWithoutRuntimeAssetId`, `TextureColorFormatVersion`, etc. — same numeric values in both files) and the same per-version conditional read branches, independently. There is no shared source for this logic; the two files are kept in sync **by convention**, not by the compiler. A payload written by `EditorAssetBinarySerializer` at version N must be exactly what `PackagedAssetBinarySerializer` at version N expects to read.

### Invariants — do not break

- Any change to the write side (`EditorAssetBinarySerializer`) that changes on-disk layout for a given `CurrentVersion` must bump the version and add a matching read branch in **both** serializers, or packaged runtime reads will silently misparse or throw deep in the reader with no schema-level protection.
- Never add write methods to `helengine.core.PackagedAssetBinarySerializer` or its callers — that would reintroduce the write-side dependency Core is deliberately kept free of.
- Legacy version branches (`LegacyVersion`, `PreviousVersionWithoutRuntimeAssetId`, etc.) must not be deleted just because current authoring no longer produces them — cooked content built at an older version must remain readable.

## 3. ContentManager & content processors

`ContentManager` (`engine/helengine.core/content/ContentManager.cs`) is the single load entry point for runtime content. It owns two lookup structures: `ProcessorRegistrationsById` (every registered processor, keyed by stable string id) and `DefaultProcessorsByTypeAndExtension` (only processors registered with at least one extension, keyed by output `Type` then normalized extension).

- **`IContentProcessor<T>`** (`Read(Stream) : T`) is the processor contract; the non-generic `IContentProcessor` (`ReadObject(Stream) : object`, plus `OutputType`) lets `ContentManager` reason about processors without a generic parameter.
- **Three concrete processor shapes exist:**
  - `AssetContentProcessor<TAsset>` — generic wrapper that calls the project-local `AssetSerializer.Deserialize` and casts to `TAsset`, throwing with rich diagnostics (asset path, read stage, last checkpoint from `EngineBinaryReadContext`) on a type mismatch. Used for every plain `Asset`-derived type that round-trips through the shared HELE format.
  - `BinaryContentProcessor<T>` — wraps an arbitrary `Func<Stream, T>` reader delegate. Used where the format has its own dedicated top-level reader rather than going through the generic `Asset`-dispatch path: `SceneAsset` (`PackagedAssetBinarySerializer.DeserializeSceneAsset`) and `FontAsset` (`FontAssetBinarySerializer.Deserialize`).
  - `RawByteContentProcessor` — the built-in wildcard (`*`) fallback every `ContentManager` registers in its constructor via `RegisterBuiltInProcessors`, for content with no specific processor.
- **Load resolution** (`Load<T>(assetPath, processorId = null)`): if `processorId` is supplied, resolve by id and validate `OutputType == typeof(T)` (throws on mismatch); otherwise resolve by `typeof(T)` + the requested file's extension via `TryResolveRegisteredExtension`, which picks the **longest matching registered extension** for the file name (so a more specific extension like `.platform.hasset` would win over `.hasset` if both were registered), falling back to a registered `*` wildcard for that type only if no specific extension matches.
- **Registration is fail-fast on collision.** `RegisterProcessor` throws if the processor id is already registered, and throws separately if a *default* (extension-bound) registration already exists for the same output type + extension pair. There is no "last registration wins" behavior.
- **Reads set `EngineBinaryReadContext`** (`CurrentAssetPath`, `CurrentReadStage`) around the stream-open and processor-read calls, restoring the previous values in a `finally` — this is what lets deep-nested deserialization failures report which asset and which read stage they were in without threading that context through every reader method's parameters.

### Invariants — do not break

- Extension matching must remain longest-match-wins with wildcard as the lowest-priority fallback; do not change this to first-registered-wins or similar without auditing every registered extension set for order-dependence.
- Processor registration must stay fail-fast (throw on id or type+extension collision) — silently overwriting a prior registration would let a later-loaded module quietly change which processor handles an already-configured type/extension pair.
- `EngineBinaryReadContext` must be restored (not just set) around a load, including on exception paths (already guaranteed by the `finally` in `LoadProcessedContent`) — nested loads (a scene loading referenced assets) depend on the outer context being restored correctly once the inner load completes.

## 4. Content stream sources & per-source caching

- **`IContentStreamSource`** is a single-method abstraction (`Stream OpenRead(string assetPath)`) — `ContentManager` never touches the filesystem directly, only through this seam.
- **`HostFileSystemContentStreamSource`** is the concrete host-filesystem-backed implementation. It normalizes a root path once at construction (`Path.GetFullPath`, unless the root itself is a *virtual rooted* path like `dvd:/` or `fs:/vol/content` used by console platforms, which is preserved verbatim). Path resolution (`ResolveContentPath`) handles three cases: already-virtual-rooted asset paths pass through unchanged, absolute paths resolve via `Path.GetFullPath`, and relative paths combine beneath the configured root (virtual-root string concatenation, or normal `Path.Combine`, depending on which kind of root was configured). It also contains one hardcoded platform alias (Wii U's packaged content root remaps a specific generated-material path to a WUHB-safe flat filename) — this is a narrow, deliberate special case, not a general aliasing mechanism.
- **`Core` caches one `ContentManager` per distinct `IContentStreamSource` instance** (§1 of the core-runtime-model spec) — this is where multiple content roots (e.g. a shared engine content root plus a project-specific one) each get their own processor registry and cache, keyed by stream-source object identity.

### Invariants — do not break

- `IContentStreamSource` must remain the only filesystem seam `ContentManager` uses; do not add a path where `ContentManager` or a processor opens files directly, since that would break virtual-root platform paths (consoles) and testability.
- Virtual-rooted paths (anything matching the `prefix:/...` shape) must be preserved verbatim through path resolution, never run through `Path.GetFullPath`, which would corrupt them on the host OS.

## 5. Stable processor ids & default registration

- **`RuntimeContentProcessorIds`** defines the stable string ids (`"runtime.model-asset"`, `"runtime.texture-asset"`, etc.) that runtime scene-loading code depends on to resolve processors by id rather than by type+extension inference. These strings are a cross-module contract: anything that loads assets by explicit processor id must use these constants, not ad hoc literals.
- **`RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager`** is the single place that wires the default runtime processor set onto a `ContentManager` (called once from `Core.Initialize`, §1 of the core-runtime-model spec). Every registration goes through `RegisterProcessorIfMissing`, making the whole configuration **idempotent** — calling it twice, or against a `ContentManager` a caller already partially configured, does not throw on duplicate registration.
- **Material resolution has a compile-time fork.** Under `HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED`, the material processor id resolves to `PlatformMaterialAsset` (a generic platform-owned cooked material shape) instead of the default `MaterialAsset`. This is a build-configuration switch, not a runtime decision — a given compiled runtime is wired to exactly one of the two shapes.
- **`RuntimeSceneCatalog`** is the built-scene lookup table injected via `CoreInitializationOptions.SceneCatalog`: an immutable, scene-id-keyed (case-insensitive) set of `RuntimeSceneCatalogEntry` records mapping stable scene ids to cooked relative paths. It throws on construction if given a duplicate scene id or a null entry — the catalog itself guarantees no duplicate/missing-id ambiguity can reach `SceneManager`. The build graph that *produces* this catalog is out of scope here (platform/build-system spec).

### Invariants — do not break

- `RuntimeContentProcessorIds` values must not change once shipped in a cooked build's expectations — packaged content and runtime code on either side of a platform build boundary must agree on these ids.
- Default processor configuration must remain idempotent (`RegisterProcessorIfMissing`); do not change it to unconditional `RegisterProcessor` calls, since multiple call sites may configure the same shared `ContentManager`.
- `RuntimeSceneCatalog` must remain immutable after construction and must keep rejecting duplicate scene ids at construction time rather than silently keeping the first or last entry.

## 6. RuntimeMaterial inheritance model

`RuntimeMaterial` (`engine/helengine.core/assets/RuntimeMaterial.cs`) supports a parent/child override model distinct from the raw `MaterialAsset` data:

- **A material can inherit from a parent material** (`SetParentMaterial`). Inheriting clones the parent's `MaterialRenderState` at bind time and re-synchronizes whenever the parent's render state changes (`SynchronizeWithParentMaterial`, triggered transitively through `SynchronizeChildMaterials`). A parented material's `SetRenderState` throws — once parented, render state is not independently settable, only inherited.
- **Cycle prevention is explicit.** `ValidateParentMaterial` walks the *proposed* parent's ancestor chain and throws if `this` would appear in it (self-parenting or parenting to one of your own children).
- **Primary texture resolution walks the parent chain** (`ResolvePrimaryTexture`): a material with no local texture override defers to its parent recursively, terminating at the first material (walking up) that has one set, or `null` if none does.
- **Disposal unregisters from the parent** but does not cascade-dispose children — a disposed parent leaves its children's `ParentMaterialValue` dangling rather than clearing it (children are expected to be disposed independently by whatever owns them).

### Invariants — do not break

- Once parented, a `RuntimeMaterial`'s render state must only change via the parent (through `SynchronizeWithParentMaterial`); `SetRenderState` must keep throwing for parented materials rather than allowing a local override that would silently diverge from the parent.
- Parent-chain cycle validation must run before any parent assignment takes effect — do not reorder `SetParentMaterial` to assign first and validate after.

## Open questions for follow-up specs

- How raw `Asset` payloads actually become `RuntimeData` instances (texture upload, vertex/index buffer construction, shader/material binding) is backend-specific and belongs in a rendering-backend spec.
- The editor/cook pipeline that produces packaged content on disk (asset import, `code.module.json`/`.heproj` build graph, platform-specific cooking) is covered by the platform/build-system spec, not here.
- `RuntimeMeshPreparationService` and `ModelTessellationProcessor` (load-time geometry preparation using the retained `RawModelAsset`) are only referenced here as the reason `RuntimeModel` keeps raw geometry around; their own behavior deserves a dedicated note, likely alongside the rendering-backend spec.
