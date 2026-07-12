using System.Runtime.InteropServices;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies managed DirectX11 shader-buffer structs preserve the byte layout expected by the built-in HLSL shaders.
    /// </summary>
    public sealed class DirectX11ShaderConstantBufferLayoutTests {
        /// <summary>
        /// Ensures one packed forward-light slot still occupies four float4 values.
        /// </summary>
        [Fact]
        public void DirectX11ForwardLightSlotShaderData_SizeMatchesHlslLayout() {
            Assert.Equal(64, Marshal.SizeOf<DirectX11ForwardLightSlotShaderData>());
        }

        /// <summary>
        /// Ensures the packed forward-light buffer still matches the built-in HLSL cbuffer size.
        /// </summary>
        [Fact]
        public void DirectX11ForwardLightShaderData_SizeMatchesHlslLayout() {
            Assert.Equal(288, Marshal.SizeOf<DirectX11ForwardLightShaderData>());
        }

        /// <summary>
        /// Ensures one packed atlas-shadow slot still occupies two float4 values plus one float4x4 transform.
        /// </summary>
        [Fact]
        public void DirectX11ShadowLightSlotShaderData_SizeMatchesHlslLayout() {
            Assert.Equal(96, Marshal.SizeOf<DirectX11ShadowLightSlotShaderData>());
        }

        /// <summary>
        /// Ensures the packed atlas-shadow buffer still matches the built-in HLSL cbuffer size.
        /// </summary>
        [Fact]
        public void DirectX11ShadowShaderData_SizeMatchesHlslLayout() {
            Assert.Equal(400, Marshal.SizeOf<DirectX11ShadowShaderData>());
        }

        /// <summary>
        /// Ensures the point-shadow depth buffer still matches the built-in HLSL cbuffer size.
        /// </summary>
        [Fact]
        public void DirectX11PointShadowDepthShaderData_SizeMatchesHlslLayout() {
            Assert.Equal(144, Marshal.SizeOf<DirectX11PointShadowDepthShaderData>());
        }
    }
}
