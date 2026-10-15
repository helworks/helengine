using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Security.AccessControl;
using System.Text.Json;
using System.Text.Json.Serialization;
using helengine.editor.automation;

namespace helengine.editor.automation.discovery;

/// <summary>
/// Publishes, reads, and removes private local descriptors for running editor instances.
/// </summary>
public sealed class EditorInstanceRegistry
{
    /// <summary>File suffix for non-secret editor instance descriptors.</summary>
    const string RegistryExtension = ".json";
    /// <summary>Win32 information class used to rename a descriptor handle atomically.</summary>
    const int FileRenameInfoExClass = 22;
    /// <summary>Flag requesting replacement of an existing descriptor destination.</summary>
    const uint ReplaceExistingFile = 0x00000001;
    /// <summary>Flag preserving old open handles while atomically publishing a new descriptor version.</summary>
    const uint PosixReplaceOpenFile = 0x00000002;
    /// <summary>JSON options that reject unrecognized descriptor fields during discovery.</summary>
    static readonly JsonSerializerOptions DescriptorJsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Absolute protected directory holding this registry's descriptor files.</summary>
    readonly string DirectoryPath;
    /// <summary>Receives diagnostics for corrupt or unsafe descriptor files skipped during discovery.</summary>
    readonly Action<string> ReportIssue;

    /// <summary>Creates a registry below an absolute directory protected for the current Windows user.</summary>
    /// <param name="DirectoryPath">The absolute private directory used for instance descriptors.</param>
    /// <param name="ReportIssue">Receives diagnostics for corrupt or unsafe entries found during discovery.</param>
    public EditorInstanceRegistry(string DirectoryPath, Action<string> ReportIssue)
    {
        if (ReportIssue == null)
        {
            throw new ArgumentNullException(nameof(ReportIssue));
        }

        this.DirectoryPath = EditorRegistryPaths.PreparePrivateDirectory(DirectoryPath);
        this.ReportIssue = ReportIssue;
    }

    /// <summary>Atomically writes one descriptor under a filename derived from its exact instance identity.</summary>
    /// <param name="Instance">The non-secret editor descriptor to publish.</param>
    public void Publish(EditorInstanceDescriptor Instance)
    {
        ValidateDescriptor(Instance);
        EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
        EditorInstanceDescriptor Snapshot = CopyDescriptor(Instance);
        string EntryPath = EditorRegistryPaths.GetEntryPath(DirectoryPath, Snapshot.InstanceId, RegistryExtension);

        if (EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes ExistingAttributes))
        {
            if ((ExistingAttributes & FileAttributes.ReparsePoint) != 0 || (ExistingAttributes & FileAttributes.Directory) != 0)
            {
                throw new IOException("A registry destination cannot be a directory or reparse point.");
            }

            try
            {
                EditorInstanceDescriptor Existing = ReadDescriptor(EntryPath);
                if (!string.Equals(Existing.InstanceId, Snapshot.InstanceId, StringComparison.Ordinal))
                {
                    throw new IOException("An instance identity hash collision prevents publishing this descriptor.");
                }
            }
            catch (JsonException Error)
            {
                ReportIssue($"Replacing corrupt editor registry entry '{EntryPath}': {Error.Message}");
            }
            catch (InvalidDataException Error)
            {
                ReportIssue($"Replacing invalid editor registry entry '{EntryPath}': {Error.Message}");
            }
        }

