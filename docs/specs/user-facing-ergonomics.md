# User-Facing Ergonomics

Status: living document — states an engine-wide design principle, not one subsystem's behavior.

## Principle

Anything a consumer builds on top of the engine — gameplay components, editor panels/dialogs, importers, build pipeline extensions, or any other public extension point — must behave correctly using only its own documented surface. Consumers should never need internal engine knowledge (lifecycle ordering, deferred initialization, thread affinity, hidden call ordering, etc.) to get correct default behavior.

## Rules

- Prefer behavior that's automatically correct over a documented step a consumer must remember to call.
- If an internal contract can't be avoided, satisfy it in the framework/base code the consumer builds on — not by asking every consumer to know and follow it.
- Internal tricks traded for performance (batching, deferred init, streaming) stay internal-only. Never let them leak into a public extension point.
- When adding a new extension point, ask: if someone used it exactly as it's named/documented, without reading the implementation, would it misbehave silently? If yes, that's a defect in the surface, not the consumer.
- Prefer failing loudly over silently doing nothing when a real precondition is unmet — a silent no-op ships as "the feature doesn't work," not as a bug report.

## Precedent

`DockableEntity`/`EditorDialogBase` now self-initialize so any panel/dialog author's `Update()`-driven behavior works without knowing `Entity.InitializeHierarchy()` exists (mechanism: `core-runtime-model.md`).

## Invariant

Fix a footgun at the shared base/framework layer it's reachable from, not at the one call site that happened to surface it — the same gap is reachable from every sibling.
