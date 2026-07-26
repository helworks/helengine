using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shader compilation target selection for headless editor builds.
    /// </summary>
    public sealed class EditorCliBuildRunnerTests {
        /// <summary>
        /// Ensures a PS Vita build requests the device-backed PS Vita shader compiler target.
        /// </summary>
        [Fact]
        public void Build_WhenTargetPlatformIsPsVita_SelectsThePsVitaShaderCompileTarget() {
            ShaderCompileTarget target = EditorCliBuildRunner.ResolveShaderCompileTarget("psvita");

            Assert.Equal(ShaderCompileTarget.PsVita, target);
        }

    }
}
