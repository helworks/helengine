using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Captures one frame of scene and editor metrics displayed by the viewport stats overlay.
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
        /// Total registered entities in the object manager.
        /// </summary>
        public int EntityCount;
        /// <summary>
        /// 3D drawables visible to the stats viewport camera.
        /// </summary>
        public int VisibleDrawables3D;
        /// <summary>
        /// Total registered 3D drawables.
        /// </summary>
        public int TotalDrawables3D;
        /// <summary>
        /// Total registered 2D drawables.
        /// </summary>
        public int TotalDrawables2D;
        /// <summary>
        /// Registered directional lights.
        /// </summary>
        public int DirectionalLightCount;
        /// <summary>
        /// Registered point lights.
        /// </summary>
        public int PointLightCount;
        /// <summary>
        /// Registered spot lights.
        /// </summary>
        public int SpotLightCount;
        /// <summary>
        /// Registered ambient lights.
        /// </summary>
        public int AmbientLightCount;
        /// <summary>
        /// Registered update-loop participants.
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
                "\nEntities: ", snapshot.EntityCount.ToString(CultureInfo.InvariantCulture),
                "\nDraw 3D: ", snapshot.VisibleDrawables3D.ToString(CultureInfo.InvariantCulture),
                " / ", snapshot.TotalDrawables3D.ToString(CultureInfo.InvariantCulture),
                "\nDraw 2D: ", snapshot.TotalDrawables2D.ToString(CultureInfo.InvariantCulture),
                "\nLights: ", snapshot.DirectionalLightCount.ToString(CultureInfo.InvariantCulture),
                " dir  ", snapshot.PointLightCount.ToString(CultureInfo.InvariantCulture),
                " pt  ", snapshot.SpotLightCount.ToString(CultureInfo.InvariantCulture),
                " spot  ", snapshot.AmbientLightCount.ToString(CultureInfo.InvariantCulture),
                " amb",
                "\nUpdates: ", snapshot.UpdateableCount.ToString(CultureInfo.InvariantCulture));
        }
    }
}
