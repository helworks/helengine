namespace helengine {
    /// <summary>
    /// Represents serialized data for a shader resource binding.
    /// </summary>
    public class ShaderBindingAsset {
        /// <summary>
        /// Resource name as declared in the shader.
        /// </summary>
        public string Name;

        /// <summary>
        /// Resource type classification.
        /// </summary>
        public ShaderResourceType Type;

        /// <summary>
        /// Logical descriptor set or register space.
        /// </summary>
        public int Set;

        /// <summary>
        /// Binding slot within the set or space.
        /// </summary>
        public int Slot;

        /// <summary>
        /// Size in bytes for constant buffers, otherwise zero.
        /// </summary>
        public int Size;

        /// <summary>
        /// Constant buffer members associated with the binding.
        /// </summary>
        public ShaderConstantMemberAsset[] Members;

        /// <summary>
        /// Builds a runtime binding definition from serialized data.
        /// </summary>
        /// <returns>Binding definition instance.</returns>
        public ShaderBinding ToBinding() {
            Validate();

            ShaderConstantMember[] members = BuildMembers();
            return new ShaderBinding(Name, Type, Set, Slot, Size, members);
        }

        /// <summary>
        /// Creates a serialized binding asset from a runtime binding definition.
        /// </summary>
        /// <param name="binding">Binding definition to convert.</param>
        /// <returns>Serialized binding asset.</returns>
        public static ShaderBindingAsset FromBinding(ShaderBinding binding) {
            if (binding == null) {
                throw new ArgumentNullException(nameof(binding));
            }

            ShaderBindingAsset asset = new ShaderBindingAsset {
                Name = binding.Name,
                Type = binding.Type,
                Set = binding.Set,
                Slot = binding.Slot,
                Size = binding.Size,
                Members = BuildMemberAssets(binding)
            };

            return asset;
        }

        /// <summary>
        /// Validates binding data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new InvalidOperationException("Binding name must be provided.");
            } else if (Set < 0) {
                throw new InvalidOperationException("Binding set cannot be negative.");
            } else if (Slot < 0) {
                throw new InvalidOperationException("Binding slot cannot be negative.");
            } else if (Size < 0) {
                throw new InvalidOperationException("Binding size cannot be negative.");
            } else if (Members == null) {
                throw new InvalidOperationException("Binding members must be provided.");
            }
        }

        /// <summary>
        /// Builds runtime constant member definitions from serialized members.
        /// </summary>
        /// <returns>Array of constant members.</returns>
        ShaderConstantMember[] BuildMembers() {
            ShaderConstantMember[] members = new ShaderConstantMember[Members.Length];
            for (int i = 0; i < Members.Length; i++) {
                ShaderConstantMemberAsset member = Members[i];
                if (member == null) {
                    throw new InvalidOperationException("Binding members contain a null entry.");
                }

                members[i] = member.ToMember();
            }

            return members;
        }

        /// <summary>
        /// Builds serialized member assets from a runtime binding definition.
        /// </summary>
        /// <param name="binding">Binding definition to read.</param>
        /// <returns>Array of member assets.</returns>
        static ShaderConstantMemberAsset[] BuildMemberAssets(ShaderBinding binding) {
            IReadOnlyList<ShaderConstantMember> members = binding.Members;
            ShaderConstantMemberAsset[] assets = new ShaderConstantMemberAsset[members.Count];
            for (int i = 0; i < members.Count; i++) {
                assets[i] = ShaderConstantMemberAsset.FromMember(members[i]);
            }

            return assets;
        }
    }
}
