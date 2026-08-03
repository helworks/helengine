# Asset & Content Pipeline

Status: living document — reflects `engine/helengine.core/assets`, `engine/helengine.core/content`, and the `engine/helengine.files` write side as built. Backend-specific materialization of assets into GPU resources, and the editor/cook orchestration that produces packaged output, are covered elsewhere.

## Raw assets vs. runtime resources

Serialized payload types (models, textures, materials, scenes, and similar) share a common `Asset` base carrying just an id — they're plain data with no engine or graphics dependency. GPU/backend-resident resources (`RuntimeModel`, `RuntimeMaterial`, `RuntimeTexture`, ...) are a separate, unrelated hierarchy that backend-specific code builds *from* raw assets. Keep that boundary clean: raw asset types shouldn't grow graphics dependencies, and runtime resource types shouldn't grow serialization logic.

## Read-only core, read+write files

Every asset is encoded in one shared binary format, but the two sides that touch it are split by direction: `helengine.core` only ever *reads* it (packaged/player runtime), while `helengine.files` both reads and writes it (editor/cook time). This is deliberate — packaged runtime code should never need write-side serialization. The two readers currently duplicate their version-handling logic independently, so any format change needs a matching update on both sides, and the version number bumped, or older cooked content becomes unreadable.

Guidelines:
- Don't add write methods to the core-side reader — that would pull the write-side dependency back into runtime.
- Bump the format version and add matching read branches on both sides for any on-disk layout change; don't drop old-version read branches just because current authoring no longer produces them.

## ContentManager & processors

Content is loaded by output type + file extension (longest matching registered extension wins, with a wildcard fallback), or by an explicit processor id. Registration is fail-fast on collision rather than "last one wins." Keep it that way — silent overwrite would let a later-loaded module quietly change how an already-configured type/extension is handled.

## Stream sources & processor ids

All filesystem access goes through a small `IContentStreamSource` seam, which is also what lets console platforms use virtual root paths (e.g. `dvd:/...`) instead of normal filesystem paths — those must be preserved verbatim, never run through path normalization. `Core` caches one content manager per distinct stream source. Default runtime processor registration is idempotent and keyed by stable string ids that packaged builds and runtime code must agree on.

## RuntimeMaterial inheritance

A `RuntimeMaterial` can inherit render state from a parent material, with cycle checks on assignment and primary-texture resolution walking up the parent chain. Once parented, a material's render state should only ever change via the parent sync path — allowing an independent local override would silently diverge from what the parent expects.
