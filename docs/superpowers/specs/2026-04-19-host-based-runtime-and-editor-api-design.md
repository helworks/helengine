# Host-Based Runtime And Editor API Design

## Summary

This design replaces the current bootstrap shape with two explicit host entry points:

- `GameHost` with `GameHostOptions`
- `EditorHost` with `EditorHostOptions`

The goal is to make the engine easy for humans to start from code while still allowing advanced customization through explicit override hooks. Runtime and editor APIs remain structurally separate: editor classes do not appear in runtime-facing APIs, and runtime code never requires editor setup concepts.

## Problem Statement

The current bootstrap path asks humans to know too much about engine internals up front.

Today the editor host path requires manual setup of:

- project root and assets root resolution
- render-manager creation
- input-manager creation
- content setup
- font creation
- toolbar icon loading
- importer registration
- editor session wiring

The runtime/editor boundary is also unclear because mutable process-global state is used heavily:

- `Core.Instance`
- static editor project paths
- static selection state
- static asset-picker routing
- static scene-mutation notifications

This creates several human-facing problems:

- startup ownership is ambiguous
- dependencies are hidden
- multiple sessions are architecturally blocked
- tests need global resets
- advanced setup exists, but the default path is still high-friction
- editor and runtime concerns are not clearly isolated

The design must fix the human ergonomics problem without removing the ability to customize renderer/input/content/runtime/editor behavior when necessary.

## Goals

- Provide one short, obvious startup path for runtime code.
- Provide one short, obvious startup path for editor code.
- Auto-create default platform, renderer, input, content, and session services.
- Allow advanced users to override defaults through explicit option hooks.
- Keep editor classes completely separate from runtime/game-facing classes.
- Remove ambiguous bootstrap ownership.
- Replace mutable global coordination state with host-scoped or session-scoped services.
- Make dependency requirements legible from constructors or scoped service access.
- Make runtime and editor startup testable without resetting process-global state.

## Non-Goals

- Preserve existing bootstrap APIs for compatibility.
- Support editor APIs from runtime-facing assemblies.
- Introduce a general-purpose dependency injection framework.
- Re-architect rendering, scene serialization, or UI composition beyond what is required to support the new host model.
- Collapse editor and runtime into one shared "god" host or options type.

## Design Principles

### 1. Easy Path First

Humans should be able to start the runtime or editor with a small options object and no manual engine plumbing.

### 2. Explicit Ownership

Each host owns its bootstrap, lifecycle, and disposal. Lower-level consumers do not initialize the host again.

### 3. Hard Runtime/Editor Separation

Runtime APIs must not expose editor types. Editor code may build on runtime primitives, but the dependency direction remains one-way.

### 4. Scoped Mutable State

Mutable coordination state belongs to runtime/editor scopes, not process-global static state.

### 5. Defaults With Escape Hatches

The default path should be short. Complexity is opt-in through explicit override hooks, not mandatory constructor ceremony.

## Public API Shape

## Runtime Entry Path

The primary runtime-facing entry point becomes:

```csharp
public static class GameHost {
    public static void Run(GameHostOptions options);
}
```

```csharp
public sealed class GameHostOptions {
    public string ProjectPath { get; set; }
    public RuntimeWindowOptions Window { get; set; }
    public Func<GameHostContext, RenderManager3D> RendererFactory { get; set; }
    public Func<GameHostContext, InputManager> InputFactory { get; set; }
    public Action<RuntimeServiceCollection> ConfigureRuntimeServices { get; set; }
}
```

Minimal usage:

```csharp
GameHost.Run(new GameHostOptions {
    ProjectPath = projectPath
});
```

Advanced usage:

```csharp
GameHost.Run(new GameHostOptions {
    ProjectPath = projectPath,
    RendererFactory = context => new VulkanRenderer3D(),
    InputFactory = context => new InputManagerWindows(context.WindowHandle),
    ConfigureRuntimeServices = services => {
        services.SetContentRoot("mods");
    }
});
```

## Editor Entry Path

The primary editor-facing entry point becomes:

