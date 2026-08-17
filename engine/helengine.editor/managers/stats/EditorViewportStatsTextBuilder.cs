using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Captures one group of scene or editor metrics displayed by the viewport stats overlay.
    /// </summary>
    public struct EditorViewportStatsGroup {
        /// <summary>
        /// Registered entities in this group.
        /// </summary>
        public int EntityCount;
        /// <summary>
        /// 3D drawables in this group visible to the stats viewport camera.
        /// </summary>
        public int VisibleDrawables3D;
        /// <summary>
        /// Total registered 3D drawables in this group.
        /// </summary>
        public int TotalDrawables3D;
        /// <summary>
        /// Total registered 2D drawables in this group.
        /// </summary>
        public int TotalDrawables2D;
        /// <summary>
        /// Registered directional lights in this group.
        /// </summary>
        public int DirectionalLightCount;
        /// <summary>
        /// Registered point lights in this group.
        /// </summary>
        public int PointLightCount;
        /// <summary>
        /// Registered spot lights in this group.
        /// </summary>
        public int SpotLightCount;
        /// <summary>
        /// Registered ambient lights in this group.
        /// </summary>
        public int AmbientLightCount;
    }

    /// <summary>
    /// Captures one frame of metrics displayed by the viewport stats overlay, split into authored-scene and editor groups.
    /// </summary>
    public struct EditorViewportStatsSnapshot {
        /// <summary>
        /// Smoothed frames-per-second for the editor update loop.
        /// </summary>
        public double Fps;
        /// <summary>
        /// Smoothed frame duration in milliseconds for the editor update loop.
        /// </summary>
        public double FrameMilliseconds;
        /// <summary>
        /// Metrics for authored game-scene content.
        /// </summary>
        public EditorViewportStatsGroup Scene;
        /// <summary>
        /// Metrics for editor-internal content such as panels, gizmos, and previews.
        /// </summary>
        public EditorViewportStatsGroup Editor;
        /// <summary>
        /// Registered update-loop participants across the whole editor.
        /// </summary>
        public int UpdateableCount;
    }

    /// <summary>
    /// Formats one viewport stats snapshot into the multiline overlay text.
    /// </summary>
    public static class EditorViewportStatsTextBuilder {
        /// <summary>
        /// Builds the multiline stats text rendered by the viewport stats overlay.
        /// </summary>
        /// <param name="snapshot">Scene and editor metrics for the current frame.</param>
        /// <returns>Multiline stats text.</returns>
        public static string Build(EditorViewportStatsSnapshot snapshot) {
            return string.Concat(
                "FPS: ", snapshot.Fps.ToString("0.0", CultureInfo.InvariantCulture),
                " (", snapshot.FrameMilliseconds.ToString("0.0", CultureInfo.InvariantCulture), " ms)",
                "\n-- Scene --\n", BuildGroup(snapshot.Scene),
                "\n-- Editor --\n", BuildGroup(snapshot.Editor),
                "\nUpdates: ", snapshot.UpdateableCount.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Builds the metric lines for one scene or editor group.
        /// </summary>
        /// <param name="group">Group metrics to format.</param>
        /// <returns>Multiline group text without a trailing newline.</returns>
        static string BuildGroup(EditorViewportStatsGroup group) {
            return string.Concat(
                "Entities: ", group.EntityCount.ToString(CultureInfo.InvariantCulture),
                "\nDraw 3D: ", group.VisibleDrawables3D.ToString(CultureInfo.InvariantCulture),
                " / ", group.TotalDrawables3D.ToString(CultureInfo.InvariantCulture),
                "\nDraw 2D: ", group.TotalDrawables2D.ToString(CultureInfo.InvariantCulture),
                "\nLights: ", group.DirectionalLightCount.ToString(CultureInfo.InvariantCulture),
                " dir  ", group.PointLightCount.ToString(CultureInfo.InvariantCulture),
                " pt  ", group.SpotLightCount.ToString(CultureInfo.InvariantCulture),
                " spot  ", group.AmbientLightCount.ToString(CultureInfo.InvariantCulture),
                " amb");
        }
    }
}
