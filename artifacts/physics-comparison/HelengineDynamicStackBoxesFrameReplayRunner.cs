using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Replays the authored four-box dynamic stack scene through helengine at host-frame boundaries.
    /// </summary>
    public sealed class HelengineDynamicStackBoxesFrameReplayRunner {
        /// <summary>
        /// Runs the helengine scene over the supplied host-frame deltas and records one sample per rendered frame.
        /// </summary>
        /// <param name="outputDirectoryPath">Directory receiving the CSV trace.</param>
        /// <param name="frameDeltas">Ordered host-frame deltas in seconds.</param>
        /// <returns>Captured per-frame samples.</returns>
        public IReadOnlyList<PhysicsTraceSample> Run(string outputDirectoryPath, IReadOnlyList<float> frameDeltas) {
            if (outputDirectoryPath == null) {
                throw new ArgumentNullException(nameof(outputDirectoryPath));
            }
            if (frameDeltas == null) {
                throw new ArgumentNullException(nameof(frameDeltas));
            }

            List<PhysicsTraceSample> samples = new List<PhysicsTraceSample>();
            using PhysicsTraceWriter traceWriter = new PhysicsTraceWriter(Path.Combine(outputDirectoryPath, "helengine-frame-replay-trace.csv"));
            helengine.Core core = new helengine.Core(new helengine.CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new helengine.PlatformInfo("trace", "trace-version"));

            try {
                Vector3 groundPosition = DynamicStackBoxesSceneDefinition.CreateGroundPosition();
                Vector3 groundSize = DynamicStackBoxesSceneDefinition.CreateGroundSize();
                helengine.Entity groundEntity = CreateBoxEntity(
                    new helengine.float3(groundPosition.X, groundPosition.Y, groundPosition.Z),
                    helengine.BodyKind3D.Static,
                    false,
                    new helengine.float3(groundSize.X, groundSize.Y, groundSize.Z));

                Vector3[] positions = DynamicStackBoxesSceneDefinition.CreateBoxPositions();
                helengine.Entity[] boxEntities = new helengine.Entity[positions.Length];
                helengine.RigidBody3DComponent[] boxBodies = new helengine.RigidBody3DComponent[positions.Length];
                for (int index = 0; index < positions.Length; index++) {
                    boxEntities[index] = CreateBoxEntity(
                        new helengine.float3(positions[index].X, positions[index].Y, positions[index].Z),
                        helengine.BodyKind3D.Dynamic,
                        true,
                        new helengine.float3(1f, 1f, 1f));
                    boxBodies[index] = FindRigidBody(boxEntities[index]);
                }

                helengine.Entity[] rootEntities = new helengine.Entity[boxEntities.Length + 1];
                rootEntities[0] = groundEntity;
                for (int index = 0; index < boxEntities.Length; index++) {
                    rootEntities[index + 1] = boxEntities[index];
                }

                helengine.PhysicsWorld3D world = helengine.PhysicsWorld3D.CreateMediumDefault();
                world.BindScene(rootEntities);
                core.AttachPhysicsRuntime(world);

                Vector3[] previousLinearVelocities = new Vector3[boxEntities.Length];
                Vector3[] previousAngularVelocities = new Vector3[boxEntities.Length];
                float elapsedTimeSeconds = 0f;
                for (int frameIndex = 0; frameIndex < frameDeltas.Count; frameIndex++) {
                    float frameDelta = frameDeltas[frameIndex];
                    core.Update(frameDelta);
                    elapsedTimeSeconds += frameDelta;
                    for (int bodyIndex = 0; bodyIndex < boxEntities.Length; bodyIndex++) {
                        CaptureSample(
                            samples,
                            traceWriter,
                            "helengine-frame-replay",
                            frameIndex,
                            elapsedTimeSeconds,
                            DynamicStackBoxesSceneDefinition.CreateBodyName(bodyIndex),
                            boxEntities[bodyIndex],
                            boxBodies[bodyIndex],
                            previousLinearVelocities[bodyIndex],
                            previousAngularVelocities[bodyIndex],
                            frameDelta);
                        previousLinearVelocities[bodyIndex] = Convert(boxBodies[bodyIndex].LinearVelocity);
                        previousAngularVelocities[bodyIndex] = Convert(boxBodies[bodyIndex].AngularVelocity);
                    }
                }
            } finally {
                core.Dispose();
            }

            return samples;
        }

        /// <summary>
        /// Creates one entity with rigid body and box collider components.
        /// </summary>
        /// <param name="position">Entity position.</param>
        /// <param name="bodyKind">Rigid body kind.</param>
        /// <param name="useGravity">True when gravity should affect the body.</param>
        /// <param name="size">Box collider size.</param>
        /// <returns>Created entity.</returns>
        static helengine.Entity CreateBoxEntity(helengine.float3 position, helengine.BodyKind3D bodyKind, bool useGravity, helengine.float3 size) {
            helengine.Entity entity = new helengine.Entity {
                LocalPosition = position
            };
            entity.InitComponents();
            entity.AddComponent(new helengine.RigidBody3DComponent {
                BodyKind = bodyKind,
                UseGravity = useGravity,
                Mass = 1d
            });
            entity.AddComponent(new helengine.BoxCollider3DComponent {
                Size = size,
                StaticFriction = 1d,
                DynamicFriction = 1d
            });
            return entity;
        }

        /// <summary>
        /// Finds the rigid body component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached rigid body component.</returns>
        static helengine.RigidBody3DComponent FindRigidBody(helengine.Entity entity) {
            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is helengine.RigidBody3DComponent rigidBody) {
                    return rigidBody;
                }
            }

            throw new InvalidOperationException("Entity does not contain a rigid body component.");
        }

        /// <summary>
        /// Captures one body-state sample from helengine.
        /// </summary>
        /// <param name="samples">Sample list to append.</param>
        /// <param name="traceWriter">Trace writer receiving the row.</param>
        /// <param name="engineName">Trace engine name.</param>
        /// <param name="frameIndex">Host-frame index.</param>
        /// <param name="timeSeconds">Elapsed frame time in seconds.</param>
        /// <param name="bodyName">Body label.</param>
        /// <param name="entity">Entity containing transform state.</param>
        /// <param name="body">Rigid body containing velocity state.</param>
        /// <param name="previousLinearVelocity">Previous linear velocity.</param>
        /// <param name="previousAngularVelocity">Previous angular velocity.</param>
        /// <param name="frameDeltaSeconds">Host-frame delta in seconds.</param>
        static void CaptureSample(
            List<PhysicsTraceSample> samples,
            PhysicsTraceWriter traceWriter,
            string engineName,
            int frameIndex,
            float timeSeconds,
            string bodyName,
            helengine.Entity entity,
            helengine.RigidBody3DComponent body,
            Vector3 previousLinearVelocity,
            Vector3 previousAngularVelocity,
            float frameDeltaSeconds) {
            float safeFrameDeltaSeconds = frameDeltaSeconds <= 0f ? 1f : frameDeltaSeconds;
            Vector3 linearVelocity = Convert(body.LinearVelocity);
            Vector3 angularVelocity = Convert(body.AngularVelocity);
            PhysicsTraceSample sample = new PhysicsTraceSample {
                EngineName = engineName,
                StepIndex = frameIndex,
                TimeSeconds = timeSeconds,
                BodyName = bodyName,
                Position = Convert(entity.LocalPosition),
                Orientation = Convert(entity.LocalOrientation),
                LinearVelocity = linearVelocity,
                AngularVelocity = angularVelocity,
                LinearForceApproximation = (linearVelocity - previousLinearVelocity) / safeFrameDeltaSeconds,
                AngularForceApproximation = (angularVelocity - previousAngularVelocity) / safeFrameDeltaSeconds
            };
            samples.Add(sample);
            traceWriter.Write(sample);
        }

        /// <summary>
        /// Converts a helengine vector to a system vector.
        /// </summary>
        /// <param name="value">Vector to convert.</param>
        /// <returns>Converted vector.</returns>
        static Vector3 Convert(helengine.float3 value) {
            return new Vector3(value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Converts a helengine quaternion to a system quaternion.
        /// </summary>
        /// <param name="value">Quaternion to convert.</param>
        /// <returns>Converted quaternion.</returns>
        static Quaternion Convert(helengine.float4 value) {
            return new Quaternion(value.X, value.Y, value.Z, value.W);
        }
    }
}
