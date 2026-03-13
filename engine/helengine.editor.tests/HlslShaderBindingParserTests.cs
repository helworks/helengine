using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies minimal HLSL binding inference used by the runtime shader file pipeline.
    /// </summary>
    public class HlslShaderBindingParserTests {
        /// <summary>
        /// Ensures constant buffers, textures, and samplers are inferred from HLSL register declarations.
        /// </summary>
        [Fact]
        public void ParseBindings_WithTexturedShader_ReturnsExpectedBindings() {
            ShaderBinding[] bindings = HlslShaderBindingParser.ParseBindings(CreateTexturedShaderSource(), ShaderBindingPolicies.Default);

            Assert.Equal(3, bindings.Length);
            Assert.Equal("TransformBuffer", bindings[0].Name);
            Assert.Equal(ShaderResourceType.ConstantBuffer, bindings[0].Type);
            Assert.Equal(0, bindings[0].Set);
            Assert.Equal(0, bindings[0].Slot);
            Assert.Equal("LabelTexture", bindings[1].Name);
            Assert.Equal(ShaderResourceType.Texture2D, bindings[1].Type);
            Assert.Equal(0, bindings[1].Set);
            Assert.Equal(100, bindings[1].Slot);
            Assert.Equal("LabelSampler", bindings[2].Name);
            Assert.Equal(ShaderResourceType.Sampler, bindings[2].Type);
            Assert.Equal(0, bindings[2].Set);
            Assert.Equal(200, bindings[2].Slot);
        }

        /// <summary>
        /// Ensures constant-buffer member offsets and total size follow HLSL register packing for the supported scalar, vector, and matrix subset.
        /// </summary>
        [Fact]
        public void ParseBindings_WithPackedConstantBuffer_ComputesOffsetsAndSize() {
            ShaderBinding[] bindings = HlslShaderBindingParser.ParseBindings(CreatePackedConstantBufferSource(), ShaderBindingPolicies.Default);

            Assert.Single(bindings);
            ShaderBinding binding = bindings[0];
            Assert.Equal("MaterialBuffer", binding.Name);
            Assert.Equal(96, binding.Size);
            Assert.Equal(4, binding.Members.Count);
            Assert.Equal(0, binding.Members[0].Offset);
            Assert.Equal(16, binding.Members[1].Offset);
            Assert.Equal(20, binding.Members[2].Offset);
            Assert.Equal(32, binding.Members[3].Offset);
            Assert.Equal(64, binding.Members[3].Size);
        }

        /// <summary>
        /// Ensures register spaces and arrays are preserved in inferred binding metadata.
        /// </summary>
        [Fact]
        public void ParseBindings_WithRegisterSpacesAndArrays_PreservesSpaceAndArraySize() {
            ShaderBinding[] bindings = HlslShaderBindingParser.ParseBindings(CreateArrayAndSpaceSource(), ShaderBindingPolicies.Default);

            Assert.Equal(2, bindings.Length);
            Assert.Equal("MaterialParams", bindings[0].Name);
            Assert.Equal(2, bindings[0].Set);
            Assert.Equal(32, bindings[0].Size);
            Assert.Single(bindings[0].Members);
            Assert.Equal(32, bindings[0].Members[0].Size);
            Assert.Equal("DiffuseTexture", bindings[1].Name);
            Assert.Equal(2, bindings[1].Set);
            Assert.Equal(103, bindings[1].Slot);
        }

        /// <summary>
        /// Creates representative textured shader source containing a transform buffer, one texture, and one sampler.
        /// </summary>
        /// <returns>Representative textured shader source.</returns>
        static string CreateTexturedShaderSource() {
            return
                "cbuffer TransformBuffer : register(b0)\n" +
                "{\n" +
                "    float4x4 worldViewProj;\n" +
                "};\n" +
                "\n" +
                "Texture2D LabelTexture : register(t0);\n" +
                "SamplerState LabelSampler : register(s0);\n";
        }

        /// <summary>
        /// Creates shader source whose constant buffer exercises mixed register packing.
        /// </summary>
        /// <returns>Representative constant-buffer shader source.</returns>
        static string CreatePackedConstantBufferSource() {
            return
                "cbuffer MaterialBuffer : register(b1)\n" +
                "{\n" +
                "    float4 Color;\n" +
                "    float Intensity;\n" +
                "    float3 Direction;\n" +
                "    matrix World;\n" +
                "};\n";
        }

        /// <summary>
        /// Creates shader source that exercises register spaces and HLSL array packing.
        /// </summary>
        /// <returns>Representative shader source containing one array constant buffer and one texture.</returns>
        static string CreateArrayAndSpaceSource() {
            return
                "cbuffer MaterialParams : register(b2, space2)\n" +
                "{\n" +
                "    float4 Colors[2];\n" +
                "};\n" +
                "\n" +
                "Texture2D DiffuseTexture : register(t3, space2);\n";
        }
    }
}
