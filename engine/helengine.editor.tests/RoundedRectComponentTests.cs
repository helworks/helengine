using helengine;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the shared rounded-rect primitive exposes corner selection.
    /// </summary>
    public sealed class RoundedRectComponentTests {
        /// <summary>
        /// Ensures the default rounded rectangle keeps all corners enabled and can be switched to a square mask.
        /// </summary>
        [Fact]
        public void Constructor_UsesAllCorners_AndSquareCornersClearTheMask() {
            RoundedRectComponent shape = new RoundedRectComponent();

            Assert.Equal(RoundedRectCorners.All, shape.Corners);

            shape.Corners = RoundedRectCorners.None;

            Assert.Equal(RoundedRectCorners.None, shape.Corners);
        }
    }
}
