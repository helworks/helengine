namespace helengine {
    /// <summary>
    /// Declares one typed effect parameter and where its value lands in the shared constant buffer.
    /// </summary>
    public class EffectParameterAsset : IDisposable {
        /// <summary>
        /// Gets or sets the parameter name compositions and command lines use.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the explanation shown in help text and editors.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value shape, which also decides how many constant slots the parameter occupies.
        /// </summary>
        public EffectParameterType Type { get; set; } = EffectParameterType.Float;

        /// <summary>
        /// Gets or sets the default value; scalar types use X and enums store the default index in X.
        /// </summary>
        public float4 DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets the inclusive lower bound applied to every numeric component.
        /// </summary>
        public float Minimum { get; set; } = -1000000f;

        /// <summary>
        /// Gets or sets the inclusive upper bound applied to every numeric component.
        /// </summary>
        public float Maximum { get; set; } = 1000000f;

        /// <summary>
        /// Gets or sets the first constant slot (0 to 15) the value is written to.
        /// </summary>
        public int Slot { get; set; }

        /// <summary>
        /// Gets or sets the allowed names of an <see cref="EffectParameterType.Enum"/> parameter, indexed from zero.
        /// </summary>
        public string[] AllowedValues { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Releases the owned allowed-value array.
        /// </summary>
        public void Dispose() {
            string[] allowed = AllowedValues;
            AllowedValues = null;
            AnimationClipAsset.DeleteOwnedArray(allowed);
        }
    }
}
