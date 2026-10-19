using helengine;
using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>Verifies immutable numeric build policy, capability validation and option resolution.</summary>
    public sealed class PlatformNumericPolicyTests {
        /// <summary>Existing selections resolve to the documented 32-bit storage and arithmetic policy.</summary>
        [Fact]
        public void DefaultUses32By32() {
            NumericPrecisionPolicy policy = NumericPrecisionPolicy.Default;
            Assert.Equal(32, policy.StorageBits);
            Assert.Equal(32, policy.ArithmeticBits);
            Assert.Equal(1, policy.RepresentationVersion);
            Assert.Equal("real-v1-s32-a32", policy.Identity);
        }

        /// <summary>Each valid pair receives a deterministic identity without claiming backend support.</summary>
        [Theory]
        [InlineData(16, 16)] [InlineData(16, 32)] [InlineData(16, 64)]
        [InlineData(32, 16)] [InlineData(32, 32)] [InlineData(32, 64)]
        [InlineData(64, 16)] [InlineData(64, 32)] [InlineData(64, 64)]
        public void IdentityIsDeterministic(int storageBits, int arithmeticBits) {
            NumericPrecisionPolicy policy = new(storageBits, arithmeticBits);
            Assert.Equal($"real-v1-s{storageBits}-a{arithmeticBits}", policy.Identity);
            Assert.Equal(policy.Identity, new NumericPrecisionPolicy(storageBits, arithmeticBits).Identity);
        }

        /// <summary>Unsupported bit widths fail before their policy can enter a build request.</summary>
        [Theory]
        [InlineData(8, 32)] [InlineData(24, 32)] [InlineData(128, 32)]
        [InlineData(32, 8)] [InlineData(32, 24)] [InlineData(32, 128)]
        public void RejectsUnsupportedBitWidth(int storageBits, int arithmeticBits) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumericPrecisionPolicy(storageBits, arithmeticBits));
        }

        /// <summary>A backend cannot accidentally advertise unimplemented half arithmetic.</summary>
        [Fact]
        public void CapabilitiesRejectUnavailableCombination() {
            PlatformNumericCapabilities capabilities = new(NumericPrecisionPolicy.Default, [NumericPrecisionPolicy.Default]);
            NotSupportedException error = Assert.Throws<NotSupportedException>(() => capabilities.RequireSupported(new(16, 16)));
            Assert.Contains("real-v1-s16-a16", error.Message);
            capabilities.RequireSupported(new(32, 32));
        }

        /// <summary>Changing the caller's capability array cannot change published backend support.</summary>
        [Fact]
        public void CapabilitiesRetainTheirOwnSnapshot() {
            NumericPrecisionPolicy[] supported = [NumericPrecisionPolicy.Default];
            PlatformNumericCapabilities capabilities = new(NumericPrecisionPolicy.Default, supported);
            supported[0] = new NumericPrecisionPolicy(64, 64);
            capabilities.RequireSupported(new(32, 32));
            Assert.Throws<NotSupportedException>(() => capabilities.RequireSupported(new(64, 64)));
        }

        /// <summary>A default must be one of the combinations the backend actually implements.</summary>
        [Fact]
        public void CapabilitiesRejectUnsupportedDefault() {
            Assert.Throws<ArgumentException>(() => new PlatformNumericCapabilities(new(64, 64), [NumericPrecisionPolicy.Default]));
        }

        /// <summary>Explicit options override platform defaults without changing later requests.</summary>
        [Fact]
        public void ExplicitWindowsPoliciesRemainIndependent() {
            PlatformNumericCapabilities capabilities = new(NumericPrecisionPolicy.Default, [NumericPrecisionPolicy.Default, new(64, 64)]);
            Dictionary<string, string> options = new() {
                [PlatformCodegenSettingIds.RealStorageBits] = "64",
                [PlatformCodegenSettingIds.RealArithmeticBits] = "64"
            };
            NumericPrecisionPolicy first = PlatformNumericPolicyResolver.Resolve(options, capabilities);
            options[PlatformCodegenSettingIds.RealStorageBits] = "32";
            options[PlatformCodegenSettingIds.RealArithmeticBits] = "32";
            NumericPrecisionPolicy second = PlatformNumericPolicyResolver.Resolve(options, capabilities);
            Assert.Equal("real-v1-s64-a64", first.Identity);
            Assert.Equal("real-v1-s32-a32", second.Identity);
            Assert.Equal("real-v1-s32-a32", capabilities.DefaultPolicy.Identity);
        }

        /// <summary>Malformed persisted precision never silently becomes the platform default.</summary>
        [Theory]
        [InlineData("")] [InlineData(" ")] [InlineData("float")] [InlineData("32.0")] [InlineData("8")]
        public void MalformedOptionFailsBeforeBuild(string value) {
            PlatformNumericCapabilities capabilities = new(NumericPrecisionPolicy.Default, [NumericPrecisionPolicy.Default]);
            Assert.Throws<ArgumentException>(() => PlatformNumericPolicyResolver.Resolve(
                new Dictionary<string, string> { [PlatformCodegenSettingIds.RealStorageBits] = value }, capabilities));
        }

        /// <summary>Build requests copy their numeric choice before callers edit mutable selections.</summary>
        [Fact]
        public void BuildRequestRetainsNumericSnapshot() {
            helengine.baseplatform.Requests.PlatformBuildRequest template =
                Requests.PlatformBuildRequestTestFactory.Create(helengine.baseplatform.Requests.PlatformBuildExecutionScope.Full, true);
            Dictionary<string, string> options = new() {
                [PlatformCodegenSettingIds.RealStorageBits] = "64", [PlatformCodegenSettingIds.RealArithmeticBits] = "64"
            };
            helengine.baseplatform.Requests.PlatformBuildRequest request = new(
                template.Manifest, template.TargetVariants, template.CookProfiles, template.OutputRoot, template.WorkingRoot,
                "", "", "", null, null, options);
            options[PlatformCodegenSettingIds.RealStorageBits] = "32";
            Assert.Equal("real-v1-s64-a64", request.NumericPolicy.Identity);
            Assert.Equal("real-v1-s32-a32", template.NumericPolicy.Identity);
        }

        /// <summary>Explicit numeric metadata exposes only declared widths and preserves old profile constructors.</summary>
        [Fact]
        public void ProfileSettingsReflectImplementedCapabilities() {
            PlatformCodegenProfileDefinition legacy = new("legacy", "Legacy", "Existing profile", PlatformCodegenLanguage.Cpp,
                helengine.baseplatform.Profiles.PlatformSerializationEndianness.LittleEndian, []);
            Assert.Empty(legacy.Settings);
            legacy.NumericCapabilities.RequireSupported(NumericPrecisionPolicy.Default);
            PlatformNumericCapabilities capabilities = new(NumericPrecisionPolicy.Default, [NumericPrecisionPolicy.Default, new(64, 64)]);
            PlatformCodegenProfileDefinition profile = new("numeric", "Numeric", "Numeric profile", PlatformCodegenLanguage.Cpp,
                helengine.baseplatform.Profiles.PlatformSerializationEndianness.LittleEndian, [], capabilities);
            Assert.Equal(2, profile.Settings.Length);
            Assert.All(profile.Settings, setting => Assert.Equal(new[] { "32", "64" }, setting.AllowedValues));
        }

        /// <summary>A serialized policy with a future ABI version cannot be accepted as the current representation.</summary>
        [Fact]
        public void RejectsUnknownRepresentationVersion() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumericPrecisionPolicy(32, 32, 2));
        }

        /// <summary>Omitted values use the backend's explicit supported default, not a global setting.</summary>
        [Fact]
        public void MissingOptionsUsePlatformDefault() {
            PlatformNumericCapabilities capabilities = new(new(64, 64), [new(64, 64)]);
            NumericPrecisionPolicy result = PlatformNumericPolicyResolver.Resolve(new Dictionary<string, string>(), capabilities);
            Assert.Equal("real-v1-s64-a64", result.Identity);
        }
    }
}
