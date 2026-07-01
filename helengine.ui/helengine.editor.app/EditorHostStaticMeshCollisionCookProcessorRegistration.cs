using helengine.editor;

namespace helengine.editor.app {
    /// <summary>
    /// Registers editor-host static-mesh collision cook processors exposed by runtime plugins.
    /// </summary>
    internal static class EditorHostStaticMeshCollisionCookProcessorRegistration {
        /// <summary>
        /// Registers the default static-mesh collision cook processors used by the editor host.
        /// </summary>
        public static void RegisterDefaults() {
            if (StaticMeshCollisionCookProcessorRegistry.Shared.Processors.Count > 0) {
                return;
            }

            StaticMeshCollisionCookProcessorRegistry.Shared.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
        }
    }
}
