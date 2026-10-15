using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

[assembly: InternalsVisibleTo("helengine.editor.mcp.tests")]

namespace helengine.editor.automation.discovery;

/// <summary>
/// Defines per-user storage locations and the private filesystem primitives used by discovery.
/// </summary>
public static class EditorRegistryPaths
{
    /// <summary>Per-user product directory under LocalApplicationData.</summary>
    const string ProductDirectoryName = "Helengine";
    /// <summary>Product storage root reserved for editor MCP discovery.</summary>
    const string AutomationDirectoryName = "EditorMcp";
    /// <summary>Subdirectory containing non-secret editor instance descriptors.</summary>
    const string RegistryDirectoryName = "instances";
    /// <summary>Subdirectory containing bearer credentials kept separate from descriptors.</summary>
    const string CredentialDirectoryName = "credentials";
    /// <summary>Win32 desired-access bit for reading an opened local file.</summary>
    const uint GenericReadAccess = 0x80000000;
    /// <summary>Win32 desired-access bit required to mark the opened file object for deletion.</summary>
    const uint DeleteAccess = 0x00010000;
    /// <summary>Win32 flag that opens a reparse point itself instead of following its target.</summary>
    const uint FileFlagOpenReparsePoint = 0x00200000;
    /// <summary>Win32 information class for file attributes and a reparse tag.</summary>
    const int FileAttributeTagInformationClass = 9;
    /// <summary>Win32 information class for file link count and delete state.</summary>
    const int FileStandardInformationClass = 1;
    /// <summary>Win32 information class that sets deletion on the opened file object.</summary>
    const int FileDispositionInformationClass = 4;
    /// <summary>Win32 attribute indicating a symbolic link, junction, or other reparse point.</summary>
    const uint ReparsePointAttribute = 0x00000400;
    /// <summary>Win32 attribute indicating a directory rather than a regular file.</summary>
    const uint DirectoryAttribute = 0x00000010;

    /// <summary>Returns the production directory containing editor instance descriptors.</summary>
    /// <returns>An absolute path below the current user's local application data.</returns>
    public static string GetRegistryDirectory()
    {
        return Path.Combine(GetAutomationRoot(), RegistryDirectoryName);
    }

    /// <summary>Returns the production directory containing per-instance bearer credentials.</summary>
    /// <returns>An absolute path below the current user's local application data.</returns>
    public static string GetCredentialDirectory()
    {
        return Path.Combine(GetAutomationRoot(), CredentialDirectoryName);
    }

