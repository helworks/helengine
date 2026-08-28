using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
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
        const int PosixLockExclusive = 2;
        const int PosixLockNonBlocking = 4;
        static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        readonly List<SafeFileHandle> Handles;
        readonly string ProjectRootPath;
        readonly string TargetDirectoryPath;
        bool IsDisposed;

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
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(targetDirectoryPath)) {
                throw new ArgumentException("Mutation target directory must be provided.", nameof(targetDirectoryPath));
            }

            string projectRoot = Path.GetFullPath(projectRootPath);
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
                } else {
                    OpenPosixDirectoryChain(projectRoot, existingChain, missingDirectories, handles);
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
            FileMode openMode = mode == FileMode.Create
                ? (File.Exists(fullPath) ? FileMode.Open : FileMode.CreateNew)
                : mode;
            SafeFileHandle handle = OperatingSystem.IsWindows()
                ? OpenAndVerifyWindowsFile(fullPath, openMode, access, share, includeDelete)
                : OpenAndVerifyPosixFile(Path.GetFileName(fullPath), openMode, access);
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

        /// <summary>Atomically moves a verified leaf into a destination leaf.</summary>
        internal void ReplaceLeaf(string sourcePath, string destinationPath, bool replaceExisting) {
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
            VerifyExistingLeafIfPresent(source);
            if (File.Exists(destination)) {
                VerifyExistingLeafIfPresent(destination);
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
                    replaceExisting && File.Exists(destination));
            } else {
                SafeFileHandle parent = Handles[Handles.Count - 1];
                int result = RenameAt(
                    parent.DangerousGetHandle().ToInt32(),
                    Path.GetFileName(source),
                    parent.DangerousGetHandle().ToInt32(),
                    Path.GetFileName(destination));
                if (result != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not atomically replace '{destination}'.");
                }
            }
        }

        /// <summary>Deletes a verified regular-file leaf without following links.</summary>
        internal void DeleteLeaf(string filePath) {
            EnsureNotDisposed();
            string fullPath = Path.GetFullPath(filePath);
            if (!string.Equals(Path.GetDirectoryName(fullPath), TargetDirectoryPath, PathComparison)) {
                throw new InvalidDataException("Verified leaf deletion must use the pinned parent directory.");
            }
            if (!File.Exists(fullPath)) {
                return;
            }

            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, ProjectRootPath);
            VerifyExistingLeafIfPresent(fullPath);
            if (OperatingSystem.IsWindows()) {
                using EditorAuthoringVerifiedFile file = OpenVerifiedFileCore(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    true);
                DeleteVerifiedWindowsLeaf(file.Stream.SafeFileHandle);
            } else if (UnlinkAt(
                Handles[Handles.Count - 1].DangerousGetHandle().ToInt32(),
                Path.GetFileName(fullPath),
                0) != 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not delete '{fullPath}'.");
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
            using EditorAuthoringMutationScope sourceScope = AcquireForMutation(projectRootPath, sourceParent);
            using EditorAuthoringMutationScope destinationScope = string.Equals(sourceParent, destinationParent, PathComparison)
                ? null
                : AcquireForMutation(projectRootPath, destinationParent);
            sourceScope.MoveLeafToPinnedDestination(source, destination, destinationScope);
        }

        /// <summary>Copies one regular-file leaf through verified handles.</summary>
        internal static void CopyLeaf(string projectRootPath, string sourcePath, string destinationPath) {
            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            string sourceParent = Path.GetDirectoryName(source);
            string destinationParent = Path.GetDirectoryName(destination);
            using EditorAuthoringMutationScope sourceScope = AcquireForMutation(projectRootPath, sourceParent);
            using EditorAuthoringMutationScope destinationScope = string.Equals(sourceParent, destinationParent, PathComparison)
                ? null
                : AcquireForMutation(projectRootPath, destinationParent);
            using EditorAuthoringVerifiedFile sourceFile = sourceScope.OpenVerifiedFile(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            using EditorAuthoringVerifiedFile destinationFile = (destinationScope ?? sourceScope).OpenVerifiedFile(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            sourceFile.Stream.CopyTo(destinationFile.Stream);
            destinationFile.Stream.Flush(true);
        }

        /// <summary>Deletes one regular-file leaf through a pinned parent.</summary>
        internal static void DeleteLeaf(string projectRootPath, string filePath) {
            string fullPath = Path.GetFullPath(filePath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(
                projectRootPath,
                Path.GetDirectoryName(fullPath));
            scope.DeleteLeaf(fullPath);
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
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, sourceParent);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(source, projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destination, projectRootPath);
            if (OperatingSystem.IsWindows()) {
                using SafeFileHandle sourceDirectory = OpenAndVerifyWindowsDirectory(source, true);
                scope.RenameVerifiedWindowsLeaf(sourceDirectory, Path.GetFileName(destination), false);
            } else if (RenameAt(
                scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(),
                Path.GetFileName(source),
                scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(),
                Path.GetFileName(destination)) != 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not move directory '{source}' to '{destination}'.");
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
            foreach (string child in Directory.EnumerateFileSystemEntries(directoryPath, "*", SearchOption.TopDirectoryOnly).ToArray()) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(child, containingRoot);
                if ((File.GetAttributes(child) & FileAttributes.Directory) != 0) {
                    DeleteDirectoryContents(projectRootPath, child, containingRoot);
                    DeleteEmptyDirectory(projectRootPath, child, containingRoot);
                } else {
                    scope.DeleteLeaf(child);
                }
            }
        }

        static void DeleteEmptyDirectory(string projectRootPath, string directoryPath, string containingRoot) {
            string parentPath = Path.GetDirectoryName(directoryPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, parentPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directoryPath, containingRoot);
            if (OperatingSystem.IsWindows()) {
                using SafeFileHandle directory = OpenAndVerifyWindowsDirectory(directoryPath, true);
                DeleteVerifiedWindowsLeaf(directory);
            } else if (UnlinkAt(
                scope.Handles[scope.Handles.Count - 1].DangerousGetHandle().ToInt32(),
                Path.GetFileName(directoryPath),
                0x200) != 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not delete directory '{directoryPath}'.");
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

        /// <summary>
        /// Writes and atomically replaces a regular-file leaf through verified
        /// handles. The temporary leaf is always created exclusively.
        /// </summary>
        internal static void WriteAllBytesAtomically(string projectRootPath, string filePath, byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            string fullPath = Path.GetFullPath(filePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            using EditorAuthoringMutationScope scope = AcquireForMutation(projectRootPath, directoryPath);
            string temporaryPath = Path.Combine(directoryPath, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (EditorAuthoringVerifiedFile temporary = scope.OpenVerifiedFile(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None)) {
                    temporary.Stream.Write(bytes, 0, bytes.Length);
                    temporary.Stream.Flush(true);
                }
                scope.ReplaceLeaf(temporaryPath, fullPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    scope.DeleteLeaf(temporaryPath);
                }
            }
        }

        void MoveLeafToPinnedDestination(
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
            if (File.Exists(destination)) {
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
            } else {
                SafeFileHandle sourceDirectory = Handles[Handles.Count - 1];
                SafeFileHandle destinationDirectory = destinationScope?.Handles[destinationScope.Handles.Count - 1] ?? sourceDirectory;
                if (RenameAt(
                    sourceDirectory.DangerousGetHandle().ToInt32(),
                    Path.GetFileName(source),
                    destinationDirectory.DangerousGetHandle().ToInt32(),
                    Path.GetFileName(destination)) != 0) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not move '{source}' to '{destination}'.");
                }
            }
        }

        /// <summary>Acquires an exclusive advisory lock on a verified leaf on POSIX.</summary>
        internal bool TryAcquireExclusiveFileLock(EditorAuthoringVerifiedFile file) {
            EnsureNotDisposed();
            if (OperatingSystem.IsWindows()) {
                return true;
            }

            return Flock(file.Stream.SafeFileHandle.DangerousGetHandle().ToInt32(), PosixLockExclusive | PosixLockNonBlocking) == 0;
        }

        void VerifyExistingLeafIfPresent(string path) {
            if (!File.Exists(path)) {
                return;
            }
            using EditorAuthoringVerifiedFile file = OpenVerifiedFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorAuthoringMutationScope));
            }
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

            handles.Add(OpenPosixDirectory(projectRoot, null));
            for (int index = 1; index < existingChain.Count; index++) {
                handles.Add(OpenPosixDirectory(Path.GetFileName(existingChain[index]), handles[index - 1]));
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
            return new SafeFileHandle(new IntPtr(fd), true);
        }

        SafeFileHandle OpenAndVerifyPosixFile(string leafName, FileMode mode, FileAccess access) {
            int flags = access == FileAccess.Read ? PosixReadOnly : access == FileAccess.Write ? PosixWriteOnly : PosixReadWrite;
            flags |= PosixNoFollow | PosixCloseOnExec;
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
            return new SafeFileHandle(new IntPtr(fd), true);
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

        static bool IsInside(string root, string candidate) {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = normalizedRoot + Path.DirectorySeparatorChar;
            return string.Equals(normalizedRoot, normalizedCandidate, PathComparison) ||
                normalizedCandidate.StartsWith(prefix, PathComparison);
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
        static extern int PosixOpen(string path, int flags, uint mode);

        [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
        static extern int PosixOpenAt(int directoryFd, string path, int flags, uint mode);

        [DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
        static extern int MkdirAt(int directoryFd, string path, uint mode);

        [DllImport("libc", EntryPoint = "renameat", SetLastError = true)]
        static extern int RenameAt(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath);

        [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
        static extern int UnlinkAt(int directoryFd, string path, int flags);

        [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
        static extern int Flock(int fileDescriptor, int operation);

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
