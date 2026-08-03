using helengine.vfx;
using helengine.vfx.cli;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.cli.tests {
    /// <summary>
    /// Covers the pre-GPU parameter validation pass. These are the checks that turn a mistyped
    /// --param from a silent run-with-defaults into an immediate, actionable failure, and they must
    /// work without any Direct3D11 device being created.
    /// </summary>
    public class VfxCliParameterValidatorTests {
        /// <summary>
        /// Parameters the effect actually declares must pass validation untouched.
        /// </summary>
        [Fact]
        public void TryValidate_KnownParameters_Succeeds() {
            IVfxEffect effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> {
                ["HueCyclesPerClip"] = "2",
                ["Easing"] = "EaseInOut"
            };

            bool result = VfxCliParameterValidator.TryValidate(effect, values, out string error);

            Assert.True(result);
            Assert.Null(error);
        }

        /// <summary>
        /// A mistyped parameter name must fail and the message must both name the offender and list the
        /// parameters the effect really accepts.
        /// </summary>
        [Fact]
        public void TryValidate_UnknownParameterName_FailsAndListsValidParameters() {
            IVfxEffect effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["HueCyclesPerClips"] = "5" };

            bool result = VfxCliParameterValidator.TryValidate(effect, values, out string error);

            Assert.False(result);
            Assert.Contains("HueCyclesPerClips", error);
            Assert.Contains("HueCyclesPerClip ", error);
            Assert.Contains("BackgroundColor", error);
        }

        /// <summary>
        /// Parameter names are case sensitive; a near-miss must be reported rather than quietly ignored.
        /// </summary>
        [Fact]
        public void TryValidate_WrongCaseParameterName_Fails() {
            IVfxEffect effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["startscale"] = "2" };

            bool result = VfxCliParameterValidator.TryValidate(effect, values, out string error);

            Assert.False(result);
            Assert.Contains("startscale", error);
        }

        /// <summary>
        /// A known parameter with an unusable value must be caught here, before device creation, and
        /// reported as a message instead of escaping as an unhandled ArgumentException.
        /// </summary>
        [Fact]
        public void TryValidate_InvalidParameterValue_FailsWithoutThrowing() {
            IVfxEffect effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["Easing"] = "NotARealEasing" };

            bool result = VfxCliParameterValidator.TryValidate(effect, values, out string error);

            Assert.False(result);
            Assert.Contains("Easing", error);
        }

        /// <summary>
        /// A malformed color must be rejected by the same pre-GPU pass.
        /// </summary>
        [Fact]
        public void TryValidate_InvalidBackgroundColor_FailsWithoutThrowing() {
            IVfxEffect effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["BackgroundColor"] = "1,2" };

            bool result = VfxCliParameterValidator.TryValidate(effect, values, out string error);

            Assert.False(result);
            Assert.Contains("BackgroundColor", error);
        }
    }
}
