namespace helengine.editor {
    /// <summary>
    /// Builds the procedural material used to render rotation snap-preview discs.
    /// </summary>
    public static class TransformGizmoRotationPreviewMaterialFactory {
        /// <summary>
        /// Shader asset identifier used by the rotation-preview material.
        /// </summary>
        const string ShaderAssetId = "EditorTransformGizmoRotationPreview";
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
        /// Logical source path used for shader diagnostics.
        /// </summary>
        const string SourcePath = "EditorTransformGizmoRotationPreview.hlsl";

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
        /// Builds the runtime shader asset required by the rotation-preview material.
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
        /// Builds the Vulkan shader asset required by the rotation-preview material.
        /// </summary>
        /// <returns>Compiled Vulkan shader asset.</returns>
        static ShaderAsset BuildVulkanShaderAsset() {
            byte[] vertexBytecode = helengine.vulkan.VulkanShaderCompiler.CompileGlslToSpirv(
                GetVulkanVertexShaderSource(),
                Silk.NET.Shaderc.ShaderKind.VertexShader,
                "EditorTransformGizmoRotationPreview.vert");
            byte[] pixelBytecode = helengine.vulkan.VulkanShaderCompiler.CompileGlslToSpirv(
                GetVulkanFragmentShaderSource(),
                Silk.NET.Shaderc.ShaderKind.FragmentShader,
                "EditorTransformGizmoRotationPreview.frag");

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
                    throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo rotation previews.");
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

            throw new InvalidOperationException("Unsupported renderer backend for transform-gizmo rotation previews.");
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

            string message = string.Concat("Transform gizmo rotation-preview ", stageName, " shader compilation failed.");
            if (result.Diagnostics.Count > 0 && !string.IsNullOrWhiteSpace(result.Diagnostics[0].Message)) {
                message = result.Diagnostics[0].Message;
            }

            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Builds the DirectX11 HLSL source used to render the procedural rotation preview.
        /// </summary>
        /// <returns>HLSL shader source for the rotation preview material.</returns>
        static string GetDirectX11ShaderSource() {
            string previewRadiusText = TransformRotationSnapPreviewModelFactory.PreviewRadius.ToString("0.0############", System.Globalization.CultureInfo.InvariantCulture);
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
                "    float snapDegrees : TEXCOORD1;\n" +
                "};\n" +
                "\n" +
                "float ComputeLine(float distanceToLine, float lineWidth)\n" +
                "{\n" +
                "    return saturate((lineWidth - distanceToLine) / lineWidth);\n" +
                "}\n" +
                "\n" +
                "PS_IN VS(VS_IN input)\n" +
                "{\n" +
                "    PS_IN output;\n" +
                "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
                "    output.localPos = input.pos.xy;\n" +
                "    output.snapDegrees = input.texCoord.x;\n" +
                "    return output;\n" +
                "}\n" +
                "\n" +
                "float4 PS(PS_IN input) : SV_Target\n" +
                "{\n" +
                "    float2 localPos = input.localPos;\n" +
                "    float radius = length(localPos);\n" +
                "    float previewRadius = " + previewRadiusText + "f;\n" +
                "    float normalizedRadius = radius / previewRadius;\n" +
                "    float edgeFade = saturate(1.0f - normalizedRadius);\n" +
                "    edgeFade *= edgeFade;\n" +
                "    float outerRing = ComputeLine(abs(normalizedRadius - 0.96f), 0.08f);\n" +
                "    float centerGlow = saturate(1.0f - (radius / (previewRadius * 0.58f)));\n" +
                "    centerGlow *= centerGlow;\n" +
                "    float snapRadians = max(radians(max(input.snapDegrees, 1.0f)), 0.0001f);\n" +
                "    float angle = atan2(localPos.y, localPos.x);\n" +
                "    float spokePhase = frac((angle + 3.14159265f) / snapRadians);\n" +
                "    float spokeDistance = abs(spokePhase - 0.5f);\n" +
                "    float spokeMask = ComputeLine(spokeDistance, 0.035f);\n" +
                "    float spokeRadialFade = saturate((normalizedRadius - 0.12f) / 0.18f) * saturate((1.0f - normalizedRadius) / 0.05f);\n" +
                "    spokeMask *= spokeRadialFade;\n" +
                "    float brightness = saturate((spokeMask * 0.52f) + (centerGlow * 0.78f) + (outerRing * 0.26f));\n" +
                "    float3 color = lerp(float3(0.72f, 0.72f, 0.76f), float3(1.0f, 1.0f, 1.0f), brightness);\n" +
                "    float alpha = edgeFade * ((spokeMask * 0.18f) + (centerGlow * 0.24f) + (outerRing * 0.10f) + 0.03f);\n" +
                "    clip(alpha - 0.01f);\n" +
                "    return float4(color, alpha);\n" +
                "}\n";
        }

        /// <summary>
        /// Builds the Vulkan GLSL vertex shader used to render the procedural rotation preview.
        /// </summary>
        /// <returns>GLSL vertex shader source for the rotation preview material.</returns>
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
                "layout(location = 1) out float fragSnapDegrees;\n" +
                "\n" +
                "void main() {\n" +
                "    gl_Position = worldViewProj * vec4(inPos, 1.0);\n" +
                "    fragLocalPos = inPos.xy;\n" +
                "    fragSnapDegrees = inTexCoord.x;\n" +
                "}\n";
        }

