# Editor Asset Identity and Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every persisted editor-authored asset reference resolve by stable UUID, then project-relative path, then SHA-256, with deterministic duplicate repair and self-healing migration.

**Architecture:** Keep `SceneAssetReference` as the compatibility type name, but extend it into the single canonical five-field authoring-reference value used across scenes, blueprints, materials, workspace state, and build state. A project-scoped identity subsystem owns `.hmeta`, asset classification, hash caching, collision repair, reference creation, and resolution; packaging resolves authoring references before emitting existing runtime paths and ids.

**Tech Stack:** C#/.NET 9, xUnit, `System.Text.Json`, existing HELE binary serializers, existing editor asset/import services.

**Spec:** `docs/superpowers/specs/2026-08-24-editor-asset-identity-resolution-design.md`

## Global Constraints

- Resolution order is stable UUID, normalized project-relative path, then SHA-256.
- File reference hashes use `sha256:` followed by lowercase hexadecimal SHA-256.
- `.hmeta` is UTF-8 camel-case JSON version 1 with `assetId` and `formerAssetIds`.
- Stable UUIDs use lowercase `Guid.ToString("N")` and never change when source content or importer settings change.
- Existing `AssetImporterSettings.AssetId` remains a processed-cache identity and is not reused as the stable authored UUID.
- Multiple compatible SHA-256 matches select the ordinally smallest normalized project-relative path.
- Generated assets remain provider-backed and do not receive `.hmeta` or content hashes.
- Malformed metadata and completely unresolved required references fail explicitly.
- Runtime packages contain no `.hmeta`, former UUIDs, or editor recovery hashes.
- Output directories, code-module locations, and cooked internal paths remain ordinary paths.
- Follow `AGENTS.md`: one class per file, substantive XML comments on every type/member, PascalCase fields, opening braces on declaration lines, no tuples, no nullable annotations, and no local helper functions.
- Preserve the unrelated working-tree changes in `engine/helengine.core/Core.cs`, `engine/helengine.core/managers/input/PointerInteractionSystem.cs`, and `engine/helengine.input/InputSystem.cs`.
- Before changing any test, read `C:\Users\Helena\.codex\plugins\cache\openai-curated-remote\superpowers\6.3.0\skills\test-driven-development\writing-good-tests.md` completely.

---

### Task 1: Canonical Five-Field Asset Reference and Versioned Binary Encoding

**Files:**
- Modify: `engine/helengine.core/assets/raw/scene/SceneAssetReference.cs`
- Modify: `engine/helengine.core/assets/raw/scene/SceneAssetReferenceFactory.cs`
- Modify: `engine/helengine.core/assets/raw/scene/EngineSceneAssetReferenceFactory.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/PackagedAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneComponentBinaryFieldEncoding.cs`
- Modify: `engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs`
- Modify: `engine/helengine.core/assets/raw/scene/SceneComponentAssetRecord.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneAssetReferenceFactoryTests.cs`
- Modify: `engine/helengine.editor.tests/SceneAssetReferenceTestFactory.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`

**Interfaces:**
- Consumes: existing `SceneAssetReferenceSourceKind`, `EngineBinaryReader`, `EngineBinaryWriter`, editor asset binary version 22.
- Produces: immutable `SceneAssetReference.ContentHash`; `SceneAssetReferenceFactory.CreateFileSystemReference(string assetId, string relativePath, string contentHash)`; version-aware reference readers; editor asset binary version 23.

- [ ] **Step 1: Add failing reference-shape and v23 round-trip tests**

Add assertions equivalent to:

```csharp
[Fact]
public void CreateFileSystemReference_ReturnsCanonicalIdentityPathAndHash() {
    SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
        "4f4f84c3cc0f49f19cc7af53ea2f83c6",
        "Models/Ship.fbx",
        "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
    Assert.Equal("4f4f84c3cc0f49f19cc7af53ea2f83c6", reference.AssetId);
    Assert.Equal("Models/Ship.fbx", reference.RelativePath);
    Assert.Equal("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", reference.ContentHash);
    Assert.Equal(string.Empty, reference.ProviderId);
}
```

Add a `BinarySerializationTests` case that writes a `SceneAsset` containing the canonical reference, verifies header version 23, and asserts all five fields round-trip. Add a fixture writer that emits an editor asset version 22 scene with the existing four-field reference and asserts the new reader returns `ContentHash == string.Empty`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SceneAssetReferenceFactoryTests|FullyQualifiedName~BinarySerializationTests" -v:minimal
```

Expected: FAIL because `ContentHash`, `CreateFileSystemReference`, and version 23 support do not exist.

- [ ] **Step 3: Extend the immutable reference and sanctioned factories**

Change the constructor and properties to this shape, preserving the existing four properties:

```csharp
internal SceneAssetReference(
    SceneAssetReferenceSourceKind sourceKind,
    string relativePath,
    string providerId,
    string assetId,
    string contentHash) {
    SourceKind = sourceKind;
    RelativePath = relativePath ?? string.Empty;
    ProviderId = providerId ?? string.Empty;
    AssetId = assetId ?? string.Empty;
    ContentHash = contentHash ?? string.Empty;
}

public string ContentHash { get; }
```

Add `CreateFileSystemReference` validation for a lowercase 32-character hex UUID and `sha256:` plus 64 lowercase hex characters. Preserve the current one-argument file-type factories as legacy construction paths that return blank `AssetId` and `ContentHash`; only legacy readers and generated/test fixtures may use them after Task 4. Generated factories pass an empty hash.

- [ ] **Step 4: Version the editor reference encoding without changing packaged runtime encoding**

In `EditorAssetBinarySerializer`, set `CurrentVersion = 23`, add `AssetReferenceContentHashVersion = 23`, write `ContentHash` after `AssetId`, and pass the owning asset version into `ReadSceneAssetReference` and `ReadSceneAssetReferenceArray`.

Use this reader branch:

```csharp
string contentHash = version >= AssetReferenceContentHashVersion
    ? reader.ReadString()
    : string.Empty;
