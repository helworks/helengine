using System.Reflection;
using SharpDX.Direct3D11;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies DirectX11 rasterizer front-face settings stay aligned with the engine-authored mesh winding convention.
    /// </summary>
    public sealed class DirectX11Renderer3DWindingTests {
        /// <summary>
        /// Ensures the default DirectX11 3D rasterizer treats counter-clockwise triangles as front-facing so built-in meshes match the Vulkan backend and authored model winding.
        /// </summary>
        [Fact]
        public void Constructor_WhenCreatingDefault3DRasterizer_UsesCounterClockwiseFrontFaces() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();

            RasterizerState rasterizerState = GetRequiredRasterizerState(renderer, "rasterizerState3D");
            RasterizerStateDescription description = rasterizerState.Description;

            Assert.True(description.IsFrontCounterClockwise);
            Assert.Equal(SharpDX.Direct3D11.CullMode.Back, description.CullMode);
        }

        /// <summary>
        /// Ensures material-specific DirectX11 rasterizer states preserve the same counter-clockwise front-face convention as the default 3D rasterizer.
        /// </summary>
        [Fact]
        public void ResolveRasterizerState_WhenCreatingMaterialSpecificState_PreservesCounterClockwiseFrontFaces() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            MaterialRenderState renderState = new MaterialRenderState {
                CullMode = MaterialCullMode.Front
            };

            MethodInfo resolveMethod = typeof(DirectX11Renderer3D).GetMethod("ResolveRasterizerState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(resolveMethod);

            RasterizerState rasterizerState = Assert.IsType<RasterizerState>(resolveMethod.Invoke(renderer, [renderState]));
            RasterizerStateDescription description = rasterizerState.Description;

            Assert.True(description.IsFrontCounterClockwise);
            Assert.Equal(SharpDX.Direct3D11.CullMode.Front, description.CullMode);
        }

        /// <summary>
        /// Retrieves one private rasterizer-state field and fails clearly when it cannot be resolved.
        /// </summary>
        /// <param name="renderer">Renderer instance that owns the state.</param>
        /// <param name="fieldName">Private field name to read.</param>
        /// <returns>Resolved rasterizer-state instance.</returns>
        static RasterizerState GetRequiredRasterizerState(DirectX11Renderer3D renderer, string fieldName) {
            FieldInfo field = typeof(DirectX11Renderer3D).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<RasterizerState>(field.GetValue(renderer));
        }
    }
}
