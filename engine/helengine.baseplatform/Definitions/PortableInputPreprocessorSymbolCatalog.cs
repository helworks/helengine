namespace helengine.baseplatform.Definitions;

/// <summary>
/// Publishes stable portable-input preprocessor symbols that platform builders can request for generated-core source selection.
/// </summary>
public static class PortableInputPreprocessorSymbolCatalog {
    /// <summary>
    /// Selects the GameCube/Wii GX matrix ABI branch owned by helengine source.
    /// </summary>
    public const string MatrixAbiGxGameCubeWiiSymbol = "HELENGINE_CODEGEN_MATRIX_ABI_GX_GAMECUBE_WII";
}
