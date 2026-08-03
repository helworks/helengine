namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that content processors transfer newly materialized payloads to their callers in native builds.
    /// </summary>
    public class ContentProcessorNativeOwnershipContractTests {
        /// <summary>
        /// Ensures the non-generic processor contract transfers ownership of its boxed content result.
        /// </summary>
        [Fact]
        public void ReadObject_ReturnValue_TransfersOwnershipToCaller() {
            System.Reflection.MethodInfo method = typeof(IContentProcessor).GetMethod(
                nameof(IContentProcessor.ReadObject));

            AssertOwnedReturn(method);
        }

        /// <summary>
        /// Ensures the typed processor contract transfers ownership of its newly materialized content result.
        /// </summary>
        [Fact]
        public void Read_ReturnValue_TransfersOwnershipToCaller() {
            System.Reflection.MethodInfo method = typeof(IContentProcessor<>).GetMethod(
                nameof(IContentProcessor<object>.Read));

            AssertOwnedReturn(method);
        }

        /// <summary>
        /// Verifies that one reflected method carries the native owned-return contract.
        /// </summary>
        /// <param name="method">Processor method whose returned payload becomes the caller's responsibility.</param>
        static void AssertOwnedReturn(System.Reflection.MethodInfo method) {
            Assert.NotNull(method);
            Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeOwnedReturnAttribute), false));
        }
    }
}
