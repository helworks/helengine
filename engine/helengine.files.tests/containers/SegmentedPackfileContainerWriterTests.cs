using helengine.baseplatform.Manifest;
using helengine.files;

namespace helengine.files.tests.containers {
    /// <summary>
    /// Verifies segmented packfile output emits a stable mock segment manifest.
    /// </summary>
    public sealed class SegmentedPackfileContainerWriterTests : IDisposable {
        readonly string SourceRootPath;
        readonly string OutputRootPath;

        public SegmentedPackfileContainerWriterTests() {
            string root = Path.Combine(Path.GetTempPath(), "helengine-segmented-packfile-container-writer-tests", Guid.NewGuid().ToString("N"));
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
        public void Write_splits_large_sources_into_segment_manifests() {
            SegmentedPackfileContainerWriter writer = new(new PackfileWritePlan("container-0", 4));
            PlatformBuildManifest manifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0-engine",
                Array.Empty<PlatformBuildScene>(),
                Array.Empty<PlatformBuildAsset>());

            writer.Write(manifest, SourceRootPath, OutputRootPath);

            string[] segmentFiles = Directory.GetFiles(OutputRootPath, "container-0.segment-*.pack.json");
            Assert.NotEmpty(segmentFiles);
            string firstSegment = File.ReadAllText(segmentFiles[0]);
            Assert.Contains("root.txt", firstSegment, StringComparison.OrdinalIgnoreCase);
        }
    }
}
