namespace helengine.editor {
    /// <summary>
    /// Builds the procedural material used by the default editor viewport grid.
    /// </summary>
    public static class EditorViewportGridMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by the viewport-grid material.
        /// </summary>
        const string ShaderFileName = "EditorViewportGrid.hlsl";
        /// <summary>
        /// Material asset identifier used by the viewport-grid material.
        /// </summary>
        const string MaterialAssetId = "EditorViewportGrid.material";
        /// <summary>
        /// Vertex program name used by the runtime viewport-grid shader.
        /// </summary>
        const string VertexProgramName = "EditorViewportGrid.vs";
        /// <summary>
        /// Pixel program name used by the runtime viewport-grid shader.
        /// </summary>
        const string PixelProgramName = "EditorViewportGrid.ps";
        /// <summary>
        /// Variant name used by the runtime viewport-grid shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the runtime material used by the default editor viewport grid.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material configured for the default editor viewport grid.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            ShaderCompileTarget target = ResolveTarget(render3D);
            ShaderAsset shaderAsset = BuildShaderAsset(target);
            var materialAsset = new MaterialAsset {
                Id = MaterialAssetId,
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = VertexProgramName,
                PixelProgram = PixelProgramName,
                Variant = VariantName,
                RenderState = new MaterialRenderState {
                    BlendMode = MaterialBlendMode.AlphaBlend,
                    CullMode = MaterialCullMode.None,
                    DepthTestEnabled = true,
                    DepthWriteEnabled = false
                }
            };

            return render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Loads the runtime shader asset required by the viewport-grid material.
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

            if (render3D is IShaderCompileTargetProvider targetProvider) {
                return targetProvider.ShaderCompileTarget;
            }

            throw new InvalidOperationException("Unsupported renderer backend for editor viewport grids.");
        }
    }
}
