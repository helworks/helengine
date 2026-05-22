namespace helengine.editor {
    /// <summary>
    /// Builds the procedural material used to render rotation snap-preview discs.
    /// </summary>
    public static class TransformGizmoRotationPreviewMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by the rotation-preview material.
        /// </summary>
        const string ShaderFileName = "EditorTransformGizmoRotationPreview.hlsl";
        /// <summary>
        /// Material asset identifier used by the rotation-preview material.
        /// </summary>
        const string MaterialAssetId = "EditorTransformGizmoRotationPreview.material";
        /// <summary>
        /// Vertex program name used by the runtime preview shader.
        /// </summary>
        const string VertexProgramName = "EditorTransformGizmoRotationPreview.vs";
        /// <summary>
        /// Pixel program name used by the runtime preview shader.
        /// </summary>
        const string PixelProgramName = "EditorTransformGizmoRotationPreview.ps";
        /// <summary>
        /// Variant name used for the runtime preview shader.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the runtime material used by rotation snap previews.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material configured for procedural rotation previews.</returns>
        public static RuntimeMaterial Create(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            ShaderCompileTarget target = ResolveTarget(render3D);
            ShaderAsset shaderAsset = BuildShaderAsset(target);
            var materialAsset = new ShaderMaterialAsset {
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
        /// Loads the runtime shader asset required by the rotation-preview material.
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

            throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo rotation previews.");
        }
    }
}