return SceneAssetReferenceFactory.Rehydrate(sourceKind, relativePath, providerId, assetId, contentHash);
```

Keep `PackagedAssetBinarySerializer` on its existing four-field runtime reference encoding. It must call the five-argument rehydration method with `string.Empty` for the hash.

- [ ] **Step 5: Mark nested component records with their owning reference encoding**

Add `byte AssetReferenceEncodingVersion` to `SceneComponentAssetRecord`. When `EditorAssetBinarySerializer` reads scene/blueprint component records, assign `0` for editor asset versions through 22 and `1` for version 23. Writers assign `1` to new records.

Change `SceneComponentBinaryFieldEncoding` to expose:

```csharp
public const byte CurrentReferenceEncodingVersion = 1;
public static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader, byte referenceEncodingVersion);
public static SceneAssetReference[] ReadOptionalReferenceArray(EngineBinaryReader reader, byte referenceEncodingVersion);
```

Version `0` reads four fields; version `1` reads five. Update `FontAssetScenePersistenceSupport` to forward the explicit version. Do not insert a version byte into legacy opaque component payloads.

- [ ] **Step 6: Update test reference helpers and run GREEN**

Add a five-field overload to `SceneAssetReferenceTestFactory.CreateSerialized` and keep the four-field overload delegating with an empty hash. Run the focused command from Step 2.

Expected: PASS with editor asset version 23 and legacy version 22 coverage.

- [ ] **Step 7: Run the serializer regression slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~BlueprintFileLoadServiceTests|FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptorTests" -v:minimal
```

Expected: PASS. Fix compile errors by forwarding `SceneComponentAssetRecord.AssetReferenceEncodingVersion`; do not make readers guess from remaining stream length.

- [ ] **Step 8: Commit Task 1**

```powershell
rtk git add -- engine/helengine.core/assets/raw/scene/SceneAssetReference.cs engine/helengine.core/assets/raw/scene/SceneAssetReferenceFactory.cs engine/helengine.core/assets/raw/scene/EngineSceneAssetReferenceFactory.cs engine/helengine.core/assets/raw/scene/SceneComponentAssetRecord.cs engine/helengine.core/assets/PackagedAssetBinarySerializer.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor/serialization/scene/SceneComponentBinaryFieldEncoding.cs engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs engine/helengine.editor.tests/serialization/scene/SceneAssetReferenceFactoryTests.cs engine/helengine.editor.tests/SceneAssetReferenceTestFactory.cs engine/helengine.editor.tests/BinarySerializationTests.cs
rtk git commit -m "Add canonical editor asset reference payload"
```

### Task 2: `.hmeta` Persistence and Shared Asset Classification

**Files:**
- Create: `engine/helengine.editor/managers/asset/AssetIdentityMetadataDocument.cs`
- Create: `engine/helengine.editor/managers/asset/AssetIdentityMetadataService.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetPathClassifier.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Create: `engine/helengine.editor.tests/managers/asset/AssetIdentityMetadataServiceTests.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAssetManagerTests.cs`

**Interfaces:**
- Consumes: project `assets` root and existing `AssetEntryKind` extension catalogs.
- Produces: `AssetIdentityMetadataService.Load`, `LoadOrCreate`, `Save`, `GetMetadataPath`; reusable `EditorAssetPathClassifier.Classify` and `ShouldHide`.

- [ ] **Step 1: Write failing metadata round-trip, malformed-data, and browser-hiding tests**

Cover the exact JSON contract:

```csharp
AssetIdentityMetadataDocument document = service.LoadOrCreate(assetPath, string.Empty);
Assert.Matches("^[0-9a-f]{32}$", document.AssetId);
Assert.Empty(document.FormerAssetIds);

string json = File.ReadAllText(assetPath + ".hmeta");
Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
Assert.Contains("\"assetId\"", json, StringComparison.Ordinal);
Assert.Contains("\"formerAssetIds\"", json, StringComparison.Ordinal);
```

Add cases for an explicitly requested UUID, invalid UUID, unsupported version, duplicate former UUID, missing source asset, and `.hmeta` omission from `EditorAssetManager.LoadEntries`.

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetIdentityMetadataServiceTests|FullyQualifiedName~EditorAssetManagerTests" -v:minimal
```

Expected: FAIL because metadata and shared classification types do not exist.

- [ ] **Step 3: Implement the metadata document and strict service**

Use this document shape:

```csharp
public sealed class AssetIdentityMetadataDocument {
    public int Version { get; set; } = 1;
    public string AssetId { get; set; } = string.Empty;
    public List<string> FormerAssetIds { get; set; } = [];
}
```

`LoadOrCreate(assetPath, requestedAssetId)` returns existing valid metadata, or creates metadata using the requested unclaimed UUID string when nonblank and a fresh `Guid.NewGuid().ToString("N")` otherwise. `Load` throws `InvalidOperationException` containing the `.hmeta` path for malformed JSON, version other than 1, invalid current/former UUIDs, duplicate former ids, or a former id equal to the current id.

Write JSON with camel-case names and indentation to an adjacent `.<guid>.tmp`, then use `File.Move(temporaryPath, metadataPath, true)`. Delete the adjacent temporary file in a `finally` block when it still exists.

- [ ] **Step 4: Extract classification and hide all metadata sidecars**

Move extension sets and the `ClassifyEntryKind`, `ShouldHideFile`, and `.hasset` discrimination logic behind `EditorAssetPathClassifier`:

```csharp
public AssetEntryKind Classify(string fullPath);
public bool ShouldHide(string fullPath);
public bool IsAuthoredAsset(string fullPath);
```

`ShouldHide` returns true for `.hmeta` and for importer `.hasset` sidecars, but false for authored material `.hasset`. `EditorAssetManager` delegates to one classifier instance so the identity index and asset browser cannot disagree.

