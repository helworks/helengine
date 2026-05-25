using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Runs the City stacked-box setup through BEPUphysics2.
    /// </summary>
    public sealed class BepuStackedBoxTraceRunner {
        /// <summary>
        /// Runs the BEPU reference scene and writes its traces.
        /// </summary>
        /// <param name="outputDirectoryPath">Output directory for CSV files.</param>
        /// <param name="stepCount">Number of fixed steps to simulate.</param>
        /// <param name="stepSeconds">Fixed step duration.</param>
        /// <returns>Body trace samples captured from BEPU.</returns>
        public IReadOnlyList<PhysicsTraceSample> Run(string outputDirectoryPath, int stepCount, float stepSeconds) {
            List<PhysicsTraceSample> samples = new List<PhysicsTraceSample>();
            using PhysicsTraceWriter traceWriter = new PhysicsTraceWriter(Path.Combine(outputDirectoryPath, "bepu-trace.csv"));
            using ContactTraceWriter contactWriter = new ContactTraceWriter(Path.Combine(outputDirectoryPath, "bepu-contacts.csv"));
            BepuContactTraceCollector collector = new BepuContactTraceCollector(contactWriter);
            BufferPool bufferPool = new BufferPool();
            Simulation simulation = Simulation.Create(bufferPool, new BepuNarrowPhaseCallbacks(collector), new BepuPoseIntegratorCallbacks(new Vector3(0f, -9.81f, 0f)), new SolveDescription(8, 1));

            Vector3[] positions = CreateTowerPositions();
            BodyHandle[] handles = new BodyHandle[positions.Length];
            for (int index = 0; index < positions.Length; index++) {
                handles[index] = CreateDynamicBox(simulation, positions[index]);
            }
            simulation.Statics.Add(new StaticDescription(new Vector3(0f, -0.5f, 0f), simulation.Shapes.Add(new Box(18f, 1f, 18f))));

            Vector3[] previousLinearVelocities = new Vector3[handles.Length];
            Vector3[] previousAngularVelocities = new Vector3[handles.Length];
            for (int stepIndex = 0; stepIndex <= stepCount; stepIndex++) {
                for (int bodyIndex = 0; bodyIndex < handles.Length; bodyIndex++) {
                    BodyReference body = simulation.Bodies[handles[bodyIndex]];
                    CaptureSample(samples, traceWriter, "bepu", stepIndex, stepSeconds, CreateBodyName(bodyIndex), body, previousLinearVelocities[bodyIndex], previousAngularVelocities[bodyIndex]);
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
        /// Creates the same eight-box tower used by the City physics demo scene.
        /// </summary>
        /// <returns>Initial box center positions.</returns>
        static Vector3[] CreateTowerPositions() {
            return new[] {
                new Vector3(0f, 1f, 0f),
                new Vector3(0.9f, 3f, 0f),
                new Vector3(-0.45f, 5f, 0f),
                new Vector3(0.45f, 7f, 0f),
                new Vector3(-0.25f, 9f, 0f),
                new Vector3(0.25f, 11f, 0f),
                new Vector3(-0.1f, 13f, 0f),
                new Vector3(0.1f, 15f, 0f)
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
