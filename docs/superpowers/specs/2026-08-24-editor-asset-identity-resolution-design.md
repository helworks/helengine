# Editor Asset Identity and Recovery Design

## Summary

Helengine editor-authored asset references will use three ordered identifiers:

1. a stable asset UUID;
2. a normalized project-relative path;
3. a SHA-256 content hash.

The editor resolves references in that order. The stable UUID preserves references when an asset and its metadata move together. The path recovers an asset whose identity metadata was deleted. The content hash recovers an asset that moved without its metadata. Successful fallback resolution heals the in-memory reference so the corrected UUID, path, and hash are written the next time the owning document is saved.

This design applies to every persisted editor-authored reference to a file beneath the project `assets` directory. It does not convert arbitrary filesystem locations, build output directories, source-code module locations, or internal cooked-resource paths into asset references.

## Goals

- Give every referenced authored asset a stable logical identity that does not change when its contents or import settings change.
- Resolve authored file references by stable UUID, then path, then SHA-256.
- Recover automatically from moved files and deleted identity metadata.
- Give duplicated assets independent UUIDs while preserving their copied import settings.
- Migrate all persisted editor asset-reference surfaces rather than leaving parallel path-only systems.
- Preserve explicit load and build failures when no compatible asset can be resolved.
- Keep editor identity metadata out of packaged runtime content.

## Non-Goals

- Replacing cache identities derived from file contents and import settings.
- Making runtime packages depend on editor metadata files.
- Converting output directories or other location settings into asset references.
- Using content hashes as logical identity.
- Silently repairing malformed identity metadata.

## Existing State

`SceneAssetReference` currently carries `SourceKind`, `RelativePath`, `ProviderId`, and `AssetId`. File-backed references populate only `RelativePath`; their `AssetId` is empty. `EditorSceneAssetReferenceResolver` therefore resolves file-backed models, materials, fonts, textures, animation clips, and audio directly by path.

Asset import sidecars already store `AssetImporterSettings.AssetId` and `SourceChecksum`. The existing importer `AssetId` is a processed-cache identity derived from source contents, importer selection, target platform, and processor settings. It can change when the asset or its settings change, so it cannot serve as a stable logical identity. That cache identity remains unchanged by this design.

Other editor persistence surfaces use path or string identifiers directly. These include blueprint instance metadata, material asset-reference fields, preview bindings, the last-opened scene, selected build scenes, and scene ordering. The migration must cover these surfaces as well as scene component references.

## Chosen Approach

Each referenced authored asset has a dedicated `<asset-file>.hmeta` sidecar. A shared editor identity service indexes those files and creates an immutable general asset-reference value containing the stable UUID, relative path, and content hash.

This approach is preferred over extending importer-specific `.hasset` documents because `.hasset` has different roles for imported sources and authored materials and is not available uniformly for every asset kind. It is preferred over a single project identity database because per-asset sidecars move and merge with their assets and avoid one project-wide merge hotspot.

## Identity Metadata

### File placement

For an authored file at:

```text
assets/Models/Ship.fbx
```

the identity sidecar is:

```text
assets/Models/Ship.fbx.hmeta
```

Identity sidecars are source-controlled project files. They are hidden from normal asset-browser results and are never treated as authored assets themselves. Existing importer sidecars such as `Ship.fbx.hasset` remain separate and keep their current meaning.

### Metadata contents

The metadata document is UTF-8 JSON with camel-case property names. Its initial schema is:

```json
{
  "version": 1,
  "assetId": "4f4f84c3cc0f49f19cc7af53ea2f83c6",
  "formerAssetIds": []
}
```

It contains:

- an explicit metadata schema version;
- the current stable asset UUID;
- zero or more former UUIDs retained after automatic collision repair.

UUIDs use lowercase `Guid.ToString("N")` form. A new `.hmeta` is created when an authored file first becomes the target of a persisted reference. Generated provider assets do not receive `.hmeta` files.

Former UUIDs are aliases, not additional current identities. They exist so references saved before an external copy collision was repaired can use their saved path to identify the re-keyed copy and heal to its new current UUID.

Malformed metadata fails explicitly with the metadata path and validation reason. The editor does not silently replace malformed metadata because that could redirect existing references.

