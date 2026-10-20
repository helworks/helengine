using System.Runtime.InteropServices;
using System.Text;
namespace helengine.recording.windows;
/// <summary>Implements exclusive destination ownership and recognizes actual mounted volumes.</summary>
sealed class RecordingFileSystem : IRecordingFileSystem {
    /// <summary>Checks files and directories so neither is overwritten by a recording.</summary>
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    /// <summary>Rejects absent parents and ordinary files used as output directories.</summary>
    public bool DirectoryExists(string path) => Directory.Exists(path);
    /// <summary>Uses volume GUIDs; unsupported/network queries safely select the copy path.</summary>
    public bool IsSameVolume(string source, string destination) {
        var sourceMount = new StringBuilder(32768); var targetMount = new StringBuilder(32768);
        var sourceVolume = new StringBuilder(64); var targetVolume = new StringBuilder(64);
        return GetVolumePathName(Path.GetDirectoryName(Path.GetFullPath(source)), sourceMount, sourceMount.Capacity)
            && GetVolumePathName(Path.GetDirectoryName(Path.GetFullPath(destination)), targetMount, targetMount.Capacity)
            && GetVolumeNameForVolumeMountPoint(sourceMount.ToString(), sourceVolume, sourceVolume.Capacity)
            && GetVolumeNameForVolumeMountPoint(targetMount.ToString(), targetVolume, targetVolume.Capacity)
            && string.Equals(sourceVolume.ToString(), targetVolume.ToString(), StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>Renames without overwriting any path raced into the destination.</summary>
    public void MoveNoOverwrite(string source, string destination) => File.Move(source, destination, false);
    /// <summary>Reserves a new file without truncating an existing one.</summary>
    public Stream CreateNew(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    /// <summary>Reads a completed file without permitting concurrent writers.</summary>
    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    /// <summary>Deletes a session-owned file, tolerating an already moved source.</summary>
    public void Delete(string ownedPath) => File.Delete(ownedPath);
    /// <summary>Resolves the mounted volume path containing a directory.</summary>
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);
    /// <summary>Obtains the stable volume identity even when it has multiple mount points.</summary>
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, int bufferLength);
}
