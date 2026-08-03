namespace helengine {
    /// <summary>
    /// Declares that a generated native field or property owns its assigned allocation and must release it before replacement and disposal.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class NativeOwnedMemberAttribute : Attribute {
        /// <summary>
        /// Initializes compile-time ownership metadata for one native-owning member.
        /// </summary>
        public NativeOwnedMemberAttribute() {
        }
    }
}
