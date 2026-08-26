# Unified Asset Cook Graph Design

## Summary

Helengine will replace duplicated editor packaging and transformation paths with one platform-neutral asset cook graph. The graph starts from resolved current authoring references, discovers typed dependencies, computes target-specific cook keys, delegates platform-specific leaf operations through existing platform-builder contracts, and produces a manifest of immutable cooked artifacts.

Windows and every dynamically loaded platform consume the same graph. Platform builders describe or execute target-specific cooking; they do not reimplement dependency traversal, authored-reference resolution, generic scene transformation, output naming, or artifact publication.

## Goals

- Establish one source of truth for authored-reference to cooked-artifact conversion.
- Remove duplicated model, material, texture, audio, font, animation, shader, and scene packaging logic.
- Make dependency discovery explicit and cycle-checked.
- Produce deterministic cook keys and content-addressed artifacts.
- Reuse identical cooked outputs within and across targets when their contracts match.
- Keep platform-specific formats behind typed base-platform interfaces.
- Record enough provenance to diagnose why an artifact was rebuilt.
- Ensure runtime packages contain no editor identity metadata or raw authored references.
- Preserve the existing shared build graph's phase ownership and direct-source build behavior.

## Non-Goals

- Moving native compiler or SDK orchestration into the editor.
- Making all platforms emit identical bytes.
- Changing authored native formats.
- Supporting old package or authored formats.
- Designing optical-media physical placement; media layout remains a later phase consuming cooked artifacts.
- Making project builds concurrent while source-mutating prebuild commands remain serialized.

## Current Problem

`EditorWindowsBuildScenePackager` and `SceneComponentPackagingTransformService` both contain large paths for resolving and writing models, textures, materials, audio, fonts, generated geometry, shader artifacts, and transformed scene records. Similar behavior also exists in `EditorPlatformAssetCookService` and individual packaging seams.

The duplication causes three failures:

1. fixes and new asset features can land in one platform path but not another;
2. output identity and cache behavior depend on which entry point performed the cook; and
3. tests validate implementations rather than one shared behavioral contract.

## Core Model

### Cook request

`EditorAssetCookGraphRequest` contains:

- project authoring session;
- exact engine version;
- platform ID, build profile, and graphics profile;
- platform asset builder capabilities;
- selected root scene references in build order;
- cook root and artifact store;
- current project code/component schema; and
- cancellation and diagnostic sinks.

Every root reference is canonicalized through the authoring session before graph discovery.

### Node identity

Each `EditorAssetCookNode` has a deterministic key derived from:

- canonical source content hash;
- source asset kind;
- current serializer/cook-contract version;
- normalized relevant import or material settings;
- platform capability/profile inputs that affect bytes;
- hashes of dependent cooked artifacts when the node embeds them; and
- processor implementation identity supplied by the platform builder.

Asset ID and source path are provenance, not cook-key inputs unless a runtime format intentionally persists a logical identity. Moving an unchanged source therefore does not force a recook.

### Dependencies

Typed dependency edges include:

- scene to blueprint, model, material, font, image, audio, animation, and nested scene;
- blueprint to its component references;
- material to shaders and textures;
- model to imported materials and generated variants;
- font to atlas texture;
- animation to model or skeleton requirements; and
- shader material to selected shader programs and compiled artifacts.

Discovery produces a directed acyclic graph. A cycle fails with the complete normalized dependency chain. The graph never resolves dependencies opportunistically while writing output.

### Artifact

`CookedAssetArtifact` records:

- cook key;
- runtime asset kind and runtime ID;
- current packaged format version;
- content hash and byte length;
- artifact-store path;
- source-reference provenance;
- dependency cook keys; and
- platform/profile contract.

Artifacts are immutable after publication.

## Graph Phases

### 1. Resolve roots

Resolve every selected authored scene through the project authoring session. Fail before cooking when a required reference cannot resolve.

### 2. Discover and validate

Load current authored assets and component schemas, collect typed dependencies, validate compatible kinds, and topologically sort the graph.

### 3. Compute keys

Normalize settings and platform inputs, then compute cook keys from leaves toward roots. Existing artifacts whose receipt and content hash validate are cache hits.

### 4. Cook leaves

Run required platform builder operations for model, material, texture, shader, audio, or other target-specific bytes. Generic transformations use editor-owned focused processors shared by all platforms.

### 5. Assemble scenes

Replace authored references with runtime artifact identities, apply generic current component transforms once, and serialize current packaged scene assets.

### 6. Publish manifest

