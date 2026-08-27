using System.Diagnostics;
using System.Text.RegularExpressions;

namespace helengine.editor.tests;

/// <summary>
/// Keeps persisted-data compatibility behavior out of production source after the current-format break.
/// </summary>
public sealed class CurrentFormatOnlySourceContractTests {
    static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    static readonly (string Name, Regex Pattern)[] ForbiddenPatterns = [
        ("legacy symbol or path", CreateForbiddenRegex(@"\b\w*legacy\w*\b", RegexOptions.IgnoreCase)),
        ("migration method or symbol", CreateForbiddenRegex(@"\b\w*(?:migrate|upgrade)\w*\b", RegexOptions.IgnoreCase)),
        ("legacy conversion method", CreateForbiddenRegex(@"\b\w*(?:convertlegacy|normalizelegacy)\w*\b", RegexOptions.IgnoreCase)),
        ("backward compatibility claim", CreateForbiddenRegex(@"backward\s+compatibility", RegexOptions.IgnoreCase)),
        ("persisted compatibility construct", CreateForbiddenRegex(@"\bcompatibility\s+(?:cycle|fallback|path|alias|overload)\b", RegexOptions.IgnoreCase)),
        ("persisted version range acceptance", CreateForbiddenRegex(
            @"(?:(?:\b(?:\w*(?:format|schema|payload|received)version|(?:\w*(?:document|header|record|asset|serialized|stored)\w*)\s*(?:\?\s*)?\.\s*version|version)\b\s*(?:is\s*)?(?:<=|>=|<|>)\s*(?:\d+|[A-Za-z_]\w*version)\b)|(?:(?:\d+|[A-Za-z_]\w*version)\b\s*(?:<=|>=|<|>)\s*(?:\b(?:\w*(?:format|schema|payload|received)version|(?:\w*(?:document|header|record|asset|serialized|stored)\w*)\s*(?:\?\s*)?\.\s*version|version)\b)))",
            RegexOptions.IgnoreCase)),
        ("persisted version compatibility helper", CreateForbiddenRegex(@"\b(?:IsVersionSupported|AcceptsVersion)\s*\(", RegexOptions.IgnoreCase))
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
    /// Ensures raw compatibility diagnostics and comments remain visible to the source contract scanner.
    /// </summary>
    [Fact]
    public void Forbidden_patterns_scan_raw_comments_strings_and_interpolations() {
        string sourceText = """
            // backward compatibility
            /* compatibility cycle */
            string diagnostic = "MIGRATE legacy asset";
            string interpolated = $"payloadVersion >= CurrentVersion ({1})";
            """;

        IReadOnlyList<(string Name, int Index)> violations = FindForbiddenMatches(sourceText);

        Assert.Contains(violations, violation => violation.Name == "backward compatibility claim");
        Assert.Contains(violations, violation => violation.Name == "persisted compatibility construct");
        Assert.Contains(violations, violation => violation.Name == "migration method or symbol");
        Assert.Contains(violations, violation => violation.Name == "legacy symbol or path");
        Assert.Contains(violations, violation => violation.Name == "persisted version range acceptance");
    }

    /// <summary>
    /// Ensures every persistence version spelling, operand order, relational pattern, and multiline form is detected without treating unrelated numeric comparisons as compatibility.
    /// </summary>
    [Fact]
    public void Forbidden_patterns_detect_persistence_version_ranges_in_both_operand_orders() {
        string sourceText = """
            void ReadCurrentPayload() {
                if (
                    formatVersion
                    >=
                    CurrentVersion) { }
                if (schemaVersion <= 3) { }
                if (payloadVersion is >= CurrentVersion) { }
                if (receivedVersion is <= CurrentVersion) { }
                if (3 < document.
                    Version) { }
                if (CurrentVersion > header?.
                    Version) { }
                if (record?.
                    Version >= 2) { }
                if (1 <= asset.Version) { }
                if (serializedAsset.Version > 3) { }
                if (stored.Version < minimumVersion) { }
                if (IsVersionSupported(payloadVersion)) { }
                if (AcceptsVersion(schemaVersion)) { }
                if (gpuVersion > 1) { }
                if (minimumVersion < maximumVersion) { }
                if (summary > UnsupportedFormatVersion) { }
            }
            """;

        IReadOnlyList<(string Name, int Index)> violations = FindForbiddenMatches(sourceText);

        Assert.Equal(12, violations.Count);
        Assert.Equal(10, violations.Count(violation => violation.Name == "persisted version range acceptance"));
        Assert.Equal(2, violations.Count(violation => violation.Name == "persisted version compatibility helper"));
        Assert.DoesNotContain(violations, violation => violation.Index == sourceText.IndexOf("gpuVersion", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, violation => violation.Index == sourceText.IndexOf("minimumVersion < maximumVersion", StringComparison.Ordinal));
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
    /// Ensures excluded directories are pruned before the source walk descends into their contents.
    /// </summary>
    [Fact]
    public void Production_source_enumeration_prunes_excluded_directories_before_descent() {
        string temporaryRootPath = Path.Combine(Path.GetTempPath(), "helengine-current-format-" + Guid.NewGuid().ToString("N"));
        try {
            string includedPath = Path.Combine(temporaryRootPath, "engine", "Included.cs");
            string[] excludedRelativePaths = [
                "engine/bin/Skipped.cs",
                "engine/obj/Skipped.cs",
                "engine/vendor/Skipped.cs",
                "engine/tests/Skipped.cs",
                "engine/Generated/Skipped.cs",
                "engine/Example.generated/Skipped.cs"
            ];

            Directory.CreateDirectory(Path.GetDirectoryName(includedPath));
            File.WriteAllText(includedPath, "public class Included { }");
            foreach (string relativePath in excludedRelativePaths) {
                string excludedPath = Path.Combine(temporaryRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(excludedPath));
                File.WriteAllText(excludedPath, "public class Skipped { }");
            }

            string[] discoveredRelativePaths = EnumerateProductionSources(temporaryRootPath)
                .Select(sourcePath => Path.GetRelativePath(temporaryRootPath, sourcePath).Replace(Path.DirectorySeparatorChar, '/'))
                .ToArray();

            Assert.Equal(["engine/Included.cs"], discoveredRelativePaths);
        } finally {
            if (Directory.Exists(temporaryRootPath)) {
                Directory.Delete(temporaryRootPath, true);
            }
        }
    }

    /// <summary>
    /// Ensures the directory-pruning predicate excludes only build, vendor, test, and generated directory names.
    /// </summary>
    [Fact]
    public void Production_source_directory_pruning_predicate_is_bounded_to_nonproduction_names() {
        Assert.True(IsExcludedProductionDirectory("bin"));
        Assert.True(IsExcludedProductionDirectory("obj"));
        Assert.True(IsExcludedProductionDirectory("vendor"));
        Assert.True(IsExcludedProductionDirectory("tests"));
        Assert.True(IsExcludedProductionDirectory("sample.tests"));
        Assert.True(IsExcludedProductionDirectory("generated"));
        Assert.True(IsExcludedProductionDirectory("sample.generated"));
        Assert.False(IsExcludedProductionDirectory("engine"));
        Assert.False(IsExcludedProductionDirectory("production"));
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

        List<(string Name, int Index)> violations = [];
        foreach ((string name, Regex pattern) in ForbiddenPatterns) {
            foreach (Match match in pattern.Matches(sourceText)) {
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
        foreach (string directoryName in ProductionSourceDirectoryNames) {
            string sourceRootPath = Path.Combine(repositoryRootPath, directoryName);
            if (!Directory.Exists(sourceRootPath)) {
                continue;
            }

            foreach (string sourcePath in EnumerateProductionSourceFiles(sourceRootPath)) {
                yield return sourcePath;
            }
        }
    }

    /// <summary>
    /// Walks one production source directory while pruning non-production trees before descent.
    /// </summary>
    /// <param name="directoryPath">Directory whose immediate entries should be inspected.</param>
    /// <returns>Discovered production C# source paths.</returns>
    static IEnumerable<string> EnumerateProductionSourceFiles(string directoryPath) {
        foreach (FileSystemInfo entry in new DirectoryInfo(directoryPath).EnumerateFileSystemInfos()) {
            if (entry is DirectoryInfo childDirectory) {
                if (IsExcludedProductionDirectory(childDirectory.Name)
                    || childDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    continue;
                }

                foreach (string sourcePath in EnumerateProductionSourceFiles(childDirectory.FullName)) {
                    yield return sourcePath;
                }
            } else if (string.Equals(entry.Extension, ".cs", StringComparison.OrdinalIgnoreCase)) {
                yield return entry.FullName;
            }
        }
    }

    /// <summary>
    /// Determines whether one directory name belongs to a non-production tree.
    /// </summary>
    /// <param name="directoryName">Directory name to inspect.</param>
    /// <returns><c>true</c> when the directory should be pruned before descent.</returns>
    static bool IsExcludedProductionDirectory(string directoryName) {
        return string.Equals(directoryName, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(directoryName, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(directoryName, "vendor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(directoryName, "tests", StringComparison.OrdinalIgnoreCase)
            || directoryName.EndsWith(".tests", StringComparison.OrdinalIgnoreCase)
            || string.Equals(directoryName, "generated", StringComparison.OrdinalIgnoreCase)
            || directoryName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase);
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
