namespace helengine.baseplatform.Player {

    /// <summary>
    /// Lets the desktop C# player load one platform's cooked content. A platform that supports editor Play implements
    /// this adapter and names it through <see cref="Definitions.PlatformPlayerCapability.AdapterTypeName"/>.
    /// The player calls <see cref="Initialize"/> exactly once before any other member.
    /// </summary>
    public interface IPlatformPlayerAdapter {
        /// <summary>
        /// Binds the adapter to the cooked content root produced by an assets-only build. Called once, before any other member.
        /// </summary>
        /// <param name="contentRootPath">Absolute path of the platform-cooked content root.</param>
        void Initialize(string contentRootPath);

        /// <summary>
        /// Registers the platform's content processors so the player's content manager can decode platform-cooked assets.
        /// </summary>
        /// <param name="contentManager">Content manager owned by the player.</param>
        void RegisterContentProcessors(ContentManager contentManager);

        /// <summary>
        /// Decodes one platform-cooked material into the platform-neutral description the player renders with.
        /// </summary>
        /// <param name="cookedMaterial">Stream positioned at the start of the cooked material payload.</param>
        /// <param name="materialRelativePath">Content-root-relative path of the material, used for diagnostics and for resolving sibling assets.</param>
        /// <returns>The decoded material description.</returns>
        PlayerMaterialDescription DecodeMaterial(Stream cookedMaterial, string materialRelativePath);

        /// <summary>
        /// Builds the aliases that map content paths the scene references to the paths the platform actually cooked.
        /// </summary>
        /// <returns>Map from referenced content path to cooked content path.</returns>
        IReadOnlyDictionary<string, string> BuildContentAliases();
    }
}
