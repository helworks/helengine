using helengine.directx11;
using helengine.vulkan;

namespace helengine.render.validation {
    /// <summary>
    /// Builds runtime shader and material assets used by render validation scenarios.
    /// </summary>
    public static class RenderShaderFactory {
        /// <summary>
        /// Shader asset identifier used by validation materials.
        /// </summary>
        const string ShaderAssetId = "ValidationShader";
        /// <summary>
        /// Shader asset identifier used by transform gizmo validation materials.
        /// </summary>
        const string TransformGizmoShaderAssetId = "ValidationTransformGizmoShader";
        /// <summary>
        /// Material asset identifier used by validation meshes.
        /// </summary>
        const string MaterialAssetId = "ValidationMaterial.material";
        /// <summary>
        /// Material asset identifier used by transform gizmo validation meshes.
        /// </summary>
        const string TransformGizmoMaterialAssetId = "ValidationTransformGizmoMaterial.material";
        /// <summary>
        /// Vertex program name for validation shaders.
        /// </summary>
        const string VertexProgramName = "ValidationShader.vs";
        /// <summary>
        /// Vertex program name for transform gizmo validation shaders.
        /// </summary>
        const string TransformGizmoVertexProgramName = "ValidationTransformGizmoShader.vs";
        /// <summary>
        /// Pixel program name for validation shaders.
        /// </summary>
        const string PixelProgramName = "ValidationShader.ps";
        /// <summary>
        /// Pixel program name for transform gizmo validation shaders.
        /// </summary>
        const string TransformGizmoPixelProgramName = "ValidationTransformGizmoShader.ps";
        /// <summary>
        /// Variant name used during shader compilation.
        /// </summary>
        const string VariantName = "default";
        /// <summary>
        /// In-memory source path identifier used for diagnostics.
        /// </summary>
        const string SourcePath = "ValidationShader.hlsl";
        /// <summary>
        /// In-memory source path identifier used for transform gizmo diagnostics.
        /// </summary>
        const string TransformGizmoSourcePath = "ValidationTransformGizmoShader.hlsl";
        /// <summary>
        /// HLSL source used for backend validation.
        /// </summary>
        const string ShaderSource =
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
            "};\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    return float4(0.0, 1.0, 0.0, 1.0);\n" +
            "}\n";
        /// <summary>
        /// HLSL source used for transform gizmo validation.
        /// </summary>
        const string TransformGizmoShaderSource =
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
            "    float3 normal : NORMAL;\n" +
            "    float2 marker : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "float3 DecodeAxisColor(float2 marker)\n" +
            "{\n" +
            "    if (marker.y > 0.5f)\n" +
            "    {\n" +
            "        return float3(0.20f, 0.50f, 1.00f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.5f)\n" +
            "    {\n" +
            "        return float3(0.20f, 0.95f, 0.35f);\n" +
            "    }\n" +
            "\n" +
            "    return float3(1.00f, 0.30f, 0.30f);\n" +
            "}\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    output.normal = input.normal;\n" +
            "    output.marker = input.texCoord;\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    float3 normal = normalize(input.normal);\n" +
            "    float3 lightDirection0 = normalize(float3(0.45f, 0.85f, -0.30f));\n" +
            "    float3 lightDirection1 = normalize(float3(-0.60f, 0.55f, 0.65f));\n" +
            "    float diffuse0 = saturate(dot(normal, lightDirection0));\n" +
            "    float diffuse1 = saturate(dot(normal, lightDirection1));\n" +
            "    float lighting = 0.22f + diffuse0 * 0.72f + diffuse1 * 0.28f;\n" +
            "    float3 axisColor = DecodeAxisColor(input.marker);\n" +
            "    return float4(axisColor * lighting, 1.0f);\n" +
            "}\n";

        /// <summary>
        /// Creates a shader asset for the selected render backend.
        /// </summary>
        /// <param name="backend">Backend requiring compiled shader binaries.</param>
        /// <returns>Compiled shader asset.</returns>
        public static ShaderAsset BuildShaderAsset(RenderBackend backend) {
            return BuildShaderAssetInternal(
                backend,
                ShaderAssetId,
                VertexProgramName,
                PixelProgramName,
                SourcePath,
                ShaderSource);
        }

