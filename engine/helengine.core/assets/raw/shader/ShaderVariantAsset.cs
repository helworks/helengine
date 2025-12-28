namespace helengine {
    /// <summary>
    /// Represents serialized data for a shader compile variant.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class ShaderVariantAsset {
        /// <summary>
        /// Variant identifier used for selection.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string Name;

        /// <summary>
        /// Define list used for the variant.
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public string[] Defines;

        /// <summary>
        /// Builds a runtime variant definition from serialized data.
        /// </summary>
        /// <returns>Variant definition.</returns>
        public ShaderVariant ToVariant() {
            Validate();
            return new ShaderVariant(Name, Defines);
        }

        /// <summary>
        /// Creates a serialized variant asset from a runtime definition.
        /// </summary>
        /// <param name="variant">Variant definition to convert.</param>
        /// <returns>Serialized variant asset.</returns>
        public static ShaderVariantAsset FromVariant(ShaderVariant variant) {
            if (variant == null) {
                throw new ArgumentNullException(nameof(variant));
            }

            string[] defines = BuildDefines(variant);
            ShaderVariantAsset asset = new ShaderVariantAsset {
                Name = variant.Name,
                Defines = defines
            };

            return asset;
        }

        /// <summary>
        /// Validates variant data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new InvalidOperationException("Variant name must be provided.");
            }

            if (Defines == null) {
                throw new InvalidOperationException("Variant defines must be provided.");
            }
        }

        /// <summary>
        /// Copies define strings from a runtime variant definition.
        /// </summary>
        /// <param name="variant">Variant definition to read.</param>
        /// <returns>Copied define array.</returns>
        static string[] BuildDefines(ShaderVariant variant) {
            IReadOnlyList<string> defines = variant.Defines;
            string[] result = new string[defines.Count];
            for (int i = 0; i < defines.Count; i++) {
                result[i] = defines[i];
            }

            return result;
        }
    }
}
