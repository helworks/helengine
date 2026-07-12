using System.Reflection;
using helengine.editor.tests.testing;
using Xunit.Sdk;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the packaged Tilt Trial Windows scene keeps the playable sphere motion continuous while the real gameplay update path drives physics.
/// </summary>
public sealed class CityTiltTrialPackagedSceneRuntimeTests {
    /// <summary>
    /// Absolute packaged scene path for the current Windows Tilt Trial build.
    /// </summary>
    const string PackagedScenePath = @"C:\dev\helprojs\city\windows-build\cooked\scenes\games\tilt_trial.hasset";

    /// <summary>
    /// Absolute packaged scene path for the first dedicated Tilt Trial gameplay level.
    /// </summary>
    const string PackagedLevel01ScenePath = @"C:\dev\helprojs\city\windows-build\cooked\scenes\games\tilt_trial_level_01.hasset";

    /// <summary>
    /// Absolute packaged content root used to resolve cooked runtime assets.
    /// </summary>
    const string PackagedContentRootPath = @"C:\dev\helprojs\city\windows-build";

    /// <summary>
    /// Absolute managed gameplay assembly path used by runtime deserialization of city-authored script components during editor test execution.
    /// </summary>
    const string GameplayAssemblyPath = @"C:\dev\helprojs\output\city\generated_code\bin\gameplay\Debug\net9.0\gameplay.dll";

    /// <summary>
    /// Maximum allowed world-space center travel for one 60 Hz frame before the ball is considered to have teleported.
    /// </summary>
    const float MaximumAllowedFrameTravel = 0.2f;

    /// <summary>
    /// Ensures the packaged Tilt Trial sphere does not jump by an implausibly large amount while forward input is held across real runtime update and physics steps.
    /// </summary>
    [Fact]
    public void Windows_packaged_tilt_trial_scene_keeps_player_sphere_motion_continuous_while_holding_forward_input() {
        Assert.True(File.Exists(PackagedScenePath), $"Expected packaged scene asset '{PackagedScenePath}' to exist.");
        Assert.True(File.Exists(GameplayAssemblyPath), $"Expected gameplay assembly '{GameplayAssemblyPath}' to exist before loading Tilt Trial runtime components.");

        EnsureGameplayAssemblyLoaded();

        using FileStream stream = File.OpenRead(PackagedScenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        using Core core = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(PackagedContentRootPath),
            PhysicsFixedStepSeconds = 1.0d / 60.0d
        });

        TestInputBackend inputBackend = new TestInputBackend();
        core.Initialize(new TestRenderManager3D(ShaderCompileTarget.DirectX11), new TestRenderManager2D(), inputBackend, new PlatformInfo("test", "test-version"));
        core.InputSystem.SetKeyboardActive(true);
        BepuRuntimeComponentRegistration.Register(core);

        RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
        IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
        InvokeBepuLoadedSceneBinding(core, rootEntities);

        uint playerSphereSceneEntityId = FindRequiredSceneEntityAssetIdByName(sceneAsset.RootEntities, "PlayerSphere");
        Entity playerSphere = FindRequiredRuntimeEntityBySceneId(rootEntities, playerSphereSceneEntityId);
        RigidBody3DComponent rigidBody = FindRequiredRigidBody(playerSphere);

        for (int settleFrameIndex = 0; settleFrameIndex < 5; settleFrameIndex++) {
            inputBackend.SetKeyboardState(new KeyboardState());
            core.Update(1.0d / 60.0d);
        }

        float3 previousPosition = playerSphere.Position;
        float maximumObservedFrameTravel = 0f;
        int maximumObservedFrameIndex = -1;

