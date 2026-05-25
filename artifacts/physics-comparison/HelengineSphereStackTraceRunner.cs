using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Runs the City sphere-stack setup through helengine physics.
    /// </summary>
    public sealed class HelengineSphereStackTraceRunner {
        /// <summary>
        /// Runs the helengine sphere-stack scene and writes its traces.
        /// </summary>
        /// <param name="outputDirectoryPath">Output directory for CSV files.</param>
        /// <param name="stepCount">Number of fixed steps to simulate.</param>
        /// <param name="stepSeconds">Fixed step duration.</param>
        /// <returns>Body trace samples captured from helengine.</returns>
        public IReadOnlyList<PhysicsTraceSample> Run(string outputDirectoryPath, int stepCount, float stepSeconds) {
            List<PhysicsTraceSample> samples = new List<PhysicsTraceSample>();
            using PhysicsTraceWriter traceWriter = new PhysicsTraceWriter(Path.Combine(outputDirectoryPath, "helengine-sphere-trace.csv"));
            helengine.Core core = new helengine.Core(new helengine.CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new helengine.PlatformInfo("trace", "trace-version"));

            try {
                helengine.Entity groundEntity = CreateBoxEntity(new helengine.float3(0f, -0.5f, 0f), helengine.BodyKind3D.Static, false, new helengine.float3(16f, 1f, 14f));
                helengine.float3[] positions = CreateStackPositions();
                helengine.Entity[] sphereEntities = new helengine.Entity[positions.Length];
                helengine.RigidBody3DComponent[] sphereBodies = new helengine.RigidBody3DComponent[positions.Length];
                for (int index = 0; index < positions.Length; index++) {
                    sphereEntities[index] = CreateSphereEntity(positions[index], helengine.BodyKind3D.Dynamic, true, 0.5f);
                    sphereBodies[index] = FindRigidBody(sphereEntities[index]);
                }

                helengine.Entity[] rootEntities = new helengine.Entity[sphereEntities.Length + 1];
                rootEntities[0] = groundEntity;
                for (int index = 0; index < sphereEntities.Length; index++) {
                    rootEntities[index + 1] = sphereEntities[index];
                }

                helengine.PhysicsWorld3D world = helengine.PhysicsWorld3D.CreateMediumDefault();
                world.BindScene(rootEntities);

                Vector3[] previousLinearVelocities = new Vector3[sphereEntities.Length];
                Vector3[] previousAngularVelocities = new Vector3[sphereEntities.Length];
                for (int stepIndex = 0; stepIndex <= stepCount; stepIndex++) {
                    for (int bodyIndex = 0; bodyIndex < sphereEntities.Length; bodyIndex++) {
                        CaptureSample(samples, traceWriter, "helengine", stepIndex, stepSeconds, CreateBodyName(bodyIndex), sphereEntities[bodyIndex], sphereBodies[bodyIndex], previousLinearVelocities[bodyIndex], previousAngularVelocities[bodyIndex]);
                        previousLinearVelocities[bodyIndex] = Convert(sphereBodies[bodyIndex].LinearVelocity);
                        previousAngularVelocities[bodyIndex] = Convert(sphereBodies[bodyIndex].AngularVelocity);
                    }

                    if (stepIndex < stepCount) {
                        world.Step(stepSeconds);
                    }
                }
            } finally {
                core.Dispose();
            }

            return samples;
        }

        /// <summary>
        /// Creates the same eight-sphere stack positions used by the City physics demo scene.
        /// </summary>
        /// <returns>Initial sphere center positions.</returns>
        static helengine.float3[] CreateStackPositions() {
            helengine.float3[] positions = new helengine.float3[8];
            for (int sphereIndex = 0; sphereIndex < positions.Length; sphereIndex++) {
                float staggerX = sphereIndex % 2 == 0 ? 0f : 0.08f;
                float staggerZ = sphereIndex % 3 == 0 ? -0.06f : 0.06f;
                positions[sphereIndex] = new helengine.float3(staggerX, 0.5f + sphereIndex, staggerZ);
            }

            return positions;
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
        /// Creates one entity with rigid body and sphere collider components.
        /// </summary>
        /// <param name="position">Entity position.</param>
        /// <param name="bodyKind">Rigid body kind.</param>
        /// <param name="useGravity">True when gravity should affect the body.</param>
        /// <param name="radius">Sphere collider radius.</param>
        /// <returns>Created entity.</returns>
        static helengine.Entity CreateSphereEntity(helengine.float3 position, helengine.BodyKind3D bodyKind, bool useGravity, float radius) {
            helengine.Entity entity = new helengine.Entity {
                LocalPosition = position
            };
            entity.InitComponents();
            entity.AddComponent(new helengine.RigidBody3DComponent {
                BodyKind = bodyKind,
                UseGravity = useGravity,
                Mass = 1d
            });
            entity.AddComponent(new helengine.SphereCollider3DComponent {
                Radius = radius,
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
        /// Creates the stable trace label for one sphere index.
        /// </summary>
        /// <param name="bodyIndex">Zero-based sphere index.</param>
        /// <returns>Trace body label.</returns>
        static string CreateBodyName(int bodyIndex) {
            return "sphere" + (bodyIndex + 1).ToString("00");
        }

        /// <summary>
        /// Captures one body state sample from helengine.
        /// </summary>
        /// <param name="samples">Sample list to append.</param>
        /// <param name="traceWriter">Trace writer receiving the row.</param>
        /// <param name="engineName">Trace engine name.</param>
        /// <param name="stepIndex">Fixed-step index.</param>
        /// <param name="stepSeconds">Fixed step duration.</param>
        /// <param name="bodyName">Body label.</param>
        /// <param name="entity">Entity containing transform state.</param>
        /// <param name="body">Rigid body containing velocity state.</param>
        /// <param name="previousLinearVelocity">Previous linear velocity.</param>
        /// <param name="previousAngularVelocity">Previous angular velocity.</param>
        static void CaptureSample(
            List<PhysicsTraceSample> samples,
            PhysicsTraceWriter traceWriter,
            string engineName,
            int stepIndex,
            float stepSeconds,
            string bodyName,
            helengine.Entity entity,
            helengine.RigidBody3DComponent body,
            Vector3 previousLinearVelocity,
            Vector3 previousAngularVelocity) {
            Vector3 linearVelocity = Convert(body.LinearVelocity);
            Vector3 angularVelocity = Convert(body.AngularVelocity);
            PhysicsTraceSample sample = new PhysicsTraceSample {
                EngineName = engineName,
                StepIndex = stepIndex,
                TimeSeconds = stepIndex * stepSeconds,
                BodyName = bodyName,
                Position = Convert(entity.LocalPosition),
                Orientation = Convert(entity.LocalOrientation),
                LinearVelocity = linearVelocity,
                AngularVelocity = angularVelocity,
                LinearForceApproximation = (linearVelocity - previousLinearVelocity) / stepSeconds,
                AngularForceApproximation = (angularVelocity - previousAngularVelocity) / stepSeconds
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
