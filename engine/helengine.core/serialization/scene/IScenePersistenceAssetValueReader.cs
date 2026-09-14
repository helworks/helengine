#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
namespace helengine {
    /// <summary>
    /// Decodes the asset-backed member types of one reflected scene-persistence payload on behalf of <see cref="ScenePersistenceValueWalker"/>.
    /// The walk itself owns the deterministic leaf, enum, dictionary, array and nested-object layout, while each host supplies
    /// the asset pipeline that turns the encoded scene asset references back into the values its own runtime expects.
    /// </summary>
    public interface IScenePersistenceAssetValueReader {
        /// <summary>
        /// Attempts to decode one asset-backed member value from the current reader position.
        /// Implementations must consume payload bytes only when they return true so the shared walk can continue with the
        /// remaining generic branches for every member type they decline.
        /// </summary>
        /// <param name="reader">Reader positioned at the member payload.</param>
        /// <param name="valueType">Runtime value type expected for the payload.</param>
        /// <param name="value">Decoded asset-backed value when the member type is handled by this reader.</param>
        /// <returns>True when the member type was consumed as one asset-backed value; otherwise false.</returns>
        bool TryReadAssetValue(EngineBinaryReader reader, Type valueType, out object value);
    }
}
#endif
