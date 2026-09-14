using System.Reflection;
using helengine.directx11;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Supplies the constructor-owned collaborators to DirectX11 renderer doubles that are created without running the constructor, and reads back collaborators the tests need to assert on.
    /// Renderer tests skip the constructor because it creates a real Direct3D device; the planning collaborators it builds are pure CPU services, so the tests supply those directly instead.
    /// </summary>
    internal static class DirectX11RendererTestAccess {
        /// <summary>
        /// Populates every device-independent planning collaborator the renderer constructor would have created.
        /// </summary>
        /// <param name="renderer">Renderer instance created without running its constructor.</param>
        public static void PopulatePlanningCollaborators(DirectX11Renderer3D renderer) {
            if (renderer == null) {
                throw new ArgumentNullException(nameof(renderer));
            }

            SetField(renderer, "FrameExtractionService", new RenderFrameExtractionService());
            SetField(renderer, "RenderPlanBuilder", new DirectX11RenderPlanBuilder());
            SetField(renderer, "RenderPlanExecutor", new DirectX11RenderPlanExecutor(true, false));
            SetField(renderer, "RenderQueueSnapshotVisitor", new DirectX11RenderQueueSnapshotVisitor());
            SetField(renderer, "ForwardLightShaderDataBuilder", new DirectX11ForwardLightShaderDataBuilder());
            SetField(renderer, "LightSelectionService", new DirectX11LightSelectionService());
            SetField(renderer, "ShadowResourcePlanner", new DirectX11ShadowResourcePlanner());
            SetField(renderer, "ShadowShaderDataBuilder", new DirectX11ShadowShaderDataBuilder());
        }

        /// <summary>
        /// Reads the pipeline state cache the renderer built for its own device.
        /// </summary>
        /// <param name="renderer">Renderer that owns the cache.</param>
        /// <returns>Pipeline state cache held by the renderer.</returns>
        public static DirectX11PipelineStateCache GetPipelineStateCache(DirectX11Renderer3D renderer) {
            if (renderer == null) {
                throw new ArgumentNullException(nameof(renderer));
            }

            return (DirectX11PipelineStateCache)ResolveField("PipelineStateCache").GetValue(renderer);
        }

        /// <summary>
        /// Assigns one private renderer field.
        /// </summary>
        /// <param name="renderer">Renderer whose field should be written.</param>
        /// <param name="fieldName">Private field name to write.</param>
        /// <param name="value">Value to assign.</param>
        static void SetField(DirectX11Renderer3D renderer, string fieldName, object value) {
            ResolveField(fieldName).SetValue(renderer, value);
        }

        /// <summary>
        /// Resolves one private instance field declared by the DirectX11 renderer.
        /// </summary>
        /// <param name="fieldName">Private field name to resolve.</param>
        /// <returns>Resolved reflection field metadata.</returns>
        static FieldInfo ResolveField(string fieldName) {
            FieldInfo field = typeof(DirectX11Renderer3D).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException($"DirectX11Renderer3D no longer declares the field '{fieldName}'.");
            }

            return field;
        }
    }
}
