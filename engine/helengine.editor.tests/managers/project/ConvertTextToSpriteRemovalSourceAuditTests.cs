using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Locks the full removal of the abandoned build-time text-to-sprite pipeline.
    /// </summary>
    public sealed class ConvertTextToSpriteRemovalSourceAuditTests {
        /// <summary>
        /// Ensures the shared text component no longer exposes the removed authoring flag.
        /// </summary>
        [Fact]
        public void TextComponent_whenBuildTimeTextSpriteCleanupIsComplete_doesNotExposeConvertTextToSprite() {
            string textComponentSourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.core",
                "components",
                "2d",
                "TextComponent.cs"));
            string sourceText = File.ReadAllText(textComponentSourcePath);

            Assert.DoesNotContain("ConvertTextToSprite", sourceText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the packaging transform no longer carries the removed generated text-sprite path.
        /// </summary>
        [Fact]
        public void SceneComponentPackagingTransformService_whenBuildTimeTextSpriteCleanupIsComplete_doesNotReferenceTheBakePipeline() {
            string transformServiceSourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.editor",
                "managers",
                "project",
                "SceneComponentPackagingTransformService.cs"));
            string sourceText = File.ReadAllText(transformServiceSourcePath);

            Assert.DoesNotContain("TextComponentSpriteBakeService", sourceText, StringComparison.Ordinal);
            Assert.DoesNotContain("ConvertTextToSprite", sourceText, StringComparison.Ordinal);
            Assert.DoesNotContain("generated/text-sprites", sourceText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the removed bake service files are gone from the editor project.
        /// </summary>
        [Fact]
        public void EditorProject_whenBuildTimeTextSpriteCleanupIsComplete_doesNotContainTheBakeServiceFiles() {
            string editorProjectRootPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.editor",
                "managers",
                "project"));
            string bakeInterfacePath = Path.Combine(editorProjectRootPath, "ITextComponentSpriteBakeService.cs");
            string bakeServicePath = Path.Combine(editorProjectRootPath, "TextComponentSpriteBakeService.cs");

            Assert.False(File.Exists(bakeInterfacePath));
            Assert.False(File.Exists(bakeServicePath));
        }

        /// <summary>
        /// Ensures the build packager no longer wires the removed bake service into packaging.
        /// </summary>
        [Fact]
        public void EditorWindowsBuildScenePackager_whenBuildTimeTextSpriteCleanupIsComplete_doesNotReferenceTheBakeService() {
            string packagerSourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "helengine.editor",
                "managers",
                "project",
                "EditorWindowsBuildScenePackager.cs"));
            string sourceText = File.ReadAllText(packagerSourcePath);

            Assert.DoesNotContain("TextComponentSpriteBakeService", sourceText, StringComparison.Ordinal);
        }
    }
}
