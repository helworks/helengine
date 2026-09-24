namespace helengine {
    /// <summary>
    /// Provides the ordered built-in variants used to compile the shared 2D sprite batch shader.
    /// </summary>
    public static class SpriteBatchShaderVariants {
        /// <summary>
        /// Stores the immutable ordered sprite batch variant definitions.
        /// </summary>
        static readonly List<StandardShaderVariant> AllValue = [
            new StandardShaderVariant("Textured", "VS", "PS", []),
            new StandardShaderVariant("RoundedShape", "VS", "PS", ["HELENGINE_BATCH_ROUNDED=1"])
        ];

        /// <summary>
        /// Gets every sprite batch variant in deterministic compile and bundle order.
        /// </summary>
        public static IReadOnlyList<StandardShaderVariant> All => AllValue;
    }
}
