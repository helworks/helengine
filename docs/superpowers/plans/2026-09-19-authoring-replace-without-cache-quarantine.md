# Authoring Replace Without Cache Quarantine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `replace` authoring mutation never moves the user's existing file into `cache/`; the staged payload is renamed over the destination in one atomic step, so deleting `cache/editor/authoring-mutations` at any moment leaves the destination either original or fully replaced.

**Architecture:** `EditorAuthoringMutationScope.WriteAllBytesAtomically` stages the payload under the operation folder as today, then publishes it with a new `FixedRenameReplace` primitive (Windows `FileRenameInformation` with `ReplaceIfExists`, Linux `renameat2` with flags 0) after proving both inodes. The `destination.old` artifact, the `DestinationOld*` document fields and the `DestinationQuarantined` phase are deleted from `EditorAuthoringMutationJournal`; `RecoverPayloadPublication` finishes a `replace` from the two observable states only: destination still original (publish with replace) or destination already published (retire).

**Tech Stack:** C# / .NET 9, xUnit, Win32 `SetFileInformationByHandle`, Linux `renameat2`.

**Spec:** none. Design rationale: `cache/` must be deletable at any time; on 2026-09-12 an interrupted replace left the only copy of `assets/textures/instructions/controls/generated/ds/l.png.hasset` inside `cache/editor/authoring-mutations/<id>/destination.old` for a week.

## Global Constraints

- Work on `main` of `C:\dev\helworks\helengine`. No worktrees, no branches.
- One class per file, XML `<summary>` on every member, no tuples, nullable disabled, PascalCase fields, braces on the same line, no local functions.
- Production `.cs` under `engine/` must not contain the words `legacy`, `migrate`, `migration`, `upgrade`, `backward compatibility`, `compatibility path`, `fallback`, `alias`, or a `Version <op> N` comparison other than `!=` (`CurrentFormatOnlySourceContractTests`). Old journal documents are rejected, never converted.
- Every probe of a path that may be absent goes through `EditorFileAttributesProbe.TryGetAttributes`, `CaptureVerifiedIdentity` or `TryGetVerifiedSha256` (already exception-free). Recovery must raise zero first-chance exceptions; prove it with a counting test as in `EditorAuthoringMutationJournalTornDocumentRecoveryTests`.
- Never assert substrings of hand-written source in tests. Delete such assertions when they get in the way; do not add new ones.
- Files are CRLF. After editing, `git diff --numstat` must show only the lines you meant to change; if a whole file flips, restore CRLF with `sed -i 's/\r$//; s/$/\r/'`.
- Run tests from Windows PowerShell 5.1 with the full path: `& "C:\Program Files\dotnet\dotnet.exe" test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorAuthoringMutation|FullyQualifiedName~EditorAuthoringTransaction" --nologo -v q 2>$null`. Do not pipe native stderr through `2>&1`.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

---

### Task 1: Atomic replace publication for `WriteAllBytesAtomically`

**Files:**
- Modify: `engine/helengine.editor/managers/asset/EditorAuthoringMutationScope.cs` (`WriteAllBytesAtomically` ~line 1625; `FixedRename` ~line 293; `RenameVerifiedWindowsLeaf` ~line 2110; Linux `RenameLinuxNoReplaceRaw` ~line 1198)
- Modify: `engine/helengine.editor/managers/asset/EditorAuthoringMutationJournal.cs` (`IsFixedDocumentArtifactPath` line 76; `CreateDestinationOldPath`, `RecordDestinationOld`, `DestinationOldIdentityValue`, `DestinationOldHashValue` ~lines 210-285; `ValidateDocument` phase lists; `Recover` ~line 859; `RecoverPayloadPublication`; `ValidateOperationEntries`; `MutationDocument` ~line 2118)
- Modify: `engine/helengine.editor.tests/managers/asset/EditorAuthoringMutationJournalTests.cs` (lines 51, 64, 1538-1560)
- Modify: `engine/helengine.editor.tests/managers/asset/EditorAuthoringMutationJournalTornDocumentRecoveryTests.cs`
- Create: `engine/helengine.editor.tests/managers/asset/EditorAuthoringReplaceCacheDeletionTests.cs`

