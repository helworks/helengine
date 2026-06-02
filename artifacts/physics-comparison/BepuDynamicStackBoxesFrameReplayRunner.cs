using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Replays the authored four-box dynamic stack scene through BEPU at host-frame boundaries.
    /// </summary>
    public sealed class BepuDynamicStackBoxesFrameReplayRunner {
        /// <summary>
        /// Runs the BEPU reference scene over the supplied host-frame deltas and records one sample per rendered frame.
        /// </summary>
        /// <param name="outputDirectoryPath">Directory receiving the CSV trace.</param>
        /// <param name="frameDeltas">Ordered host-frame deltas in seconds.</param>
        /// <param name="stepSeconds">Fixed physics step length in seconds.</param>
        /// <returns>Captured per-frame samples.</returns>
        public IReadOnlyList<PhysicsTraceSample> Run(string outputDirectoryPath, IReadOnlyList<float> frameDeltas, float stepSeconds) {
            if (outputDirectoryPath == null) {
                throw new ArgumentNullException(nameof(outputDirectoryPath));
            }
            if (frameDeltas == null) {
                throw new ArgumentNullException(nameof(frameDeltas));
            }

            List<PhysicsTraceSample> samples = new List<PhysicsTraceSample>();
            using PhysicsTraceWriter traceWriter = new PhysicsTraceWriter(Path.Combine(outputDirectoryPath, "bepu-frame-replay-trace.csv"));
            using ContactTraceWriter contactWriter = new ContactTraceWriter(Path.Combine(outputDirectoryPath, "bepu-frame-replay-contacts.csv"));
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
            double accumulatedSeconds = 0d;
            float elapsedTimeSeconds = 0f;
            for (int frameIndex = 0; frameIndex < frameDeltas.Count; frameIndex++) {
                float frameDelta = frameDeltas[frameIndex];
                accumulatedSeconds += frameDelta;
                while (accumulatedSeconds >= stepSeconds) {
                    collector.CurrentStepIndex++;
                    simulation.Timestep(stepSeconds);
                    accumulatedSeconds -= stepSeconds;
                }

                elapsedTimeSeconds += frameDelta;
                for (int bodyIndex = 0; bodyIndex < handles.Length; bodyIndex++) {
                    BodyReference body = simulation.Bodies[handles[bodyIndex]];
                    CaptureSample(
                        samples,
                        traceWriter,
                        "bepu-frame-replay",
                        frameIndex,
                        elapsedTimeSeconds,
                        DynamicStackBoxesSceneDefinition.CreateBodyName(bodyIndex),
                        body,
                        previousLinearVelocities[bodyIndex],
                        previousAngularVelocities[bodyIndex],
                        frameDelta);
                    previousLinearVelocities[bodyIndex] = body.Velocity.Linear;
                    previousAngularVelocities[bodyIndex] = body.Velocity.Angular;
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
        /// Captures one body-state sample from BEPU.
        /// </summary>
        /// <param name="samples">Sample list to append.</param>
        /// <param name="traceWriter">Trace writer receiving the row.</param>
        /// <param name="engineName">Trace engine name.</param>
        /// <param name="frameIndex">Host-frame index.</param>
        /// <param name="timeSeconds">Elapsed frame time in seconds.</param>
        /// <param name="bodyName">Body label.</param>
        /// <param name="body">BEPU body reference.</param>
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
            BodyReference body,
            Vector3 previousLinearVelocity,
            Vector3 previousAngularVelocity,
            float frameDeltaSeconds) {
            float safeFrameDeltaSeconds = frameDeltaSeconds <= 0f ? 1f : frameDeltaSeconds;
            PhysicsTraceSample sample = new PhysicsTraceSample {
                EngineName = engineName,
                StepIndex = frameIndex,
                TimeSeconds = timeSeconds,
                BodyName = bodyName,
                Position = body.Pose.Position,
                Orientation = body.Pose.Orientation,
                LinearVelocity = body.Velocity.Linear,
                AngularVelocity = body.Velocity.Angular,
                LinearForceApproximation = (body.Velocity.Linear - previousLinearVelocity) / safeFrameDeltaSeconds,
                AngularForceApproximation = (body.Velocity.Angular - previousAngularVelocity) / safeFrameDeltaSeconds
            };
            samples.Add(sample);
            traceWriter.Write(sample);
        }
    }
}
