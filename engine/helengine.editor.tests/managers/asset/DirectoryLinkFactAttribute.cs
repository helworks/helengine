using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Runs a filesystem-link test only when this process can create a real
/// directory link. Discovery performs the capability probe and reports an
/// explicit skip reason when the operating system or process policy rejects it.
/// </summary>
sealed class DirectoryLinkFactAttribute : FactAttribute {
    public DirectoryLinkFactAttribute() {
        string probeRoot = Path.Combine(Path.GetTempPath(), "helengine-directory-link-probe-" + Guid.NewGuid().ToString("N"));
        string targetPath = Path.Combine(probeRoot, "target");
        string linkPath = Path.Combine(probeRoot, "link");
        try {
            Directory.CreateDirectory(targetPath);
            Directory.CreateSymbolicLink(linkPath, targetPath);
        } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is PlatformNotSupportedException) {
            Skip = $"Directory-link capability is unavailable: {exception.Message}";
        } finally {
            if (Directory.Exists(linkPath)) {
                Directory.Delete(linkPath);
            }
            if (Directory.Exists(probeRoot)) {
                Directory.Delete(probeRoot, true);
            }
        }
    }
}
