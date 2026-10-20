namespace helengine.recording.windows;
/// <summary>File operations with explicit exclusive creation and non-overwriting transfer semantics.</summary>
interface IRecordingFileSystem {
    /// <summary>Checks whether a destination is already occupied.</summary>
    bool Exists(string path);
    /// <summary>Identifies whether a rename can stay within one volume.</summary>
    bool IsSameVolume(string source, string destination);
    /// <summary>Moves without replacing any destination created by a competing process.</summary>
    void MoveNoOverwrite(string source, string destination);
    /// <summary>Creates a file only if absent, transferring ownership on successful return.</summary>
    Stream CreateNew(string path);
    /// <summary>Opens the completed work file for reading.</summary>
    Stream OpenRead(string path);
    /// <summary>Removes a path that the recording session actually created.</summary>
    void Delete(string ownedPath);
}