        /// <summary>
        /// Builds the Vulkan GLSL fragment shader used to render the procedural rotation preview.
        /// </summary>
        /// <returns>GLSL fragment shader source for the rotation preview material.</returns>
        static string GetVulkanFragmentShaderSource() {
            string previewRadiusText = TransformRotationSnapPreviewModelFactory.PreviewRadius.ToString("0.0############", System.Globalization.CultureInfo.InvariantCulture);
            return
                "#version 450\n" +
                "\n" +
                "layout(location = 0) in vec2 fragLocalPos;\n" +
                "layout(location = 1) in float fragSnapDegrees;\n" +
                "layout(location = 0) out vec4 outColor;\n" +
                "\n" +
                "float computeLine(float distanceToLine, float lineWidth) {\n" +
                "    return clamp((lineWidth - distanceToLine) / lineWidth, 0.0, 1.0);\n" +
                "}\n" +
                "\n" +
                "void main() {\n" +
                "    vec2 localPos = fragLocalPos;\n" +
                "    float radius = length(localPos);\n" +
                "    float previewRadius = " + previewRadiusText + ";\n" +
                "    float normalizedRadius = radius / previewRadius;\n" +
                "    float edgeFade = clamp(1.0 - normalizedRadius, 0.0, 1.0);\n" +
                "    edgeFade *= edgeFade;\n" +
                "    float outerRing = computeLine(abs(normalizedRadius - 0.96), 0.08);\n" +
                "    float centerGlow = clamp(1.0 - (radius / (previewRadius * 0.58)), 0.0, 1.0);\n" +
                "    centerGlow *= centerGlow;\n" +
                "    float snapRadians = max(radians(max(fragSnapDegrees, 1.0)), 0.0001);\n" +
                "    float angle = atan(localPos.y, localPos.x);\n" +
                "    float spokePhase = fract((angle + 3.14159265) / snapRadians);\n" +
                "    float spokeDistance = abs(spokePhase - 0.5);\n" +
                "    float spokeMask = computeLine(spokeDistance, 0.035);\n" +
                "    float spokeRadialFade = clamp((normalizedRadius - 0.12) / 0.18, 0.0, 1.0) * clamp((1.0 - normalizedRadius) / 0.05, 0.0, 1.0);\n" +
                "    spokeMask *= spokeRadialFade;\n" +
                "    float brightness = clamp((spokeMask * 0.52) + (centerGlow * 0.78) + (outerRing * 0.26), 0.0, 1.0);\n" +
                "    vec3 color = mix(vec3(0.72, 0.72, 0.76), vec3(1.0, 1.0, 1.0), brightness);\n" +
                "    float alpha = edgeFade * ((spokeMask * 0.18) + (centerGlow * 0.24) + (outerRing * 0.10) + 0.03);\n" +
                "    if (alpha <= 0.01) {\n" +
                "        discard;\n" +
                "    }\n" +
                "    outColor = vec4(color, alpha);\n" +
                "}\n";
        }
    }
}