**Interfaces:**
- Produces: `internal static void FixedRenameReplace(string projectRootPath, string sourcePath, string destinationPath, string expectedSourceIdentity, string expectedDestinationIdentity, string expectedSourceHash, string expectedDestinationHash)` on `EditorAuthoringMutationScope`. Verifies the source identity and hash and the destination identity and hash by path, invokes `InvokeMutationHook("FixedRename.BeforeSyscall")` and `InvokeMutationHook($"FixedRename.BeforeSyscall:{sourceName}->{destinationName}")` exactly like `FixedRename`, then renames the source over the destination atomically. On Windows: open the source through the pinned source scope and call `RenameVerifiedWindowsLeaf(handle, destinationName, replaceExisting: true, destinationScope)`. On Linux: `renameat2` with flags `0` through the pinned parent descriptors. After the syscall, mirror `FixedRename`'s `AfterSyscallBeforeFsync` hook and directory flushes. Throws `InvalidDataException` when either proof fails, `IOException` when the destination is unexpectedly missing.
- Removes: `CreateDestinationOldPath`, `RecordDestinationOld`, `DestinationOldIdentityValue`, `DestinationOldHashValue`, the `DestinationOldRelativePath` / `DestinationOldIdentity` / `DestinationOldHash` fields on `MutationDocument`, the phase `DestinationQuarantined`, and the artifact name `destination.old` everywhere (`IsFixedDocumentArtifactPath`, `ValidateOperationEntries`, `Recover`, `RecoverPayloadPublication`).

- [ ] **Step 1: Write the cache-deletion property test (fails today)**

Create `EditorAuthoringReplaceCacheDeletionTests.cs` with the same fixture shape as `EditorAuthoringMutationJournalTornDocumentRecoveryTests` (temp project root with `assets/`, hook cleared in `Dispose`). One `[Fact]`:

```csharp
/// <summary>
/// Records every mutation hook point a replace over an existing file passes, then for each point cuts a fresh
/// replace there, deletes the whole authoring-mutations cache folder, and asserts the destination is either the
/// original bytes or the replacement bytes. Recovery afterwards must be a no-op that raises nothing.
/// </summary>
[Fact]
public void WriteAllBytesAtomically_CacheDeletedAtAnyCutPoint_LeavesDestinationOriginalOrReplaced() {
    byte[] originalBytes = new byte[] { 1, 2, 3 };
    byte[] replacementBytes = new byte[] { 9, 8, 7, 6 };
    string destination = Path.Combine(ProjectRootPath, "assets", "cache-cut.hasset");
    File.WriteAllBytes(destination, originalBytes);

    List<string> points = new List<string>();
    EditorAuthoringMutationScope.MutationHookForTests = point => points.Add(point);
    try {
        EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, destination, replacementBytes);
    } finally {
        EditorAuthoringMutationScope.MutationHookForTests = null;
    }
    Assert.Equal(replacementBytes, File.ReadAllBytes(destination));
    Assert.NotEmpty(points);

    string journalRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
    for (int cutIndex = 0; cutIndex < points.Count; cutIndex++) {
        File.WriteAllBytes(destination, originalBytes);
        int seen = 0;
        bool interrupted = false;
        int capturedIndex = cutIndex;
        EditorAuthoringMutationScope.MutationHookForTests = point => {
            if (!interrupted && seen == capturedIndex) {
                interrupted = true;
                throw new IOException("injected cache deletion cut at " + point);
            }
            seen++;
        };
        try {
            Assert.ThrowsAny<Exception>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, destination, replacementBytes));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }
        Assert.True(interrupted, $"Cut {cutIndex} ('{points[cutIndex]}') was never reached.");

        if (Directory.Exists(journalRoot)) {
            Directory.Delete(journalRoot, true);
        }

        Assert.True(File.Exists(destination), $"Cut {cutIndex} ('{points[cutIndex]}') lost the destination after the cache was deleted.");
        byte[] actual = File.ReadAllBytes(destination);
        Assert.True(actual.SequenceEqual(originalBytes) || actual.SequenceEqual(replacementBytes),
            $"Cut {cutIndex} ('{points[cutIndex]}') left neither original nor replacement bytes.");

        int firstChanceCount = 0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => firstChanceCount++;
        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }
        Assert.Equal(0, firstChanceCount);
        Assert.Equal(actual, File.ReadAllBytes(destination));
    }
}
```

