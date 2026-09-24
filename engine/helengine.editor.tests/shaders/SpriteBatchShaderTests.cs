using helengine.directx11;
using helengine.vulkan;
using SharpDX.D3DCompiler;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Compiles the shared sprite batch variants through the real DX11 and Vulkan backends and checks compiler reflection.
    /// </summary>
    public class SpriteBatchShaderTests {
        /// <summary>
        /// Names the opt-in environment variable that exports compiler bytecode for external inspection.
        /// </summary>
        const string BytecodeExportDirectoryEnvironmentVariable = "HELENGINE_SPRITE_BATCH_BYTECODE_EXPORT_DIR";

        /// <summary>
        /// Stores the shader source and stable path used by both compiler backends.
        /// </summary>
        readonly ShaderSourceInfo SourceInfoValue;

        /// <summary>
        /// Initializes the built-in source description from the shared engine checkout used by shader tests.
        /// </summary>
        public SpriteBatchShaderTests() {
            string repositoryRootPath = TestSourceRepositoryLocator.ResolveHelEngineRootPath();
            string sourcePath = Path.Combine(repositoryRootPath, "engine", "helengine.editor", "shaders", "builtin", "SpriteBatchShader.hlsl");
            SourceInfoValue = new ShaderSourceInfo(sourcePath, File.ReadAllText(sourcePath));
        }

        /// <summary>
        /// Compiles both catalog variants for a real backend and checks their reflected vertex and resource contracts.
        /// </summary>
        /// <param name="target">Compiler backend that must accept each declared variant.</param>
        [Theory]
        [InlineData(ShaderCompileTarget.DirectX11)]
        [InlineData(ShaderCompileTarget.Vulkan)]
        public void CompileVariants_WhenSpriteBatchSourceIsCompiled_ReflectsTheVariantSpecificInterface(ShaderCompileTarget target) {
            Assert.Collection(
                SpriteBatchShaderVariants.All,
                variant => {
                    Assert.Equal("Textured", variant.Name);
                    Assert.Equal("VS", variant.VertexEntryPoint);
                    Assert.Equal("PS", variant.PixelEntryPoint);
                    Assert.Empty(variant.Defines);
                },
                variant => {
                    Assert.Equal("RoundedShape", variant.Name);
                    Assert.Equal("VS", variant.VertexEntryPoint);
                    Assert.Equal("PS", variant.PixelEntryPoint);
                    Assert.Equal(new[] { "HELENGINE_BATCH_ROUNDED=1" }, variant.Defines);
                });

            ShaderBackendRegistry backendRegistry = new ShaderBackendRegistry();
            backendRegistry.Register(new DirectX11ShaderBackend());
            backendRegistry.Register(new VulkanShaderBackend());
            string shaderDirectory = Path.GetDirectoryName(SourceInfoValue.Path);
            ShaderCompileService compileService = backendRegistry.CreateCompileService(
                new ShaderFilesystemIncludeResolver(shaderDirectory),
                new ShaderMemoryCompileCache(),
                new ShaderSourceHasher());

            List<ShaderCompileResult> vertexResults = new List<ShaderCompileResult>(SpriteBatchShaderVariants.All.Count);
            List<ShaderCompileResult> pixelResults = new List<ShaderCompileResult>(SpriteBatchShaderVariants.All.Count);
            for (int variantIndex = 0; variantIndex < SpriteBatchShaderVariants.All.Count; variantIndex++) {
                StandardShaderVariant variant = SpriteBatchShaderVariants.All[variantIndex];
                vertexResults.Add(CompileStage(compileService, target, variant, ShaderStage.Vertex, variant.VertexEntryPoint));
                pixelResults.Add(CompileStage(compileService, target, variant, ShaderStage.Pixel, variant.PixelEntryPoint));
            }

            for (int variantIndex = 0; variantIndex < SpriteBatchShaderVariants.All.Count; variantIndex++) {
                StandardShaderVariant variant = SpriteBatchShaderVariants.All[variantIndex];
                ShaderCompileResult vertexResult = vertexResults[variantIndex];
                ShaderCompileResult pixelResult = pixelResults[variantIndex];
                Assert.True(vertexResult.Success);
                Assert.True(pixelResult.Success);
                Assert.Equal(variant.Name, vertexResult.Request.Variant);
                Assert.Equal(variant.Name, pixelResult.Request.Variant);
                Assert.Equal("SpriteBatchShader.vs", vertexResult.Request.ProgramName);
                Assert.Equal("SpriteBatchShader.ps", pixelResult.Request.ProgramName);
                Assert.Equal(variant.VertexEntryPoint, vertexResult.Request.EntryPoint);
                Assert.Equal(variant.PixelEntryPoint, pixelResult.Request.EntryPoint);
                Assert.Equal(ShaderStage.Vertex, vertexResult.Request.Stage);
                Assert.Equal(ShaderStage.Pixel, pixelResult.Request.Stage);
                Assert.Equal(target, vertexResult.Request.Target);
                Assert.Equal(target, pixelResult.Request.Target);
                Assert.NotEmpty(vertexResult.Binary.Bytecode);
                Assert.NotEmpty(pixelResult.Binary.Bytecode);
                if (target == ShaderCompileTarget.Vulkan) {
                    Assert.Equal(0x07230203u, BitConverter.ToUInt32(vertexResult.Binary.Bytecode, 0));
                    Assert.Equal(0x07230203u, BitConverter.ToUInt32(pixelResult.Binary.Bytecode, 0));
                }
                ExportBytecodeIfRequested(target, variant.Name, ShaderStage.Vertex, vertexResult.Binary.Bytecode);
                ExportBytecodeIfRequested(target, variant.Name, ShaderStage.Pixel, pixelResult.Binary.Bytecode);

                if (target == ShaderCompileTarget.DirectX11) {
                    AssertDirectX11VertexReflection(vertexResult.Binary.Bytecode, variant.Name);
                    AssertDirectX11PixelReflection(pixelResult.Binary.Bytecode, variant.Name);
                }
            }
        }

        /// <summary>
        /// Compiles one source entry point with the exact name and defines from its catalog record.
        /// </summary>
        /// <param name="compileService">Real compiler service with DirectX11 and Vulkan backends registered.</param>
        /// <param name="target">Backend target selected for this compile request.</param>
        /// <param name="variant">Catalog variant whose entry point and defines are applied.</param>
        /// <param name="stage">Pipeline stage to compile.</param>
        /// <param name="entryPoint">Entry point required for the selected stage.</param>
        /// <returns>Successful compiler result including reflected program metadata.</returns>
        ShaderCompileResult CompileStage(
            ShaderCompileService compileService,
            ShaderCompileTarget target,
            StandardShaderVariant variant,
            ShaderStage stage,
            string entryPoint) {
            string programName = stage == ShaderStage.Vertex ? "SpriteBatchShader.vs" : "SpriteBatchShader.ps";
            ShaderDefine[] defines = new ShaderDefine[variant.Defines.Count];
            for (int index = 0; index < variant.Defines.Count; index++) {
                string define = variant.Defines[index];
                int separatorIndex = define.IndexOf('=');
                defines[index] = separatorIndex < 0
                    ? new ShaderDefine(define, "1")
                    : new ShaderDefine(define.Substring(0, separatorIndex), define.Substring(separatorIndex + 1));
            }

            ShaderCompileRequest request = new ShaderCompileRequest(
                SourceInfoValue,
                programName,
                entryPoint,
                stage,
                target,
                new ShaderModel(4, 0),
                variant.Name,
                defines,
                new ShaderCompileOptions(ShaderBindingPolicies.Default, true, false, false));
            return compileService.Compile(request);
        }

        /// <summary>
        /// Verifies the active vertex signature and camera matrix by reflecting actual DirectX11 bytecode.
        /// </summary>
        /// <param name="bytecode">Compiled DirectX11 vertex bytecode returned by the real backend.</param>
        /// <param name="variantName">Variant whose reflected interface is expected.</param>
        static void AssertDirectX11VertexReflection(byte[] bytecode, string variantName) {
            using (ShaderReflection reflection = new ShaderReflection(bytecode)) {
                ShaderDescription shaderDescription = reflection.Description;
                Assert.Equal(6, shaderDescription.InputParameters);

                List<ShaderParameterDescription> inputParameters = new List<ShaderParameterDescription>(shaderDescription.InputParameters);
                for (int index = 0; index < shaderDescription.InputParameters; index++) {
                    inputParameters.Add(reflection.GetInputParameterDescription(index));
                }

                AssertDirectX11Input(inputParameters, "POSITION", 0, 15);
                AssertDirectX11Input(inputParameters, "TEXCOORD", 0, variantName == "Textured" ? (byte)3 : (byte)12);
                AssertDirectX11Input(inputParameters, "COLOR", 0, 15);
                if (variantName == "RoundedShape") {
                    AssertDirectX11Input(inputParameters, "TEXCOORD", 1, 15);
                    AssertDirectX11Input(inputParameters, "TEXCOORD", 2, 15);
                    AssertDirectX11Input(inputParameters, "COLOR", 1, 15);
                } else {
                    AssertDirectX11Input(inputParameters, "TEXCOORD", 1, 0);
                    AssertDirectX11Input(inputParameters, "TEXCOORD", 2, 0);
                    AssertDirectX11Input(inputParameters, "COLOR", 1, 0);
                }

                InputBindingDescription cameraBinding = reflection.GetResourceBindingDescription("BatchCameraBuffer");
                Assert.Equal("BatchCameraBuffer", cameraBinding.Name);
                Assert.Equal(ShaderInputType.ConstantBuffer, cameraBinding.Type);
                ConstantBuffer cameraBuffer = reflection.GetConstantBuffer("BatchCameraBuffer");
                ConstantBufferDescription cameraDescription = cameraBuffer.Description;
                Assert.Equal(64, cameraDescription.Size);
                Assert.Equal(1, cameraDescription.VariableCount);
                ShaderReflectionVariable projection = cameraBuffer.GetVariable("projection");
                Assert.Equal("projection", projection.Description.Name);
                ShaderTypeDescription projectionType = projection.GetVariableType().Description;
                Assert.Equal(4, projectionType.RowCount);
                Assert.Equal(4, projectionType.ColumnCount);
            }
        }

        /// <summary>
        /// Verifies resources retained in actual DirectX11 pixel bytecode after variant optimization.
        /// </summary>
        /// <param name="bytecode">Compiled DirectX11 pixel bytecode returned by the real backend.</param>
        /// <param name="variantName">Variant whose optimized resource set is expected.</param>
        static void AssertDirectX11PixelReflection(byte[] bytecode, string variantName) {
            using (ShaderReflection reflection = new ShaderReflection(bytecode)) {
                ShaderDescription shaderDescription = reflection.Description;
                if (variantName == "Textured") {
                    Assert.Equal(2, shaderDescription.BoundResources);
                    InputBindingDescription texture = reflection.GetResourceBindingDescription("BatchTexture");
                    InputBindingDescription sampler = reflection.GetResourceBindingDescription("BatchSampler");
                    Assert.Equal("BatchTexture", texture.Name);
                    Assert.Equal(ShaderInputType.Texture, texture.Type);
                    Assert.Equal(SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D, texture.Dimension);
                    Assert.Equal("BatchSampler", sampler.Name);
                    Assert.Equal(ShaderInputType.Sampler, sampler.Type);
                } else {
                    Assert.Equal(0, shaderDescription.BoundResources);
                }
            }
        }

        /// <summary>
        /// Finds one required semantic and index in a reflected DirectX11 vertex signature.
        /// </summary>
        /// <param name="inputParameters">Active input signature entries returned by DirectX shader reflection.</param>
        /// <param name="semanticName">Expected semantic name.</param>
        /// <param name="semanticIndex">Expected semantic index.</param>
        static void AssertDirectX11Input(
            List<ShaderParameterDescription> inputParameters,
            string semanticName,
            int semanticIndex,
            byte expectedReadMask) {
            ShaderParameterDescription input = Assert.Single(
                inputParameters,
                parameter => parameter.SemanticName == semanticName && parameter.SemanticIndex == semanticIndex);
            Assert.Equal(RegisterComponentType.Float32, input.ComponentType);
            Assert.Equal((byte)15, (byte)input.UsageMask);
            Assert.Equal(expectedReadMask, (byte)input.ReadWriteMask);
        }

        /// <summary>
        /// Writes one compiled stage only when the opt-in bytecode export path is configured by the test runner.
        /// </summary>
        /// <param name="target">Compiler target that produced the stage.</param>
        /// <param name="variantName">Stable variant name recorded in the export filename.</param>
        /// <param name="stage">Pipeline stage recorded in the export filename.</param>
        /// <param name="bytecode">Compiled stage bytecode to retain.</param>
        static void ExportBytecodeIfRequested(ShaderCompileTarget target, string variantName, ShaderStage stage, byte[] bytecode) {
            string exportDirectory = Environment.GetEnvironmentVariable(BytecodeExportDirectoryEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(exportDirectory)) {
                return;
            }

            Directory.CreateDirectory(exportDirectory);
            string fileName = string.Concat("SpriteBatchShader.", target, ".", variantName, ".", stage, ".bin");
            File.WriteAllBytes(Path.Combine(exportDirectory, fileName), bytecode);
        }

        /// <summary>
        /// Checks representative rounded-shape coverage values against hand-derived expectations from the shader contract.
        /// </summary>
        [Fact]
        public void RoundedShapeCoverageOracle_WhenEvaluatingContractCases_ReturnsExpectedCoverage() {
            Assert.Equal(1.0, EvaluateRoundedCoverage(0.0, 0.0, 10.0, 10.0, 4.0, 0.0, true, true, true, true, 1.0, 1.0), 6);
            Assert.Equal(0.0, EvaluateRoundedCoverage(10.5, 0.0, 10.0, 10.0, 0.0, 0.0, false, false, false, false, 1.0, 1.0), 6);
            Assert.Equal(0.5, EvaluateRoundedCoverage(10.0, 0.0, 10.0, 10.0, 0.0, 0.0, false, false, false, false, 1.0, 1.0), 6);
            Assert.Equal(1.0, EvaluateRoundedCoverage(9.0, 0.0, 10.0, 10.0, 0.0, 2.0, false, false, false, false, 1.0, 1.0), 6);

            double selectedTopLeft = EvaluateRoundedCoverage(-9.0, -9.0, 10.0, 10.0, 4.0, 0.0, true, false, false, false, 1.0, 1.0);
            double unroundedTopRight = EvaluateRoundedCoverage(9.0, -9.0, 10.0, 10.0, 4.0, 0.0, true, false, false, false, 1.0, 1.0);
            Assert.InRange(selectedTopLeft, 0.0, 0.5);
            Assert.Equal(1.0, unroundedTopRight, 6);
        }

        /// <summary>
        /// Computes one CPU oracle coverage using the rounded distance and disjoint fill/border alpha equations.
        /// </summary>
        /// <param name="localX">Pixel center horizontal coordinate relative to shape center.</param>
        /// <param name="localY">Pixel center vertical coordinate relative to shape center.</param>
        /// <param name="halfWidth">Shape half-width in pixels.</param>
        /// <param name="halfHeight">Shape half-height in pixels.</param>
        /// <param name="radius">Requested rounded corner radius.</param>
        /// <param name="borderWidth">Border width in pixels.</param>
        /// <param name="topLeft">Whether the top-left corner is rounded.</param>
        /// <param name="topRight">Whether the top-right corner is rounded.</param>
        /// <param name="bottomLeft">Whether the bottom-left corner is rounded.</param>
        /// <param name="bottomRight">Whether the bottom-right corner is rounded.</param>
        /// <param name="fillAlpha">Fill color alpha.</param>
        /// <param name="borderAlpha">Border color alpha.</param>
        /// <returns>Composited shape coverage alpha.</returns>
        static double EvaluateRoundedCoverage(
            double localX,
            double localY,
            double halfWidth,
            double halfHeight,
            double radius,
            double borderWidth,
            bool topLeft,
            bool topRight,
            bool bottomLeft,
            bool bottomRight,
            double fillAlpha,
            double borderAlpha) {
            double outerDistance = RoundedDistance(localX, localY, halfWidth, halfHeight, radius, topLeft, topRight, bottomLeft, bottomRight);
            double outer = 1.0 - SmoothStep(-0.5, 0.5, outerDistance);
            double inner = outer;
            if (borderWidth > 0.0) {
                if (borderWidth >= Math.Min(halfWidth, halfHeight)) {
                    inner = 0.0;
                } else {
                    double innerDistance = RoundedDistance(
                        localX,
                        localY,
                        halfWidth - borderWidth,
                        halfHeight - borderWidth,
                        Math.Max(radius - borderWidth, 0.0),
                        topLeft,
                        topRight,
                        bottomLeft,
                        bottomRight);
                    inner = Math.Min(outer, 1.0 - SmoothStep(-0.5, 0.5, innerDistance));
                }
            }

            double fillCoverage = fillAlpha * inner;
            double borderCoverage = borderAlpha * Math.Max(outer - inner, 0.0);
            return fillCoverage + borderCoverage;
        }

        /// <summary>
        /// Computes the selected-corner rounded-box signed distance in pixel coordinates.
        /// </summary>
        /// <param name="localX">Pixel center horizontal coordinate relative to shape center.</param>
        /// <param name="localY">Pixel center vertical coordinate relative to shape center.</param>
        /// <param name="halfWidth">Shape half-width in pixels.</param>
        /// <param name="halfHeight">Shape half-height in pixels.</param>
        /// <param name="radius">Requested rounded corner radius.</param>
        /// <param name="topLeft">Whether the top-left corner is rounded.</param>
        /// <param name="topRight">Whether the top-right corner is rounded.</param>
        /// <param name="bottomLeft">Whether the bottom-left corner is rounded.</param>
        /// <param name="bottomRight">Whether the bottom-right corner is rounded.</param>
        /// <returns>Signed distance from the selected-corner rounded rectangle.</returns>
        static double RoundedDistance(
            double localX,
            double localY,
            double halfWidth,
            double halfHeight,
            double radius,
            bool topLeft,
            bool topRight,
            bool bottomLeft,
            bool bottomRight) {
            bool isTop = localY < 0.0;
            bool isLeft = localX < 0.0;
            bool roundedCorner = isTop
                ? (isLeft ? topLeft : topRight)
                : (isLeft ? bottomLeft : bottomRight);
            double cornerRadius = roundedCorner ? Math.Min(Math.Max(radius, 0.0), Math.Min(halfWidth, halfHeight)) : 0.0;
            double distanceX = Math.Abs(localX) - halfWidth + cornerRadius;
            double distanceY = Math.Abs(localY) - halfHeight + cornerRadius;
            double outsideX = Math.Max(distanceX, 0.0);
            double outsideY = Math.Max(distanceY, 0.0);
            double outsideDistance = Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
            return outsideDistance + Math.Min(Math.Max(distanceX, distanceY), 0.0) - cornerRadius;
        }

        /// <summary>
        /// Evaluates the cubic interpolation used by HLSL smoothstep for the CPU coverage oracle.
        /// </summary>
        /// <param name="edge0">Lower edge of the transition.</param>
        /// <param name="edge1">Upper edge of the transition.</param>
        /// <param name="value">Signed distance to interpolate.</param>
        /// <returns>Clamped cubic interpolation value.</returns>
        static double SmoothStep(double edge0, double edge1, double value) {
            double amount = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
            return amount * amount * (3.0 - 2.0 * amount);
        }
    }
}
