namespace helengine.editor {
    /// <summary>
    /// Builds the textured material used to render world-space transform-gizmo axis labels.
    /// </summary>
    public static class TransformGizmoAxisLabelMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by the axis-label billboard material.
        /// </summary>
        const string ShaderFileName = "EditorTransformGizmoAxisLabel.hlsl";
        /// <summary>
        /// Material asset identifier used by the axis-label billboard material.
        /// </summary>
        const string MaterialAssetId = "EditorTransformGizmoAxisLabel.material";
        /// <summary>
        /// Vertex program name used by the axis-label billboard material.
        /// </summary>
        const string VertexProgramName = "EditorTransformGizmoAxisLabel.vs";
        /// <summary>
        /// Pixel program name used by the axis-label billboard material.
        /// </summary>
        const string PixelProgramName = "EditorTransformGizmoAxisLabel.ps";
        /// <summary>
        /// Variant name used by the runtime axis-label shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds a textured runtime material that samples the supplied font atlas in 3D.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <param name="font">Font whose atlas texture will be sampled by the material.</param>
        /// <returns>Runtime material configured for axis-label billboards.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D, FontAsset font) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            if (font.Texture == null) {
                throw new InvalidOperationException("Axis-label billboards require the font atlas texture to be loaded.");
            }

            ShaderCompileTarget target = ResolveTarget(render3D);
            ShaderAsset shaderAsset = BuildShaderAsset(target);
            var materialAsset = new MaterialAsset {
                Id = MaterialAssetId,
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = VertexProgramName,
                PixelProgram = PixelProgramName,
                Variant = VariantName
            };

            RuntimeMaterial material = render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            ShaderRuntimeMaterialAccess.Require(material).Properties.SetTexture("LabelTexture", font.Texture);
            return material;
        }

        /// <summary>
        /// Loads the runtime shader asset required by the axis-label material.
        /// </summary>
        /// <param name="target">Renderer backend target that will consume the shader.</param>
        /// <returns>Compiled shader asset for the selected backend.</returns>
        static ShaderAsset BuildShaderAsset(ShaderCompileTarget target) {
            return EditorBuiltInShaderAssetLibrary.LoadShaderAsset(target, ShaderFileName);
        }

        /// <summary>
        /// Resolves the shader compile target that matches the active renderer.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Shader compile target matching the runtime renderer.</returns>
        static ShaderCompileTarget ResolveTarget(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (render3D is helengine.directx11.DirectX11Renderer3D) {
                return ShaderCompileTarget.DirectX11;
            } else if (render3D is helengine.vulkan.VulkanRenderer3D) {
                return ShaderCompileTarget.Vulkan;
            }

            throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo axis labels.");
        }
    }
}
