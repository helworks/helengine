using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides one minimal builder implementation that cooks Windows material requests into serialized runtime material assets.
    /// </summary>
    public sealed class TestPlatformMaterialAssetBuilder : IPlatformAssetBuilder {
        /// <summary>
        /// Initializes one minimal material-cook builder.
        /// </summary>
        public TestPlatformMaterialAssetBuilder() {
            Descriptor = new PlatformBuilderDescriptor(
                "helengine.editor.tests.material-builder",
                "1.0.0",
                "windows",
                new EngineCompatibilityRange("1.0.0", "999.0.0"),
                new ManifestCompatibilityRange(1, 1),
                ["windows"],
                ["debug"]);
            Definition = new PlatformDefinition(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [])
                ],
                [],
                [
                    new PlatformMaterialSchemaDefinition(
                        "standard-shader",
                        "Standard Shader",
                        ["directx11"],
                        [
                            new PlatformMaterialFieldDefinition("use-custom-shader", "Use Custom Shader", PlatformMaterialFieldKind.Boolean, "false", true, []),
                            new PlatformMaterialFieldDefinition("shader-asset-id", "Shader Asset", PlatformMaterialFieldKind.AssetReference, string.Empty, true, []),
                            new PlatformMaterialFieldDefinition("texture-id", "Texture", PlatformMaterialFieldKind.AssetReference, string.Empty, true, []),
                            new PlatformMaterialFieldDefinition("roughness", "Roughness", PlatformMaterialFieldKind.Number, "1.0", true, []),
                            new PlatformMaterialFieldDefinition("roughness-texture-id", "Roughness Texture", PlatformMaterialFieldKind.AssetReference, string.Empty, true, []),
                            new PlatformMaterialFieldDefinition("casts-shadow", "Casts Shadow", PlatformMaterialFieldKind.Boolean, "true", true, []),
                            new PlatformMaterialFieldDefinition("receives-shadow", "Receives Shadow", PlatformMaterialFieldKind.Boolean, "true", true, []),
                            new PlatformMaterialFieldDefinition("vertex-program", "Vertex Program", PlatformMaterialFieldKind.Text, string.Empty, true, []),
                            new PlatformMaterialFieldDefinition("pixel-program", "Pixel Program", PlatformMaterialFieldKind.Text, string.Empty, true, [])
                        ])
                ],
                [],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default codegen profile",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [])
                ]);
        }

        /// <summary>
        /// Gets the builder descriptor exposed to the editor build graph.
        /// </summary>
        public PlatformBuilderDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the platform definition that exposes the standard-shader material schema.
        /// </summary>
        public PlatformDefinition Definition { get; }

        /// <summary>
        /// Cooks one schema-driven material request into a serialized runtime material payload.
        /// </summary>
        /// <param name="request">Material translation request to process.</param>
        /// <returns>Serialized cooked material payload plus referenced shader dependencies.</returns>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            string shaderAssetId = ReadRequiredField(request.FieldValues, "shader-asset-id");
            string vertexProgram = ReadRequiredField(request.FieldValues, "vertex-program");
            string pixelProgram = ReadRequiredField(request.FieldValues, "pixel-program");
            string variant = ReadRequiredField(request.FieldValues, "variant");
            string textureAssetId = ReadOptionalField(request.FieldValues, "texture-id");
            string roughnessTextureAssetId = ReadOptionalField(request.FieldValues, "roughness-texture-id");
            bool castsShadows = ReadBooleanField(request.FieldValues, "casts-shadow", true);
            bool receivesShadows = ReadBooleanField(request.FieldValues, "receives-shadow", true);

            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                Id = request.MaterialAssetId,
                ShaderAssetId = shaderAssetId,
                VertexProgram = vertexProgram,
                PixelProgram = pixelProgram,
                DiffuseTextureAssetId = textureAssetId,
                RoughnessTextureAssetId = roughnessTextureAssetId,
                CastsShadows = castsShadows,
                ReceivesShadows = receivesShadows,
                Variant = variant,
                RenderState = new MaterialRenderState()
            };

            return new PlatformMaterialCookResult(
                global::helengine.editor.AssetSerializer.SerializeToBytes(materialAsset),
                [shaderAssetId]);
        }

        /// <summary>
        /// Returns a success report without mutating the supplied build request.
        /// </summary>
        /// <param name="request">Build request supplied by the editor build graph.</param>
        /// <param name="progressReporter">Progress reporter supplied by the editor build graph.</param>
        /// <param name="diagnosticReporter">Diagnostic reporter supplied by the editor build graph.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the editor build graph.</param>
        /// <returns>A completed success report.</returns>
        public Task<PlatformBuildReport> BuildAsync(
            PlatformBuildRequest request,
            IPlatformBuildProgressReporter progressReporter,
            IPlatformBuildDiagnosticReporter diagnosticReporter,
            CancellationToken cancellationToken) {
            return Task.FromResult(new PlatformBuildReport(true, [], [], []));
        }

        /// <summary>
        /// Reads one required material field value from the supplied request map.
        /// </summary>
        /// <param name="fieldValues">Field values keyed by field identifier.</param>
        /// <param name="fieldId">Field identifier that must exist and be non-empty.</param>
        /// <returns>Resolved non-empty field value.</returns>
        static string ReadRequiredField(IReadOnlyDictionary<string, string> fieldValues, string fieldId) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            if (!fieldValues.TryGetValue(fieldId, out string value) || string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"Missing required material field '{fieldId}'.");
            }

            return value;
        }

        /// <summary>
        /// Reads one optional material field value from the supplied request map.
        /// </summary>
        /// <param name="fieldValues">Field values keyed by field identifier.</param>
        /// <param name="fieldId">Field identifier that may be absent.</param>
        /// <returns>Resolved field value or an empty string when the field is missing.</returns>
        static string ReadOptionalField(IReadOnlyDictionary<string, string> fieldValues, string fieldId) {
            if (fieldValues == null) {
                return string.Empty;
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            return fieldValues.TryGetValue(fieldId, out string value) ? value ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Reads one optional boolean material field value from the supplied request map.
        /// </summary>
        /// <param name="fieldValues">Field values keyed by field identifier.</param>
        /// <param name="fieldId">Field identifier that may be absent.</param>
        /// <param name="defaultValue">Fallback value used when the field is missing.</param>
        /// <returns>Resolved boolean value.</returns>
        static bool ReadBooleanField(IReadOnlyDictionary<string, string> fieldValues, string fieldId, bool defaultValue) {
            if (fieldValues == null) {
                return defaultValue;
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            if (!fieldValues.TryGetValue(fieldId, out string value) || string.IsNullOrWhiteSpace(value)) {
                return defaultValue;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
