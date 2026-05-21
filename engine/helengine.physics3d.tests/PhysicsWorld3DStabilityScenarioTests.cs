namespace helengine.physics3d.tests {
    /// <summary>
    /// Runs compact deterministic physics scenarios that guard against stability regressions.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DStabilityScenarioTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed physics stability tests.
        /// </summary>
        public PhysicsWorld3DStabilityScenarioTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Leaves the active core singleton attached after each stability scenario test.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures an edge-supported cube changes orientation within three seconds.
        /// </summary>
        [Fact]
        public void EdgeSupportedCube_ChangesOrientationWithinThreeSeconds() {
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            RigidBody3DComponent body = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            Entity support = CreateBox(new float3(-0.34f, 0.5f, -0.06f), BodyKind3D.Static);
            Entity dynamic = CreateBox(new float3(0.50f, 1.62f, 0.06f), body);
            world.BindScene(new[] {
                support,
                dynamic
            });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), dynamic.LocalOrientation);
            Assert.True(up.Y < 0.98f);
        }

        /// <summary>
        /// Ensures the exported city cube-stack setup does not let a dynamic cube freeze while visibly tilted.
        /// </summary>
        [Fact]
        public void CityOffsetStackCube_DoesNotTeleportAfterVisibleTilt() {
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            Entity ground = CreateSizedBox(new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), BodyKind3D.Static);
            Entity lower = CreateSizedBox(new float3(0f, 1f, 0f), float3.One, BodyKind3D.Dynamic);
            Entity upper = CreateSizedBox(new float3(0.9f, 3f, 0f), float3.One, BodyKind3D.Dynamic);
            world.BindScene(new[] {
                ground,
                lower,
                upper
            });

            bool observedVisibleTilt = false;
            float previousHeight = upper.LocalPosition.Y;
            float largestUpwardStepAfterTilt = 0f;
            float previousUpY = 1f;
            float largestUpYStepAfterTilt = 0f;
            for (int index = 0; index < 420; index++) {
                world.Step(1.0 / 60.0);
                float3 currentUp = float4.RotateVector(new float3(0f, 1f, 0f), upper.LocalOrientation);
                if (Math.Abs(currentUp.Y) < 0.995f) {
                    observedVisibleTilt = true;
                }
                if (observedVisibleTilt) {
                    largestUpwardStepAfterTilt = Math.Max(largestUpwardStepAfterTilt, upper.LocalPosition.Y - previousHeight);
                    largestUpYStepAfterTilt = Math.Max(largestUpYStepAfterTilt, Math.Abs(currentUp.Y - previousUpY));
                }

                previousHeight = upper.LocalPosition.Y;
                previousUpY = currentUp.Y;
            }

            Assert.True(observedVisibleTilt);
            Assert.True(largestUpwardStepAfterTilt < 0.08f, $"Upper cube teleported upward by {largestUpwardStepAfterTilt} after visible tilt.");
            Assert.True(largestUpYStepAfterTilt < 0.25f, $"Upper cube orientation snapped by {largestUpYStepAfterTilt} after visible tilt.");
        }

        /// <summary>
        /// Ensures a barely supported cube topples off the support within a small number of seconds instead of creeping down.
        /// </summary>
        [Fact]
        public void CityOffsetStackCube_TopplesOffSupportWithinThreeSeconds() {
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            Entity ground = CreateSizedBox(new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), BodyKind3D.Static);
            Entity lower = CreateSizedBox(new float3(0f, 1f, 0f), float3.One, BodyKind3D.Dynamic);
            Entity upper = CreateSizedBox(new float3(0.9f, 3f, 0f), float3.One, BodyKind3D.Dynamic);
            world.BindScene(new[] {
                ground,
                lower,
                upper
            });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            float3 upperUp = float4.RotateVector(new float3(0f, 1f, 0f), upper.LocalOrientation);

            Assert.True(upper.LocalPosition.Y < 0.58f, $"Upper cube was still high at Y {upper.LocalPosition.Y} with up.Y {upperUp.Y}.");
            Assert.True(Math.Abs(upperUp.Y) > 0.99f, $"Upper cube was not flat after toppling. up.Y was {upperUp.Y}.");
        }

        /// <summary>
        /// Creates a box entity with a new rigid body of the requested kind.
        /// </summary>
        /// <param name="position">Initial local position.</param>
        /// <param name="bodyKind">Rigid body kind.</param>
        /// <returns>Initialized box entity.</returns>
        static Entity CreateBox(float3 position, BodyKind3D bodyKind) {
            RigidBody3DComponent body = new RigidBody3DComponent {
                BodyKind = bodyKind,
                UseGravity = bodyKind == BodyKind3D.Dynamic,
                Mass = 1d
            };
            return CreateBox(position, body);
        }

        /// <summary>
        /// Creates a box entity with an existing rigid body component.
        /// </summary>
        /// <param name="position">Initial local position.</param>
        /// <param name="body">Rigid body component.</param>
        /// <returns>Initialized box entity.</returns>
        static Entity CreateBox(float3 position, RigidBody3DComponent body) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }

            Entity entity = new Entity {
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            entity.InitComponents();
            entity.InitChildren();
            entity.AddComponent(body);
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });
            return entity;
        }

        /// <summary>
        /// Creates a box entity with an authored collider size.
        /// </summary>
        /// <param name="position">Initial local position.</param>
        /// <param name="size">Full collider size.</param>
        /// <param name="bodyKind">Rigid body kind.</param>
        /// <returns>Initialized box entity.</returns>
        static Entity CreateSizedBox(float3 position, float3 size, BodyKind3D bodyKind) {
            Entity entity = CreateBox(position, bodyKind);
            entity.Components.OfType<BoxCollider3DComponent>().Single().Size = size;
            return entity;
        }
    }
}