### Atomic operations

Metadata creation and updates use an adjacent temporary file followed by an atomic replacement or rename. Editor-driven asset moves move the source file, its `.hmeta`, and existing importer settings sidecars as one operation. Editor-driven duplication copies import settings but writes a new `.hmeta` with a fresh UUID and no former UUIDs.

## General Asset Reference

One immutable asset-reference value replaces persisted path-only or string-only authored references. A file-backed reference contains:

- `SourceKind = FileSystem`;
- `AssetId`: the stable UUID from `.hmeta`;
- `RelativePath`: normalized path beneath `assets`, using forward slashes;
- `ContentHash`: `sha256:` followed by lowercase hexadecimal SHA-256;
- an empty generated-provider identifier.

A generated reference contains its existing provider and provider-local asset identity. It retains a virtual relative path when current generated-asset behavior requires one and does not require a content hash.

The general value supersedes the scene-specific authoring role of `SceneAssetReference`. Runtime-facing packaged references remain concrete cooked paths or runtime identities produced after editor resolution. The implementation may preserve a compatibility facade while callers migrate, but there must be one canonical persisted authoring-reference shape after the final audit.

Content hash is a recovery snapshot, not identity. When the file identified by UUID is edited, its UUID remains unchanged and the reference's hash is refreshed the next time that reference is canonicalized or saved.

## Identity Index and Hash Cache

One project-scoped editor identity service owns:

- `.hmeta` parsing, creation, and atomic updates;
- lookup by current and former UUID;
- lookup by normalized path;
- asset-kind classification;
- UUID collision detection and repair;
- SHA-256 computation and compatible-kind search;
- canonical-reference creation;
- move and duplicate metadata operations.

Project startup enumerates authored asset paths and metadata but does not hash every file. Hashes are computed when a reference is created, refreshed, or requires content fallback. The disposable local cache is stored at `cache/editor/asset-identity-index.json` beneath the project root and records hashes keyed by normalized path, file length, and last-write timestamp. It remains outside source-controlled assets and is ignored by source control. A cache miss or stale fingerprint recomputes SHA-256 from the file.

Every resolved candidate path is normalized with `Path.GetFullPath` and verified to remain beneath the project `assets` root before the file is read or returned.

## Resolution Algorithm

The resolver accepts an asset reference and the asset kind expected by its consuming field.

### 1. Stable UUID

The index first finds candidates whose current or former UUID matches `AssetId`.

- One compatible current-UUID candidate wins regardless of a stale saved path or hash.
- When a UUID collision or former-UUID alias produces more than one candidate, an exact normalized `RelativePath` match breaks the tie within the UUID tier.
- If no saved-path match exists, the identity service uses recorded ownership from the current index session.
- If ownership is not available, candidates are ordered by normalized project-relative path using `StringComparer.Ordinal`, and the first compatible candidate wins.

Using the saved path to break a duplicate-UUID tie does not reverse the overall priority. All candidates in this step still match the saved UUID or its collision history.

### 2. Relative path

If the UUID tier has no candidate, the resolver checks the exact normalized relative path.

- If the file has valid `.hmeta`, its current UUID becomes the healed reference UUID.
- If `.hmeta` is missing and the saved UUID is not currently owned, a new sidecar adopts the saved UUID.
- If `.hmeta` is missing and the saved UUID is already owned elsewhere, the recovered file receives a fresh UUID so two assets do not acquire the same current identity.

The selected file's current SHA-256 becomes the healed content hash.

### 3. SHA-256

If UUID and path resolution fail, the resolver searches files compatible with the expected asset kind for the saved `ContentHash`.

- One compatible hash match wins.
- Multiple compatible matches are ordered by normalized project-relative path using `StringComparer.Ordinal`; the first wins automatically.
- An unclaimed saved UUID is adopted by a newly created `.hmeta` for the chosen file.
- When the saved UUID is already owned, the chosen file keeps or receives its own UUID and the reference heals to that UUID.

Hash fallback never crosses incompatible asset kinds merely because two files contain identical bytes.

### 4. Failure

If no compatible candidate resolves, the original reference remains available for diagnostics. Loading or building fails explicitly and reports:

