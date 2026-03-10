namespace helengine.editor {
    /// <summary>
    /// Builds the textured material used to render world-space transform-gizmo axis labels.
    /// </summary>
    public static class TransformGizmoAxisLabelMaterialFactory {
        /// <summary>
        /// Shader asset identifier used by the axis-label billboard material.
        /// </summary>
        const string ShaderAssetId = "EditorTransformGizmoAxisLabel";
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
        /// Variant name used for the runtime billboard shader.
        /// </summary>
        const string VariantName = "default";
        /// <summary>
        /// Logical source path used for shader diagnostics.
        /// </summary>
        const string SourcePath = "EditorTransformGizmoAxisLabel.hlsl";
        /// <summary>
        /// HLSL source used to render the textured axis-label billboard on DirectX11.
        /// </summary>
        const string ShaderSource =
            "cbuffer TransformBuffer : register(b0)\n" +
            "{\n" +
            "    float4x4 worldViewProj;\n" +
            "};\n" +
            "\n" +
            "Texture2D LabelTexture : register(t0);\n" +
            "SamplerState LabelSampler : register(s0);\n" +
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
            "    float2 texCoord : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    output.texCoord = input.texCoord;\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    uint textureWidth;\n" +
            "    uint textureHeight;\n" +
            "    LabelTexture.GetDimensions(textureWidth, textureHeight);\n" +
            "    float2 texelSize = float2(1.0f / textureWidth, 1.0f / textureHeight);\n" +
            "    float4 color = LabelTexture.Sample(LabelSampler, input.texCoord);\n" +
            "    float outlineAlpha = 0.0f;\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(-texelSize.x, 0.0f)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(texelSize.x, 0.0f)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(0.0f, -texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(0.0f, texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(-texelSize.x, -texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(texelSize.x, -texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(-texelSize.x, texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(texelSize.x, texelSize.y)).a);\n" +
            "    if (color.a > 0.01f)\n" +
            "    {\n" +
            "        return color;\n" +
            "    }\n" +
            "    clip(outlineAlpha - 0.01f);\n" +
            "    return float4(0.0f, 0.0f, 0.0f, 1.0f);\n" +
            "}\n";
        /// <summary>
        /// GLSL vertex shader used to render the textured axis-label billboard on Vulkan.
        /// </summary>
        const string VulkanVertexShaderSource =
            "#version 450\n" +
            "layout(set = 0, binding = 0) uniform TransformBuffer {\n" +
            "    mat4 worldViewProj;\n" +
            "};\n" +
            "\n" +
            "layout(location = 0) in vec3 inPos;\n" +
            "layout(location = 1) in vec3 inNormal;\n" +
            "layout(location = 2) in vec2 inTexCoord;\n" +
            "\n" +
            "layout(location = 0) out vec2 fragTexCoord;\n" +
            "\n" +
            "void main() {\n" +
            "    gl_Position = worldViewProj * vec4(inPos, 1.0);\n" +
            "    fragTexCoord = inTexCoord;\n" +
            "}\n";
        /// <summary>
        /// GLSL fragment shader used to render the textured axis-label billboard on Vulkan.
        /// </summary>
        const string VulkanFragmentShaderSource =
            "#version 450\n" +
            "layout(set = 1, binding = 0) uniform texture2D LabelTexture;\n" +
            "layout(set = 1, binding = 1) uniform sampler LabelSampler;\n" +
            "\n" +
            "layout(location = 0) in vec2 fragTexCoord;\n" +
            "layout(location = 0) out vec4 outColor;\n" +
            "\n" +
            "void main() {\n" +
            "    vec2 texelSize = 1.0 / vec2(textureSize(sampler2D(LabelTexture, LabelSampler), 0));\n" +
            "    vec4 color = texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord);\n" +
            "    float outlineAlpha = 0.0;\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(-texelSize.x, 0.0)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(texelSize.x, 0.0)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(0.0, -texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(0.0, texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(-texelSize.x, -texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(texelSize.x, -texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(-texelSize.x, texelSize.y)).a);\n" +
            "    outlineAlpha = max(outlineAlpha, texture(sampler2D(LabelTexture, LabelSampler), fragTexCoord + vec2(texelSize.x, texelSize.y)).a);\n" +
            "    if (color.a > 0.01) {\n" +
            "        outColor = color;\n" +
            "        return;\n" +
            "    }\n" +
            "    if (outlineAlpha <= 0.01) {\n" +
            "        discard;\n" +
            "    }\n" +
            "    outColor = vec4(0.0, 0.0, 0.0, 1.0);\n" +
            "}\n";

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
            material.Texture = font.Texture;
            return material;
        }

        /// <summary>
        /// Builds the runtime shader asset required by the axis-label material.
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
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(SourcePath, ShaderSource);
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
        /// Builds the Vulkan shader asset required by the axis-label material.
        /// </summary>
        /// <returns>Compiled Vulkan shader asset.</returns>
        static ShaderAsset BuildVulkanShaderAsset() {
            byte[] vertexBytecode = helengine.vulkan.VulkanShaderCompiler.CompileGlslToSpirv(
                VulkanVertexShaderSource,
                Silk.NET.Shaderc.ShaderKind.VertexShader,
                "EditorTransformGizmoAxisLabel.vert");
            byte[] pixelBytecode = helengine.vulkan.VulkanShaderCompiler.CompileGlslToSpirv(
                VulkanFragmentShaderSource,
                Silk.NET.Shaderc.ShaderKind.FragmentShader,
                "EditorTransformGizmoAxisLabel.frag");

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
                    throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo axis labels.");
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

            throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo axis labels.");
        }

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

            string message = string.Concat("Transform gizmo axis-label ", stageName, " shader compilation failed.");
            if (result.Diagnostics.Count > 0 && !string.IsNullOrWhiteSpace(result.Diagnostics[0].Message)) {
                message = result.Diagnostics[0].Message;
            }

            throw new InvalidOperationException(message);
        }
    }
}
