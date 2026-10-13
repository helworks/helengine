using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Serializes Play session tests against the rest of the suite, because they write to and capture the static
/// <see cref="Logger"/> and would otherwise leak player errors into other tests that count captured log entries.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EditorPlayLoggerCollection {
    /// <summary>
    /// Collection name shared by test classes that emit or capture static logger events during Play session tests.
    /// </summary>
    public const string Name = "Editor Play static logger";
}