- [ ] **Step 5: Run GREEN and the asset-browser regression slice**

Run the Step 2 command, then:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetBrowser|FullyQualifiedName~EditorAssetManager|FullyQualifiedName~AssetImportManager" -v:minimal
```

Expected: PASS; `.hasset` materials stay visible and `.hmeta` never appears as an asset.

- [ ] **Step 6: Commit Task 2**

```powershell
rtk git add -- engine/helengine.editor/managers/asset/AssetIdentityMetadataDocument.cs engine/helengine.editor/managers/asset/AssetIdentityMetadataService.cs engine/helengine.editor/managers/asset/EditorAssetPathClassifier.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor.tests/managers/asset/AssetIdentityMetadataServiceTests.cs engine/helengine.editor.tests/managers/asset/EditorAssetManagerTests.cs
rtk git commit -m "Add authored asset identity metadata"
```

### Task 3: Hash Cache, Identity Index, and Duplicate UUID Repair

**Files:**
- Create: `engine/helengine.editor/managers/asset/EditorAssetHashCacheDocument.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetHashCacheEntry.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetHashCache.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetIdentityEntry.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetIdentityIndex.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAssetHashCacheTests.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAssetIdentityIndexTests.cs`

**Interfaces:**
- Consumes: `AssetFileHasher`, `AssetIdentityMetadataService`, `EditorAssetPathClassifier`.
- Produces: indexed current/former UUID and path lookups, compatible-kind enumeration, deterministic duplicate repair, cached `sha256:` values.

- [ ] **Step 1: Write failing hash-cache tests**

Test that `GetContentHash(assetPath)` returns `sha256:` plus 64 lowercase hex characters, reuses a cached value when path/length/last-write ticks match, and recomputes after bytes or timestamp change. Verify persistence at `cache/editor/asset-identity-index.json` and verify malformed cache JSON is discarded and rebuilt because this file is explicitly disposable.

- [ ] **Step 2: Run hash-cache tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetHashCacheTests" -v:minimal
```

Expected: FAIL because the cache classes do not exist.

- [ ] **Step 3: Implement the fingerprinted hash cache**

Store entries with these exact properties:

```csharp
public sealed class EditorAssetHashCacheEntry {
    public string RelativePath { get; set; } = string.Empty;
    public long Length { get; set; }
    public long LastWriteUtcTicks { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}
```

Normalize paths relative to `assets`, validate containment before hashing, and use `AssetFileHasher.ComputeHash` with the `sha256:` prefix. Save cache JSON atomically using the same adjacent-file pattern as `.hmeta`.

- [ ] **Step 4: Write failing index and collision-repair tests**

Create two authored files with copied `.hmeta` and assert:

```csharp
index.Refresh();

EditorAssetIdentityEntry owner = index.FindByPath("Models/A.fbx");
EditorAssetIdentityEntry copy = index.FindByPath("Models/B.fbx");
Assert.Equal(duplicatedId, owner.AssetId);
Assert.NotEqual(duplicatedId, copy.AssetId);
Assert.Contains(duplicatedId, copy.FormerAssetIds);
```

Add a second refresh test proving prior ownership wins even when its path is ordinally later, a cold-index test proving ordinal path ownership, and lookups by current UUID, former UUID, path, and `AssetEntryKind`.

- [ ] **Step 5: Run index tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetIdentityIndexTests" -v:minimal
```

Expected: FAIL because the index does not exist.

- [ ] **Step 6: Implement refresh and deterministic collision repair**

`EditorAssetIdentityEntry` exposes `FullPath`, `RelativePath`, `EntryKind`, `AssetId`, and `IReadOnlyList<string> FormerAssetIds`. `EditorAssetIdentityIndex` exposes:

```csharp
public void Refresh();
public EditorAssetIdentityEntry FindByPath(string relativePath);
public IReadOnlyList<EditorAssetIdentityEntry> FindByAssetId(string assetId, AssetEntryKind expectedKind);
public IReadOnlyList<EditorAssetIdentityEntry> EnumerateCompatible(AssetEntryKind expectedKind);
public bool IsCurrentAssetIdOwned(string assetId);
public EditorAssetIdentityEntry RegisterOrRefresh(string fullPath);
```

Keep an in-memory previous-owner map across refreshes. For duplicate current ids, preserve the previous owner when present; otherwise sort normalized paths with `StringComparer.Ordinal`. Re-key every non-owner, append the duplicated id to `FormerAssetIds` once, save metadata immediately, and rebuild maps before returning.

- [ ] **Step 7: Run Task 3 GREEN tests**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetHashCacheTests|FullyQualifiedName~EditorAssetIdentityIndexTests" -v:minimal
```

Expected: PASS with deterministic collision results on repeated refreshes.

- [ ] **Step 8: Commit Task 3**

```powershell
rtk git add -- engine/helengine.editor/managers/asset/EditorAssetHashCacheDocument.cs engine/helengine.editor/managers/asset/EditorAssetHashCacheEntry.cs engine/helengine.editor/managers/asset/EditorAssetHashCache.cs engine/helengine.editor/managers/asset/EditorAssetIdentityEntry.cs engine/helengine.editor/managers/asset/EditorAssetIdentityIndex.cs engine/helengine.editor.tests/managers/asset/EditorAssetHashCacheTests.cs engine/helengine.editor.tests/managers/asset/EditorAssetIdentityIndexTests.cs
rtk git commit -m "Index stable editor asset identities"
```

### Task 4: Ordered Resolution, Canonical Creation, Move, and Duplicate Operations

**Files:**
- Create: `engine/helengine.editor/managers/asset/AssetReferenceResolutionTier.cs`
- Create: `engine/helengine.editor/managers/asset/AssetReferenceResolution.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetReferenceResolver.cs`
- Create: `engine/helengine.editor/managers/asset/EditorAssetFileOperationService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceFactory.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserEntry.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAssetReferenceResolverTests.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAssetFileOperationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneAssetReferenceFactoryTests.cs`

