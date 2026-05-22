using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shared material-layout construction from shader metadata.
    /// </summary>
    public class MaterialLayoutBuilderTests {
        /// <summary>
        /// Ensures shader bindings are categorized correctly and engine-managed transform data is excluded.
        /// </summary>
        [Fact]
        public void Build_CollectsBindingsAndSkipsTransformBuffer() {
            ShaderMaterialAsset materialAsset = CreateMaterialAsset();
            ShaderAsset shaderAsset = CreateShaderAssetWithBindings(
                CreateBinding("TransformBuffer", ShaderResourceType.ConstantBuffer, 0, 0, 64),
                CreateBinding("MaterialParams", ShaderResourceType.ConstantBuffer, 0, 1, 16),
                CreateBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 0, 0),
                CreateBinding("DiffuseSampler", ShaderResourceType.Sampler, 0, 1, 0));

            MaterialLayout layout = MaterialLayoutBuilder.Build(materialAsset, shaderAsset);

            Assert.Equal(materialAsset.ShaderAssetId, layout.ShaderAssetId);
            Assert.Equal(materialAsset.VertexProgram, layout.VertexProgram);
            Assert.Equal(materialAsset.PixelProgram, layout.PixelProgram);
            Assert.Equal(materialAsset.Variant, layout.Variant);
            Assert.Equal(MaterialBlendMode.AlphaBlend, layout.RenderState.BlendMode);
            Assert.Single(layout.TextureBindings);
            Assert.Single(layout.ConstantBufferBindings);
            Assert.Single(layout.SamplerBindings);
            Assert.Equal("DiffuseTexture", layout.TextureBindings[0].Name);
            Assert.Equal("MaterialParams", layout.ConstantBufferBindings[0].Name);
            Assert.Equal("DiffuseSampler", layout.SamplerBindings[0].Name);
        }

        /// <summary>
        /// Ensures duplicate bindings shared across shader stages are merged into one layout entry.
        /// </summary>
        [Fact]
        public void Build_MergesDuplicateBindingsAcrossStages() {
            ShaderMaterialAsset materialAsset = CreateMaterialAsset();
            ShaderBindingAsset sharedTexture = CreateBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 0, 0);
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = materialAsset.ShaderAssetId,
                Programs = new[] {
                    CreateProgram("VS", ShaderStage.Vertex, sharedTexture),
                    CreateProgram("PS", ShaderStage.Pixel, sharedTexture)
                }
            };
            materialAsset.VertexProgram = "VS";
            materialAsset.PixelProgram = "PS";

            MaterialLayout layout = MaterialLayoutBuilder.Build(materialAsset, shaderAsset);

            Assert.Single(layout.TextureBindings);
            Assert.Equal("DiffuseTexture", layout.TextureBindings[0].Name);
        }

        /// <summary>
        /// Ensures conflicting duplicate bindings across shader stages fail fast.
        /// </summary>
        [Fact]
        public void Build_WithConflictingDuplicateBindings_Throws() {
            ShaderMaterialAsset materialAsset = CreateMaterialAsset();
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = materialAsset.ShaderAssetId,
                Programs = new[] {
                    CreateProgram("VS", ShaderStage.Vertex, CreateBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 0, 0)),
                    CreateProgram("PS", ShaderStage.Pixel, CreateBinding("DiffuseTexture", ShaderResourceType.Texture2D, 0, 1, 0))
                }
            };
            materialAsset.VertexProgram = "VS";
            materialAsset.PixelProgram = "PS";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => MaterialLayoutBuilder.Build(materialAsset, shaderAsset));

            Assert.Contains("conflicting slots", exception.Message);
        }

        /// <summary>
        /// Creates a representative material asset for layout-builder testing.
        /// </summary>
        /// <returns>Material asset with non-default render state.</returns>
        static ShaderMaterialAsset CreateMaterialAsset() {
            return new ShaderMaterialAsset {
                ShaderAssetId = "shader/material",
                VertexProgram = "VS",
                PixelProgram = "PS",
                Variant = "default",
                RenderState = new MaterialRenderState {
                    BlendMode = MaterialBlendMode.AlphaBlend,
                    CullMode = MaterialCullMode.None,
                    DepthTestEnabled = true,
                    DepthWriteEnabled = false
                }
            };
        }

        /// <summary>
        /// Creates a shader asset whose vertex and pixel programs share the supplied bindings.
        /// </summary>
        /// <param name="bindings">Bindings to expose from both shader stages.</param>
        /// <returns>Shader asset ready for layout construction.</returns>
        static ShaderAsset CreateShaderAssetWithBindings(params ShaderBindingAsset[] bindings) {
            return new ShaderAsset {
                Id = "shader/material",
                Programs = new[] {
                    CreateProgram("VS", ShaderStage.Vertex, bindings),
                    CreateProgram("PS", ShaderStage.Pixel, bindings)
                }
            };
        }

        /// <summary>
        /// Creates one shader program asset with the supplied bindings.
        /// </summary>
        /// <param name="name">Program name.</param>
        /// <param name="stage">Shader stage.</param>
        /// <param name="bindings">Bindings exposed by the program.</param>
        /// <returns>Configured shader program asset.</returns>
        static ShaderProgramAsset CreateProgram(string name, ShaderStage stage, params ShaderBindingAsset[] bindings) {
            return new ShaderProgramAsset {
                Name = name,
                Stage = stage,
                EntryPoint = name,
                Bindings = bindings,
                Inputs = Array.Empty<ShaderVertexElementAsset>(),
                Outputs = Array.Empty<ShaderVertexElementAsset>(),
                Variants = Array.Empty<ShaderVariantAsset>()
            };
        }

        /// <summary>
        /// Creates one shader binding asset for layout-builder testing.
        /// </summary>
        /// <param name="name">Binding name.</param>
        /// <param name="resourceType">Binding resource type.</param>
        /// <param name="set">Binding set.</param>
        /// <param name="slot">Binding slot.</param>
        /// <param name="size">Constant-buffer size in bytes.</param>
        /// <returns>Configured shader binding asset.</returns>
        static ShaderBindingAsset CreateBinding(string name, ShaderResourceType resourceType, int set, int slot, int size) {
            return new ShaderBindingAsset {
                Name = name,
                Type = resourceType,
                Set = set,
                Slot = slot,
                Size = size,
                Members = Array.Empty<ShaderConstantMemberAsset>()
            };
        }
    }
}
