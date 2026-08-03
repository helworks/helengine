# Core Runtime Model

Status: living document — reflects `engine/helengine.core` as built. Update this file in the same change that alters the behavior it describes; do not let it drift into aspirational or historical territory.

Scope: the frame loop, entity/component model, object registration, and scene lifecycle owned by `helengine.core`. Rendering backend internals, physics solver internals, and the asset/content pipeline are out of scope here and get their own specs.

## 1. Core

`Core` (`engine/helengine.core/Core.cs`) is the single coordinating object for a running engine instance.

- **Singleton.** `Core.Instance` is set in the constructor. Only one `Core` is expected to be live at a time; `Entity` and other runtime types reach it through the static singleton rather than injection.
- **Two-phase startup.** The constructor accepts `CoreInitializationOptions` and sets up cheap, allocation-only state (input system, stopwatches, profiler counters). `Initialize(RenderManager3D, RenderManager2D, IInputBackend, PlatformInfo, CoreInitializationOptions)` does the real setup: it normalizes options, builds the physics scheduler, creates `ObjectManager`, the entity factory, the content manager, `SceneManager` (only if a scene catalog or scene path resolver was supplied), and `RuntimeDiagnosticsService`. Code that touches `Core.ObjectManager`, `Core.SceneManager`, etc. before `Initialize` runs will see `null`.
- **`Update()` stage order.** `AdvanceUpdate` runs stages in this fixed order every call:
  1. `Input.EarlyUpdate()`
  2. `FPSComponent.RecordUpdateFrame()` / `DebugComponent.RecordUpdateFrame()`
  3. `ObjectManager.Update()` — all registered `IUpdateable`s, in update-order
  4. `AudioManager?.Update()`
  5. `UpdatePhysics(elapsedSeconds)` — fixed-step physics catch-up loop
  6. `Input.Update()`
  7. `PointerInteractionSystem.Update()`

  `RuntimeExecutionPhaseProbe.SetCurrentPhaseId` is called around each stage so native crash diagnostics can report which stage was executing. `Update()` (parameterless) measures real elapsed wall-clock time from an internal stopwatch; `Update(double)` accepts host-supplied elapsed time and validates it is finite and non-negative.
- **Physics is fixed-step and capped.** `PhysicsFixedStepScheduler` accumulates elapsed seconds and yields whole `StepSeconds` increments. `UpdatePhysics` drains the accumulator by calling `PhysicsRuntime.Step(StepSeconds)` in a loop bounded by `InitializationOptions.PhysicsMaxStepsPerUpdate` (default 8). Debt beyond the cap is **not** dropped — it stays in the accumulator and is consumed on subsequent updates. `PredictedPhysicsStepSeconds` exposes the amount of physics time the *current* update is expected to consume, computed the same way but without mutating the accumulator, for hosts that want to predict step count ahead of time (e.g. interpolation).
- **`Draw()`** optionally commits deferred scene operations first (`CommitPendingSceneOperationsDuringDraw`, on by default), then calls `RenderManager3D.Draw()`, then records the FPS/debug render frame. Hosts that need commit to happen at a different safe point (e.g. post-present) set `CommitPendingSceneOperationsDuringDraw = false` and call `Core.CompleteFrameBoundary()` explicitly themselves.
- **`CompleteFrameBoundary()`** is the only place `SceneManager.CommitPendingOperationsAtFrameBoundary()` is invoked from the engine loop. This is a deliberate seam: scene load/unload must never happen mid-update or mid-draw, only at this explicit boundary.
- **Content managers are cached per stream source.** `GetContentManager(IContentStreamSource)` lazily creates and caches one `ContentManager` per distinct `IContentStreamSource` instance (dictionary keyed by the source, lock-protected). Calling it twice with the same source returns the same manager.
- **Attaching physics is explicit and resets timing.** `AttachPhysicsRuntime`/`DetachPhysicsRuntime`/`ResetPhysicsTimingState` all reset the fixed-step accumulator. There is no implicit physics runtime — nothing steps until a host calls `AttachPhysicsRuntime`.

### Invariants — do not break