- expected asset kind;
- asset UUID;
- relative path;
- content hash;
- each resolution tier attempted.

The editor does not bind a placeholder or unrelated asset silently.

## UUID Collision Repair

Two source files can acquire the same UUID when a user copies both an authored file and its `.hmeta` outside the editor. The identity service repairs this automatically.

- A previously indexed owner retains the UUID.
- Without previous ownership, the candidate with the ordinally smallest normalized relative path retains the UUID.
- Every other candidate receives a fresh UUID.
- Each re-keyed candidate records the duplicated UUID in `FormerAssetIds`.

An older reference whose UUID equals the former UUID and whose path names a re-keyed copy resolves that copy through the former-UUID alias, then heals to the copy's current UUID. This prevents collision repair from redirecting path-qualified old references to the file that retained the original UUID.

## Healing and Dirty State

Resolution returns an `AssetReferenceResolution`-style result containing:

- the resolved absolute path or generated asset handle;
- the canonical healed reference;
- the tier that succeeded;
- whether metadata or reference state changed.

Consumers must replace their in-memory reference with the canonical value. Project documents such as scenes, blueprints, and materials become dirty and write healed values on their next normal save. Editor-local JSON state may be rewritten immediately. A scene or material is not saved automatically merely because it was opened and healed.

Metadata repair is applied immediately because the identity index must not continue operating with missing or duplicate current UUID ownership.

## Persistence Surfaces

The migration covers every persisted reference to an authored asset below `assets`.

### Scenes and blueprints

- Scene and blueprint asset-reference tables use the general reference shape.
- Component asset fields use the same encoding.
- `BlueprintInstanceComponent`, `BlueprintInheritedEntityComponent`, and `BlueprintInheritedComponentMarker` replace `BlueprintAssetPath` with a typed reference.
- Blueprint expansion and packaging resolve the typed blueprint reference before reading source content.

### Materials and nested asset fields

`MaterialAssetProcessorSettings.FieldValues` remains the store for Boolean, text, choice, numeric, and color values. Builder fields declared as `PlatformMaterialFieldKind.AssetReference` move to a typed asset-reference dictionary keyed by the same stable field id.

The editor UI reads and writes the typed dictionary for asset-reference fields. Immediately before invoking an existing platform material builder, the editor resolves those references and projects them into the concrete path or processed-cache identity expected by the current builder request. This preserves the platform builder API while removing persisted string-only authoring references.

Shader, diffuse, normal, emissive, roughness, and future builder-declared asset fields all follow this path. The design does not maintain a hard-coded list limited to today's standard material fields.

### Animation, audio, fonts, models, and textures

All persisted component or asset-document references to these authored asset kinds use the general reference value and shared resolver. Importer cache `AssetId` and `SourceChecksum` remain cache and change-detection data rather than authoring identity.

### Editor workspace state

- last-opened scene;
- locked asset preview binding;
- other persisted asset selections beneath `assets`.

These become typed references in their JSON documents. Location preferences outside `assets` remain paths.

### Build configuration

Authored scene selections and scene-order entries become scene asset references. Generated boot scenes remain generated provider references. Build execution resolves selected scene references to current source paths before cataloging, cooking, or packaging. Output directory fields remain paths.

## Serialization and Legacy Migration

Each persistence boundary owns an explicit version transition.

- The shared editor asset binary serializer advances beyond version 22 and passes the owning asset version into reference readers.
- Scene and blueprint reference tables read the legacy four-field `SceneAssetReference` layout for old versions and the general reference layout for the new version.
- Component payload schemas containing embedded references receive their own version increment and legacy reader path; a new reference payload is never appended blindly to an unversioned nested layout.
- Material import/settings serializers advance their individual versions and add typed asset-reference dictionaries while reading legacy asset-reference strings from `FieldValues`.
- Editor-local JSON documents add typed reference properties while retaining legacy path/string properties as read-only migration inputs for one compatibility period.

When a legacy file-backed reference contains only a path, the loader constructs an incomplete in-memory reference, resolves it through the identity service, creates `.hmeta` if required, computes SHA-256, and stores the canonical reference in memory. The owning file remains readable and is upgraded only on its next normal save.

Generated legacy references retain their provider and asset ids and do not receive file metadata.

