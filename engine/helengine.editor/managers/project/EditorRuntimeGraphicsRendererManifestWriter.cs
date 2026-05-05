using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Writes generated C++ source fragments that embed platform renderer defaults into the native player.
    /// </summary>
    public sealed class EditorRuntimeGraphicsRendererManifestWriter {
        /// <summary>
        /// Writes the generated renderer-default manifest source files into the generated-core runtime folder.
        /// </summary>
        /// <param name="generatedCoreRootPath">Absolute path to the generated core output root.</param>
        /// <param name="manifest">Resolved renderer-default manifest to embed into native source.</param>
        public void Write(string generatedCoreRootPath, RuntimeGraphicsRendererManifest manifest) {
            if (string.IsNullOrWhiteSpace(generatedCoreRootPath)) {
                throw new ArgumentException("Generated core root path must be provided.", nameof(generatedCoreRootPath));
            }
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            string runtimeRootPath = Path.Combine(generatedCoreRootPath, "runtime");
            Directory.CreateDirectory(runtimeRootPath);

            File.WriteAllText(
                Path.Combine(runtimeRootPath, "runtime_graphics_renderer_manifest.hpp"),
                BuildHeaderContents());
            File.WriteAllText(
                Path.Combine(runtimeRootPath, "runtime_graphics_renderer_manifest.cpp"),
                BuildSourceContents(manifest));
        }

        /// <summary>
        /// Builds the generated runtime graphics-renderer manifest header.
        /// </summary>
        /// <returns>Generated C++ header text.</returns>
        static string BuildHeaderContents() {
            StringBuilder builder = new();
            builder.AppendLine("#pragma once");
            builder.AppendLine();
            builder.AppendLine("enum class HERuntimeDepthPrepassMode {");
            builder.AppendLine("    Auto = 0,");
            builder.AppendLine("    Disabled = 1,");
            builder.AppendLine("    Always = 2");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("enum class HERuntimePostProcessTier {");
            builder.AppendLine("    Disabled = 0,");
            builder.AppendLine("    Low = 1,");
            builder.AppendLine("    High = 2");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("enum class HERuntimePs2DepthHandlerMode {");
            builder.AppendLine("    Hardware = 0,");
            builder.AppendLine("    Software = 1");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("struct HERuntimeGraphicsRendererManifest {");
            builder.AppendLine("    HERuntimeDepthPrepassMode DepthPrepassMode;");
            builder.AppendLine("    const char* ShadowQualityTier;");
            builder.AppendLine("    bool HdrEnabled;");
            builder.AppendLine("    HERuntimePostProcessTier PostProcessTier;");
            builder.AppendLine("    HERuntimePs2DepthHandlerMode Ps2DepthHandlerMode;");
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("const HERuntimeGraphicsRendererManifest* he_get_runtime_graphics_renderer_manifest();");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the generated runtime graphics-renderer manifest implementation.
        /// </summary>
        /// <param name="manifest">Resolved renderer-default manifest to embed into native source.</param>
        /// <returns>Generated C++ implementation text.</returns>
        static string BuildSourceContents(RuntimeGraphicsRendererManifest manifest) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            StringBuilder builder = new();
            builder.AppendLine("#include \"runtime/runtime_graphics_renderer_manifest.hpp\"");
            builder.AppendLine();
            builder.AppendLine("static const HERuntimeGraphicsRendererManifest kRuntimeGraphicsRendererManifest = {");
            builder.AppendLine("    " + ResolveDepthPrepassModeExpression(manifest.DepthPrepassMode) + ",");
            builder.AppendLine("    \"" + EscapeCppStringLiteral(manifest.ShadowQualityTier) + "\",");
            builder.AppendLine("    " + (manifest.HdrEnabled ? "true" : "false") + ",");
            builder.AppendLine("    " + ResolvePostProcessTierExpression(manifest.PostProcessTier) + ",");
            builder.AppendLine("    " + ResolvePs2DepthHandlerModeExpression(manifest.Ps2DepthHandlerMode));
            builder.AppendLine("};");
            builder.AppendLine();
            builder.AppendLine("const HERuntimeGraphicsRendererManifest* he_get_runtime_graphics_renderer_manifest() {");
            builder.AppendLine("    return &kRuntimeGraphicsRendererManifest;");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// Resolves one runtime depth-prepass mode into its generated C++ enumeration expression.
        /// </summary>
        /// <param name="depthPrepassMode">Runtime depth-prepass mode to translate.</param>
        /// <returns>Generated C++ enumeration expression.</returns>
        static string ResolveDepthPrepassModeExpression(DepthPrepassMode depthPrepassMode) {
            if (depthPrepassMode == DepthPrepassMode.Always) {
                return "HERuntimeDepthPrepassMode::Always";
            }
            if (depthPrepassMode == DepthPrepassMode.Disabled) {
                return "HERuntimeDepthPrepassMode::Disabled";
            }

            return "HERuntimeDepthPrepassMode::Auto";
        }

        /// <summary>
        /// Resolves one runtime post-process tier into its generated C++ enumeration expression.
        /// </summary>
        /// <param name="postProcessTier">Runtime post-process tier to translate.</param>
        /// <returns>Generated C++ enumeration expression.</returns>
        static string ResolvePostProcessTierExpression(PostProcessTier postProcessTier) {
            if (postProcessTier == PostProcessTier.High) {
                return "HERuntimePostProcessTier::High";
            }
            if (postProcessTier == PostProcessTier.Low) {
                return "HERuntimePostProcessTier::Low";
            }

            return "HERuntimePostProcessTier::Disabled";
        }

        /// <summary>
        /// Resolves one PS2 depth-handler mode into its generated C++ enumeration expression.
        /// </summary>
        /// <param name="ps2DepthHandlerMode">PS2 depth-handler mode to translate.</param>
        /// <returns>Generated C++ enumeration expression.</returns>
        static string ResolvePs2DepthHandlerModeExpression(Ps2DepthHandlerMode ps2DepthHandlerMode) {
            if (ps2DepthHandlerMode == Ps2DepthHandlerMode.Software) {
                return "HERuntimePs2DepthHandlerMode::Software";
            }

            return "HERuntimePs2DepthHandlerMode::Hardware";
        }

        /// <summary>
        /// Escapes one string for safe embedding inside a C++ string literal.
        /// </summary>
        /// <param name="value">String value to escape.</param>
        /// <returns>Escaped literal contents without the surrounding quotes.</returns>
        static string EscapeCppStringLiteral(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int index = 0; index < value.Length; index++) {
                char current = value[index];
                if (current == '\\') {
                    builder.Append("\\\\");
                } else if (current == '"') {
                    builder.Append("\\\"");
                } else if (current == '\n') {
                    builder.Append("\\n");
                } else if (current == '\r') {
                    builder.Append("\\r");
                } else if (current == '\t') {
                    builder.Append("\\t");
                } else {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }
    }
}
