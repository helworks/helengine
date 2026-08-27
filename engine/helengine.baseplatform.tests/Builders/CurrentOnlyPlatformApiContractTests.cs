using System.Reflection;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Defines the current-only public contracts used by platform build and cook callers.
/// </summary>
public sealed class CurrentOnlyPlatformApiContractTests {
    /// <summary>
    /// Ensures every manifest constructor requires explicit platform identity metadata.
    /// </summary>
    [Fact]
    public void PlatformBuildManifest_constructors_require_explicit_platform_name_and_version() {
        ConstructorInfo[] constructors = typeof(PlatformBuildManifest).GetConstructors();

        ConstructorInfo constructor = Assert.Single(constructors);
        ParameterInfo[] parameters = constructor.GetParameters();
        Assert.Equal(15, parameters.Length);
        Assert.Contains(parameters, parameter => string.Equals(parameter.Name, "platformName", StringComparison.Ordinal));
        Assert.Contains(parameters, parameter => string.Equals(parameter.Name, "platformVersion", StringComparison.Ordinal));
        Assert.Contains(parameters, parameter => string.Equals(parameter.Name, "platformCookWorkItems", StringComparison.Ordinal));
        Assert.Contains(parameters, parameter => string.Equals(parameter.Name, "runtimeFeatureManifest", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures shader artifact requests expose typed dependencies rather than an ID-only compatibility constructor.
    /// </summary>
    [Fact]
    public void PlatformShaderArtifactCookRequest_exposes_typed_dependency_constructor_only() {
        ConstructorInfo[] constructors = typeof(PlatformShaderArtifactCookRequest).GetConstructors();

        Assert.Contains(constructors, constructor => constructor.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(IReadOnlyList<PlatformShaderDependency>)));
        Assert.DoesNotContain(constructors, constructor => constructor.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(IReadOnlyList<string>)));
        Assert.Empty(typeof(PlatformShaderArtifactCookRequest).GetMethods(BindingFlags.Public | BindingFlags.Static));
    }

    /// <summary>
    /// Ensures material cook results expose one complete typed dependency constructor.
    /// </summary>
    [Fact]
    public void PlatformMaterialCookResult_exposes_typed_dependency_constructor_only() {
        ConstructorInfo[] constructors = typeof(PlatformMaterialCookResult).GetConstructors();

        Assert.Contains(constructors, constructor => constructor.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(PlatformShaderDependency[])));
        Assert.DoesNotContain(constructors, constructor => constructor.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(string[])));
        Assert.Null(typeof(PlatformMaterialCookResult).GetMethod("CreateWithDependencies", BindingFlags.Public | BindingFlags.Static));
    }
}