**Interfaces:**
- Consumes: identity index and hash cache from Task 3.
- Produces: `Resolve(SceneAssetReference, AssetEntryKind)`, canonical file-reference creation, editor move and duplicate APIs.

- [ ] **Step 1: Write the complete failing resolution-priority matrix**

Use real files and metadata, not mocked index calls. Cover:

- UUID wins with stale path/hash;
- path recreates missing `.hmeta` and adopts an unclaimed UUID;
- path creates a fresh UUID when the saved UUID is owned;
- hash finds a moved file with missing metadata;
- edited content keeps UUID and refreshes hash;
- former UUID plus saved path selects a re-keyed copy;
- multiple compatible hash matches select the ordinally smallest path;
- incompatible kinds do not match by hash;
- outside-root and unresolved references throw diagnostics containing kind, UUID, path, hash, and tier names.

Assert the result shape:

```csharp
AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);
Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
Assert.Equal(expectedFullPath, result.FullPath);
Assert.Equal(expectedId, result.CanonicalReference.AssetId);
Assert.Equal(expectedRelativePath, result.CanonicalReference.RelativePath);
Assert.StartsWith("sha256:", result.CanonicalReference.ContentHash, StringComparison.Ordinal);
Assert.True(result.ReferenceChanged);
```

- [ ] **Step 2: Run resolver tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetReferenceResolverTests" -v:minimal
```

Expected: FAIL because resolution types do not exist.

- [ ] **Step 3: Implement the exact ordered resolver**

Expose:

```csharp
public AssetReferenceResolution Resolve(SceneAssetReference reference, AssetEntryKind expectedKind);
public SceneAssetReference CreateFileReference(string fullPath, AssetEntryKind expectedKind);
```

For UUID candidates, use exact saved path as the collision/former-id tie-breaker, then recorded current ownership, then ordinal path. If no UUID candidate exists, try exact path. If no path exists and the saved hash is valid, enumerate compatible candidates, compare cached hashes, sort matches ordinally, and take the first. Canonicalize the winner through `SceneAssetReferenceFactory.CreateFileSystemReference`.

When metadata is missing, adopt the saved UUID only when it is valid and unowned; otherwise create a fresh UUID. `ReferenceChanged` compares all five reference fields. `MetadataChanged` reports sidecar creation or re-keying.

- [ ] **Step 4: Write failing move and duplicate tests**

Assert that move carries `asset`, `asset.hmeta`, and `asset.hasset`; duplicate copies the source and importer sidecar but writes a new `.hmeta` without former ids. Verify destination paths stay beneath `assets` and existing destinations fail explicitly.

- [ ] **Step 5: Implement file operations and canonical browser entries**

`EditorAssetFileOperationService` exposes:

```csharp
public void Move(string sourceFullPath, string destinationFullPath);
public void Duplicate(string sourceFullPath, string destinationFullPath);
```

Resolve and verify every absolute source/destination first. Perform paired operations in source, importer-sidecar, identity-sidecar order with rollback that restores already-moved files when a later move fails. Duplication never copies `.hmeta`; it calls `LoadOrCreate` for the destination.

Add stable `AssetId` and `ContentHash` values to file-backed `AssetBrowserEntry`. Make editor `SceneAssetReferenceFactory` require an `EditorAssetReferenceResolver` and delegate file entry creation to `CreateFileReference`; generated entry behavior remains unchanged.

- [ ] **Step 6: Run Task 4 GREEN tests**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorAssetReferenceResolverTests|FullyQualifiedName~EditorAssetFileOperationServiceTests|FullyQualifiedName~SceneAssetReferenceFactoryTests" -v:minimal
```

Expected: PASS for all fallback and file-operation cases.

- [ ] **Step 7: Commit Task 4**

```powershell
rtk git add -- engine/helengine.editor/managers/asset/AssetReferenceResolutionTier.cs engine/helengine.editor/managers/asset/AssetReferenceResolution.cs engine/helengine.editor/managers/asset/EditorAssetReferenceResolver.cs engine/helengine.editor/managers/asset/EditorAssetFileOperationService.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceFactory.cs engine/helengine.editor/components/ui/asset/AssetBrowserEntry.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor.tests/managers/asset/EditorAssetReferenceResolverTests.cs engine/helengine.editor.tests/managers/asset/EditorAssetFileOperationServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneAssetReferenceFactoryTests.cs
rtk git commit -m "Resolve editor assets by id path and hash"
```

### Task 5: Scene and Blueprint Runtime Resolution with Healed Save Metadata

**Files:**
- Create: `engine/helengine.editor/serialization/scene/IEditorAssetReferenceHealingResolver.cs`
- Create: `engine/helengine.editor/serialization/scene/SceneAssetReferenceHealingService.cs`
- Modify: `engine/helengine.editor/serialization/scene/ISceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/ComponentPlatformOverridePayloadService.cs`
- Modify: `engine/helengine.editor/components/persistence/EntityComponentSaveState.cs`
- Modify: `engine/helengine.editor/components/persistence/EntityComponentPlatformOverrideState.cs`
- Modify: `engine/helengine.editor/serialization/blueprint/BlueprintLoadService.cs`
- Modify: `engine/helengine.editor/serialization/blueprint/BlueprintFileLoadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/LoadedEditorSceneDocument.cs`
- Modify: `engine/helengine.editor/serialization/blueprint/LoadedEditorBlueprintDocument.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor.tests/testing/AnySceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor.tests/testing/TestSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/blueprint/BlueprintFileLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`

**Interfaces:**
- Consumes: `EditorAssetReferenceResolver.Resolve` and nested payload version from Tasks 1 and 4.
- Produces: runtime loading from healed full paths, replacement of loaded save metadata, `ReferencesHealed` document flags, editor dirty-state notification.

