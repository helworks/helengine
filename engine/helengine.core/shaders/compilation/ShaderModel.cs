namespace helengine {
    /// <summary>
    /// Represents a shader model version used for HLSL compilation targets.
    /// </summary>
    public class ShaderModel {
        /// <summary>
        /// Initializes a new shader model version.
        /// </summary>
        /// <param name="major">Major shader model version.</param>
        /// <param name="minor">Minor shader model version.</param>
        public ShaderModel(int major, int minor) {
            if (major < 0) {
                throw new ArgumentOutOfRangeException(nameof(major), "Major version cannot be negative.");
            }

            if (minor < 0) {
                throw new ArgumentOutOfRangeException(nameof(minor), "Minor version cannot be negative.");
            }

            Major = major;
            Minor = minor;
        }

        /// <summary>
        /// Gets the major shader model version.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Gets the minor shader model version.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Builds a standard HLSL profile string for the requested stage.
        /// </summary>
        /// <param name="stage">Shader stage to describe.</param>
        /// <returns>Profile string such as vs_5_0.</returns>
        public string GetProfile(ShaderStage stage) {
            string prefix = GetStagePrefix(stage);
            return string.Concat(prefix, "_", Major.ToString(), "_", Minor.ToString());
        }

        /// <summary>
        /// Returns the shader model as a major.minor string.
        /// </summary>
        /// <returns>Shader model string.</returns>
        public override string ToString() {
            return string.Concat(Major.ToString(), ".", Minor.ToString());
        }

        /// <summary>
        /// Maps a shader stage to its HLSL profile prefix.
        /// </summary>
        /// <param name="stage">Shader stage to map.</param>
        /// <returns>HLSL profile prefix.</returns>
        string GetStagePrefix(ShaderStage stage) {
            switch (stage) {
                case ShaderStage.Vertex:
                    return "vs";
                case ShaderStage.Pixel:
                    return "ps";
                case ShaderStage.Geometry:
                    return "gs";
                case ShaderStage.Hull:
                    return "hs";
                case ShaderStage.Domain:
                    return "ds";
                case ShaderStage.Compute:
                    return "cs";
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported shader stage.");
            }
        }
    }
}
