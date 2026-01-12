namespace helengine {
    /// <summary>
    /// Represents raw text content stored as a string.
    /// </summary>
    [ProtoBuf.ProtoContract]
    public class TextAsset : Asset {
        /// <summary>
        /// Raw text content for the asset.
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public string Text;
    }
}
