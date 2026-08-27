using System.Diagnostics;
using System.Text.RegularExpressions;

namespace helengine.editor.tests;

/// <summary>
/// Keeps persisted-data compatibility behavior out of production source after the current-format break.
/// </summary>
public sealed class CurrentFormatOnlySourceContractTests {
    static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    static readonly (string Name, Regex Pattern, bool CodeOnly)[] ForbiddenPatterns = [
        ("legacy symbol or path", CreateForbiddenRegex(@"\b\w*legacy\w*\b", RegexOptions.IgnoreCase), true),
        ("migration method or symbol", CreateForbiddenRegex(@"\b\w*(?:migrate|upgrade)\w*\b", RegexOptions.IgnoreCase), true),
        ("legacy conversion method", CreateForbiddenRegex(@"\b\w*(?:convertlegacy|normalizelegacy)\w*\b", RegexOptions.IgnoreCase), true),
        ("backward compatibility claim", CreateForbiddenRegex(@"backward\s+compatibility", RegexOptions.IgnoreCase), true),
        ("persisted compatibility construct", CreateForbiddenRegex(@"\bcompatibility\s+(?:cycle|fallback|path|alias|overload)\b", RegexOptions.IgnoreCase), true),
        ("persisted version range acceptance", CreateForbiddenRegex(@"\b(?:\w*(?:payload|received|document|header|record|asset|serialized|stored)version|[A-Za-z_]\w*\s*\.\s*version|version)\b\s*(?:<=|>=|<|>)\s*(?:\d+|[A-Za-z_]\w*)\b", RegexOptions.IgnoreCase), true)
    ];

    static readonly string[] ProductionSourceDirectoryNames = [
        "engine",
        "helengine.ui",
        "tools",
        "scripts"
    ];

