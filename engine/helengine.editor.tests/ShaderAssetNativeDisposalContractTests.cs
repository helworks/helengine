namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that serialized shader assets release every native-owned container in their nested object graph.
    /// </summary>
    public class ShaderAssetNativeDisposalContractTests {
        /// <summary>
        /// Ensures disposing a root shader asset recursively disposes nested owners and clears every owned array reference.
        /// </summary>
        [Fact]
        public void ShaderAsset_Dispose_ReleasesNestedOwnedContainers() {
            ShaderVariantAsset variant = new ShaderVariantAsset {
                Defines = new[] { "USE_TEXTURE" }
            };
            ShaderBindingAsset binding = new ShaderBindingAsset {
                Members = new[] { new ShaderConstantMemberAsset() }
            };
            ShaderProgramAsset program = new ShaderProgramAsset {
                Bindings = new[] { binding },
                Inputs = new[] { new ShaderVertexElementAsset() },
                Outputs = new[] { new ShaderVertexElementAsset() },
                Variants = new[] { variant }
            };
            ShaderBinaryAsset binary = new ShaderBinaryAsset {
                Bytecode = new byte[] { 1, 2, 3, 4 }
            };
            ShaderAsset asset = new ShaderAsset {
                Programs = new[] { program },
                Binaries = new[] { binary }
            };

            IDisposable disposableAsset = Assert.IsAssignableFrom<IDisposable>(asset);
            disposableAsset.Dispose();

            Assert.Null(asset.Programs);
            Assert.Null(asset.Binaries);
            Assert.Null(program.Bindings);
            Assert.Null(program.Inputs);
            Assert.Null(program.Outputs);
            Assert.Null(program.Variants);
            Assert.Null(binding.Members);
            Assert.Null(variant.Defines);
            Assert.Null(binary.Bytecode);
        }

        /// <summary>
        /// Ensures shader materials reuse the generic material render state and recursively release authored constant-buffer storage.
        /// </summary>
        [Fact]
        public void ShaderMaterialAsset_Dispose_ReleasesInheritedStateAndConstantBuffers() {
            MaterialConstantBufferAsset constantBuffer = new MaterialConstantBufferAsset {
                Data = new byte[] { 1, 2, 3, 4 }
            };
            ShaderMaterialAsset material = new ShaderMaterialAsset {
                ConstantBuffers = new[] { constantBuffer }
            };

            material.Dispose();

            Assert.Null(material.RenderState);
            Assert.Null(material.ConstantBuffers);
            Assert.Null(constantBuffer.Data);
            Assert.Null(typeof(ShaderMaterialAsset).GetField(
                nameof(ShaderMaterialAsset.RenderState),
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly));
        }

        /// <summary>
        /// Ensures the constant-buffer factory transfers its byte payload into one explicitly owned record.
        /// </summary>
        [Fact]
        public void MaterialConstantBufferAsset_Create_TransfersPayloadIntoOwnedRecord() {
            byte[] data = new byte[] { 1, 2, 3, 4 };

            MaterialConstantBufferAsset constantBuffer = MaterialConstantBufferAsset.Create("BaseColorBuffer", data);

            Assert.Equal("BaseColorBuffer", constantBuffer.Name);
            Assert.Same(data, constantBuffer.Data);
            Assert.NotNull(typeof(MaterialConstantBufferAsset)
                .GetMethod(nameof(MaterialConstantBufferAsset.Create))
                .GetCustomAttributes(typeof(NativeOwnedReturnAttribute), false)
                .Single());
            Assert.NotNull(typeof(MaterialConstantBufferAsset)
                .GetMethod(nameof(MaterialConstantBufferAsset.Create))
                .GetParameters()[1]
                .GetCustomAttributes(typeof(NativeTakesOwnershipAttribute), false)
                .Single());

            constantBuffer.Dispose();

            Assert.Null(constantBuffer.Data);
        }
    }
}
