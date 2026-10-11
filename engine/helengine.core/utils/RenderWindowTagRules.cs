namespace helengine {
    /// <summary>Defines portable view identifiers matching the native host's camera-routing tags.</summary>
    public static class RenderWindowTagRules {
        /// <summary>Limits view identifiers to the host's bounded diagnostic and configuration format.</summary>
        const int MaximumTagLength = 64;

        /// <summary>Rejects missing or malformed identifiers before camera or host routing state is changed.</summary>
        /// <param name="tag">View identifier to validate.</param>
        /// <param name="allowShared">Whether empty explicitly denotes rendering in all views.</param>
        public static void Validate(string tag, bool allowShared) {
            if (allowShared && tag == string.Empty) {
                return;
            }
            if (tag == null) {
                throw new ArgumentNullException(nameof(tag));
            }
            if (tag.Length == 0 || tag.Length > MaximumTagLength) {
                throw new ArgumentException("Output window tags must contain 1 to 64 ASCII characters.", nameof(tag));
            }
            for (int index = 0; index < tag.Length; index++) {
                char character = tag[index];
                if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9') || character == '_' || character == '-')) {
                    throw new ArgumentException("Output window tags accept ASCII letters, digits, underscores and hyphens.", nameof(tag));
                }
            }
        }
    }
}
