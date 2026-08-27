using System.Text.RegularExpressions;

namespace helengine.editor.tests;

/// <summary>
/// Keeps persisted-data compatibility behavior out of production source after the current-format break.
/// </summary>
public sealed class CurrentFormatOnlySourceContractTests {
    static readonly (string Name, Regex Pattern)[] ForbiddenPatterns = [
        ("legacy symbol or path", new Regex(@"\b\w*legacy\w*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("migration method or symbol", new Regex(@"\b\w*(?:Migrate|Upgrade)\w*\b", RegexOptions.Compiled)),
        ("legacy conversion method", new Regex(@"\b\w*(?:ConvertLegacy|NormalizeLegacy)\w*\b", RegexOptions.Compiled)),
        ("backward compatibility claim", new Regex(@"backward\s+compatibility", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("persisted compatibility construct", new Regex(@"\bcompatibility\s+(?:cycle|fallback|path|alias|overload)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("persisted version range acceptance", new Regex(@"\b(?:header\.Version|version)\s*(?:<|>|<=|>=)\s*(?:\d+|[A-Za-z_]\w*Version)\b", RegexOptions.Compiled))
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

            string[] lines = File.ReadAllLines(sourcePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                string line = lines[lineIndex];
                foreach ((string name, Regex pattern) in ForbiddenPatterns) {
                    MatchCollection matches = pattern.Matches(line);
                    foreach (Match match in matches) {
                        violations.Add($"{Path.GetRelativePath(repositoryRootPath, sourcePath)}:{lineIndex + 1}: {name}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Production source contains current-format compatibility behavior:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// Enumerates engine and editor application production sources while excluding generated, vendor, and test trees.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root containing production source roots.</param>
    /// <returns>Production C# source paths.</returns>
    static IEnumerable<string> EnumerateProductionSources(string repositoryRootPath) {
        string[] sourceRoots = [
            Path.Combine(repositoryRootPath, "engine"),
            Path.Combine(repositoryRootPath, "helengine.ui")
        ];

        return sourceRoots
            .Where(Directory.Exists)
            .SelectMany(sourceRootPath => Directory.EnumerateFiles(sourceRootPath, "*.cs", SearchOption.AllDirectories))
            .Where(sourcePath => {
                string normalizedPath = sourcePath.Replace(Path.DirectorySeparatorChar, '/');
                return !normalizedPath.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains("/vendor/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains(".tests/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase);
            });
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
