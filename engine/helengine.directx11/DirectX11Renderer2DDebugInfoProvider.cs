using System.Collections.Generic;

namespace helengine.directx11 {
    /// <summary>
    /// Supplies debug information for the DirectX11 2D renderer.
    /// </summary>
    internal class DirectX11Renderer2DDebugInfoProvider : IDebugInfoProvider {
        readonly DirectX11Renderer2D renderer;

        /// <summary>
        /// Initializes the debug provider for a 2D renderer.
        /// </summary>
        /// <param name="renderer">Renderer to query for debug info.</param>
        public DirectX11Renderer2DDebugInfoProvider(DirectX11Renderer2D renderer) {
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
