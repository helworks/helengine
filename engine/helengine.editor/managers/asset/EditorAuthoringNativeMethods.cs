using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace helengine.editor {
    /// <summary>Contains the host-native imports used by the verified authoring filesystem algorithms.</summary>
    internal static class EditorAuthoringNativeMethods {
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowsByHandleFileInformation {
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
    internal static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateDirectoryW(string path, IntPtr securityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder filePath, uint filePathLength, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetFileInformationByHandle(SafeFileHandle handle, out WindowsByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        IntPtr fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
    internal static extern bool SetFileInformationByHandleDisposition(
        SafeFileHandle file,
        int fileInformationClass,
        ref WindowsFileDispositionInfoEx fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetFilePointerEx(SafeFileHandle file, long distanceToMove, IntPtr newFilePointer, uint moveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetEndOfFile(SafeFileHandle file);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    internal static extern int NativePosixOpen(string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    internal static extern int NativePosixOpenAt(int directoryFd, string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "mkdirat", SetLastError = true)]
    internal static extern int NativeMkdirAt(int directoryFd, string path, uint mode);

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    internal static extern int NativeRenameAt2(int oldDirectoryFd, string oldPath, int newDirectoryFd, string newPath, uint flags);

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    internal static extern int NativeUnlinkAt(int directoryFd, string path, int flags);

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    internal static extern int NativeFlock(int fileDescriptor, int operation);

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    internal static extern int NativePosixDup(int fileDescriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static extern int PosixClose(int fileDescriptor);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    internal static extern int NativePosixFsync(int fileDescriptor);

    [DllImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
    internal static extern IntPtr NativePosixFdOpenDir(int fileDescriptor);

    [DllImport("libc", EntryPoint = "readdir", SetLastError = true)]
    internal static extern IntPtr NativePosixReadDir(IntPtr directoryStream);

    [DllImport("libc", EntryPoint = "closedir", SetLastError = true)]
    internal static extern int PosixClosedDir(IntPtr directoryStream);

    [DllImport("libc", EntryPoint = "fstatat", SetLastError = true)]
    internal static extern int NativePosixFStatAt(int directoryFd, string path, out LinuxPosixStat status, int flags);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    internal static extern int NativePosixFStat(int fileDescriptor, out LinuxPosixStat status);

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    internal static extern int NativePosixFcntl(int fileDescriptor, int command, int argument);

    const int FileRenameInformation = 3;
    const int FileDispositionInformationEx = 21;
    const uint FileDispositionDelete = 0x00000001;
    const uint FileDispositionPosixSemantics = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileRenameInfoHeader {
        public byte ReplaceIfExists;
        public IntPtr RootDirectory;
        public uint FileNameLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowsFileDispositionInfoEx {
        public uint Flags;
    }

    // Linux x64 struct stat layout. Only the file-type bits in st_mode are
    // consumed; the complete prefix keeps native offsets intact.
    [StructLayout(LayoutKind.Sequential)]
    internal struct LinuxPosixStat {
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

    }
}
