namespace helengine.recording.windows;
/// <summary>Transfers only finalized work files without replacing existing destinations.</summary>
sealed class RecordingOutputPublisher {
    /// <summary>All operations use explicit exclusive creation and ownership.</summary>
    readonly IRecordingFileSystem Files;
    /// <summary>Creates a publisher with native or controlled filesystem operations.</summary>
    internal RecordingOutputPublisher(IRecordingFileSystem files) { Files = files; }
    /// <summary>Checks preflight occupancy without treating that check as reservation.</summary>
    internal bool Exists(string path) => Files.Exists(path);
    /// <summary>Reserves an exclusively named work file under the authorized scratch root.</summary>
    internal string CreateWorkFile() {
        const string root = "C:\\temp\\helengine-window-recording"; Directory.CreateDirectory(root);
        string path = Path.Combine(root, "recording-" + Guid.NewGuid().ToString("N") + ".mp4"); using var reservation = Files.CreateNew(path); return path;
    }
    /// <summary>Removes only the session-owned work file supplied by its worker.</summary>
    internal void DeleteWorkFile(string ownedPath) => Files.Delete(ownedPath);
    /// <summary>Publishes after trailer completion, using non-overwrite move or exclusive cross-volume copy.</summary>
    internal void Publish(string completedWorkPath, string outputPath) {
        if (Files.IsSameVolume(completedWorkPath, outputPath)) { Files.MoveNoOverwrite(completedWorkPath, outputPath); return; }
        bool ownedTarget = false;
        try {
            using var target = Files.CreateNew(outputPath); ownedTarget = true;
            using var source = Files.OpenRead(completedWorkPath); source.CopyTo(target); target.Flush();
        } catch (Exception original) {
            if (ownedTarget) {
                try { Files.Delete(outputPath); } catch (Exception cleanup) { throw new AggregateException("Publication and partial-file cleanup failed.", original, cleanup); }
            }
            throw;
        }
        try { Files.Delete(completedWorkPath); } catch (Exception cleanup) { throw new RecordingPublicationException(cleanup); }
    }
}
