namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Minimal reflected component used to verify automatic scene persistence rejects raw binary blob members.
    /// </summary>
    public sealed class AutomaticScriptComponentBinaryPayloadGuardTestComponent : Component {
        /// <summary>
        /// Gets or sets one forbidden raw binary payload that should not be allowed through automatic component persistence.
        /// </summary>
        public byte[] RawPayload { get; set; }
    }
}