`Directory.Delete` on the journal root can fail with a sharing violation on Windows if a handle from the cut is still open; the cut path must dispose all scopes (it does today through `using`). If it still fails intermittently, retry the delete up to three times with `Thread.Sleep(50)` inside the test only.

- [ ] **Step 2: Run it and confirm it fails at the quarantine cut**

Expected: the iteration whose point is `FixedRename.BeforeSyscall:cache-cut.hasset->destination.old` (or the `AfterSyscall` hook right after it) fails with "lost the destination after the cache was deleted".

- [ ] **Step 3: Add `FixedRenameReplace` to the scope**

Place it right after `FixedRenameNoReplace`. Reuse the body of `FixedRename` for parent validation, scope acquisition, source proof (`CaptureVerifiedIdentity` + `VerifyExpectedHash`) and hooks. Differences from `FixedRename`: the destination must exist and match `expectedDestinationIdentity` and `expectedDestinationHash` (compare hash with `TryGetVerifiedSha256`), and the syscall replaces. Windows: `sourceScope.OpenVerifiedFileCore(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, true)` then `sourceScope.RenameVerifiedWindowsLeaf(handle, Path.GetFileName(destination), true, destinationScope)`; after the rename, verify the handle's hash still equals `expectedSourceHash` and `CaptureVerifiedIdentity(root, destination)` equals `expectedSourceIdentity`. Linux: add `RenameLinuxReplaceRaw` beside `RenameLinuxNoReplaceRaw` that passes flags `0`, and verify the destination inode afterwards with `TryGetLinuxEntry`. Both platforms flush the source and destination directories the same way `FixedRename` does.

- [ ] **Step 4: Rewrite the publish tail of `WriteAllBytesAtomically`**

After `journal.RecordPublishingPayload(publishingPath);`:

```csharp
if (destinationIdentity == "missing") {
    EditorAuthoringMutationScope.FixedRenameNoReplace(
        projectRootPath,
        publishingPath,
        fullPath,
        journal.PublishingPayloadIdentityValue,
        "missing",
        journal.PublishingPayloadHashValue);
} else {
    // The former destination never leaves its directory: the proven payload is renamed over it in one
    // step, so an interrupted replace leaves either the original or the replacement at the destination.
    EditorAuthoringMutationScope.FixedRenameReplace(
        projectRootPath,
        publishingPath,
        fullPath,
        journal.PublishingPayloadIdentityValue,
        journal.ExpectedDestinationIdentityValue,
        journal.PublishingPayloadHashValue,
        journal.ExpectedDestinationHashValue);
}
journal.ValidatePublishedPayload(fullPath);
journal.MarkPhase("Published");
journal.Complete();
```

Delete the `destinationOldPath` variable and the `CreateDestinationOldPath` call above it. Keep `RequireDestinationIdentity` and its `journal.Complete()` on failure.

- [ ] **Step 5: Delete the quarantine from the journal**

In `EditorAuthoringMutationJournal.cs`: remove `CreateDestinationOldPath`, `RecordDestinationOld`, `DestinationOldIdentityValue`, `DestinationOldHashValue`; remove the three `DestinationOld*` properties from `MutationDocument`; remove `"destination.old"` from `IsFixedDocumentArtifactPath` and `ValidateOperationEntries`; remove `DestinationQuarantined` from every accepted-phase list in `ValidateDocument` (there are two lists; find them with `grep -n DestinationQuarantined`); in `Recover`, the branch `if (!string.IsNullOrWhiteSpace(document.PublishingPayloadRelativePath) || !string.IsNullOrWhiteSpace(document.DestinationOldRelativePath))` becomes `if (!string.IsNullOrWhiteSpace(document.PublishingPayloadRelativePath))`.

In `RecoverPayloadPublication`: delete `destinationOldPath`, `oldIdentity`, `oldHash`, `oldValid` and every branch that reads them. The remaining decision for a non-copy kind is:

