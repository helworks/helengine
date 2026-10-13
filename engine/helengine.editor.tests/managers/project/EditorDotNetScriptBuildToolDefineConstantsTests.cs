using System.Xml.Linq;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the MSBuild property file the script build tool hands to generated module projects, which carries the
/// invocation output root and, for Play builds, the selected platform's gameplay preprocessor symbols.
/// </summary>
public sealed class EditorDotNetScriptBuildToolDefineConstantsTests {
    /// <summary>
    /// Ensures supplied symbols are appended to the inherited define constants and the output root is preserved.
    /// </summary>
    [Fact]
    public void BuildExecutionOutputPropsXml_appends_supplied_symbols_to_inherited_define_constants() {
        string propsXml = EditorDotNetScriptBuildTool.BuildExecutionOutputPropsXml("C:/x", new[] { "A", "HELENGINE_ENV_RELEASE" });

        XDocument document = XDocument.Parse(propsXml);
        XElement defineConstants = Assert.Single(document.Descendants("DefineConstants"));
        Assert.Equal("$(DefineConstants);A;HELENGINE_ENV_RELEASE", defineConstants.Value);
        XElement outputRoot = Assert.Single(document.Descendants("HelengineExecutionOutputRoot"));
        Assert.Equal("C:/x", outputRoot.Value);
    }

    /// <summary>
    /// Ensures an empty symbol list leaves define constants untouched so ordinary script builds keep their behavior.
    /// </summary>
    [Fact]
    public void BuildExecutionOutputPropsXml_omits_define_constants_when_no_symbols_are_supplied() {
        string propsXml = EditorDotNetScriptBuildTool.BuildExecutionOutputPropsXml("C:/x", Array.Empty<string>());

        XDocument document = XDocument.Parse(propsXml);
        Assert.Empty(document.Descendants("DefineConstants"));
        XElement outputRoot = Assert.Single(document.Descendants("HelengineExecutionOutputRoot"));
        Assert.Equal("C:/x", outputRoot.Value);
    }

    /// <summary>
    /// Ensures a missing symbol list is rejected instead of being treated as an empty one.
    /// </summary>
    [Fact]
    public void BuildExecutionOutputPropsXml_rejects_null_symbol_list() {
        Assert.Throws<ArgumentNullException>(() => EditorDotNetScriptBuildTool.BuildExecutionOutputPropsXml("C:/x", null));
    }

    /// <summary>
    /// Ensures the public build overload rejects a missing symbol list before launching any process.
    /// </summary>
    [Fact]
    public void Build_rejects_null_additional_define_constants() {
        EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();

        Assert.Throws<ArgumentNullException>(() => buildTool.Build("C:/x/solution.slnf", "C:/x/out", (IReadOnlyList<string>)null));
    }
}
