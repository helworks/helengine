using System.Reflection;
using System.Security.Cryptography;
using helengine.baseplatform.Manifest;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies producer shader declarations retain their runtime identity through cooked manifest collection and package staging.
/// </summary>
public sealed class EditorDeclaredCookedArtifactStagingTests : IDisposable {
    /// <summary>
    /// Owns the temporary project and cooked payload used by each isolated test.
    /// </summary>
    readonly string ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-declared-artifact-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Writes a real shader payload without requiring a platform shader compiler.
    /// </summary>
    public EditorDeclaredCookedArtifactStagingTests() {
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cooked", "shaders"));
        File.WriteAllBytes(Path.Combine(ProjectRootPath, "cooked", "shaders", "renderer.wgsp"), [1, 2, 3, 4]);
    }

    /// <summary>
    /// Releases the owned temporary project after each test.
    /// </summary>
    public void Dispose() {
        Directory.Delete(ProjectRootPath, true);
    }

    /// <summary>
    /// Exercises the real collection and staging boundaries so cook-relative declarations cannot silently lose renderer shaders.
    /// </summary>
    /// <param name="producerPath">Either supported producer path form for the same physical cooked shader.</param>
    [Theory]
    [InlineData("shaders/renderer.wgsp")]
    [InlineData("cooked/shaders/renderer.wgsp")]
    public void Collected_shader_survives_package_staging_with_identity_and_hash(string producerPath) {
        EditorPlatformCookedArtifactPool pool = new(ProjectRootPath);
        PlatformCookedArtifactDeclaration declaration = new(producerPath, "shader:renderer", "shader", "Textured");
        MethodInfo collect = typeof(EditorPlatformAssetCookService).GetMethod("AddDeclaredCookedArtifacts", BindingFlags.Static | BindingFlags.NonPublic);
        HashSet<string> declaredPaths = Assert.IsType<HashSet<string>>(collect.Invoke(null, [pool, Path.Combine(ProjectRootPath, "cooked"), new[] { declaration }]));
        Assert.Contains("cooked/shaders/renderer.wgsp", declaredPaths);
        MethodInfo stage = typeof(EditorPlatformBuildGraphRunner).GetMethod("ResolveStagedCookedArtifacts", BindingFlags.Static | BindingFlags.NonPublic);
        HashSet<string> packagePaths = new(StringComparer.OrdinalIgnoreCase) { "cooked/shaders/renderer.wgsp" };
        PlatformBuildArtifact[] staged = Assert.IsType<PlatformBuildArtifact[]>(stage.Invoke(null, [pool.ToArray(), packagePaths]));
        PlatformBuildArtifact artifact = Assert.Single(staged);
        Assert.Equal("cooked/shaders/renderer.wgsp", artifact.RelativePath);
        Assert.Equal("shader:renderer", artifact.LogicalArtifactId);
        Assert.Equal("shader", artifact.ArtifactKind);
        Assert.Equal("Textured", artifact.VariantId);
        Assert.Equal("sha256:" + Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3, 4 })).ToLowerInvariant(), artifact.ContentHash);
    }

    /// <summary>
    /// Rejects two aliases of the same payload before they can introduce duplicate manifest identities.
    /// </summary>
    [Fact]
    public void Collection_rejects_duplicate_paths_with_and_without_cooked_prefix() {
        EditorPlatformCookedArtifactPool pool = new(ProjectRootPath);
        PlatformCookedArtifactDeclaration[] declarations = [
            new("shaders/renderer.wgsp", "shader:renderer", "shader", "Textured"),
            new("cooked/shaders/renderer.wgsp", "shader:renderer", "shader", "Textured")
        ];
        MethodInfo collect = typeof(EditorPlatformAssetCookService).GetMethod("AddDeclaredCookedArtifacts", BindingFlags.Static | BindingFlags.NonPublic);
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => collect.Invoke(null, [pool, Path.Combine(ProjectRootPath, "cooked"), declarations]));
        Assert.Contains("declared more than once", Assert.IsType<InvalidOperationException>(exception.InnerException).Message);
    }
}