        /// <summary>
        /// Creates a transform gizmo shader asset for the selected render backend.
        /// </summary>
        /// <param name="backend">Backend requiring compiled shader binaries.</param>
        /// <returns>Compiled transform gizmo shader asset.</returns>
        public static ShaderAsset BuildTransformGizmoShaderAsset(RenderBackend backend) {
            return BuildShaderAssetInternal(
                backend,
                TransformGizmoShaderAssetId,
                TransformGizmoVertexProgramName,
                TransformGizmoPixelProgramName,
                TransformGizmoSourcePath,
                TransformGizmoShaderSource);
        }

        /// <summary>
        /// Creates the material asset matching the validation shader.
        /// </summary>
        /// <returns>Material asset configured with validation program names.</returns>
        public static MaterialAsset BuildMaterialAsset() {
            return BuildMaterialAssetInternal(
                MaterialAssetId,
                ShaderAssetId,
                VertexProgramName,
                PixelProgramName);
        }

        /// <summary>
        /// Creates the material asset matching the transform gizmo validation shader.
        /// </summary>
        /// <returns>Material asset configured with transform gizmo program names.</returns>
        public static MaterialAsset BuildTransformGizmoMaterialAsset() {
            return BuildMaterialAssetInternal(
                TransformGizmoMaterialAssetId,
                TransformGizmoShaderAssetId,
                TransformGizmoVertexProgramName,
                TransformGizmoPixelProgramName);
        }

        /// <summary>
        /// Creates a shader asset from the supplied source and program names.
        /// </summary>
        /// <param name="backend">Backend requiring compiled shader binaries.</param>
        /// <param name="shaderAssetId">Shader asset identifier.</param>
        /// <param name="vertexProgramName">Vertex program name.</param>
        /// <param name="pixelProgramName">Pixel program name.</param>
        /// <param name="sourcePath">In-memory source path used for diagnostics.</param>
        /// <param name="shaderSource">HLSL source to compile.</param>
        /// <returns>Compiled shader asset.</returns>
        static ShaderAsset BuildShaderAssetInternal(
            RenderBackend backend,
            string shaderAssetId,
            string vertexProgramName,
            string pixelProgramName,
            string sourcePath,
            string shaderSource) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            if (string.IsNullOrWhiteSpace(vertexProgramName)) {
                throw new ArgumentException("Vertex program name must be provided.", nameof(vertexProgramName));
            }

            if (string.IsNullOrWhiteSpace(pixelProgramName)) {
                throw new ArgumentException("Pixel program name must be provided.", nameof(pixelProgramName));
            }

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(shaderSource)) {
                throw new ArgumentException("Shader source must be provided.", nameof(shaderSource));
            }

            ShaderCompileTarget target = ResolveTarget(backend);
            ShaderCompileService compileService = CreateCompileService(target);
            ShaderCompileOptions compileOptions = CreateCompileOptions();
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(sourcePath, shaderSource);
            ShaderDefine[] defines = Array.Empty<ShaderDefine>();

            ShaderCompileResult vertexResult = CompileStage(
                compileService,
                sourceInfo,
                target,
                ShaderStage.Vertex,
                vertexProgramName,
                "VS",
                compileOptions,
                defines);
            ShaderCompileResult pixelResult = CompileStage(
                compileService,
                sourceInfo,
                target,
                ShaderStage.Pixel,
                pixelProgramName,
                "PS",
                compileOptions,
                defines);

            ValidateCompileResult(vertexResult);
            ValidateCompileResult(pixelResult);

