using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the shared shader package pipeline recognizes the PS Vita target as a first-class compile target.
    /// </summary>
    public class PsVitaShaderPackagePipelineTests {
        /// <summary>
        /// Ensures the stable target-name mapping returns the expected lowercase PS Vita identifier.
        /// </summary>
        [Fact]
        public void GetTargetName_whenTargetIsPsVita_returnsPsvita() {
            Assert.Equal("psvita", ShaderTargetNames.GetTargetName(ShaderCompileTarget.PsVita));
        }

        /// <summary>
        /// Ensures the target-name parser resolves the PS Vita identifier back into the compile-target enum value.
        /// </summary>
        [Fact]
        public void TryParseTarget_whenNameIsPsvita_returnsPsVita() {
            Assert.True(ShaderTargetNames.TryParseTarget("psvita", out ShaderCompileTarget target));
            Assert.Equal(ShaderCompileTarget.PsVita, target);
        }
    }
}
