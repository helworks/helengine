# Deterministic Editor Authoring Design

## Summary

Helengine will expose one public, project-scoped editor authoring session for project tooling. The session owns asset reference creation, identity indexing, hashing, native asset writes, importer setup, imported-runtime resolution, multi-file transactions, and repair reporting.

Generated native files preserve the embedded identity already owned by their destination path. Writing the same logical asset twice produces identical bytes and does not update the file. Multi-file generators stage and publish one complete transaction. Demodisc and other project tools call only this public API and never construct editor internals, depend on process-global project paths, or reflect into the editor application assembly.

## Goals

- Give project tools one obvious public entry point for editor authoring.
- Preserve an existing native asset's ID whenever its path is regenerated.
- Assign a new ID only when a native asset is first created or an external duplicate is repaired.
- Avoid filesystem writes and timestamp changes when output bytes are unchanged.
- Scan authored assets once per authoring session rather than once per reference.
- Keep hash-cache updates in memory and flush them once per commit or disposal.
- Resolve duplicate identities automatically using all persisted evidence.
- Make multi-output generation recoverable after ordinary failure and process interruption.
- Report every automatic repair without requiring a user prompt.
- Remove demodisc reflection, direct serializer usage, importer registration, and duplicate path-safety code.

## Non-Goals

- Supporting historical native or settings formats.
- Making asset IDs content-derived.
- Treating a content hash as logical identity.
- Providing concurrent write transactions against the same project.
- Hiding explicit unresolved-reference or malformed-metadata failures.
- Exposing editor application UI types through the authoring API.

## Public API

Project-authored commands receive the disposable project session through their existing public command context:

```csharp
public void Execute(IEditorCommandContext context) {
    IEditorProjectAuthoringSession session = context.Authoring;

    SceneAssetReference model = session.CreateReference(
        "models/ship.obj",
        AssetEntryKind.Model);

    using EditorAuthoringTransaction transaction = session.BeginTransaction();
    transaction.WriteAsset("scenes/demo.helen", sceneAsset);
    transaction.Commit();

    EditorAssetRepairReport report = session.RepairReport;
}
```

`EditorProjectAuthoringSession` is project-root scoped and owns:

- canonical project and assets roots;
- one `EditorAssetIdentityIndex`;
- one `EditorAssetHashCache`;
- one configured `AssetImportManager` and importer registry;
- one reference resolver;
- the active transaction, if any; and
- an append-only repair report for the session.

`IEditorCommandContext` adds:

```csharp
IEditorProjectAuthoringSession Authoring { get; }
```

The required public authoring operations are:

```csharp
public SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind);
public AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind);
public RuntimeModel LoadImportedRuntimeModel(string relativePath);
public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset);
public EditorAuthoringTransaction BeginTransaction();
public void RefreshExternalChanges();
public EditorAssetRepairReport RepairReport { get; }
```

The editor host creates the concrete `EditorProjectAuthoringSession` with its current importer registrations and owns its lifetime. GUI and CLI command contexts expose the same session interface. Convenience typed reference methods may exist, but they delegate to that session and do not create new indexes. There is no public API that requires `EditorProjectPaths`, `EditorHostImporterFactory`, `AssetSerializer`, or `AssetImportManager` knowledge from project code.

## Session Lifetime and Concurrency

An authoring command opens one session and reuses it for the whole command. Interactive editor sessions own one long-lived authoring session and refresh it from filesystem notifications or explicit external-change boundaries.

Only one write transaction may be active per session. A project-scoped operating-system lock prevents two authoring transactions from committing concurrently against the same project. Read-only reference creation may coexist with the interactive editor's own session only when both observe the same committed index generation.

`RefreshExternalChanges()` reconciles files added, removed, or changed outside the API. Writes performed through the session update the index incrementally and do not trigger a full refresh.

## Stable Native Identity on Write

When writing a native asset:

1. validate and canonicalize the destination beneath `assets`;
2. if a current native destination exists, load its embedded identity;
3. copy that current ID and former-ID set into the new in-memory asset;
4. if the destination is new, accept a valid unowned caller-provided ID or generate one fresh ID;
5. reject a caller-provided ID already owned by another current asset;
6. serialize the current payload deterministically;
7. compare the complete serialized bytes with the current destination; and
8. replace the destination only when bytes differ.

The destination identity is authoritative for an overwrite. Ordinary generation cannot replace it accidentally. Explicit identity repair remains owned by the identity service and is not an option on `WriteAsset`.

`EditorAssetWriteResult` reports:

- normalized relative path;
- final asset ID;
- content hash excluding embedded identity where required by the recovery contract;
- whether the destination was created, changed, or unchanged; and
- whether an existing identity was preserved.

## Deterministic Serialization

All current native writers emit canonical bytes:

- dictionaries and sets are written in ordinal key order;
- unordered reflection results are sorted by stable field identity;
- paths use forward slashes and current casing rules;
- no timestamps, random execution IDs, machine paths, or temporary paths enter payloads;
- floating-point and text encodings use existing fixed binary encodings; and
- embedded asset identity is the only intentionally unique per-asset field.

A serializer-level determinism test serializes the same logical object assembled with different dictionary insertion orders and expects identical bytes.

## Batched Identity Index and Hash Cache

Opening the session performs one metadata enumeration and index refresh. `CreateReference` performs indexed lookup and hashes only the requested asset. It must not construct another resolver or enumerate the full assets tree.

