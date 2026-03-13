namespace helengine.editor {
    /// <summary>
    /// Builds the procedural material used to render world-space transform-gizmo snap previews.
    /// </summary>
    public static class TransformGizmoGridPreviewMaterialFactory {
        /// <summary>
        /// Logical width of the preview plane expressed in grid-cell units.
        /// </summary>
        public const float PreviewCellSpan = 48f;
        /// <summary>
        /// Half-width of the preview plane expressed in grid-cell units.
        /// </summary>
        const double PreviewHalfCellSpan = PreviewCellSpan * 0.5;
        /// <summary>
        /// Shader asset identifier used by the grid-preview material.
        /// </summary>
        const string ShaderAssetId = "EditorTransformGizmoGridPreview";
        /// <summary>
        /// Material asset identifier used by the grid-preview material.
        /// </summary>
        const string MaterialAssetId = "EditorTransformGizmoGridPreview.material";
        /// <summary>
        /// Vertex program name used by the grid-preview material.
        /// </summary>
        const string VertexProgramName = "EditorTransformGizmoGridPreview.vs";
        /// <summary>
        /// Pixel program name used by the grid-preview material.
        /// </summary>
        const string PixelProgramName = "EditorTransformGizmoGridPreview.ps";
        /// <summary>
        /// Variant name used for the runtime grid-preview shader.
        /// </summary>
        const string VariantName = "default";
        /// <summary>
        /// Logical source path used for shader diagnostics.
        /// </summary>
        const string SourcePath = "EditorTransformGizmoGridPreview.hlsl";

        /// <summary>
        /// Builds the runtime material used by transform-gizmo snap previews.
        /// </summary>
        /// <param name="render3D">Renderer that will own the runtime material.</param>
        /// <returns>Runtime material configured for procedural grid previews.</returns>
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
                Variant = VariantName
            };

            return render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Builds the runtime shader asset required by the grid-preview material.
        /// </summary>
        /// <param name="target">Renderer backend target that will consume the shader.</param>
        /// <returns>Compiled shader asset for the selected backend.</returns>
        static ShaderAsset BuildShaderAsset(ShaderCompileTarget target) {
            if (target == ShaderCompileTarget.Vulkan) {
                return BuildVulkanShaderAsset();
            }

            ShaderCompileService compileService = CreateCompileService(target);
            ShaderCompileOptions compileOptions = new ShaderCompileOptions(
                ShaderBindingPolicies.Default,
                true,
                false,
                false);
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(SourcePath, GetDirectX11ShaderSource());
            IReadOnlyList<ShaderDefine> defines = Array.Empty<ShaderDefine>();

            ShaderCompileResult vertexResult = CompileStage(
                compileService,
                sourceInfo,
                target,
                ShaderStage.Vertex,
                VertexProgramName,
                "VS",
                compileOptions,
                defines);
            ShaderCompileResult pixelResult = CompileStage(
                compileService,
                sourceInfo,
                target,
                ShaderStage.Pixel,
                PixelProgramName,
                "PS",
                compileOptions,
                defines);

            ValidateCompileResult(vertexResult, "vertex");
            ValidateCompileResult(pixelResult, "pixel");

            string targetName = ShaderTargetNames.GetTargetName(target);
            ShaderProgramDefinition[] programs = new[] {
                vertexResult.ProgramDefinition,
                pixelResult.ProgramDefinition
            };
            ShaderProgramBinary[] binaries = new[] {
                new ShaderProgramBinary(VertexProgramName, ShaderStage.Vertex, targetName, VariantName, vertexResult.Binary.Bytecode),
                new ShaderProgramBinary(PixelProgramName, ShaderStage.Pixel, targetName, VariantName, pixelResult.Binary.Bytecode)
            };
            var moduleDefinition = new ShaderModuleDefinition(ShaderAssetId, programs, binaries);
            return ShaderAsset.FromDefinition(moduleDefinition, target);
        }

        /// <summary>
        /// Builds the Vulkan shader asset required by the grid-preview material.
        /// </summary>
        /// <returns>Compiled Vulkan shader asset.</returns>
        static ShaderAsset BuildVulkanShaderAsset() {
            byte[] vertexBytecode = helengine.vulkan.VulkanShaderCompiler.CompileGlslToSpirv(
                GetVulkanVertexShaderSource(),
                Silk.NET.Shaderc.ShaderKind.VertexShader,
                "EditorTransformGizmoGridPreview.vert");
            byte[] pixelBytecode = helengine.vulkan.VulkanShaderCompiler.CompileGlslToSpirv(
                GetVulkanFragmentShaderSource(),
                Silk.NET.Shaderc.ShaderKind.FragmentShader,
                "EditorTransformGizmoGridPreview.frag");

            string targetName = ShaderTargetNames.GetTargetName(ShaderCompileTarget.Vulkan);
            ShaderVariant[] variants = new[] { new ShaderVariant(VariantName, Array.Empty<string>()) };
            ShaderProgramDefinition[] programs = new[] {
                new ShaderProgramDefinition(
                    VertexProgramName,
                    ShaderStage.Vertex,
                    "main",
                    Array.Empty<ShaderBinding>(),
                    Array.Empty<ShaderVertexElement>(),
                    Array.Empty<ShaderVertexElement>(),
                    variants),
                new ShaderProgramDefinition(
                    PixelProgramName,
                    ShaderStage.Pixel,
                    "main",
                    Array.Empty<ShaderBinding>(),
                    Array.Empty<ShaderVertexElement>(),
                    Array.Empty<ShaderVertexElement>(),
                    variants)
            };
            ShaderProgramBinary[] binaries = new[] {
                new ShaderProgramBinary(VertexProgramName, ShaderStage.Vertex, targetName, VariantName, vertexBytecode),
                new ShaderProgramBinary(PixelProgramName, ShaderStage.Pixel, targetName, VariantName, pixelBytecode)
            };
            var moduleDefinition = new ShaderModuleDefinition(ShaderAssetId, programs, binaries);
            return ShaderAsset.FromDefinition(moduleDefinition, ShaderCompileTarget.Vulkan);
        }

        /// <summary>
        /// Compiles one shader stage for the selected backend target.
        /// </summary>
        /// <param name="compileService">Compile service used for shader compilation.</param>
        /// <param name="sourceInfo">Source code and logical source path.</param>
        /// <param name="target">Backend target to compile for.</param>
        /// <param name="stage">Shader stage being compiled.</param>
        /// <param name="programName">Logical program name stored in the shader asset.</param>
        /// <param name="entryPoint">Entry point function to compile.</param>
        /// <param name="compileOptions">Compile options shared by both stages.</param>
        /// <param name="defines">Preprocessor defines applied during compilation.</param>
        /// <returns>Compile result for the requested stage.</returns>
        static ShaderCompileResult CompileStage(
            ShaderCompileService compileService,
            ShaderSourceInfo sourceInfo,
            ShaderCompileTarget target,
            ShaderStage stage,
            string programName,
            string entryPoint,
            ShaderCompileOptions compileOptions,
            IReadOnlyList<ShaderDefine> defines) {
            var request = new ShaderCompileRequest(
                sourceInfo,
                programName,
                entryPoint,
                stage,
                target,
                new ShaderModel(4, 0),
                VariantName,
                defines,
                compileOptions);
            return compileService.Compile(request);
        }

        /// <summary>
        /// Creates the compile service configured for the selected backend target.
        /// </summary>
        /// <param name="target">Target backend that will consume the compiled shader.</param>
        /// <returns>Configured compile service.</returns>
        static ShaderCompileService CreateCompileService(ShaderCompileTarget target) {
            string includeRoot = Environment.CurrentDirectory;
            var includeResolver = new ShaderFilesystemIncludeResolver(includeRoot);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            var compileService = new ShaderCompileService(includeResolver, cache, hasher);

            switch (target) {
                case ShaderCompileTarget.DirectX11:
                    compileService.RegisterBackend(new helengine.directx11.DirectX11ShaderBackend());
                    return compileService;
                case ShaderCompileTarget.Vulkan:
                    compileService.RegisterBackend(new helengine.vulkan.VulkanShaderBackend());
                    return compileService;
                default:
                    throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo grid previews.");
            }
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
            }

            if (render3D is helengine.vulkan.VulkanRenderer3D) {
                return ShaderCompileTarget.Vulkan;
            }

            throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo grid previews.");
        }

        /// <summary>
        /// Validates a shader compile result and throws the leading diagnostic on failure.
        /// </summary>
        /// <param name="result">Compile result to validate.</param>
        /// <param name="stageName">Display name of the stage being validated.</param>
        static void ValidateCompileResult(ShaderCompileResult result, string stageName) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Success) {
                return;
            }

            string message = string.Concat("Transform gizmo grid-preview ", stageName, " shader compilation failed.");
            if (result.Diagnostics.Count > 0 && !string.IsNullOrWhiteSpace(result.Diagnostics[0].Message)) {
                message = result.Diagnostics[0].Message;
            }

            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Builds the DirectX11 HLSL source used to render the procedural grid preview.
        /// </summary>
        /// <returns>HLSL shader source for the grid preview material.</returns>
        static string GetDirectX11ShaderSource() {
            string halfExtentText = PreviewHalfCellSpan.ToString("0.0############", System.Globalization.CultureInfo.InvariantCulture);
            return
                "cbuffer TransformBuffer : register(b0)\n" +
                "{\n" +
                "    float4x4 worldViewProj;\n" +
                "};\n" +
                "\n" +
                "struct VS_IN\n" +
                "{\n" +
                "    float3 pos : POSITION;\n" +
                "    float3 normal : NORMAL;\n" +
                "    float2 texCoord : TEXCOORD0;\n" +
                "};\n" +
                "\n" +
                "struct PS_IN\n" +
                "{\n" +
                "    float4 pos : SV_POSITION;\n" +
                "    float2 localPos : TEXCOORD0;\n" +
                "};\n" +
                "\n" +
                "float ComputeLine(float value)\n" +
                "{\n" +
                "    float distanceToLine = abs(frac(value + 0.5f) - 0.5f);\n" +
                "    float lineWidth = 0.06f;\n" +
                "    return saturate((lineWidth - distanceToLine) / lineWidth);\n" +
                "}\n" +
                "\n" +
                "PS_IN VS(VS_IN input)\n" +
                "{\n" +
                "    PS_IN output;\n" +
                "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
                "    output.localPos = input.pos.xy;\n" +
                "    return output;\n" +
                "}\n" +
                "\n" +
                "float4 PS(PS_IN input) : SV_Target\n" +
                "{\n" +
                "    float2 localPos = input.localPos;\n" +
                "    float radial = saturate(length(localPos) / " + halfExtentText + ");\n" +
                "    float edgeFade = 1.0f - radial;\n" +
                "    edgeFade *= edgeFade;\n" +
                "    float lineMask = max(ComputeLine(localPos.x), ComputeLine(localPos.y));\n" +
                "    float centerGlow = saturate(1.0f - (length(localPos) / 2.5f));\n" +
                "    centerGlow *= centerGlow;\n" +
                "    float axisGlow = max(\n" +
                "        saturate((0.10f - abs(localPos.x)) / 0.10f),\n" +
                "        saturate((0.10f - abs(localPos.y)) / 0.10f));\n" +
                "    float brightness = saturate((lineMask * 0.45f) + (centerGlow * 0.75f) + (axisGlow * 0.35f));\n" +
                "    float3 color = lerp(float3(0.72f, 0.72f, 0.76f), float3(1.0f, 1.0f, 1.0f), brightness);\n" +
                "    float alpha = edgeFade * ((lineMask * 0.24f) + (centerGlow * 0.28f) + (axisGlow * 0.14f) + 0.04f);\n" +
                "    clip(alpha - 0.01f);\n" +
                "    return float4(color, alpha);\n" +
                "}\n";
        }

        /// <summary>
        /// Builds the Vulkan GLSL vertex shader used to render the procedural grid preview.
        /// </summary>
        /// <returns>GLSL vertex shader source for the grid preview material.</returns>
        static string GetVulkanVertexShaderSource() {
            return
                "#version 450\n" +
                "layout(set = 0, binding = 0) uniform TransformBuffer {\n" +
                "    mat4 worldViewProj;\n" +
                "};\n" +
                "\n" +
                "layout(location = 0) in vec3 inPos;\n" +
                "layout(location = 1) in vec3 inNormal;\n" +
                "layout(location = 2) in vec2 inTexCoord;\n" +
                "\n" +
                "layout(location = 0) out vec2 fragLocalPos;\n" +
                "\n" +
                "void main() {\n" +
                "    gl_Position = worldViewProj * vec4(inPos, 1.0);\n" +
                "    fragLocalPos = inPos.xy;\n" +
                "}\n";
        }

        /// <summary>
        /// Builds the Vulkan GLSL fragment shader used to render the procedural grid preview.
        /// </summary>
        /// <returns>GLSL fragment shader source for the grid preview material.</returns>
        static string GetVulkanFragmentShaderSource() {
            string halfExtentText = PreviewHalfCellSpan.ToString("0.0############", System.Globalization.CultureInfo.InvariantCulture);
            return
                "#version 450\n" +
                "\n" +
                "layout(location = 0) in vec2 fragLocalPos;\n" +
                "layout(location = 0) out vec4 outColor;\n" +
                "\n" +
                "float computeLine(float value) {\n" +
                "    float distanceToLine = abs(fract(value + 0.5) - 0.5);\n" +
                "    float lineWidth = 0.06;\n" +
                "    return clamp((lineWidth - distanceToLine) / lineWidth, 0.0, 1.0);\n" +
                "}\n" +
                "\n" +
                "void main() {\n" +
                "    vec2 localPos = fragLocalPos;\n" +
                "    float radial = clamp(length(localPos) / " + halfExtentText + ", 0.0, 1.0);\n" +
                "    float edgeFade = 1.0 - radial;\n" +
                "    edgeFade *= edgeFade;\n" +
                "    float lineMask = max(computeLine(localPos.x), computeLine(localPos.y));\n" +
                "    float centerGlow = clamp(1.0 - (length(localPos) / 2.5), 0.0, 1.0);\n" +
                "    centerGlow *= centerGlow;\n" +
                "    float axisGlow = max(\n" +
                "        clamp((0.10 - abs(localPos.x)) / 0.10, 0.0, 1.0),\n" +
                "        clamp((0.10 - abs(localPos.y)) / 0.10, 0.0, 1.0));\n" +
                "    float brightness = clamp((lineMask * 0.45) + (centerGlow * 0.75) + (axisGlow * 0.35), 0.0, 1.0);\n" +
                "    vec3 color = mix(vec3(0.72, 0.72, 0.76), vec3(1.0, 1.0, 1.0), brightness);\n" +
                "    float alpha = edgeFade * ((lineMask * 0.24) + (centerGlow * 0.28) + (axisGlow * 0.14) + 0.04);\n" +
                "    if (alpha <= 0.01) {\n" +
                "        discard;\n" +
                "    }\n" +
                "    outColor = vec4(color, alpha);\n" +
                "}\n";
        }
    }
}
