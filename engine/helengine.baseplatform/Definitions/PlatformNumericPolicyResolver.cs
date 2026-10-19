using System.Globalization;

namespace helengine.baseplatform.Definitions {
    /// <summary>Resolves one independent build policy from selected options and backend capabilities.</summary>
    public static class PlatformNumericPolicyResolver {
        /// <summary>Resolves explicit widths or backend defaults and verifies the exact pair is implemented.</summary>
        /// <param name="optionValues">One build's selected numeric/codegen options.</param>
        /// <param name="capabilities">Backend support and its explicit default.</param>
        /// <returns>Immutable policy detached from the mutable selected-options dictionary.</returns>
        public static NumericPrecisionPolicy Resolve(IReadOnlyDictionary<string, string> optionValues, PlatformNumericCapabilities capabilities) {
            if (optionValues == null) { throw new ArgumentNullException(nameof(optionValues)); }
            else if (capabilities == null) { throw new ArgumentNullException(nameof(capabilities)); }
            NumericPrecisionPolicy policy = Read(optionValues, capabilities.DefaultPolicy);
            capabilities.RequireSupported(policy);
            return policy;
        }

        /// <summary>Reads the selected pair before a caller with backend metadata checks its availability.</summary>
        /// <param name="optionValues">Selected build options, copied independently by request owners.</param>
        /// <param name="defaultPolicy">Explicit default used only for absent numeric options.</param>
        /// <returns>Validated immutable representation choice without implying backend support.</returns>
        public static NumericPrecisionPolicy Read(IReadOnlyDictionary<string, string> optionValues, NumericPrecisionPolicy defaultPolicy) {
            if (optionValues == null) { throw new ArgumentNullException(nameof(optionValues)); }
            else if (defaultPolicy == null) { throw new ArgumentNullException(nameof(defaultPolicy)); }
            return new NumericPrecisionPolicy(
                ReadWidth(optionValues, PlatformCodegenSettingIds.RealStorageBits, defaultPolicy.StorageBits),
                ReadWidth(optionValues, PlatformCodegenSettingIds.RealArithmeticBits, defaultPolicy.ArithmeticBits));
        }

        /// <summary>Writes canonical native options from the authoritative immutable build snapshot.</summary>
        /// <param name="policy">Numeric policy captured for the build.</param>
        /// <param name="optionValues">Effective options passed to generation and native compilation.</param>
        public static void ApplyToOptions(NumericPrecisionPolicy policy, IDictionary<string, string> optionValues) {
            if (policy == null) { throw new ArgumentNullException(nameof(policy)); }
            else if (optionValues == null) { throw new ArgumentNullException(nameof(optionValues)); }
            string[] alternateKeys = optionValues.Keys.Where(key =>
                string.Equals(key, PlatformCodegenSettingIds.RealStorageBits, StringComparison.OrdinalIgnoreCase) && key != PlatformCodegenSettingIds.RealStorageBits
                || string.Equals(key, PlatformCodegenSettingIds.RealArithmeticBits, StringComparison.OrdinalIgnoreCase) && key != PlatformCodegenSettingIds.RealArithmeticBits).ToArray();
            foreach (string key in alternateKeys) { optionValues.Remove(key); }
            optionValues[PlatformCodegenSettingIds.RealStorageBits] = policy.StorageBits.ToString(CultureInfo.InvariantCulture);
            optionValues[PlatformCodegenSettingIds.RealArithmeticBits] = policy.ArithmeticBits.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reads one width without allowing malformed or ambiguously cased persisted values.</summary>
        /// <param name="options">One build's selected options.</param>
        /// <param name="key">Canonical setting identifier.</param>
        /// <param name="defaultValue">Backend value used only when this setting is absent.</param>
        /// <returns>The selected width, or the backend default when no override exists.</returns>
        static int ReadWidth(IReadOnlyDictionary<string, string> options, string key, int defaultValue) {
            bool found = false;
            int result = defaultValue;
            foreach (KeyValuePair<string, string> option in options) {
                if (!string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (found) { throw new ArgumentException($"Numeric setting '{key}' is specified more than once.", nameof(options)); }
                else if (option.Value == null || !int.TryParse(option.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result)
                    || result != 16 && result != 32 && result != 64) {
                    throw new ArgumentException($"Numeric setting '{key}' must be 16, 32 or 64.", nameof(options));
                }
                found = true;
            }
            return result;
        }
    }
}
