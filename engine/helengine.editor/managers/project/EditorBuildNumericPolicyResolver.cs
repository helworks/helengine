using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>Captures numeric build selections independently of mutable platform preferences.</summary>
    public static class EditorBuildNumericPolicyResolver {
        /// <summary>Validates the queued snapshot and supplies exactly that policy to each execution phase.</summary>
        /// <param name="queueItem">Queued build, including older documents without a numeric snapshot.</param>
        /// <param name="codegenProfile">Backend profile whose supported pairs must still include the snapshot.</param>
        /// <param name="effectiveOptions">Options forwarded to cook, Core generation and authored modules.</param>
        public static void Apply(EditorBuildQueueItemDocument queueItem, PlatformCodegenProfileDefinition codegenProfile,
            Dictionary<string, string> effectiveOptions) {
            if (queueItem == null) { throw new ArgumentNullException(nameof(queueItem)); }
            else if (effectiveOptions == null) { throw new ArgumentNullException(nameof(effectiveOptions)); }
            PlatformNumericCapabilities capabilities = codegenProfile == null
                ? PlatformNumericCapabilities.SinglePrecision : codegenProfile.NumericCapabilities;
            NumericPrecisionPolicy policy = queueItem.NumericPolicy ?? Capture(effectiveOptions, codegenProfile);
            capabilities.RequireSupported(policy);
            queueItem.NumericPolicy = policy;
            PlatformNumericPolicyResolver.ApplyToOptions(policy, effectiveOptions);
        }

        /// <summary>Captures a validated immutable snapshot using the selected backend's explicit defaults.</summary>
        /// <param name="selectedOptions">Current build-specific option values.</param>
        /// <param name="codegenProfile">Selected codegen profile, or null when no native profile exists.</param>
        /// <returns>Numeric policy detached from subsequent preference edits.</returns>
        public static NumericPrecisionPolicy Capture(IReadOnlyDictionary<string, string> selectedOptions, PlatformCodegenProfileDefinition codegenProfile) {
            PlatformNumericCapabilities capabilities = codegenProfile == null
                ? PlatformNumericCapabilities.SinglePrecision : codegenProfile.NumericCapabilities;
            return PlatformNumericPolicyResolver.Resolve(selectedOptions, capabilities);
        }
    }
}
