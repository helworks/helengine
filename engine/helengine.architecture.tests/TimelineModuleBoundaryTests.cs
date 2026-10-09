using System.Xml.Linq;

namespace helengine.architecture.tests;

/// <summary>
/// Guards the opt-in shape of the timeline modules: core and every other engine project build without them, the shipping
/// runtime depends only on core, the curve catalog and native ownership, and its sources stay inside the C# subset the
/// C++ transpiler accepts.
/// </summary>
public sealed class TimelineModuleBoundaryTests {
    /// <summary>
    /// Projects allowed to reference a timeline module.
    /// </summary>
    static readonly string[] TimelineConsumers = {
        "helengine.timeline",
        "helengine.timeline.tests",
        "helengine.timeline.runtime.tests"
    };

    /// <summary>
    /// Constructs the transpiler rejects or that would pull managed-only machinery into native builds.
    /// </summary>
    static readonly string[] ForbiddenRuntimeTokens = {
        "System.Linq",
        "=>",
        "foreach",
        "async ",
        "await ",
        "dynamic ",
        "yield ",
        "ValueTuple",
        "System.Reflection"
    };

    /// <summary>
    /// Core neither references a timeline project nor uses a timeline namespace in code (comments may mention them).
    /// </summary>
    [Fact]
    public void Core_doesNotReferenceTimelineModules() {
        string engine = Path.Combine(RepositorySourceLocator.FindRepositoryRoot(), "engine");
        Assert.DoesNotContain(ProjectReferences(Path.Combine(engine, "helengine.core", "helengine.core.csproj")), reference => reference.Contains("timeline", StringComparison.OrdinalIgnoreCase));
        foreach (string file in Directory.GetFiles(Path.Combine(engine, "helengine.core"), "*.cs", SearchOption.AllDirectories)) {
            if (IsBuildOutput(file)) {
                continue;
            }

            foreach (string line in File.ReadAllLines(file)) {
                if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) {
                    Assert.False(line.Contains("helengine.timeline", StringComparison.Ordinal), $"'{Path.GetFileName(file)}' uses a timeline namespace: {line.Trim()}");
                }
            }
        }
    }

    /// <summary>
    /// Only the timeline modules' own projects and tests reference them, so the editor, platforms and every other module
    /// (and therefore a game that does not opt in) build without timeline code.
    /// </summary>
    [Fact]
    public void OnlyTimelineProjects_referenceTimelineModules() {
        string engine = Path.Combine(RepositorySourceLocator.FindRepositoryRoot(), "engine");
        foreach (string project in Directory.GetFiles(engine, "*.csproj", SearchOption.AllDirectories)) {
            if (IsBuildOutput(project)) {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(project);
            bool referencesTimeline = ProjectReferences(project).Any(reference => Path.GetFileNameWithoutExtension(reference).StartsWith("helengine.timeline", StringComparison.Ordinal));
            if (referencesTimeline) {
                Assert.Contains(name, TimelineConsumers);
            }
        }
    }

    /// <summary>
    /// The shipping runtime depends on core, the curve catalog and native ownership only (never on the authoring module
    /// or helengine.files), and the curve catalog depends on nothing.
    /// </summary>
    [Fact]
    public void TimelineRuntime_dependsOnlyOnShippingProjects() {
        string engine = Path.Combine(RepositorySourceLocator.FindRepositoryRoot(), "engine");
        string[] runtimeReferences = ProjectReferences(Path.Combine(engine, "helengine.timeline.runtime", "helengine.timeline.runtime.csproj"))
            .Select(reference => Path.GetFileNameWithoutExtension(reference))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "helengine.core", "helengine.curves", "helengine.nativeownership" }, runtimeReferences);
        Assert.Empty(ProjectReferences(Path.Combine(engine, "helengine.curves", "helengine.curves.csproj")));
    }

    /// <summary>
    /// Runtime and curve sources avoid LINQ, lambdas and expression bodies, foreach, async, dynamic, iterators, tuples and
    /// reflection, and never import the authoring namespace.
    /// </summary>
    [Fact]
    public void TimelineRuntimeSources_stayInsideTheTranspilableSubset() {
        string engine = Path.Combine(RepositorySourceLocator.FindRepositoryRoot(), "engine");
        List<string> files = new List<string>();
        files.AddRange(Directory.GetFiles(Path.Combine(engine, "helengine.timeline.runtime"), "*.cs", SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles(Path.Combine(engine, "helengine.curves"), "*.cs", SearchOption.AllDirectories));
        foreach (string file in files) {
            if (IsBuildOutput(file) || Path.GetFileName(file) == "AssemblyInfo.cs") {
                continue;
            }

            string source = File.ReadAllText(file);
            foreach (string token in ForbiddenRuntimeTokens) {
                Assert.False(source.Contains(token, StringComparison.Ordinal), $"'{Path.GetFileName(file)}' uses '{token}', which the transpiled runtime avoids.");
            }
            Assert.DoesNotContain("using helengine.timeline;", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Reads the project references of a project file.
    /// </summary>
    /// <param name="projectPath">Project file path.</param>
    /// <returns>The referenced project paths as written.</returns>
    static IEnumerable<string> ProjectReferences(string projectPath) {
        return XDocument.Load(projectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string)element.Attribute("Include") ?? string.Empty)
            .ToArray();
    }

    /// <summary>
    /// Returns whether a path lies in a build output folder.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns>True for bin and obj contents.</returns>
    static bool IsBuildOutput(string path) {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.Ordinal) || normalized.Contains("/bin/", StringComparison.Ordinal);
    }
}
