using System.Collections.Generic;

namespace helengine.directx11 {
    /// <summary>
    /// Supplies debug information for the DirectX11 3D renderer.
    /// </summary>
    internal class DirectX11Renderer3DDebugInfoProvider : IDebugInfoProvider {
        readonly DirectX11Renderer3D renderer;

        /// <summary>
        /// Initializes the debug provider for a 3D renderer.
        /// </summary>
        /// <param name="renderer">Renderer to query for debug info.</param>
        public DirectX11Renderer3DDebugInfoProvider(DirectX11Renderer3D renderer) {
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
            RendererBackendCapabilityProfile capabilityProfile = renderer.GetCapabilityProfile();
            items.Add(("FPS", renderer.LastFps.ToString("0.0")));
            items.Add(("Draw Calls", renderer.LastDrawCalls.ToString()));
            items.Add(("Frame (ms)", renderer.LastFrameTimeMs.ToString("0.00")));
            items.Add(("Forward", capabilityProfile.SupportsForwardRendering ? "yes" : "no"));
            items.Add(("Light Budget", capabilityProfile.MaximumVisibleLights.ToString()));
            items.Add(("Selected Lights", renderer.LastSelectedLightCount.ToString()));
            items.Add(("Shadow Budget", capabilityProfile.MaximumShadowedLights.ToString()));
            items.Add(("Selected Shadow Lights", renderer.LastSelectedShadowLightCount.ToString()));
        }
    }
}
