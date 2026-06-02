using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Runs the authored four-box dynamic stack scene through BEPU at fixed simulation-step boundaries.
    /// </summary>
    public sealed class BepuDynamicStackBoxesTraceRunner {
        /// <summary>
        /// Runs the BEPU reference scene and writes its fixed-step traces.
        /// </summary>
        /// <param name="outputDirectoryPath">Output directory for CSV files.</param>
        /// <param name="stepCount">Number of fixed steps to simulate.</param>
        /// <param name="stepSeconds">Fixed step duration.</param>
        /// <returns>Body trace samples captured from BEPU.</returns>
        public IReadOnlyList<PhysicsTraceSample> Run(string outputDirectoryPath, int stepCount, float stepSeconds) {
            List<PhysicsTraceSample> samples = new List<PhysicsTraceSample>();
            using PhysicsTraceWriter traceWriter = new PhysicsTraceWriter(Path.Combine(outputDirectoryPath, "bepu-dynamic-stack-trace.csv"));
            using ContactTraceWriter contactWriter = new ContactTraceWriter(Path.Combine(outputDirectoryPath, "bepu-dynamic-stack-contacts.csv"));
            BepuContactTraceCollector collector = new BepuContactTraceCollector(contactWriter);
            BufferPool bufferPool = new BufferPool();
            Simulation simulation = Simulation.Create(bufferPool, new BepuNarrowPhaseCallbacks(collector), new BepuPoseIntegratorCallbacks(new Vector3(0f, -9.81f, 0f)), new SolveDescription(8, 1));

            Vector3[] positions = DynamicStackBoxesSceneDefinition.CreateBoxPositions();
            BodyHandle[] handles = new BodyHandle[positions.Length];
            for (int index = 0; index < positions.Length; index++) {
                handles[index] = CreateDynamicBox(simulation, positions[index]);
            }

            Vector3 groundPosition = DynamicStackBoxesSceneDefinition.CreateGroundPosition();
            Vector3 groundSize = DynamicStackBoxesSceneDefinition.CreateGroundSize();
            simulation.Statics.Add(new StaticDescription(groundPosition, simulation.Shapes.Add(new Box(groundSize.X, groundSize.Y, groundSize.Z))));

            Vector3[] previousLinearVelocities = new Vector3[handles.Length];
            Vector3[] previousAngularVelocities = new Vector3[handles.Length];
            for (int stepIndex = 0; stepIndex <= stepCount; stepIndex++) {
                for (int bodyIndex = 0; bodyIndex < handles.Length; bodyIndex++) {
                    BodyReference body = simulation.Bodies[handles[bodyIndex]];
                    CaptureSample(samples, traceWriter, "bepu-dynamic-stack", stepIndex, stepSeconds, DynamicStackBoxesSceneDefinition.CreateBodyName(bodyIndex), body, previousLinearVelocities[bodyIndex], previousAngularVelocities[bodyIndex]);
                    previousLinearVelocities[bodyIndex] = body.Velocity.Linear;
                    previousAngularVelocities[bodyIndex] = body.Velocity.Angular;
                }

                if (stepIndex < stepCount) {
                    collector.CurrentStepIndex = stepIndex + 1;
                    simulation.Timestep(stepSeconds);
                }
            }

            simulation.Dispose();
            bufferPool.Clear();
            return samples;
        }

        /// <summary>
        /// Creates one unit dynamic box body.
        /// </summary>
        /// <param name="simulation">Simulation receiving the body.</param>
        /// <param name="position">Initial body position.</param>
        /// <returns>Handle of the created body.</returns>
        static BodyHandle CreateDynamicBox(Simulation simulation, Vector3 position) {
            Box box = new Box(1f, 1f, 1f);
            return simulation.Bodies.Add(BodyDescription.CreateConvexDynamic(new RigidPose(position), 1f, simulation.Shapes, box));
        }

        /// <summary>
        /// Captures one body state sample from BEPU.
        /// </summary>
        /// <param name="samples">Sample list to append.</param>
        /// <param name="traceWriter">Trace writer receiving the row.</param>
        /// <param name="engineName">Trace engine name.</param>
        /// <param name="stepIndex">Fixed-step index.</param>
        /// <param name="stepSeconds">Fixed step duration.</param>
        /// <param name="bodyName">Body label.</param>
        /// <param name="body">BEPU body reference.</param>
        /// <param name="previousLinearVelocity">Previous linear velocity.</param>
        /// <param name="previousAngularVelocity">Previous angular velocity.</param>
        static void CaptureSample(
            List<PhysicsTraceSample> samples,
            PhysicsTraceWriter traceWriter,
            string engineName,
            int stepIndex,
            float stepSeconds,
            string bodyName,
            BodyReference body,
            Vector3 previousLinearVelocity,
            Vector3 previousAngularVelocity) {
            PhysicsTraceSample sample = new PhysicsTraceSample {
                EngineName = engineName,
                StepIndex = stepIndex,
                TimeSeconds = stepIndex * stepSeconds,
                BodyName = bodyName,
                Position = body.Pose.Position,
                Orientation = body.Pose.Orientation,
                LinearVelocity = body.Velocity.Linear,
                AngularVelocity = body.Velocity.Angular,
                LinearForceApproximation = (body.Velocity.Linear - previousLinearVelocity) / stepSeconds,
                AngularForceApproximation = (body.Velocity.Angular - previousAngularVelocity) / stepSeconds
            };
            samples.Add(sample);
            traceWriter.Write(sample);
        }
    }
}
