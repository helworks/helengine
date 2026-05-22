namespace helengine.editor {
    /// <summary>
    /// Builds the translucent materials used by translation gizmo plane handles.
    /// </summary>
    public static class TransformGizmoPlaneMaterialFactory {
        /// <summary>
        /// Built-in shader source file used by the normal plane-handle material.
        /// </summary>
        const string NormalShaderFileName = "EditorTransformGizmoPlane.hlsl";
        /// <summary>
        /// Built-in shader source file used by the highlighted plane-handle material.
        /// </summary>
        const string HighlightShaderFileName = "EditorTransformGizmoPlaneHighlight.hlsl";
        /// <summary>
        /// Material asset identifier used by the normal plane-handle material.
        /// </summary>
        const string NormalMaterialAssetId = "EditorTransformGizmoPlane.material";
        /// <summary>
        /// Material asset identifier used by the highlighted plane-handle material.
        /// </summary>
        const string HighlightMaterialAssetId = "EditorTransformGizmoPlaneHighlight.material";
        /// <summary>
        /// Shared runtime shader variant name used by the plane-handle materials.
        /// </summary>
        const string VariantName = "default";

        /// <summary>
        /// Builds the normal translucent material used by translation gizmo plane handles.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material configured for non-hovered plane handles.</returns>
        public static RuntimeMaterial CreateNormal(RenderManager3D render3D) {
            return Create(render3D, NormalShaderFileName, NormalMaterialAssetId);
        }

        /// <summary>
        /// Builds the highlighted translucent material used by translation gizmo plane handles.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material configured for hovered plane handles.</returns>
        public static RuntimeMaterial CreateHighlight(RenderManager3D render3D) {
            return Create(render3D, HighlightShaderFileName, HighlightMaterialAssetId);
        }

        /// <summary>
        /// Loads the runtime shader asset required by one plane-handle material.
        /// </summary>
        /// <param name="target">Renderer backend target that will consume the shader.</param>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <returns>Compiled shader asset for the selected backend.</returns>
        static ShaderAsset BuildShaderAsset(ShaderCompileTarget target, string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            return EditorBuiltInShaderAssetLibrary.LoadShaderAsset(target, shaderFileName);
        }

        /// <summary>
        /// Builds one translucent plane-handle material from the supplied built-in shader file.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <param name="shaderFileName">Built-in shader source file name.</param>
        /// <param name="materialAssetId">Material asset identifier written to the raw material.</param>
        /// <returns>Runtime material configured for plane-handle rendering.</returns>
        static RuntimeMaterial Create(RenderManager3D render3D, string shaderFileName, string materialAssetId) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            if (string.IsNullOrWhiteSpace(materialAssetId)) {
                throw new ArgumentException("Material asset id must be provided.", nameof(materialAssetId));
            }

            ShaderCompileTarget target = ResolveTarget(render3D);
            ShaderAsset shaderAsset = BuildShaderAsset(target, shaderFileName);
            string shaderName = Path.GetFileNameWithoutExtension(shaderFileName);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Built-in plane shader name could not be resolved.");
            }

            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Plane shader asset id must be provided.");
            }

            var materialAsset = new ShaderMaterialAsset {
                Id = materialAssetId,
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = string.Concat(shaderName, ".vs"),
                PixelProgram = string.Concat(shaderName, ".ps"),
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
            } else if (render3D is IShaderCompileTargetProvider shaderCompileTargetProvider) {
                return shaderCompileTargetProvider.ShaderCompileTarget;
            }

            throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo plane materials.");
        }
    }
}