```csharp
public static class EditorHost {
    public static void Run(EditorHostOptions options);
}
```

```csharp
public sealed class EditorHostOptions {
    public string ProjectPath { get; set; }
    public EditorWindowOptions Window { get; set; }
    public Func<EditorHostContext, RenderManager3D> RendererFactory { get; set; }
    public Func<EditorHostContext, InputManager> InputFactory { get; set; }
    public Action<RuntimeServiceCollection> ConfigureRuntimeServices { get; set; }
    public Action<EditorServiceCollection> ConfigureEditorServices { get; set; }
    public Action<EditorRegistrationCollection> RegisterEditorExtensions { get; set; }
}
```

Minimal usage:

```csharp
EditorHost.Run(new EditorHostOptions {
    ProjectPath = projectPath
});
```

Advanced usage:

```csharp
EditorHost.Run(new EditorHostOptions {
    ProjectPath = projectPath,
    RendererFactory = context => new DirectX11Renderer3D(),
    ConfigureEditorServices = services => {
        services.UseCustomToolbarIcons(iconSet);
    },
    RegisterEditorExtensions = registrations => {
        registrations.RegisterImporter(new TextureImporterRegistration(...));
    }
});
```

## Separation Rules

These rules are mandatory:

- `GameHost`, `GameHostOptions`, and runtime-facing service types must not reference editor types.
- `EditorHost`, `EditorHostOptions`, and editor-facing service types may depend on runtime primitives, but only one-way.
- No shared top-level host or options type may combine runtime and editor concerns.
- If runtime and editor options need similar data, they may share neutral abstractions or duplicated shapes, but not editor-aware runtime APIs.
- Game-facing assemblies must not reference editor assemblies.

## Ownership And Lifecycle

## Runtime Ownership

`GameHost` owns:

- project path validation and normalization
- window creation
- renderer creation
- input creation
- runtime core/service graph creation
- update/draw lifecycle
- disposal of resources it created

Runtime consumers should receive prepared services or a prepared runtime context. They must not initialize the host or recreate runtime-global infrastructure.

## Editor Ownership

`EditorHost` owns:

- editor project path validation and normalization
- window creation
- renderer creation
- input creation
- runtime core/service graph creation
- editor service graph creation
- editor session creation
- default editor registrations
- update/draw lifecycle
- disposal of resources it created

`EditorSession` becomes a consumer of prepared editor services, not the root bootstrap coordinator.

That means:

- `MainForm` or any future platform shell becomes a thin host adapter
- `EditorSession` no longer initializes `EditorCore`
- `EditorSession` no longer owns renderer/input/content/bootstrap policy
- runtime/editor lifecycle ownership becomes obvious from the entry point

## Service Boundaries

Mutable state and event hubs move from static globals into host-scoped or session-scoped services.

### Runtime-Scoped State

Runtime-scoped state belongs in runtime services created by `GameHost`, such as:

- object management
- content access
- render access
- input access
- project path resolution

### Editor-Scoped State

Editor-scoped state belongs in editor services created by `EditorHost`, such as:

- selection state
- asset picker routing
- scene mutation notifications
- editor project paths
- asset import registrations
- panel registrations

### Allowed Static Shapes

The following may remain static:

- pure helpers
- constants
- stateless utility methods

The following must stop being static:

- mutable shared state
- event aggregators
- per-session project paths
- selection state
- editor action routing

## Scoped Service Access

To avoid constructor bloat while still removing hidden dependencies, services are grouped into a few coherent scoped access objects.

Runtime code uses runtime-scoped access such as:

- `RuntimeServices`
- `RuntimePaths`
- `RuntimeContentServices`

Editor code uses editor-scoped access such as:

- `EditorServices`
- `EditorPaths`
- `EditorInteractionServices`

The exact class names may change during implementation, but the boundary must remain:

- runtime-scoped access objects contain runtime concerns only
- editor-scoped access objects contain editor concerns only

## Defaults And Override Model

## Default Runtime Setup

When `GameHost.Run` is called with only `ProjectPath`, the host creates:

