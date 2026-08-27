using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Pins the existing Windows directory chain used by one filesystem mutation.
    /// The handles are shared by cooperating readers and writers, but not for
    /// deletion, so a linked ancestor cannot be swapped while the operation runs.
    /// </summary>
    internal sealed class EditorAuthoringMutationScope : IDisposable {
        const uint GenericRead = 0x80000000;
        const uint FileShareRead = 0x00000001;
        const uint FileShareWrite = 0x00000002;
        const uint OpenExisting = 3;
        const uint FileFlagBackupSemantics = 0x02000000;
        const uint FileFlagOpenReparsePoint = 0x00200000;
        static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        readonly List<SafeFileHandle> Handles;
        bool IsDisposed;

        EditorAuthoringMutationScope(List<SafeFileHandle> handles) {
            Handles = handles;
        }

        /// <summary>
        /// Opens and pins every directory from the physical project root to the
        /// directory containing the target. Missing descendants are created while
        /// their parent handle is held, then pinned before returning.
        /// </summary>
        internal static EditorAuthoringMutationScope AcquireForMutation(string projectRootPath, string targetDirectoryPath) {
            if (!OperatingSystem.IsWindows()) {
                throw new PlatformNotSupportedException("Authoring filesystem mutations require a secure directory-handle implementation on this platform.");
            }
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

            List<SafeFileHandle> handles = new List<SafeFileHandle>();
            try {
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

                existingChain.Reverse();
                foreach (string directory in existingChain) {
                    handles.Add(OpenAndVerifyDirectory(directory));
                }

                missingDirectories.Reverse();
                foreach (string directory in missingDirectories) {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directory, projectRoot);
                    Directory.CreateDirectory(directory);
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directory, projectRoot);
                    handles.Add(OpenAndVerifyDirectory(directory));
                }

                return new EditorAuthoringMutationScope(handles);
            } catch {
                DisposeHandles(handles);
                throw;
            }
        }

        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            DisposeHandles(Handles);
            IsDisposed = true;
        }

        static SafeFileHandle OpenAndVerifyDirectory(string directoryPath) {
            FileAttributes attributes = File.GetAttributes(directoryPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidDataException($"The authoring mutation directory '{directoryPath}' is a reparse point.");
            }

            SafeFileHandle handle = CreateFileW(
                directoryPath,
                GenericRead,
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
                StringBuilder finalPathBuffer = new StringBuilder(1024);
                uint length = GetFinalPathNameByHandleW(handle, finalPathBuffer, (uint)finalPathBuffer.Capacity, 0);
                if (length == 0 || length >= finalPathBuffer.Capacity) {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not resolve the pinned authoring directory '{directoryPath}'.");
                }

                string actualPath = RemoveExtendedPrefix(finalPathBuffer.ToString());
                string expectedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                actualPath = actualPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.Equals(actualPath, expectedPath, PathComparison)) {
                    throw new InvalidDataException($"The authoring mutation directory '{directoryPath}' resolves to '{actualPath}'.");
                }

                FileAttributes currentAttributes = File.GetAttributes(directoryPath);
                if ((currentAttributes & FileAttributes.ReparsePoint) != 0) {
                    throw new InvalidDataException($"The authoring mutation directory '{directoryPath}' became a reparse point while it was pinned.");
                }
                return handle;
            } catch {
                handle.Dispose();
                throw;
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(
            SafeFileHandle file,
            StringBuilder filePath,
            uint filePathLength,
            uint flags);
    }
}