```csharp
string payloadPath = publishingValid ? publishingPath : stagedValid ? stagedPath : null;
string payloadIdentity = publishingValid ? publishingIdentity : stagedIdentity;
string payloadHash = publishingValid ? publishingHash : stagedHash;
if (destinationIsPublished) { /* existing: flush, delete leftover payloads, retire */ }
if (destinationIdentity == "missing" && document.ExpectedDestinationIdentity == "missing") { /* existing no-replace publish */ }
if (destinationIsOriginal) {
    if (payloadPath == null) {
        throw new InvalidOperationException($"The authoring mutation '{journalPath}' has no verifiable staged payload.");
    }
    EditorAuthoringMutationScope.FixedRenameReplace(root, payloadPath, destinationPath, payloadIdentity, document.ExpectedDestinationIdentity, payloadHash, document.ExpectedDestinationHash);
    string publishedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
    string publishedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
    if (publishedIdentity != document.StagedIdentity || publishedHash != document.StagedExactHash) {
        throw new InvalidOperationException($"The authoring mutation '{journalPath}' published an unverifiable destination.");
    }
    RetireDocument(root, journalPath);
    return;
}
throw new InvalidOperationException($"The authoring mutation '{journalPath}' has an ambiguous inode publication state.");
```

Delete the helper `destinationIsMissing` if nothing else uses it. Any old on-disk document that still carries `DestinationQuarantined` or `DestinationOldRelativePath` fails `ValidateDocument` and blocks boot with the existing "invalid" error; that is the intended fail-closed behavior, do not add a reader for it.

- [ ] **Step 6: Update the existing tests**

- `MutationScope_ExposesDedicatedFixedNamePrimitives` (line ~51): replace the `CreateDestinationOldPath` reflection assert with `Assert.NotNull(typeof(EditorAuthoringMutationScope).GetMethod("FixedRenameReplace", flags));`.
- `MutationJournalSource_UsesFixedPrimitivesForItsOwnLifecycle` (line ~64): delete the `Assert.Contains("DestinationOld", source, ...)` line. Do not add another source-substring assertion.
- `WriteAllBytesAtomically_PersistsFormerDestinationProofBeforeMovingExistingDestination` (line ~1538): replace with `WriteAllBytesAtomically_CutBeforeReplaceSyscall_RecoveryPublishesThePayloadOverTheOriginal`: cut at `FixedRename.BeforeSyscall:payload.publishing->former-proof-order.hasset`, assert the destination still holds the original bytes and the operation directory exists, run `Recover`, assert the destination holds the replacement bytes and the journal root is empty.
- `EditorAuthoringMutationJournalTornDocumentRecoveryTests`: the replace no longer produces `destination.old`. Cut instead at the `document.next->document.json` promotion that follows `RecordPublishingPayload` (arm on `FixedRename.BeforeSyscall:payload->payload.publishing`, then cut on the next `FixedRename.BeforeSyscall:document.next->document.json`). Replace the `destination.old` assertion with `Assert.Equal(originalBytes, File.ReadAllBytes(destination))` before recovery; keep the zero-first-chance and replacement-bytes assertions after recovery.
- Search the test file for any remaining `destination.old` or `DestinationOld` text and remove it.

- [ ] **Step 7: Run the mutation, transaction and recovery test classes**

Filter: `FullyQualifiedName~EditorAuthoringMutation|FullyQualifiedName~EditorAuthoringTransaction|FullyQualifiedName~EditorAuthoringReplaceCacheDeletion|FullyQualifiedName~EditorFileAttributesProbe`. Expected: all pass (139 before this task plus the new test). Then run `--filter "FullyQualifiedName~CurrentFormatOnlySourceContractTests"`; expected pass.

- [ ] **Step 8: Commit**

```
git add engine/helengine.editor/managers/asset/EditorAuthoringMutationScope.cs engine/helengine.editor/managers/asset/EditorAuthoringMutationJournal.cs engine/helengine.editor.tests/managers/asset/EditorAuthoringMutationJournalTests.cs engine/helengine.editor.tests/managers/asset/EditorAuthoringMutationJournalTornDocumentRecoveryTests.cs engine/helengine.editor.tests/managers/asset/EditorAuthoringReplaceCacheDeletionTests.cs
git commit -m "fix(editor): publish replaced assets with one atomic rename, never through cache" -m "<why: cache must be deletable; destination.old held the only copy>" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
