using System.Collections.Generic;

namespace helengine.sharpdx {
    /// <summary>
    /// Supplies debug information for the SharpDX 3D renderer.
    /// </summary>
    internal class SharpDXRenderer3DDebugInfoProvider : IDebugInfoProvider {
        readonly SharpDXRenderer3D renderer;

        /// <summary>
        /// Initializes the debug provider for a 3D renderer.
        /// </summary>
        /// <param name="renderer">Renderer to query for debug info.</param>
        public SharpDXRenderer3DDebugInfoProvider(SharpDXRenderer3D renderer) {
            this.renderer = renderer;
        }

        /// <summary>
        /// Gets the debug category name.
        /// </summary>
        public string Category => "Renderer";

        /// <summary>
        /// Appends 3D renderer debug information to the list.
        /// </summary>
        /// <param name="items">List to append debug entries to.</param>
        public void AppendInfo(List<(string Key, string Value)> items) {
            items.Add(("FPS", renderer.LastFps.ToString("0.0")));
            items.Add(("Draw Calls", renderer.LastDrawCalls.ToString()));
            items.Add(("Frame (ms)", renderer.LastFrameTimeMs.ToString("0.00")));
        }
    }
}