    /// <summary>Creates or secures an absolute private directory for registry or credential data.</summary>
    /// <param name="DirectoryPath">The caller-selected absolute directory.</param>
    /// <returns>The full path after creation and reparse-point checks.</returns>
    internal static string PreparePrivateDirectory(string DirectoryPath)
    {
        EnsureWindowsPlatform();
        if (!Path.IsPathFullyQualified(DirectoryPath))
        {
            throw new ArgumentException("The private storage directory must be an absolute path.", nameof(DirectoryPath));
        }

        string FullPath = Path.GetFullPath(DirectoryPath);
        string PathRoot = Path.GetPathRoot(FullPath);
        if (string.Equals(FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PathRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A filesystem root cannot be used as private storage.", nameof(DirectoryPath));
        }

        EnsureNoReparsePointInPath(FullPath);
        DirectorySecurity Security = CreatePrivateDirectorySecurity();
        DirectoryInfo Directory = FileSystemAclExtensions.CreateDirectory(Security, FullPath);
        FileAttributes Attributes = File.GetAttributes(Directory.FullName);
        if ((Attributes & FileAttributes.Directory) == 0 || (Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Private storage must be a real directory, not a file or reparse point.");
        }

        FileSystemAclExtensions.SetAccessControl(Directory, Security);
        EnsureNoReparsePointInPath(Directory.FullName);
        return Directory.FullName;
    }

    /// <summary>Confirms an initialized private directory remains a real directory inside its supplied path.</summary>
    /// <param name="DirectoryPath">The private root to validate before each filesystem operation.</param>
    internal static void VerifyPrivateDirectory(string DirectoryPath)
    {
        EnsureWindowsPlatform();
        EnsureNoReparsePointInPath(DirectoryPath);
        FileAttributes Attributes = File.GetAttributes(DirectoryPath);
        if ((Attributes & FileAttributes.Directory) == 0 || (Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Private storage directory was replaced by a file or reparse point.");
        }
    }

    /// <summary>Builds a flat entry path from the exact instance identity without exposing it as a path segment.</summary>
    /// <param name="DirectoryPath">The already secured storage root.</param>
    /// <param name="InstanceId">The case-sensitive identity stored in the entry contents.</param>
    /// <param name="Extension">The fixed extension used by the owning store.</param>
    /// <returns>A normalized child path in the storage root.</returns>
    internal static string GetEntryPath(string DirectoryPath, string InstanceId, string Extension)
    {
        if (string.IsNullOrWhiteSpace(InstanceId))
        {
            throw new ArgumentException("An instance identity is required.", nameof(InstanceId));
        }

        if (Extension != ".json" && Extension != ".token")
        {
            throw new ArgumentException("Only registry and credential entries are supported.", nameof(Extension));
        }

        byte[] IdentityBytes = Encoding.UTF8.GetBytes(InstanceId);
        string FileName = Convert.ToHexString(SHA256.HashData(IdentityBytes)).ToLowerInvariant() + Extension;
        string EntryPath = Path.GetFullPath(Path.Combine(DirectoryPath, FileName));
        if (!string.Equals(Path.GetDirectoryName(EntryPath), Path.GetFullPath(DirectoryPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("A local registry entry resolved outside its private directory.");
        }

        return EntryPath;
    }

    /// <summary>Returns private directory permissions with inheritance disabled and only the current user granted.</summary>
    /// <returns>A DACL suitable for a discovery directory.</returns>
    internal static DirectorySecurity CreatePrivateDirectorySecurity()
    {
        SecurityIdentifier UserSid = GetCurrentUserSid();
        DirectorySecurity Security = new();
        Security.SetAccessRuleProtection(true, false);
        Security.AddAccessRule(new FileSystemAccessRule(UserSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        return Security;
    }

    /// <summary>Returns file permissions with inheritance disabled and only the current user granted.</summary>
    /// <returns>A DACL suitable for a descriptor, temporary publish file, or credential file.</returns>
    internal static FileSecurity CreatePrivateFileSecurity()
    {
        SecurityIdentifier UserSid = GetCurrentUserSid();
        FileSecurity Security = new();
        Security.SetAccessRuleProtection(true, false);
        Security.AddAccessRule(new FileSystemAccessRule(UserSid, FileSystemRights.FullControl, AccessControlType.Allow));
        return Security;
    }

    /// <summary>Opens one regular private file for read and conditional deletion without following a reparse point.</summary>
    /// <param name="EntryPath">The checked file path whose identity must remain attached to its open handle.</param>
    /// <returns>An open handle with read and delete access to the exact regular file at the path.</returns>
    internal static SafeFileHandle OpenRegularFileForConditionalDelete(string EntryPath)
    {
        SafeFileHandle Handle = CreateFileW(EntryPath, GenericReadAccess | DeleteAccess,
            (uint)(FileShare.Read | FileShare.Write | FileShare.Delete), IntPtr.Zero, 3, FileFlagOpenReparsePoint, IntPtr.Zero);
        if (Handle.IsInvalid)
        {
            int ErrorCode = Marshal.GetLastWin32Error();
            Handle.Dispose();
            throw new Win32Exception(ErrorCode, $"Windows could not open the private file for identity-checked removal (Win32 error {ErrorCode}: {new Win32Exception(ErrorCode).Message}).");
        }

        try
        {
            if (!GetFileInformationByHandleEx(Handle, FileAttributeTagInformationClass, out NativeFileAttributeTagInformation Information, (uint)Marshal.SizeOf<NativeFileAttributeTagInformation>()))
            {
                int ErrorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(ErrorCode, $"Windows could not inspect the opened private file (Win32 error {ErrorCode}: {new Win32Exception(ErrorCode).Message}).");
            }

            if ((Information.FileAttributes & ReparsePointAttribute) != 0 || (Information.FileAttributes & DirectoryAttribute) != 0)
            {
                throw new IOException("Conditional deletion requires an opened regular file, not a directory or reparse point.");
            }

            return Handle;
        }
        catch
        {
            Handle.Dispose();
            throw;
        }
    }

    /// <summary>Marks the exact file object referenced by a validated handle for deletion.</summary>
    /// <param name="Handle">The regular private file handle whose identity has already been validated.</param>
    internal static void DeleteOpenedFile(SafeFileHandle Handle)
    {
        NativeFileStandardInformation InitialInformation = GetFileStandardInformation(Handle);
        if (InitialInformation.NumberOfLinks == 0)
        {
            return;
        }

        NativeFileDispositionInformation Information = new()
        {
            DeleteFile = 1
        };
        if (!SetFileInformationByHandle(Handle, FileDispositionInformationClass, ref Information, (uint)Marshal.SizeOf<NativeFileDispositionInformation>()))
        {
            int ErrorCode = Marshal.GetLastWin32Error();
            if (ErrorCode == 5 && GetFileStandardInformation(Handle).NumberOfLinks == 0)
            {
                return;
            }

            throw new Win32Exception(ErrorCode, $"Windows could not remove the validated private file handle (Win32 error {ErrorCode}: {new Win32Exception(ErrorCode).Message}).");
        }
    }

    /// <summary>Reads standard metadata from a file handle to determine whether its name was already replaced.</summary>
    /// <param name="Handle">The validated file object whose remaining link count should be inspected.</param>
    /// <returns>The standard file metadata reported for the opened object.</returns>
    static NativeFileStandardInformation GetFileStandardInformation(SafeFileHandle Handle)
    {
        if (!GetFileInformationByHandleEx(Handle, FileStandardInformationClass, out NativeFileStandardInformation Information, (uint)Marshal.SizeOf<NativeFileStandardInformation>()))
        {
            int ErrorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(ErrorCode, $"Windows could not inspect the opened private file's link count (Win32 error {ErrorCode}: {new Win32Exception(ErrorCode).Message}).");
        }

        return Information;
    }

    /// <summary>Ensures callers do not use a platform where the Windows ACL guarantee is unavailable.</summary>
    internal static void EnsureWindowsPlatform()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Editor discovery storage requires Windows filesystem ACL support.");
        }
    }

    /// <summary>Returns the current process user's SID or fails before creating private storage.</summary>
    /// <returns>The user SID whose access alone is granted to local discovery files.</returns>
    static SecurityIdentifier GetCurrentUserSid()
    {
        using WindowsIdentity Identity = WindowsIdentity.GetCurrent();
        return Identity.User ?? throw new InvalidOperationException("Windows did not provide a user SID for private editor storage.");
    }

    /// <summary>Native FILE_ATTRIBUTE_TAG_INFO returned for the exact file object opened by handle.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct NativeFileAttributeTagInformation
    {
        /// <summary>Gets the file attributes associated with the opened file object.</summary>
        public uint FileAttributes;

        /// <summary>Gets the tag identifying the reparse-point kind when the object is a reparse point.</summary>
        public uint ReparseTag;
    }

    /// <summary>Native FILE_DISPOSITION_INFO payload that marks an opened file for deletion.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct NativeFileDispositionInformation
    {
        /// <summary>Gets or sets whether the opened file object should be deleted when its handles close.</summary>
        public byte DeleteFile;
    }

    /// <summary>Native FILE_STANDARD_INFO returned for one open file object.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct NativeFileStandardInformation
    {
        /// <summary>Gets the number of filesystem bytes reserved by the file.</summary>
        public long AllocationSize;

        /// <summary>Gets the logical byte length of the file.</summary>
        public long EndOfFile;

        /// <summary>Gets the number of directory entries that currently name the file object.</summary>
        public uint NumberOfLinks;

        /// <summary>Gets whether deletion has already been requested for the file object.</summary>
        public byte DeletePending;

        /// <summary>Gets whether the file object is a directory.</summary>
        public byte Directory;
    }

    /// <summary>Opens a file with optional reparse-point following behavior and explicit share permissions.</summary>
    /// <param name="FileName">The absolute file path to open.</param>
    /// <param name="DesiredAccess">The read and delete access requested for the file.</param>
    /// <param name="ShareMode">The access other handles may request while this handle remains open.</param>
    /// <param name="SecurityAttributes">An optional security descriptor for a newly created file.</param>
    /// <param name="CreationDisposition">The Win32 open-existing disposition used for the file.</param>
    /// <param name="FlagsAndAttributes">The flags that open the reparse point itself for inspection.</param>
    /// <param name="TemplateFile">An unused optional template handle.</param>
    /// <returns>A safe handle for the exact opened filesystem object.</returns>
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string FileName, uint DesiredAccess, uint ShareMode, IntPtr SecurityAttributes, uint CreationDisposition, uint FlagsAndAttributes, IntPtr TemplateFile);

    /// <summary>Queries selected metadata from the opened file object without resolving its path again.</summary>
    /// <param name="Handle">The handle whose metadata should be read.</param>
    /// <param name="FileInformationClass">The native class value for FILE_ATTRIBUTE_TAG_INFO.</param>
    /// <param name="Information">The file attributes and reparse tag returned by Windows.</param>
    /// <param name="BufferSize">The exact size of the native information structure.</param>
    /// <returns>True when Windows filled the requested metadata structure.</returns>
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetFileInformationByHandleEx(SafeFileHandle Handle, int FileInformationClass, out NativeFileAttributeTagInformation Information, uint BufferSize);

    /// <summary>Queries standard metadata from an opened filesystem object without resolving its path again.</summary>
    /// <param name="Handle">The handle whose standard metadata should be read.</param>
    /// <param name="FileInformationClass">The native class value for FILE_STANDARD_INFO.</param>
    /// <param name="Information">The allocation, length, link count, and deletion state returned by Windows.</param>
    /// <param name="BufferSize">The exact size of the native information structure.</param>
    /// <returns>True when Windows filled the requested metadata structure.</returns>
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetFileInformationByHandleEx(SafeFileHandle Handle, int FileInformationClass, out NativeFileStandardInformation Information, uint BufferSize);

    /// <summary>Applies file disposition to the file object referenced by an open handle.</summary>
    /// <param name="Handle">The validated regular file handle to mark for deletion.</param>
    /// <param name="FileInformationClass">The native class value for FILE_DISPOSITION_INFO.</param>
    /// <param name="Information">The delete disposition requested for this file object.</param>
    /// <param name="BufferSize">The exact size of the native disposition structure.</param>
    /// <returns>True when Windows accepted the disposition on the opened file object.</returns>
    [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetFileInformationByHandle(SafeFileHandle Handle, int FileInformationClass, ref NativeFileDispositionInformation Information, uint BufferSize);

    /// <summary>Rejects any existing path component that is a symbolic link, junction, or other reparse point.</summary>
    /// <param name="FullPath">The absolute root path to check without following reparse points.</param>
    static void EnsureNoReparsePointInPath(string FullPath)
    {
        string PathRoot = Path.GetPathRoot(FullPath);
        if (string.IsNullOrEmpty(PathRoot))
        {
            throw new ArgumentException("Private storage path must have a filesystem root.", nameof(FullPath));
        }

        string CurrentPath = PathRoot;
        string RelativePath = FullPath.Substring(PathRoot.Length);
        string[] Segments = RelativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string Segment in Segments)
        {
            CurrentPath = Path.Combine(CurrentPath, Segment);
            if (TryGetAttributes(CurrentPath, out FileAttributes Attributes) && (Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Private storage path contains a reparse point: '{CurrentPath}'.");
            }
        }
    }

    /// <summary>Reads file attributes without following a missing path into a fallback location.</summary>
    /// <param name="PathName">The exact path whose attributes should be queried.</param>
    /// <param name="Attributes">The discovered attributes, or zero when the path does not exist.</param>
    /// <returns>True when an entry exists at the path.</returns>
    internal static bool TryGetAttributes(string PathName, out FileAttributes Attributes)
    {
        try
        {
            Attributes = File.GetAttributes(PathName);
            return true;
        }
        catch (FileNotFoundException)
        {
            Attributes = 0;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            Attributes = 0;
            return false;
        }
    }

    /// <summary>Resolves the stable production root below LocalApplicationData without creating it.</summary>
    /// <returns>The per-user Helengine editor MCP storage root.</returns>
    static string GetAutomationRoot()
    {
        string LocalApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(LocalApplicationData) || !Path.IsPathFullyQualified(LocalApplicationData))
        {
            throw new InvalidOperationException("Windows LocalApplicationData is unavailable for private editor discovery.");
        }

        return Path.Combine(LocalApplicationData, ProductDirectoryName, AutomationDirectoryName);
    }
}
