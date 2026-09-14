namespace helengine {
    /// <summary>
    /// Holds the single authoritative table describing every shader compile target so target names, API defines and backend lookups stay in agreement instead of drifting across hand-written switches.
    /// </summary>
    public static class ShaderTargetDescriptors {
        /// <summary>
        /// Stores every descriptor in declaration order so callers can enumerate the supported targets.
        /// </summary>
        static readonly ShaderTargetDescriptor[] Descriptors = BuildDescriptors();

        /// <summary>
        /// Indexes the descriptor table by compile target for constant-time lookups.
        /// </summary>
        static readonly Dictionary<ShaderCompileTarget, ShaderTargetDescriptor> DescriptorsByTarget = BuildDescriptorsByTarget();

        /// <summary>
        /// Indexes the descriptor table by lowercase target name so persisted identifiers can be parsed back.
        /// </summary>
        static readonly Dictionary<string, ShaderTargetDescriptor> DescriptorsByName = BuildDescriptorsByName();

        /// <summary>
        /// Gets every known compile target descriptor in declaration order.
        /// </summary>
        public static IReadOnlyList<ShaderTargetDescriptor> All {
            get {
                return Descriptors;
            }
        }

        /// <summary>
        /// Returns the descriptor for a compile target.
        /// </summary>
        /// <param name="target">Compile target to look up.</param>
        /// <returns>Descriptor registered for the target.</returns>
        public static ShaderTargetDescriptor Get(ShaderCompileTarget target) {
            ShaderTargetDescriptor descriptor;
            if (!DescriptorsByTarget.TryGetValue(target, out descriptor)) {
                throw new ArgumentOutOfRangeException(nameof(target), "Unsupported compile target.");
            }

            return descriptor;
        }

        /// <summary>
        /// Attempts to resolve a descriptor from a persisted target name, ignoring surrounding whitespace and casing.
        /// </summary>
        /// <param name="name">Target name to resolve.</param>
        /// <param name="descriptor">Resolved descriptor when the name is known.</param>
        /// <returns>True when the name matches a known target.</returns>
        public static bool TryGetByName(string name, out ShaderTargetDescriptor descriptor) {
            if (string.IsNullOrWhiteSpace(name)) {
                descriptor = null;
                return false;
            }

            return DescriptorsByName.TryGetValue(name.Trim().ToLowerInvariant(), out descriptor);
        }

        /// <summary>
        /// Builds the descriptor table covering every value of <see cref="ShaderCompileTarget"/>.
        /// </summary>
        /// <returns>Descriptor array in declaration order.</returns>
        static ShaderTargetDescriptor[] BuildDescriptors() {
            return new ShaderTargetDescriptor[] {
                new ShaderTargetDescriptor(ShaderCompileTarget.DirectX9, "dx9", "HEL_API_DX9"),
                new ShaderTargetDescriptor(ShaderCompileTarget.DirectX11, "dx11", "HEL_API_DX11"),
                new ShaderTargetDescriptor(ShaderCompileTarget.DirectX12, "dx12", "HEL_API_DX12"),
                new ShaderTargetDescriptor(ShaderCompileTarget.Vulkan, "vulkan", "HEL_API_VULKAN"),
                new ShaderTargetDescriptor(ShaderCompileTarget.Metal, "metal", "HEL_API_METAL"),
                new ShaderTargetDescriptor(ShaderCompileTarget.PsVita, "psvita", "HEL_API_PSVITA"),
                new ShaderTargetDescriptor(ShaderCompileTarget.WiiU, "wiiu", "HEL_API_WIIU")
            };
        }

        /// <summary>
        /// Builds the compile-target index over the descriptor table.
        /// </summary>
        /// <returns>Dictionary keyed by compile target.</returns>
        static Dictionary<ShaderCompileTarget, ShaderTargetDescriptor> BuildDescriptorsByTarget() {
            Dictionary<ShaderCompileTarget, ShaderTargetDescriptor> descriptorsByTarget = new Dictionary<ShaderCompileTarget, ShaderTargetDescriptor>();
            for (int i = 0; i < Descriptors.Length; i++) {
                ShaderTargetDescriptor descriptor = Descriptors[i];
                if (descriptorsByTarget.ContainsKey(descriptor.Target)) {
                    throw new InvalidOperationException("Shader target descriptor table declares a duplicate compile target.");
                }

                descriptorsByTarget.Add(descriptor.Target, descriptor);
            }

            return descriptorsByTarget;
        }

        /// <summary>
        /// Builds the target-name index over the descriptor table.
        /// </summary>
        /// <returns>Dictionary keyed by lowercase target name.</returns>
        static Dictionary<string, ShaderTargetDescriptor> BuildDescriptorsByName() {
            Dictionary<string, ShaderTargetDescriptor> descriptorsByName = new Dictionary<string, ShaderTargetDescriptor>(StringComparer.Ordinal);
            for (int i = 0; i < Descriptors.Length; i++) {
                ShaderTargetDescriptor descriptor = Descriptors[i];
                if (descriptorsByName.ContainsKey(descriptor.Name)) {
                    throw new InvalidOperationException("Shader target descriptor table declares a duplicate target name.");
                }

                descriptorsByName.Add(descriptor.Name, descriptor);
            }

            return descriptorsByName;
        }
    }
}
