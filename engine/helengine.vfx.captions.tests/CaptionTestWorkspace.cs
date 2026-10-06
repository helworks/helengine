namespace helengine.vfx.captions.tests;

/// <summary>Owns test artifacts in a visible caller-selected workspace folder, never the system temp folder.</summary>
sealed class CaptionTestWorkspace : IDisposable {
    /// <summary>Creates an isolated child of the required test artifact root.</summary>
    public CaptionTestWorkspace() {
        string root = Environment.GetEnvironmentVariable("HELWORKS_CAPTION_TEST_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(root)) {
            throw new InvalidOperationException("Set HELWORKS_CAPTION_TEST_ARTIFACTS to a workspace-owned validation directory.");
        }
        DirectoryPath = Path.Combine(Path.GetFullPath(root), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>Gets this test's unique visible artifact directory.</summary>
    public string DirectoryPath { get; }

    /// <summary>Retains artifacts for inspection; no test data or outputs are silently deleted.</summary>
    public void Dispose() { }
}
