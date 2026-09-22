namespace helengine {
    /// <summary>Regresses oriented top-face containment for controller support samples.</summary>
    public sealed class HelPhysicsControllerIntegrationRegressionTests {
        [Fact]
        public void ControllerSupport_RotatedBoxCornerOutsideTopFace_IsRejected() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Entity support = new Entity(core) { LocalPosition = new float3(0f, 0f, 0f) };
            support.InitComponents();
            support.InitChildren();
            support.AddComponent(new RigidBody3DComponent { BodyKind = BodyKind3D.Static, UseGravity = false });
            BoxCollider3DComponent supportCollider = new BoxCollider3DComponent { Size = new float3(2f, 1f, 2f) };
            support.AddComponent(supportCollider);
            float4 yaw;
            float4.CreateFromAxisAngle(new float3(0f, 1f, 0f), (float)(45d * Math.PI / 180d), out yaw);
            support.LocalOrientation = yaw;

            Entity controller = new Entity(core) { LocalPosition = new float3(1.2f, 1f, 1.2f) };
            controller.InitComponents();
            controller.InitChildren();
            controller.AddComponent(new BoxCollider3DComponent { Size = new float3(0.4f, 1.5f, 0.4f) });
            controller.AddComponent(new CharacterController3DComponent {
                StepHeight = 0.75d,
                GroundSnapDistance = 0.3d,
                MaximumSlopeDegrees = 45d
            });

            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { support, controller });
            HelPhysicsEntityBinding3D binding = runtime.Binder.GetBinding(controller);
            Assert.False(runtime.Binder.FindControllerSupport(binding, controller.Position, 0.75d).IsValid);
            runtime.Dispose();
        }

        [Fact]
        public void ControllerTarget_ZAndDiagonalMovement_BlocksSolidWall() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Entity wall = new Entity(core) { LocalPosition = new float3(0f, 1.5f, 1.5f) };
            wall.InitComponents();
            wall.InitChildren();
            wall.AddComponent(new RigidBody3DComponent { BodyKind = BodyKind3D.Static, UseGravity = false });
            wall.AddComponent(new BoxCollider3DComponent { Size = new float3(8f, 3f, 0.5f) });
            Entity controller = new Entity(core) { LocalPosition = new float3(0f, 1f, 0f) };
            controller.InitComponents();
            controller.InitChildren();
            controller.AddComponent(new BoxCollider3DComponent { Size = new float3(0.9f, 1.5f, 0.9f) });
            controller.AddComponent(new CharacterController3DComponent {
                DesiredMoveDirection = new float3(1f, 0f, 1f),
                MoveSpeed = 2d,
                StepHeight = 0.75d,
                GroundSnapDistance = 0.3d,
                MaximumSlopeDegrees = 45d
            });
            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { wall, controller });
            HelPhysicsEntityBinding3D binding = runtime.Binder.GetBinding(controller);
            float3 zTarget = runtime.Binder.ClampControllerTarget(binding, controller.Position, new float3(0f, 1f, 2f));
            float3 diagonalTarget = runtime.Binder.ClampControllerTarget(binding, controller.Position, new float3(2f, 1f, 2f));
            Assert.InRange(zTarget.Z, 0.7f, 0.801f);
            Assert.InRange(diagonalTarget.Z, 0.7f, 0.801f);
            runtime.Dispose();
        }

        /// <summary>Rejects an invalid later controller before an earlier controller queues a command.</summary>
        [Fact]
        public void ControllerBatch_InvalidLaterInput_DoesNotMutateEarlierController() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Entity first = new Entity(core) { LocalPosition = new float3(0f, 1f, 0f) };
            first.InitComponents();
            first.InitChildren();
            first.AddComponent(new BoxCollider3DComponent { Size = new float3(0.9f, 1.5f, 0.9f) });
            first.AddComponent(new CharacterController3DComponent {
                DesiredMoveDirection = new float3(1f, 0f, 0f),
                MoveSpeed = 2d
            });
            Entity second = new Entity(core) { LocalPosition = new float3(5f, 1f, 0f) };
            second.InitComponents();
            second.InitChildren();
            second.AddComponent(new BoxCollider3DComponent { Size = new float3(0.9f, 1.5f, 0.9f) });
            second.AddComponent(new CharacterController3DComponent {
                DesiredMoveDirection = new float3(float.NaN, 0f, 0f),
                MoveSpeed = 2d
            });
            CharacterController3DComponent secondController = null;
            for (int componentIndex = 0; componentIndex < second.Components.Count; componentIndex++) {
                if (second.Components[componentIndex] is CharacterController3DComponent value) {
                    secondController = value;
                }
            }
            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { first, second });
            HelPhysicsEntityBinding3D firstBinding = runtime.Binder.GetBinding(first);
            HelPhysicsBodySnapshot3D beforeSnapshot = firstBinding.GetBodySnapshot();
            float beforeVerticalVelocity = firstBinding.ControllerVerticalVelocity;
            float3 before = first.Position;
            Assert.Throws<InvalidOperationException>(() => runtime.Step(runtime.ConfiguredFixedStepSeconds));
            HelPhysicsBodySnapshot3D failedSnapshot = firstBinding.GetBodySnapshot();
            Assert.Equal(beforeSnapshot.Position, failedSnapshot.Position);
            Assert.Equal(beforeSnapshot.LinearVelocity, failedSnapshot.LinearVelocity);
            Assert.Equal(beforeVerticalVelocity, firstBinding.ControllerVerticalVelocity);
            secondController.DesiredMoveDirection = new float3(1f, 0f, 0f);
            runtime.Step(runtime.ConfiguredFixedStepSeconds);
            Assert.True(first.Position.X > before.X);
            runtime.Dispose();
        }
    }
}
