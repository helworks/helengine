using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Locks shared runtime ownership seams that managed tests cannot observe directly in native builds.
    /// </summary>
    public sealed class RuntimeOwnershipSourceAuditTests {

        /// <summary>
        /// Reads one source file from the engine tree used by native ownership audits.
        /// </summary>
        /// <param name="segments">Relative path segments under the engine folder.</param>
        /// <returns>Full source text.</returns>
        static string ReadSource(params string[] segments) {
            string[] fullSegments = new string[segments.Length + 5];
            fullSegments[0] = AppContext.BaseDirectory;
            fullSegments[1] = "..";
            fullSegments[2] = "..";
            fullSegments[3] = "..";
            fullSegments[4] = "..";
            for (int index = 0; index < segments.Length; index++) {
                fullSegments[index + 5] = segments[index];
            }

            string sourcePath = Path.GetFullPath(Path.Combine(fullSegments));
            return File.ReadAllText(sourcePath);
        }
    }
}
