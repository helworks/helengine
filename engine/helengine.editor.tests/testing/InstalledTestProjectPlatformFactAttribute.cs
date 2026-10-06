namespace helengine.editor.tests.testing;

/// <summary>
/// Runs an integration test only when the committed test project's exact engine version has the required builder installed.
/// Unit tests use explicit platform definitions and do not require this machine-level prerequisite.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class InstalledTestProjectPlatformFactAttribute : FactAttribute {
    /// <summary>Checks installation metadata without loading or executing the external platform builder.</summary>
    /// <param name="platformId">Platform required by the integration test.</param>
    public InstalledTestProjectPlatformFactAttribute(string platformId) {
        string projectPath = Path.Combine(TestSourceRepositoryLocator.ResolveHelEngineRootPath(), "test-project", "project.heproj");
        EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(projectPath);
        helengine.platforms.AvailablePlatformDescriptor descriptor = bootstrap.AvailablePlatforms
            .SingleOrDefault(platform => platform.Id == platformId);
        if (descriptor == null || !descriptor.IsInstalled || !File.Exists(descriptor.BuilderAssemblyPath)) {
            Skip = $"Requires the installed '{platformId}' builder for engine '{bootstrap.RequiredEngineVersion}'.";
        }
    }
}
