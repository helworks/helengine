namespace helengine {
    /// <summary>
    /// Declares one named image an effect samples, such as the layer being processed or an external mask.
    /// </summary>
    public class EffectInputAsset : IDisposable {
        /// <summary>
        /// Gets or sets the role name compositions bind a source to and passes read by name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the bound image must carry a real alpha channel, because the effect keys on it.
        /// </summary>
        public bool RequiresAlpha { get; set; }

        /// <summary>
        /// Initializes an empty input for serializers and object initializers.
        /// </summary>
        public EffectInputAsset() { }

        /// <summary>
        /// Initializes one named input.
        /// </summary>
        /// <param name="name">Role name the input is bound and read by.</param>
        /// <param name="requiresAlpha">Whether the bound image must carry alpha.</param>
        public EffectInputAsset(string name, bool requiresAlpha) {
            Name = name;
            RequiresAlpha = requiresAlpha;
        }

        /// <summary>
        /// Releases nothing; an input owns no nested allocations beyond its name.
        /// </summary>
        public void Dispose() { }
    }
}