- [ ] **Step 1: Write failing moved-model and deleted-metadata scene integration tests**

Build a scene whose model reference points to an old path but whose `.hmeta` moved with the model. Load it and assert the runtime model materializes, `EntityComponentSaveState.TryGetAssetReference` returns the new canonical path/hash, and `LoadedEditorSceneDocument.ReferencesHealed` is true. Add the same path-recovery case for deleted `.hmeta` and one blueprint file case.

- [ ] **Step 2: Run integration tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSceneAssetReferenceResolverTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~BlueprintFileLoadServiceTests" -v:minimal
```

Expected: FAIL because the editor resolver still combines `AssetsRootPath` with `RelativePath` directly and does not return healed state.

- [ ] **Step 3: Add healing tracking without making references mutable**

Define:

```csharp
public interface IEditorAssetReferenceHealingResolver {
    void BeginReferenceHealing();
    IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> CompleteReferenceHealing();
    IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> CancelReferenceHealing();
}
```

Add `AudioAsset ResolveAudio(SceneAssetReference reference)` to `ISceneAssetReferenceResolver`, `AnySceneAssetReferenceResolver`, `TestSceneAssetReferenceResolver`, `BlueprintPackagedSceneExpansionService.NullSceneAssetReferenceResolver`, and `ComponentPlatformEditingService.ThrowingSceneAssetReferenceResolver`. Extend `AutomaticScriptComponentPersistenceDescriptor` to route `AudioAsset` through that method.

`EditorSceneAssetReferenceResolver` starts a per-load reference map. Every file-backed `ResolveModel`, `ResolveMaterial`, `ResolveFont`, `ResolveTexture`, `ResolveAnimationClip`, and `ResolveAudio` call first invokes the shared identity resolver with the domain `AssetEntryKind`, loads from `AssetReferenceResolution.FullPath`, and records old-to-canonical mapping when changed. Track resolved audio assets in the active `RuntimeSceneOwnedAssetSet` just like textures and fonts. Generated paths retain current provider behavior.

- [ ] **Step 4: Replace loaded save-state references centrally**

Add `ReplaceAssetReference(SceneAssetReference previous, SceneAssetReference canonical)` to `EntityComponentSaveState` and `EntityComponentPlatformOverrideState`. It replaces dictionary values by object identity or matching five-field value and returns whether anything changed.

`SceneAssetReferenceHealingService.Apply(IReadOnlyList<EditorEntity> roots, IReadOnlyDictionary<SceneAssetReference, SceneAssetReference> replacements)` recursively visits each `EntitySaveComponent`, base save state, and platform/environment override state. It returns true when at least one reference changes.

- [ ] **Step 5: Integrate load scopes and nested reference encoding**

Wrap `SceneFileLoadService` and `BlueprintFileLoadService` materialization in `BeginReferenceHealing`/`CompleteReferenceHealing`, apply replacements before returning, and set `ReferencesHealed`. On failure call `CancelReferenceHealing` before asset cleanup.

Pass each `SceneComponentAssetRecord.AssetReferenceEncodingVersion` into `AutomaticScriptComponentPersistenceDescriptor`, `ComponentPlatformOverridePayloadService`, and all `SceneComponentBinaryFieldEncoding` reads. New writes always use encoding version 1.

- [ ] **Step 6: Mark an opened scene dirty when recovery healed references**

In `EditorSession` construct one project-scoped metadata service, classifier, hash cache, identity index, and asset reference resolver before constructing scene services. When a loaded document reports `ReferencesHealed`, call the existing untracked scene mutation path once after the new scene becomes current. Do not auto-save.

- [ ] **Step 7: Run GREEN and scene persistence regressions**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSceneAssetReferenceResolverTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~BlueprintFileLoadServiceTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~EditorSessionSceneOpenTests" -v:minimal
```

Expected: PASS; healed scenes are dirty, untouched scenes are not.

- [ ] **Step 8: Commit Task 5**

```powershell
rtk git add -- engine/helengine.editor/serialization/scene/IEditorAssetReferenceHealingResolver.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceHealingService.cs engine/helengine.editor/serialization/scene/ISceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor/serialization/scene/SceneFileLoadService.cs engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/ComponentPlatformOverridePayloadService.cs engine/helengine.editor/components/persistence/EntityComponentSaveState.cs engine/helengine.editor/components/persistence/EntityComponentPlatformOverrideState.cs engine/helengine.editor/serialization/blueprint/BlueprintLoadService.cs engine/helengine.editor/serialization/blueprint/BlueprintFileLoadService.cs engine/helengine.editor/serialization/scene/LoadedEditorSceneDocument.cs engine/helengine.editor/serialization/blueprint/LoadedEditorBlueprintDocument.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor.tests/testing/AnySceneAssetReferenceResolver.cs engine/helengine.editor.tests/testing/TestSceneAssetReferenceResolver.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/serialization/blueprint/BlueprintFileLoadServiceTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs
rtk git commit -m "Heal scene and blueprint asset references"
```

### Task 6: Blueprint Instance References and Legacy Component Migration

**Files:**
- Create: `engine/helengine.editor/serialization/LegacyAssetReferenceInputAttribute.cs`
- Modify: `engine/helengine.editor/components/authoring/BlueprintInstanceComponent.cs`
- Modify: `engine/helengine.editor/components/authoring/BlueprintInheritedEntityComponent.cs`
- Modify: `engine/helengine.editor/components/authoring/BlueprintInheritedComponentMarker.cs`
- Modify: `engine/helengine.editor/managers/scene/BlueprintEditorExpansionService.cs`
- Modify: `engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/serialization/blueprint/BlueprintSaveServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/BlueprintSceneEmbeddingTests.cs`

**Interfaces:**
- Consumes: canonical reference factory/resolver.
- Produces: `BlueprintAssetReference` typed properties and legacy `BlueprintAssetPath` read migration.

