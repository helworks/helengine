using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies runtime-material behavior that sits above the backend-specific resource implementations.
    /// </summary>
    public class RuntimeMaterialTests {
        /// <summary>
        /// Ensures matching property values survive a layout rebuild.
        /// </summary>
        [Fact]
        public void SetLayout_PreservesMatchingTexturesAndConstantBuffers() {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            MaterialLayout firstLayout = CreateFirstLayout();
            MaterialLayout secondLayout = CreateSecondLayout();
            var texture = new TestRuntimeTexture();
            byte[] payload = new byte[] { 4, 3, 2, 1 };

            material.SetLayout(firstLayout);
            material.Properties.SetTexture("DiffuseTexture", texture);
            material.Properties.SetConstantBufferData("MaterialParams", payload);
            material.SetLayout(secondLayout);

            Assert.Same(texture, material.Properties.GetTexture(0));
            Assert.Equal(payload, material.Properties.GetConstantBufferData(0));
            Assert.Null(material.Properties.GetTexture(1));
        }

        /// <summary>
        /// Ensures child materials resolve their local texture overrides before inherited parent values.
        /// </summary>
        [Fact]
        public void ResolveTexture_PrefersChildOverridesOverParentValues() {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            MaterialLayout layout = CreateFirstLayout();
            RuntimeMaterial childMaterial = new RuntimeMaterial();
            var parentTexture = new TestRuntimeTexture();
            var childTexture = new TestRuntimeTexture();

            material.SetLayout(layout);
            material.Properties.SetTexture("DiffuseTexture", parentTexture);
            childMaterial.SetParentMaterial(material);
            childMaterial.Properties.SetTexture("DiffuseTexture", childTexture);

            RuntimeTexture resolvedTexture = childMaterial.ResolveTexture();

            Assert.Same(childTexture, resolvedTexture);
        }

        /// <summary>
        /// Ensures materials without any assigned texture value resolve no texture instead of throwing.
        /// </summary>
        [Fact]
        public void ResolveTexture_WithoutAssignedTexture_ReturnsNull() {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            MaterialLayout layout = CreateFirstLayout();

            material.SetLayout(layout);

            RuntimeTexture resolvedTexture = material.ResolveTexture();

            Assert.Null(resolvedTexture);
        }

        /// <summary>
        /// Ensures child materials resolve their root parent for renderer-side backend material lookup.
        /// </summary>
        [Fact]
        public void ResolveRootMaterial_ReturnsTopMostParentMaterial() {
            TestRuntimeMaterial rootMaterial = new TestRuntimeMaterial();
            RuntimeMaterial middleMaterial = new RuntimeMaterial();
            RuntimeMaterial childMaterial = new RuntimeMaterial();

            middleMaterial.SetParentMaterial(rootMaterial);
            childMaterial.SetParentMaterial(middleMaterial);

            RuntimeMaterial resolvedMaterial = childMaterial.ResolveRootMaterial();

            Assert.Same(rootMaterial, resolvedMaterial);
        }

        /// <summary>
        /// Ensures runtime materials expose the compact Windows-forward lighting feature flags with conservative defaults.
        /// </summary>
        [Fact]
        public void Constructor_InitializesCompactLightingFeatureFlags() {
            RuntimeMaterial material = new RuntimeMaterial();

            Assert.Equal(RuntimeMaterialLightingModel.Unlit, material.LightingModel);
            Assert.False(material.SupportsNormalMapping);
            Assert.False(material.SupportsEmissive);
            Assert.True(material.CastsShadows);
            Assert.True(material.ReceivesShadows);
        }

        /// <summary>
        /// Creates the initial material layout used by runtime-material tests.
        /// </summary>
        /// <returns>Material layout with one texture and one constant buffer.</returns>
        static MaterialLayout CreateFirstLayout() {
            return new MaterialLayout(
                "shader/material",
                "VS",
                "PS",
                "default",
                new MaterialRenderState(),
                new[] {
                    new MaterialLayoutBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 0, 0)
                },
                new[] {
                    new MaterialLayoutBinding("MaterialParams", ShaderResourceType.ConstantBuffer, 0, 1, 4)
                },
                Array.Empty<MaterialLayoutBinding>());
        }

        /// <summary>
        /// Creates the replacement material layout used to verify layout-transition preservation.
        /// </summary>
        /// <returns>Material layout that keeps one existing texture binding and adds a new one.</returns>
        static MaterialLayout CreateSecondLayout() {
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
                    new MaterialLayoutBinding("MaterialParams", ShaderResourceType.ConstantBuffer, 0, 1, 4)
                },
                Array.Empty<MaterialLayoutBinding>());
        }
    }
}