Atomically publish new artifacts and an `AssetCookManifest` mapping roots and node keys to immutable artifacts. The later media-layout and packaging phases consume only this manifest.

## Artifact Store

The graph writes into a content-addressed store beneath the stable build cache:

```text
<build-cache>/cook/
  objects/<cook-key>/<runtime-file>
  manifests/<platform>/<profile>/<graph-hash>.json
```

A temporary sibling directory is used until an artifact validates. Publishing an already existing cook key verifies and reuses it. The graph never mutates a published object.

Cache receipts include current contract versions and processor identity, so changing cooking code invalidates affected nodes without relying on timestamps.

## Platform Boundary

The base-platform contract exposes typed capabilities for every platform-dependent leaf. Existing interfaces such as material and shader artifact builders are retained where their current shape is sufficient. Missing asset kinds receive focused interfaces rather than one untyped catch-all request.

The editor owns:

- reference resolution;
- dependency traversal;
- generic scene/component transformation;
- cook-key construction;
- artifact-store publication;
- manifest writing; and
- cache reuse decisions.

The platform builder owns:

- target byte formats;
- target profile interpretation;
- target-specific limits and validation;
- shader compilation or selection;
- texture/model/audio encoding when target-specific; and
- processor implementation identity.

## Consolidation Strategy

The graph is introduced behind one integration seam and then adopts asset kinds incrementally. During a task, an asset kind has exactly one active cook implementation. The old implementation for that kind is deleted as soon as the graph path passes its cross-platform tests.

The final state removes `EditorWindowsBuildScenePackager` as an independent cooker. A small Windows package-layout adapter may remain, but it consumes `AssetCookManifest` and does not resolve or cook authored assets. `SceneComponentPackagingTransformService` is split into focused generic node processors; duplicated file writing and dependency resolution are deleted.

No permanent feature flag keeps old and new cookers alive. Temporary test seams exist only within the implementation branch and are gone from the final commit.

## Determinism and Parallelism

Node discovery and manifest ordering use ordinal normalized keys. Independent nodes may cook concurrently, but parallel completion order cannot affect bytes or manifest order. A cook key is produced once per graph; concurrent requests for the same key share one operation.

Failures cancel dependent nodes but may allow already running independent nodes to finish safely into the immutable store. No root manifest publishes unless all required nodes succeed.

## Diagnostics

Every node records one terminal state: cache hit, cooked, skipped because dependent failure, cancelled, or failed. Diagnostics include source reference, platform/profile, processor, cook key, dependencies, elapsed time, and failure path.

The build summary reports:

- root scenes;
- discovered node count by kind;
- cache hits and cooked counts;
- graph and manifest hashes;
- failed node and dependency chain; and
- artifact manifest path.

## Testing Strategy

### Graph tests

- deterministic discovery and topological order;
- complete cycle diagnostics;
- deduplication of shared dependencies;
- cancellation and dependent-failure propagation; and
- parallel execution produces deterministic manifests.

### Key and cache tests

- path-only moves retain cook keys;
- relevant content or settings changes invalidate the node;
- irrelevant settings do not invalidate unrelated kinds;
- platform/profile differences split keys only when their contract affects bytes;
- processor identity changes invalidate affected nodes; and
- corrupt cached objects are rejected and rebuilt.

### Asset-kind contract tests

For scenes, blueprints, models, materials, textures, audio, fonts, animation, shaders, and generated geometry:

- dependency discovery is complete;
- platform requests contain normalized current inputs;
- output runtime references target manifest artifacts; and
- no editor metadata or raw authoring path leaks into packaged bytes.

### Cross-platform tests

- Windows and PS2 use the same graph entry point;
- identical contracts reuse logical artifacts;
- differing contracts produce platform variants;
- package layout adapters consume the same manifest; and
- removing the old cooker leaves no platform-specific editor branch.

### Demodisc integration

- cook all selected demodisc scenes through the graph;
- compare runtime behavior and required artifact inventory with the pre-consolidation baseline;
- run twice and require complete cache hits on the second unchanged run; and
- verify the committed exact engine/platform pin without temporary rewrites.

## Success Criteria

- All platform builds use one editor-owned cook graph.
- Authored dependency traversal and generic transformations exist in one implementation.
- Windows-specific packaging no longer cooks assets.
- Cook outputs are immutable, content-addressed, and deterministically manifested.
- Unchanged second builds reuse every valid artifact.
- Runtime packages contain only current cooked formats and resolved runtime identities.
- Adding a new platform requires platform capability implementations, not another editor packager.
