using helengine.editor.tests.testing;
using Xunit.Sdk;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the packaged Windows render-only slope scene materializes cameras, lights, and visible meshes at runtime.
/// </summary>
public sealed class CityRenderOnlySlopePackagedSceneRuntimeTests {
    /// <summary>
    /// Absolute packaged scene path for the current Windows diagnostic build.
    /// </summary>
    const string PackagedScenePath = @"C:\dev\helprojs\city\windows-build\cooked\scenes\physics\test_scene_render_only_slope.hasset";

    /// <summary>
    /// Absolute packaged content root used to resolve cooked runtime assets.
    /// </summary>
    const string PackagedContentRootPath = @"C:\dev\helprojs\city\windows-build";

    /// <summary>
    /// Ensures the packaged render-only slope scene loads one camera, one directional light, and visible 3D drawables.
    /// </summary>
    [Fact]
    public void Windows_packaged_render_only_slope_scene_registers_camera_light_and_meshes() {
        Assert.True(File.Exists(PackagedScenePath), $"Expected packaged scene asset '{PackagedScenePath}' to exist.");

        using FileStream stream = File.OpenRead(PackagedScenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        using Core core = new Core(new CoreInitializationOptions {
            ContentRootPath = PackagedContentRootPath
        });
        core.Initialize(new TestRenderManager3D(ShaderCompileTarget.DirectX11), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        BepuRuntimeComponentRegistration.Register(core);

        RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
        IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);

        Assert.Single(core.ObjectManager.Cameras);
        Assert.Single(core.ObjectManager.DirectionalLights);
        Assert.True(core.ObjectManager.Drawables3D.Count >= 2, $"Expected at least two registered 3D drawables, but only found {core.ObjectManager.Drawables3D.Count}.");

        ICamera camera = Assert.Single(core.ObjectManager.Cameras);
        Assert.True(camera.RenderQueue3D.Count >= 2, $"Expected the runtime camera to own visible mesh drawables, but its 3D queue count is {camera.RenderQueue3D.Count}.");

        MeshComponent[] meshes = FindComponents<MeshComponent>(rootEntities).ToArray();
        Assert.True(meshes.Length >= 2, $"Expected at least two mesh components in the packaged runtime scene, but found {meshes.Length}.");

        for (int index = 0; index < meshes.Length; index++) {
            MeshComponent mesh = meshes[index];
            if (mesh.Model == null) {
                throw new XunitException($"Mesh #{index} did not resolve a runtime model.");
            }

            if (mesh.Materials == null || mesh.Materials.Length < 1) {
                throw new XunitException($"Mesh #{index} did not resolve any runtime materials.");
            }

            for (int materialIndex = 0; materialIndex < mesh.Materials.Length; materialIndex++) {
                if (mesh.Materials[materialIndex] == null) {
                    throw new XunitException($"Mesh #{index} material slot #{materialIndex} did not resolve a runtime material.");
                }
            }
        }
    }

    /// <summary>
    /// Enumerates every component of the requested type beneath the supplied entity subtree.
    /// </summary>
    /// <typeparam name="T">Requested component type.</typeparam>
    /// <param name="entities">Entity subtree to inspect.</param>
    /// <returns>Matching runtime components.</returns>
    static IEnumerable<T> FindComponents<T>(IReadOnlyList<Entity> entities) where T : Component {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }

        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            foreach (T component in FindComponents<T>(entities[entityIndex])) {
                yield return component;
            }
        }
    }

    /// <summary>
    /// Enumerates every component of the requested type beneath one runtime entity subtree.
    /// </summary>
    /// <typeparam name="T">Requested component type.</typeparam>
    /// <param name="entity">Entity subtree root to inspect.</param>
    /// <returns>Matching runtime components.</returns>
    static IEnumerable<T> FindComponents<T>(Entity entity) where T : Component {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
            if (entity.Components[componentIndex] is T component) {
                yield return component;
            }
        }

        for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
            foreach (T component in FindComponents<T>(entity.Children[childIndex])) {
                yield return component;
            }
        }
    }
}
