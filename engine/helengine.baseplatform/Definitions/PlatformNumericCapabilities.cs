namespace helengine.baseplatform.Definitions {
    /// <summary>Publishes only the numeric policy combinations implemented by one backend.</summary>
    public sealed class PlatformNumericCapabilities {
        /// <summary>Creates a capability snapshot whose default is supported by the backend.</summary>
        /// <param name="defaultPolicy">Explicit policy used when build options omit numeric widths.</param>
        /// <param name="supportedPolicies">Implemented combinations, defensively copied before publication.</param>
        public PlatformNumericCapabilities(NumericPrecisionPolicy defaultPolicy, NumericPrecisionPolicy[] supportedPolicies) {
            if (defaultPolicy == null) { throw new ArgumentNullException(nameof(defaultPolicy)); }
            else if (supportedPolicies == null) { throw new ArgumentNullException(nameof(supportedPolicies)); }
            else if (supportedPolicies.Length == 0) { throw new ArgumentException("At least one numeric policy must be supported.", nameof(supportedPolicies)); }
            HashSet<string> identities = new(StringComparer.Ordinal);
            foreach (NumericPrecisionPolicy policy in supportedPolicies) {
                if (policy == null) { throw new ArgumentException("Supported numeric policies cannot contain null.", nameof(supportedPolicies)); }
                else if (!identities.Add(policy.Identity)) { throw new ArgumentException("Supported numeric policies cannot contain duplicates.", nameof(supportedPolicies)); }
            }
            if (!identities.Contains(defaultPolicy.Identity)) { throw new ArgumentException("The default numeric policy must be supported.", nameof(defaultPolicy)); }
            DefaultPolicy = defaultPolicy;
            SupportedPolicies = Array.AsReadOnly((NumericPrecisionPolicy[])supportedPolicies.Clone());
        }

        /// <summary>Gets the explicit single-precision contract used by existing profile definitions.</summary>
        public static PlatformNumericCapabilities SinglePrecision { get; } = new(NumericPrecisionPolicy.Default, [NumericPrecisionPolicy.Default]);

        /// <summary>Gets the immutable numeric policy used when a build omits an override.</summary>
        public NumericPrecisionPolicy DefaultPolicy { get; }

        /// <summary>Gets a read-only snapshot of the implemented policy combinations.</summary>
        public IReadOnlyList<NumericPrecisionPolicy> SupportedPolicies { get; }

        /// <summary>Creates build-selectable widths from the implemented combinations without advertising new pairs.</summary>
        /// <returns>Storage and evaluation setting definitions for the existing codegen profile UI.</returns>
        public PlatformSettingDefinition[] CreateSettings() {
            string[] storage = SupportedPolicies.Select(policy => policy.StorageBits).Distinct().OrderBy(bits => bits)
                .Select(bits => bits.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            string[] arithmetic = SupportedPolicies.Select(policy => policy.ArithmeticBits).Distinct().OrderBy(bits => bits)
                .Select(bits => bits.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return [
                new PlatformSettingDefinition(PlatformCodegenSettingIds.RealStorageBits, "Numeric storage bits", PlatformSettingKind.Choice,
                    DefaultPolicy.StorageBits.ToString(System.Globalization.CultureInfo.InvariantCulture), true, storage),
                new PlatformSettingDefinition(PlatformCodegenSettingIds.RealArithmeticBits, "Numeric arithmetic bits", PlatformSettingKind.Choice,
                    DefaultPolicy.ArithmeticBits.ToString(System.Globalization.CultureInfo.InvariantCulture), true, arithmetic)
            ];
        }

        /// <summary>Rejects a numeric combination before the backend consumes precision-dependent data.</summary>
        /// <param name="policy">Resolved policy to validate against this capability snapshot.</param>
        public void RequireSupported(NumericPrecisionPolicy policy) {
            if (policy == null) { throw new ArgumentNullException(nameof(policy)); }
            foreach (NumericPrecisionPolicy supported in SupportedPolicies) {
                if (supported.Identity == policy.Identity) { return; }
            }
            throw new NotSupportedException($"The backend does not support numeric policy '{policy.Identity}'.");
        }
    }
}
