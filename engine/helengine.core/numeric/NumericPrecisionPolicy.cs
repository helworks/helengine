namespace helengine {
    /// <summary>Immutable numeric representation selected for one build, independent of its target platform.</summary>
    public sealed class NumericPrecisionPolicy {
        /// <summary>Current representation contract shared by cooked data and generated runtime code.</summary>
        public const int CurrentRepresentationVersion = 1;

        /// <summary>Creates a validated storage/arithmetic pair without claiming backend availability.</summary>
        /// <param name="storageBits">IEEE width used at configurable stored destinations.</param>
        /// <param name="arithmeticBits">IEEE width used for configurable expression evaluation.</param>
        /// <param name="representationVersion">Serialized contract version; unsupported versions are rejected.</param>
        public NumericPrecisionPolicy(int storageBits, int arithmeticBits, int representationVersion = CurrentRepresentationVersion) {
            if (storageBits != 16 && storageBits != 32 && storageBits != 64) {
                throw new ArgumentOutOfRangeException(nameof(storageBits), "Numeric storage width must be 16, 32 or 64 bits.");
            } else if (arithmeticBits != 16 && arithmeticBits != 32 && arithmeticBits != 64) {
                throw new ArgumentOutOfRangeException(nameof(arithmeticBits), "Numeric arithmetic width must be 16, 32 or 64 bits.");
            } else if (representationVersion != CurrentRepresentationVersion) {
                throw new ArgumentOutOfRangeException(nameof(representationVersion), "Unsupported numeric representation version.");
            }
            StorageBits = storageBits;
            ArithmeticBits = arithmeticBits;
            RepresentationVersion = representationVersion;
        }

        /// <summary>Gets the documented default when a build has no explicit numeric selection.</summary>
        public static NumericPrecisionPolicy Default { get; } = new(32, 32);

        /// <summary>Gets the IEEE width retained in configurable runtime fields and cooked values.</summary>
        public int StorageBits { get; }

        /// <summary>Gets the IEEE width used to evaluate configurable arithmetic operations.</summary>
        public int ArithmeticBits { get; }

        /// <summary>Gets the validated representation contract version.</summary>
        public int RepresentationVersion { get; }

        /// <summary>Gets a canonical identity usable by cook, code generation and module ABI keys.</summary>
        public string Identity => $"real-v{RepresentationVersion}-s{StorageBits}-a{ArithmeticBits}";
    }
}
