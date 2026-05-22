namespace helengine {
    /// <summary>
    /// Represents serialized data for a constant buffer member.
    /// </summary>
    public class ShaderConstantMemberAsset {
        /// <summary>
        /// Member name as declared in the shader.
        /// </summary>
        public string Name;

        /// <summary>
        /// Type name such as float4x4.
        /// </summary>
        public string Type;

        /// <summary>
        /// Byte offset of the member within the constant buffer.
        /// </summary>
        public int Offset;

        /// <summary>
        /// Size in bytes occupied by the member.
        /// </summary>
        public int Size;

        /// <summary>
        /// Builds a runtime constant member definition from serialized data.
        /// </summary>
        /// <returns>Constant member definition.</returns>
        public ShaderConstantMember ToMember() {
            Validate();
            return new ShaderConstantMember(Name, Type, Offset, Size);
        }

        /// <summary>
        /// Creates a serialized member asset from a runtime definition.
        /// </summary>
        /// <param name="member">Member definition to convert.</param>
        /// <returns>Serialized member asset.</returns>
        public static ShaderConstantMemberAsset FromMember(ShaderConstantMember member) {
            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }

            ShaderConstantMemberAsset asset = new ShaderConstantMemberAsset {
                Name = member.Name,
                Type = member.Type,
                Offset = member.Offset,
                Size = member.Size
            };

            return asset;
        }

        /// <summary>
        /// Validates member data before conversion.
        /// </summary>
        void Validate() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new InvalidOperationException("Member name must be provided.");
            } else if (string.IsNullOrWhiteSpace(Type)) {
                throw new InvalidOperationException("Member type must be provided.");
            } else if (Offset < 0) {
                throw new InvalidOperationException("Member offset cannot be negative.");
            } else if (Size < 0) {
                throw new InvalidOperationException("Member size cannot be negative.");
            }
        }
    }
}
