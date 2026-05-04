using helengine.baseplatform.Manifest;
using helengine.files;

namespace helengine.files.tests.containers {
    /// <summary>
    /// Verifies loose-file container writing mirrors cooked payloads.
    /// </summary>
    public sealed class LooseFileContainerWriterTests : IDisposable {
        readonly string SourceRootPath;
        readonly string OutputRootPath;

        public LooseFileContainerWriterTests() {
            string root = Path.Combine(Path.GetTempPath(), "helengine-loose-file-container-writer-tests", Guid.NewGuid().ToString("N"));
            SourceRootPath = Path.Combine(root, "source");
            OutputRootPath = Path.Combine(root, "output");
            Directory.CreateDirectory(Path.Combine(SourceRootPath, "nested"));
            File.WriteAllText(Path.Combine(SourceRootPath, "root.txt"), "root");
            File.WriteAllText(Path.Combine(SourceRootPath, "nested", "child.txt"), "child");
        }

        public void Dispose() {
            string root = Path.GetDirectoryName(SourceRootPath) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Write_copies_source_tree_preserving_relative_paths() {
            LooseFileContainerWriter writer = new();
            PlatformBuildManifest manifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0-engine",
                Array.Empty<PlatformBuildScene>(),
                Array.Empty<PlatformBuildAsset>());

            writer.Write(manifest, SourceRootPath, OutputRootPath);

            Assert.Equal("root", File.ReadAllText(Path.Combine(OutputRootPath, "root.txt")));
            Assert.Equal("child", File.ReadAllText(Path.Combine(OutputRootPath, "nested", "child.txt")));
        }
    }
}
