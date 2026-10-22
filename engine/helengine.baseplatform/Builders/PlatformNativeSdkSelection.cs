namespace helengine.baseplatform.Builders {
    /// <summary>Captures the native SDK and compiler implementation which determine one build's compiled asset ABI.</summary>
    public sealed class PlatformNativeSdkSelection {
        /// <summary>Creates a validated selection after the platform provider has resolved its actual installed toolchain.</summary>
        public PlatformNativeSdkSelection(string toolchainId, string name, string version, string compilerIdentity) {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolchainId);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(version);
            ArgumentException.ThrowIfNullOrWhiteSpace(compilerIdentity);
            ToolchainId = toolchainId;
            Name = name;
            Version = version;
            CompilerIdentity = compilerIdentity;
        }
        /// <summary>Identifies the compiler/toolchain family independently of the platform display name.</summary>
        public string ToolchainId { get; }
        /// <summary>Names the selected SDK, such as iphoneos or iphonesimulator.</summary>
        public string Name { get; }
        /// <summary>Records the actual validated installed SDK version.</summary>
        public string Version { get; }
        /// <summary>Identifies the compiler implementation which produces the payload, including shared plugin code.</summary>
        public string CompilerIdentity { get; }
    }
}
