using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using System.Numerics;

namespace helengine {
    /// <summary>
    /// Owns a raw managed BEPU simulation for the exact ground-and-four-box benchmark workload without entity-adapter overhead or leaked pooled memory.
    /// </summary>
    public sealed class HelPhysicsBepuBenchmarkWorld3D : IDisposable {
        /// <summary>
        /// Stores the four dynamic handles used to report final BEPU activity without relying on adapter-only diagnostics.
        /// </summary>
        readonly BodyHandle[] DynamicBodyHandles;

        /// <summary>
        /// Stores the BEPU memory pool owned exclusively by this benchmark world.
        /// </summary>
        readonly BufferPool BufferPoolValue;

        /// <summary>
        /// Stores per-collidable filtering and contact materials consumed by Helengine's BEPU callback.
        /// </summary>
        readonly CollidableProperty<BepuCollidableProperties3D> CollidablePropertiesValue;

        /// <summary>
        /// Stores per-body gravity consumed by Helengine's BEPU pose integration callback.
        /// </summary>
        readonly CollidableProperty<float> GravityAccelerationsValue;

        /// <summary>
        /// Stores the raw BEPU simulation whose timestep is measured directly.
        /// </summary>
        readonly Simulation SimulationValue;

        /// <summary>
        /// Tracks whether unmanaged and pooled resources have already been released.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Creates one single-threaded BEPU simulation with explicit 4/1 solving, matched box geometry, material, gravity, mass, and aggressive sleep settings.
        /// </summary>
        public HelPhysicsBepuBenchmarkWorld3D() {
            BufferPoolValue = new BufferPool(16384, 8);
            CollidablePropertiesValue = new CollidableProperty<BepuCollidableProperties3D>(BufferPoolValue);
            GravityAccelerationsValue = new CollidableProperty<float>(BufferPoolValue);
            SimulationValue = Simulation.Create(
                BufferPoolValue,
                new HelengineBepuNarrowPhaseCallbacks(CollidablePropertiesValue),
                new HelengineBepuPoseIntegratorCallbacks(GravityAccelerationsValue),
                new SolveDescription(4, 1));
            DynamicBodyHandles = new BodyHandle[4];

            Box groundShape = new Box(10f, 1f, 10f);
            TypedIndex groundShapeIndex = SimulationValue.Shapes.Add(groundShape);
            StaticHandle groundHandle = SimulationValue.Statics.Add(new StaticDescription(
                new Vector3(0f, -0.5f, 0f),
                groundShapeIndex));
            CollidablePropertiesValue.Allocate(groundHandle) = CreateCollidableProperties();

            Box dynamicShape = new Box(1f, 1f, 1f);
            TypedIndex dynamicShapeIndex = SimulationValue.Shapes.Add(dynamicShape);
            BodyInertia dynamicInertia = dynamicShape.ComputeInertia(1f);
            BodyActivityDescription activity = new BodyActivityDescription(0.2f, 5);
            for (int boxIndex = 0; boxIndex < DynamicBodyHandles.Length; boxIndex++) {
                BodyDescription description = BodyDescription.CreateDynamic(
                    new RigidPose(new Vector3(0f, 0.5f + boxIndex, 0f), Quaternion.Identity),
                    new BodyVelocity(),
                    dynamicInertia,
                    dynamicShapeIndex,
                    activity);
                BodyHandle bodyHandle = SimulationValue.Bodies.Add(description);
                DynamicBodyHandles[boxIndex] = bodyHandle;
                CollidablePropertiesValue.Allocate(bodyHandle) = CreateCollidableProperties();
                GravityAccelerationsValue.Allocate(bodyHandle) = -9.81f;
            }
        }

        /// <summary>
        /// Gets the exact static-plus-dynamic body count authored by this fixed workload.
        /// </summary>
        public int BodyCount => 5;

        /// <summary>
        /// Gets the number of dynamic boxes still awake in the raw BEPU simulation.
        /// </summary>
        public int AwakeDynamicBodyCount {
            get {
                ThrowIfDisposed();
                int awakeBodyCount = 0;
                for (int bodyIndex = 0; bodyIndex < DynamicBodyHandles.Length; bodyIndex++) {
                    if (SimulationValue.Bodies[DynamicBodyHandles[bodyIndex]].Awake) {
                        awakeBodyCount++;
                    }
                }

                return awakeBodyCount;
            }
        }

        /// <summary>
        /// Advances only the raw BEPU simulation by the supplied positive fixed-step duration.
        /// </summary>
        /// <param name="stepSeconds">Positive finite fixed-step duration.</param>
        public void Step(double stepSeconds) {
            ThrowIfDisposed();
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d || stepSeconds > float.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "The BEPU benchmark step must be positive, finite, and representable as a float.");
            }

            SimulationValue.Timestep((float)stepSeconds);
        }

        /// <summary>
        /// Releases the simulation, callback property stores, and all blocks retained by the benchmark-owned BEPU pool.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            SimulationValue.Dispose();
            CollidablePropertiesValue.Dispose();
            GravityAccelerationsValue.Dispose();
            BufferPoolValue.Clear();
            IsDisposed = true;
        }

        /// <summary>
        /// Creates the explicit collision filter and no-bounce dynamic-friction material shared by all benchmark boxes.
        /// </summary>
        /// <returns>Helengine BEPU callback properties matching the HelPhysics benchmark material where BEPU exposes equivalent controls.</returns>
        static BepuCollidableProperties3D CreateCollidableProperties() {
            return new BepuCollidableProperties3D {
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue,
                IsTrigger = false,
                DynamicFriction = 0.6f,
                MaximumRecoveryVelocity = 2f,
                SpringSettings = new SpringSettings(30f, 1f)
            };
        }

        /// <summary>
        /// Rejects access after benchmark-owned BEPU resources have been released.
        /// </summary>
        void ThrowIfDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(HelPhysicsBepuBenchmarkWorld3D));
            }
        }
    }
}
