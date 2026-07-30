using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies DirectX11 swap-chain resize behavior while a host window has no drawable client area.
    /// </summary>
    public sealed class DirectX11RendererMinimizeTests {
        /// <summary>
        /// Ensures a minimized zero-width window does not recreate invalid Direct3D resources.
        /// </summary>
        [Fact]
        public void OnWindowResized_WhenWidthIsZero_DoesNotRecreateSwapChainResources() {
            DirectX11Renderer3D renderer = (DirectX11Renderer3D)RuntimeHelpers.GetUninitializedObject(typeof(DirectX11Renderer3D));
            Dictionary<IntPtr, DirectX11SwapChainSurface> surfacesByHandle = new Dictionary<IntPtr, DirectX11SwapChainSurface> {
                [IntPtr.Zero] = new DirectX11SwapChainSurface()
            };
            SetPrivateField(renderer, "surfacesByHandle", surfacesByHandle);
            MethodInfo resizeMethod = typeof(DirectX11Renderer3D).GetMethod("OnWindowResized", BindingFlags.Instance | BindingFlags.NonPublic);
            if (resizeMethod == null) {
                throw new InvalidOperationException("Expected DirectX11 resize handler was not found.");
            }

            Exception exception = Record.Exception(() => resizeMethod.Invoke(renderer, new object[] { IntPtr.Zero, 0, 640 }));

            Assert.Null(exception);
        }

        /// <summary>
        /// Assigns one non-public instance field used to prepare an uninitialized renderer for isolated testing.
        /// </summary>
        /// <param name="target">Object whose field should be assigned.</param>
        /// <param name="fieldName">Name of the non-public field.</param>
        /// <param name="value">Value assigned to the field.</param>
        static void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected non-public field was not found.");
            }

            field.SetValue(target, value);
        }
    }
}
