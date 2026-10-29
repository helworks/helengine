namespace helengine.editor.tests.testing;

/// <summary>Supplies deterministic shader metadata for packaging tests that exercise asset closure without executing GPU programs.</summary>
public sealed class PackagingTestShaderBackend : IShaderBackend {
    /// <summary>Targets the raw shader material policy used by the packaging fixture.</summary>
    public ShaderCompileTarget Target => ShaderCompileTarget.DirectX11;

    /// <summary>Accepts the vertex and pixel stages requested by the built-in shader library.</summary>
    public ShaderBackendCapabilities Capabilities { get; } = new(new ShaderModel(4, 0), new ShaderModel(4, 0), [ShaderStage.Vertex, ShaderStage.Pixel], false);

    /// <summary>Returns a nonempty inert binary and the requested program identity so packaging can serialize its shader dependency.</summary>
    /// <param name="request">Program selection to preserve in the packaged shader metadata.</param>
    /// <param name="includeResolver">Unused source resolver because this fixture does not compile GPU instructions.</param>
    /// <returns>Successful shader metadata suitable only for asset packaging tests.</returns>
    public ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver) {
        ShaderProgramDefinition program = new(request.ProgramName, request.Stage, request.EntryPoint, [], [], [], []);
        ShaderCompiledBinary binary = new(request.Target, request.Stage, request.EntryPoint, request.Variant, [1]);
        return new ShaderCompileResult(request, program, binary, [], true);
    }
}
