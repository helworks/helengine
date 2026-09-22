using System.Reflection;

namespace helengine {
    /// <summary>Exercises HelPhysics registration through the real SceneManager callback order.</summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsSceneManagerLifecycleTests {
        /// <summary>Verifies initialized entities bind on SceneLoaded, update, unload, and reload safely.</summary>
        [Fact]
        public void SceneLoadedEvent_AfterHierarchyInitialization_BindsBeforeCoreUpdate() {
            using Core core = CreateInitializedCore();
            Entity root = HelPhysicsTestSceneFactory3D.CreateEntity(core, float3.Zero);
            Entity trigger = HelPhysicsTestSceneFactory3D.CreateEntity(core, float3.Zero);
            trigger.AddComponent(new RigidBody3DComponent { BodyKind = BodyKind3D.Static });
            trigger.AddComponent(new SphereCollider3DComponent { Radius = 0.5f, IsTrigger = true });
            trigger.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = 100u });
            SceneEntityTriggerObserverComponent observer = new SceneEntityTriggerObserverComponent {
                TargetEntityReference = new SceneEntityReference { EntityId = 200u }
            };
            trigger.AddComponent(observer);
            Entity target = HelPhysicsTestSceneFactory3D.CreateEntity(core, float3.Zero);
            target.AddComponent(new RigidBody3DComponent { BodyKind = BodyKind3D.Dynamic, UseGravity = true });
            target.AddComponent(new SphereCollider3DComponent { Radius = 0.25f });
            target.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = 200u });
            root.AddChild(trigger);
            root.AddChild(target);

            root.InitializeHierarchy();
            HelPhysicsRuntimeComponentRegistration.Register(core);
            Dispatch(core.SceneManager, "DispatchSceneLoaded", new SceneLoadedEventArgs(
                "lifecycle", "lifecycle.scene", [root]));

            HelPhysicsRuntime3D firstRuntime = Assert.IsType<HelPhysicsRuntime3D>(core.PhysicsRuntime);
            float initialTargetY = target.Position.Y;
            double stepSeconds = core.PhysicsScheduler.StepSeconds;
            core.Update(stepSeconds);
            core.Update(stepSeconds);
            Assert.True(observer.GetIsTriggered());
            Assert.True(target.Position.Y < initialTargetY);
            Assert.Equal(1, core.LastPhysicsStepCount);

            Dispatch(core.SceneManager, "DispatchSceneUnloading", new SceneUnloadingEventArgs(
                "lifecycle", "lifecycle.scene", [root]));
            Assert.Null(core.PhysicsRuntime);

            Dispatch(core.SceneManager, "DispatchSceneLoaded", new SceneLoadedEventArgs(
                "lifecycle-reload", "lifecycle-reload.scene", [root]));
            Assert.NotSame(firstRuntime, Assert.IsType<HelPhysicsRuntime3D>(core.PhysicsRuntime));
        }

        /// <summary>Creates an initialized core with an empty runtime scene catalog.</summary>
        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = new RuntimeSceneCatalog(Array.Empty<RuntimeSceneCatalogEntry>())
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>Invokes one private SceneManager dispatch method to exercise its public event subscribers.</summary>
        static void Dispatch<TEventArgs>(SceneManager manager, string methodName, TEventArgs eventArgs) {
            MethodInfo dispatch = typeof(SceneManager).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(dispatch);
            dispatch.Invoke(manager, [eventArgs]);
        }
    }
}
