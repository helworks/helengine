using helengine.vfx.cli;
using Xunit;

namespace helengine.vfx.cli.tests {
    /// <summary>
    /// Covers command-line parsing: the repeatable --input Role=folder and --param name=value syntax,
    /// the required flags an export run needs, and the help form that deliberately relaxes them.
    /// </summary>
    public class VfxCliArgumentsTests {
        /// <summary>
        /// Every --input occurrence must accumulate into the input-folder dictionary, keyed by role.
        /// </summary>
        [Fact]
        public void TryParse_AllRequiredArguments_Succeeds() {
            string[] args = { "--input", "Source=src", "--input", "Mask=mask", "--effect", "rainbow-expand", "--out", "out" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Null(error);
            Assert.Equal("src", parsed.InputFolders["Source"]);
            Assert.Equal("mask", parsed.InputFolders["Mask"]);
            Assert.Equal("rainbow-expand", parsed.EffectId);
            Assert.Equal("out", parsed.OutputFolder);
            Assert.False(parsed.ShowHelp);
        }

        /// <summary>
        /// A run with no --input at all, or a missing --effect/--out, must fail with usage text rather
        /// than running with an empty input set or a null folder.
        /// </summary>
        [Fact]
        public void TryParse_MissingRequiredArgument_Fails() {
            string[] args = { "--input", "Source=src", "--input", "Mask=mask", "--effect", "rainbow-expand" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
            Assert.NotNull(error);
        }

        /// <summary>
        /// An --input value without an equals sign is ambiguous and must be rejected.
        /// </summary>
        [Fact]
        public void TryParse_MalformedInput_Fails() {
            string[] args = { "--input", "NoEqualsSign", "--effect", "rainbow-expand", "--out", "out" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }

        /// <summary>
        /// Every --param occurrence must accumulate into the parameter dictionary.
        /// </summary>
        [Fact]
        public void TryParse_ParamArguments_AreCollected() {
            string[] args = {
                "--input", "Source=src", "--input", "Mask=mask", "--effect", "rainbow-expand", "--out", "out",
                "--param", "HueCyclesPerClip=2", "--param", "StartScale=0.5"
            };

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
            string[] args = { "--input", "Source=src", "--input", "Mask=mask", "--effect", "rainbow-expand", "--out", "out", "--param", "NoEqualsSign" };

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