- a default runtime window or shell
- a default renderer
- a default input manager
- a default content root based on the project
- the runtime core and runtime service graph

## Default Editor Setup

When `EditorHost.Run` is called with only `ProjectPath`, the host creates:

- a default editor window or shell
- a default renderer
- a default input manager
- a default content root based on the project
- the runtime core and runtime service graph needed by the editor
- the editor service graph
- the default editor session
- default built-in editor panels, tools, importers, and registrations

## Override Hooks

Override hooks must be narrow and explicit.

Examples include:

- `RendererFactory`
- `InputFactory`
- `ConfigureRuntimeServices(...)`
- `ConfigureEditorServices(...)`
- `RegisterEditorExtensions(...)`

The default path must not require callers to understand every hook. Hooks exist only for advanced customization.

## Bootstrap Refactor Requirements

The implementation must remove these structural problems:

- double initialization of the core from both shell code and editor-session code
- runtime/editor services reaching through `Core.Instance` for mutable coordination behavior
- editor-only event hubs implemented as process-global static classes
- global project-path singletons shared across sessions
- misleading configuration paths that appear to support multiple backends but ignore the caller's choice

## Path Resolution Rules

Path behavior must be predictable and identical across runtime and editor hosts.

Required behavior:

- `ProjectPath` must be provided
- `ProjectPath` may be a project directory or project file path
- path validation happens at host creation time
- nonexistent paths fail immediately with a clear error
- project root resolution is centralized in one shared neutral runtime/platform helper, not duplicated differently in runtime/editor shells

## Migration Strategy

The migration is intentionally breaking. The design should not keep the confusing bootstrap shape alive behind wrappers.

### Phase 1: Introduce Host APIs

Add:

- `GameHost`
- `GameHostOptions`
- `EditorHost`
- `EditorHostOptions`
- runtime-scoped service access objects
- editor-scoped service access objects

These become the intended public entry points immediately.

### Phase 2: Move Ownership Into Hosts

Refactor startup so:

- application shells call hosts
- hosts own bootstrap and disposal
- `EditorSession` consumes prepared services
- old bootstrap responsibilities are removed from shells and sessions

### Phase 3: Replace Static Mutable State

Migrate:

- editor project paths
- selection state
- asset picker routing
- scene mutation notifications
- direct mutable coordination dependencies on `Core.Instance`

### Phase 4: Remove Misleading Or Dead Surfaces

Remove:

- fake or ignored backend-selection paths
- bootstrap APIs whose ownership semantics conflict with the new host model
- parallel object graphs that do not represent the true runtime/editor source of truth

## Testing Strategy

The refactor must be verified at three levels.

### 1. Minimal Startup Tests

Runtime:

- start a runtime host with only `ProjectPath`
- verify default renderer/input/content/bootstrap completes

Editor:

- start an editor host with only `ProjectPath`
- verify default editor session/bootstrap completes

### 2. Override Tests

Runtime:

- custom renderer factory is used
- custom input factory is used
- runtime service overrides are applied

Editor:

- custom renderer factory is used
- editor service overrides are applied
- importer/panel/editor registrations are applied

### 3. Isolation Tests

Verify:

- runtime-facing APIs do not expose editor types
- editor-facing APIs can use runtime primitives without reversing the dependency direction
- editor scoped state is not shared through mutable static globals
- tests can instantiate fresh hosts/scopes without calling global reset helpers

## Success Criteria

The design is successful when all of the following are true:

- a minimal runtime app starts with one short options object
- a minimal editor app starts with one short options object
- editor classes never appear in runtime-facing APIs
- runtime setup never requires editor concepts
- advanced customization is available without polluting the default path
- startup ownership is obvious from reading the entry point
- host/session state is scoped instead of globally mutable
- tests no longer depend on resetting process-global editor coordination state

Most importantly, a new human reading the code should be able to answer these questions quickly:

- What do I call to start the game?
- What do I call to start the editor?
- Where do I override defaults if I need something custom?

If those answers are still hard to find, the redesign has failed its primary goal.
