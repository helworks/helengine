using System.Collections.Generic;

namespace helengine.sharpdx {
    /// <summary>
    /// Supplies debug information for the SharpDX 2D renderer.
    /// </summary>
    internal class SharpDXRenderer2DDebugInfoProvider : IDebugInfoProvider {
        readonly SharpDXRenderer2D renderer;

        /// <summary>
        /// Initializes the debug provider for a 2D renderer.
        /// </summary>
        /// <param name="renderer">Renderer to query for debug info.</param>
        public SharpDXRenderer2DDebugInfoProvider(SharpDXRenderer2D renderer) {
            this.renderer = renderer;
        }

        /// <summary>
        /// Gets the debug category name.
        /// </summary>
        public string Category => "Renderer";

        /// <summary>
        /// Appends 2D renderer debug information to the list.
        /// </summary>
        /// <param name="items">List to append debug entries to.</param>
        public void AppendInfo(List<(string Key, string Value)> items) {
            items.Add(("UI Backend", renderer.CurrentRoundedRectBackend.ToString()));
        }
    }
}
