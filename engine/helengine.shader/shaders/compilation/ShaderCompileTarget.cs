namespace helengine {
    /// <summary>
    /// Identifies the backend API target for shader compilation.
    /// </summary>
    public enum ShaderCompileTarget {
        /// <summary>
        /// Direct3D 9 bytecode target.
        /// </summary>
        DirectX9,

        /// <summary>
        /// Direct3D 11 bytecode target.
        /// </summary>
        DirectX11,

        /// <summary>
        /// Direct3D 12 bytecode target.
        /// </summary>
        DirectX12,

        /// <summary>
        /// Vulkan SPIR-V target.
        /// </summary>
        Vulkan,

        /// <summary>
        /// Metal shader library target.
        /// </summary>
        Metal,

        /// <summary>
        /// PlayStation Vita shader artifact target compiled by the device-backed compiler.
        /// </summary>
        PsVita,

        /// <summary>
        /// Wii U GLSL source target compiled into GX2 binaries by the platform build.
        /// </summary>
        WiiU
    }
}