- [ ] **Step 1: Write failing blueprint-move and legacy-path tests**

Assert that a blueprint instance created from an asset browser entry stores a canonical reference, survives a moved blueprint by UUID, and packages from the resolved full path. Add a legacy component payload containing only `BlueprintAssetPath` and assert it migrates to `BlueprintAssetReference` during load.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BlueprintSaveServiceTests|FullyQualifiedName~EditorSceneCreationServiceTests|FullyQualifiedName~BlueprintSceneEmbeddingTests" -v:minimal
```

Expected: FAIL because blueprint authoring components persist paths.

- [ ] **Step 3: Add typed properties and one-version legacy inputs**

Create `[AttributeUsage(AttributeTargets.Property)] public sealed class LegacyAssetReferenceInputAttribute : Attribute` and apply it to each legacy path property. Each component gets:

```csharp
public SceneAssetReference BlueprintAssetReference { get; set; }
public string BlueprintAssetPath { get; set; } = string.Empty;
```

New code writes only `BlueprintAssetReference`. Deserialization accepts `BlueprintAssetPath` when the typed reference is absent, creates a legacy path-only reference, canonicalizes it as `AssetEntryKind.Blueprint`, assigns the typed property, and clears the legacy string before the next save.

- [ ] **Step 4: Resolve before expansion and packaging**

Replace every direct `Path.Combine(assetsRoot, BlueprintAssetPath)` with `EditorAssetReferenceResolver.Resolve(reference, AssetEntryKind.Blueprint).FullPath`. Inherited markers receive the same canonical reference as their instance root.

- [ ] **Step 5: Run GREEN and commit**

Run the Step 2 command. Expected: PASS. Commit with:

```powershell
rtk git add -- engine/helengine.editor/serialization/LegacyAssetReferenceInputAttribute.cs engine/helengine.editor/components/authoring/BlueprintInstanceComponent.cs engine/helengine.editor/components/authoring/BlueprintInheritedEntityComponent.cs engine/helengine.editor/components/authoring/BlueprintInheritedComponentMarker.cs engine/helengine.editor/managers/scene/BlueprintEditorExpansionService.cs engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/serialization/blueprint/BlueprintSaveServiceTests.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/BlueprintSceneEmbeddingTests.cs
rtk git commit -m "Reference blueprints through stable asset identity"
```

### Task 7: Typed Material Asset Fields and Builder Projection

**Files:**
- Modify: `engine/helengine.editor/managers/asset/MaterialAssetProcessorSettings.cs`
- Modify: `engine/helengine.editor/managers/asset/MaterialAssetCommonSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/asset/MaterialAssetPlatformOverrideDocument.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetReferenceMigrationService.cs`
- Create: `engine/helengine.editor/managers/asset/MaterialAssetReferenceProjectionService.cs`
- Modify: `engine/helengine.editor/serialization/MaterialAssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/MaterialAssetCommonSettingsDocumentBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/MaterialAssetPlatformOverrideDocumentBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs`
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformCookWorkItemFactory.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/MaterialAssetSettingsServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`

**Interfaces:**
- Consumes: builder `PlatformMaterialFieldKind.AssetReference`, canonical resolver.
- Produces: typed `AssetReferenceValues`; material serializer version 2; resolved string projection only at the platform-builder boundary.

- [ ] **Step 1: Write failing serializer and legacy migration tests**

Add `AssetReferenceValues` expectations:

```csharp
settings.Processor.Platforms["windows"].AssetReferenceValues["texture-id"] = canonicalTextureReference;

MaterialAssetImportSettings roundTrip = SerializeAndRead(settings);
Assert.Equal(canonicalTextureReference.AssetId, roundTrip.Processor.Platforms["windows"].AssetReferenceValues["texture-id"].AssetId);
Assert.False(roundTrip.Processor.Platforms["windows"].FieldValues.ContainsKey("texture-id"));
```

Write a version 1 material settings fixture with `FieldValues["texture-id"] = "Images/Logo.png"`, run migration with a schema whose field kind is `AssetReference`, and assert the value moves into the typed dictionary as a canonical reference. Non-asset fields remain unchanged.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~MaterialAssetSettingsServiceTests" -v:minimal
```

Expected: FAIL because typed material references and serializer version 2 do not exist.

- [ ] **Step 3: Add typed storage and version 2 encoding**

Initialize:

```csharp
public Dictionary<string, SceneAssetReference> AssetReferenceValues { get; set; }
```

Set it to a case-insensitive dictionary in the constructor. Keep `FieldValues` for every non-reference kind. Advance `MaterialAssetImportSettingsBinarySerializer` and `MaterialAssetCommonSettingsDocumentBinarySerializer` from version 1 to version 2, and `MaterialAssetPlatformOverrideDocumentBinarySerializer` from version 2 to version 3. Each current serializer writes `FieldValues` followed by the typed reference count and five-field references. The respective previous versions read only `FieldValues` and leave `AssetReferenceValues` empty.

- [ ] **Step 4: Migrate using builder-declared field kinds**

`MaterialAssetReferenceMigrationService.Migrate(MaterialAssetProcessorSettings settings, IReadOnlyList<PlatformMaterialFieldDefinition> fields)` iterates definitions with `FieldKind == AssetReference`. For each legacy nonblank string, resolve it as a relative path or existing imported asset id through the identity/index service, store the canonical reference, and remove the string key. Generated shader ids become generated provider references rather than `.hmeta` file references.

- [ ] **Step 5: Update UI and builder projection**

Asset picker controls read/write `AssetReferenceValues`. `MaterialAssetReferenceProjectionService.CreateResolvedFieldValues` copies non-reference `FieldValues`, resolves every declared asset-reference field, and inserts the concrete legacy string required by that builder: authored source-relative path for shader/material definitions and processed importer `AssetId` where the existing cook contract requires cache identity.

Only cook request construction calls this projection. Persisted settings never receive projected path/cache strings.

- [ ] **Step 6: Run material and cook GREEN tests**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~MaterialAssetSettingsServiceTests|FullyQualifiedName~EditorPlatformCookWorkItemFactoryTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~BinarySerializationTests" -v:minimal
```