            string targetName = ShaderTargetNames.GetTargetName(target);
            ShaderProgramDefinition[] programs = new[] {
                vertexResult.ProgramDefinition,
                pixelResult.ProgramDefinition
            };
            ShaderProgramBinary[] binaries = new[] {
                new ShaderProgramBinary(vertexProgramName, ShaderStage.Vertex, targetName, VariantName, vertexResult.Binary.Bytecode),
                new ShaderProgramBinary(pixelProgramName, ShaderStage.Pixel, targetName, VariantName, pixelResult.Binary.Bytecode)
            };
            var definition = new ShaderModuleDefinition(shaderAssetId, programs, binaries);
            return ShaderAsset.FromDefinition(definition, target);
        }

        /// <summary>
        /// Creates a material asset for the supplied shader and program identifiers.
        /// </summary>
        /// <param name="materialAssetId">Material asset identifier.</param>
        /// <param name="shaderAssetId">Shader asset identifier referenced by the material.</param>
        /// <param name="vertexProgramName">Vertex program name.</param>
        /// <param name="pixelProgramName">Pixel program name.</param>
        /// <returns>Material asset configured with supplied program names.</returns>
        static MaterialAsset BuildMaterialAssetInternal(
            string materialAssetId,
            string shaderAssetId,
            string vertexProgramName,
            string pixelProgramName) {
            if (string.IsNullOrWhiteSpace(materialAssetId)) {
                throw new ArgumentException("Material asset id must be provided.", nameof(materialAssetId));
            }

            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            if (string.IsNullOrWhiteSpace(vertexProgramName)) {
                throw new ArgumentException("Vertex program name must be provided.", nameof(vertexProgramName));
            }

            if (string.IsNullOrWhiteSpace(pixelProgramName)) {
                throw new ArgumentException("Pixel program name must be provided.", nameof(pixelProgramName));
            }

            return new MaterialAsset {
                Id = materialAssetId,
                ShaderAssetId = shaderAssetId,
                VertexProgram = vertexProgramName,
                PixelProgram = pixelProgramName,
                Variant = VariantName
            };
        }

        /// <summary>
        /// Compiles one shader stage for the requested backend target.
        /// </summary>
        /// <param name="compileService">Compile service used for shader compilation.</param>
        /// <param name="sourceInfo">Source code and logical path.</param>
        /// <param name="target">Target backend.</param>
        /// <param name="stage">Shader stage to compile.</param>
        /// <param name="programName">Program name to assign.</param>
        /// <param name="entryPoint">Entry point function to compile.</param>
        /// <param name="compileOptions">Shared compile options.</param>
        /// <param name="defines">Preprocessor defines.</param>
        /// <returns>Compile result for the stage.</returns>
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
        /// Builds compile options used by validation shaders.
        /// </summary>
        /// <returns>Compile options instance.</returns>
        static ShaderCompileOptions CreateCompileOptions() {
            return new ShaderCompileOptions(
                ShaderBindingPolicies.Default,
                true,
                false,
                false);
        }

        /// <summary>
        /// Creates a compile service and registers the backend matching the target.
        /// </summary>
        /// <param name="target">Target backend to register.</param>
        /// <returns>Configured compile service.</returns>
        static ShaderCompileService CreateCompileService(ShaderCompileTarget target) {
            string includeRoot = Environment.CurrentDirectory;
            var includeResolver = new ShaderFilesystemIncludeResolver(includeRoot);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            var service = new ShaderCompileService(includeResolver, cache, hasher);

            if (target == ShaderCompileTarget.DirectX11) {
                service.RegisterBackend(new DirectX11ShaderBackend());
                return service;
            }

            if (target == ShaderCompileTarget.Vulkan) {
                service.RegisterBackend(new VulkanShaderBackend());
                return service;
            }

            throw new InvalidOperationException("Unsupported shader compile target.");
        }

        /// <summary>
        /// Resolves a shader compile target for the selected renderer backend.
        /// </summary>
        /// <param name="backend">Renderer backend selection.</param>
        /// <returns>Matching shader compile target.</returns>
        static ShaderCompileTarget ResolveTarget(RenderBackend backend) {
            if (backend == RenderBackend.DirectX11) {
                return ShaderCompileTarget.DirectX11;
            }

            if (backend == RenderBackend.Vulkan) {
                return ShaderCompileTarget.Vulkan;
            }

            throw new InvalidOperationException("Unsupported renderer backend.");
        }

        /// <summary>
        /// Validates successful shader compilation and reports diagnostics on failure.
        /// </summary>
        /// <param name="result">Compile result to validate.</param>
        static void ValidateCompileResult(ShaderCompileResult result) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Success) {
                return;
            }

            string message = "Shader compilation failed.";
            if (result.Diagnostics.Count > 0) {
                message = result.Diagnostics[0].Message;
            }

            throw new InvalidOperationException(message);
        }
    }
}
