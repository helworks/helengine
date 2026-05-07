using helengine.demo_disc_scene_writer;
using Xunit;

namespace helengine.editor.tests.tools {
    /// <summary>
    /// Verifies the committed rendering scene writer generates user-side attract-mode source files and showcase scenes.
    /// </summary>
    public sealed class RenderingSceneWriterTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current rendering scene writer test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root with the minimum authored assets tree required by the writer.
        /// </summary>
        public RenderingSceneWriterTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-rendering-scene-writer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        }

        /// <summary>
        /// Deletes the temporary project root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures rendering-scene generation writes the user-side attract-mode components beneath the rendering codebase folder.
        /// </summary>
        [Fact]
        public void WriteAll_WhenRenderingShowcaseIsGenerated_WritesMotionSourceFilesUnderCodebaseRendering() {
            RenderingSceneWriter writer = new RenderingSceneWriter();

            writer.WriteAll(ProjectRootPath);

            string renderingCodeRootPath = Path.Combine(ProjectRootPath, "assets", "codebase", "rendering");
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowTowerSpinComponent.cs")));
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowOrbitComponent.cs")));
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowSunSweepComponent.cs")));
            Assert.True(File.Exists(Path.Combine(renderingCodeRootPath, "DirectionalShadowCameraOrbitComponent.cs")));
        }
    }
}
