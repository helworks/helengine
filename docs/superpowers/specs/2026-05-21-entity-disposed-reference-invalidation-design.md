# Entity Disposed Reference Invalidation Design

## Goal

Make `Entity` and `Component` references fail fast after disposal begins, so dead objects never continue to look valid in managed engine code.

## Problem

The current runtime allows cached references to generated child entities and components to survive past disposal boundaries unless each caller manually clears them. That led to hidden workaround components such as `FPSComponentOverlayLifetimeComponent` and `DebugComponentOverlayLifetimeComponent`, which exist only to notify owners before generated overlay subtrees are deleted.

That design is wrong for two reasons:

1. Dead scene objects still appear valid in managed code unless each caller patches around disposal.
2. Internal ownership bugs leak into user-visible scene/component structure.

The user requirement is explicit:

- no new events
- disposed objects should not remain usable references
- touching a disposed object should throw immediately

## Non-Goals

- No new runtime event system
- No native-handle redesign
- No silent null-return fallback for disposed objects
- No partial fix limited only to FPS/debug overlays

## Chosen Approach

Adopt a general engine rule:

- once `Entity` disposal begins, that `Entity` is invalid for further external use
- once `Component` disposal begins, that `Component` is invalid for further external use
- external access paths fail fast with `ObjectDisposedException`

This is implemented at the managed ownership layer, not by adding more per-system cleanup patches.

## Alternatives Considered

### 1. Managed disposed-state guards on `Entity` and `Component` with fail-fast exceptions

This is the chosen approach.

Pros:

- directly matches the required runtime semantics
- removes the need for overlay sentinel components
- keeps the fix at the ownership boundary instead of per-feature workarounds
- can be implemented incrementally inside the existing engine model

Cons:

- requires careful separation between external use and internal teardown paths
- may expose existing latent lifetime bugs by throwing sooner

### 2. Native-handle validity enforcement across all wrappers

Rejected for this task.

Pros:

- stronger long-term ownership model

Cons:

- much larger scope
- touches codegen/native interop assumptions unrelated to the immediate problem
- too expensive for this cleanup

### 3. Overlay-only cleanup simplification

Rejected for this task.

Pros:

- smaller initial patch

Cons:

- leaves the engine-wide zombie-reference problem intact
- guarantees the same bug class appears elsewhere later

## Engine Rule

The new rule is:

- a disposing or disposed `Entity` is invalid
- a disposing or disposed `Component` is invalid
- external use of such an object throws `ObjectDisposedException`

For this design, “disposing” already counts as “disposed enough” for external callers. There is no grace period where an object is half-dead but still externally valid.

## Scope of Guarding

The invalidation rule applies to:

- scene graph mutation APIs such as `AddChild`, `RemoveChild`, `AddComponent`, and `RemoveComponent`
- lifecycle flows that depend on a live parent/entity relationship
- reusable engine methods that operate on scene graph state
- property reads that expose engine-owned runtime state and would otherwise make dead objects appear valid

Examples:

- reading `entity.Parent` after disposal should throw
- reading `component.Parent` after disposal should throw
- mutating transforms or layer state on a disposed entity should throw
- attaching or detaching children/components on a disposed object should throw

The design intentionally prefers strictness over convenience. A dead object should not be partially inspectable in ordinary runtime code.

## Internal Teardown Boundary

Disposal itself still has to complete recursively. That means internal teardown code cannot blindly use the same public guard path that external callers hit.

The design therefore splits behavior into two categories:

### External or normal-use paths

These validate liveness and throw on disposed/disposing objects.

### Internal disposal paths

These are allowed to continue teardown after the disposed flag is set, but only inside the disposal implementation itself.

This preserves deterministic recursive cleanup while still making the object externally invalid as soon as disposal begins.

## Entity Design

`Entity` already tracks `isDisposing`. This design formalizes that into the public ownership contract.

Changes:

- add a public read-only disposed-state surface, such as `IsDisposed`
- treat `isDisposing` as the same invalid state for external callers
- add guard helpers for:
  - receiver validity
  - child/component argument validity
- update entity APIs to reject:
  - calls on disposed receivers
  - disposed child entities
  - disposed components

Representative methods to harden:

- `AddChild`
- `RemoveChild`
- `InitChildren`
- `InitComponents`
- `AddComponent`
- `RemoveComponent`
- hierarchy initialization and parent-change paths where external callers can still reach them

Property and state accessors that make dead entities look live should also validate disposed state.

## Component Design

`Component` gets the same explicit invalidation rule.

Changes:

- add a disposed-state surface, such as `IsDisposed`
- mark a component invalid when disposal begins
- throw on later external use, especially for:
  - parent/entity access
  - attach/detach misuse
  - lifecycle calls that require a live parent

This keeps component lifetime symmetric with entity lifetime and avoids a mixed ownership model.

## Overlay Cleanup Simplification

After the engine rule exists:

- remove `FPSComponentOverlayLifetimeComponent`
- remove `DebugComponentOverlayLifetimeComponent`
- simplify `FPSComponent` and `DebugComponent` so they rely on normal entity disposal semantics instead of hidden cleanup sentinels

Expected behavior:

- normal creation and teardown still work
- if stale overlay references survive in owner fields and are touched later, they throw immediately because the referenced objects are dead
- no hidden implementation-detail component remains in the scene hierarchy

## Error Behavior

Use `ObjectDisposedException` consistently for disposed-object access.

Guidelines:

- receiver disposed: throw referring to the receiver type
- disposed argument supplied: throw referring to the argument name
- do not silently ignore usage of dead scene objects

The intent is for failures to be obvious and attributable instead of becoming later null-state corruption.

## Compatibility and Risk

This change is intentionally stricter than current behavior.

Likely effects:

- existing lifetime bugs will surface earlier and more explicitly
- code that accidentally touches objects during or after teardown will now fail fast
- some tests or runtime flows may need adjustment if they relied on zombie references remaining readable

This is acceptable because the previous behavior is the root design bug.

## Testing Strategy

Focused validation is sufficient.

Add regression coverage for:

1. disposed entity reuse throws
2. disposed component reuse throws
3. disposed entity property access throws
4. disposed component parent access throws
5. adding a disposed child or component throws
6. `FPSComponent` overlay lifecycle works without overlay sentinel components
7. `DebugComponent` overlay lifecycle works without overlay sentinel components
8. scene transition / teardown paths do not require hidden overlay cleanup components

Testing should stay narrow and target ownership semantics rather than broad gameplay scenarios.

## Implementation Notes

The implementation should avoid introducing local helper functions and should follow existing repository rules:

- one class per file
- XML comments on new or changed members
- no nullable annotations
- no new event system

The first pass should focus on the core ownership layer and the two overlay systems already known to depend on the old workaround.

## Success Criteria

This design is successful when all of the following are true:

- disposed `Entity` and `Component` objects fail fast on later use
- disposal-begin state is already externally invalid
- `FPSComponentOverlayLifetimeComponent` and `DebugComponentOverlayLifetimeComponent` are no longer needed
- scene transitions and generated overlay teardown work without hidden sentinel components
- users no longer see internal lifetime-patch components in scene hierarchies
