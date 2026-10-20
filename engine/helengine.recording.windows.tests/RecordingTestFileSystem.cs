namespace helengine.recording.windows.tests;
/// <summary>Models filesystem race and cross-volume behavior without requiring a second disk.</summary>
sealed class RecordingTestFileSystem : IRecordingFileSystem {
    /// <summary>Exclusive paths and their current bytes.</summary>
    internal readonly Dictionary<string, byte[]> Files = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Selects the cross-volume copy path.</summary>
    internal bool CrossVolume;
    /// <summary>Models an absent output parent without writing outside the test scratch directory.</summary>
    internal bool ParentDirectoryExists = true;
    /// <summary>Injects a partial copy failure.</summary>
    internal bool FailCopy;
    /// <summary>Injects a destination created after preflight.</summary>
    internal Action BeforePublish;
    /// <summary>Reads metadata without creating anything.</summary>
    public bool Exists(string path) => Files.ContainsKey(path);
    /// <summary>Represents an absent parent or a file occupying its path.</summary>
    public bool DirectoryExists(string path) => ParentDirectoryExists && !Files.ContainsKey(path);
    /// <summary>Represents the configured volume relationship.</summary>
    public bool IsSameVolume(string source, string destination) => !CrossVolume;
    /// <summary>Transfers the completed bytes without overwriting an existing destination.</summary>
    public void MoveNoOverwrite(string source, string destination) {
        BeforePublish?.Invoke();
        if (Files.ContainsKey(destination)) { throw new IOException("destination exists"); }
        Files.Add(destination, Files[source]); Files.Remove(source);
    }
    /// <summary>Creates one exclusive file, including for work-file reservations.</summary>
    public Stream CreateNew(string path) {
        BeforePublish?.Invoke();
        if (Files.ContainsKey(path)) { throw new IOException("destination exists"); }
        Files.Add(path, []); return new RecordingTestWriteStream(bytes => Files[path] = bytes, FailCopy);
    }
    /// <summary>Reads only completed fixture bytes.</summary>
    public Stream OpenRead(string path) => new MemoryStream(Files[path], false);
    /// <summary>Removes a path the caller owns.</summary>
    public void Delete(string ownedPath) => Files.Remove(ownedPath);
}