    /// <summary>
    /// Ensures production C# sources do not reintroduce persisted-data migration, legacy aliases, or version-range readers.
    /// </summary>
    [Fact]
    public void Production_sources_do_not_retain_persisted_data_compatibility_paths() {
        string repositoryRootPath = ResolveRepositoryRootPath();
        List<string> violations = [];

        foreach (string sourcePath in EnumerateProductionSources(repositoryRootPath)) {
            if (IsNativeMigrationMarker(repositoryRootPath, sourcePath)) {
                continue;
            }

            string sourceText = File.ReadAllText(sourcePath);
            foreach ((string name, int index) in FindForbiddenMatches(sourceText)) {
                string relativePath = Path.GetRelativePath(repositoryRootPath, sourcePath).Replace(Path.DirectorySeparatorChar, '/');
                int lineNumber = GetLineNumber(sourceText, index);
                string line = GetLineText(sourceText, index);
                violations.Add($"{relativePath}:{lineNumber}: {name}: {line}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Production source contains current-format compatibility behavior:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// Ensures multiline version checks and case variants of migration symbols are detected without treating unrelated numeric comparisons as persistence compatibility.
    /// </summary>
    [Fact]
    public void Forbidden_patterns_detect_multiline_version_ranges_and_case_insensitive_migration_symbols() {
        string sourceText = """
            void ReadCurrentPayload() {
                if (
                    payloadVersion
                    >=
                    CurrentVersion) { }
                if (DOCUMENT.
                    VERSION < 2) { }
                if (serializedAsset.
                    Version >=
                    CurrentVersion) { }
                if (record?.
                    Version > 3) { }
                TryMIGRATEAsset();
                UPGRADEAsset();
                if (gpuVersion > 1) { }
                if (minimumVersion < maximumVersion) { }
            }
            """;

        IReadOnlyList<(string Name, int Index)> violations = FindForbiddenMatches(sourceText);

        Assert.Equal(6, violations.Count);
        Assert.Equal(4, violations.Count(violation => violation.Name == "persisted version range acceptance"));
        Assert.Equal(2, violations.Count(violation => violation.Name == "migration method or symbol"));
        Assert.Contains(violations, violation => violation.Name == "persisted version range acceptance" && violation.Index == sourceText.IndexOf("serializedAsset", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures comments, escaped literals, verbatim literals, interpolated literals, and raw literals do not create source-contract violations.
    /// </summary>
    [Fact]
    public void Forbidden_patterns_ignore_comments_and_all_csharp_string_literal_forms() {
        string sourceText = """"
            // TryMIGRATEAsset();
            /* UPGRADEAsset(); asset.Version >= 2 */
            string regular = "TryMIGRATEAsset(); asset.Version >= 2";
            string escaped = "\\\"TryMIGRATEAsset();";
            string verbatim = @"TryMIGRATEAsset(); asset.Version >= 2";
            string interpolated = $"TryMIGRATEAsset(); {1}";
            string raw = """TryMIGRATEAsset();
            asset.Version >= 2""";
            char quote = '\'';
            """";

        Assert.Empty(FindForbiddenMatches(sourceText));
    }

    /// <summary>
    /// Ensures a long nonmatching identifier cannot cause pathological regular-expression backtracking in the source guard.
    /// </summary>
    [Fact]
    public void Forbidden_patterns_scan_long_nonmatching_source_with_bounded_work() {
        string sourceText = "class Stable { string value = \"" + new string('a', 200_000) + "\"; }";

        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<(string Name, int Index)> violations = FindForbiddenMatches(sourceText);
        stopwatch.Stop();

        Assert.Empty(violations);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Source guard took {stopwatch.Elapsed} for one bounded input.");
    }

    /// <summary>
    /// Ensures repository enumeration includes production tools while excluding test, vendor, build, and generated trees.
    /// </summary>
    [Fact]
    public void Production_source_enumeration_includes_tools_and_excludes_nonproduction_trees() {
        string repositoryRootPath = ResolveRepositoryRootPath();
        string[] relativePaths = EnumerateProductionSources(repositoryRootPath)
            .Select(sourcePath => Path.GetRelativePath(repositoryRootPath, sourcePath).Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();

        Assert.Contains(relativePaths, relativePath => string.Equals(relativePath, "tools/build-waiter/BuildWaiter.cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(relativePaths, relativePath => relativePath.Contains(".tests/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(relativePaths, relativePath => relativePath.Contains("/tests/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(relativePaths, relativePath => relativePath.Contains("/vendor/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(relativePaths, relativePath => relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(relativePaths, relativePath => relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(relativePaths, relativePath => relativePath.Contains("/generated/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds all forbidden constructs in one source text while preserving source indexes for diagnostics.
    /// </summary>
    /// <param name="sourceText">Complete C# source text.</param>
    /// <returns>Forbidden pattern names and their source indexes.</returns>
    static IReadOnlyList<(string Name, int Index)> FindForbiddenMatches(string sourceText) {
        if (sourceText == null) {
            throw new ArgumentNullException(nameof(sourceText));
        }

        string codeText = MaskCommentsAndStrings(sourceText);
        List<(string Name, int Index)> violations = [];
        foreach ((string name, Regex pattern, bool codeOnly) in ForbiddenPatterns) {
            string textToScan = codeOnly ? codeText : sourceText;
            foreach (Match match in pattern.Matches(textToScan)) {
                violations.Add((name, match.Index));
            }
        }

        return violations;
    }

    /// <summary>
    /// Creates one bounded regular expression used by the source contract scanner.
    /// </summary>
    static Regex CreateForbiddenRegex(string pattern, RegexOptions options) {
        return new Regex(pattern, options | RegexOptions.Compiled | RegexOptions.NonBacktracking, RegexMatchTimeout);
    }

    /// <summary>
    /// Masks comments and string/character literals while preserving every source index and newline for code diagnostics.
    /// </summary>
    /// <param name="sourceText">Complete C# source text.</param>
    /// <returns>Source text with comments and literals replaced by spaces.</returns>
    static string MaskCommentsAndStrings(string sourceText) {
        char[] masked = sourceText.ToCharArray();
        int index = 0;
        while (index < sourceText.Length) {
            if (sourceText[index] == '/' && index + 1 < sourceText.Length && sourceText[index + 1] == '/') {
                MaskUntilLineEnd(sourceText, masked, ref index);
            } else if (sourceText[index] == '/' && index + 1 < sourceText.Length && sourceText[index + 1] == '*') {
                MaskBlockComment(sourceText, masked, ref index);
            } else if (sourceText[index] == '"') {
                int quoteLength = CountConsecutiveQuotes(sourceText, index);
                if (quoteLength >= 3) {
                    MaskRawStringLiteral(sourceText, masked, ref index, quoteLength);
                } else {
                    MaskQuotedLiteral(sourceText, masked, ref index, IsVerbatimString(sourceText, index));
                }
            } else if (sourceText[index] == '\'') {
                MaskQuotedLiteral(sourceText, masked, ref index, false);
            } else {
                index++;
            }
        }

        return new string(masked);
    }

    /// <summary>
    /// Counts consecutive double-quote characters beginning at one source index.
    /// </summary>
    static int CountConsecutiveQuotes(string sourceText, int index) {
        int quoteCount = 0;
        while (index + quoteCount < sourceText.Length && sourceText[index + quoteCount] == '"') {
            quoteCount++;
        }

        return quoteCount;
    }

    /// <summary>
    /// Determines whether the string beginning at one quote uses C# verbatim escaping.
    /// </summary>
    static bool IsVerbatimString(string sourceText, int quoteIndex) {
        if (quoteIndex > 0 && sourceText[quoteIndex - 1] == '@') {
            return true;
        }

        return quoteIndex > 1
            && ((sourceText[quoteIndex - 2] == '@' && sourceText[quoteIndex - 1] == '$')
                || (sourceText[quoteIndex - 2] == '$' && sourceText[quoteIndex - 1] == '@'));
    }

    /// <summary>
    /// Masks a line comment and leaves its newline intact.
    /// </summary>
    static void MaskUntilLineEnd(string sourceText, char[] masked, ref int index) {
        while (index < sourceText.Length && sourceText[index] != '\r' && sourceText[index] != '\n') {
            masked[index++] = ' ';
        }
    }

    /// <summary>
    /// Masks a block comment and leaves all newline characters intact.
    /// </summary>
    static void MaskBlockComment(string sourceText, char[] masked, ref int index) {
        while (index < sourceText.Length) {
            if (sourceText[index] == '*' && index + 1 < sourceText.Length && sourceText[index + 1] == '/') {
                masked[index++] = ' ';
                masked[index++] = ' ';
                return;
            }

            if (sourceText[index] != '\r' && sourceText[index] != '\n') {
                masked[index] = ' ';
            }
            index++;
        }
    }

    /// <summary>
    /// Masks one C# raw string literal with its exact quote delimiter length.
    /// </summary>
    static void MaskRawStringLiteral(string sourceText, char[] masked, ref int index, int delimiterLength) {
        for (int delimiterIndex = 0; delimiterIndex < delimiterLength && index < sourceText.Length; delimiterIndex++) {
            masked[index++] = ' ';
        }

        while (index < sourceText.Length) {
            if (sourceText[index] == '"' && CountConsecutiveQuotes(sourceText, index) >= delimiterLength) {
                for (int delimiterIndex = 0; delimiterIndex < delimiterLength && index < sourceText.Length; delimiterIndex++) {
                    masked[index++] = ' ';
                }
                return;
            }

            if (sourceText[index] != '\r' && sourceText[index] != '\n') {
                masked[index] = ' ';
            }
            index++;
        }
    }

    /// <summary>
    /// Masks one quoted string or character literal and preserves escaped delimiters and newlines.
    /// </summary>
    static void MaskQuotedLiteral(string sourceText, char[] masked, ref int index, bool verbatim) {
        char quote = sourceText[index];
        masked[index++] = ' ';
        while (index < sourceText.Length) {
            char character = sourceText[index];
            if (character == quote) {
                if (verbatim && index + 1 < sourceText.Length && sourceText[index + 1] == quote) {
                    masked[index++] = ' ';
                    masked[index++] = ' ';
                    continue;
                }

                masked[index++] = ' ';
                return;
            }

            if (!verbatim && character == '\\' && index + 1 < sourceText.Length) {
                masked[index++] = ' ';
                if (sourceText[index] != '\r' && sourceText[index] != '\n') {
                    masked[index] = ' ';
                }
                index++;
                continue;
            }

            if (character != '\r' && character != '\n') {
                masked[index] = ' ';
            }
            index++;
        }
    }

    /// <summary>
    /// Gets the one-based source line containing a character index.
    /// </summary>
    static int GetLineNumber(string sourceText, int index) {
        int lineNumber = 1;
        for (int currentIndex = 0; currentIndex < index; currentIndex++) {
            if (sourceText[currentIndex] == '\n') {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    /// <summary>
    /// Gets the complete source line containing a character index.
    /// </summary>
    static string GetLineText(string sourceText, int index) {
        int lineStart = sourceText.LastIndexOf('\n', Math.Max(0, index - 1));
        int lineEnd = sourceText.IndexOf('\n', index);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        lineEnd = lineEnd < 0 ? sourceText.Length : lineEnd;
        return sourceText[lineStart..lineEnd].Trim();
    }

    /// <summary>
    /// Enumerates repository production sources while excluding generated, vendor, build, and test trees.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root containing production source roots.</param>
    /// <returns>Production C# source paths.</returns>
    static IEnumerable<string> EnumerateProductionSources(string repositoryRootPath) {
        return ProductionSourceDirectoryNames
            .Select(directoryName => Path.Combine(repositoryRootPath, directoryName))
            .Where(Directory.Exists)
            .SelectMany(sourceRootPath => Directory.EnumerateFiles(sourceRootPath, "*.cs", SearchOption.AllDirectories))
            .Where(sourcePath => !IsExcludedProductionSource(repositoryRootPath, sourcePath));
    }

    /// <summary>
    /// Determines whether one source path belongs to a non-production tree.
    /// </summary>
    static bool IsExcludedProductionSource(string repositoryRootPath, string sourcePath) {
        string relativePath = Path.GetRelativePath(repositoryRootPath, sourcePath).Replace(Path.DirectorySeparatorChar, '/');
        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments) {
            if (string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "vendor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "tests", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith(".tests", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "generated", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith(".generated", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Identifies the one native ownership marker whose migration wording is not persisted-data compatibility.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path.</param>
    /// <param name="sourcePath">Production source path.</param>
    /// <returns><c>true</c> when the source is the native migration marker definition.</returns>
    static bool IsNativeMigrationMarker(string repositoryRootPath, string sourcePath) {
        string relativePath = Path.GetRelativePath(repositoryRootPath, sourcePath).Replace(Path.DirectorySeparatorChar, '/');
        return string.Equals(
            relativePath,
            "engine/helengine.nativeownership/NativeMigrationRequiredAttribute.cs",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the HelEngine repository root by walking upward from the test assembly directory.
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

        throw new InvalidOperationException("Could not resolve the HelEngine repository root from the current test assembly location.");
    }
}
