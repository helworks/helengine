using helengine.vfx.cli;
using Xunit;

namespace helengine.vfx.cli.tests {
    public class VfxCliArgumentsTests {
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
        }

        [Fact]
        public void TryParse_MissingRequiredArgument_Fails() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryParse_ParamArguments_AreCollected() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out", "--param", "HueCyclesPerClip=2", "--param", "StartScale=0.5" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Equal("2", parsed.ParameterValues["HueCyclesPerClip"]);
            Assert.Equal("0.5", parsed.ParameterValues["StartScale"]);
        }

        [Fact]
        public void TryParse_MalformedParam_Fails() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out", "--param", "NoEqualsSign" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }

        [Fact]
        public void TryParse_UnknownArgument_Fails() {
            string[] args = { "--nonsense", "value" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }
    }
}
