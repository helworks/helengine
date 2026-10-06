namespace helengine.editor.tests;

/// <summary>
/// Verifies the renderer source reads the exact entity world matrix instead of recomposing a lossy decomposed transform.
/// </summary>
public sealed class RendererWorldMatrixSourceTests {

    /// <summary>
    /// Counts how many times one exact source fragment appears within one source file.
    /// </summary>
    /// <param name="source">Source text to scan.</param>
    /// <param name="fragment">Exact source fragment to count.</param>
    /// <returns>Occurrence count for the requested fragment.</returns>
    static int CountOccurrences(string source, string fragment) {
        if (string.IsNullOrEmpty(source)) {
            throw new ArgumentException("Source text must be provided.", nameof(source));
        }
        if (string.IsNullOrEmpty(fragment)) {
            throw new ArgumentException("Source fragment must be provided.", nameof(fragment));
        }

        int count = 0;
        int searchIndex = 0;
        while (true) {
            int foundIndex = source.IndexOf(fragment, searchIndex, StringComparison.Ordinal);
            if (foundIndex < 0) {
                return count;
            }

            count++;
            searchIndex = foundIndex + fragment.Length;
        }
    }
}
