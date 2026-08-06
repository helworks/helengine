using helengine.vfx;
using helengine.vfx.cli;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.cli.tests {
    /// <summary>
    /// Covers the pre-GPU input-role validation pass: the checks that turn a mistyped or missing
    /// --input role from an obscure folder-not-found error into an immediate, actionable failure.
    /// </summary>
    public class VfxCliInputValidatorTests {
        /// <summary>
        /// Every role the effect declares, and no others, must pass validation untouched.
        /// </summary>
        [Fact]
        public void TryValidate_KnownRoles_Succeeds() {
            IVfxEffect effect = new RainbowExpandEffect();
            var inputFolders = new Dictionary<string, string> { ["Source"] = "src", ["Mask"] = "mask" };

            bool result = VfxCliInputValidator.TryValidate(effect, inputFolders, out string error);

            Assert.True(result);
            Assert.Null(error);
        }

        /// <summary>
        /// A three-role effect (DepthComposite) must also validate cleanly when every role is supplied.
        /// </summary>
        [Fact]
        public void TryValidate_ThreeRoleEffect_KnownRoles_Succeeds() {
            IVfxEffect effect = new DepthCompositeEffect();
            var inputFolders = new Dictionary<string, string> {
                ["Subject"] = "subject",
                ["RenderColor"] = "render-color",
                ["RenderDepth"] = "render-depth"
            };

            bool result = VfxCliInputValidator.TryValidate(effect, inputFolders, out string error);

            Assert.True(result);
            Assert.Null(error);
        }

        /// <summary>
        /// A mistyped role name must fail and the message must both name the offender and list the
        /// roles the effect really requires.
        /// </summary>
        [Fact]
        public void TryValidate_UnknownRoleName_FailsAndListsRequiredRoles() {
            IVfxEffect effect = new RainbowExpandEffect();
            var inputFolders = new Dictionary<string, string> { ["Souce"] = "src", ["Mask"] = "mask" };

            bool result = VfxCliInputValidator.TryValidate(effect, inputFolders, out string error);

            Assert.False(result);
            Assert.Contains("Souce", error);
            Assert.Contains("Source", error);
            Assert.Contains("Mask", error);
        }

        /// <summary>
        /// A role the effect requires but the caller never supplied must fail rather than run with a
        /// missing input.
        /// </summary>
        [Fact]
        public void TryValidate_MissingRequiredRole_Fails() {
            IVfxEffect effect = new DepthCompositeEffect();
            var inputFolders = new Dictionary<string, string> { ["Subject"] = "subject", ["RenderColor"] = "render-color" };

            bool result = VfxCliInputValidator.TryValidate(effect, inputFolders, out string error);

            Assert.False(result);
            Assert.Contains("RenderDepth", error);
        }
    }
}