## Packaging Boundary

Build and packaging services resolve every authored asset reference before copying, importing, cooking, or rewriting scene payloads. Packaged runtime content contains only the concrete cooked paths and runtime asset identities needed by the target platform.

`.hmeta`, former UUID aliases, editor-relative source paths, and editor recovery hashes are not copied into runtime packages unless a future runtime feature explicitly requires them. Existing runtime `SceneAssetReference` behavior may remain as the resolved packaging output while the editor authoring type becomes general.

## Error Handling

- Blank or invalid UUIDs in `.hmeta` are malformed metadata errors.
- Duplicate current UUIDs are repaired according to the deterministic collision rules.
- A hash read failure reports the file and underlying I/O failure.
- A path outside `assets` is rejected before lookup or hashing.
- A reference with neither usable UUID, path, nor hash is invalid.
- An unresolved required reference fails load or build and preserves the original values in its diagnostic.

Existing explicit failure behavior is retained. Recovery expands the set of references that can resolve; it does not convert failures into best-effort placeholder binding.

## Testing Strategy

### Identity metadata tests

- create and round-trip `.hmeta`;
- reject malformed version and UUID values;
- create metadata atomically;
- keep importer `.hasset` separate;
- hide `.hmeta` from the asset browser;
- move metadata with an asset;
- duplicate import settings while assigning a new UUID.

### Resolver tests

- UUID wins when path and hash are stale;
- current path wins when metadata was deleted;
- missing metadata adopts an unclaimed saved UUID;
- missing metadata mints a new UUID when the saved UUID is owned elsewhere;
- SHA-256 finds a moved file without metadata;
- edited content keeps UUID and refreshes hash;
- former UUID plus path resolves a re-keyed external duplicate;
- duplicate UUID ownership is deterministic;
- multiple hash matches select the ordinally smallest normalized path;
- incompatible asset kinds are excluded from hash fallback;
- unresolved and malformed inputs fail with complete diagnostics;
- resolved paths cannot escape `assets`.

### Persistence migration tests

- version 22 scene and blueprint references load and heal;
- new binary references round-trip UUID, path, and hash;
- every embedded component reference payload supports its legacy and current schema;
- legacy material asset-reference strings migrate into typed fields;
- editor-session, preview, and build JSON documents migrate legacy path/scene-id values;
- saving migrated documents emits only the current typed form.

### Integration tests

- scenes and blueprints survive editor and external moves;
- material shader and texture fields survive moves and deleted metadata;
- animation, audio, font, model, and texture component references heal;
- preview and last-scene state follow moved assets;
- build scene selection and ordering survive scene moves;
- packaging succeeds after recovery and emits no `.hmeta` dependency;
- a repository-wide persisted-reference audit finds no remaining authored path-only or string-only asset fields.

## Implementation Stages

The implementation is one feature delivered through dependent stages:

1. Add the general reference value, `.hmeta` document, identity index, hash cache, collision handling, and resolver.
2. Migrate scene and blueprint reference tables, component payloads, blueprint instance metadata, and their serializers.
3. Migrate material builder-declared asset fields and other nested authored asset dependencies.
4. Migrate editor workspace state and build scene selection/order.
5. Audit persisted editor documents and services, remove remaining direct authored-path resolution entry points, and verify packaging strips editor identity metadata.

The feature is complete only after the final audit and full targeted integration suite pass. Each stage must use test-driven development and preserve compatibility with legacy persisted data until that stage's migration is verified.

## Success Criteria

- Moving an asset with `.hmeta` preserves every editor reference through UUID resolution.
- Deleting `.hmeta` while leaving the asset at its saved path preserves the reference through path recovery and recreates valid identity metadata without creating a duplicate UUID.
- Moving an asset without `.hmeta` preserves the reference through SHA-256 recovery when compatible content exists.
- Copying an asset and its settings through the editor produces an independent UUID.
- Copying an asset and `.hmeta` externally is repaired deterministically, and old path-qualified references heal to the intended copy.
- Every persisted editor-authored asset reference uses the shared typed reference contract.
- Cache identities continue to change according to existing content and processor rules without changing the stable authored UUID.
- Runtime packages contain no dependency on `.hmeta` or editor recovery hashes.
