using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Pins the directory chain used by one authoring filesystem operation and
    /// exposes no-follow leaf handles for the operation's files.
    /// </summary>
    internal sealed class EditorAuthoringMutationScope : IDisposable {
        const uint GenericRead = 0x80000000;
        const uint GenericWrite = 0x40000000;
        const uint DeleteAccess = 0x00010000;
        const uint FileShareRead = 0x00000001;
        const uint FileShareWrite = 0x00000002;
        const uint FileShareDelete = 0x00000004;
        const uint CreateNew = 1;
        const uint CreateAlways = 2;
        const uint OpenExisting = 3;
        const uint OpenAlways = 4;
        const uint FileFlagWriteThrough = 0x80000000;
        const uint FileFlagBackupSemantics = 0x02000000;
        const uint FileFlagOpenReparsePoint = 0x00200000;
        const uint FileAttributeReparsePoint = 0x00000400;
        const int PosixReadOnly = 0;
        const int PosixWriteOnly = 1;
        const int PosixReadWrite = 2;
        const int PosixCreate = 0x40;
        const int PosixExclusive = 0x80;
        const int PosixDirectory = 0x10000;
        const int PosixNoFollow = 0x20000;
        const int PosixCloseOnExec = 0x80000;
        const int PosixNonBlock = 0x800;
        const int PosixRenameNoReplace = 1;
        const int PosixAtRemovedDirectory = 0x200;
        const int PosixAtSymlinkNoFollow = 0x100;
        const int PosixFileTypeMask = 0xF000;
        const int PosixRegularFileType = 0x8000;
        const int PosixDirectoryFileType = 0x4000;
        const int PosixFGetFlags = 3;
        const int PosixFSetFlags = 4;
        const int PosixFDupFdCloexec = 1030;
        const int PosixLockExclusive = 2;
        const int PosixLockNonBlocking = 4;
        const int PosixInterrupted = 4;
        const int PosixNotFound = 2;
        const int PosixAlreadyExists = 17;
        static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        readonly List<SafeFileHandle> Handles;
        readonly string ProjectRootPath;
        readonly string TargetDirectoryPath;
        bool IsDisposed;

        // A deterministic seam for exercising the proof-to-syscall window.
        // Production callers never set this hook.
        internal static Action<string> MutationHookForTests { get; set; }

        static void InvokeMutationHook(string point) {
            MutationHookForTests?.Invoke(point);
        }

        internal static void FlushContainingDirectoryForRecovery(
            string projectRootPath,
            string path,
            string hookPoint) {
            InvokeMutationHook(hookPoint);
            if (!OperatingSystem.IsLinux()) {
                return;
            }

            string fullPath = Path.GetFullPath(path);
            string directoryPath = Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidDataException($"The authoring recovery path '{path}' has no containing directory.");
            }
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, directoryPath);
            FsyncDirectory(scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(), directoryPath);
        }

        /// <summary>
        /// Identifies the secure filesystem backend selected for the current process.
        /// </summary>
        internal static string FilesystemBackendNameForTests => OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsLinux() && IsSupportedLinuxArchitecture()
                ? "linux"
                : "unsupported";

        EditorAuthoringMutationScope(
            List<SafeFileHandle> handles,
            string projectRootPath,
            string targetDirectoryPath) {
            Handles = handles;
            ProjectRootPath = projectRootPath;
            TargetDirectoryPath = targetDirectoryPath;
        }

        /// <summary>
        /// Opens and pins every directory from the project root to the target.
        /// POSIX uses openat/mkdirat with O_NOFOLLOW; Windows uses non-following
        /// directory handles and verifies each handle's final path.
        /// </summary>
        internal static EditorAuthoringMutationScope AcquireForMutation(string projectRootPath, string targetDirectoryPath) {
            EnsureSupportedPlatform();
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(targetDirectoryPath)) {
                throw new ArgumentException("Mutation target directory must be provided.", nameof(targetDirectoryPath));
            }

            string projectRoot = NormalizeDirectoryIdentity(Path.GetFullPath(projectRootPath));
            string targetDirectory = Path.GetFullPath(targetDirectoryPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(projectRoot, projectRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(targetDirectory, projectRoot);
            EnsureContained(projectRoot, targetDirectory);
            if (!Directory.Exists(projectRoot)) {
                throw new DirectoryNotFoundException($"The project root '{projectRoot}' does not exist.");
            }

            List<string> missingDirectories = new List<string>();
            string existingDirectory = targetDirectory;
            while (!Directory.Exists(existingDirectory)) {
                missingDirectories.Add(existingDirectory);
                string parent = Path.GetDirectoryName(existingDirectory);
                if (string.IsNullOrWhiteSpace(parent) || !IsInside(projectRoot, parent)) {
                    throw new InvalidDataException($"The mutation target '{targetDirectory}' is not beneath the project root.");
                }
                existingDirectory = parent;
            }

            List<string> existingChain = new List<string>();
            string current = existingDirectory;
            while (true) {
                existingChain.Add(current);
                if (string.Equals(current, projectRoot, PathComparison)) {
                    break;
                }
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent) || !IsInside(projectRoot, parent)) {
                    throw new InvalidDataException($"The mutation target '{targetDirectory}' is not beneath the project root.");
                }
                current = parent;
            }

            List<SafeFileHandle> handles = new List<SafeFileHandle>();
            try {
                if (OperatingSystem.IsWindows()) {
                    existingChain.Reverse();
                    foreach (string directory in existingChain) {
                        handles.Add(OpenAndVerifyWindowsDirectory(directory));
                    }

                    missingDirectories.Reverse();
                    foreach (string directory in missingDirectories) {
                        EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directory, projectRoot);
                        if (!CreateDirectoryW(directory, IntPtr.Zero)) {
                            int error = Marshal.GetLastWin32Error();
                            if (error != 183) {
                                throw new Win32Exception(error, $"Could not create '{directory}'.");
                            }
                        }
                        EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directory, projectRoot);
                        handles.Add(OpenAndVerifyWindowsDirectory(directory));
                    }
                } else if (OperatingSystem.IsLinux()) {
                    OpenPosixDirectoryChain(projectRoot, existingChain, missingDirectories, handles);
                } else {
                    throw CreateUnsupportedPlatformException();
                }

                return new EditorAuthoringMutationScope(handles, projectRoot, targetDirectory);
            } catch {
                DisposeHandles(handles);
                throw;
            }
        }

        /// <summary>
        /// Opens a verified regular-file leaf beneath this scope's pinned target
        /// directory. The returned stream owns the verified leaf handle.
        /// </summary>
        internal EditorAuthoringVerifiedFile OpenVerifiedFile(
            string filePath,
            FileMode mode,
            FileAccess access,
            FileShare share) {
            EnsureNotDisposed();
            return OpenVerifiedFileCore(filePath, mode, access, share, false);
        }

        EditorAuthoringVerifiedFile OpenVerifiedFileCore(
            string filePath,
            FileMode mode,
            FileAccess access,
            FileShare share,
            bool includeDelete) {
            EnsureNotDisposed();
            string fullPath = Path.GetFullPath(filePath);
            string parent = Path.GetDirectoryName(fullPath);
            if (!string.Equals(parent, TargetDirectoryPath, PathComparison)) {
                throw new InvalidDataException($"The verified leaf '{filePath}' is not directly beneath the pinned mutation directory.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, ProjectRootPath);
            SafeFileHandle handle = OperatingSystem.IsWindows()
                ? OpenAndVerifyWindowsFile(fullPath, mode, access, share, includeDelete)
                : OperatingSystem.IsLinux()
                    ? OpenAndVerifyPosixFile(Path.GetFileName(fullPath), mode, access)
                    : throw CreateUnsupportedPlatformException();
            try {
                FileStream stream = new FileStream(handle, access, 4096, false);
                if (mode == FileMode.Create || mode == FileMode.Truncate) {
                    stream.SetLength(0);
                }
                return new EditorAuthoringVerifiedFile(stream, fullPath);
            } catch {
                handle.Dispose();
                throw;
            }
        }

        // These fixed-name primitives are intentionally separate from the
        // journal-aware public mutation helpers.  Journal persistence owns the
        // exact names it passes here, so the primitives never create a second
        // operation, quarantine entry, or anonymous temporary name.
        internal static void FixedWrite(
            string projectRootPath,
            string filePath,
            byte[] bytes,
            bool createNew = false) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            string fullPath = Path.GetFullPath(filePath);
            string parent = Path.GetDirectoryName(fullPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, parent);
            using (EditorAuthoringVerifiedFile file = scope.OpenVerifiedFile(
                fullPath,
                createNew ? FileMode.CreateNew : FileMode.Create,
                FileAccess.Write,
                FileShare.None)) {
                file.Stream.Write(bytes, 0, bytes.Length);
                file.Stream.Flush(true);
            }
            if (OperatingSystem.IsLinux()) {
                FsyncDirectory(scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(), parent);
            }
        }

        internal static void FixedCreateExclusive(
            string projectRootPath,
            string filePath,
            byte[] bytes) {
            FixedWrite(projectRootPath, filePath, bytes, createNew: true);
        }

        internal static void FixedRenameNoReplace(
            string projectRootPath,
            string sourcePath,
            string destinationPath,
            string expectedSourceIdentity = null,
            string expectedDestinationIdentity = null,
            string expectedSourceHash = null) {
            FixedRename(projectRootPath, sourcePath, destinationPath, expectedSourceIdentity, expectedDestinationIdentity, expectedSourceHash);
        }

        static void FixedRename(
            string projectRootPath,
            string sourcePath,
            string destinationPath,
            string expectedSourceIdentity,
            string expectedDestinationIdentity,
            string expectedSourceHash) {
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(sourceParent) || string.IsNullOrWhiteSpace(destinationParent)) {
                throw new InvalidDataException("A fixed authoring rename requires contained parent directories.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(source, projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destination, projectRootPath);
            using EditorAuthoringMutationScope sourceScope = AcquireForMutation(projectRootPath, sourceParent);
            EditorAuthoringMutationScope destinationScope = null;
            try {
                destinationScope = string.Equals(sourceParent, destinationParent, PathComparison)
                    ? sourceScope
                    : AcquireForMutation(projectRootPath, destinationParent);

                string sourceIdentityBefore = CaptureVerifiedIdentity(projectRootPath, source);
                if (sourceIdentityBefore == "missing" || sourceIdentityBefore == "unavailable" ||
                    (expectedSourceIdentity != null && !string.Equals(sourceIdentityBefore, expectedSourceIdentity, StringComparison.Ordinal))) {
                    throw new InvalidDataException($"The fixed authoring source '{source}' failed identity verification.");
                }
                string destinationIdentityBefore = CaptureVerifiedIdentity(projectRootPath, destination);
                if (destinationIdentityBefore != "missing") {
                    throw new IOException($"The fixed rename destination '{destination}' already exists.");
                }
                if (expectedDestinationIdentity != null &&
                    !string.Equals(destinationIdentityBefore, expectedDestinationIdentity, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The fixed authoring destination '{destination}' failed identity verification.");
                }
                VerifyExpectedHash(projectRootPath, source, expectedSourceHash, "source");

                // The project lock coordinates supported authoring writers.
                // Namespace changes by an uncooperative same-credential
                // process outside this API are not a supported guarantee.
                // The first check records the durable proof. The hook is
                // deliberately between that proof and the handles/fstat used
                // by the namespace syscall so tests can exercise the race.
                InvokeMutationHook("FixedRename.BeforeSyscall");
                InvokeMutationHook($"FixedRename.BeforeSyscall:{Path.GetFileName(source)}->{Path.GetFileName(destination)}");

                if (OperatingSystem.IsWindows()) {
                    using SafeFileHandle sourceHandle = OpenWindowsIdentityHandle(source, sourceIdentityBefore);
                    string sourceHandleIdentity = DescribeWindowsHandle(sourceHandle);
                    if (!string.Equals(sourceHandleIdentity, sourceIdentityBefore, StringComparison.Ordinal) ||
                        (expectedSourceIdentity != null && !string.Equals(sourceHandleIdentity, expectedSourceIdentity, StringComparison.Ordinal))) {
                        throw new InvalidDataException($"The fixed authoring source '{source}' changed after identity proof.");
                    }
                    VerifyExpectedHash(sourceHandle, expectedSourceHash, "source");

                    if (expectedDestinationIdentity != null && expectedDestinationIdentity != "missing") {
                        throw new InvalidDataException($"The fixed authoring destination '{destination}' failed identity verification.");
                    }

                    InvokeMutationHook("FixedRename.AfterHandleProof");
                    sourceScope.RenameVerifiedWindowsLeaf(
                        sourceHandle,
                        Path.GetFileName(destination),
                        false,
                        destinationScope);
                    InvokeMutationHook("FixedRename.AfterSyscallBeforeFsync");
                    InvokeMutationHook($"FixedRename.AfterSyscallBeforeFsync:{Path.GetFileName(source)}->{Path.GetFileName(destination)}");
                    VerifyExpectedHash(sourceHandle, expectedSourceHash, "published destination");
                } else if (OperatingSystem.IsLinux()) {
                    int sourceParentFd = sourceScope.Handles[sourceScope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                    int destinationParentFd = destinationScope.Handles[destinationScope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                    string sourceName = Path.GetFileName(source);
                    string destinationName = Path.GetFileName(destination);
                    if (!TryGetLinuxEntry(sourceParentFd, sourceName, out PosixStat sourceStatus)) {
                        throw new FileNotFoundException($"The fixed authoring source '{source}' does not exist.");
                    }
                    string sourceStatusIdentity = new PosixEntryIdentity(sourceStatus).Describe();
                    if (!string.Equals(sourceStatusIdentity, sourceIdentityBefore, StringComparison.Ordinal) ||
                        (expectedSourceIdentity != null && !string.Equals(sourceStatusIdentity, expectedSourceIdentity, StringComparison.Ordinal))) {
                        throw new InvalidDataException($"The fixed authoring source '{source}' changed after identity proof.");
                    }
                    VerifyExpectedHash(projectRootPath, source, expectedSourceHash, "source");
                    bool sourceIsDirectory = (sourceStatus.Mode & PosixFileTypeMask) == PosixDirectoryFileType;
                    EnsureLinuxEntryType(sourceStatus, sourceIsDirectory, source);
                    if (!sourceIsDirectory && HasContentProof(expectedSourceHash)) {
                        using SafeFileHandle sourceFile = OpenPosixRegularFileAt(sourceScope.Handles[sourceScope.Handles.Count - 1], sourceName);
                        VerifyExpectedHash(sourceFile, expectedSourceHash, "source");
                    }
                    bool destinationExists = TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat destinationStatus);
                    string destinationStatusIdentity = destinationExists ? new PosixEntryIdentity(destinationStatus).Describe() : "missing";
                    if (!string.Equals(destinationStatusIdentity, destinationIdentityBefore, StringComparison.Ordinal) ||
                        (expectedDestinationIdentity != null && !string.Equals(destinationStatusIdentity, expectedDestinationIdentity, StringComparison.Ordinal))) {
                        throw new InvalidDataException($"The fixed authoring destination '{destination}' changed after identity proof.");
                    }
                    if (destinationExists) {
                        throw new IOException($"The fixed rename destination '{destination}' already exists.");
                    }

                    PosixEntryIdentity verifiedSourceIdentity = new PosixEntryIdentity(sourceStatus);
                    string sourceQuarantine = EditorAuthoringMutationJournal.IsWritingDocument ||
                        EditorAuthoringMutationJournal.IsFixedOperationArtifactPath(source) ||
                        EditorAuthoringMutationJournal.IsFixedOperationArtifactPath(destination) ||
                        EditorAuthoringMutationJournal.IsRecordedTransientPath(source) ||
                        sourceIsDirectory
                        ? null
                        : QuarantineLinuxEntry(
                            sourceParentFd,
                            sourceName,
                            verifiedSourceIdentity,
                            source,
                            projectRootPath,
                            expectedSourceHash,
                            destination,
                            "RollbackPublication");
                    string publicationSourceName = sourceQuarantine ?? sourceName;
                    try {
                        EnsureLinuxIdentity(sourceParentFd, publicationSourceName, verifiedSourceIdentity, source);
                        VerifyExpectedHash(projectRootPath, Path.Combine(sourceParent, publicationSourceName), expectedSourceHash, "verified source");
                        InvokeMutationHook("FixedRename.AfterHandleProof");
                        if (sourceQuarantine != null) {
                            InvokeMutationHook("FixedRename.AfterQuarantineProof");
                        }
                        EnsureLinuxIdentity(sourceParentFd, publicationSourceName, verifiedSourceIdentity, source);
                        VerifyExpectedHash(projectRootPath, Path.Combine(sourceParent, publicationSourceName), expectedSourceHash, "verified source");
                        if (TryGetLinuxEntry(destinationParentFd, destinationName, out _)) {
                            throw new IOException($"The fixed authoring rename destination '{destination}' appeared before publication.");
                        }
                        RenameLinuxNoReplace(
                            sourceParentFd,
                            publicationSourceName,
                            destinationParentFd,
                            destinationName,
                            destination,
                            verifiedSourceIdentity,
                            null);
                        if (sourceQuarantine != null) {
                            EditorAuthoringMutationJournal.MarkTransientPublished(Path.Combine(sourceParent, sourceQuarantine));
                        }
                    } catch (Exception primary) {
                        // A post-syscall durability failure leaves the exact
                        // source inode at the destination and the quarantine
                        // name absent; retain that proven result for journal
                        // recovery. Restore only while the inode is still at
                        // the operation-owned quarantine name.
                        List<Exception> rollbackFailures = new List<Exception>();
                        try {
                            if (sourceQuarantine != null &&
                                TryGetLinuxEntry(sourceParentFd, sourceQuarantine, out PosixStat quarantined) &&
                                verifiedSourceIdentity.Matches(quarantined)) {
                                VerifyExpectedHash(projectRootPath, Path.Combine(sourceParent, sourceQuarantine), expectedSourceHash, "quarantined source");
                                RenameLinuxNoReplace(sourceParentFd, sourceQuarantine, sourceParentFd, sourceName, source, verifiedSourceIdentity);
                            }
                        } catch (Exception rollback) {
                            rollbackFailures.Add(rollback);
                        }
                        if (rollbackFailures.Count != 0) {
                            rollbackFailures.Insert(0, primary);
                            throw new AggregateException($"Could not publish verified authoring source '{source}' and rollback failed.", rollbackFailures);
                        }
                        throw;
                    }
                } else {
                    throw CreateUnsupportedPlatformException();
                }

                string destinationIdentityAfter = CaptureVerifiedIdentity(projectRootPath, destination);
                if (destinationIdentityAfter == "missing" || destinationIdentityAfter == "unavailable" ||
                    (sourceIdentityBefore != "unavailable" && !string.Equals(destinationIdentityAfter, sourceIdentityBefore, StringComparison.Ordinal))) {
                    throw new IOException($"The fixed authoring rename did not publish the verified source at '{destination}'.");
                }
                VerifyExpectedHash(projectRootPath, destination, expectedSourceHash, "published destination");
            } finally {
                if (!ReferenceEquals(destinationScope, sourceScope)) {
                    destinationScope?.Dispose();
                }
            }
        }

        internal static void FixedDeleteVerifiedLeaf(
            string projectRootPath,
            string filePath,
            string expectedIdentity = null,
            string expectedHash = null) {
            string fullPath = Path.GetFullPath(filePath);
            string parent = Path.GetDirectoryName(fullPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, parent);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, projectRootPath);
            string actualIdentity = CaptureVerifiedIdentity(projectRootPath, fullPath);
            if (actualIdentity == "missing") {
                return;
            }
            if (actualIdentity == "unavailable" || (expectedIdentity != null && !string.Equals(actualIdentity, expectedIdentity, StringComparison.Ordinal))) {
                throw new InvalidDataException($"The fixed authoring leaf '{fullPath}' failed identity verification.");
            }
            VerifyExpectedHash(projectRootPath, fullPath, expectedHash, "leaf");
            InvokeMutationHook("FixedDelete.BeforeSyscall");
            if (OperatingSystem.IsWindows()) {
                using SafeFileHandle file = OpenAndVerifyWindowsFile(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    true);
                string handleIdentity = DescribeWindowsHandle(file);
                if (!string.Equals(handleIdentity, actualIdentity, StringComparison.Ordinal) ||
                    (expectedIdentity != null && !string.Equals(handleIdentity, expectedIdentity, StringComparison.Ordinal))) {
                    throw new InvalidDataException($"The fixed authoring leaf '{fullPath}' changed after identity proof.");
                }
                VerifyExpectedHash(file, expectedHash, "leaf");
                InvokeMutationHook("FixedDelete.AfterHandleProof");
                DeleteVerifiedWindowsLeaf(file);
                InvokeMutationHook("FixedDelete.AfterSyscallBeforeFsync");
            } else if (OperatingSystem.IsLinux()) {
                int parentFd = scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                if (!TryGetLinuxEntry(parentFd, Path.GetFileName(fullPath), out PosixStat status)) {
                    return;
                }
                string statusIdentity = new PosixEntryIdentity(status).Describe();
                if (!string.Equals(statusIdentity, actualIdentity, StringComparison.Ordinal) ||
                    (expectedIdentity != null && !string.Equals(statusIdentity, expectedIdentity, StringComparison.Ordinal))) {
                    throw new InvalidDataException($"The fixed authoring leaf '{fullPath}' changed after identity proof.");
                }
                VerifyExpectedHash(projectRootPath, fullPath, expectedHash, "leaf");
                if (HasContentProof(expectedHash)) {
                    using SafeFileHandle file = OpenPosixRegularFileAt(scope.Handles[scope.Handles.Count - 1], Path.GetFileName(fullPath));
                    VerifyExpectedHash(file, expectedHash, "leaf");
                }
                EnsureLinuxEntryType(status, false, fullPath);
                InvokeMutationHook("FixedDelete.AfterHandleProof");
                PosixEntryIdentity candidateIdentity = new PosixEntryIdentity(status);
                // Document artifacts are already owned by the fixed document
                // state machine. Deleting one while that document is being
                // persisted must not allocate another transient entry or
                // recursively persist the journal.
                if (EditorAuthoringMutationJournal.IsWritingDocument ||
                    EditorAuthoringMutationJournal.IsFixedOperationArtifactPath(fullPath) ||
                    EditorAuthoringMutationJournal.IsRecordedTransientPath(fullPath)) {
                    EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                    VerifyExpectedHash(projectRootPath, fullPath, expectedHash, "document artifact");
                    InvokeMutationHook("FixedDelete.BeforeFinalSyscall");
                    EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                    if (UnlinkAt(parentFd, Path.GetFileName(fullPath), 0) != 0) {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove fixed document artifact '{fullPath}'.");
                    }
                    FsyncDirectory(parentFd, parent);
                    return;
                }
                // Public deletion publishes to its journal-owned deleting
                // entry first. All other callers reach this fallback only
                // for an already-owned fixed artifact, so delete directly.
                InvokeMutationHook("FixedDelete.BeforeFinalSyscall");
                EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                VerifyExpectedHash(projectRootPath, fullPath, expectedHash, "leaf");
                if (UnlinkAt(parentFd, Path.GetFileName(fullPath), 0) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove verified authoring leaf '{fullPath}'.");
                }
                FsyncDirectory(parentFd, parent);
            } else {
                throw CreateUnsupportedPlatformException();
            }
        }

        static void VerifyExpectedHash(string projectRootPath, string path, string expectedHash, string label) {
            if (!HasContentProof(expectedHash)) {
                return;
            }
            string actualHash = TryGetVerifiedSha256(projectRootPath, path);
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The fixed authoring {label} '{path}' failed content verification.");
            }
        }

        static void VerifyExpectedHash(SafeFileHandle handle, string expectedHash, string label) {
            if (!HasContentProof(expectedHash)) {
                return;
            }
            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[64 * 1024];
            long offset = 0;
            int read;
            do {
                read = RandomAccess.Read(handle, buffer, offset);
                if (read > 0) {
                    hasher.AppendData(buffer, 0, read);
                    offset += read;
                }
            } while (read > 0);
            string actualHash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The fixed authoring {label} content failed handle-bound verification.");
            }
        }

        static bool HasContentProof(string expectedHash) =>
            !string.IsNullOrWhiteSpace(expectedHash) && expectedHash is not ("missing" or "unavailable" or "directory");

        internal static void FixedDeleteVerifiedDirectoryTree(
            string projectRootPath,
            string directoryPath,
            string containingRoot = null,
            string expectedIdentity = null) {
            string fullPath = Path.GetFullPath(directoryPath);
            string root = string.IsNullOrWhiteSpace(containingRoot) ? projectRootPath : containingRoot;
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, root);
            string actualIdentity = CaptureVerifiedIdentity(projectRootPath, fullPath);
            if (actualIdentity == "missing") {
                return;
            }
            if (actualIdentity == "unavailable" ||
                (expectedIdentity != null && !string.Equals(actualIdentity, expectedIdentity, StringComparison.Ordinal))) {
                throw new InvalidDataException($"The fixed authoring directory '{fullPath}' failed identity verification.");
            }
            InvokeMutationHook("FixedDeleteDirectory.BeforeSyscall");
            FixedDeleteDirectoryTreeCore(projectRootPath, fullPath, root, actualIdentity);
        }

        static void FixedDeleteDirectoryTreeCore(string projectRootPath, string fullPath, string containingRoot, string expectedIdentity = null) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            if (OperatingSystem.IsLinux()) {
                string parentPath = Path.GetDirectoryName(fullPath);
                using EditorAuthoringMutationScope parentScope = AcquireForMutation(projectRootPath, parentPath);
                int parentFd = parentScope.Handles[parentScope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                if (!TryGetLinuxEntry(parentFd, Path.GetFileName(fullPath), out PosixStat status)) {
                    return;
                }
                string statusIdentity = new PosixEntryIdentity(status).Describe();
                if (expectedIdentity != null && !string.Equals(statusIdentity, expectedIdentity, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The fixed authoring directory '{fullPath}' changed after identity proof.");
                }
                EnsureLinuxEntryType(status, true, fullPath);
                PosixEntryIdentity candidateIdentity = new PosixEntryIdentity(status);
                if (EditorAuthoringMutationJournal.IsWritingDocument ||
                    EditorAuthoringMutationJournal.IsFixedOperationArtifactPath(fullPath) ||
                    EditorAuthoringMutationJournal.IsAuthoringMutationDirectoryPath(fullPath) ||
                    EditorAuthoringMutationJournal.IsRecordedTransientPath(fullPath)) {
                    using SafeFileHandle fixedDirectory = OpenPosixDirectory(Path.GetFileName(fullPath), parentScope.Handles[parentScope.Handles.Count - 1]);
                    EnsureLinuxHandleIdentity(fixedDirectory, candidateIdentity, fullPath);
                    FixedDeleteDirectoryContentsLinux(projectRootPath, fixedDirectory, fullPath, containingRoot);
                    EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                    InvokeMutationHook("FixedDeleteDirectory.BeforeFinalSyscall");
                    EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                    if (UnlinkAt(parentFd, Path.GetFileName(fullPath), PosixAtRemovedDirectory) != 0) {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not delete fixed authoring directory '{fullPath}'.");
                    }
                    FsyncDirectory(parentFd, parentPath);
                    return;
                }
                // Public directory deletion has already moved the top-level
                // directory to its journal-owned .deleting entry. If a
                // caller reaches this fallback, recurse under its pinned
                // handle and remove the verified tree directly.
                using SafeFileHandle directory = OpenPosixDirectory(Path.GetFileName(fullPath), parentScope.Handles[parentScope.Handles.Count - 1]);
                EnsureLinuxHandleIdentity(directory, candidateIdentity, fullPath);
                FixedDeleteDirectoryContentsLinux(projectRootPath, directory, fullPath, containingRoot);
                EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                InvokeMutationHook("FixedDeleteDirectory.BeforeFinalSyscall");
                EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), candidateIdentity, fullPath);
                if (UnlinkAt(parentFd, Path.GetFileName(fullPath), PosixAtRemovedDirectory) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not delete fixed authoring directory '{fullPath}'.");
                }
                FsyncDirectory(parentFd, parentPath);
                return;
            }
            if (!OperatingSystem.IsWindows()) {
                throw CreateUnsupportedPlatformException();
            }
            using (SafeFileHandle verifiedDirectory = OpenAndVerifyWindowsDirectory(fullPath, false)) {
                string verifiedIdentity = DescribeWindowsHandle(verifiedDirectory);
                if (expectedIdentity != null && !string.Equals(verifiedIdentity, expectedIdentity, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The fixed authoring directory '{fullPath}' changed after identity proof.");
                }
                InvokeMutationHook("FixedDeleteDirectory.AfterHandleProof");
                // Keep the verified directory handle open while enumerating
                // and retiring every child. This pins the directory entry
                // against a concurrent parent swap for the full operation.
                foreach (string child in Directory.GetFileSystemEntries(fullPath, "*", SearchOption.TopDirectoryOnly).ToArray()) {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(child, containingRoot);
                    string childIdentity = CaptureVerifiedIdentity(projectRootPath, child);
                    if (childIdentity == "missing") {
                        continue;
                    }
                    if (childIdentity.EndsWith(":directory", StringComparison.Ordinal)) {
                        FixedDeleteDirectoryTreeCore(projectRootPath, child, containingRoot, childIdentity);
                    } else {
                        FixedDeleteVerifiedLeaf(projectRootPath, child, childIdentity);
                    }
                }
            }
            // The read-pinned handle protects the enumeration and child
            // deletes. Reopen the now-empty directory with delete access only
            // for its final entry removal.
            InvokeMutationHook("FixedDeleteDirectory.BeforeFinalOpen");
            using SafeFileHandle deleteDirectory = OpenAndVerifyWindowsDirectory(fullPath, true);
            if (!string.Equals(DescribeWindowsHandle(deleteDirectory), expectedIdentity, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The fixed authoring directory '{fullPath}' changed before final removal.");
            }
            DeleteVerifiedWindowsLeaf(deleteDirectory);
        }

        static void FixedDeleteDirectoryContentsLinux(
            string projectRootPath,
            SafeFileHandle directory,
            string directoryPath,
            string containingRoot) {
            int duplicateFd = PosixDup(directory.DangerousGetHandle().ToInt32());
            if (duplicateFd < 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not enumerate fixed authoring directory '{directoryPath}'.");
            }
            IntPtr directoryStream = PosixFdOpenDir(duplicateFd);
            if (directoryStream == IntPtr.Zero) {
                PosixClose(duplicateFd);
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not enumerate fixed authoring directory '{directoryPath}'.");
            }
            try {
                int parentFd = directory.DangerousGetHandle().ToInt32();
                while (true) {
                    IntPtr entry = PosixReadDir(directoryStream);
                    if (entry == IntPtr.Zero) {
                        break;
                    }
                    string name = ReadLinuxDirectoryEntryName(entry);
                    if (name == "." || name == "..") {
                        continue;
                    }
                    string childPath = Path.Combine(directoryPath, name);
                    if (!TryGetLinuxEntry(parentFd, name, out PosixStat status)) {
                        continue;
                    }
                    if ((status.Mode & PosixFileTypeMask) == PosixDirectoryFileType) {
                        PosixEntryIdentity childIdentity = new PosixEntryIdentity(status);
                        using SafeFileHandle childDirectory = OpenPosixDirectory(name, directory);
                        EnsureLinuxHandleIdentity(childDirectory, childIdentity, childPath);
                        FixedDeleteDirectoryContentsLinux(projectRootPath, childDirectory, childPath, containingRoot);
                        EnsureLinuxIdentity(parentFd, name, childIdentity, childPath);
                        if (UnlinkAt(parentFd, name, PosixAtRemovedDirectory) != 0) {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove fixed directory '{childPath}'.");
                        }
                        FsyncDirectory(parentFd, directoryPath);
                    } else {
                        EnsureLinuxEntryType(status, false, childPath);
                        PosixEntryIdentity childIdentity = new PosixEntryIdentity(status);
                        using SafeFileHandle childFile = OpenPosixRegularFileAt(directory, name);
                        EnsureLinuxHandleIdentity(childFile, childIdentity, childPath);
                        if (UnlinkAt(parentFd, name, 0) != 0) {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove fixed leaf '{childPath}'.");
                        }
                        FsyncDirectory(parentFd, directoryPath);
                    }
                }
            } finally {
                PosixClosedDir(directoryStream);
            }
        }

        /// <summary>Atomically moves a verified leaf into a destination leaf.</summary>
        internal void ReplaceLeaf(string sourcePath, string destinationPath, bool replaceExisting) {
            EnsureNotDisposed();
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", sourcePath, destinationPath);
            journal.RequireDestinationIdentity(destinationPath);
            ReplaceLeafCore(sourcePath, destinationPath, replaceExisting);
            journal.MarkPhase("Published");
            journal.Complete();
        }

        void ReplaceLeafCore(string sourcePath, string destinationPath, bool replaceExisting) {
            EnsureNotDisposed();
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            if (!string.Equals(sourceParent, TargetDirectoryPath, PathComparison) ||
                !string.Equals(destinationParent, TargetDirectoryPath, PathComparison)) {
                throw new InvalidDataException("Verified leaf replacement must use the pinned parent directory.");
            }

            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(source, ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destination, ProjectRootPath);
            if (OperatingSystem.IsWindows()) {
                using EditorAuthoringVerifiedFile sourceFile = OpenVerifiedFileCore(
                    source,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    true);
                RenameVerifiedWindowsLeaf(
                    sourceFile.Stream.SafeFileHandle,
                    Path.GetFileName(destination),
                    replaceExisting && File.Exists(destination));
            } else if (OperatingSystem.IsLinux()) {
                ReplaceLinuxLeaf(
                    Handles[Handles.Count - 1].DangerousGetHandle().ToInt32(),
                    Path.GetFileName(source),
                    Path.GetFileName(destination),
                    replaceExisting,
                    source,
                    destination);
            } else {
                throw CreateUnsupportedPlatformException();
            }
        }

        static void ReplaceLinuxLeaf(int parentFd, string sourceName, string destinationName, bool replaceExisting, string sourcePath, string destinationPath) {
            PosixEntryIdentity sourceIdentity = RequireLinuxEntry(parentFd, sourceName, false, destinationPath);
            bool destinationExists = TryGetLinuxEntry(parentFd, destinationName, out PosixStat destinationStatus);
            if (destinationExists && !replaceExisting) {
                throw new IOException($"The verified destination '{destinationPath}' already exists.");
            }
            if (destinationExists) {
                EnsureLinuxEntryType(destinationStatus, false, destinationPath);
            }
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(
                sourceIdentity.Describe(),
                destinationExists ? new PosixEntryIdentity(destinationStatus).Describe() : "missing");

            // Move both verified entries into operation-owned names before
            // publishing. The destination is never exchanged by name: its
            // exact inode remains quarantined until the new source has been
            // published and the old inode has been verified for deletion.
            if (destinationExists) {
                PosixEntryIdentity destinationIdentity = new PosixEntryIdentity(destinationStatus);
                string destinationQuarantine = null;
                string sourceQuarantine = null;
                try {
                    // Reserve the former destination first. The source
                    // publication record is then the last graph edge, so
                    // reverse recovery restores source before destination.
                    destinationQuarantine = QuarantineLinuxEntry(
                        parentFd,
                        destinationName,
                        destinationIdentity,
                        destinationPath,
                        recoveryIntent: "RestoreOriginal");
                    sourceQuarantine = QuarantineLinuxEntry(
                        parentFd,
                        sourceName,
                        sourceIdentity,
                        sourcePath,
                        recoveryIntent: "RollbackPublication",
                        intendedDestinationPath: destinationPath);
                    EnsureLinuxIdentity(parentFd, sourceQuarantine, sourceIdentity, destinationPath);
                    EnsureLinuxIdentity(parentFd, destinationQuarantine, destinationIdentity, destinationPath);
                    InvokeMutationHook("FixedRename.AfterQuarantineProof");
                    EnsureLinuxIdentity(parentFd, sourceQuarantine, sourceIdentity, destinationPath);
                    EnsureLinuxIdentity(parentFd, destinationQuarantine, destinationIdentity, destinationPath);
                    if (TryGetLinuxEntry(parentFd, destinationName, out _)) {
                        throw new IOException($"The verified destination '{destinationPath}' reappeared before replacement.");
                    }
                    RenameLinuxNoReplace(parentFd, sourceQuarantine, parentFd, destinationName, destinationPath, sourceIdentity);
                    EnsureLinuxIdentity(parentFd, destinationName, sourceIdentity, destinationPath);
                    EditorAuthoringMutationJournal.MarkTransientPublished(Path.Combine(Path.GetDirectoryName(sourcePath), sourceQuarantine));
                    EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
                } catch (Exception primary) {
                    List<Exception> replacementRollbackFailures = new List<Exception>();
                    try {
                        bool newDestinationPublished = TryGetLinuxEntry(parentFd, destinationName, out PosixStat currentDestination) &&
                            sourceIdentity.Matches(currentDestination);
                        if (!newDestinationPublished && destinationQuarantine != null &&
                            TryGetLinuxEntry(parentFd, destinationQuarantine, out PosixStat quarantinedDestination) &&
                            destinationIdentity.Matches(quarantinedDestination) &&
                            !TryGetLinuxEntry(parentFd, destinationName, out _)) {
                            RenameLinuxNoReplace(parentFd, destinationQuarantine, parentFd, destinationName, destinationPath, destinationIdentity);
                        }
                    } catch (Exception exception) {
                        replacementRollbackFailures.Add(exception);
                    }
                    try {
                        bool newDestinationPublished = TryGetLinuxEntry(parentFd, destinationName, out PosixStat currentDestination) &&
                            sourceIdentity.Matches(currentDestination);
                        if (!newDestinationPublished && sourceQuarantine != null &&
                            TryGetLinuxEntry(parentFd, sourceQuarantine, out PosixStat quarantinedSource) &&
                            sourceIdentity.Matches(quarantinedSource) && !TryGetLinuxEntry(parentFd, sourceName, out _)) {
                            RenameLinuxNoReplace(parentFd, sourceQuarantine, parentFd, sourceName, destinationPath, sourceIdentity);
                        }
                    } catch (Exception exception) {
                        replacementRollbackFailures.Add(exception);
                    }
                    if (replacementRollbackFailures.Count != 0) {
                        replacementRollbackFailures.Insert(0, primary);
                        throw new AggregateException($"Could not atomically replace verified authoring leaf '{destinationPath}' and rollback failed.", replacementRollbackFailures);
                    }
                    throw;
                }
                return;
            }

            string sourceQuarantineForCreate = QuarantineLinuxEntry(
                parentFd,
                sourceName,
                sourceIdentity,
                sourcePath,
                recoveryIntent: "RollbackPublication",
                intendedDestinationPath: destinationPath);
            bool published = false;
            List<Exception> rollbackFailures = new List<Exception>();
            try {
                // Mark the operation before entering the rename helper: its
                // durability step can fail after the directory entry has
                // already moved. Rollback therefore always verifies the
                // destination inode before attempting to restore it.
                published = true;
                RenameLinuxNoReplace(parentFd, sourceQuarantineForCreate, parentFd, destinationName, destinationPath, sourceIdentity);
                EnsureLinuxIdentity(parentFd, destinationName, sourceIdentity, destinationPath);
                EditorAuthoringMutationJournal.MarkTransientPublished(Path.Combine(Path.GetDirectoryName(sourcePath), sourceQuarantineForCreate));
                EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            } catch (Exception primary) {
                try {
                    if (published && TryGetLinuxEntry(parentFd, destinationName, out PosixStat publishedStatus) &&
                        sourceIdentity.Matches(publishedStatus)) {
                        RenameLinuxNoReplace(parentFd, destinationName, parentFd, sourceQuarantineForCreate, destinationPath, sourceIdentity);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                try {
                    if (sourceQuarantineForCreate != null && TryGetLinuxEntry(parentFd, sourceQuarantineForCreate, out PosixStat ignoredSource)) {
                        RenameLinuxNoReplace(parentFd, sourceQuarantineForCreate, parentFd, sourceName, destinationPath, sourceIdentity);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                if (rollbackFailures.Count != 0) {
                    rollbackFailures.Insert(0, primary);
                    throw new AggregateException($"Could not replace verified authoring leaf '{destinationPath}' and rollback failed.", rollbackFailures);
                }
                throw;
            }
        }

        static void MoveLinuxLeaf(int sourceParentFd, string sourceName, int destinationParentFd, string destinationName, string sourcePath, string destinationPath) {
            PosixEntryIdentity sourceIdentity = RequireLinuxEntry(sourceParentFd, sourceName, false, destinationPath);
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(sourceIdentity.Describe(), "missing");
            if (TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat destinationStatus)) {
                EnsureLinuxEntryType(destinationStatus, false, destinationPath);
                throw new IOException($"The verified destination '{destinationPath}' already exists.");
            }

            string sourceQuarantine = QuarantineLinuxEntry(
                sourceParentFd,
                sourceName,
                sourceIdentity,
                sourcePath,
                recoveryIntent: "RollbackPublication",
                intendedDestinationPath: destinationPath);
            bool published = false;
            try {
                published = true;
                RenameLinuxNoReplace(sourceParentFd, sourceQuarantine, destinationParentFd, destinationName, destinationPath, sourceIdentity);
                EnsureLinuxIdentity(destinationParentFd, destinationName, sourceIdentity, destinationPath);
                EditorAuthoringMutationJournal.MarkTransientPublished(Path.Combine(Path.GetDirectoryName(sourcePath), sourceQuarantine));
                EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            } catch (Exception primary) {
                List<Exception> rollbackFailures = new List<Exception>();
                try {
                    if (published && TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat publishedStatus) &&
                        sourceIdentity.Matches(publishedStatus)) {
                        RenameLinuxNoReplace(destinationParentFd, destinationName, sourceParentFd, sourceQuarantine, destinationPath, sourceIdentity);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                try {
                    if (TryGetLinuxEntry(sourceParentFd, sourceQuarantine, out PosixStat ignored)) {
                        RenameLinuxNoReplace(sourceParentFd, sourceQuarantine, sourceParentFd, sourceName, destinationPath, sourceIdentity);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                if (rollbackFailures.Count != 0) {
                    rollbackFailures.Insert(0, primary);
                    throw new AggregateException($"Could not move verified authoring leaf '{destinationPath}' and rollback failed.", rollbackFailures);
                }
                throw;
            }
        }

        static void MoveLinuxDirectory(
            int sourceParentFd,
            string sourceName,
            int destinationParentFd,
            string destinationName,
            string sourcePath,
            string destinationPath) {
            PosixEntryIdentity sourceIdentity = RequireLinuxEntry(sourceParentFd, sourceName, true, destinationPath);
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(sourceIdentity.Describe(), "missing");
            if (TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat destinationStatus)) {
                EnsureLinuxEntryType(destinationStatus, true, destinationPath);
                throw new IOException($"The verified destination '{destinationPath}' already exists.");
            }
            string quarantine = QuarantineLinuxEntry(
                sourceParentFd,
                sourceName,
                sourceIdentity,
                sourcePath,
                recoveryIntent: "RollbackPublication",
                intendedDestinationPath: destinationPath);
            bool published = false;
            try {
                published = true;
                RenameLinuxNoReplace(sourceParentFd, quarantine, destinationParentFd, destinationName, destinationPath, sourceIdentity);
                EnsureLinuxIdentity(destinationParentFd, destinationName, sourceIdentity, destinationPath);
                EditorAuthoringMutationJournal.MarkTransientPublished(Path.Combine(Path.GetDirectoryName(sourcePath), quarantine));
                EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            } catch (Exception primary) {
                List<Exception> rollbackFailures = new List<Exception>();
                try {
                    if (published && TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat publishedStatus) &&
                        sourceIdentity.Matches(publishedStatus)) {
                        RenameLinuxNoReplace(destinationParentFd, destinationName, sourceParentFd, quarantine, destinationPath, sourceIdentity);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                try {
                    if (TryGetLinuxEntry(sourceParentFd, quarantine, out PosixStat ignored)) {
                        RenameLinuxNoReplace(sourceParentFd, quarantine, sourceParentFd, sourceName, destinationPath, sourceIdentity);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                if (rollbackFailures.Count != 0) {
                    rollbackFailures.Insert(0, primary);
                    throw new AggregateException($"Could not move verified authoring directory '{destinationPath}' and rollback failed.", rollbackFailures);
                }
                throw;
            }
        }

        static string QuarantineLinuxEntry(
            int parentFd,
            string name,
            PosixEntryIdentity expected,
            string path,
            string projectRootPath = null,
            string expectedHash = null,
            string intendedDestinationPath = null,
            string recoveryIntent = null) {
            for (int attempt = 0; attempt < 32; attempt++) {
                string mutationRoot = projectRootPath ?? EditorAuthoringMutationJournal.CurrentProjectRootPath;
                string proofHash = expectedHash;
                if (string.IsNullOrWhiteSpace(proofHash) && mutationRoot != null && !expected.IsDirectory) {
                    proofHash = TryGetVerifiedSha256(mutationRoot, path);
                }
                string quarantine = EditorAuthoringMutationJournal.ReserveTransient(
                    path,
                    Path.GetDirectoryName(path),
                    intendedDestinationPath,
                    expected.Describe(),
                    expected.IsDirectory ? null : proofHash,
                    expected.IsDirectory ? "Directory" : "File",
                    recoveryIntent ?? "RestoreOriginal");
                try {
                    RenameLinuxNoReplace(parentFd, name, parentFd, quarantine, path, expected);
                } catch (Exception exception) {
                    // Only a proven pre-syscall name collision is retryable.
                    // If the source name has disappeared, the rename may
                    // already have succeeded and failed during durability;
                    // retain that operation-owned inode for recovery.
                    bool sourceStillPresent = TryGetLinuxEntry(parentFd, name, out _);
                    if (sourceStillPresent && Marshal.GetLastPInvokeError() == PosixAlreadyExists) {
                        EditorAuthoringMutationJournal.CompleteTransient(Path.Combine(Path.GetDirectoryName(path), quarantine));
                        continue;
                    }
                    throw;
                }

                try {
                    EnsureLinuxIdentity(parentFd, quarantine, expected, path);
                    if (mutationRoot != null) {
                        VerifyExpectedHash(mutationRoot, Path.Combine(Path.GetDirectoryName(path), quarantine), proofHash, "quarantined entry");
                    }
                    EditorAuthoringMutationJournal.RecordTransientOccupied(Path.Combine(Path.GetDirectoryName(path), quarantine));
                    return quarantine;
                } catch {
                    try {
                        if (TryGetLinuxEntry(parentFd, quarantine, out PosixStat ignored)) {
                            RenameLinuxNoReplace(parentFd, quarantine, parentFd, name, path, expected);
                            EditorAuthoringMutationJournal.CompleteTransient(Path.Combine(Path.GetDirectoryName(path), quarantine));
                        }
                    } catch {
                        // Preserve the quarantined inode when it cannot be restored.
                    }
                    throw;
                }
            }
            throw new IOException($"Could not reserve a verified quarantine entry beneath '{path}'.");
        }

        static PosixEntryIdentity RequireLinuxEntry(int parentFd, string name, bool directory, string path) {
            if (!TryGetLinuxEntry(parentFd, name, out PosixStat status)) {
                throw new FileNotFoundException($"The verified authoring entry '{path}' does not exist.");
            }
            EnsureLinuxEntryType(status, directory, path);
            return new PosixEntryIdentity(status);
        }

        static bool TryGetLinuxEntry(int parentFd, string name, out PosixStat status) {
            if (PosixFStatAt(parentFd, name, out status, PosixAtSymlinkNoFollow) == 0) {
                return true;
            }
            int error = Marshal.GetLastPInvokeError();
            if (error == PosixNotFound) {
                status = default;
                return false;
            }
            throw new Win32Exception(error, $"Could not inspect authoring entry '{name}'.");
        }

        static void EnsureLinuxEntryType(PosixStat status, bool directory, string path) {
            int type = (int)(status.Mode & PosixFileTypeMask);
            int expected = directory ? PosixDirectoryFileType : PosixRegularFileType;
            if (type != expected) {
                throw new InvalidDataException($"The authoring entry '{path}' is not a regular non-reparse {(directory ? "directory" : "file")}.");
            }
        }

        static void EnsureLinuxIdentity(int parentFd, string name, PosixEntryIdentity expected, string path) {
            if (!TryGetLinuxEntry(parentFd, name, out PosixStat actual) || !expected.Matches(actual)) {
                throw new InvalidDataException($"The authoring entry '{path}' changed while it was being secured.");
            }
        }

        static void EnsureLinuxHandleIdentity(SafeFileHandle handle, PosixEntryIdentity expected, string path) {
            if (handle == null || handle.IsInvalid || PosixFStat(handle.DangerousGetHandle().ToInt32(), out PosixStat actual) != 0 ||
                !expected.Matches(actual)) {
                throw new InvalidDataException($"The opened authoring entry '{path}' changed before its operation began.");
            }
        }

        static void RenameLinuxNoReplace(
            int sourceParentFd,
            string sourceName,
            int destinationParentFd,
            string destinationName,
            string path,
            PosixEntryIdentity? expectedSource = null,
            PosixEntryIdentity? expectedDestination = null) {
            if (expectedSource.HasValue) {
                EnsureLinuxIdentity(sourceParentFd, sourceName, expectedSource.Value, path);
            }
            if (expectedDestination.HasValue) {
                EnsureLinuxIdentity(destinationParentFd, destinationName, expectedDestination.Value, path);
            } else if (TryGetLinuxEntry(destinationParentFd, destinationName, out _)) {
                throw new IOException($"The verified no-replace destination '{path}' appeared before publication.");
            }
            InvokeMutationHook("FixedRename.BeforeFinalSyscall");
            InvokeMutationHook($"FixedRename.BeforeFinalSyscall:{sourceName}->{destinationName}");
            if (expectedSource.HasValue) {
                EnsureLinuxIdentity(sourceParentFd, sourceName, expectedSource.Value, path);
            }
            if (expectedDestination.HasValue) {
                EnsureLinuxIdentity(destinationParentFd, destinationName, expectedDestination.Value, path);
            } else if (TryGetLinuxEntry(destinationParentFd, destinationName, out _)) {
                throw new IOException($"The verified no-replace destination '{path}' appeared before publication.");
            }
            RenameLinuxNoReplaceRaw(sourceParentFd, sourceName, destinationParentFd, destinationName, path);
            // The namespace syscall has already succeeded at this point. Keep
            // the durability boundary observable so a test or recovery hook
            // can exercise the ambiguous post-rename/pre-fsync cut without
            // pretending that the entry was never moved.
            InvokeMutationHook("FixedRename.AfterSyscallBeforeFsync");
            InvokeMutationHook($"FixedRename.AfterSyscallBeforeFsync:{sourceName}->{destinationName}");
            FsyncDirectory(sourceParentFd, path);
            if (destinationParentFd != sourceParentFd) {
                FsyncDirectory(destinationParentFd, path);
            }
            if (expectedSource.HasValue) {
                EnsureLinuxIdentity(destinationParentFd, destinationName, expectedSource.Value, path);
            }
        }

        static void RenameLinuxNoReplaceRaw(
            int sourceParentFd,
            string sourceName,
            int destinationParentFd,
            string destinationName,
            string path) {
            if (RenameAt2(sourceParentFd, sourceName, destinationParentFd, destinationName, PosixRenameNoReplace) != 0) {
                throw CreatePosixRenameException(path);
            }
        }

        /// <summary>Deletes a verified regular-file leaf without following links.</summary>
        internal void DeleteLeaf(string filePath) {
            EnsureNotDisposed();
            if (CaptureVerifiedIdentity(ProjectRootPath, filePath) == "missing") {
                return;
            }
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "delete", filePath, filePath);
            string deletingPath = journal.CreateDeletingPath(filePath);
            EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, filePath, deletingPath, journal.ExpectedSourceIdentityValue, "missing");
            journal.MarkPhase("Published");
            EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(ProjectRootPath, deletingPath, journal.ExpectedSourceIdentityValue);
            journal.Complete();
        }

        /// <summary>
        /// Moves one regular-file leaf while both source and destination
        /// parents are pinned. Callers own the project publication lock.
        /// </summary>
        internal static void MoveLeaf(string projectRootPath, string sourcePath, string destinationPath) {
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(projectRootPath, "move", source, destination);
            journal.RequireDestinationIdentity(destination);
            using EditorAuthoringMutationScope sourceScope = AcquireForMutation(projectRootPath, sourceParent);
            // The source is in the operation's staging directory, so the
            // destination parent must always be pinned explicitly even when
            // the caller's original source and destination share a folder.
            using EditorAuthoringMutationScope destinationScope = AcquireForMutation(projectRootPath, destinationParent);
            sourceScope.MoveLeafToPinnedDestination(source, destination, destinationScope);
            journal.MarkPhase("Published");
            journal.Complete();
        }

        /// <summary>Copies one regular-file leaf through verified handles.</summary>
        internal static void CopyLeaf(string projectRootPath, string sourcePath, string destinationPath) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(projectRootPath, "copy", source, destination);
            if (journal.ExpectedDestinationIdentityValue != "missing") {
                throw new IOException($"The copy destination '{destination}' must be absent at operation start.");
            }
            string stagedPath = journal.CreateStagedPayloadPath("payload");
            string stagedNextPath = journal.CreateStagedPayloadNextPath();
            string stagedHash;
            using EditorAuthoringMutationScope sourceScope = AcquireForMutation(projectRootPath, sourceParent);
            using EditorAuthoringMutationScope stagedScope = AcquireForMutation(projectRootPath, Path.GetDirectoryName(stagedPath));
            {
                using EditorAuthoringVerifiedFile sourceFile = sourceScope.OpenVerifiedFile(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                using EditorAuthoringVerifiedFile stagedFile = stagedScope.OpenVerifiedFile(
                    stagedNextPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[64 * 1024];
                int read;
                while ((read = sourceFile.Stream.Read(buffer, 0, buffer.Length)) > 0) {
                    stagedFile.Stream.Write(buffer, 0, read);
                    hasher.AppendData(buffer, 0, read);
                }
                stagedFile.Stream.Flush(true);
                stagedHash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }
            EditorAuthoringMutationScope.FixedRenameNoReplace(projectRootPath, stagedNextPath, stagedPath);
            journal.RecordStagedPayload(stagedPath, stagedHash);
            journal.ValidateStagedPayload();

            // First move the proven payload to its fixed publication name.
            // The destination is never exchanged by name: while a replacement
            // is pending, the journal owns the former destination inode.
            string publishingPath = journal.CreatePublishingPayloadPath();
            EditorAuthoringMutationScope.FixedRenameNoReplace(
                projectRootPath,
                stagedPath,
                publishingPath,
                journal.StagedIdentityValue,
                "missing",
                stagedHash);
            journal.RecordPublishingPayload(publishingPath);
            journal.MarkPhase("Publishing");
            using EditorAuthoringMutationScope destinationScope = AcquireForMutation(projectRootPath, destinationParent);
            string destinationIdentity;
            try {
                destinationIdentity = journal.RequireDestinationIdentity(destination);
            } catch {
                journal.Complete();
                throw;
            }
            if (destinationIdentity == "missing") {
                try {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(
                        projectRootPath,
                        publishingPath,
                        destination,
                        journal.PublishingPayloadIdentityValue,
                        "missing",
                        journal.PublishingPayloadHashValue);
                } catch (IOException exception) {
                    string currentDestinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(projectRootPath, destination);
                    string currentPublishingIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(projectRootPath, publishingPath);
                    bool destinationCollisionProvenBeforeSyscall =
                        currentDestinationIdentity != "missing" &&
                        currentDestinationIdentity != journal.PublishingPayloadIdentityValue &&
                        currentPublishingIdentity == journal.PublishingPayloadIdentityValue;
                    if (!destinationCollisionProvenBeforeSyscall) {
                        throw;
                    }
                    // A strict copy never overwrites a concurrently-created
                    // destination. Only the state in which the exact
                    // publishing inode is still present proves that the
                    // no-replace syscall did not consume it. Retire this
                    // unpublished operation; every ambiguous outcome stays
                    // durable for startup recovery.
                    journal.Complete();
                    throw new IOException($"The copy destination '{destination}' appeared before publication.", exception);
                }
            } else {
                // Copy is intentionally strict: the destination was required
                // to be absent at journal begin and can never be overwritten.
                journal.Complete();
                throw new IOException($"The copy destination '{destination}' appeared before publication.");
            }
            journal.ValidatePublishedPayload(destination);
            journal.MarkPhase("Published");
            journal.Complete();
        }

        /// <summary>Deletes one regular-file leaf through a pinned parent.</summary>
        internal static void DeleteLeaf(string projectRootPath, string filePath) {
            string fullPath = Path.GetFullPath(filePath);
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(projectRootPath, "delete", fullPath, fullPath);
            string deletingPath = journal.CreateDeletingPath(fullPath);
            FixedRenameNoReplace(projectRootPath, fullPath, deletingPath, journal.ExpectedSourceIdentityValue, "missing");
            journal.MarkPhase("Published");
            FixedDeleteVerifiedLeaf(projectRootPath, deletingPath, journal.ExpectedSourceIdentityValue);
            journal.Complete();
        }

        /// <summary>Moves a directory entry while its parent remains pinned.</summary>
        internal static void MoveDirectory(string projectRootPath, string sourcePath, string destinationPath) {
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            if (!string.Equals(sourceParent, destinationParent, PathComparison)) {
                throw new InvalidDataException("Verified directory moves require one pinned parent.");
            }
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(projectRootPath, "move-directory", source, destination);
            MoveDirectoryCore(projectRootPath, source, destination, sourceParent);
            journal.MarkPhase("Published");
            journal.Complete();
        }

        static void MoveDirectoryCore(string projectRootPath, string source, string destination, string sourceParent) {
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, sourceParent);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(source, projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destination, projectRootPath);
            if (OperatingSystem.IsWindows()) {
                using SafeFileHandle sourceDirectory = OpenAndVerifyWindowsDirectory(source, true);
                scope.RenameVerifiedWindowsLeaf(sourceDirectory, Path.GetFileName(destination), false);
            } else if (OperatingSystem.IsLinux()) {
                MoveLinuxDirectory(
                    scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(),
                    Path.GetFileName(source),
                    scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(),
                    Path.GetFileName(destination),
                    source,
                    destination);
            } else if (!OperatingSystem.IsLinux()) {
                throw CreateUnsupportedPlatformException();
            }
        }

        /// <summary>Creates and pins a directory tree beneath the project root.</summary>
        internal static void EnsureDirectory(string projectRootPath, string directoryPath) {
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, directoryPath);
        }

        /// <summary>
        /// Deletes a validated directory tree without following any leaf or
        /// directory link. Every directory removal is performed via its pinned
        /// parent entry.
        /// </summary>
        internal static void DeleteDirectoryTree(string projectRootPath, string directoryPath, string containingRoot) {
            string fullPath = Path.GetFullPath(directoryPath);
            if (CaptureVerifiedIdentity(projectRootPath, fullPath) == "missing") {
                return;
            }
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(projectRootPath, "delete-directory", fullPath, fullPath);
            string deletingPath = journal.CreateDeletingPath(fullPath);
            FixedRenameNoReplace(projectRootPath, fullPath, deletingPath, journal.ExpectedSourceIdentityValue, "missing");
            journal.MarkPhase("Published");
            FixedDeleteVerifiedDirectoryTree(projectRootPath, deletingPath, containingRoot, journal.ExpectedSourceIdentityValue);
            journal.Complete();
        }

        static SafeFileHandle OpenPosixRegularFileAt(SafeFileHandle parent, string name) {
            int flags = PosixReadOnly | PosixNoFollow | PosixCloseOnExec | PosixNonBlock;
            int fd = PosixOpenAt(parent.DangerousGetHandle().ToInt32(), name, flags, 0);
            if (fd < 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open cleanup file '{name}'.");
            }

            SafeFileHandle handle = new SafeFileHandle(new IntPtr(fd), true);
            try {
                if (PosixFStat(fd, out PosixStat status) != 0 || (status.Mode & PosixFileTypeMask) != PosixRegularFileType) {
                    throw new InvalidDataException($"The authoring cleanup entry '{name}' is not a regular file.");
                }
                int fileFlags = PosixFcntl(fd, PosixFGetFlags, 0);
                if (fileFlags < 0 || PosixFcntl(fd, PosixFSetFlags, fileFlags & ~PosixNonBlock) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not configure verified cleanup file '{name}'.");
                }
                return handle;
            } catch {
                handle.Dispose();
                throw;
            }
        }

        static string ReadLinuxDirectoryEntryName(IntPtr entry) {
            // linux_dirent64 places d_name immediately after ino, offset, reclen, and d_type.
            const int nameOffset = 19;
            List<byte> name = new List<byte>();
            for (int index = 0; index < 256; index++) {
                byte value = Marshal.ReadByte(entry, nameOffset + index);
                if (value == 0) {
                    break;
                }
                name.Add(value);
            }
            if (name.Count == 0) {
                throw new InvalidDataException("The Linux authoring directory contained an invalid entry name.");
            }
            string decoded;
            try {
                decoded = new UTF8Encoding(false, true).GetString(name.ToArray());
            } catch (DecoderFallbackException exception) {
                throw new InvalidDataException("The Linux authoring directory contained a non-UTF8 entry name.", exception);
            }
            if (decoded.IndexOf('\0') >= 0 || decoded == "." || decoded == "..") {
                throw new InvalidDataException("The Linux authoring directory contained an invalid entry name.");
            }
            return decoded;
        }

        /// <summary>Reads a regular-file leaf through a verified handle.</summary>
        internal static byte[] ReadAllBytes(string projectRootPath, string filePath) {
            string fullPath = Path.GetFullPath(filePath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(
                projectRootPath,
                Path.GetDirectoryName(fullPath));
            using EditorAuthoringVerifiedFile file = scope.OpenVerifiedFile(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            using MemoryStream bytes = new MemoryStream();
            file.Stream.CopyTo(bytes);
            return bytes.ToArray();
        }

        internal static string TryGetVerifiedSha256(string projectRootPath, string filePath) {
            try {
                return Convert.ToHexString(SHA256.HashData(ReadAllBytes(projectRootPath, filePath))).ToLowerInvariant();
            } catch (FileNotFoundException) {
                return "missing";
            } catch (DirectoryNotFoundException) {
                return "missing";
            } catch (Win32Exception exception) when (exception.NativeErrorCode == 2 || exception.NativeErrorCode == 3) {
                return "missing";
            }
        }

        /// <summary>
        /// Captures the identity of one verified current filesystem entry for a
        /// mutation journal. The value is based on the opened entry rather than
        /// mutable length/time metadata, and includes the entry kind.
        /// </summary>
        internal static string CaptureVerifiedIdentity(string projectRootPath, string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return "missing";
            }

            string fullPath = Path.GetFullPath(path);
            string parent = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parent)) {
                return "missing";
            }

            try {
                using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, parent);
                if (OperatingSystem.IsWindows()) {
                    using SafeFileHandle entry = OpenAndVerifyWindowsEntry(fullPath);
                    return DescribeWindowsHandle(entry);
                }

                if (OperatingSystem.IsLinux()) {
                    if (!TryGetLinuxEntry(scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(), Path.GetFileName(fullPath), out PosixStat status)) {
                        return "missing";
                    }
                    return new PosixEntryIdentity(status).Describe();
                }
            } catch (FileNotFoundException) {
                return "missing";
            } catch (DirectoryNotFoundException) {
                return "missing";
            } catch (Win32Exception exception) when (exception.NativeErrorCode == 2 || exception.NativeErrorCode == 3) {
                return "missing";
            } catch {
                return "unavailable";
            }

            return "unavailable";
        }

        /// <summary>
        /// Writes and atomically replaces a regular-file leaf through verified
        /// handles. The temporary leaf is always created exclusively.
        /// </summary>
        internal static void WriteAllBytesAtomically(string projectRootPath, string filePath, byte[] bytes) {
            string fullPath = Path.GetFullPath(filePath);
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(
                projectRootPath,
                "replace",
                fullPath,
                fullPath);
            string stagedPath = journal.CreateStagedPayloadPath("payload");
            string stagedNextPath = journal.CreateStagedPayloadNextPath();
            string directoryPath = Path.GetDirectoryName(fullPath);
            using (EditorAuthoringMutationScope stagedScope = AcquireForMutation(projectRootPath, Path.GetDirectoryName(stagedNextPath)))
            using (EditorAuthoringVerifiedFile stagedFile = stagedScope.OpenVerifiedFile(
                stagedNextPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None)) {
                stagedFile.Stream.Write(bytes, 0, bytes.Length);
                stagedFile.Stream.Flush(true);
            }
            EditorAuthoringMutationScope.FixedRenameNoReplace(projectRootPath, stagedNextPath, stagedPath);
            string stagedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            journal.RecordStagedPayload(stagedPath, stagedHash);
            journal.ValidateStagedPayload();

            // Reserve the former destination before publishing any payload
            // state. A recovery observer must never see the original
            // destination alongside a publishing payload without the exact
            // proof and fixed path needed to continue the replacement.
            string destinationIdentity;
            try {
                destinationIdentity = journal.RequireDestinationIdentity(fullPath);
            } catch {
                journal.Complete();
                throw;
            }
            string destinationOldPath = destinationIdentity == "missing"
                ? null
                : journal.CreateDestinationOldPath();

            string publishingPath = journal.CreatePublishingPayloadPath();
            EditorAuthoringMutationScope.FixedRenameNoReplace(
                projectRootPath,
                stagedPath,
                publishingPath,
                journal.StagedIdentityValue,
                "missing",
                stagedHash);
            journal.RecordPublishingPayload(publishingPath);
            if (destinationIdentity == "missing") {
                EditorAuthoringMutationScope.FixedRenameNoReplace(
                    projectRootPath,
                    publishingPath,
                    fullPath,
                    journal.PublishingPayloadIdentityValue,
                    journal.ExpectedDestinationIdentityValue,
                        journal.PublishingPayloadHashValue);
            } else {
                EditorAuthoringMutationScope.FixedRenameNoReplace(
                    projectRootPath,
                    fullPath,
                    destinationOldPath,
                    journal.ExpectedDestinationIdentityValue,
                    "missing",
                    journal.ExpectedDestinationHashValue);
                journal.RecordDestinationOld(destinationOldPath);
                EditorAuthoringMutationScope.FixedRenameNoReplace(
                    projectRootPath,
                    publishingPath,
                    fullPath,
                    journal.PublishingPayloadIdentityValue,
                    "missing",
                    journal.PublishingPayloadHashValue);
                journal.ValidatePublishedPayload(fullPath);
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(
                    projectRootPath,
                    destinationOldPath,
                    journal.DestinationOldIdentityValue,
                    journal.DestinationOldHashValue);
            }
            journal.ValidatePublishedPayload(fullPath);
            journal.MarkPhase("Published");
            journal.Complete();
        }

        internal void MoveLeafToPinnedDestination(
            string source,
            string destination,
            EditorAuthoringMutationScope destinationScope) {
            EnsureNotDisposed();
            if (!string.Equals(Path.GetDirectoryName(source), TargetDirectoryPath, PathComparison)) {
                throw new InvalidDataException("The verified source is not beneath the pinned source directory.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(source, ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destination, ProjectRootPath);
            VerifyExistingLeafIfPresent(source);
            string destinationIdentity = CaptureVerifiedIdentity(ProjectRootPath, destination);
            if (destinationIdentity == "unavailable") {
                throw new InvalidDataException($"Could not verify the destination '{destination}'.");
            }
            if (destinationIdentity != "missing") {
                throw new IOException($"The verified destination '{destination}' already exists.");
            }
            if (OperatingSystem.IsWindows()) {
                using EditorAuthoringVerifiedFile sourceFile = OpenVerifiedFileCore(
                    source,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    true);
                RenameVerifiedWindowsLeaf(
                    sourceFile.Stream.SafeFileHandle,
                    Path.GetFileName(destination),
                    false,
                    destinationScope);
                if (CaptureVerifiedIdentity(ProjectRootPath, destination) == "missing") {
                    throw new IOException($"Verified rename did not publish '{destination}'.");
                }
            } else {
                SafeFileHandle sourceDirectory = Handles[Handles.Count - 1];
                SafeFileHandle destinationDirectory = destinationScope?.Handles[destinationScope.Handles.Count - 1] ?? sourceDirectory;
                MoveLinuxLeaf(
                    sourceDirectory.DangerousGetHandle().ToInt32(),
                    Path.GetFileName(source),
                    destinationDirectory.DangerousGetHandle().ToInt32(),
                    Path.GetFileName(destination),
                    source,
                    destination);
            }
        }

        /// <summary>Acquires an exclusive advisory lock on a verified leaf on POSIX.</summary>
        internal bool TryAcquireExclusiveFileLock(EditorAuthoringVerifiedFile file) {
            EnsureNotDisposed();
            if (OperatingSystem.IsWindows()) {
                return true;
            }
            if (!OperatingSystem.IsLinux()) {
                throw CreateUnsupportedPlatformException();
            }

            return Flock(file.Stream.SafeFileHandle.DangerousGetHandle().ToInt32(), PosixLockExclusive | PosixLockNonBlocking) == 0;
        }

        void VerifyExistingLeafIfPresent(string path) {
            string identity = CaptureVerifiedIdentity(ProjectRootPath, path);
            if (identity == "unavailable") {
                throw new InvalidDataException($"Could not verify the source '{path}'.");
            }
            if (identity == "missing") {
                return;
            }
            using EditorAuthoringVerifiedFile file = OpenVerifiedFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorAuthoringMutationScope));
            }
        }

        static void EnsureSupportedPlatform() {
            if (!OperatingSystem.IsWindows() &&
                (!OperatingSystem.IsLinux() || !IsSupportedLinuxArchitecture())) {
                throw CreateUnsupportedPlatformException();
            }
        }

        static bool IsSupportedLinuxArchitecture() {
            // PosixStat below matches the Linux x64 ABI. Do not bind that
            // layout to another architecture until its native offsets are
            // supplied and verified.
            return OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64;
        }

        static PlatformNotSupportedException CreateUnsupportedPlatformException() {
            return new PlatformNotSupportedException(
                $"Secure editor authoring filesystem mutations are not implemented for '{RuntimeInformation.OSDescription}' on this architecture. Supported platforms are Windows and Linux x64.");
        }

        static Exception CreatePosixRenameException(string destination) {
            int error = Marshal.GetLastWin32Error();
            if (error == 38) {
                return new PlatformNotSupportedException(
                    "The Linux kernel does not expose renameat2; secure no-replace authoring moves are unavailable.");
            }
            return new Win32Exception(error, $"Could not atomically move authoring entry to '{destination}'.");
        }

        static void OpenPosixDirectoryChain(
            string projectRoot,
            List<string> existingChain,
            List<string> missingDirectories,
            List<SafeFileHandle> handles) {
            existingChain.Reverse();
            if (existingChain.Count == 0 || !string.Equals(existingChain[0], projectRoot, PathComparison)) {
                throw new InvalidDataException("The POSIX mutation chain does not start at the project root.");
            }

            // Anchor the walk at the filesystem root. Opening the absolute project
            // path in one call would leave an ancestor swap outside the pinned chain.
            handles.Add(OpenPosixDirectory(Path.GetPathRoot(projectRoot) ?? "/", null));
            string relativeProjectRoot = projectRoot.Substring((Path.GetPathRoot(projectRoot) ?? "/").Length)
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string component in relativeProjectRoot.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries)) {
                handles.Add(OpenPosixDirectory(component, handles[handles.Count - 1]));
            }

            string existingRelativePath = Path.GetRelativePath(projectRoot, existingChain[existingChain.Count - 1]);
            foreach (string component in existingRelativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries)) {
                if (component == ".") {
                    continue;
                }
                handles.Add(OpenPosixDirectory(component, handles[handles.Count - 1]));
            }

            missingDirectories.Reverse();
            for (int index = 0; index < missingDirectories.Count; index++) {
                string directory = missingDirectories[index];
                SafeFileHandle parent = handles[handles.Count - 1];
                string name = Path.GetFileName(directory);
                int parentFd = parent.DangerousGetHandle().ToInt32();
                int mkdirResult = MkdirAt(parentFd, name, 0x1ED);
                if (mkdirResult != 0 && Marshal.GetLastWin32Error() != 17) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not create '{directory}'.");
                }
                if (mkdirResult == 0) {
                    FsyncDirectory(parentFd, directory);
                }
                handles.Add(OpenPosixDirectory(name, parent));
            }
        }

        static SafeFileHandle OpenPosixDirectory(string path, SafeFileHandle parent) {
            int flags = PosixReadOnly | PosixDirectory | PosixNoFollow | PosixCloseOnExec;
            int fd = parent == null
                ? PosixOpen(path, flags, 0)
                : PosixOpenAt(parent.DangerousGetHandle().ToInt32(), path, flags, 0);
            if (fd < 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not pin directory '{path}'.");
            }
            SafeFileHandle handle = new SafeFileHandle(new IntPtr(fd), true);
            try {
                if (PosixFStat(fd, out PosixStat status) != 0 || (status.Mode & PosixFileTypeMask) != PosixDirectoryFileType) {
                    throw new InvalidDataException($"The POSIX authoring path '{path}' is not a directory.");
                }
                return handle;
            } catch {
                handle.Dispose();
                throw;
            }
        }

        SafeFileHandle OpenAndVerifyPosixFile(string leafName, FileMode mode, FileAccess access) {
            int flags = access == FileAccess.Read ? PosixReadOnly : access == FileAccess.Write ? PosixWriteOnly : PosixReadWrite;
            flags |= PosixNoFollow | PosixCloseOnExec | PosixNonBlock;
            switch (mode) {
                case FileMode.CreateNew:
                    flags |= PosixCreate | PosixExclusive;
                    break;
                case FileMode.OpenOrCreate:
                case FileMode.Create:
                    flags |= PosixCreate;
                    break;
            }

            int fd = PosixOpenAt(Handles[Handles.Count - 1].DangerousGetHandle().ToInt32(), leafName, flags, 0x1A4);
            if (fd < 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open verified file '{leafName}'.");
            }
            SafeFileHandle handle = new SafeFileHandle(new IntPtr(fd), true);
            try {
                if (PosixFStat(fd, out PosixStat status) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not inspect verified file '{leafName}'.");
                }
                if ((status.Mode & PosixFileTypeMask) != PosixRegularFileType) {
                    throw new InvalidDataException($"The POSIX authoring leaf '{leafName}' is not a regular file.");
                }
                int fileFlags = PosixFcntl(fd, PosixFGetFlags, 0);
                if (fileFlags < 0 || PosixFcntl(fd, PosixFSetFlags, fileFlags & ~PosixNonBlock) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not configure verified file '{leafName}'.");
                }
                return handle;
            } catch {
                handle.Dispose();
                throw;
            }
        }

        static SafeFileHandle OpenAndVerifyWindowsDirectory(string directoryPath, bool includeDelete = false) {
            FileAttributes attributes = File.GetAttributes(directoryPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidDataException($"The authoring mutation directory '{directoryPath}' is a reparse point.");
            }

            SafeFileHandle handle = CreateFileW(
                directoryPath,
                GenericRead | (includeDelete ? DeleteAccess : 0),
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid) {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Could not pin authoring mutation directory '{directoryPath}'.");
            }

            try {
                VerifyWindowsHandlePath(handle, directoryPath, true);
                return handle;
            } catch {
                handle.Dispose();
                throw;
            }
        }

        static SafeFileHandle OpenAndVerifyWindowsFile(
            string filePath,
            FileMode mode,
            FileAccess access,
            FileShare share,
            bool includeDelete = false) {
            uint desiredAccess = access == FileAccess.Read
                ? GenericRead
                : access == FileAccess.Write
                    ? GenericWrite
                    : GenericRead | GenericWrite;
            if (includeDelete) {
                desiredAccess |= DeleteAccess;
            }
            uint creationDisposition = mode switch {
                FileMode.CreateNew => CreateNew,
                FileMode.Create => CreateAlways,
                FileMode.OpenOrCreate => OpenAlways,
                FileMode.Truncate => OpenExisting,
                _ => OpenExisting
            };
            uint shareMode = share.HasFlag(FileShare.Read) ? FileShareRead : 0;
            if (share.HasFlag(FileShare.Write)) {
                shareMode |= FileShareWrite;
            }
            if (includeDelete || share.HasFlag(FileShare.Delete)) {
                shareMode |= FileShareDelete;
            }
            SafeFileHandle handle = CreateFileW(
                filePath,
                desiredAccess,
                shareMode,
                IntPtr.Zero,
                creationDisposition,
                FileFlagWriteThrough | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid) {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                if (error == 32 || error == 33) {
                    throw new IOException($"The verified file '{filePath}' is currently held by another owner.", new Win32Exception(error));
                }
                throw new Win32Exception(error, $"Could not open verified file '{filePath}'.");
            }

            try {
                VerifyWindowsHandlePath(handle, filePath, false);
                if (mode == FileMode.Truncate) {
                    if (!SetFilePointerEx(handle, 0, IntPtr.Zero, 0) || !SetEndOfFile(handle)) {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not truncate verified file '{filePath}'.");
                    }
                }
                return handle;
            } catch {
                handle.Dispose();
                throw;
            }
        }

        static void VerifyWindowsHandlePath(SafeFileHandle handle, string expectedPath, bool directory) {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information)) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not inspect verified path '{expectedPath}'.");
            }
            if ((information.FileAttributes & FileAttributeReparsePoint) != 0 ||
                (directory && (information.FileAttributes & (uint)FileAttributes.Directory) == 0) ||
                (!directory && (information.FileAttributes & (uint)FileAttributes.Directory) != 0)) {
                throw new InvalidDataException($"The authoring path '{expectedPath}' is not a non-reparse {(directory ? "directory" : "file")}.");
            }

            StringBuilder finalPathBuffer = new StringBuilder(4096);
            uint length = GetFinalPathNameByHandleW(handle, finalPathBuffer, (uint)finalPathBuffer.Capacity, 0);
            if (length == 0 || length >= finalPathBuffer.Capacity) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not resolve verified path '{expectedPath}'.");
            }
            string actualPath = RemoveExtendedPrefix(finalPathBuffer.ToString()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string canonicalExpectedPath = Path.GetFullPath(expectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(actualPath, canonicalExpectedPath, PathComparison)) {
                throw new InvalidDataException($"The authoring path '{expectedPath}' resolves to '{actualPath}'.");
            }
        }

        static SafeFileHandle OpenAndVerifyWindowsEntry(string path) {
            SafeFileHandle handle = CreateFileW(
                path,
                GenericRead,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                OpenExisting,
                FileFlagWriteThrough | FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid) {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Could not open verified authoring entry '{path}'.");
            }
            try {
                if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not inspect verified authoring entry '{path}'.");
                }
                bool directory = (information.FileAttributes & (uint)FileAttributes.Directory) != 0;
                VerifyWindowsHandlePath(handle, path, directory);
                return handle;
            } catch {
                handle.Dispose();
                throw;
            }
        }

        static SafeFileHandle OpenWindowsIdentityHandle(string path, string identity, bool includeDelete = true) {
            if (identity.EndsWith(":directory", StringComparison.Ordinal)) {
                return OpenAndVerifyWindowsDirectory(path, true);
            }
            return OpenAndVerifyWindowsFile(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                includeDelete);
        }

        static uint GetWindowsFileAttributes(SafeFileHandle handle) {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information)) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not inspect verified authoring handle.");
            }
            return information.FileAttributes;
        }

        static string DescribeWindowsHandle(SafeFileHandle handle) {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information)) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not inspect verified authoring handle identity.");
            }
            if ((information.FileAttributes & FileAttributeReparsePoint) != 0) {
                throw new InvalidDataException("A reparse entry cannot be used for an authoring mutation.");
            }
            string kind = (information.FileAttributes & (uint)FileAttributes.Directory) != 0 ? "directory" : "file";
            return $"windows:{information.VolumeSerialNumber:X8}:{information.FileIndexHigh:X8}{information.FileIndexLow:X8}:{kind}";
        }

        /// <summary>
        /// Renames an already verified Windows leaf through its handle. The
        /// destination is relative to the pinned target-directory handle, so
        /// the operation never reopens either leaf by path.
        /// </summary>
        void RenameVerifiedWindowsLeaf(
            SafeFileHandle source,
            string destinationName,
            bool replaceExisting,
            EditorAuthoringMutationScope destinationScope = null) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(destinationName) ||
                destinationName.IndexOfAny(new[] { '\\', '/' }) >= 0 ||
                destinationName == "." ||
                destinationName == "..") {
                throw new InvalidDataException("The verified rename destination must be one leaf name.");
            }

            EditorAuthoringMutationScope destinationOwner = destinationScope ?? this;
            string destinationPath = Path.Combine(destinationOwner.TargetDirectoryPath, destinationName);
            byte[] nameBytes = Encoding.Unicode.GetBytes(destinationPath + "\0");
            int nameOffset = Marshal.OffsetOf<FileRenameInfoHeader>(nameof(FileRenameInfoHeader.FileNameLength)).ToInt32() + sizeof(uint);
            int size = nameOffset + nameBytes.Length;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try {
                FileRenameInfoHeader header = new FileRenameInfoHeader {
                    ReplaceIfExists = replaceExisting ? (byte)1 : (byte)0,
                    RootDirectory = IntPtr.Zero,
                    FileNameLength = (uint)(nameBytes.Length - sizeof(char))
                };
                Marshal.StructureToPtr(header, buffer, false);
                Marshal.Copy(nameBytes, 0, IntPtr.Add(buffer, nameOffset), nameBytes.Length);
                if (!SetFileInformationByHandle(
                    source,
                    FileRenameInformation,
                    buffer,
                    (uint)size)) {
                    int error = Marshal.GetLastWin32Error();
                    if (!replaceExisting && error == 183) {
                        throw new IOException($"The fixed authoring destination '{destinationName}' already exists.", new Win32Exception(error));
                    }
                    throw new Win32Exception(error, $"Could not atomically rename verified authoring leaf to '{destinationName}' (win32={error}).");
                }
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static void DeleteVerifiedWindowsLeaf(SafeFileHandle file) {
            FileDispositionInfoEx disposition = new FileDispositionInfoEx {
                Flags = FileDispositionDelete | FileDispositionPosixSemantics
            };
            if (!SetFileInformationByHandleDisposition(
                file,
                FileDispositionInformationEx,
                ref disposition,
                (uint)Marshal.SizeOf<FileDispositionInfoEx>())) {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Could not delete verified authoring leaf (win32={error}).");
            }
        }

        static string RemoveExtendedPrefix(string path) {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) {
                return @"\" + path.Substring(7);
            }
            if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) {
                return path.Substring(4);
            }
            return path;
        }

        static int PosixOpen(string path, int flags, uint mode) {
            while (true) {
                int result = NativePosixOpen(path, flags, mode);
                if (result >= 0) {
                    return NormalizePosixFileDescriptor(result);
                }
                if (Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int PosixOpenAt(int directoryFd, string path, int flags, uint mode) {
            while (true) {
                int result = NativePosixOpenAt(directoryFd, path, flags, mode);
                if (result >= 0) {
                    return NormalizePosixFileDescriptor(result);
                }
                if (Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int NormalizePosixFileDescriptor(int fileDescriptor) {
            if (fileDescriptor < 0 || fileDescriptor >= 3) {
                return fileDescriptor;
            }
            int duplicate;
            while (true) {
                duplicate = NativePosixFcntl(fileDescriptor, PosixFDupFdCloexec, 3);
                if (duplicate >= 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    break;
                }
            }
            int closeResult = PosixClose(fileDescriptor);
            if (duplicate < 0) {
                return duplicate;
            }
            if (closeResult != 0) {
                PosixClose(duplicate);
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not normalize the POSIX authoring descriptor.");
            }
            return duplicate;
        }

        static int PosixDup(int fileDescriptor) {
            while (true) {
                int result = NativePosixFcntl(fileDescriptor, PosixFDupFdCloexec, 3);
                if (result >= 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static void FsyncDirectory(int directoryFd, string path) {
            if (directoryFd < 0) {
                throw new ArgumentOutOfRangeException(nameof(directoryFd));
            }
            while (true) {
                int result = NativePosixFsync(directoryFd);
                if (result == 0) {
                    return;
                }
                int error = Marshal.GetLastPInvokeError();
                if (error == PosixInterrupted) {
                    continue;
                }
                throw new Win32Exception(error, $"Could not durably synchronize authoring directory '{path}'.");
            }
        }

        static IntPtr PosixFdOpenDir(int fileDescriptor) {
            while (true) {
                IntPtr result = NativePosixFdOpenDir(fileDescriptor);
                if (result != IntPtr.Zero || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int MkdirAt(int directoryFd, string path, uint mode) {
            while (true) {
                int result = NativeMkdirAt(directoryFd, path, mode);
                if (result == 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int RenameAt2(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, uint flags) {
            try {
                while (true) {
                    int result = NativeRenameAt2(oldDirectoryFd, oldPath, newDirectoryFd, newPath, flags);
                    if (result == 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                        return result;
                    }
                }
            } catch (EntryPointNotFoundException exception) {
                throw new PlatformNotSupportedException(
                    "The Linux kernel interface renameat2 is unavailable; secure authoring entry mutations cannot continue.",
                    exception);
            }
        }

        static int UnlinkAt(int directoryFd, string path, int flags) {
            while (true) {
                int result = NativeUnlinkAt(directoryFd, path, flags);
                if (result == 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int Flock(int fileDescriptor, int operation) {
            while (true) {
                int result = NativeFlock(fileDescriptor, operation);
                if (result == 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int PosixFStatAt(int directoryFd, string path, out PosixStat status, int flags) {
            while (true) {
                int result = NativePosixFStatAt(directoryFd, path, out status, flags);
                if (result == 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int PosixFStat(int fileDescriptor, out PosixStat status) {
            while (true) {
                int result = NativePosixFStat(fileDescriptor, out status);
                if (result == 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static int PosixFcntl(int fileDescriptor, int command, int argument) {
            while (true) {
                int result = NativePosixFcntl(fileDescriptor, command, argument);
                if (result >= 0 || Marshal.GetLastPInvokeError() != PosixInterrupted) {
                    return result;
                }
            }
        }

        static IntPtr PosixReadDir(IntPtr directoryStream) {
            while (true) {
                Marshal.SetLastPInvokeError(0);
                IntPtr result = NativePosixReadDir(directoryStream);
                if (result != IntPtr.Zero) {
                    return result;
                }
                int error = Marshal.GetLastPInvokeError();
                if (error == PosixInterrupted) {
                    continue;
                }
                if (error != 0) {
                    throw new Win32Exception(error, "Could not read the pinned authoring directory.");
                }
                return IntPtr.Zero;
            }
        }

        static bool IsInside(string root, string candidate) {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = normalizedRoot + Path.DirectorySeparatorChar;
            return string.Equals(normalizedRoot, normalizedCandidate, PathComparison) ||
                normalizedCandidate.StartsWith(prefix, PathComparison);
        }

        static string NormalizeDirectoryIdentity(string path) {
            string fullPath = Path.GetFullPath(path);
            string pathRoot = Path.GetPathRoot(fullPath);
            if (!string.Equals(fullPath, pathRoot, PathComparison)) {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        static void EnsureContained(string root, string candidate) {
            if (!IsInside(root, candidate)) {
                throw new InvalidDataException($"The authoring mutation target '{candidate}' escapes project root '{root}'.");
            }
        }

        static void DisposeHandles(List<SafeFileHandle> handles) {
            List<Exception> failures = null;
            for (int index = handles.Count - 1; index >= 0; index--) {
                try {
                    handles[index].Dispose();
                } catch (Exception exception) {
                    (failures ??= new List<Exception>()).Add(exception);
                }
            }
            if (failures != null) {
                throw new AggregateException("Authoring mutation directory handles could not be released.", failures);
            }
            handles.Clear();
        }

        public void Dispose() {
            if (IsDisposed) {
                return;
            }
            DisposeHandles(Handles);
            IsDisposed = true;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ByHandleFileInformation {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateDirectoryW(string path, IntPtr securityAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder filePath, uint filePathLength, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileInformationByHandle(
            SafeFileHandle file,
            int fileInformationClass,
            IntPtr fileInformation,
            uint bufferSize);

        [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
        static extern bool SetFileInformationByHandleDisposition(
            SafeFileHandle file,
            int fileInformationClass,
            ref FileDispositionInfoEx fileInformation,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFilePointerEx(SafeFileHandle file, long distanceToMove, IntPtr newFilePointer, uint moveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetEndOfFile(SafeFileHandle file);

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        static extern int NativePosixOpen(string path, int flags, uint mode);

        [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
        static extern int NativePosixOpenAt(int directoryFd, string path, int flags, uint mode);

        [DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
        static extern int NativeMkdirAt(int directoryFd, string path, uint mode);

        [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
        static extern int NativeRenameAt2(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, uint flags);

        [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
        static extern int NativeUnlinkAt(int directoryFd, string path, int flags);

        [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
        static extern int NativeFlock(int fileDescriptor, int operation);

        [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
        static extern int NativePosixDup(int fileDescriptor);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        static extern int PosixClose(int fileDescriptor);

        [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
        static extern int NativePosixFsync(int fileDescriptor);

        [DllImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
        static extern IntPtr NativePosixFdOpenDir(int fileDescriptor);

        [DllImport("libc", EntryPoint = "readdir", SetLastError = true)]
        static extern IntPtr NativePosixReadDir(IntPtr directoryStream);

        [DllImport("libc", EntryPoint = "closedir", SetLastError = true)]
        static extern int PosixClosedDir(IntPtr directoryStream);

        [DllImport("libc", EntryPoint = "fstatat", SetLastError = true)]
        static extern int NativePosixFStatAt(int directoryFd, string path, out PosixStat status, int flags);

        [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
        static extern int NativePosixFStat(int fileDescriptor, out PosixStat status);

        [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
        static extern int NativePosixFcntl(int fileDescriptor, int command, int argument);

        const int FileRenameInformation = 3;
        const int FileDispositionInformationEx = 21;
        const uint FileDispositionDelete = 0x00000001;
        const uint FileDispositionPosixSemantics = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        struct FileRenameInfoHeader {
            public byte ReplaceIfExists;
            public IntPtr RootDirectory;
            public uint FileNameLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FileDispositionInfoEx {
            public uint Flags;
        }

        // Linux x64 struct stat layout. Only the file-type bits in st_mode are
        // consumed; the complete prefix keeps native offsets intact.
        [StructLayout(LayoutKind.Sequential)]
        struct PosixStat {
            public ulong Device;
            public ulong Inode;
            public ulong LinkCount;
            public uint Mode;
            public uint UserId;
            public uint GroupId;
            public uint Padding;
            public ulong SpecialDevice;
            public long Size;
            public long BlockSize;
            public long Blocks;
            public long AccessTime;
            public ulong AccessTimeNanoseconds;
            public long ModifyTime;
            public ulong ModifyTimeNanoseconds;
            public long ChangeTime;
            public ulong ChangeTimeNanoseconds;
            public long BirthTime;
            public ulong BirthTimeNanoseconds;
            public int Reserved0;
            public int Reserved1;
            public int Reserved2;
        }

        readonly struct PosixEntryIdentity {
            internal PosixEntryIdentity(PosixStat status) {
                Device = status.Device;
                Inode = status.Inode;
                Mode = status.Mode & PosixFileTypeMask;
            }

            readonly ulong Device;
            readonly ulong Inode;
            readonly uint Mode;

            internal bool Matches(PosixStat status) {
                return Device == status.Device && Inode == status.Inode && Mode == (status.Mode & PosixFileTypeMask);
            }

            internal bool IsDirectory => Mode == PosixDirectoryFileType;

            internal string Describe() {
                return $"dev:{Device};inode:{Inode};type:{Mode:X}";
            }
        }
    }

    /// <summary>Owns a stream opened through a verified no-follow leaf handle.</summary>
    internal sealed class EditorAuthoringVerifiedFile : IDisposable {
        internal EditorAuthoringVerifiedFile(FileStream stream, string path) {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        internal FileStream Stream { get; }

        internal string Path { get; }

        public void Dispose() {
            Stream.Dispose();
        }
    }
}
