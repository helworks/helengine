namespace helengine.directx11.tests {
    /// <summary>Reports native DirectX tests as skipped on hosts without Windows D3D11.</summary>
    sealed class WindowsDirectXFactAttribute : FactAttribute {
        /// <summary>Preserves an explicit platform skip rather than treating an unexecuted GPU test as passed.</summary>
        public WindowsDirectXFactAttribute() {
            if (!OperatingSystem.IsWindows()) {
                Skip = "Requires Windows DirectX11 WARP; CPU cache tests run on every platform.";
            }
        }
    }
}
