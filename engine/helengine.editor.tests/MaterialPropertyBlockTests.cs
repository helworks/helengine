using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies runtime material-property storage and validation behavior.
    /// </summary>
    public class MaterialPropertyBlockTests {
        /// <summary>
        /// Ensures textures can be resolved from the first assigned binding.
        /// </summary>
        [Fact]
        public void TryGetFirstTexture_ReturnsAssignedTexture() {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock(CreateLayout());
            var texture = new TestRuntimeTexture();

            propertyBlock.SetTexture("NormalTexture", texture);
            bool foundTexture = propertyBlock.TryGetFirstTexture(out RuntimeTexture resolvedTexture);

            Assert.True(foundTexture);
            Assert.Same(texture, resolvedTexture);
        }

        /// <summary>
        /// Ensures constant-buffer data is copied on assignment and retrieval.
        /// </summary>
        [Fact]
        public void SetConstantBufferData_CopiesPayloadOnSetAndGet() {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock(CreateLayout());
            byte[] payload = new byte[] { 1, 2, 3, 4 };

            propertyBlock.SetConstantBufferData("MaterialParams", payload);
            payload[0] = 9;
            byte[] firstRead = propertyBlock.GetConstantBufferData(0);
            firstRead[1] = 7;
            byte[] secondRead = propertyBlock.GetConstantBufferData(0);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, secondRead);
        }

        /// <summary>
        /// Ensures constant-buffer sizes must match the declared material layout.
        /// </summary>
        [Fact]
        public void SetConstantBufferData_WithWrongSize_Throws() {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock(CreateLayout());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                propertyBlock.SetConstantBufferData("MaterialParams", new byte[] { 1, 2, 3 }));

            Assert.Contains("expects 4 bytes", exception.Message);
        }

        /// <summary>Live edits replace and clear private payloads without changing caller-owned data or discarding a valid buffer after a rejected edit.</summary>
        [Fact]
        public void SetConstantBufferData_RepeatedEditsAndClearPreserveValidationAndCopyIsolation() {
            using MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock(CreateLayout());
            propertyBlock.SetConstantBufferData(0, new byte[] { 1, 2, 3, 4 });
            byte[] replacement = new byte[] { 5, 6, 7, 8 };
            propertyBlock.SetConstantBufferData(0, replacement);
            replacement[0] = 99;
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, propertyBlock.GetConstantBufferData(0));
            Assert.Throws<InvalidOperationException>(() => propertyBlock.SetConstantBufferData(0, new byte[] { 9 }));
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, propertyBlock.GetConstantBufferData(0));
            propertyBlock.SetConstantBufferData(0, null);
            Assert.Null(propertyBlock.GetConstantBufferData(0));
            propertyBlock.SetConstantBufferData(0, new byte[] { 8, 7, 6, 5 });
            Assert.Equal(new byte[] { 8, 7, 6, 5 }, propertyBlock.GetConstantBufferData(0));
        }

        /// <summary>
        /// Creates a representative material layout for property-block tests.
        /// </summary>
        /// <returns>Material layout with two textures and one constant buffer.</returns>
        static MaterialLayout CreateLayout() {
            return new MaterialLayout(
                "shader/material",
                "VS",
                "PS",
                "default",
                new MaterialRenderState(),
                new[] {
                    new MaterialLayoutBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 0, 0),
                    new MaterialLayoutBinding("NormalTexture", ShaderResourceType.Texture2D, 0, 1, 0)
                },
                new[] {
                    new MaterialLayoutBinding("MaterialParams", ShaderResourceType.ConstantBuffer, 0, 2, 4)
                },
                Array.Empty<MaterialLayoutBinding>());
        }
    }
}
