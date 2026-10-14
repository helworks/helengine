using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Runs the process-cancellation tests apart from every other test class, because they count first-chance exceptions
/// raised anywhere in the process.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EditorProcessCancellationTestCollection {
    /// <summary>
    /// Name of the collection.
    /// </summary>
    public const string Name = "EditorProcessCancellation";
}
