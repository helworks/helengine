using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies parented runtime materials that inherit layout and render-state data from another material.
    /// </summary>
    public class RuntimeMaterialParentingTests {
        /// <summary>
        /// Ensures child materials stay synchronized when the parent material layout changes.
        /// </summary>
        [Fact]
        public void ParentMaterialLayoutChange_PreservesMatchingChildOverrideValues() {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            MaterialLayout firstLayout = CreateFirstLayout();
            MaterialLayout secondLayout = CreateSecondLayout();
            TestRuntimeMaterial childMaterial = new TestRuntimeMaterial();
            var texture = new TestRuntimeTexture();
            byte[] payload = new byte[] { 5, 4, 3, 2 };

            material.SetLayout(firstLayout);
            childMaterial.SetParentMaterial(material);
            childMaterial.Properties.SetTexture("DiffuseTexture", texture);
            childMaterial.Properties.SetConstantBufferData("MaterialParams", payload);
            material.SetLayout(secondLayout);

            Assert.Same(texture, childMaterial.Properties.GetTexture(0));
            Assert.Equal(payload, childMaterial.Properties.GetConstantBufferData(0));
            Assert.Null(childMaterial.Properties.GetTexture(1));
        }

        /// <summary>
        /// Ensures child materials inherit render-state changes from their parent.
        /// </summary>
        [Fact]
        public void ParentMaterialRenderStateChange_UpdatesChildRenderState() {
            TestRuntimeMaterial material = new TestRuntimeMaterial();
            TestRuntimeMaterial childMaterial = new TestRuntimeMaterial();
            var renderState = new MaterialRenderState {
                BlendMode = MaterialBlendMode.AlphaBlend,
                CullMode = MaterialCullMode.None,
                DepthTestEnabled = false,
                DepthWriteEnabled = false
            };

            childMaterial.SetParentMaterial(material);
            material.SetRenderState(renderState);

            Assert.Equal(MaterialBlendMode.AlphaBlend, childMaterial.RenderState.BlendMode);
            Assert.Equal(MaterialCullMode.None, childMaterial.RenderState.CullMode);
            Assert.False(childMaterial.RenderState.DepthTestEnabled);
            Assert.False(childMaterial.RenderState.DepthWriteEnabled);
        }

        /// <summary>
        /// Ensures child materials inherit unresolved constant-buffer values from their parent material.
        /// </summary>
        [Fact]
        public void TryResolveConstantBufferData_WhenChildHasNoLocalOverride_InheritsParentPayload() {
            TestRuntimeMaterial parentMaterial = new TestRuntimeMaterial();
            TestRuntimeMaterial childMaterial = new TestRuntimeMaterial();
            MaterialLayout layout = CreateFirstLayout();
            byte[] payload = new byte[] { 9, 8, 7, 6 };

            parentMaterial.SetLayout(layout);
            parentMaterial.Properties.SetConstantBufferData("MaterialParams", payload);
            childMaterial.SetParentMaterial(parentMaterial);

            bool resolved = childMaterial.TryResolveConstantBufferData("MaterialParams", out byte[] resolvedPayload);

            Assert.True(resolved);
            Assert.Equal(payload, resolvedPayload);
            Assert.NotSame(payload, resolvedPayload);
        }

        /// <summary>
        /// Creates the initial material layout used by parented material synchronization tests.
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
        /// Creates the replacement material layout used to verify child-material synchronization.
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
