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
            @"(?:(?:\b(?:\w*(?:format|schema|payload|received|document|header|record|asset|serialized|stored)version|(?:\w*(?:document|header|record|asset|serialized|stored)\w*)\s*(?:\?\s*)?\.\s*version|version)\b\s*(?:is\s*)?(?:<=|>=|<|>)\s*(?:\d+|[A-Za-z_]\w*version)\b)|(?:(?:\d+|[A-Za-z_]\w*version)\b\s*(?:is\s*)?(?:<=|>=|<|>)\s*(?:\b(?:\w*(?:format|schema|payload|received|document|header|record|asset|serialized|stored)version|(?:\w*(?:document|header|record|asset|serialized|stored)\w*)\s*(?:\?\s*)?\.\s*version|version)\b)))",
            RegexOptions.IgnoreCase)),
        ("persisted version compatibility helper", CreateForbiddenRegex(@"\b(?:IsVersionSupported|AcceptsVersion|IsSupportedVersion|SupportsVersion|CanReadVersion|IsCompatibleVersion)\s*\(", RegexOptions.IgnoreCase))
    ];

    static readonly string[] ProductionSourceDirectoryNames = [
        "engine",
        "helengine.ui",
        "tools",
        "scripts"
    ];

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
