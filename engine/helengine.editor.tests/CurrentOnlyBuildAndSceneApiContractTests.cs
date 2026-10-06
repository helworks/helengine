using helengine;
using helengine.baseplatform.Results;
using System.Reflection;

namespace helengine.editor.tests;

/// <summary>
/// Verifies compiled editor and runtime types expose the current build and scene APIs.
/// </summary>
public sealed class CurrentOnlyBuildAndSceneApiContractTests {
    /// <summary>
    /// Ensures the runtime scene resolver has only its current content-manager constructor.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_exposes_only_content_manager_constructor() {
        Assert.Single(typeof(RuntimeSceneAssetReferenceResolver).GetConstructors());
    }

    /// <summary>
    /// Ensures the scene-packager result accepts complete typed shader dependencies without an ID-only compatibility path.
    /// </summary>
    [Fact]
    public void EditorPlatformBuildScenePackagerResult_exposes_typed_dependencies_only() {
        ConstructorInfo constructor = Assert.Single(typeof(EditorPlatformBuildScenePackagerResult).GetConstructors());
        ParameterInfo[] parameters = constructor.GetParameters();

        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(IReadOnlyList<PlatformShaderDependency>));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(IReadOnlyList<string>));
        Assert.Null(typeof(EditorPlatformBuildScenePackagerResult).GetProperty("ReferencedShaderAssetIds"));

    }
}
