# Content Stream Source Design

## Goal

Replace direct filesystem ownership in `helengine.core` content loading with one generic stream-source abstraction that all platforms use. This removes `File.OpenRead` from `ContentManager`, lets packaged platforms provide their own runtime content source, and creates the generic seam required to disable `host_file_system` for packaged generated-core builds later.

## Current Problem

`ContentManager` currently owns both:

- processor registration and dispatch
- filesystem-backed stream opening

That coupling is too strong for the engine's current platform mix.

Packaged platforms such as DS already maintain platform-owned packaged asset loading paths, but generated-core still routes runtime asset loads through `ContentManager`, which means the engine still owns host-filesystem behavior even when the runtime is packaged. The result is:

- stale feature ownership in the generated-core feature catalog
- larger packaged generated-core output than necessary
- platform-specific loader duplication outside the core seam

## Goals

- Remove direct file IO from `ContentManager`.
- Introduce one engine-wide content stream source abstraction that every platform can implement.
- Make `CoreInitializationOptions` depend on a content source instead of `ContentRootPath`.
- Update all platform hosts to provide the correct content source in the same breaking change.
- Preserve the current processor registration and typed load behavior in `ContentManager`.
- Leave room for future generated-core feature pruning of `host_file_system`.

## Non-Goals

- Do not redesign content processors.
- Do not add a second content manager type.
- Do not keep a compatibility shim around `ContentRootPath`.
- Do not implement asset enumeration or write support unless a current runtime path actually requires it.

## Recommended Architecture

### New Abstraction

Add one minimal interface in `helengine.core` for readable content streams.

Proposed shape:

```csharp
namespace helengine {
    /// <summary>
    /// Opens readable streams for runtime content paths.
    /// </summary>
    public interface IContentStreamSource {
        /// <summary>
        /// Opens one readable stream for the supplied runtime content path.
        /// </summary>
        /// <param name="assetPath">Runtime asset path understood by the active source.</param>
        /// <returns>Readable content stream.</returns>
        Stream OpenRead(string assetPath);
    }
}
```

This should stay intentionally narrow for the first pass. The engine currently needs stream opening, not a broader virtual filesystem.

### ContentManager

`ContentManager` keeps:

- processor registration
- processor lookup by type and extension
- typed `Load<T>` APIs
- engine binary read context staging

`ContentManager` stops owning:

- root path normalization
- path-to-filesystem translation
- direct `File.OpenRead` calls

The constructor becomes source-based instead of root-path-based.

Proposed shape:

```csharp
public ContentManager(IContentStreamSource streamSource) {
    StreamSource = streamSource ?? throw new ArgumentNullException(nameof(streamSource));
    ProcessorRegistrationsById = new Dictionary<string, ContentProcessorRegistration>(StringComparer.OrdinalIgnoreCase);
    DefaultProcessorsByTypeAndExtension = new Dictionary<Type, Dictionary<string, ContentProcessorRegistration>>();
    RegisterBuiltInProcessors();
}
```

`LoadProcessedContent` changes from `File.OpenRead(fullPath)` to `StreamSource.OpenRead(assetPath)`.

The content path supplied to processors remains a runtime asset path string. For filesystem-backed sources that can still be a rooted path or a source-normalized path. For packaged runtimes it becomes a platform-owned content path key.

### CoreInitializationOptions

This is a breaking API change.

Remove:

- `string ContentRootPath`

Add:

- `IContentStreamSource ContentStreamSource`

This becomes required for runtime initialization paths. `Core` should throw if it is missing instead of creating any default source silently.

### Core

`Core` currently caches `ContentManager` instances by normalized root path. That should become source-based caching.

Recommended first-pass rule:

- keep one primary `ContentManager` for `InitializationOptions.ContentStreamSource`
- if multi-source support is still required, key caches by `IContentStreamSource` object identity instead of strings

If no current production path actually needs multiple runtime sources, the simpler and safer first pass is a single engine-owned content manager instance.

### Platform Implementations

Every platform host provides one implementation of `IContentStreamSource`.

Required implementations:

- host filesystem source in `helengine.core` or a shared runtime assembly for editor-like and desktop hosts
- packaged-runtime source per packaged platform family

Platform guidance:

- Windows and editor-hosted runtimes use a filesystem-backed source.
- DS, 3DS, PS2, PSP, Vita, Wii, and GameCube use packaged-content-backed sources.
- Existing DS packaged-loader logic should move behind the new source seam rather than remain parallel to it.

### Feature Catalog Consequence

After this migration:

- `host_file_system` should remain owned only by types that truly require host filesystem behavior
- packaged platforms can eventually force-disable `host_file_system` in generated-core once they no longer instantiate a filesystem source

This design does not itself remove `host_file_system`, but it creates the correct seam for that follow-up cleanup.

## API Direction

### Recommended first-pass API

```csharp
public interface IContentStreamSource {
    Stream OpenRead(string assetPath);
}

public sealed class HostFileSystemContentStreamSource : IContentStreamSource {
    public HostFileSystemContentStreamSource(string rootPath) { ... }
    public Stream OpenRead(string assetPath) { ... }
}
```

```csharp
public class CoreInitializationOptions {
    public IContentStreamSource ContentStreamSource { get; set; }
}
```

```csharp
public class ContentManager {
    public ContentManager(IContentStreamSource streamSource) { ... }
}
```

### Deferred API ideas

Do not add these in the first pass unless a real runtime path proves they are needed:

- `bool Exists(string assetPath)`
- `string NormalizePath(string assetPath)`
- directory enumeration
- write operations
- asset-specific loader interfaces

## Migration Plan

### Step 1

Add the new core abstraction and a filesystem-backed source implementation.

### Step 2

Update `ContentManager` to depend on `IContentStreamSource` and remove direct `File.OpenRead` ownership.

### Step 3

Update `CoreInitializationOptions` and `Core` to require a content source.

### Step 4

Update all platform hosts and runtime boot paths to construct and pass the right source.

### Step 5

Move DS packaged-loading behavior behind the abstraction instead of maintaining a parallel path.

### Step 6

Update tests across engine and platform repos to use explicit content-source test doubles or filesystem sources.

## Risks

- This is a breaking API change across many repos and hosts.
- Some platforms may have hidden assumptions that runtime asset paths are filesystem paths.
- `ContentManager` cache behavior may need careful updates if any platform currently relies on multiple runtime roots.
- Packaged platforms may still have some direct file IO outside `ContentManager`; that does not block this change but it means `host_file_system` will not disappear everywhere immediately.

## Testing Strategy

### Engine

- `ContentManager` unit tests for source-based reads and processor dispatch
- `Core` tests for required content source initialization
- scene-loading tests through `SceneManager` using a fake content source

### Platform

- one proving packaged-platform test path per packaged runtime family
- DS source-audit tests updated to ensure generated-core no longer depends on path-root construction
- desktop/editor tests updated to use the filesystem source explicitly

## Decision Summary

The engine should not split into separate filesystem and packaged content managers. The right seam is one minimal `IContentStreamSource` abstraction injected through `CoreInitializationOptions` and consumed by `ContentManager`. This keeps processor behavior centralized, updates all platforms consistently, and gives the engine one generic path toward disabling host filesystem ownership in packaged generated-core builds.
