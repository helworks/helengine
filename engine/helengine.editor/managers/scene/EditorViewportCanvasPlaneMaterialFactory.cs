namespace helengine.editor {
    /// <summary>
    /// Builds the textured material used by the world-space 2D canvas preview plane.
    /// </summary>
    public static class EditorViewportCanvasPlaneMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by the canvas-plane material.
        /// </summary>
        const string ShaderFileName = "EditorViewportCanvasPlane.hlsl";
        /// <summary>
        /// Material asset identifier used by the canvas-plane material.
        /// </summary>
        const string MaterialAssetId = "EditorViewportCanvasPlane.material";
        /// <summary>
        /// Vertex program name used by the runtime canvas-plane shader.
        /// </summary>
        const string VertexProgramName = "EditorViewportCanvasPlane.vs";
        /// <summary>
        /// Pixel program name used by the runtime canvas-plane shader.
        /// </summary>
        const string PixelProgramName = "EditorViewportCanvasPlane.ps";
        /// <summary>
        /// Variant name used by the runtime canvas-plane shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the runtime material used by the world-space 2D canvas preview plane.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <param name="canvasTexture">Sampleable render target texture used by the preview plane.</param>
        /// <returns>Runtime material configured to sample the preview canvas texture.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D, RuntimeTexture canvasTexture) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (canvasTexture == null) {
                throw new ArgumentNullException(nameof(canvasTexture));
            }

            ShaderCompileTarget target = ResolveTarget(render3D);
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(target, ShaderFileName);
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

            RuntimeMaterial material = render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            material.Properties.SetTexture("CanvasTexture", canvasTexture);
            return material;
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

            throw new InvalidOperationException("Unsupported renderer backend for viewport canvas planes.");
        }
    }
}
