namespace helengine {
    /// <summary>
    /// Identifies the backend API target stored in runtime shader packages and selected during compilation.
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
        WiiU,

        /// <summary>
        /// PlayStation 3 RSX vertex and fragment programs compiled by the Docker SDK.
        /// </summary>
        Ps3,

        /// <summary>
        /// Xbox 360 shader target.
        /// </summary>
        Xbox360 = 8,

        /// <summary>
        /// Original Xbox NV2A vertex microcode and pixel register-combiner artifact target.
        /// </summary>
        Xbox = 9
    }
}
