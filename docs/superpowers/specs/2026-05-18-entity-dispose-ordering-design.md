# Entity Dispose Ordering Design

## Summary

Strengthen the global entity teardown contract so an entity's own components always finish their disable and remove lifecycle before any child entity in that subtree is disposed.

This moves the safety guarantee into the engine instead of forcing user-authored components to understand current disposal ordering, cached child-reference invalidation, or special lifetime-sentinel patterns.

## Goals

- Make parent-owned generated child hierarchies safe during normal parent disposal.
- Give every component one simple global rule for teardown ordering.
- Remove the need for deep engine-lifecycle knowledge in user-facing components.
- Fix the class of unload crashes where a component writes into cached child-component references after the child subtree has already been destroyed.

## Non-Goals

- Redesigning the full component lifecycle surface.
- Adding a new pre-dispose callback in this change.
- Solving arbitrary externally-triggered subtree disposal outside parent disposal.
- Refactoring all existing overlay or helper-hierarchy components in the same step.

## Problem

The current `Entity.Dispose()` implementation disposes child entities before it removes the entity's own components.

That ordering is unsafe for components that:

- create generated helper child entities under their parent entity
- cache references into those generated child entities or child components
- perform cleanup or last-state writes during `ParentEnabledChange(false)` or `ComponentRemoved(...)`

Under the current contract, those cached references can already be stale by the time the parent component begins its own teardown lifecycle.

This is an engine contract problem, not a user-component-design problem.

## Recommended Approach

Change the global teardown ordering in `Entity.Dispose()` so the entity's components are removed and disposed before its children are disposed.

This is preferred over a new lifecycle callback because it improves the existing contract instead of expanding it. It is preferred over component-local lifetime sentinels because it solves the general ownership problem at the engine boundary where it actually originates.

## New Teardown Contract

When `Entity.Dispose()` is called on entity `E`:

1. mark `E` as disposing
2. remove and dispose all components attached directly to `E`
3. dispose all child entities of `E`
4. clear owned collections
5. detach `E` from its parent if still attached
6. unregister `E` from the object manager

### Lifecycle Guarantee

While a component attached directly to `E` is executing:

- `ParentEnabledChange(false)`
- `ComponentRemoved(E)`
- `Dispose()`

because `E` itself is disposing, `E`'s child hierarchy is still live.

That means the component may still:

- inspect its generated child entities
- remove generated helper components
- dispose its own generated helper subtree
- clear cached references safely

without racing the engine's child-disposal phase.

## Behavior Details

### Component Phase

During the component-removal phase:

- existing `RemoveComponent` lifecycle behavior remains intact
- components still receive `ParentEnabledChange(false)` before `ComponentRemoved(...)`
- `DetachFromEntity()` still happens after removal callbacks

The important change is only that this phase now completes before child disposal begins.

### Child Phase

After direct components are fully removed, children are disposed recursively using the same stronger contract.

Each child therefore gets the same owner-before-children guarantee for its own teardown.

### Parent Detach Phase

Detaching the disposing entity from its parent remains a late step. This preserves the current expectation that the entity can still reason about its parent relationship during its own teardown unless another specific removal path already detached it.

## Scope Boundaries

This contract is specifically about normal parent-driven disposal.

It does not claim that cached references remain safe when:

- a generated child subtree is disposed externally before the parent begins disposal
- child entities are reparented away manually
- unrelated runtime code disposes helper entities directly

Those cases may still need local defensive code depending on the component design. The engine-level change is still worthwhile because the most fundamental and common teardown path becomes safe by default.

## Expected Impact

### Positive Impact

- User-facing components no longer need deep teardown-order knowledge just to manage generated helper hierarchies safely.
- Engine components such as overlay-style helpers become simpler because normal parent disposal is no longer hostile to cached child references.
- Native generated builds become less crash-prone during scene unload.

### Compatibility Risk

This changes a global disposal ordering rule, so some existing code may accidentally depend on children being gone before parent components are removed.

That dependency would be unusual, but the implementation must verify it through focused runtime tests.

## Testing Strategy

Implementation should be driven by focused tests that prove the new contract.

### Entity Lifecycle Tests

Add tests that verify:

- direct component removal happens before child disposal during `Entity.Dispose()`
- a parent component can still observe its generated child entity during removal
- recursive child disposal still completes correctly

### Regression Tests

Keep and expand regression coverage for components that create generated helper hierarchies, especially:

- `FPSComponent`
- `DebugComponent`

The important regression is that normal scene unload through parent disposal must not crash when those components own generated overlay children.

### Windows Verification

Rebuild the Windows probe export and rerun the authored navigation sequence:

- `DemoDiscMainMenu`
- `cube_test`
- `DemoDiscMainMenu`
- `cube_test`
- `DemoDiscMainMenu`

Success means:

- no startup or unload crash
- no invalid access during component disable/removal
- resource release/build behavior remains observable in diagnostics

## Migration Guidance

Once this contract exists, component-local lifetime sentinels that were added only to survive parent disposal should be reconsidered case by case.

They should not be removed blindly because some may still protect against externally-disposed subtrees. But they should no longer be treated as the primary engine pattern for ordinary owned-child teardown.

## Recommendation

Adopt the stronger global `Entity.Dispose()` ordering.

This is the smallest engine-level change that gives component authors the right abstraction boundary: owners tear down before owned children. That is the contract users will naturally expect, and it removes a class of disposal bugs without making them learn engine internals.
