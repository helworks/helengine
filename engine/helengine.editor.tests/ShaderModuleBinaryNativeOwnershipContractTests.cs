namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that shader binary lookup keeps ownership in the module definition that stores each descriptor.
    /// </summary>
    public class ShaderModuleBinaryNativeOwnershipContractTests {
        /// <summary>
        /// Ensures module-definition lookup borrows the binary descriptor retained in the module's binary array.
        /// </summary>
        [Fact]
        public void DefinitionGetBinary_ReturnValue_IsBorrowedFromModuleDefinition() {
            System.Reflection.MethodInfo method = typeof(ShaderModuleDefinition).GetMethod(
                nameof(ShaderModuleDefinition.GetBinary));

            AssertBorrowedReturn(method);
        }

        /// <summary>
        /// Ensures package lookup preserves the borrowed lifetime of the module definition's binary descriptor.
        /// </summary>
        [Fact]
        public void PackageGetBinary_ReturnValue_IsBorrowedFromModuleDefinition() {
            System.Reflection.MethodInfo method = typeof(ShaderModulePackage).GetMethod(
                nameof(ShaderModulePackage.GetBinary));

            AssertBorrowedReturn(method);
        }

        /// <summary>
        /// Verifies that one reflected method carries the native borrowed-return contract.
        /// </summary>
        /// <param name="method">Binary lookup method whose returned descriptor remains owned by its module definition.</param>
        static void AssertBorrowedReturn(System.Reflection.MethodInfo method) {
            Assert.NotNull(method);
            Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeBorrowedReturnAttribute), false));
        }
    }
}
