using helengine.vfx.cli;
using Xunit;

namespace helengine.vfx.cli.tests {
    /// <summary>
    /// Covers command-line parsing: the flags the export run requires, the repeatable --param syntax,
    /// and the help form that deliberately relaxes the required-argument check.
    /// </summary>
    public class VfxCliArgumentsTests {
        /// <summary>
        /// All four required flags present must parse into the corresponding properties.
        /// </summary>
        [Fact]
        public void TryParse_AllRequiredArguments_Succeeds() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Null(error);
            Assert.Equal("src", parsed.SourceFolder);
            Assert.Equal("mask", parsed.MaskFolder);
            Assert.Equal("rainbow-expand", parsed.EffectId);
            Assert.Equal("out", parsed.OutputFolder);
            Assert.False(parsed.ShowHelp);
        }

        /// <summary>
        /// A missing required flag must fail with usage text rather than running with a null folder.
        /// </summary>
        [Fact]
        public void TryParse_MissingRequiredArgument_Fails() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
            Assert.NotNull(error);
        }

        /// <summary>
        /// Every --param occurrence must accumulate into the parameter dictionary.
        /// </summary>
        [Fact]
        public void TryParse_ParamArguments_AreCollected() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out", "--param", "HueCyclesPerClip=2", "--param", "StartScale=0.5" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Equal("2", parsed.ParameterValues["HueCyclesPerClip"]);
            Assert.Equal("0.5", parsed.ParameterValues["StartScale"]);
        }

        /// <summary>
        /// A --param value without an equals sign is ambiguous and must be rejected.
        /// </summary>
        [Fact]
        public void TryParse_MalformedParam_Fails() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out", "--param", "NoEqualsSign" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }

        /// <summary>
        /// An unrecognized flag must fail rather than being ignored.
        /// </summary>
        [Fact]
        public void TryParse_UnknownArgument_Fails() {
            string[] args = { "--nonsense", "value" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }

        /// <summary>
        /// --help on its own must parse successfully even though none of the export flags were given.
        /// </summary>
        [Fact]
        public void TryParse_HelpAlone_SucceedsWithoutRequiredArguments() {
            string[] args = { "--help" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Null(error);
            Assert.True(parsed.ShowHelp);
            Assert.Null(parsed.EffectId);
        }

        /// <summary>
        /// --help combined with --effect must retain the effect id so per-effect parameter help can be printed.
        /// </summary>
        [Fact]
        public void TryParse_HelpWithEffect_RetainsEffectId() {
            string[] args = { "--help", "--effect", "rainbow-expand" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.True(parsed.ShowHelp);
            Assert.Equal("rainbow-expand", parsed.EffectId);
        }
    }
}
