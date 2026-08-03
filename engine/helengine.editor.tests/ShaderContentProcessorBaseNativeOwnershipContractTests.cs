namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that shader content processors transfer each newly deserialized payload through both processor contracts.
    /// </summary>
    public class ShaderContentProcessorBaseNativeOwnershipContractTests {
        /// <summary>
        /// Ensures typed shader payload reads transfer ownership to the content-loading caller.
        /// </summary>
        [Fact]
        public void Read_ReturnValue_TransfersOwnershipToCaller() {
            System.Reflection.MethodInfo method = typeof(ShaderContentProcessorBase<>).GetMethod(
                nameof(ShaderContentProcessorBase<object>.Read));

            AssertOwnedReturn(method);
        }

        /// <summary>
        /// Ensures the explicit non-generic processor bridge preserves the owned return contract.
        /// </summary>
        [Fact]
        public void ReadObject_ReturnValue_TransfersOwnershipToCaller() {
            System.Reflection.MethodInfo method = typeof(ShaderContentProcessorBase<>).GetMethods(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Single(candidate => candidate.Name.EndsWith(".ReadObject", StringComparison.Ordinal));

            AssertOwnedReturn(method);
        }

        /// <summary>
        /// Verifies that one reflected shader processor method carries the native owned-return contract.
        /// </summary>
        /// <param name="method">Shader processor method whose returned payload becomes the caller's responsibility.</param>
        static void AssertOwnedReturn(System.Reflection.MethodInfo method) {
            Assert.NotNull(method);
            Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeOwnedReturnAttribute), false));
        }
    }
}