        byte[] DescriptorBytes = JsonSerializer.SerializeToUtf8Bytes(Snapshot);
        WriteAtomically(EntryPath, DescriptorBytes);
    }

    /// <summary>Returns independently deserialized candidates without probing their MCP endpoints.</summary>
    /// <returns>Valid descriptors ordered by their exact instance identity.</returns>
    public IReadOnlyList<EditorInstanceDescriptor> ReadCandidates()
    {
        EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
        List<EditorInstanceDescriptor> Candidates = new();

        foreach (string EntryPath in Directory.EnumerateFiles(DirectoryPath, "*" + RegistryExtension, SearchOption.TopDirectoryOnly))
        {
            try
            {
                EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
                if (!EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes Attributes))
                {
                    continue;
                }

                if ((Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    ReportIssue($"Skipped reparse-point editor registry entry '{EntryPath}'.");
                    continue;
                }

                if ((Attributes & FileAttributes.Directory) != 0)
                {
                    ReportIssue($"Skipped directory where an editor registry file was expected: '{EntryPath}'.");
                    continue;
                }

                EditorInstanceDescriptor Candidate = ReadDescriptor(EntryPath);
                ValidateDescriptor(Candidate);
                string ExpectedPath = EditorRegistryPaths.GetEntryPath(DirectoryPath, Candidate.InstanceId, RegistryExtension);
                if (!string.Equals(ExpectedPath, Path.GetFullPath(EntryPath), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The descriptor identity does not match its private registry filename.");
                }

                Candidates.Add(Candidate);
            }
            catch (Exception Error) when (IsInvalidEntryError(Error))
            {
                ReportIssue($"Skipped invalid editor registry entry '{EntryPath}': {Error.Message}");
            }
        }

        Candidates.Sort((Left, Right) => StringComparer.Ordinal.Compare(Left.InstanceId, Right.InstanceId));
        return new ReadOnlyCollection<EditorInstanceDescriptor>(Candidates);
    }

    /// <summary>Removes only the regular descriptor whose contents still identify the requested instance.</summary>
    /// <param name="InstanceId">The exact case-sensitive identity of the descriptor to remove.</param>
    public void Remove(string InstanceId)
    {
        Remove(InstanceId, null);
    }

    /// <summary>Removes only the validated file object, even if a new entry replaces its path during cleanup.</summary>
    /// <param name="InstanceId">The exact case-sensitive identity of the descriptor to remove.</param>
    /// <param name="BeforeDelete">An internal synchronization action used to exercise replacement races in tests.</param>
    internal void Remove(string InstanceId, Action BeforeDelete)
    {
        string EntryPath = EditorRegistryPaths.GetEntryPath(DirectoryPath, InstanceId, RegistryExtension);
        EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
        if (!EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes Attributes))
        {
            return;
        }

        if ((Attributes & FileAttributes.ReparsePoint) != 0 || (Attributes & FileAttributes.Directory) != 0)
        {
            ReportIssue($"Refused to remove an unsafe editor registry entry '{EntryPath}'.");
            return;
        }

        SafeFileHandle EntryHandle;
        try
        {
            EntryHandle = EditorRegistryPaths.OpenRegularFileForConditionalDelete(EntryPath);
        }
        catch (Win32Exception Error) when (Error.NativeErrorCode == 2 || Error.NativeErrorCode == 3)
        {
            return;
        }
        catch (Exception Error) when (IsInvalidEntryError(Error) || Error is Win32Exception)
        {
            ReportIssue($"Refused to remove an invalid editor registry entry '{EntryPath}': {Error.Message}");
            return;
        }

        using (EntryHandle)
        {
            using FileStream Stream = new(EntryHandle, FileAccess.Read, 4096, false);
            EditorInstanceDescriptor Existing;
            try
            {
                Existing = ReadDescriptor(Stream);
                ValidateDescriptor(Existing);
            }
            catch (Exception Error) when (IsInvalidEntryError(Error))
            {
                ReportIssue($"Refused to remove an invalid editor registry entry '{EntryPath}': {Error.Message}");
                return;
            }

            if (!string.Equals(Existing.InstanceId, InstanceId, StringComparison.Ordinal))
            {
                ReportIssue($"Refused to remove registry entry '{EntryPath}' because its identity was replaced.");
                return;
            }

            BeforeDelete?.Invoke();
            EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
            EditorRegistryPaths.DeleteOpenedFile(Stream.SafeFileHandle);
        }

    }

    /// <summary>Reads and deserializes one regular registry file using strict JSON member handling.</summary>
    /// <param name="EntryPath">The already checked descriptor file path.</param>
    /// <returns>The descriptor stored in the file.</returns>
    static EditorInstanceDescriptor ReadDescriptor(string EntryPath)
    {
        using FileStream Stream = new(EntryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        return ReadDescriptor(Stream);
    }

    /// <summary>Deserializes one registry descriptor from the exact stream already opened by the caller.</summary>
    /// <param name="Stream">The stream bound to the descriptor file object being validated.</param>
    /// <returns>The editor instance descriptor stored in the stream.</returns>
    static EditorInstanceDescriptor ReadDescriptor(Stream Stream)
    {
        EditorInstanceDescriptor Descriptor = JsonSerializer.Deserialize<EditorInstanceDescriptor>(Stream, DescriptorJsonOptions);
        if (Descriptor == null)
        {
            throw new InvalidDataException("The registry entry does not contain an editor descriptor.");
        }

        return Descriptor;
    }

    /// <summary>Validates the required public fields without probing the endpoint or changing project paths.</summary>
    /// <param name="Instance">The descriptor being published or read.</param>
    static void ValidateDescriptor(EditorInstanceDescriptor Instance)
    {
        if (Instance == null)
        {
            throw new ArgumentNullException(nameof(Instance));
        }

        if (string.IsNullOrWhiteSpace(Instance.InstanceId) || string.IsNullOrWhiteSpace(Instance.ProjectName) ||
            string.IsNullOrWhiteSpace(Instance.ProjectRootPath) || !Path.IsPathFullyQualified(Instance.ProjectRootPath) ||
            string.IsNullOrWhiteSpace(Instance.ProjectGeneration) || string.IsNullOrWhiteSpace(Instance.Endpoint) ||
            string.IsNullOrWhiteSpace(Instance.EditorVersion) || Instance.ContractVersion <= 0 || Instance.ProcessId <= 0 || Instance.StartedAt == default)
        {
            throw new ArgumentException("An editor descriptor must contain an identity, absolute project path, endpoint, version, process id, contract version, and start time.", nameof(Instance));
        }
    }

    /// <summary>Creates a detached descriptor snapshot before serialization.</summary>
    /// <param name="Instance">The caller-owned descriptor.</param>
    /// <returns>A copy containing only the public, non-secret instance fields.</returns>
    static EditorInstanceDescriptor CopyDescriptor(EditorInstanceDescriptor Instance)
    {
        return new EditorInstanceDescriptor
        {
            InstanceId = Instance.InstanceId,
            ProjectName = Instance.ProjectName,
            ProjectRootPath = Instance.ProjectRootPath,
            ProjectGeneration = Instance.ProjectGeneration,
            Endpoint = Instance.Endpoint,
            ContractVersion = Instance.ContractVersion,
            EditorVersion = Instance.EditorVersion,
            ProcessId = Instance.ProcessId,
            StartedAt = Instance.StartedAt
        };
    }

    /// <summary>Creates a private same-directory temporary file and atomically replaces one descriptor.</summary>
    /// <param name="EntryPath">The final file path inside the private root.</param>
    /// <param name="Contents">The complete serialized file contents.</param>
    static void WriteAtomically(string EntryPath, byte[] Contents)
    {
        string TempPath = EntryPath + ".tmp-" + Guid.NewGuid().ToString("N");
        string ParentDirectory = Path.GetDirectoryName(EntryPath);
        EditorRegistryPaths.VerifyPrivateDirectory(ParentDirectory);

        if (EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes ExistingAttributes) &&
            ((ExistingAttributes & FileAttributes.ReparsePoint) != 0 || (ExistingAttributes & FileAttributes.Directory) != 0))
        {
            throw new IOException("An atomic registry destination cannot be a directory or reparse point.");
        }

        try
        {
            FileSecurity FileSecurity = EditorRegistryPaths.CreatePrivateFileSecurity();
            using (FileStream Stream = FileSystemAclExtensions.Create(new FileInfo(TempPath), FileMode.CreateNew, FileSystemRights.FullControl, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough, FileSecurity))
            {
                Stream.Write(Contents, 0, Contents.Length);
                Stream.Flush(true);

                EditorRegistryPaths.VerifyPrivateDirectory(ParentDirectory);
                if (EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes CurrentAttributes) &&
                    ((CurrentAttributes & FileAttributes.ReparsePoint) != 0 || (CurrentAttributes & FileAttributes.Directory) != 0))
                {
                    throw new IOException("An atomic registry destination changed into a directory or reparse point.");
                }

                RenameAtomically(Stream.SafeFileHandle, EntryPath);
            }
        }
        finally
        {
            if (EditorRegistryPaths.TryGetAttributes(TempPath, out FileAttributes TempAttributes) &&
                (TempAttributes & FileAttributes.ReparsePoint) == 0 &&
                (TempAttributes & FileAttributes.Directory) == 0)
            {
                File.Delete(TempPath);
            }
        }
    }

    /// <summary>Atomically publishes the complete temporary file, including over an open prior descriptor.</summary>
    /// <param name="TemporaryFileHandle">The private, flushed file handle opened with delete access.</param>
    /// <param name="EntryPath">The exact destination path in the checked private registry directory.</param>
    static void RenameAtomically(Microsoft.Win32.SafeHandles.SafeFileHandle TemporaryFileHandle, string EntryPath)
    {
        byte[] FileNameBytes = System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(EntryPath));
        int FileNameOffset = Marshal.OffsetOf<NativeFileRenameInformationEx>(nameof(NativeFileRenameInformationEx.FileName)).ToInt32();
        int FileNameLengthOffset = Marshal.OffsetOf<NativeFileRenameInformationEx>(nameof(NativeFileRenameInformationEx.FileNameLength)).ToInt32();
        int BufferLength = checked(Marshal.SizeOf<NativeFileRenameInformationEx>() + FileNameBytes.Length);
        IntPtr RenameInformation = Marshal.AllocHGlobal(BufferLength);

        try
        {
            for (int Index = 0; Index < BufferLength; Index++)
            {
                Marshal.WriteByte(RenameInformation, Index, 0);
            }

            Marshal.WriteInt32(RenameInformation, 0, unchecked((int)(ReplaceExistingFile | PosixReplaceOpenFile)));
            Marshal.WriteIntPtr(RenameInformation, Marshal.OffsetOf<NativeFileRenameInformationEx>(nameof(NativeFileRenameInformationEx.RootDirectory)).ToInt32(), IntPtr.Zero);
            Marshal.WriteInt32(RenameInformation, FileNameLengthOffset, FileNameBytes.Length);
            Marshal.Copy(FileNameBytes, 0, IntPtr.Add(RenameInformation, FileNameOffset), FileNameBytes.Length);

            if (!SetFileInformationByHandle(TemporaryFileHandle, FileRenameInfoExClass, RenameInformation, (uint)BufferLength))
            {
                int ErrorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(ErrorCode, $"Windows could not atomically publish the editor registry descriptor (Win32 error {ErrorCode}: {new Win32Exception(ErrorCode).Message}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(RenameInformation);
        }
    }

    /// <summary>Identifies expected invalid-entry failures that should be diagnosed without ending enumeration.</summary>
    /// <param name="Error">The error raised while reading one candidate.</param>
    /// <returns>True for malformed, unsafe, inaccessible, or invalid candidate files.</returns>
    static bool IsInvalidEntryError(Exception Error)
    {
        return Error is IOException || Error is UnauthorizedAccessException || Error is JsonException ||
            Error is ArgumentException || Error is System.Security.SecurityException;
    }

    /// <summary>Native FILE_RENAME_INFORMATION_EX header followed by the variable UTF-16 destination name.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NativeFileRenameInformationEx
    {
        /// <summary>Gets or sets replace-existing and POSIX open-handle replacement flags.</summary>
        public uint Flags;

        /// <summary>Gets or sets the optional target directory handle; zero means the full path is supplied.</summary>
        public IntPtr RootDirectory;

        /// <summary>Gets or sets the destination name length in bytes.</summary>
        public uint FileNameLength;

        /// <summary>Marks the start of the variable UTF-16 destination name.</summary>
        public char FileName;
    }

    /// <summary>Renames a file handle atomically, optionally replacing an existing path with POSIX handle semantics.</summary>
    /// <param name="FileHandle">An open handle with DELETE access to the source file.</param>
    /// <param name="FileInformationClass">The FILE_INFO_BY_HANDLE_CLASS value for FILE_RENAME_INFO_EX.</param>
    /// <param name="FileInformation">The variable-length FILE_RENAME_INFORMATION_EX buffer.</param>
    /// <param name="BufferSize">The size in bytes of <paramref name="FileInformation"/>.</param>
    /// <returns>True when the filesystem performed the atomic rename.</returns>
    [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle FileHandle, int FileInformationClass, IntPtr FileInformation, uint BufferSize);
}
