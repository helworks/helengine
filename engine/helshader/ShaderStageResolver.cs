using helengine;

namespace helshader {
    /// <summary>
    /// Maps stage strings to shader stage enums.
    /// </summary>
    public class ShaderStageResolver {
        /// <summary>
        /// Parses a stage string to a shader stage enum.
        /// </summary>
        /// <param name="stage">Stage name string.</param>
        /// <returns>Parsed shader stage.</returns>
        public ShaderStage Parse(string stage) {
            if (string.IsNullOrWhiteSpace(stage)) {
                throw new ArgumentException("Stage name must be provided.", nameof(stage));
            }

            if (string.Equals(stage, "vertex", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "vs", StringComparison.OrdinalIgnoreCase)) {
                return ShaderStage.Vertex;
            }

            if (string.Equals(stage, "pixel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "fragment", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "ps", StringComparison.OrdinalIgnoreCase)) {
                return ShaderStage.Pixel;
            }

            if (string.Equals(stage, "geometry", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "gs", StringComparison.OrdinalIgnoreCase)) {
                return ShaderStage.Geometry;
            }

            if (string.Equals(stage, "hull", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "hs", StringComparison.OrdinalIgnoreCase)) {
                return ShaderStage.Hull;
            }

            if (string.Equals(stage, "domain", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "ds", StringComparison.OrdinalIgnoreCase)) {
                return ShaderStage.Domain;
            }

            if (string.Equals(stage, "compute", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "cs", StringComparison.OrdinalIgnoreCase)) {
                return ShaderStage.Compute;
            }

            throw new InvalidOperationException($"Unsupported shader stage '{stage}'.");
        }

        /// <summary>
        /// Gets a short stage suffix for file naming.
        /// </summary>
        /// <param name="stage">Shader stage enum.</param>
        /// <returns>Stage suffix string.</returns>
        public string GetStageSuffix(ShaderStage stage) {
            if (stage == ShaderStage.Vertex) {
                return "VS";
            }

            if (stage == ShaderStage.Pixel) {
                return "PS";
            }

            if (stage == ShaderStage.Geometry) {
                return "GS";
            }

            if (stage == ShaderStage.Hull) {
                return "HS";
            }

            if (stage == ShaderStage.Domain) {
                return "DS";
            }

            if (stage == ShaderStage.Compute) {
                return "CS";
            }

            throw new InvalidOperationException($"Unsupported shader stage '{stage}'.");
        }
    }
}
