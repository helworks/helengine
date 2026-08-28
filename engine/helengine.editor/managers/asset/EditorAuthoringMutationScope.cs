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
        const int PosixRenameExchange = 2;
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
            string expectedSourceIdentity = null) {
            FixedRename(projectRootPath, sourcePath, destinationPath, false, false, expectedSourceIdentity, null);
        }

        internal static void FixedRenameExchange(
            string projectRootPath,
            string sourcePath,
            string destinationPath,
            string expectedSourceIdentity = null,
            string expectedDestinationIdentity = null) {
            FixedRename(projectRootPath, sourcePath, destinationPath, true, true, expectedSourceIdentity, expectedDestinationIdentity);
        }

        static void FixedRename(
            string projectRootPath,
            string sourcePath,
            string destinationPath,
            bool replaceExisting,
            bool exchange,
            string expectedSourceIdentity,
            string expectedDestinationIdentity) {
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
                if (exchange && destinationIdentityBefore == "missing") {
                    throw new FileNotFoundException($"The fixed exchange destination '{destination}' does not exist.");
                }
                if (!exchange && destinationIdentityBefore != "missing") {
                    throw new IOException($"The fixed rename destination '{destination}' already exists.");
                }
                if (expectedDestinationIdentity != null &&
                    !string.Equals(destinationIdentityBefore, expectedDestinationIdentity, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The fixed authoring destination '{destination}' failed identity verification.");
                }

                if (OperatingSystem.IsWindows()) {
                    bool sourceIsDirectory = (File.GetAttributes(source) & FileAttributes.Directory) != 0;
                    if (sourceIsDirectory) {
                        using SafeFileHandle sourceDirectory = OpenAndVerifyWindowsDirectory(source, true);
                        sourceScope.RenameVerifiedWindowsLeaf(
                            sourceDirectory,
                            Path.GetFileName(destination),
                            replaceExisting,
                            destinationScope);
                    } else {
                        using EditorAuthoringVerifiedFile sourceFile = sourceScope.OpenVerifiedFileCore(
                            source,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite,
                            true);
                        sourceScope.RenameVerifiedWindowsLeaf(
                            sourceFile.Stream.SafeFileHandle,
                            Path.GetFileName(destination),
                            replaceExisting,
                            destinationScope);
                    }
                } else if (OperatingSystem.IsLinux()) {
                int sourceParentFd = sourceScope.Handles[sourceScope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                int destinationParentFd = destinationScope.Handles[destinationScope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                if (!TryGetLinuxEntry(sourceParentFd, Path.GetFileName(source), out PosixStat sourceStatus)) {
                    throw new FileNotFoundException($"The fixed authoring source '{source}' does not exist.");
                }
                bool sourceIsDirectory = (sourceStatus.Mode & PosixFileTypeMask) == PosixDirectoryFileType;
                EnsureLinuxEntryType(sourceStatus, sourceIsDirectory, source);
                bool destinationExists = TryGetLinuxEntry(destinationParentFd, Path.GetFileName(destination), out PosixStat destinationStatus);
                if (destinationExists) {
                    EnsureLinuxEntryType(destinationStatus, sourceIsDirectory, destination);
                }
                if (exchange) {
                    RenameLinuxExchangeRaw(
                        sourceParentFd,
                        Path.GetFileName(source),
                        destinationParentFd,
                        Path.GetFileName(destination),
                        destination);
                } else {
                    RenameLinuxNoReplaceRaw(
                        sourceParentFd,
                        Path.GetFileName(source),
                        destinationParentFd,
                        Path.GetFileName(destination),
                        destination);
                }
                // Namespace success is observed before the durability step;
                // callers can reconcile both names if fsync reports failure.
                FsyncDirectory(sourceParentFd, sourceParent);
                if (destinationParentFd != sourceParentFd) {
                    FsyncDirectory(destinationParentFd, destinationParent);
                }
                } else {
                    throw CreateUnsupportedPlatformException();
                }

                string destinationIdentityAfter = CaptureVerifiedIdentity(projectRootPath, destination);
                if (destinationIdentityAfter == "missing" || destinationIdentityAfter == "unavailable") {
                    throw new IOException($"The fixed authoring rename did not publish '{destination}'.");
                }
            } finally {
                if (!ReferenceEquals(destinationScope, sourceScope)) {
                    destinationScope?.Dispose();
                }
            }
        }

        internal static void FixedDeleteVerifiedLeaf(
            string projectRootPath,
            string filePath,
            string expectedIdentity = null) {
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
            if (OperatingSystem.IsWindows()) {
                using EditorAuthoringVerifiedFile file = scope.OpenVerifiedFileCore(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    true);
                DeleteVerifiedWindowsLeaf(file.Stream.SafeFileHandle);
            } else if (OperatingSystem.IsLinux()) {
                int parentFd = scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                if (!TryGetLinuxEntry(parentFd, Path.GetFileName(fullPath), out PosixStat status)) {
                    return;
                }
                EnsureLinuxEntryType(status, false, fullPath);
                if (UnlinkAt(parentFd, Path.GetFileName(fullPath), 0) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not delete fixed authoring leaf '{fullPath}'.");
                }
                FsyncDirectory(parentFd, parent);
            } else {
                throw CreateUnsupportedPlatformException();
            }
        }

        internal static void FixedDeleteVerifiedDirectoryTree(
            string projectRootPath,
            string directoryPath,
            string containingRoot = null) {
            string fullPath = Path.GetFullPath(directoryPath);
            string root = string.IsNullOrWhiteSpace(containingRoot) ? projectRootPath : containingRoot;
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, root);
            if (CaptureVerifiedIdentity(projectRootPath, fullPath) == "missing") {
                return;
            }
            FixedDeleteDirectoryTreeCore(projectRootPath, fullPath, root);
        }

        static void FixedDeleteDirectoryTreeCore(string projectRootPath, string fullPath, string containingRoot) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            if (OperatingSystem.IsLinux()) {
                string parentPath = Path.GetDirectoryName(fullPath);
                using EditorAuthoringMutationScope parentScope = AcquireForMutation(projectRootPath, parentPath);
                int parentFd = parentScope.Handles[parentScope.Handles.Count - 1].DangerousGetHandle().ToInt32();
                if (!TryGetLinuxEntry(parentFd, Path.GetFileName(fullPath), out PosixStat status)) {
                    return;
                }
                EnsureLinuxEntryType(status, true, fullPath);
                using SafeFileHandle directory = OpenPosixDirectory(Path.GetFileName(fullPath), parentScope.Handles[parentScope.Handles.Count - 1]);
                FixedDeleteDirectoryContentsLinux(projectRootPath, directory, fullPath, containingRoot);
                EnsureLinuxIdentity(parentFd, Path.GetFileName(fullPath), new PosixEntryIdentity(status), fullPath);
                if (UnlinkAt(parentFd, Path.GetFileName(fullPath), PosixAtRemovedDirectory) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not delete fixed authoring directory '{fullPath}'.");
                }
                FsyncDirectory(parentFd, parentPath);
                return;
            }
            if (!OperatingSystem.IsWindows()) {
                throw CreateUnsupportedPlatformException();
            }
            foreach (string child in Directory.GetFileSystemEntries(fullPath, "*", SearchOption.TopDirectoryOnly).ToArray()) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(child, containingRoot);
                if ((File.GetAttributes(child) & FileAttributes.Directory) != 0) {
                    FixedDeleteDirectoryTreeCore(projectRootPath, child, containingRoot);
                } else {
                    FixedDeleteVerifiedLeaf(projectRootPath, child);
                }
            }
            string windowsParent = Path.GetDirectoryName(fullPath);
            using EditorAuthoringMutationScope windowsParentScope = AcquireForMutation(projectRootPath, windowsParent);
            using SafeFileHandle windowsDirectory = OpenAndVerifyWindowsDirectory(fullPath, true);
            DeleteVerifiedWindowsLeaf(windowsDirectory);
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
                        using SafeFileHandle childDirectory = OpenPosixDirectory(name, directory);
                        FixedDeleteDirectoryContentsLinux(projectRootPath, childDirectory, childPath, containingRoot);
                        EnsureLinuxIdentity(parentFd, name, new PosixEntryIdentity(status), childPath);
                        if (UnlinkAt(parentFd, name, PosixAtRemovedDirectory) != 0) {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove fixed authoring directory '{childPath}'.");
                        }
                        FsyncDirectory(parentFd, childPath);
                    } else {
                        EnsureLinuxEntryType(status, false, childPath);
                        using SafeFileHandle childFile = OpenPosixRegularFileAt(directory, name);
                        EnsureLinuxIdentity(parentFd, name, new PosixEntryIdentity(status), childPath);
                        if (UnlinkAt(parentFd, name, 0) != 0) {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove fixed authoring leaf '{childPath}'.");
                        }
                        FsyncDirectory(parentFd, childPath);
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
            ReplaceLeafCore(sourcePath, destinationPath, replaceExisting);
            journal.MarkPhase("Published");
            journal.Complete();
        }

        // Journal persistence itself uses the verified filesystem primitives,
        // but cannot recursively journal its own document replacement.
        internal void ReplaceLeafWithoutJournal(string sourcePath, string destinationPath, bool replaceExisting) {
            using IDisposable ephemeralJournal = EditorAuthoringMutationJournal.EnterEphemeral(ProjectRootPath);
            ReplaceLeafCore(sourcePath, destinationPath, replaceExisting);
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
                    destination);
            } else {
                throw CreateUnsupportedPlatformException();
            }
        }

        static void ReplaceLinuxLeaf(int parentFd, string sourceName, string destinationName, bool replaceExisting, string destinationPath) {
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

            // Keep the destination name continuously bound to either the old
            // or the new inode. The exchange is performed only after moving
            // the verified source into a recognized transient name, so a
            // failed exchange can restore the original source without ever
            // deleting an unverified directory entry.
            if (destinationExists) {
                PosixEntryIdentity destinationIdentity = new PosixEntryIdentity(destinationStatus);
                string sourceQuarantineForExchange = QuarantineLinuxEntry(parentFd, sourceName, sourceIdentity, destinationPath);
                bool exchanged = false;
                try {
                    RenameLinuxExchange(parentFd, sourceQuarantineForExchange, destinationName, destinationPath);
                    exchanged = true;
                    FsyncDirectory(parentFd, destinationPath);
                    EnsureLinuxIdentity(parentFd, destinationName, sourceIdentity, destinationPath);
                    EnsureLinuxIdentity(parentFd, sourceQuarantineForExchange, destinationIdentity, destinationPath);
                    EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
                    DeleteQuarantinedLinuxEntry(parentFd, sourceQuarantineForExchange, destinationIdentity, destinationPath);
                } catch (Exception primary) {
                    List<Exception> exchangeRollbackFailures = new List<Exception>();
                    try {
                        if (exchanged && TryGetLinuxEntry(parentFd, destinationName, out PosixStat currentDestination) &&
                            sourceIdentity.Matches(currentDestination)) {
                            RenameLinuxExchange(parentFd, sourceQuarantineForExchange, destinationName, destinationPath);
                            FsyncDirectory(parentFd, destinationPath);
                            exchanged = false;
                        }
                    } catch (Exception exception) {
                        exchangeRollbackFailures.Add(exception);
                    }
                    try {
                        if (TryGetLinuxEntry(parentFd, sourceQuarantineForExchange, out PosixStat quarantinedSource) &&
                            sourceIdentity.Matches(quarantinedSource)) {
                            RenameLinuxNoReplace(parentFd, sourceQuarantineForExchange, parentFd, sourceName, destinationPath);
                        }
                    } catch (Exception exception) {
                        exchangeRollbackFailures.Add(exception);
                    }
                    if (exchangeRollbackFailures.Count != 0) {
                        exchangeRollbackFailures.Insert(0, primary);
                        throw new AggregateException($"Could not atomically replace verified authoring leaf '{destinationPath}' and rollback failed.", exchangeRollbackFailures);
                    }
                    throw;
                }
                return;
            }

            string sourceQuarantine = QuarantineLinuxEntry(parentFd, sourceName, sourceIdentity, destinationPath);
            bool published = false;
            List<Exception> rollbackFailures = new List<Exception>();
            try {
                // Mark the operation before entering the rename helper: its
                // durability step can fail after the directory entry has
                // already moved. Rollback therefore always verifies the
                // destination inode before attempting to restore it.
                published = true;
                RenameLinuxNoReplace(parentFd, sourceQuarantine, parentFd, destinationName, destinationPath);
                EnsureLinuxIdentity(parentFd, destinationName, sourceIdentity, destinationPath);
                EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            } catch (Exception primary) {
                try {
                    if (published && TryGetLinuxEntry(parentFd, destinationName, out PosixStat publishedStatus) &&
                        sourceIdentity.Matches(publishedStatus)) {
                        RenameLinuxNoReplace(parentFd, destinationName, parentFd, sourceQuarantine, destinationPath);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                try {
                    if (sourceQuarantine != null && TryGetLinuxEntry(parentFd, sourceQuarantine, out PosixStat ignoredSource)) {
                        RenameLinuxNoReplace(parentFd, sourceQuarantine, parentFd, sourceName, destinationPath);
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

        static void MoveLinuxLeaf(int sourceParentFd, string sourceName, int destinationParentFd, string destinationName, string destinationPath) {
            PosixEntryIdentity sourceIdentity = RequireLinuxEntry(sourceParentFd, sourceName, false, destinationPath);
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(sourceIdentity.Describe(), "missing");
            if (TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat destinationStatus)) {
                EnsureLinuxEntryType(destinationStatus, false, destinationPath);
                throw new IOException($"The verified destination '{destinationPath}' already exists.");
            }

            string sourceQuarantine = QuarantineLinuxEntry(sourceParentFd, sourceName, sourceIdentity, destinationPath);
            bool published = false;
            try {
                published = true;
                RenameLinuxNoReplace(sourceParentFd, sourceQuarantine, destinationParentFd, destinationName, destinationPath);
                EnsureLinuxIdentity(destinationParentFd, destinationName, sourceIdentity, destinationPath);
                EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            } catch (Exception primary) {
                List<Exception> rollbackFailures = new List<Exception>();
                try {
                    if (published && TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat publishedStatus) &&
                        sourceIdentity.Matches(publishedStatus)) {
                        RenameLinuxNoReplace(destinationParentFd, destinationName, sourceParentFd, sourceQuarantine, destinationPath);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                try {
                    if (TryGetLinuxEntry(sourceParentFd, sourceQuarantine, out PosixStat ignored)) {
                        RenameLinuxNoReplace(sourceParentFd, sourceQuarantine, sourceParentFd, sourceName, destinationPath);
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

        static void MoveLinuxDirectory(int sourceParentFd, string sourceName, int destinationParentFd, string destinationName, string destinationPath) {
            PosixEntryIdentity sourceIdentity = RequireLinuxEntry(sourceParentFd, sourceName, true, destinationPath);
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(sourceIdentity.Describe(), "missing");
            if (TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat destinationStatus)) {
                EnsureLinuxEntryType(destinationStatus, true, destinationPath);
                throw new IOException($"The verified destination '{destinationPath}' already exists.");
            }
            string quarantine = QuarantineLinuxEntry(sourceParentFd, sourceName, sourceIdentity, destinationPath);
            bool published = false;
            try {
                published = true;
                RenameLinuxNoReplace(sourceParentFd, quarantine, destinationParentFd, destinationName, destinationPath);
                EnsureLinuxIdentity(destinationParentFd, destinationName, sourceIdentity, destinationPath);
                EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            } catch (Exception primary) {
                List<Exception> rollbackFailures = new List<Exception>();
                try {
                    if (published && TryGetLinuxEntry(destinationParentFd, destinationName, out PosixStat publishedStatus) &&
                        sourceIdentity.Matches(publishedStatus)) {
                        RenameLinuxNoReplace(destinationParentFd, destinationName, sourceParentFd, quarantine, destinationPath);
                    }
                } catch (Exception exception) {
                    rollbackFailures.Add(exception);
                }
                try {
                    if (TryGetLinuxEntry(sourceParentFd, quarantine, out PosixStat ignored)) {
                        RenameLinuxNoReplace(sourceParentFd, quarantine, sourceParentFd, sourceName, destinationPath);
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

        static void DeleteLinuxDirectory(int parentFd, string name, string path) {
            PosixEntryIdentity identity = RequireLinuxEntry(parentFd, name, true, path);
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(identity.Describe(), "missing");
            string quarantine = QuarantineLinuxEntry(parentFd, name, identity, path);
            try {
                DeleteQuarantinedLinuxEntry(parentFd, quarantine, identity, path, true);
            } catch (Exception primary) {
                try {
                    if (TryGetLinuxEntry(parentFd, quarantine, out PosixStat ignored)) {
                        RenameLinuxNoReplace(parentFd, quarantine, parentFd, name, path);
                    }
                } catch (Exception rollback) {
                    throw new AggregateException($"Could not delete verified authoring directory '{path}' and rollback failed.", primary, rollback);
                }
                throw;
            }
        }

        static void DeleteLinuxLeaf(int parentFd, string name, string path) {
            if (!TryGetLinuxEntry(parentFd, name, out PosixStat status)) {
                return;
            }
            PosixEntryIdentity identity = RequireLinuxEntry(parentFd, name, false, path);
            EditorAuthoringMutationJournal.SetCurrentExpectedIdentities(identity.Describe(), "missing");
            string quarantine = QuarantineLinuxEntry(parentFd, name, identity, path);
            try {
                DeleteQuarantinedLinuxEntry(parentFd, quarantine, identity, path);
            } catch (Exception primary) {
                try {
                    if (TryGetLinuxEntry(parentFd, quarantine, out PosixStat ignored)) {
                        RenameLinuxNoReplace(parentFd, quarantine, parentFd, name, path);
                    }
                } catch (Exception rollback) {
                    throw new AggregateException($"Could not delete verified authoring leaf '{path}' and rollback failed.", primary, rollback);
                }
                throw;
            }
        }

        static string QuarantineLinuxEntry(int parentFd, string name, PosixEntryIdentity expected, string path) {
            for (int attempt = 0; attempt < 32; attempt++) {
                string quarantine = EditorAuthoringMutationJournal.ReserveTransientName(name);
                try {
                    RenameLinuxNoReplace(parentFd, name, parentFd, quarantine, path);
                } catch (Exception) when (Marshal.GetLastPInvokeError() == PosixAlreadyExists) {
                    continue;
                }

                try {
                    EnsureLinuxIdentity(parentFd, quarantine, expected, path);
                    return quarantine;
                } catch {
                    try {
                        if (TryGetLinuxEntry(parentFd, quarantine, out PosixStat ignored)) {
                            RenameLinuxNoReplace(parentFd, quarantine, parentFd, name, path);
                        }
                    } catch {
                        // Preserve the quarantined inode when it cannot be restored.
                    }
                    throw;
                }
            }
            throw new IOException($"Could not reserve a verified quarantine entry beneath '{path}'.");
        }

        static void DeleteQuarantinedLinuxEntry(int parentFd, string name, PosixEntryIdentity expected, string path) {
            EnsureLinuxIdentity(parentFd, name, expected, path);
            EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            if (UnlinkAt(parentFd, name, 0) != 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove verified quarantine entry for '{path}'.");
            }
            FsyncDirectory(parentFd, path);
        }

        static void DeleteQuarantinedLinuxEntry(int parentFd, string name, PosixEntryIdentity expected, string path, bool directory) {
            EnsureLinuxIdentity(parentFd, name, expected, path);
            EditorAuthoringMutationJournal.MarkCurrentPhase("Published");
            if (UnlinkAt(parentFd, name, directory ? PosixAtRemovedDirectory : 0) != 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not remove verified quarantine entry for '{path}'.");
            }
            FsyncDirectory(parentFd, path);
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

        static void RenameLinuxNoReplace(int sourceParentFd, string sourceName, int destinationParentFd, string destinationName, string path) {
            RenameLinuxNoReplaceRaw(sourceParentFd, sourceName, destinationParentFd, destinationName, path);
            FsyncDirectory(sourceParentFd, path);
            if (destinationParentFd != sourceParentFd) {
                FsyncDirectory(destinationParentFd, path);
            }
        }

        static void RenameLinuxExchange(int parentFd, string sourceName, string destinationName, string path) {
            RenameLinuxExchangeRaw(parentFd, sourceName, parentFd, destinationName, path);
            FsyncDirectory(parentFd, path);
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

        static void RenameLinuxExchangeRaw(
            int sourceParentFd,
            string sourceName,
            int destinationParentFd,
            string destinationName,
            string path) {
            if (RenameAt2(sourceParentFd, sourceName, destinationParentFd, destinationName, PosixRenameExchange) != 0) {
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
            EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, filePath, deletingPath);
            journal.MarkPhase("Published");
            EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(ProjectRootPath, deletingPath);
            journal.Complete();
        }

        internal void DeleteLeafWithoutJournal(string filePath) {
            using IDisposable ephemeralJournal = EditorAuthoringMutationJournal.EnterEphemeral(ProjectRootPath);
            DeleteLeafCore(filePath);
        }

        void DeleteLeafCore(string filePath) {
            EnsureNotDisposed();
            string fullPath = Path.GetFullPath(filePath);
            if (!string.Equals(Path.GetDirectoryName(fullPath), TargetDirectoryPath, PathComparison)) {
                throw new InvalidDataException("Verified leaf deletion must use the pinned parent directory.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, ProjectRootPath);
            if (OperatingSystem.IsWindows()) {
                try {
                    using EditorAuthoringVerifiedFile file = OpenVerifiedFileCore(
                        fullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        true);
                    DeleteVerifiedWindowsLeaf(file.Stream.SafeFileHandle);
                } catch (FileNotFoundException) {
                    return;
                } catch (DirectoryNotFoundException) {
                    return;
                } catch (Win32Exception exception) when (exception.NativeErrorCode == 2 || exception.NativeErrorCode == 3) {
                    return;
                }
            } else if (OperatingSystem.IsLinux()) {
                DeleteLinuxLeaf(
                    Handles[Handles.Count - 1].DangerousGetHandle().ToInt32(),
                    Path.GetFileName(fullPath),
                    fullPath);
            } else if (!OperatingSystem.IsLinux()) {
                throw CreateUnsupportedPlatformException();
            }
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

            // The staged payload remains the operation's publication source.
            // Publishing is an explicit durable intent followed by one fixed
            // no-replace/exchange namespace operation.
            journal.MarkPhase("Publishing");
            using EditorAuthoringMutationScope destinationScope = AcquireForMutation(projectRootPath, destinationParent);
            string destinationIdentity = CaptureVerifiedIdentity(projectRootPath, destination);
            if (destinationIdentity == "missing") {
                EditorAuthoringMutationScope.FixedRenameNoReplace(projectRootPath, stagedPath, destination);
            } else {
                EditorAuthoringMutationScope.FixedRenameExchange(projectRootPath, stagedPath, destination);
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(projectRootPath, stagedPath, destinationIdentity);
            }
            journal.MarkPhase("Published");
            journal.Complete();
        }

        /// <summary>Deletes one regular-file leaf through a pinned parent.</summary>
        internal static void DeleteLeaf(string projectRootPath, string filePath) {
            string fullPath = Path.GetFullPath(filePath);
            using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(projectRootPath, "delete", fullPath, fullPath);
            string deletingPath = journal.CreateDeletingPath(fullPath);
            FixedRenameNoReplace(projectRootPath, fullPath, deletingPath);
            journal.MarkPhase("Published");
            FixedDeleteVerifiedLeaf(projectRootPath, deletingPath);
            journal.Complete();
        }

        internal static void DeleteLeafWithoutJournal(string projectRootPath, string filePath) {
            string fullPath = Path.GetFullPath(filePath);
            using IDisposable ephemeralJournal = EditorAuthoringMutationJournal.EnterEphemeral(projectRootPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(
                projectRootPath,
                Path.GetDirectoryName(fullPath));
            scope.DeleteLeafWithoutJournal(fullPath);
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

        internal static void MoveDirectoryWithoutJournal(string projectRootPath, string sourcePath, string destinationPath) {
            using IDisposable ephemeralJournal = EditorAuthoringMutationJournal.EnterEphemeral(projectRootPath);
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            if (!string.Equals(sourceParent, destinationParent, PathComparison)) {
                throw new InvalidDataException("Verified directory moves require one pinned parent.");
            }
            MoveDirectoryCore(projectRootPath, source, destination, sourceParent);
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
            FixedRenameNoReplace(projectRootPath, fullPath, deletingPath);
            journal.MarkPhase("Published");
            FixedDeleteVerifiedDirectoryTree(projectRootPath, deletingPath, containingRoot);
            journal.Complete();
        }

        internal static void DeleteDirectoryTreeWithoutJournal(string projectRootPath, string directoryPath, string containingRoot) {
            using IDisposable ephemeralJournal = EditorAuthoringMutationJournal.EnterEphemeral(projectRootPath);
            DeleteDirectoryTreeCore(projectRootPath, Path.GetFullPath(directoryPath), containingRoot);
        }

        static void DeleteDirectoryTreeCore(string projectRootPath, string fullPath, string containingRoot) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            if (!Directory.Exists(fullPath)) {
                return;
            }
            EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(fullPath, containingRoot);
            DeleteDirectoryContents(projectRootPath, fullPath, containingRoot);
            DeleteEmptyDirectory(projectRootPath, fullPath, containingRoot);
        }

        static void DeleteDirectoryContents(string projectRootPath, string directoryPath, string containingRoot) {
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, directoryPath);
            if (OperatingSystem.IsLinux()) {
                DeleteDirectoryContentsLinux(scope.Handles[scope.Handles.Count - 1], directoryPath, containingRoot);
                return;
            }

            foreach (string child in Directory.EnumerateFileSystemEntries(directoryPath, "*", SearchOption.TopDirectoryOnly).ToArray()) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(child, containingRoot);
                if ((File.GetAttributes(child) & FileAttributes.Directory) != 0) {
                    DeleteDirectoryContents(projectRootPath, child, containingRoot);
                    DeleteEmptyDirectory(projectRootPath, child, containingRoot);
                } else {
                    scope.DeleteLeafWithoutJournal(child);
                }
            }
        }

        static void DeleteDirectoryContentsLinux(SafeFileHandle directory, string directoryPath, string containingRoot) {
            int duplicateFd = PosixDup(directory.DangerousGetHandle().ToInt32());
            if (duplicateFd < 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not enumerate pinned directory '{directoryPath}'.");
            }

            IntPtr directoryStream = PosixFdOpenDir(duplicateFd);
            if (directoryStream == IntPtr.Zero) {
                int error = Marshal.GetLastWin32Error();
                PosixClose(duplicateFd);
                throw new Win32Exception(error, $"Could not enumerate pinned directory '{directoryPath}'.");
            }

            try {
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
                    int parentFd = directory.DangerousGetHandle().ToInt32();
                    if (!TryGetLinuxEntry(parentFd, name, out PosixStat status)) {
                        continue;
                    }

                    bool isDirectory = (status.Mode & PosixFileTypeMask) == PosixDirectoryFileType;
                    bool isFile = (status.Mode & PosixFileTypeMask) == PosixRegularFileType;
                    if (!isDirectory && !isFile) {
                        throw new InvalidDataException($"The authoring cleanup entry '{childPath}' is not a regular non-reparse file or directory.");
                    }

                    PosixEntryIdentity identity = new PosixEntryIdentity(status);
                    string quarantine = QuarantineLinuxEntry(parentFd, name, identity, childPath);
                    try {
                        if (isDirectory) {
                            using SafeFileHandle childDirectory = OpenPosixDirectory(quarantine, directory);
                            DeleteDirectoryContentsLinux(childDirectory, Path.Combine(directoryPath, quarantine), containingRoot);
                            DeleteQuarantinedLinuxEntry(parentFd, quarantine, identity, childPath, true);
                        } else {
                            using SafeFileHandle childFile = OpenPosixRegularFileAt(directory, quarantine);
                            DeleteQuarantinedLinuxEntry(parentFd, quarantine, identity, childPath);
                        }
                    } catch (Exception primary) {
                        try {
                            if (TryGetLinuxEntry(parentFd, quarantine, out PosixStat ignored)) {
                                RenameLinuxNoReplace(parentFd, quarantine, parentFd, name, childPath);
                            }
                        } catch (Exception rollback) {
                            throw new AggregateException($"Could not clean verified entry '{childPath}' and rollback failed.", primary, rollback);
                        }
                        throw;
                    }
                }
            } finally {
                PosixClosedDir(directoryStream);
            }
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

        static void DeleteEmptyDirectory(string projectRootPath, string directoryPath, string containingRoot) {
            string parentPath = Path.GetDirectoryName(directoryPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, parentPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directoryPath, containingRoot);
            if (OperatingSystem.IsWindows()) {
                using SafeFileHandle directory = OpenAndVerifyWindowsDirectory(directoryPath, true);
                DeleteVerifiedWindowsLeaf(directory);
            } else if (OperatingSystem.IsLinux()) {
                DeleteLinuxDirectory(
                    scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(),
                    Path.GetFileName(directoryPath),
                    directoryPath);
            } else if (!OperatingSystem.IsLinux()) {
                throw CreateUnsupportedPlatformException();
            }
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
                    FileAttributes attributes = File.GetAttributes(fullPath);
                    if ((attributes & FileAttributes.Directory) != 0) {
                        using SafeFileHandle directory = OpenAndVerifyWindowsDirectory(fullPath, true);
                        if (!GetFileInformationByHandle(directory, out ByHandleFileInformation information)) {
                            return "unavailable";
                        }
                        return $"windows:{information.VolumeSerialNumber:X8}:{information.FileIndexHigh:X8}{information.FileIndexLow:X8}:directory";
                    }

                    using SafeFileHandle file = OpenAndVerifyWindowsFile(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (!GetFileInformationByHandle(file, out ByHandleFileInformation fileInformation)) {
                        return "unavailable";
                    }
                    return $"windows:{fileInformation.VolumeSerialNumber:X8}:{fileInformation.FileIndexHigh:X8}{fileInformation.FileIndexLow:X8}:file";
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
            journal.MarkPhase("Publishing");
            string destinationIdentity = CaptureVerifiedIdentity(projectRootPath, fullPath);
            if (destinationIdentity == "missing") {
                EditorAuthoringMutationScope.FixedRenameNoReplace(projectRootPath, stagedPath, fullPath);
            } else {
                EditorAuthoringMutationScope.FixedRenameExchange(projectRootPath, stagedPath, fullPath);
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(projectRootPath, stagedPath, destinationIdentity);
            }
            journal.MarkPhase("Published");
            journal.Complete();
        }

        internal static void WriteAllBytesAtomicallyWithoutJournal(string projectRootPath, string filePath, byte[] bytes, bool replaceExisting = true) {
            WriteAllBytesAtomically(projectRootPath, filePath, bytes, replaceExisting, false);
        }

        static void WriteAllBytesAtomically(
            string projectRootPath,
            string filePath,
            byte[] bytes,
            bool replaceExisting,
            bool journalOperation) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            string fullPath = Path.GetFullPath(filePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            using IDisposable ephemeralJournal = journalOperation ? null : EditorAuthoringMutationJournal.EnterEphemeral(projectRootPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, directoryPath);
            string temporaryPath = Path.Combine(directoryPath, EditorAuthoringMutationJournal.ReserveTransientName(Path.GetFileName(fullPath)));
            try {
                using (EditorAuthoringVerifiedFile temporary = scope.OpenVerifiedFile(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None)) {
                    temporary.Stream.Write(bytes, 0, bytes.Length);
                    temporary.Stream.Flush(true);
                }
                // The outer journal is the sole durable owner for a public
                // atomic write. The temporary is published with the verified
                // no-journal primitive so persistence cannot recurse into a
                // second operation while the first one is still active.
                scope.ReplaceLeafWithoutJournal(temporaryPath, fullPath, replaceExisting);
            } finally {
                // The replace moves the temporary entry into place. Cleanup
                // must therefore be a direct verified delete of a possibly
                // already-absent leaf, not a second journaled operation.
                scope.DeleteLeafCore(temporaryPath);
            }
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
