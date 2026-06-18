namespace helengine.editor {
    /// <summary>
    /// Bakes authored text-component content into one transient sprite texture during scene packaging.
    /// </summary>
    public interface ITextComponentSpriteBakeService {
        /// <summary>
        /// Bakes one authored text-component request into a generated sprite-texture result.
        /// </summary>
        /// <param name="request">Authored text inputs that should be rendered into a sprite texture.</param>
        /// <returns>Generated texture payload and metadata used by packaging.</returns>
        TextComponentSpriteBakeResult Bake(TextComponentSpriteBakeRequest request);
    }
}
