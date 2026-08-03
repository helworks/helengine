namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that shader material texture lookups borrow textures retained by material property blocks.
    /// </summary>
    public class ShaderRuntimeMaterialNativeOwnershipContractTests {
        /// <summary>
        /// Ensures resolving a material texture does not transfer ownership away from the material property block that stores it.
        /// </summary>
        [Fact]
        public void ResolveTexture_ReturnValue_IsBorrowedFromMaterialProperties() {
            System.Reflection.MethodInfo method = typeof(ShaderRuntimeMaterial).GetMethod(
                nameof(ShaderRuntimeMaterial.ResolveTexture));

            AssertBorrowedReturn(method);
        }

        /// <summary>
        /// Ensures indexed property-block texture lookup does not transfer ownership of the stored texture to its caller.
        /// </summary>
        [Fact]
        public void GetTexture_ReturnValue_IsBorrowedFromPropertyBlock() {
            System.Reflection.MethodInfo method = typeof(MaterialPropertyBlock).GetMethod(
                nameof(MaterialPropertyBlock.GetTexture));

            AssertBorrowedReturn(method);
        }

        /// <summary>
        /// Ensures a shader runtime material owns its property block while exposing only a borrowed view to callers.
        /// </summary>
        [Fact]
        public void Properties_AreOwnedByShaderRuntimeMaterial() {
            System.Reflection.FieldInfo field = typeof(ShaderRuntimeMaterial).GetField(
                "PropertiesValue",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.PropertyInfo property = typeof(ShaderRuntimeMaterial).GetProperty(
                nameof(ShaderRuntimeMaterial.Properties));

            Assert.NotNull(field);
            Assert.NotEmpty(field.GetCustomAttributes(typeof(NativeOwnedMemberAttribute), false));
            Assert.NotNull(property);
            Assert.NotEmpty(property.GetCustomAttributes(typeof(NativeBorrowedReturnAttribute), false));
        }

        /// <summary>
        /// Ensures category insertion transfers a newly allocated binding into the layout-building collections.
        /// </summary>
        [Fact]
        public void AddBindingToCategory_BindingParameter_TakesNativeOwnership() {
            System.Reflection.MethodInfo method = typeof(MaterialLayoutBuilder).GetMethod(
                "AddBindingToCategory",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(method);
            Assert.NotEmpty(method.GetParameters()[0].GetCustomAttributes(typeof(NativeTakesOwnershipAttribute), false));
        }

        /// <summary>
        /// Verifies that one reflected method carries the native borrowed-return contract.
        /// </summary>
        /// <param name="method">Method whose returned texture remains retained by its material property block.</param>
        static void AssertBorrowedReturn(System.Reflection.MethodInfo method) {
            Assert.NotNull(method);
            Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeBorrowedReturnAttribute), false));
        }
    }
}