The hash cache remains disposable and path-fingerprinted, but mutations are accumulated in memory. The cache is atomically flushed once when:

- a transaction commits;
- the session is disposed with dirty cache state; or
- an explicit `Flush()` is requested by the host.

No individual hash miss rewrites the complete cache document. Performance tests instrument enumeration, hash, and cache-save counts rather than relying on wall-clock timing.

## Duplicate Identity Resolution

Reference resolution retains the global tier order: asset ID, path, then SHA-256. When one tier has multiple compatible candidates, candidates are scored deterministically:

1. current asset-ID match before former-ID alias;
2. exact normalized saved-path match;
3. exact saved content-hash match;
4. recorded current-session ownership; and
5. ordinal normalized relative path.

The first unique score wins automatically. Lexical ordering is the final total-order fallback, so the resolver never prompts.

An external copy that duplicates embedded or sidecar identity keeps the selected owner's current ID. Other copies receive fresh current IDs and retain the copied ID as a former alias. Resolution records which evidence selected the winner and canonicalizes the reference to the winner's current ID, path, and hash.

## Repair Reporting

Every automatic mutation appends one immutable `EditorAssetRepairRecord` containing:

- repair kind;
- affected relative path;
- previous and current IDs when applicable;
- resolution tier and tie-break evidence;
- owning document when known; and
- human-readable diagnostic text.

Repair kinds include missing external metadata creation, saved-ID adoption, duplicate-ID reassignment, path healing, hash healing, and canonical reference refresh.

The editor displays a non-blocking summary in its Problems or output surface. CLI commands print a concise summary and may write a detailed JSON report beneath `cache/editor/reports`. Reporting never becomes an approval prompt.

## Authoring Transactions

`EditorAuthoringTransaction` stages every output beneath:

```text
cache/editor/authoring-transactions/<transaction-id>/
```

The transaction manifest records destination, staged file, prior existence, prior content hash, and backup location. `Commit()`:

1. validates every staged current-format payload and reference;
2. acquires the project authoring lock;
3. verifies destinations have not changed since staging;
4. writes a committing journal;
5. backs up changed existing destinations inside the transaction directory;
6. atomically replaces changed destinations and skips unchanged destinations;
7. updates the in-memory index and hash cache;
8. marks the journal committed; and
9. removes the transaction directory after durable completion.

If an ordinary exception occurs during publication, the transaction restores every already-replaced destination before releasing the lock. If the process terminates during publication, the next session detects the journal and completes rollback before indexing the project. Recovery handles only transactions produced by this current system; it is not historical format migration.

## Importer Boundary

The editor host supplies its current importer registrations when it creates the concrete session. Registration composition may remain host-owned because it depends on host graphics/audio/importer assemblies, but it is never exposed to project commands.

`LoadImportedRuntimeModel` validates the expected model kind, resolves current import settings, imports or reuses the current cache, and returns the runtime model. Equivalent public operations may later be added for other runtime asset kinds without exposing the manager or registration graph.

Demodisc deletes reflection against `helengine.editor.app.EditorHostImporterFactory`, manual `ContentManager` construction, and manual importer registration.

## Integration with Save Services

Scene, blueprint, material, and generated-asset save services receive the shared authoring session or transaction. They do not create independent resolvers. Interactive saves may use a single-output transaction. Generator commands use one multi-output transaction per command.

The existing `EditorAssetReferenceFactory` and `GeneratedAssetWriteService` are removed after all callers move to the session. They are not retained as forwarding facades.

## Testing Strategy

### Identity and write tests

- first write assigns one embedded ID;
- overwriting with a newly constructed asset preserves that ID;
- a caller-provided duplicate ID is rejected for a new destination;
- identical second write returns `Unchanged` and preserves timestamp;
- logically identical unordered inputs serialize identically;
- content changes preserve ID and update recovery hash; and
- native identity never receives a sidecar.

### Index and performance-contract tests

- one session performs one initial enumeration;
- many reference creations reuse one index;
- session-owned writes update paths incrementally;
- external refresh reconciles additions, moves, and removals;
- many cache misses produce one cache flush; and
- cache corruption causes disposable rebuild without changing authored files.

### Duplicate and repair tests

- current ID outranks former alias;
- saved path breaks an ID tie;
- saved hash breaks the remaining tie;
- recorded ownership and lexical order provide deterministic fallbacks;
- every repair produces the expected record; and
- automatic selection never invokes UI prompting.

### Transaction tests

- successful multi-file commit publishes all outputs;
- validation failure publishes none;
- destination race aborts before replacement;
- injected publication failure restores earlier destinations;
- interrupted committing journal rolls back on next open; and
- unchanged staged files cause no destination writes.

### Public API and demodisc tests

- project tooling references only the public authoring session;
- no project source reflects into editor application assemblies;
- no project source directly calls serializer, import manager, identity index, or global editor project paths;
- two complete demodisc generation passes produce identical tree hashes and no Git diff; and
- generated scenes resolve moved, duplicated, and metadata-recovered assets deterministically.

## Success Criteria

- One project-scoped public API covers reference creation, imported-runtime loading, native writes, and transactions.
- Regenerating an existing native asset never changes its identity accidentally.
- A second identical generation performs zero authored-file writes.
- Reference creation does not rescan the project per call or save the hash cache per hash.
- Duplicate resolution uses asset ID, path, and hash evidence deterministically and reports its actions.
- Failed or interrupted multi-file generation does not leave a partially published project.
- Demodisc contains no reflection or direct knowledge of editor internals.
