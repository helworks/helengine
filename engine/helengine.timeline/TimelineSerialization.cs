using helengine.files;

namespace helengine.timeline {
    /// <summary>
    /// Connects the timeline module to the editor asset format. Hosts that read or write <c>.htimeline</c> files (editor,
    /// cooker, video tools) call <see cref="Register"/> once at startup; core and helengine.files never reference this
    /// module, so games without timelines carry none of it.
    /// </summary>
    public static class TimelineSerialization {
        /// <summary>
        /// Registers the timeline payload serializer under <see cref="EditorAssetBinaryValueKind.TimelineAsset"/>. Safe to
        /// call more than once.
        /// </summary>
        public static void Register() {
            EditorAssetPayloadSerializerRegistry.Register(new TimelineAssetPayloadSerializer());
        }
    }
}
