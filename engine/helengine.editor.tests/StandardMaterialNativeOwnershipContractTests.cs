namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that standard-material byte factories declare ownership of their newly allocated payloads.
    /// </summary>
    public class StandardMaterialNativeOwnershipContractTests {
        /// <summary>
        /// Ensures every standard scalar factory exposes its fresh byte-array return as native-owned.
        /// </summary>
        [Fact]
        public void StandardMaterialByteFactories_DeclareOwnedReturns() {
            AssertOwnedReturn(typeof(StandardMaterialRoughnessDefaults), nameof(StandardMaterialRoughnessDefaults.CreateConstantBufferData));
            AssertOwnedReturn(typeof(StandardMaterialRoughnessDefaults), nameof(StandardMaterialRoughnessDefaults.CreateDefaultConstantBufferData));
            AssertOwnedReturn(typeof(StandardMaterialMetallicDefaults), nameof(StandardMaterialMetallicDefaults.CreateConstantBufferData));
            AssertOwnedReturn(typeof(StandardMaterialMetallicDefaults), nameof(StandardMaterialMetallicDefaults.CreateDefaultConstantBufferData));
            AssertOwnedReturn(typeof(StandardMaterialSpecularDefaults), nameof(StandardMaterialSpecularDefaults.CreateConstantBufferData));
            AssertOwnedReturn(typeof(StandardMaterialSpecularDefaults), nameof(StandardMaterialSpecularDefaults.CreateDefaultConstantBufferData));
        }

        /// <summary>
        /// Verifies one named factory method carries the native-owned return contract.
        /// </summary>
        /// <param name="declaringType">Static defaults type that declares the byte factory.</param>
        /// <param name="methodName">Factory method name to inspect.</param>
        static void AssertOwnedReturn(Type declaringType, string methodName) {
            System.Reflection.MethodInfo[] methods = declaringType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            System.Reflection.MethodInfo method = Assert.Single(methods, candidate => string.Equals(candidate.Name, methodName, StringComparison.Ordinal));
            Assert.NotNull(method.GetCustomAttributes(typeof(NativeOwnedReturnAttribute), inherit: false).SingleOrDefault());
        }
    }
}
