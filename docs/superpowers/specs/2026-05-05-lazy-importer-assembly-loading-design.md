## Lazy Importer Assembly Loading Design

### Goal

Keep editor startup as light as possible by separating heavy importer backends into their own assemblies and loading them only when an import request actually needs them.

### Scope

This design covers:

- texture importer backend isolation
- model importer backend isolation
- lazy importer instance creation
- lazy importer assembly loading
- overlapping texture importer support for the same extension
- metadata-driven importer registration in the editor host

This design does not cover:

- runtime plugin discovery
- dynamic importer download or installation
- non-Windows packaging
- automatic fallback to a different importer after a load or decode failure
- changes to preview-specific UI behavior

### Requirements

The system must satisfy the following:

1. Startup registration must not directly construct heavy texture or model importer backends.
2. Startup registration must not reference backend importer types through `typeof(...)` or direct `new` calls in host registration code.
3. Texture importers must continue to support multiple importers for the same extension.
4. The first registered texture importer for an extension remains the default importer.
5. Explicit importer override remains supported through import settings.
6. Model importers must also load lazily and must not load Assimp unless model import is actually requested.
7. Missing importer assemblies or backend dependencies must fail explicitly.
8. The engine must not automatically retry another importer after a selected importer fails.

### Recommended Approach

Use metadata-driven lazy importer factories in core editor code and move each backend into its own assembly.

Host startup should register importers using:

- importer id
- supported extensions
- target assembly simple name
- fully qualified importer type name

The host should not take compile-time dependencies on concrete importer classes in registration code.

### Architecture

#### Core Lazy Loader Contracts

The core editor layer keeps ownership of:

- importer ids
- extension lists
- default importer selection
- overlapping texture importer ordering
- lazy wrapper behavior

The core editor layer adds or generalizes factory types that can:

- store assembly name and type name metadata
- call `Assembly.Load(...)` only on first import
- resolve the importer type by name
- instantiate the importer with `Activator.CreateInstance(...)`
- validate that the created object implements the expected importer interface

This applies to:

- `ITextureImporter`
- `IModelImporter`

#### Texture Backend Assemblies

Texture backends should be split into separate assemblies:

- `helengine.editor.windows.gdiimporter`
- `helengine.editor.windows.pfimimporter`
- `helengine.editor.windows.magickimporter`

Each backend assembly owns only:

- its concrete importer implementation
- the package references required by that importer

The Windows host keeps only lightweight registration code and texture extension catalogs.

#### Model Backend Assemblies

The Assimp-backed model importer should remain in its own assembly:

- `helengine.editor.assimp`

The change for models is not to split it again, but to stop directly constructing `HelengineAssimpImporter` during startup registration.

The app host should register the model importer by metadata only, so the Assimp assembly remains unloaded until a model import is requested.

### Registration Model

#### Texture Registration

The Windows texture registration factory should create `TextureImporterRegistration` instances backed by lazy factories that know:

- importer id
- assembly simple name
- fully qualified type name
- extension list

The current order remains:

1. `gdi`
2. `pfim`
3. `magick`

That preserves current default selection behavior for overlapping extensions such as `.bmp`, `.dds`, `.tga`, and `.tiff`.

#### Model Registration

The app host should create `ModelImporterRegistration` instances backed by the same metadata-driven lazy-loading pattern.

The current default model importer remains:

1. `assimp`

Extensions remain:

- `.fbx`
- `.obj`
- `.gltf`
- `.glb`
- `.dae`
- `.3ds`

### Load Path

On startup:

1. the host creates importer registrations
2. each registration contains only metadata and a lazy wrapper
3. no backend importer instance is created
4. no backend assembly is explicitly loaded by registration

On first import for a given importer:

1. the asset import manager resolves the importer id
2. the lazy wrapper invokes its factory
3. the factory calls `Assembly.Load(...)`
4. the factory resolves the importer type by name
5. the factory creates the importer instance
6. the importer instance performs the import

On later imports through the same lazy wrapper:

- the already created importer instance is reused

### Failure Behavior

Registration failures:

- duplicate importer ids must throw
- importer registrations that conflict across asset kinds must throw
- invalid metadata such as empty assembly name or type name must throw

Load-time failures:

- missing backend assembly must throw a clear load failure
- missing backend dependency must throw a clear load failure
- type resolution failure must throw
- type/interface mismatch must throw
- importer constructor failure must propagate

Import-time failures:

- decode or conversion failures from the selected importer must propagate
- the engine must not silently fall back to another importer

### Testing Strategy

The implementation must cover:

1. texture importer registration remains ordered for overlapping extensions
2. default texture importer selection remains first-registered
3. explicit texture importer override continues to work with importer-qualified cache identities
4. startup registration is metadata-driven and does not depend on direct concrete importer activation
5. lazy factory behavior loads the target assembly only on first import
6. lazy factory behavior creates the importer only once
7. model importer registration for `assimp` is also metadata-driven and lazy
8. backend-specific tests remain in their own backend test projects

Backend-specific tests should remain split by backend:

- GDI tests in the GDI importer test project
- Pfim tests in the Pfim importer test project
- Magick tests in the Magick importer test project
- Assimp tests in the Assimp importer test project

### Packaging Notes

The backend assemblies may still be shipped alongside the editor executable.

That is acceptable for this phase. The important requirement is:

- the editor does not load those assemblies unless the user actually performs an import that needs them

This keeps startup light without introducing a plugin discovery system.

### Deferred Work

The following items are intentionally deferred:

- runtime importer discovery from folders or manifests
- importer capability negotiation beyond first-registration ordering
- automatic importer fallback chains
- cross-platform importer packaging policy
- hot-swapping importer assemblies during runtime

### Acceptance Criteria

The design is complete when:

1. texture backend DLLs are separated from the main Windows editor assembly
2. startup registration code uses metadata-driven lazy factories instead of direct backend type references
3. `helengine.editor.assimp` is registered lazily and does not load on startup
4. texture importer overlap behavior remains unchanged from the current registry design
5. importer-specific tests continue to pass in the isolated worktree
