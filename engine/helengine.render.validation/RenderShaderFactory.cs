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
        /// Material asset identifier used by validation meshes.
        /// </summary>
        const string MaterialAssetId = "ValidationMaterial.material";
        /// <summary>
        /// Vertex program name for validation shaders.
        /// </summary>
        const string VertexProgramName = "ValidationShader.vs";
        /// <summary>
        /// Pixel program name for validation shaders.
        /// </summary>
        const string PixelProgramName = "ValidationShader.ps";
        /// <summary>
        /// Variant name used during shader compilation.
        /// </summary>
        const string VariantName = "default";
        /// <summary>
        /// In-memory source path identifier used for diagnostics.
        /// </summary>
        const string SourcePath = "ValidationShader.hlsl";
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
        /// Creates a shader asset for the selected render backend.
        /// </summary>
        /// <param name="backend">Backend requiring compiled shader binaries.</param>
        /// <returns>Compiled shader asset.</returns>
        public static ShaderAsset BuildShaderAsset(RenderBackend backend) {
            ShaderCompileTarget target = ResolveTarget(backend);
            ShaderCompileService compileService = CreateCompileService(target);
            ShaderCompileOptions compileOptions = CreateCompileOptions();
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(SourcePath, ShaderSource);
            ShaderDefine[] defines = Array.Empty<ShaderDefine>();

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

            ValidateCompileResult(vertexResult);
            ValidateCompileResult(pixelResult);

            string targetName = ShaderTargetNames.GetTargetName(target);
            ShaderProgramDefinition[] programs = new[] {
                vertexResult.ProgramDefinition,
                pixelResult.ProgramDefinition
            };
            ShaderProgramBinary[] binaries = new[] {
                new ShaderProgramBinary(VertexProgramName, ShaderStage.Vertex, targetName, VariantName, vertexResult.Binary.Bytecode),
                new ShaderProgramBinary(PixelProgramName, ShaderStage.Pixel, targetName, VariantName, pixelResult.Binary.Bytecode)
            };
            var definition = new ShaderModuleDefinition(ShaderAssetId, programs, binaries);
            return ShaderAsset.FromDefinition(definition, target);
        }

        /// <summary>
        /// Creates the material asset matching the validation shader.
        /// </summary>
        /// <returns>Material asset configured with validation program names.</returns>
        public static MaterialAsset BuildMaterialAsset() {
            return new MaterialAsset {
                Id = MaterialAssetId,
                ShaderAssetId = ShaderAssetId,
                VertexProgram = VertexProgramName,
                PixelProgram = PixelProgramName,
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