Expected: PASS for legacy settings, typed round-trip, UI selection, and resolved cook requests.

- [ ] **Step 7: Commit Task 7**

```powershell
rtk git add -- engine/helengine.editor/managers/asset/MaterialAssetProcessorSettings.cs engine/helengine.editor/managers/asset/MaterialAssetCommonSettingsDocument.cs engine/helengine.editor/managers/asset/MaterialAssetPlatformOverrideDocument.cs engine/helengine.editor/managers/asset/MaterialAssetReferenceMigrationService.cs engine/helengine.editor/managers/asset/MaterialAssetReferenceProjectionService.cs engine/helengine.editor/serialization/MaterialAssetImportSettingsBinarySerializer.cs engine/helengine.editor/serialization/MaterialAssetCommonSettingsDocumentBinarySerializer.cs engine/helengine.editor/serialization/MaterialAssetPlatformOverrideDocumentBinarySerializer.cs engine/helengine.editor/managers/asset/MaterialAssetSettingsService.cs engine/helengine.editor/components/ui/MaterialAssetView.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/managers/project/EditorPlatformCookWorkItemFactory.cs engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/managers/asset/MaterialAssetSettingsServiceTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs
rtk git commit -m "Persist typed material asset references"
```

### Task 8: Workspace and Preview State Migration

**Files:**
- Create: `engine/helengine.editor/serialization/SceneAssetReferenceJsonConverter.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorSessionStateDocument.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorSessionStateService.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanelStateDocument.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/managers/workspace/EditorSessionStateServiceTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`

**Interfaces:**
- Consumes: canonical reference resolver and five-field factory.
- Produces: JSON reference converter; `LastSceneReference`; preview `AssetReference`; immediate local-state healing.

- [ ] **Step 1: Write failing JSON, last-scene move, and preview move tests**

Verify JSON round-trip contains `sourceKind`, `relativePath`, `providerId`, `assetId`, and `contentHash`. Seed legacy `lastScenePath` and `assetRelativePath` documents, load them, and assert services immediately rewrite canonical typed properties. Move the referenced scene/preview asset with `.hmeta` and assert restore follows UUID.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionStateServiceTests|FullyQualifiedName~EditorSessionWorkspaceTests" -v:minimal
```

Expected: FAIL because local state is path-only and immutable references lack a JSON converter.

- [ ] **Step 3: Implement strict JSON conversion**

`SceneAssetReferenceJsonConverter` reads/writes the five fields and calls sanctioned rehydration. Reject missing generated provider/id and malformed canonical file id/hash. Register the converter in session state and workspace layout JSON options.

- [ ] **Step 4: Add typed state with legacy read inputs**

Add `SceneAssetReference LastSceneReference` and `SceneAssetReference AssetReference`. Retain `LastScenePath` and `AssetRelativePath` only as migration inputs. On load, canonicalize typed values; when only a legacy path exists, create/canonicalize a path-only reference. Save clears legacy properties so current JSON emits only typed fields.

External last-scene paths outside `assets` remain supported through the existing ordinary-path field and are not converted.

- [ ] **Step 5: Run GREEN and commit**

Run the Step 2 command. Expected: PASS with immediate local-state rewrite. Commit with:

```powershell
rtk git add -- engine/helengine.editor/serialization/SceneAssetReferenceJsonConverter.cs engine/helengine.editor/managers/workspace/EditorSessionStateDocument.cs engine/helengine.editor/managers/workspace/EditorSessionStateService.cs engine/helengine.editor/components/ui/PreviewPanelStateDocument.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/workspace/EditorSessionStateServiceTests.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs
rtk git commit -m "Heal persisted editor workspace asset state"
```

### Task 9: Build Scene Selection, Ordering, and Packaging Boundary

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildSceneOrderDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialogSceneRow.cs`
- Modify: `engine/helengine.editor/model/BuildDialogAddRequest.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`

**Interfaces:**
- Consumes: canonical scene references, generated-provider references, JSON converter.
- Produces: typed selected scenes and order entries; source-path resolution before cook/package; runtime payload stripping of UUID/hash.

- [ ] **Step 1: Write failing moved-scene build configuration tests**

Seed legacy `selectedSceneIds` and `sceneOrders`, load config, and assert typed references are written. Move a selected scene with `.hmeta`, reload, and assert selection and order follow UUID. Include the generated boot scene as a generated reference and verify it does not create metadata.

- [ ] **Step 2: Write failing packaging-strip test**

Package a scene with a canonical source reference, deserialize the packaged scene through `PackagedAssetBinarySerializer`, and assert the runtime reference has its cooked path/provider identity but `ContentHash == string.Empty`; enumerate package output and assert no file ends with `.hmeta`.

