using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Runs the City stacked-box setup through helengine physics.
    /// </summary>
    public sealed class HelengineStackedBoxTraceRunner {
        /// <summary>
        /// Runs the helengine scene and writes its traces.
        /// </summary>
        /// <param name="outputDirectoryPath">Output directory for CSV files.</param>
        /// <param name="stepCount">Number of fixed steps to simulate.</param>
        /// <param name="stepSeconds">Fixed step duration.</param>
        /// <returns>Body trace samples captured from helengine.</returns>
        public IReadOnlyList<PhysicsTraceSample> Run(string outputDirectoryPath, int stepCount, float stepSeconds) {
            List<PhysicsTraceSample> samples = new List<PhysicsTraceSample>();
            using PhysicsTraceWriter traceWriter = new PhysicsTraceWriter(Path.Combine(outputDirectoryPath, "helengine-trace.csv"));
            using HelengineContactTraceWriter contactTraceWriter = new HelengineContactTraceWriter(Path.Combine(outputDirectoryPath, "helengine-contacts.csv"));
            helengine.Core core = new helengine.Core(new helengine.CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new helengine.PlatformInfo("trace", "trace-version"));

            try {
                helengine.Entity groundEntity = CreateBoxEntity(new helengine.float3(0f, -0.5f, 0f), helengine.BodyKind3D.Static, false, new helengine.float3(18f, 1f, 18f));
                helengine.float3[] positions = CreateTowerPositions();
                helengine.Entity[] boxEntities = new helengine.Entity[positions.Length];
                helengine.RigidBody3DComponent[] boxBodies = new helengine.RigidBody3DComponent[positions.Length];
                for (int index = 0; index < positions.Length; index++) {
                    boxEntities[index] = CreateBoxEntity(positions[index], helengine.BodyKind3D.Dynamic, true, new helengine.float3(1f, 1f, 1f));
                    boxBodies[index] = FindRigidBody(boxEntities[index]);
                }

                helengine.Entity[] rootEntities = new helengine.Entity[boxEntities.Length + 1];
                rootEntities[0] = groundEntity;
                for (int index = 0; index < boxEntities.Length; index++) {
                    rootEntities[index + 1] = boxEntities[index];
                }

                helengine.PhysicsWorld3D world = helengine.PhysicsWorld3D.CreateMediumDefault();
                world.BindScene(rootEntities);

                Vector3[] previousLinearVelocities = new Vector3[boxEntities.Length];
                Vector3[] previousAngularVelocities = new Vector3[boxEntities.Length];
                for (int stepIndex = 0; stepIndex <= stepCount; stepIndex++) {
                    for (int bodyIndex = 0; bodyIndex < boxEntities.Length; bodyIndex++) {
                        CaptureSample(samples, traceWriter, "helengine", stepIndex, stepSeconds, CreateBodyName(bodyIndex), boxEntities[bodyIndex], boxBodies[bodyIndex], previousLinearVelocities[bodyIndex], previousAngularVelocities[bodyIndex]);
                        previousLinearVelocities[bodyIndex] = Convert(boxBodies[bodyIndex].LinearVelocity);
                        previousAngularVelocities[bodyIndex] = Convert(boxBodies[bodyIndex].AngularVelocity);
                    }

                    CaptureContacts(contactTraceWriter, stepIndex, world, stepSeconds);
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
        /// Creates the same eight-box tower used by the City physics demo scene.
        /// </summary>
        /// <returns>Initial box center positions.</returns>
        static helengine.float3[] CreateTowerPositions() {
            return new[] {
                new helengine.float3(0f, 1f, 0f),
                new helengine.float3(0.9f, 3f, 0f),
                new helengine.float3(-0.45f, 5f, 0f),
                new helengine.float3(0.45f, 7f, 0f),
                new helengine.float3(-0.25f, 9f, 0f),
                new helengine.float3(0.25f, 11f, 0f),
                new helengine.float3(-0.1f, 13f, 0f),
                new helengine.float3(0.1f, 15f, 0f)
            };
        }

        /// <summary>
        /// Creates the stable trace label for one tower body index.
        /// </summary>
        /// <param name="bodyIndex">Zero-based tower body index.</param>
        /// <returns>Trace body label.</returns>
        static string CreateBodyName(int bodyIndex) {
            return "box" + (bodyIndex + 1).ToString("00");
        }

        /// <summary>
        /// Reconstructs box-box contact state from the public body states after the current step.
        /// </summary>
        /// <param name="contactTraceWriter">Writer receiving reconstructed contact rows.</param>
        /// <param name="stepIndex">Simulation step being captured.</param>
        /// <param name="world">Helengine physics world containing body states.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        static void CaptureContacts(HelengineContactTraceWriter contactTraceWriter, int stepIndex, helengine.PhysicsWorld3D world, float stepSeconds) {
            for (int firstIndex = 0; firstIndex < world.BodyStates.Count; firstIndex++) {
                for (int secondIndex = firstIndex + 1; secondIndex < world.BodyStates.Count; secondIndex++) {
                    float margin = ResolveSpeculativeContactMargin(world.BodyStates[firstIndex], world.BodyStates[secondIndex], stepSeconds);
                    contactTraceWriter.Write(stepIndex, CreatePairName(firstIndex, secondIndex), world.BodyStates[firstIndex], world.BodyStates[secondIndex], margin);
                }
            }
        }

        /// <summary>
        /// Creates a stable pair name for two physics body-state indices.
        /// </summary>
        /// <param name="firstIndex">First body-state index, where zero is the ground.</param>
        /// <param name="secondIndex">Second body-state index, where zero is the ground.</param>
        /// <returns>Readable contact pair name.</returns>
        static string CreatePairName(int firstIndex, int secondIndex) {
            return CreateBodyStateName(firstIndex) + "-" + CreateBodyStateName(secondIndex);
        }

        /// <summary>
        /// Creates a stable body name from a physics body-state index.
        /// </summary>
        /// <param name="bodyStateIndex">Body-state index, where zero is the ground and one is the first dynamic box.</param>
        /// <returns>Readable body name.</returns>
        static string CreateBodyStateName(int bodyStateIndex) {
            if (bodyStateIndex == 0) {
                return "ground";
            }

            return CreateBodyName(bodyStateIndex - 1);
        }

        /// <summary>
        /// Reconstructs the speculative contact margin used by the runtime box-box pass.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Speculative contact margin for the body pair.</returns>
        static float ResolveSpeculativeContactMargin(helengine.BodyState3D first, helengine.BodyState3D second, float stepSeconds) {
            helengine.float3 relativeVelocity = first.Velocity - second.Velocity;
            double relativeSpeedSquared = (relativeVelocity.X * relativeVelocity.X) +
                (relativeVelocity.Y * relativeVelocity.Y) +
                (relativeVelocity.Z * relativeVelocity.Z);
            return Math.Max(0.05f, (float)(Math.Sqrt(relativeSpeedSquared) * stepSeconds));
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
