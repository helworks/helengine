namespace helengine.editor.tests;

/// <summary>
/// Verifies the editor session source resets shared input-capture state at both lifecycle boundaries.
/// </summary>
public sealed class EditorSessionInputCaptureLifecycleSourceTests {
    /// <summary>
    /// Ensures session initialization and disposal each clear the static input-capture registry.
    /// </summary>
    [Fact]
    public void Editor_session_source_resets_input_capture_at_startup_and_teardown() {
        string source = File.ReadAllText(GetEditorSessionSourcePath());
        string codeOnlySource = RemoveCommentsAndLiterals(source);
        string constructorBody = GetPublicMemberBody(codeOnlySource, "public EditorSession(");
        string disposeBody = GetPublicMemberBody(codeOnlySource, "public void Dispose()");

        Assert.Equal(1, CountInputCaptureResetInvocations(constructorBody));
        Assert.Equal(1, CountInputCaptureResetInvocations(disposeBody));
    }

    /// <summary>
    /// Builds the absolute path to the editor session source file under the repository root.
    /// </summary>
    /// <returns>Absolute path to <c>EditorSession.cs</c>.</returns>
    static string GetEditorSessionSourcePath() {
        return Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.editor",
            "EditorSession.cs");
    }

    /// <summary>
    /// Resolves the helengine repository root by walking upward from the test assembly location.
    /// </summary>
    /// <returns>Absolute repository root path.</returns>
    static string ResolveRepositoryRootPath() {
        string currentPath = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            string rootMarkerPath = Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj");
            if (File.Exists(rootMarkerPath)) {
                return currentPath;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the current test assembly location.");
    }

    /// <summary>
    /// Extracts one exact public method or constructor body by balancing braces from its declaration.
    /// </summary>
    /// <param name="codeOnlySource">C# source text with comments and literals replaced by whitespace.</param>
    /// <param name="methodDeclaration">Unique declaration prefix that identifies the target member.</param>
    /// <returns>Source text beginning with the opening brace and ending with its matching closing brace.</returns>
    static string GetPublicMemberBody(string codeOnlySource, string methodDeclaration) {
        int declarationIndex = codeOnlySource.IndexOf(methodDeclaration, StringComparison.Ordinal);
        if (declarationIndex < 0) {
            throw new InvalidOperationException(string.Concat("Could not find editor session declaration '", methodDeclaration, "'."));
        }

        int bodyStartIndex = codeOnlySource.IndexOf('{', declarationIndex);
        if (bodyStartIndex < 0) {
            throw new InvalidOperationException(string.Concat("Could not find the body for editor session declaration '", methodDeclaration, "'."));
        }

        int braceDepth = 0;
        for (int currentIndex = bodyStartIndex; currentIndex < codeOnlySource.Length; currentIndex++) {
            if (codeOnlySource[currentIndex] == '{') {
                braceDepth++;
            } else if (codeOnlySource[currentIndex] == '}') {
                braceDepth--;
                if (braceDepth == 0) {
                    return codeOnlySource.Substring(bodyStartIndex, currentIndex - bodyStartIndex + 1);
                }
            }
        }

        throw new InvalidOperationException(string.Concat("Could not find the closing brace for editor session declaration '", methodDeclaration, "'."));
    }

    /// <summary>
    /// Replaces comments, character literals, and string literals with whitespace while preserving source indexes.
    /// </summary>
    /// <param name="source">C# source text to sanitize.</param>
    /// <returns>Source text containing only executable code and whitespace.</returns>
    static string RemoveCommentsAndLiterals(string source) {
        char[] codeOnlyCharacters = source.ToCharArray();
        int currentIndex = 0;

        while (currentIndex < source.Length) {
            if (source[currentIndex] == '/' && currentIndex + 1 < source.Length && source[currentIndex + 1] == '/') {
                int lineCommentEndIndex = source.IndexOfAny(new char[] { '\r', '\n' }, currentIndex + 2);
                if (lineCommentEndIndex < 0) {
                    lineCommentEndIndex = source.Length;
                }

                ReplaceWithWhitespace(codeOnlyCharacters, currentIndex, lineCommentEndIndex);
                currentIndex = lineCommentEndIndex;
            } else if (source[currentIndex] == '/' && currentIndex + 1 < source.Length && source[currentIndex + 1] == '*') {
                int blockCommentEndIndex = source.IndexOf("*/", currentIndex + 2, StringComparison.Ordinal);
                if (blockCommentEndIndex < 0) {
                    throw new InvalidOperationException("Could not find the closing delimiter for a block comment in EditorSession.cs.");
                }

                blockCommentEndIndex += 2;
                ReplaceWithWhitespace(codeOnlyCharacters, currentIndex, blockCommentEndIndex);
                currentIndex = blockCommentEndIndex;
            } else if (source[currentIndex] == '\'') {
                int characterLiteralEndIndex = FindCharacterLiteralEndIndex(source, currentIndex);
                ReplaceWithWhitespace(codeOnlyCharacters, currentIndex, characterLiteralEndIndex + 1);
                currentIndex = characterLiteralEndIndex + 1;
            } else if (source[currentIndex] == '"') {
                int stringLiteralStartIndex = FindStringLiteralStartIndex(source, currentIndex);
                int stringLiteralEndIndex = FindStringLiteralEndIndex(source, currentIndex);
                ReplaceWithWhitespace(codeOnlyCharacters, stringLiteralStartIndex, stringLiteralEndIndex + 1);
                currentIndex = stringLiteralEndIndex + 1;
            } else {
                currentIndex++;
            }
        }

        return new string(codeOnlyCharacters);
    }

    /// <summary>
    /// Replaces a half-open source range with spaces while retaining line-ending characters for diagnostics.
    /// </summary>
    /// <param name="characters">Mutable source characters being sanitized.</param>
    /// <param name="startIndex">Inclusive range start.</param>
    /// <param name="endIndex">Exclusive range end.</param>
    static void ReplaceWithWhitespace(char[] characters, int startIndex, int endIndex) {
        for (int currentIndex = startIndex; currentIndex < endIndex; currentIndex++) {
            if (characters[currentIndex] != '\r' && characters[currentIndex] != '\n') {
                characters[currentIndex] = ' ';
            }
        }
    }

    /// <summary>
    /// Finds the opening index of a normal, interpolated, or verbatim string literal.
    /// </summary>
    /// <param name="source">C# source text containing the literal.</param>
    /// <param name="openingQuoteIndex">Index of the literal's opening double quote.</param>
    /// <returns>Index of the first literal prefix character or the opening quote.</returns>
    static int FindStringLiteralStartIndex(string source, int openingQuoteIndex) {
        int stringLiteralStartIndex = openingQuoteIndex;
        if (stringLiteralStartIndex > 0 && source[stringLiteralStartIndex - 1] == '@') {
            stringLiteralStartIndex--;
        }
        if (stringLiteralStartIndex > 0 && source[stringLiteralStartIndex - 1] == '$') {
            stringLiteralStartIndex--;
        }

        return stringLiteralStartIndex;
    }

    /// <summary>
    /// Finds the closing quote of a normal, verbatim, or raw string literal.
    /// </summary>
    /// <param name="source">C# source text containing the literal.</param>
    /// <param name="openingQuoteIndex">Index of the literal's opening double quote.</param>
    /// <returns>Index of the literal's final double quote.</returns>
    static int FindStringLiteralEndIndex(string source, int openingQuoteIndex) {
        int quoteCount = CountConsecutiveQuotes(source, openingQuoteIndex);
        if (quoteCount >= 3) {
            return FindRawStringLiteralEndIndex(source, openingQuoteIndex, quoteCount);
        }

        bool isVerbatimString = openingQuoteIndex > 0 && source[openingQuoteIndex - 1] == '@';
        for (int currentIndex = openingQuoteIndex + 1; currentIndex < source.Length; currentIndex++) {
            if (source[currentIndex] != '"') {
                if (!isVerbatimString && source[currentIndex] == '\\') {
                    currentIndex++;
                }
                continue;
            }

            if (isVerbatimString && currentIndex + 1 < source.Length && source[currentIndex + 1] == '"') {
                currentIndex++;
                continue;
            }

            return currentIndex;
        }

        throw new InvalidOperationException("Could not find the closing quote for a string literal in EditorSession.cs.");
    }

    /// <summary>
    /// Finds the closing delimiter of a raw string literal.
    /// </summary>
    /// <param name="source">C# source text containing the raw literal.</param>
    /// <param name="openingQuoteIndex">Index of the first opening quote.</param>
    /// <param name="quoteCount">Number of quotes in the opening delimiter.</param>
    /// <returns>Index of the final quote in the matching closing delimiter.</returns>
    static int FindRawStringLiteralEndIndex(string source, int openingQuoteIndex, int quoteCount) {
        for (int currentIndex = openingQuoteIndex + quoteCount; currentIndex < source.Length; currentIndex++) {
            if (source[currentIndex] == '"' && CountConsecutiveQuotes(source, currentIndex) >= quoteCount) {
                return currentIndex + quoteCount - 1;
            }
        }

        throw new InvalidOperationException("Could not find the closing delimiter for a raw string literal in EditorSession.cs.");
    }

    /// <summary>
    /// Counts adjacent double-quote characters starting at one source index.
    /// </summary>
    /// <param name="source">C# source text to inspect.</param>
    /// <param name="startIndex">Index of the first potential double quote.</param>
    /// <returns>Number of consecutive double quotes.</returns>
    static int CountConsecutiveQuotes(string source, int startIndex) {
        int quoteCount = 0;
        while (startIndex + quoteCount < source.Length && source[startIndex + quoteCount] == '"') {
            quoteCount++;
        }

        return quoteCount;
    }

    /// <summary>
    /// Finds the closing quote of a character literal while accounting for escape sequences.
    /// </summary>
    /// <param name="source">C# source text containing the character literal.</param>
    /// <param name="openingQuoteIndex">Index of the literal's opening single quote.</param>
    /// <returns>Index of the literal's closing single quote.</returns>
    static int FindCharacterLiteralEndIndex(string source, int openingQuoteIndex) {
        for (int currentIndex = openingQuoteIndex + 1; currentIndex < source.Length; currentIndex++) {
            if (source[currentIndex] == '\\') {
                currentIndex++;
            } else if (source[currentIndex] == '\'') {
                return currentIndex;
            }
        }

        throw new InvalidOperationException("Could not find the closing quote for a character literal in EditorSession.cs.");
    }

    /// <summary>
    /// Counts executable invocations of the editor input-capture reset method.
    /// </summary>
    /// <param name="codeOnlyMemberBody">Sanitized source text for one editor session member body.</param>
    /// <returns>Number of reset invocations in the member body.</returns>
    static int CountInputCaptureResetInvocations(string codeOnlyMemberBody) {
        return System.Text.RegularExpressions.Regex.Count(
            codeOnlyMemberBody,
            @"\bEditorInputCaptureService\s*\.\s*Reset\s*\(\s*\)\s*;");
    }
}