- `Core.Instance` must be assigned before any `Entity` is constructed (the `Entity` constructor registers itself with `Core.Instance.ObjectManager`).
- The `Update()` stage order above is depended on by components (e.g. input must be early-updated before gameplay code reads it mid-frame, physics must run before the late `Input.Update()`/pointer pass so gameplay this frame sees post-physics transforms during pointer interaction). Do not reorder stages without checking every component that reads `Core.DeltaTime`/`Input`/physics state during `Update`.
- Scene load/unload must only ever be committed inside `CompleteFrameBoundary` (directly or via `Draw`'s optional auto-commit). Do not add a second call site that commits pending scene operations mid-frame.
- Fixed-step physics debt beyond `PhysicsMaxStepsPerUpdate` must carry over to future updates, never be discarded — discarding it would make simulation speed dependent on the render/update framerate.
- `PhysicsFixedStepScheduler`, `CoreInitializationOptions`, and `ContentManager` caching all reject non-finite/negative/null inputs by throwing rather than silently clamping or defaulting (see project convention: no default values when a valid value is required).

## 2. Entity

`Entity` (`engine/helengine.core/Entity.cs`) is a scene-graph node that owns components and child entities.

- **Explicit collection init.** `components` and `children` start `null`. `InitComponents()`/`InitChildren()` must be called once before `AddComponent`/`AddChild` are used; calling either twice, or using the collections before init, throws.
- **Transform composition.** Local state (`LocalPosition`/`LocalScale`/`LocalOrientation`) is stored uncomposed. The public `Position`/`Scale`/`Orientation` getters compose with `Parent` recursively: scale multiplies, orientation concatenates (`float4.Concatenate`), position is rotated-and-scaled by the parent then translated by the parent's position. `WorldTransformMatrix` composes the full parent chain by matrix multiplication; `LocalTransformMatrix` does not consult the parent. The matrix build order is **scale → rotate → translate** (row-vector convention) via `CreateTransformMatrix` — this convention is fixed and other systems (rendering, physics binding) depend on it.
- **Enabled propagation.** `Enabled` is a local flag; `IsHierarchyEnabled` ANDs it with every ancestor's local flag. Setting `Enabled` only fires `ParentEnabledChange` on components/children when the *effective* (hierarchy) enabled state actually changes, not on every local flag flip.
- **Static propagation.** `Static` changes call `ParentStaticChange` on all components and children unconditionally (no dedup against effective state, unlike `Enabled`).
- **Reparenting guards.** `AddChild` throws if the child already has a parent, if it would create a cycle (`WouldCreateHierarchyCycle` walks up from `this`), or if the child is disposed. `RemoveChild` requires the entity to actually be a child of `this`.
- **Lifecycle gate: `InitializeHierarchy()`.** A hierarchy is not "initialized" until this runs once (idempotent — returns early if already initialized). It calls `ComponentInitialized` on every eligible component, then recurses into children. Components added to an *already-initialized* entity get `ComponentInitialized` immediately in `AddComponent`; components added before initialization wait for `InitializeHierarchy`.
- **Component lifecycle execution is policy-gated.** Every place a component lifecycle callback would fire (`AddComponent`, `InitializeHierarchy`, `RemoveComponent`, `ParentEnabledChange`) checks `ComponentExecutionPolicy.ShouldRunComponentLifecycle` first. In editor mode, components on an entity carrying the editor "update-suppression marker" (`Component.IsEditorUpdateExecutionSuppressionMarker`) skip gameplay lifecycle execution unless explicitly marked `[RunInEditor]` — this is how the editor runs scenes live without executing gameplay update logic.
- **Disposal order.** `Dispose()` is not reentrant-safe in the naive sense but is guarded (`isDisposing` short-circuits re-entry to just release native-owned lists). The real order: (1) remove and collect every component (firing `ComponentRemoved`/detach as normal `RemoveComponent` would), (2) recursively dispose every child, (3) dispose the collected components, (4) detach from `Parent` if any, (5) unregister from `Core.Instance.ObjectManager`. Components are removed *before* children are disposed, and are disposed only *after* children are gone.
- **All public members throw on a disposed entity** (`ThrowIfDisposed`) except the internal disposal path itself.

### Invariants — do not break

- Transform composition order (scale, then rotate, then translate; parent-then-local for world transforms) must not change without auditing every consumer of `WorldTransformMatrix`/`Position`/`Orientation` across rendering and physics.
- `InitComponents`/`InitChildren` must remain required, explicit, one-time calls — do not make them implicit/lazy, since callers rely on the "not yet initialized" state to distinguish authoring-time construction from attach.
- `ComponentInitialized` must fire exactly once per component, either immediately (already-initialized entity) or via `InitializeHierarchy` — never both, never zero times for an eligible component.
- Disposal must remove components before disposing children, and dispose components only after all children are fully torn down. Downstream code (asset ownership release, `SceneManager` disposal diagnostics) depends on this ordering.
- A disposed entity must reject all public API calls via `ThrowIfDisposed`; do not add code paths that silently no-op on disposed entities instead of throwing.

## 3. Component

`Component` (`engine/helengine.core/Component.cs`) is the base class for behavior attached to an `Entity`.

- **Lifecycle callbacks are virtual no-ops by default:** `ComponentAdded`, `ComponentInitialized`, `ComponentRemoved`, `ParentEnabledChange`, `ParentStaticChange`, `Dispose`. Subclasses override only what they need; the base implementations intentionally do nothing (`Dispose` only flips `isDisposed`).
- **`Parent` is set/cleared only by `Entity`** through the internal `AttachToEntity`/`DetachFromEntity` methods — components cannot reparent themselves.
- **Synthetic members** (`SetSyntheticStringMember`/`GetSyntheticStringMemberOrDefault` and the bool/int/float equivalents) are a generic string-keyed bag per component, used to let platform-extended runtime payloads attach ad hoc typed values to a component without extending the class itself. Member names must be non-empty; missing keys return the caller-supplied default rather than throwing.
- **`IsEditorUpdateExecutionSuppressionMarker`** and **`IsEditorSceneCameraSuppressionMarker`** are opt-in marker flags (default `false`) that specific editor-owned component subtypes override to suppress gameplay execution or camera registration during authoring — see `ComponentExecutionPolicy` in the Entity section above.

### Invariants — do not break

- Lifecycle callback base implementations must stay no-ops; do not add default behavior to the base class that subclasses would need to call `base.X()` to preserve, since much existing code does not call base.
- Only `Entity` may set `Component.Parent`. Do not add a public setter or other attach path.
- Synthetic member getters must return the supplied default on a missing key, never throw for a merely-absent key (only an invalid/blank member name throws).

## 4. ObjectManager

`ObjectManager` (`engine/helengine.core/managers/ObjectManager.cs`) owns the flat registries the engine iterates every frame and mediates registration between entities/components and cameras.

- **Owned lists:** `Entities`, `Updateables`, `Drawables2D`, `Drawables3D`, `Cameras`, `DirectionalLights`, `AmbientLights`, `PointLights`, `SpotLights`, `Interactables`. Initial capacities come from `CoreInitializationOptions`. Registration methods for entities/lights/interactables dedupe by reference (`ContainsReference`) before adding.
- **Update-order sorted, mutation-safe iteration.** `Updateables` is kept sorted by `IUpdateable.UpdateOrder` (insertion via `FindUpdateInsertIndex`, a byte comparison, not a stable sort — ties are broken by insertion order relative to existing entries only). `Update()` sets `updateLoopActive = true` for the duration of the loop; any `RegisterForUpdate`/`RemoveFromUpdate` call made *while the loop is active* is queued as a `PendingUpdateOperation` and applied only after the loop finishes (`ApplyPendingUpdateOperations`), never spliced into the list mid-iteration.
- **Per-updateable crash diagnostics.** Before invoking each `IUpdateable.Update()`, the manager records a diagnostic breadcrumb (pass count, list index, a stable FNV-1a hash of the concrete type name, and — if the updateable is a `Component` — the owning entity's authored scene id via `SceneEntityRuntimeIdComponent`). This exists so a native hard-crash mid-update can be correlated to the specific component/entity that was executing.
- **Camera-scoped render queues.** 2D/3D drawables are tracked once in the manager's flat lists *and* pushed into each matching camera's own `RenderQueue2D`/`RenderQueue3D`. "Matching" is decided by `ShouldRegisterDrawableWithCamera`: the drawable owner's `LayerMask` must intersect the camera's `LayerMask`, and if a `ViewportComponent` (or other `ICameraBoundViewportOwner`) governs the drawable's subtree, its `BindingMode` (`ExplicitCameraBindingMode` / `AncestorCameraBindingMode`) further restricts which camera(s) qualify.
- **Registering a camera backfills it** with every currently-registered drawable that matches; registering a drawable pushes it into every currently-registered matching camera. Cameras are kept sorted by `CameraDrawOrder` on insert.
- **Reparenting triggers a registration refresh**, not a rebuild: `Entity.RefreshRegistrationsAfterParentChangeRecursive` removes and re-adds each `IDrawable2D`/`IDrawable3D`/`ICamera` in the reparented subtree so camera-queue membership is recalculated against the new ancestor chain (viewport binding can change after a reparent).

### Invariants — do not break

- Never mutate `Updateables` while `IsUpdateLoopActive` is true; always route through the pending-operation queue. Direct list mutation mid-iteration would invalidate the loop index.
- Drawable-to-camera matching must stay purely a function of layer mask + viewport binding mode, computed fresh at registration/reparent time — do not cache "which camera(s) a drawable belongs to" in a way that can go stale across a reparent without a refresh call.
- Reference-equality dedup (`ContainsReference`) is intentional, not accidental — registries key on instance identity, not any value-based equality.

## 5. SceneManager

`SceneManager` (`engine/helengine.core/scene/runtime/SceneManager.cs`) tracks which built scenes are currently loaded, loads/unloads them, and reference-counts the runtime assets they own.

- **All load/unload requests are deferred by default.** `LoadScene`/`UnloadScene` only enqueue a `PendingSceneOperation`; nothing happens until `CommitPendingOperationsAtFrameBoundary()` runs (called from `Core.CompleteFrameBoundary`, see §1). Requesting `SceneLoadMode.Single` discards any *other pending load* operations queued ahead of it (not pending unloads), since a single-scene load supersedes prior queued loads.
- **Two distinct load paths exist:** the plain deferred-queue path (`LoadScene`/`UnloadScene` → committed synchronously inside `CommitPendingOperationsAtFrameBoundary`), and the **engine-owned transition** path (`RequestSceneTransition`) which is a small state machine advanced one step per frame-boundary call via `AdvanceSceneTransition`: unload non-persistent scenes → begin an async-style `RuntimeSceneLoadOperation` → call `.Advance()` on successive frame boundaries, updating `SceneTransitionProgress` (0.2 once loading starts, ramping to 1.0 as the operation completes) → track the loaded scene and fire `SceneLoaded` once `IsCompleted`. A second `RequestSceneTransition` call while one is already active is ignored, not queued.
- **`DontUnload` scenes survive single-scene transitions.** Whether a loaded scene is exempt from teardown during a single-load/transition is read once from `SceneAsset.SceneSettings.DontUnload` at load time and stored on the `LoadedSceneRecord`; scenes without this flag are unloaded by `UnloadScenesForSingleLoad`.
- **Owned assets are reference-counted across scenes**, not per-scene. Five parallel dictionaries (`ActiveOwned{Texture,Font,Audio,Model,Material}ReferenceCounts`) track how many currently-loaded scenes reference each distinct asset instance. Loading a scene increments counts for everything in its `RuntimeSceneOwnedAssetSet`; unloading decrements, and only releases the underlying asset when the count reaches zero. It is a thrown invariant violation (`InvalidOperationException`) to release an asset that was never registered.
- **Events bracket both sides of load and unload:** `SceneLoading`/`SceneLoaded` and `SceneUnloading`/`SceneUnloaded`. The "-ing" events fire while the scene's entities still exist / before they exist, giving listeners a chance to react before state changes; "-ed" events fire after.
- **Untracked startup roots.** Before the very first scene is ever tracked, any root entities already alive under `ObjectManager` (e.g. entities the host created directly at startup) are disposed by `DisposeUntrackedRootEntities` the first time a `Single`-mode scene loads — this is a one-time cleanup path, not a recurring behavior.
- **Externally-tracked scenes.** `TrackExternallyLoadedScene`/`TryUntrackExternallyLoadedScene` let editor-hosted authored scenes register/unregister themselves as "loaded" for runtime scene queries without going through the normal cooked-asset load/unload path (no asset ownership, no dispose-on-untrack).

### Invariants — do not break

- Scene load/unload must never execute outside `CommitPendingOperationsAtFrameBoundary`. Do not add a path that mutates `LoadedSceneRecords`/disposes scene roots synchronously from `LoadScene`/`UnloadScene` themselves.
- Owned-asset release must remain refcounted across scenes; an asset shared by two loaded scenes must not be released until both scenes have unloaded. Releasing on the first unload would leave the second scene holding a disposed asset.
- A second concurrent `RequestSceneTransition` must be ignored while one is active, not queued or merged — callers rely on transitions being strictly one-at-a-time.
- `DontUnload` is read once at load time from the scene's authored settings; it must not be re-evaluated dynamically mid-lifetime in a way that changes teardown behavior for an already-loaded scene.

## Open questions for follow-up specs

- Input system (`InputSystem`, `StandardPlatformInput`, `PointerInteractionSystem`) is referenced here only as a stage in the `Core.Update()` order; its own contract (action resolution, backend abstraction) belongs in a dedicated spec.
- `RuntimeDiagnosticsService`, `RuntimeProfilerMetrics`, and the `RuntimeExecutionPhaseProbe`/update-stage-diagnostics mechanism are described only where they touch the update loop; a diagnostics-focused spec could cover them fully.
- Rendering (`RenderManager3D`/`RenderManager2D`, render queues, render frame extraction) and the physics runtime contract (`IPhysicsRuntime`) are deliberately out of scope — each warrants its own spec given their size.
