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
                            "shader-asset-id",
                            "Shader Asset",
                            PlatformMaterialFieldKind.AssetReference,
                            string.Empty,
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
                            "variant",
                            "Variant",
                            PlatformMaterialFieldKind.Choice,
                            "default",
                            true,
                            ["default", "skinned"])
                    ])
            ],
            [
                new PlatformComponentCompatibilityDefinition(
                    "helengine.FPSComponent",
                    PlatformComponentCompatibilityKind.PassThrough,
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

        MaterialAsset materialAsset = new MaterialAsset {
            Id = request.MaterialAssetId,
            ShaderAssetId = shaderAssetId,
            VertexProgram = vertexProgram,
            PixelProgram = pixelProgram,
            Variant = variant,
            RenderState = new MaterialRenderState(),
            ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
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
}
