using helengine.directx11;
using helengine.vulkan;

namespace helengine.editor.tests {
    /// <summary>
    /// Creates the explicit desktop shader backend registrations required by editor tests that compile shared HLSL sources.
    /// </summary>
    public static class DesktopShaderBackendRegistryFactory {
        /// <summary>
        /// Creates one registry populated with the DirectX 11 and Vulkan shader backends used by desktop editor tests.
        /// </summary>
        /// <returns>Registry containing the desktop shader backends used by editor tests.</returns>
        public static ShaderBackendRegistry Create() {
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            return shaderBackendRegistry;
        }

        /// <summary>
        /// Configures the shared built-in shader library with the desktop shader backends required by editor tests.
        /// </summary>
        public static void ConfigureEditorBuiltInShaderBackends() {
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(Create());
        }
    }
}
