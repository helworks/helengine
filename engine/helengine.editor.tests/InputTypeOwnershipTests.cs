using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shared input-facing types are owned by helengine.core instead of being duplicated in helengine.input.
    /// </summary>
    public sealed class InputTypeOwnershipTests {
        /// <summary>
        /// Ensures the shared integer vector type is defined by the core assembly.
        /// </summary>
        [Fact]
        public void int2_IsOwnedByHelengineCore() {
            Assert.Equal(typeof(Core).Assembly, typeof(int2).Assembly);
        }

        /// <summary>
        /// Ensures the input system surface is defined by the core assembly.
        /// </summary>
        [Fact]
        public void InputSystem_IsOwnedByHelengineCore() {
            Assert.Equal(typeof(Core).Assembly, typeof(InputSystem).Assembly);
        }
    }
}
