namespace helengine {
    /// <summary>
    /// Computes fixed-capacity world profiles from authored scene demand before a scene bind mutates entities.
    /// </summary>
    static class HelPhysicsSceneRuntimeSizing3D {
        /// <summary>
        /// Counts entities that can potentially own a runtime physics body, including unsupported entities so their
        /// later explicit composition error cannot be hidden by an undersized allocation.
        /// </summary>
        /// <param name="roots">Scene roots to traverse.</param>
        /// <returns>Potential body count, including zero for an empty scene.</returns>
        public static int CountPotentialBodies(IReadOnlyList<Entity> roots) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            int count = 0;
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++) {
                Entity root = roots[rootIndex] ?? throw new ArgumentNullException(nameof(roots), "Scene roots cannot contain null entities.");
                count = checked(count + CountPotentialBodies(root));
            }

            return count;
        }

        /// <summary>
        /// Creates a profile with enough fixed storage for the scene and its worst-case candidate pairs.
        /// </summary>
        /// <param name="bodyCount">Potential body count from the scene hierarchy.</param>
        /// <param name="fixedStepSeconds">Exact host fixed step.</param>
        /// <returns>A validated world profile sized for the scene.</returns>
        public static HelPhysicsWorldSettings3D CreateSettings(int bodyCount, double fixedStepSeconds) {
            if (bodyCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(bodyCount));
            }

            if (bodyCount > 65534) {
                throw new InvalidOperationException("HelPhysics scene body demand exceeds fixed handle capacity.");
            }

            int capacity = Math.Max(1, bodyCount);
            long candidateCountLong = ((long)bodyCount * (bodyCount - 1L)) / 2L;
            if (candidateCountLong > int.MaxValue) {
                throw new InvalidOperationException("HelPhysics scene broadphase demand exceeds fixed integer capacity.");
            }

            int candidateCapacity = Math.Max(1, (int)candidateCountLong);
            int manifoldCapacity = NextPowerOfTwoAtLeast(candidateCapacity);
            if (candidateCapacity > int.MaxValue / 4) {
                throw new OverflowException("HelPhysics contact-point capacity exceeds the supported integer range.");
            }

            int contactPointCapacity = Math.Max(1, candidateCapacity * 4);
            if (bodyCount > int.MaxValue / 2) {
                throw new OverflowException("HelPhysics deferred-command capacity exceeds the supported integer range.");
            }

            int deferredCommandCapacity = Math.Max(1, bodyCount * 2);
            return new HelPhysicsWorldSettings3D(
                capacity,
                capacity,
                candidateCapacity,
                manifoldCapacity,
                contactPointCapacity,
                capacity,
                deferredCommandCapacity,
                4,
                1,
                fixedStepSeconds,
                new PhysicsVector3(0f, -9.81f, 0f));
        }

        /// <summary>
        /// Counts potentially physical entities recursively without changing any authored state.
        /// </summary>
        /// <param name="entity">Current hierarchy entity.</param>
        /// <returns>Potential body demand below the entity.</returns>
        static int CountPotentialBodies(Entity entity) {
            int count = 0;
            if (entity.Components != null) {
                bool hasRigidBody = false;
                bool hasCollider = false;
                bool hasController = false;
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    hasRigidBody |= component is RigidBody3DComponent;
                    hasCollider |= component is Collider3DComponent;
                    hasController |= component is CharacterController3DComponent;
                }

                if ((hasRigidBody && hasCollider) || hasController) {
                    count = 1;
                }
            }

            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    count = checked(count + CountPotentialBodies(entity.Children[childIndex]));
                }
            }

            return count;
        }

        /// <summary>
        /// Returns the smallest positive power of two that can hold one candidate count.
        /// </summary>
        /// <param name="value">Positive candidate count.</param>
        /// <returns>Power-of-two manifold capacity.</returns>
        static int NextPowerOfTwoAtLeast(int value) {
            int result = 1;
            while (result < value) {
                if (result >= (1 << 30)) {
                    throw new InvalidOperationException("HelPhysics manifold capacity exceeds supported fixed allocation.");
                }

                result <<= 1;
            }

            return result;
        }
    }
}
