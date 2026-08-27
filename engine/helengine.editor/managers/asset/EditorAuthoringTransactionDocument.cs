using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Current-format journal for one staged authoring transaction.
    /// </summary>
    internal sealed class EditorAuthoringTransactionDocument {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public string TransactionId { get; set; }

        public EditorAuthoringTransactionState State { get; set; }

        public List<EditorAuthoringTransactionEntry> Entries { get; set; } = new List<EditorAuthoringTransactionEntry>();

        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
}
