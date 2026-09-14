# Native authoring filesystem adapters Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Move native Windows/Linux operations out of shared authoring while retaining pinned-handle mutation safety.

**Architecture:** A neutral contracts assembly defines filesystem sessions. Windows and Linux adapters implement the current algorithms; shared editor retains journal, transaction and recovery orchestration. Host composition selects the adapter once.

**Tech Stack:** C#/.NET 9, xUnit, existing platform builder contracts and native host APIs.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Modify: engine/helengine.editor/managers/asset/EditorAuthoringMutationScope.cs and all its callers discovered by symbol search.
- Create: engine/helengine.hostfilesystem/helengine.hostfilesystem.csproj, IAuthoringFileSystem.cs, IAuthoringFileSystemSession.cs, AuthoringVerifiedFile.cs, AuthoringFileIdentity.cs.
- Create: engine/helengine.hostfilesystem.windows/helengine.hostfilesystem.windows.csproj and WindowsAuthoringFileSystem.cs, WindowsAuthoringFileSystemSession.cs, WindowsNativeFileMethods.cs.
- Create: engine/helengine.hostfilesystem.linux/helengine.hostfilesystem.linux.csproj and LinuxAuthoringFileSystem.cs, LinuxAuthoringFileSystemSession.cs, LinuxNativeFileMethods.cs.
- Create: engine/helengine.hostfilesystem.tests/helengine.hostfilesystem.tests.csproj and AuthoringFileSystemContractTests.cs.
- Modify shared authoring graph construction, engine/helengine.editor.windows host construction, CLI composition and test fixtures.
- Retain and extend engine/helengine.editor.tests/managers/asset/EditorAuthoringMutationScopeTests.cs.

### Task 1: Capture the native guarantees in reusable contract tests

**Existing behavior:** AcquireForMutation pins directory ancestry; OpenVerifiedFile verifies the leaf and gives ownership of its stream to the caller. Preserve existing FixedWrite/FixedCreateExclusive/FixedRenameNoReplace/delete/recovery operations, lock semantics and MutationHookForTests race-injection points.

- [ ] Inventory every internal/static public-to-the-assembly entry point and nested verified-file/identity type in EditorAuthoringMutationScope. Build a migration table in the implementation commit description pairing every old API with its replacement; no method is dropped because it is internal.
- [ ] Add parameterized native contract tests covering parent symlink/reparse swaps, leaf replacement, no-replace rename, cross-root rejection, recovery directory flush, exclusive locks, identity mismatch, repeated disposal and errors after disposal.
- [ ] Keep deterministic race hooks at the same algorithmic boundaries. For a race test, swap the pinned path at the hook and assert the operation rejects it or safely addresses the pinned original object; it must never mutate the replacement outside the root.
- [ ] Run existing mutation/journal/recovery tests on Windows before extraction and record counts. Prepare the same fixture for Linux x64 execution.
- [ ] Commit only tests: `test: characterize authoring filesystem guarantees`.

### Task 2: Define the host-neutral contract and extract Windows implementation

**Proposed contract sketch:**
```csharp
public interface IAuthoringFileSystem {
    IAuthoringFileSystemSession Acquire(string projectRootPath, string targetDirectoryPath);
}
public interface IAuthoringFileSystemSession : IDisposable {
    AuthoringVerifiedFile OpenVerifiedFile(string path, FileMode mode, FileAccess access, FileShare share);
    void ReplaceLeaf(string sourcePath, string destinationPath, bool replaceExisting);
    void DeleteLeaf(string path);
}
```
This sketch defines the entry/session shape; carry every recovery/static operation inventoried in Task 1 into documented contract methods before migrating its caller. AuthoringVerifiedFile owns a Stream and opaque AuthoringFileIdentity; neither exposes SafeFileHandle, IntPtr, ABI structs or OS error codes. Preserve any internal handle-based operation inside the session rather than exposing handles across this boundary.

- [ ] Create the three assemblies with net9.0 for contracts/Linux and net9.0-windows for Windows. Contracts reference only BCL; adapters depend on contracts; editor depends only on contracts. Add substantive XML comments to all members.
- [ ] Move Windows interop constants, native structures and pinned-handle operations unchanged into the Windows adapter. Preserve exception translation and last-error capture order.
- [ ] Wire the Windows adapter through authoring-session construction and CLI/app host composition. Remove static global host selection; simultaneous projects must not share mutable native session state.
- [ ] Retain the existing shared journal and recovery ordering. No File.Move/File.Delete fallback replaces verified native operations.
- [ ] Run contract tests plus EditorAuthoringMutationScopeTests and the authoring recovery/journal tests, checking real selected counts.
- [ ] Build shared editor and desktop host; commit `refactor: isolate Windows authoring filesystem`.

### Task 3: Extract Linux implementation and remove shared native code

- [ ] Move openat/mkdirat/renameat2/unlinkat/flock/fsync and directory enumeration operations and the Linux x64 stat layout to LinuxNativeFileMethods and LinuxAuthoringFileSystemSession.
- [ ] Keep Linux x64 support explicit. Other ABIs remain unsupported with the existing clear diagnostic; the migration does not claim macOS or Linux ARM64 support.
- [ ] Add Linux composition at the host boundary and run the same native contract suite on a real Linux x64 filesystem. Mocks and Windows runs cannot satisfy this task.
- [ ] Remove native imports, native structs and OS dispatch from the shared mutation scope after all caller mappings are exercised. Keep any neutral facade only if it still owns meaningful transaction orchestration.
- [ ] Verify disposal closes every pin, lock and stream once, including partial construction and recovery failures.
- [ ] Run Windows and Linux contract suites, shared authoring tests and host builds; commit `refactor: isolate Linux authoring filesystem`.

## Completion

Shared authoring remains strict about identity and containment but has no kernel32/libc imports. Every native operation is accounted for. Both native contract runs are mandatory; if Linux validation is unavailable, report this plan incomplete.