        for (int frameIndex = 0; frameIndex < 45; frameIndex++) {
            inputBackend.SetKeyboardState(new KeyboardState(Keys.W));
            core.Update(1.0d / 60.0d);

            float3 currentPosition = playerSphere.Position;
            float frameTravel = (currentPosition - previousPosition).Length();
            if (frameTravel > maximumObservedFrameTravel) {
                maximumObservedFrameTravel = frameTravel;
                maximumObservedFrameIndex = frameIndex;
            }

            Assert.True(
                frameTravel <= MaximumAllowedFrameTravel,
                $"Expected Tilt Trial sphere travel to stay at or below {MaximumAllowedFrameTravel} units per 60 Hz frame, but frame {frameIndex} moved {frameTravel} units from ({previousPosition.X}, {previousPosition.Y}, {previousPosition.Z}) to ({currentPosition.X}, {currentPosition.Y}, {currentPosition.Z}) with rigid-body velocity ({rigidBody.GetLinearVelocity().X}, {rigidBody.GetLinearVelocity().Y}, {rigidBody.GetLinearVelocity().Z}).");

            previousPosition = currentPosition;
        }

        Assert.True(
            maximumObservedFrameTravel > 0f,
            $"Expected the Tilt Trial sphere to move while holding forward input, but the maximum observed frame travel after settling was {maximumObservedFrameTravel} on frame {maximumObservedFrameIndex}.");
    }

    /// <summary>
    /// Ensures the packaged first Tilt Trial gameplay level can complete one real runtime update without session dependency resolution crashing.
    /// </summary>
    [Fact]
    public void Windows_packaged_tilt_trial_level_01_scene_updates_without_session_dependency_resolution_crashing() {
        Assert.True(File.Exists(PackagedLevel01ScenePath), $"Expected packaged scene asset '{PackagedLevel01ScenePath}' to exist.");
        Assert.True(File.Exists(GameplayAssemblyPath), $"Expected gameplay assembly '{GameplayAssemblyPath}' to exist before loading Tilt Trial runtime components.");

        EnsureGameplayAssemblyLoaded();

        using FileStream stream = File.OpenRead(PackagedLevel01ScenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        using Core core = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(PackagedContentRootPath),
            PhysicsFixedStepSeconds = 1.0d / 60.0d
        });

        TestInputBackend inputBackend = new TestInputBackend();
        core.Initialize(new TestRenderManager3D(ShaderCompileTarget.DirectX11), new TestRenderManager2D(), inputBackend, new PlatformInfo("test", "test-version"));
        core.InputSystem.SetKeyboardActive(true);
        BepuRuntimeComponentRegistration.Register(core);

        RuntimeSceneLoadService sceneLoadService = new RuntimeSceneLoadService(core.SceneAssetReferenceResolver, core.SceneRuntimeComponentRegistry);
        IReadOnlyList<Entity> rootEntities = sceneLoadService.Load(sceneAsset);
        InvokeBepuLoadedSceneBinding(core, rootEntities);

        Exception exception = Record.Exception(() => core.Update(1.0d / 60.0d));
        Assert.Null(exception);
    }

    /// <summary>
    /// Loads the city gameplay assembly once so automatic runtime deserialization can materialize Tilt Trial script components by assembly-qualified name.
    /// </summary>
    static void EnsureGameplayAssemblyLoaded() {
        string expectedAssemblyName = Path.GetFileNameWithoutExtension(GameplayAssemblyPath);
        Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int index = 0; index < loadedAssemblies.Length; index++) {
            Assembly assembly = loadedAssemblies[index];
            if (string.Equals(assembly.GetName().Name, expectedAssemblyName, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
        }

        Assembly.LoadFrom(GameplayAssemblyPath);
    }

    /// <summary>
    /// Invokes the internal BEPU loaded-scene binder so packaged scene tests can attach the runtime physics world without depending on host-only scene-manager events.
     /// </summary>
    /// <param name="core">Core whose physics runtime should be attached.</param>
    /// <param name="rootEntities">Runtime scene root entities that should be scanned for physics components.</param>
    static void InvokeBepuLoadedSceneBinding(Core core, IReadOnlyList<Entity> rootEntities) {
        MethodInfo binder = typeof(BepuRuntimeComponentRegistration).GetMethod(
            "HandleLoadedScene",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(binder);
        binder.Invoke(null, new object[] { core, rootEntities });
    }

    /// <summary>
    /// Finds one authored scene entity id by entity name across the supplied scene-asset subtree.
    /// </summary>
    /// <param name="entities">Scene-asset root entities to inspect.</param>
    /// <param name="entityName">Entity name to search for.</param>
    /// <returns>Matching scene entity id.</returns>
    static uint FindRequiredSceneEntityAssetIdByName(IReadOnlyList<SceneEntityAsset> entities, string entityName) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        } else if (string.IsNullOrWhiteSpace(entityName)) {
            throw new ArgumentException("An entity name must be provided.", nameof(entityName));
        }

        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            SceneEntityAsset match = FindRequiredSceneEntityAssetByNameRecursive(entities[entityIndex], entityName);
            if (match != null) {
                return match.Id;
            }
        }

        throw new XunitException($"Expected one authored scene entity named '{entityName}'.");
    }

    /// <summary>
    /// Recursively finds one authored scene entity with the requested name.
    /// </summary>
    /// <param name="entity">Current authored scene subtree root.</param>
    /// <param name="entityName">Entity name to search for.</param>
    /// <returns>Matching scene entity asset, or <c>null</c> when absent from the subtree.</returns>
    static SceneEntityAsset FindRequiredSceneEntityAssetByNameRecursive(SceneEntityAsset entity, string entityName) {
        if (entity == null) {
            return null;
        }

        if (string.Equals(entity.Name, entityName, StringComparison.Ordinal)) {
            return entity;
        }

        if (entity.Children == null) {
            return null;
        }

        for (int childIndex = 0; childIndex < entity.Children.Length; childIndex++) {
            SceneEntityAsset match = FindRequiredSceneEntityAssetByNameRecursive(entity.Children[childIndex], entityName);
            if (match != null) {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds one runtime entity with the requested authored scene id across the supplied runtime subtree.
     /// </summary>
    /// <param name="entities">Runtime root entities to inspect.</param>
    /// <param name="sceneEntityId">Authored scene entity id to search for.</param>
    /// <returns>Matching runtime entity.</returns>
    static Entity FindRequiredRuntimeEntityBySceneId(IReadOnlyList<Entity> entities, uint sceneEntityId) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        } else if (sceneEntityId == 0u) {
            throw new ArgumentOutOfRangeException(nameof(sceneEntityId), "A non-zero scene entity id must be provided.");
        }

        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            Entity match = FindRequiredRuntimeEntityBySceneIdRecursive(entities[entityIndex], sceneEntityId);
            if (match != null) {
                return match;
            }
        }

        throw new XunitException($"Expected one runtime entity with authored scene id {sceneEntityId}.");
    }

    /// <summary>
    /// Recursively finds one runtime entity with the requested authored scene id.
    /// </summary>
    /// <param name="entity">Current runtime subtree root.</param>
    /// <param name="sceneEntityId">Authored scene entity id to search for.</param>
    /// <returns>Matching runtime entity, or <c>null</c> when absent from the subtree.</returns>
    static Entity FindRequiredRuntimeEntityBySceneIdRecursive(Entity entity, uint sceneEntityId) {
        if (entity == null) {
            return null;
        }

        if (FindSceneEntityRuntimeIdOrZero(entity) == sceneEntityId) {
            return entity;
        }

        for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
            Entity match = FindRequiredRuntimeEntityBySceneIdRecursive(entity.Children[childIndex], sceneEntityId);
            if (match != null) {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the authored scene id attached to one runtime entity when present.
    /// </summary>
    /// <param name="entity">Runtime entity to inspect.</param>
    /// <returns>Resolved authored scene id, or <c>0</c> when absent.</returns>
    static uint FindSceneEntityRuntimeIdOrZero(Entity entity) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
            if (entity.Components[componentIndex] is SceneEntityRuntimeIdComponent runtimeIdComponent) {
                return runtimeIdComponent.SceneEntityId;
            }
        }

        return 0u;
    }

    /// <summary>
    /// Finds the rigid body attached to the supplied runtime entity.
    /// </summary>
    /// <param name="entity">Runtime entity whose rigid body should be returned.</param>
    /// <returns>Attached rigid body.</returns>
    static RigidBody3DComponent FindRequiredRigidBody(Entity entity) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }

        for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
            if (entity.Components[componentIndex] is RigidBody3DComponent rigidBody) {
                return rigidBody;
            }
        }

        throw new XunitException($"Expected runtime entity with authored scene id {FindSceneEntityRuntimeIdOrZero(entity)} to expose a {nameof(RigidBody3DComponent)}.");
    }
}

