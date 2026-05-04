using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the editor container writer selects the loose-file backend for install-tree layouts.
    /// </summary>
    public sealed class EditorPlatformContainerWriterTests : IDisposable {
        readonly string SourceRootPath;
        readonly string OutputRootPath;

        public EditorPlatformContainerWriterTests() {
            string root = Path.Combine(Path.GetTempPath(), "helengine-editor-container-writer-tests", Guid.NewGuid().ToString("N"));
            SourceRootPath = Path.Combine(root, "source");
            OutputRootPath = Path.Combine(root, "output");
            Directory.CreateDirectory(SourceRootPath);
            File.WriteAllText(Path.Combine(SourceRootPath, "payload.txt"), "payload");
        }

        public void Dispose() {
            string root = Path.GetDirectoryName(SourceRootPath) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Write_install_tree_copies_source_payloads_to_output_root() {
            EditorPlatformContainerWriter writer = new();
            PlatformBuildManifest manifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0-engine",
                Array.Empty<PlatformBuildScene>(),
                Array.Empty<PlatformBuildAsset>());

            writer.Write(
                manifest,
                SourceRootPath,
                OutputRootPath,
                new PlatformStorageProfileDefinition(
                    "loose-files",
                    "Loose Files",
                    PlatformStorageProfileKind.LooseFiles,
                    "windows-loose-files",
                    false),
                new PlatformMediaProfileDefinition(
                    "windows-install-tree",
                    "Windows Install Tree",
                    PlatformMediaLayoutKind.InstallTree,
                    true,
                    false));

            Assert.Equal("payload", File.ReadAllText(Path.Combine(OutputRootPath, "payload.txt")));
        }
    }
}
