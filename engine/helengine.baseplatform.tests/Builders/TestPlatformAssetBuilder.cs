using helengine;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Provides one minimal builder instance for contract verification.
/// </summary>
public sealed class TestPlatformAssetBuilder : IPlatformAssetBuilder {
    /// <summary>
    /// Stable material field identifier used for the authored base color.
    /// </summary>
    const string BaseColorFieldId = "base-color";

    /// <summary>
    /// Constant-buffer name used for the authored base color payload.
    /// </summary>
    const string BaseColorBufferName = "BaseColorBuffer";

    /// <summary>
    /// Initializes one minimal test builder.
    /// </summary>
    public TestPlatformAssetBuilder() {
        Descriptor = new PlatformBuilderDescriptor(
            "test.builder",
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
                        new PlatformMaterialFieldDefinition(
                            "use-custom-shader",
                            "Use Custom Shader",
                            PlatformMaterialFieldKind.Boolean,
                            "false",
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "shader-asset-id",
                            "Shader Asset",
                            PlatformMaterialFieldKind.AssetReference,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "texture-id",
                            "Texture",
                            PlatformMaterialFieldKind.AssetReference,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "casts-shadow",
                            "Casts Shadow",
                            PlatformMaterialFieldKind.Boolean,
                            "true",
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "receives-shadow",
                            "Receives Shadow",
                            PlatformMaterialFieldKind.Boolean,
                            "true",
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "vertex-program",
                            "Vertex Program",
                            PlatformMaterialFieldKind.Text,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "pixel-program",
                            "Pixel Program",
                            PlatformMaterialFieldKind.Text,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            BaseColorFieldId,
                            "Base Color",
                            PlatformMaterialFieldKind.Color,
                            "#ffffff",
                            false,
                            [])
                    ])
            ],
            [
                new PlatformComponentSupportRule(
                    "helengine.FPSComponent",
                    PlatformComponentSupportKind.PassThrough,
                    "FPS overlay is canonical on this platform.",
                    string.Empty)
            ],
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
    /// Gets the builder descriptor used by the test instance.
    /// </summary>
    public PlatformBuilderDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the typed platform definition used by the test instance.
    /// </summary>
    public PlatformDefinition Definition { get; }

    /// <summary>
    /// Cooks one schema-driven shader material into a serialized runtime material payload.
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
        string textureAssetId = request.FieldValues != null && request.FieldValues.TryGetValue("texture-id", out string textureValue) ? textureValue : string.Empty;
        bool castsShadows = request.FieldValues != null && request.FieldValues.TryGetValue("casts-shadow", out string castsShadowValue) ? string.Equals(castsShadowValue, "true", StringComparison.OrdinalIgnoreCase) : true;
        bool receivesShadows = request.FieldValues != null && request.FieldValues.TryGetValue("receives-shadow", out string receivesShadowValue) ? string.Equals(receivesShadowValue, "true", StringComparison.OrdinalIgnoreCase) : true;
        string baseColor = request.FieldValues != null && request.FieldValues.TryGetValue(BaseColorFieldId, out string baseColorValue) ? baseColorValue : "#ffffff";

        MaterialAsset materialAsset = new MaterialAsset {
            Id = request.MaterialAssetId,
            ShaderAssetId = shaderAssetId,
            VertexProgram = vertexProgram,
            PixelProgram = pixelProgram,
            DiffuseTextureAssetId = textureAssetId ?? string.Empty,
            CastsShadows = castsShadows,
            ReceivesShadows = receivesShadows,
            Variant = variant,
            RenderState = new MaterialRenderState(),
            ConstantBuffers = [
                new MaterialConstantBufferAsset {
                    Name = BaseColorBufferName,
                    Data = CreateFloat4ConstantBufferData(ParseBaseColor(baseColor))
                }
            ]
        };

        return new PlatformMaterialCookResult(global::helengine.editor.AssetSerializer.SerializeToBytes(materialAsset), [shaderAssetId]);
    }

    /// <summary>
    /// Returns a minimal success report without mutating the request.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    /// <param name="diagnosticReporter">The diagnostic reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed success report.</returns>
    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        return Task.FromResult(new PlatformBuildReport(true, [], [], []));
    }

    /// <summary>
    /// Reads one required material field from the builder-owned field map.
    /// </summary>
    /// <param name="fieldValues">Serialized material field values keyed by field id.</param>
    /// <param name="fieldId">Field identifier to read.</param>
    /// <returns>Resolved field value.</returns>
    static string ReadRequiredField(IReadOnlyDictionary<string, string> fieldValues, string fieldId) {
        if (fieldValues == null) {
            throw new ArgumentNullException(nameof(fieldValues));
        } else if (string.IsNullOrWhiteSpace(fieldId)) {
            throw new ArgumentException("Field id must be provided.", nameof(fieldId));
        }

        string value;
        if (!fieldValues.TryGetValue(fieldId, out value) || string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"Missing required material field '{fieldId}'.");
        }

        return value;
    }

    /// <summary>
    /// Parses one serialized base-color string into a normalized floating-point color.
    /// </summary>
    /// <param name="serializedColor">Serialized color string in <c>#RRGGBB</c> or <c>#RRGGBBAA</c> form.</param>
    /// <returns>Normalized color value.</returns>
    static float4 ParseBaseColor(string serializedColor) {
        if (string.IsNullOrWhiteSpace(serializedColor)) {
            return new float4(1f, 1f, 1f, 1f);
        }

        string normalized = serializedColor.Trim();
        if (normalized.StartsWith("#", StringComparison.Ordinal)) {
            normalized = normalized.Substring(1);
        }

        if (normalized.Length != 6 && normalized.Length != 8) {
            throw new InvalidOperationException("Base color must use #RRGGBB or #RRGGBBAA.");
        }

        byte alpha = 255;
        int offset = 0;
        if (normalized.Length == 8) {
            alpha = Convert.ToByte(normalized.Substring(0, 2), 16);
            offset = 2;
        }

        byte red = Convert.ToByte(normalized.Substring(offset, 2), 16);
        byte green = Convert.ToByte(normalized.Substring(offset + 2, 2), 16);
        byte blue = Convert.ToByte(normalized.Substring(offset + 4, 2), 16);

        return new float4(
            red / 255f,
            green / 255f,
            blue / 255f,
            alpha / 255f);
    }

    /// <summary>
    /// Packs one floating-point color into a 16-byte constant-buffer payload.
    /// </summary>
    /// <param name="value">Normalized color value to encode.</param>
    /// <returns>Packed constant-buffer bytes.</returns>
    static byte[] CreateFloat4ConstantBufferData(float4 value) {
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteSingle(value.X);
        writer.WriteSingle(value.Y);
        writer.WriteSingle(value.Z);
        writer.WriteSingle(value.W);
        return stream.ToArray();
    }
}