- [ ] **Step 3: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests" -v:minimal
```

Expected: FAIL because build documents and catalog APIs use scene-id strings.

- [ ] **Step 4: Migrate build document types**

Add `List<SceneAssetReference> SelectedSceneReferences` to platform and queue documents and `SceneAssetReference SceneReference` to order documents. Retain old ids as migration inputs. `EditorBuildConfigService` resolves legacy ids through `EditorProjectSceneCatalogService`, canonicalizes them, clears legacy collections on save, and registers the JSON converter.

Represent generated boot scenes with the current generated provider id and stable generated asset id. Keep output paths unchanged.

- [ ] **Step 5: Resolve scene paths at the execution boundary**

Change catalog APIs to accept scene references and return a resolved record containing canonical reference, scene id derived from the current file, and current source path. Build dialog compares UUIDs, not paths. Queue creation copies typed references. Graph runner resolves them immediately before physics feature discovery, cooking, and packaging.

- [ ] **Step 6: Strip authoring identity during packaging**

Every packager rewrite creates a runtime reference with existing cooked path/provider data and an empty recovery hash. Do not enumerate or copy `.hmeta`. `SceneComponentPackagingTransformService` receives already-resolved authored source paths and keeps generated provider behavior.

- [ ] **Step 7: Run GREEN and commit**

Run the Step 3 command. Expected: PASS for legacy migration, moved-scene builds, generated boot scenes, and metadata-free packages. Commit with:

```powershell
rtk git add -- engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor/managers/project/EditorBuildSceneOrderDocument.cs engine/helengine.editor/managers/project/EditorBuildConfigService.cs engine/helengine.editor/managers/project/EditorProjectSceneCatalogService.cs engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/components/ui/BuildDialogSceneRow.cs engine/helengine.editor/model/BuildDialogAddRequest.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor/managers/project/EditorPlatformBuildExecutor.cs engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs
rtk git commit -m "Reference build scenes through stable asset identity"
```

### Task 10: Repository-Wide Persisted Reference Audit and Final Integration

**Files:**
- Create: `engine/helengine.editor.tests/PersistedAssetReferenceAuditTests.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceValidationService.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorSceneModelRefreshService.cs`
- Modify: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- Modify: `engine/helengine.editor/managers/preview/ModelPreviewSource.cs`
- Modify: `engine/helengine.editor/managers/preview/TexturePreviewSource.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

**Interfaces:**
- Consumes: all previous tasks.
- Produces: enforced absence of path-only/string-only authored asset persistence and complete integration proof.

- [ ] **Step 1: Write a failing source-and-reflection audit**

The audit loads editor assembly document/component types and rejects persisted properties whose names match `AssetPath`, `AssetRelativePath`, `BlueprintAssetPath`, or `*AssetId` when their owning type is an editor authoring/state document and the property is a plain string rather than an approved cache/runtime identity. Maintain an explicit allowlist containing only:

- `AssetImporterSettings.AssetId`;
- runtime/cooked asset id types outside editor authoring documents;
- output/source module paths;
- legacy JSON migration input properties marked with a dedicated `[LegacyAssetReferenceInput]` attribute.

Also scan production source for direct combinations of `assets` root plus a persisted reference property outside `EditorAssetReferenceResolver` and packaging projection code.

- [ ] **Step 2: Run the audit and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PersistedAssetReferenceAuditTests" -v:minimal
```

Expected: FAIL listing every remaining persisted path/string surface by type, property, or source file.

- [ ] **Step 3: Route every reported authored reference through the shared contract**

For each audit finding, replace persisted path/string storage with `SceneAssetReference`, migrate the legacy input at its owning serializer/service boundary, and call `EditorAssetReferenceResolver` before filesystem access. Do not add findings to the allowlist unless the value is demonstrably a cache identity, runtime identity, generated provider id, output directory, or code-module location covered by the spec's non-goals.

Update `SceneAssetReferenceInferenceService` to create canonical references through the shared service rather than the one-argument legacy factories. Update validation to require UUID and valid hash for newly authored file references while accepting blank identity/hash only during an explicit legacy migration path or for packaged runtime references.

- [ ] **Step 4: Run audit GREEN**

Run the Step 2 command.

Expected: PASS with no unapproved path-only/string-only persisted authored references.

- [ ] **Step 5: Run the complete targeted integration suite**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetIdentity|FullyQualifiedName~AssetReference|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~BlueprintFileLoadServiceTests|FullyQualifiedName~MaterialAssetSettingsServiceTests|FullyQualifiedName~EditorSessionStateServiceTests|FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~PersistedAssetReferenceAuditTests" -v:minimal
```

Expected: PASS with no warnings or failed tests.

- [ ] **Step 6: Run the smallest full-project regression command**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore -v:minimal
```

Expected: PASS. If a pre-existing unrelated failure occurs, record the exact test and verify it also fails at the parent commit before changing feature code.

- [ ] **Step 7: Verify package and source-control hygiene**

Run:

```powershell
rtk rg -n "\.hmeta" engine/helengine.core engine/helengine.baseplatform
rtk git status --short
rtk git diff --check
```

Expected: core/runtime packaging code contains no `.hmeta` dependency; status contains only intended feature changes plus the three preserved unrelated user modifications; diff check is clean.

- [ ] **Step 8: Commit Task 10**

```powershell
rtk git add -- engine/helengine.editor.tests/PersistedAssetReferenceAuditTests.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceValidationService.cs engine/helengine.editor/managers/scene/EditorSceneModelRefreshService.cs engine/helengine.editor/managers/preview/PreviewSourceResolver.cs engine/helengine.editor/managers/preview/ModelPreviewSource.cs engine/helengine.editor/managers/preview/TexturePreviewSource.cs engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
rtk git commit -m "Complete editor asset identity migration"
```

## Final Verification

- [ ] Every new production method has a test that was observed failing before implementation.
- [ ] A moved asset with `.hmeta` resolves by UUID and heals its path/hash.
- [ ] A deleted `.hmeta` resolves by path without duplicating a current UUID.
- [ ] A moved asset without `.hmeta` resolves by SHA-256.
- [ ] External duplicate UUIDs re-key deterministically and former-id path healing selects the intended copy.
- [ ] Multiple compatible hash matches select the ordinally smallest normalized path.
- [ ] Material, blueprint, workspace, preview, and build references use the typed contract.
- [ ] Legacy editor asset version 22, material version 1, and legacy JSON documents migrate successfully.
- [ ] Import cache ids retain existing content/processor behavior.
- [ ] Packaged outputs contain no `.hmeta` or recovery hash.
- [ ] The repository-wide persisted-reference audit passes.
- [ ] Unrelated user changes remain untouched.
