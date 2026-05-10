namespace helengine.editor {
    /// <summary>
    /// Represents the persisted workspace layout file containing five named slots.
    /// </summary>
    public sealed class EditorWorkspaceLayoutDocument {
        /// <summary>
        /// Saved workspace slots keyed by slot identifier such as `slot1`.
        /// </summary>
        public Dictionary<string, EditorWorkspaceSlotDocument> Slots { get; set; } = new Dictionary<string, EditorWorkspaceSlotDocument>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates one default layout document with all five slot keys present.
        /// </summary>
        /// <returns>Fresh layout document with empty slot payloads.</returns>
        public static EditorWorkspaceLayoutDocument CreateDefault() {
            EditorWorkspaceLayoutDocument document = new EditorWorkspaceLayoutDocument();
            for (int slotNumber = 1; slotNumber <= 5; slotNumber++) {
                document.Slots[BuildSlotKey(slotNumber)] = new EditorWorkspaceSlotDocument();
            }

            return document;
        }

        /// <summary>
        /// Returns one slot document for the supplied slot number.
        /// </summary>
        /// <param name="slotNumber">One-based slot number from one through five.</param>
        /// <returns>Matching slot document when present; otherwise null.</returns>
        public EditorWorkspaceSlotDocument GetSlot(int slotNumber) {
            string slotKey = BuildSlotKey(slotNumber);
            if (Slots.TryGetValue(slotKey, out EditorWorkspaceSlotDocument slot)) {
                return slot;
            }

            return null;
        }

        /// <summary>
        /// Replaces one saved slot payload inside the layout document.
        /// </summary>
        /// <param name="slotNumber">One-based slot number from one through five.</param>
        /// <param name="slot">Slot payload to store.</param>
        public void SetSlot(int slotNumber, EditorWorkspaceSlotDocument slot) {
            if (slot == null) {
                throw new ArgumentNullException(nameof(slot));
            }

            Slots[BuildSlotKey(slotNumber)] = slot;
        }

        /// <summary>
        /// Builds the persisted slot dictionary key for one one-based slot number.
        /// </summary>
        /// <param name="slotNumber">One-based slot number.</param>
        /// <returns>Dictionary key such as `slot1`.</returns>
        static string BuildSlotKey(int slotNumber) {
            if (slotNumber < 1 || slotNumber > 5) {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), "Workspace slot number must be between 1 and 5.");
            }

            return "slot" + slotNumber;
        }
    }
}
