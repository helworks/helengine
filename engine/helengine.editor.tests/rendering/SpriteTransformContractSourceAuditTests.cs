using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Locks the sprite-rendering contract so entity transforms remain authoritative for sprite rotation and scale.
    /// </summary>
    public sealed class SpriteTransformContractSourceAuditTests {

        /// <summary>
        /// Reads one source file directly from the engine tree so the tests can assert structural contracts without executing the renderer.
        /// </summary>
        /// <param name="projectName">Engine project folder that owns the target source file.</param>
        /// <param name="relativeSegments">Relative path segments beneath the project directory.</param>
        /// <returns>Full source text for the requested file.</returns>
        static string ReadSource(string projectName, params string[] relativeSegments) {
            string[] fullSegments = new string[relativeSegments.Length + 6];
            fullSegments[0] = AppContext.BaseDirectory;
            fullSegments[1] = "..";
            fullSegments[2] = "..";
            fullSegments[3] = "..";
            fullSegments[4] = "..";
            fullSegments[5] = projectName;
            for (int index = 0; index < relativeSegments.Length; index++) {
                fullSegments[index + 6] = relativeSegments[index];
            }

            string sourcePath = Path.GetFullPath(Path.Combine(fullSegments));
            return File.ReadAllText(sourcePath);
        }
    }
}
