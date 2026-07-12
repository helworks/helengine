using helengine.directx11;
using System.Reflection;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies managed DirectX11 renderer types are explicitly marked when their behavior must be migrated to the Windows native DirectX renderer as well.
    /// </summary>
    public sealed class DirectX11NativeMigrationMarkerTests {
        /// <summary>
        /// Ensures each concrete DirectX11 renderer type carries one explicit native-migration marker for the Windows native DirectX renderer target.
        /// </summary>
        [Fact]
        public void DirectX11_renderer_types_are_marked_for_windows_native_migration() {
            Type[] rendererTypes = typeof(DirectX11Renderer3D).Assembly
                .GetTypes()
                .Where(IsConcreteDirectX11RendererType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Assert.NotEmpty(rendererTypes);

            foreach (Type rendererType in rendererTypes) {
                NativeMigrationRequiredAttribute attribute = rendererType.GetCustomAttribute<NativeMigrationRequiredAttribute>(false);

                Assert.NotNull(attribute);
                Assert.Equal("windows.native_directx_renderer", attribute.TargetId);
                Assert.Contains("Windows native DirectX renderer", attribute.Reason, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Returns whether one reflected type is a concrete DirectX11 renderer that should carry the native-migration marker.
        /// </summary>
        /// <param name="type">Reflected type to inspect.</param>
        /// <returns>True when the type is one concrete DirectX11 renderer; otherwise false.</returns>
        static bool IsConcreteDirectX11RendererType(Type type) {
            if (type == null) {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.IsAbstract || type.Namespace != "helengine.directx11") {
                return false;
            }

            return typeof(RenderManager2D).IsAssignableFrom(type) || typeof(RenderManager3D).IsAssignableFrom(type);
        }
    }
}
