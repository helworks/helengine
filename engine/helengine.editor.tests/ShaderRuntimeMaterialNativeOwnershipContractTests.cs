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

        /// <summary>Both cloning setters borrow caller-owned payloads without retaining them, preserving native cleanup authority at each call site.</summary>
        [Fact]
        public void SetConstantBufferData_PayloadParameterDoesNotEscapeEitherOverload() {
            foreach (Type bindingType in new[] { typeof(string), typeof(int) }) {
                System.Reflection.MethodInfo method = typeof(MaterialPropertyBlock).GetMethod(
                    nameof(MaterialPropertyBlock.SetConstantBufferData), new[] { bindingType, typeof(byte[]) });
                Assert.NotNull(method);
                Assert.NotEmpty(method.GetParameters()[1].GetCustomAttributes(typeof(NativeNoEscapeAttribute), false));
            }
        }

        /// <summary>Both payload replacement paths delete the owned array element directly, avoiding unsupported ref elements and borrowed-local cleanup.</summary>
        [Fact]
        public void SetConstantBufferData_DeletesOwnedSlotBeforePublishingReplacementOrClear() {
            string sourcePath = Path.Combine(TestSourceRepositoryLocator.ResolveHelEngineRootPath(), "engine", "helengine.shader", "assets", "material", "MaterialPropertyBlock.cs");
            string source = File.ReadAllText(sourcePath);
            int setterStart = source.IndexOf("public void SetConstantBufferData(int bindingIndex, [NativeNoEscape] byte[] data)", StringComparison.Ordinal);
            int getterStart = source.IndexOf("public byte[] GetConstantBufferData", setterStart, StringComparison.Ordinal);
            string setter = source.Substring(setterStart, getterStart - setterStart);
            int clearStart = setter.IndexOf("if (data == null)", StringComparison.Ordinal);
            int clearEnd = setter.IndexOf("return;", clearStart, StringComparison.Ordinal);
            string clearPath = setter.Substring(clearStart, clearEnd - clearStart);
            int deleteClearedPayload = clearPath.IndexOf("NativeOwnership.Delete(ConstantBufferValues[bindingIndex]);", StringComparison.Ordinal);
            int clearSlot = clearPath.IndexOf("ConstantBufferValues[bindingIndex] = null;", StringComparison.Ordinal);
            Assert.True(deleteClearedPayload >= 0 && clearSlot > deleteClearedPayload, "Clearing must delete the slot's owned payload directly before removing its stored reference.");
            int copiedPayload = setter.IndexOf("Array.Copy(data, copiedData, data.Length);", StringComparison.Ordinal);
            int deletePrevious = setter.IndexOf("NativeOwnership.Delete(ConstantBufferValues[bindingIndex]);", copiedPayload, StringComparison.Ordinal);
            int publishReplacement = setter.IndexOf("ConstantBufferValues[bindingIndex] = copiedData;", copiedPayload, StringComparison.Ordinal);
            Assert.True(deletePrevious > copiedPayload && publishReplacement > deletePrevious, "Native replacement must validate and copy the new bytes before deleting the previous owned array element and publishing its replacement.");
            Assert.DoesNotContain("NativeOwnership.Release(", setter, StringComparison.Ordinal);
            Assert.DoesNotContain("byte[] previousData =", setter, StringComparison.Ordinal);
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
