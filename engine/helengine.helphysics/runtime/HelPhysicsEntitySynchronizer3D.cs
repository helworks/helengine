namespace helengine {
    /// <summary>
    /// Coordinates transactional entity validation, world input, fixed stepping, and entity write-back for one scene binder.
    /// </summary>
    public sealed class HelPhysicsEntitySynchronizer3D {
        /// <summary>
        /// Stores the accepted squared-length tolerance around normalized engine quaternions.
        /// </summary>
        const double QuaternionNormalizationTolerance = 0.0001d;

        /// <summary>
        /// Stores the binder whose public associations are synchronized.
        /// </summary>
        readonly HelPhysicsSceneBinder3D Binder;

        /// <summary>
        /// Initializes a synchronizer for one explicit scene binder.
        /// </summary>
        /// <param name="binder">Binder whose world and current associations should be synchronized.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
        public HelPhysicsEntitySynchronizer3D(HelPhysicsSceneBinder3D binder) {
            Binder = binder ?? throw new ArgumentNullException(nameof(binder));
        }

        /// <summary>
        /// Reconciles removals, validates the complete binding and kinematic batch, then accepts every authored kinematic input.
        /// </summary>
        public void SynchronizeBeforeStep() {
            Binder.ReconcilePendingWorldRemovals();
            int activeKinematicCount = ValidateBindingsAndKinematicInputs();
            Binder.World.ValidateKinematicCommandCapacity(activeKinematicCount);
            EnqueueKinematicInputs();
        }

        /// <summary>
        /// Copies current dynamic world pose and velocity into engine entities after a completed fixed step.
        /// </summary>
        public void SynchronizeAfterStep() {
            for (int bindingIndex = 0; bindingIndex < Binder.Bindings.Count; bindingIndex++) {
                HelPhysicsEntityBinding3D binding = Binder.Bindings[bindingIndex];
                if (!binding.IsValid || binding.Description.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                HelPhysicsBodySnapshot3D snapshot = binding.GetBodySnapshot();
                float3 worldPosition = ToEngineVector(snapshot.Position);
                float4 worldOrientation = ToEngineQuaternion(snapshot.Orientation);
                ResolveLocalPose(binding.Entity, worldPosition, worldOrientation, out float3 localPosition, out float4 localOrientation);
                binding.Entity.LocalPosition = localPosition;
                binding.Entity.LocalOrientation = localOrientation;
                binding.RigidBody.LinearVelocity = ToEngineVector(snapshot.LinearVelocity);
                binding.RigidBody.AngularVelocity = ToEngineVector(snapshot.AngularVelocity);
            }
        }

        /// <summary>
        /// Synchronizes validated kinematic input, advances exactly one configured fixed step, and publishes dynamic output.
        /// </summary>
        public void Step() {
            SynchronizeBeforeStep();
            Binder.World.Step(Binder.World.Settings.FixedStepSeconds);
            SynchronizeAfterStep();
        }

        /// <summary>
        /// Validates every binding and input without mutating world command storage or engine output.
        /// </summary>
        /// <returns>The exact number of active kinematic state commands required by the complete batch.</returns>
        int ValidateBindingsAndKinematicInputs() {
            int activeKinematicCount = 0;
            for (int bindingIndex = 0; bindingIndex < Binder.Bindings.Count; bindingIndex++) {
                HelPhysicsEntityBinding3D binding = Binder.Bindings[bindingIndex];
                ValidateBindingComponents(binding);
                if (binding.Description.BodyKind == BodyKind3D.Dynamic) {
                    ValidateDynamicParentTransform(binding.Entity);
                } else if (binding.Description.BodyKind == BodyKind3D.Kinematic) {
                    bool isActive = Binder.World.ValidateKinematicState(
                        binding.BodyHandle,
                        ToPhysicsVector(binding.Entity.Position),
                        ToPhysicsQuaternion(binding.Entity.Orientation),
                        ToPhysicsVector(binding.RigidBody.LinearVelocity),
                        ToPhysicsVector(binding.RigidBody.AngularVelocity));
                    if (isActive) {
                        activeKinematicCount++;
                    }
                }
            }

            return activeKinematicCount;
        }

        /// <summary>
        /// Accepts every already validated kinematic state in deterministic binding order.
        /// </summary>
        void EnqueueKinematicInputs() {
            for (int bindingIndex = 0; bindingIndex < Binder.Bindings.Count; bindingIndex++) {
                HelPhysicsEntityBinding3D binding = Binder.Bindings[bindingIndex];
                if (binding.Description.BodyKind != BodyKind3D.Kinematic) {
                    continue;
                }

                Binder.World.SetKinematicState(
                    binding.BodyHandle,
                    ToPhysicsVector(binding.Entity.Position),
                    ToPhysicsQuaternion(binding.Entity.Orientation),
                    ToPhysicsVector(binding.RigidBody.LinearVelocity),
                    ToPhysicsVector(binding.RigidBody.AngularVelocity));
            }
        }

        /// <summary>
        /// Validates exact component identity, supported composition, and immutable body mode for one live binding.
        /// </summary>
        /// <param name="binding">Current binding whose authored ownership must remain coherent.</param>
        /// <exception cref="InvalidOperationException">Thrown when an original component is absent, replaced, duplicated, unsupported, or changes body mode.</exception>
        static void ValidateBindingComponents(HelPhysicsEntityBinding3D binding) {
            if (!binding.IsValid) {
                throw new InvalidOperationException("A scene binder cannot synchronize an invalidated binding.");
            } else if (binding.Entity.Components == null) {
                throw new InvalidOperationException("A bound entity must retain its initialized component collection.");
            }

            int rigidBodyCount = 0;
            int colliderCount = 0;
            int boxColliderCount = 0;
            bool hasOriginalRigidBody = false;
            bool hasOriginalBoxCollider = false;
            for (int componentIndex = 0; componentIndex < binding.Entity.Components.Count; componentIndex++) {
                Component component = binding.Entity.Components[componentIndex];
                if (component is RigidBody3DComponent rigidBody) {
                    rigidBodyCount++;
                    if (ReferenceEquals(rigidBody, binding.RigidBody)) {
                        hasOriginalRigidBody = true;
                    }
                } else if (component is Collider3DComponent collider) {
                    colliderCount++;
                    if (collider is BoxCollider3DComponent boxCollider) {
                        boxColliderCount++;
                        if (ReferenceEquals(boxCollider, binding.BoxCollider)) {
                            hasOriginalBoxCollider = true;
                        }
                    }
                }
            }

            if (rigidBodyCount != 1 || !hasOriginalRigidBody) {
                throw new InvalidOperationException("A bound entity must retain exactly its original RigidBody3DComponent.");
            } else if (colliderCount != 1 || boxColliderCount != 1 || !hasOriginalBoxCollider) {
                throw new InvalidOperationException("A bound entity must retain exactly its original BoxCollider3DComponent.");
            } else if (binding.RigidBody.BodyKind != binding.Description.BodyKind) {
                throw new InvalidOperationException("A bound entity body mode cannot change after HelPhysics reservation.");
            }
        }

        /// <summary>
        /// Validates the complete effective parent transform required for safe dynamic world-to-local write-back.
        /// </summary>
        /// <param name="entity">Dynamic entity whose current parent transform will be inverted after stepping.</param>
        /// <exception cref="InvalidOperationException">Thrown when parent position, scale, or orientation is non-finite or non-invertible.</exception>
        static void ValidateDynamicParentTransform(Entity entity) {
            if (entity.Parent == null) {
                return;
            }

            float3 parentPosition = entity.Parent.Position;
            float3 parentScale = entity.Parent.Scale;
            float4 parentOrientation = entity.Parent.Orientation;
            ValidateFiniteVector(parentPosition, "Dynamic parent position must be finite before HelPhysics stepping.");
            if (!float.IsFinite(parentScale.X) || parentScale.X == 0f ||
                !float.IsFinite(parentScale.Y) || parentScale.Y == 0f ||
                !float.IsFinite(parentScale.Z) || parentScale.Z == 0f) {
                throw new InvalidOperationException("Dynamic parent scale must be finite and non-zero before HelPhysics stepping.");
            }

            ValidateNormalizedQuaternion(
                parentOrientation,
                "Dynamic parent orientation must be finite, invertible, and normalized before HelPhysics stepping.");
        }

        /// <summary>
        /// Computes and validates one complete local pose without publishing either transform member prematurely.
        /// </summary>
        /// <param name="entity">Dynamic entity receiving simulation output.</param>
        /// <param name="worldPosition">Finite simulation world-space center position.</param>
        /// <param name="worldOrientation">Normalized simulation world-space orientation.</param>
        /// <param name="localPosition">Receives the complete validated local position.</param>
        /// <param name="localOrientation">Receives the complete validated normalized local orientation.</param>
        static void ResolveLocalPose(
            Entity entity,
            float3 worldPosition,
            float4 worldOrientation,
            out float3 localPosition,
            out float4 localOrientation) {
            ValidateFiniteVector(worldPosition, "HelPhysics dynamic world position must remain finite during write-back.");
            ValidateNormalizedQuaternion(
                worldOrientation,
                "HelPhysics dynamic world orientation must remain finite and normalized during write-back.");
            if (entity.Parent == null) {
                localPosition = worldPosition;
                localOrientation = worldOrientation;
            } else {
                float3 relativePosition = worldPosition - entity.Parent.Position;
                float4 inverseParentOrientation = float4.Inverse(entity.Parent.Orientation);
                float3 unrotatedLocalPosition = float4.RotateVector(relativePosition, inverseParentOrientation);
                float3 parentScale = entity.Parent.Scale;
                localPosition = new float3(
                    unrotatedLocalPosition.X / parentScale.X,
                    unrotatedLocalPosition.Y / parentScale.Y,
                    unrotatedLocalPosition.Z / parentScale.Z);
                float4.Concatenate(ref worldOrientation, ref inverseParentOrientation, out localOrientation);
                localOrientation = NormalizeQuaternion(localOrientation);
            }

            ValidateFiniteVector(localPosition, "HelPhysics dynamic local position must be finite before write-back.");
            ValidateNormalizedQuaternion(
                localOrientation,
                "HelPhysics dynamic local orientation must be finite and normalized before write-back.");
        }

        /// <summary>
        /// Validates all vector components without converting a required invalid value into a default.
        /// </summary>
        /// <param name="value">Engine vector whose components must all be finite.</param>
        /// <param name="message">Specific invalid-state diagnostic.</param>
        /// <exception cref="InvalidOperationException">Thrown when any component is non-finite.</exception>
        static void ValidateFiniteVector(float3 value, string message) {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z)) {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Validates that an engine quaternion is finite, invertible, and normalized within the physics tolerance.
        /// </summary>
        /// <param name="value">Engine quaternion to validate using double-precision magnitude arithmetic.</param>
        /// <param name="message">Specific invalid-state diagnostic.</param>
        /// <exception cref="InvalidOperationException">Thrown when the quaternion cannot represent a normalized rotation.</exception>
        static void ValidateNormalizedQuaternion(float4 value, string message) {
            double lengthSquared =
                ((double)value.X * value.X) +
                ((double)value.Y * value.Y) +
                ((double)value.Z * value.Z) +
                ((double)value.W * value.W);
            if (!double.IsFinite(lengthSquared) ||
                lengthSquared <= 0d ||
                Math.Abs(lengthSquared - 1d) > QuaternionNormalizationTolerance) {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Normalizes one already finite invertible quaternion with double-precision magnitude arithmetic.
        /// </summary>
        /// <param name="value">Quaternion resulting from validated world and inverse-parent orientations.</param>
        /// <returns>A finite unit quaternion ready for atomic local-pose publication.</returns>
        static float4 NormalizeQuaternion(float4 value) {
            double lengthSquared =
                ((double)value.X * value.X) +
                ((double)value.Y * value.Y) +
                ((double)value.Z * value.Z) +
                ((double)value.W * value.W);
            if (!double.IsFinite(lengthSquared) || lengthSquared <= 0d) {
                throw new InvalidOperationException("HelPhysics cannot normalize an invalid dynamic local orientation.");
            }

            double inverseLength = 1d / Math.Sqrt(lengthSquared);
            return new float4(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength),
                (float)(value.W * inverseLength));
        }

        /// <summary>
        /// Converts one HelPhysics vector into the engine numeric domain at the scene boundary.
        /// </summary>
        /// <param name="value">Physics vector to convert component by component.</param>
        /// <returns>An engine vector carrying the same values.</returns>
        static float3 ToEngineVector(PhysicsVector3 value) {
            return new float3(value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat());
        }

        /// <summary>
        /// Converts one normalized HelPhysics orientation into the engine numeric domain.
        /// </summary>
        /// <param name="value">Physics quaternion to convert component by component.</param>
        /// <returns>An engine quaternion carrying the same normalized orientation.</returns>
        static float4 ToEngineQuaternion(PhysicsQuaternion value) {
            return new float4(value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat(), value.W.ToFloat());
        }

        /// <summary>
        /// Converts one finite engine vector into the dedicated HelPhysics numeric domain.
        /// </summary>
        /// <param name="value">Engine vector to convert component by component.</param>
        /// <returns>A physics vector carrying the same values.</returns>
        static PhysicsVector3 ToPhysicsVector(float3 value) {
            return new PhysicsVector3(value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Converts one engine quaternion into the dedicated HelPhysics numeric domain without silent normalization.
        /// </summary>
        /// <param name="value">Authored quaternion expected to already be normalized.</param>
        /// <returns>A physics quaternion carrying the same values.</returns>
        static PhysicsQuaternion ToPhysicsQuaternion(float4 value) {
            return new PhysicsQuaternion(
                PhysicsScalar.FromFloat(value.X),
                PhysicsScalar.FromFloat(value.Y),
                PhysicsScalar.FromFloat(value.Z),
                PhysicsScalar.FromFloat(value.W));
        }
    }
}
