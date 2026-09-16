using System.Xml.Linq;

namespace helengine.architecture.tests;

/// <summary>
/// Guards the build inputs that decide whether a project can be skipped when nothing changed.
/// </summary>
public class IncrementalBuildTests {
    /// <summary>
    /// Projects that generate a source file before compiling, which must not rewrite it unconditionally.
    /// </summary>
    public static TheoryData<string> GeneratedSourceProjects => new() {
        Path.Combine("engine", "helengine.editor", "helengine.editor.csproj"),
        Path.Combine("engine", "helengine.editor.tests", "helengine.editor.tests.csproj")
    };

    /// <summary>
    /// A WriteLinesToFile feeding a Compile item must only write when the content differs. Rewriting it on
    /// every build gives the file a new timestamp, so CoreCompile sees an input newer than the assembly and
    /// recompiles even with no source change. For helengine.editor that cascades to every referencing project
    /// and changes helengine.editor.dll's timestamp, which invalidates the editor's project-script build
    /// fingerprint and rebuilds game scripts on every editor launch.
    /// </summary>
    /// <param name="relativeProjectPath">Repository-relative project file that emits a generated source file.</param>
    [Theory]
    [MemberData(nameof(GeneratedSourceProjects))]
    public void Generated_source_files_are_written_only_when_different(string relativeProjectPath) {
        string projectPath = Path.Combine(RepositorySourceLocator.FindRepositoryRoot(), relativeProjectPath);
        Assert.True(File.Exists(projectPath), $"Expected project file at '{projectPath}'.");

        XDocument project = XDocument.Load(projectPath);
        XElement[] writes = project.Descendants()
            .Where(element => element.Name.LocalName == "WriteLinesToFile")
            .ToArray();
        Assert.NotEmpty(writes);

        foreach (XElement write in writes) {
            string file = (string?)write.Attribute("File") ?? string.Empty;
            Assert.True(
                string.Equals((string?)write.Attribute("WriteOnlyWhenDifferent"), "true", StringComparison.OrdinalIgnoreCase),
                $"'{relativeProjectPath}' writes '{file}' without WriteOnlyWhenDifferent, which forces a recompile on every build.");
        }
    }
}
