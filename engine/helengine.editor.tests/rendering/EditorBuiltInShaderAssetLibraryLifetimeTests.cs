using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.rendering;

/// <summary>
/// Verifies that the session-owned built-in shader library owns its compiled
/// assets and closes its cache atomically during replacement and disposal.
/// </summary>
public sealed class EditorBuiltInShaderAssetLibraryLifetimeTests {
    const string ShaderFileName = "EditorTransformGizmo.hlsl";

    [Fact]
    public void Dispose_ReleasesEveryCachedShaderAsset_AndRejectsFurtherPopulation() {
        using EditorBuiltInShaderAssetLibrary library = TestGeneratedAssetGraph.CreateShaderLibrary();
        ShaderAsset shaderAsset = CreateShaderAsset();

        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, shaderAsset);
        library.Dispose();

        Assert.Null(shaderAsset.Programs);
        Assert.Null(shaderAsset.Binaries);
        Assert.Throws<ObjectDisposedException>(() => library.RegisterCompiledAsset(
            ShaderCompileTarget.DirectX11,
            ShaderFileName,
            CreateShaderAsset()));
        Assert.Throws<ObjectDisposedException>(() => library.Load(
            ShaderCompileTarget.DirectX11,
            ShaderFileName));
    }

    [Fact]
    public void RegisterCompiledAsset_ReplacesAndDisposesOnlyTheSupersededAsset() {
        using EditorBuiltInShaderAssetLibrary library = TestGeneratedAssetGraph.CreateShaderLibrary();
        ShaderAsset first = CreateShaderAsset();
        ShaderAsset replacement = CreateShaderAsset();

        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, first);
        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, replacement);

        Assert.Null(first.Programs);
        Assert.Null(first.Binaries);
        Assert.NotNull(replacement.Programs);
        Assert.NotNull(replacement.Binaries);
        Assert.Same(replacement, library.Load(ShaderCompileTarget.DirectX11, ShaderFileName));
    }

    [Fact]
    public void RegisterCompiledAsset_WhenRegisteringTheSameInstance_DoesNotDisposeUntilLibraryDisposal() {
        using EditorBuiltInShaderAssetLibrary library = TestGeneratedAssetGraph.CreateShaderLibrary();
        ShaderAsset shaderAsset = CreateShaderAsset();

        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, shaderAsset);
        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, shaderAsset);

        Assert.NotNull(shaderAsset.Programs);
        Assert.NotNull(shaderAsset.Binaries);

        library.Dispose();

        Assert.Null(shaderAsset.Programs);
        Assert.Null(shaderAsset.Binaries);
    }

    [Fact]
    public void RegisterCompiledAsset_WhenSupersededAssetIsSharedByAnotherKey_KeepsSharedAssetAlive() {
        using EditorBuiltInShaderAssetLibrary library = TestGeneratedAssetGraph.CreateShaderLibrary();
        ShaderAsset shared = CreateShaderAsset();
        ShaderAsset replacement = CreateShaderAsset();

        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, shared);
        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, "EditorViewportBorderGizmo.hlsl", shared);
        library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, replacement);

        Assert.NotNull(shared.Programs);
        Assert.NotNull(shared.Binaries);
        Assert.Same(shared, library.Load(ShaderCompileTarget.DirectX11, "EditorViewportBorderGizmo.hlsl"));
    }

    [Fact]
    public void Dispose_RacingWithRegistration_CannotLeaveAnAcceptedAssetAlive() {
        using EditorBuiltInShaderAssetLibrary library = TestGeneratedAssetGraph.CreateShaderLibrary();
        using ManualResetEventSlim start = new ManualResetEventSlim(false);
        ConcurrentBag<ShaderAsset> acceptedAssets = new ConcurrentBag<ShaderAsset>();
        Task[] registrationTasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => {
                ShaderAsset shaderAsset = CreateShaderAsset();
                start.Wait();
                try {
                    library.RegisterCompiledAsset(ShaderCompileTarget.DirectX11, ShaderFileName, shaderAsset);
                    acceptedAssets.Add(shaderAsset);
                } catch (ObjectDisposedException) {
                    // Disposal may win the lock before this registration reaches the cache.
                }
            }))
            .ToArray();
        Task disposeTask = Task.Run(() => {
            start.Wait();
            library.Dispose();
        });

        start.Set();
        Task.WaitAll(registrationTasks.Append(disposeTask).ToArray());

        Assert.All(acceptedAssets, shaderAsset => {
            Assert.Null(shaderAsset.Programs);
            Assert.Null(shaderAsset.Binaries);
        });
        Assert.Throws<ObjectDisposedException>(() => library.RegisterCompiledAsset(
            ShaderCompileTarget.DirectX11,
            ShaderFileName,
            CreateShaderAsset()));
    }

    static ShaderAsset CreateShaderAsset() {
        return new ShaderAsset {
            Id = "EditorTransformGizmo",
            Programs = new[] {
                new ShaderProgramAsset {
                    Name = "EditorTransformGizmo.vs"
                }
            },
            Binaries = new[] {
                new ShaderBinaryAsset {
                    Bytecode = new byte[] { 1, 2, 3 }
                }
            }
        };
    }
}
