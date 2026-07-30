namespace helengine {
    /// <summary>
    /// Traverses engine entity hierarchies and reserves one HelPhysics box body for every supported rigid-body entity.
    /// </summary>
    public sealed class HelPhysicsSceneBinder3D {
        /// <summary>
        /// Stores the engine-authored default linear damping used when the rigid-body component has no damping member.
        /// </summary>
        const float DefaultLinearDamping = 0.1f;

        /// <summary>
        /// Stores the engine-authored default angular damping used when the rigid-body component has no damping member.
        /// </summary>
        const float DefaultAngularDamping = 0.1f;

        /// <summary>
        /// Stores bindings in deterministic hierarchy traversal order.
        /// </summary>
        readonly List<HelPhysicsEntityBinding3D> BindingsValue;

        /// <summary>
        /// Stores the immutable public view over the mutable deterministic binding list.
        /// </summary>
        readonly IReadOnlyList<HelPhysicsEntityBinding3D> BindingsView;

        /// <summary>
        /// Stores the next positive engine-binding identifier assigned by this binder.
        /// </summary>
        int NextBindingId;

        /// <summary>
        /// Initializes a binder that exclusively creates scene associations in the supplied world.
        /// </summary>
        /// <param name="world">Explicit HelPhysics world owned by the standalone runtime flow.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="world"/> is null.</exception>
        public HelPhysicsSceneBinder3D(HelPhysicsWorld3D world) {
            World = world ?? throw new ArgumentNullException(nameof(world));
            BindingsValue = new List<HelPhysicsEntityBinding3D>();
            BindingsView = BindingsValue.AsReadOnly();
            NextBindingId = 1;
            Synchronizer = new HelPhysicsEntitySynchronizer3D(this);
        }

        /// <summary>
        /// Gets the world owned by this scene-binding runtime.
        /// </summary>
        public HelPhysicsWorld3D World { get; }

        /// <summary>
        /// Gets current bindings in deterministic hierarchy traversal order without exposing solver storage.
        /// </summary>
        public IReadOnlyList<HelPhysicsEntityBinding3D> Bindings => BindingsView;

        /// <summary>
        /// Gets the entity synchronizer that coordinates this binder's standalone fixed-step flow.
        /// </summary>
        public HelPhysicsEntitySynchronizer3D Synchronizer { get; }

        /// <summary>
        /// Recursively binds every supported physics entity in one root hierarchy.
        /// </summary>
        /// <param name="root">Root entity whose components and descendants should be traversed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
        public void BindHierarchy(Entity root) {
            if (root == null) {
                throw new ArgumentNullException(nameof(root));
            }

            ValidateEntityAndDescendants(root);
            BindEntityAndDescendants(root);
        }

        /// <summary>
        /// Synchronizes authored input and advances the owned world by its exact configured fixed step.
        /// </summary>
        public void Step() {
            Synchronizer.Step();
        }

        /// <summary>
        /// Resolves the current binding for one exact entity reference.
        /// </summary>
        /// <param name="entity">Entity whose association is required.</param>
        /// <returns>The current valid binding owned by this runtime.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is not currently bound.</exception>
        public HelPhysicsEntityBinding3D GetBinding(Entity entity) {
            if (!TryGetBinding(entity, out HelPhysicsEntityBinding3D binding)) {
                throw new InvalidOperationException("The entity is not bound to this HelPhysics scene runtime.");
            }

            return binding;
        }

        /// <summary>
        /// Attempts to resolve the current binding for one exact entity reference.
        /// </summary>
        /// <param name="entity">Entity whose association should be queried.</param>
        /// <param name="binding">Current binding when found; otherwise null.</param>
        /// <returns>True when the entity has a current valid association; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        public bool TryGetBinding(Entity entity, out HelPhysicsEntityBinding3D binding) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            int bindingIndex = FindBindingIndex(entity);
            if (bindingIndex < 0) {
                binding = null;
                return false;
            }

            binding = BindingsValue[bindingIndex];
            return true;
        }

        /// <summary>
        /// Invalidates one current entity association and defers removal of its exact body generation.
        /// </summary>
        /// <param name="entity">Currently bound entity to remove from this runtime.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is not currently bound or body removal is rejected.</exception>
        public void Unbind(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            int bindingIndex = FindBindingIndex(entity);
            if (bindingIndex < 0) {
                throw new InvalidOperationException("The entity is not bound to this HelPhysics scene runtime.");
            }

            HelPhysicsEntityBinding3D binding = BindingsValue[bindingIndex];
            World.RemoveBody(binding.BodyHandle);
            BindingsValue.RemoveAt(bindingIndex);
            binding.Invalidate();
            if (ReferenceEquals(binding.Lifecycle.Parent, entity)) {
                entity.RemoveComponent(binding.Lifecycle);
                binding.Lifecycle.Dispose();
            }
        }

        /// <summary>
        /// Handles removal of a binding-owned lifecycle component during explicit detach or entity disposal.
        /// </summary>
        /// <param name="entity">Entity losing the lifecycle component.</param>
        /// <param name="lifecycle">Exact lifecycle component being detached.</param>
        internal void NotifyBindingLifecycleRemoved(
            Entity entity,
            HelPhysicsEntityBindingLifecycle3D lifecycle) {
            int bindingIndex = FindBindingIndex(entity);
            if (bindingIndex < 0) {
                return;
            }

            HelPhysicsEntityBinding3D binding = BindingsValue[bindingIndex];
            if (!ReferenceEquals(binding.Lifecycle, lifecycle)) {
                throw new InvalidOperationException("A foreign lifecycle component cannot invalidate a HelPhysics entity binding.");
            }

            World.RemoveBody(binding.BodyHandle);
            BindingsValue.RemoveAt(bindingIndex);
            binding.Invalidate();
        }

        /// <summary>
        /// Finds one current binding by entity reference without relying on user-overridable equality.
        /// </summary>
        /// <param name="entity">Entity reference to locate.</param>
        /// <returns>The deterministic binding-list index, or negative one when absent.</returns>
        int FindBindingIndex(Entity entity) {
            for (int bindingIndex = 0; bindingIndex < BindingsValue.Count; bindingIndex++) {
                if (ReferenceEquals(BindingsValue[bindingIndex].Entity, entity)) {
                    return bindingIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Validates an entire hierarchy before reserving any body so malformed descendants cannot leave a partial binding set.
        /// </summary>
        /// <param name="entity">Current hierarchy entity to validate.</param>
        void ValidateEntityAndDescendants(Entity entity) {
            if (FindBindingIndex(entity) >= 0) {
                throw new InvalidOperationException("An entity cannot be bound to the same HelPhysics scene runtime more than once.");
            }

            int rigidBodyCount = 0;
            int colliderCount = 0;
            int boxColliderCount = 0;
            string unsupportedColliderName = null;
            RigidBody3DComponent rigidBody = null;
            BoxCollider3DComponent boxCollider = null;
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is HelPhysicsEntityBindingLifecycle3D) {
                        throw new InvalidOperationException(
                            "An entity cannot be owned by more than one HelPhysics scene binder.");
                    } else if (component is RigidBody3DComponent body) {
                        rigidBodyCount++;
                        rigidBody = body;
                    } else if (component is Collider3DComponent collider) {
                        colliderCount++;
                        if (collider is BoxCollider3DComponent box) {
                            boxColliderCount++;
                            boxCollider = box;
                        } else if (collider is StaticMeshCollider3DComponent) {
                            unsupportedColliderName = nameof(StaticMeshCollider3DComponent);
                        } else if (unsupportedColliderName == null) {
                            unsupportedColliderName = collider.GetType().Name;
                        }
                    }
                }
            }

            if (unsupportedColliderName != null) {
                throw new InvalidOperationException($"HelPhysics scene binding does not support {unsupportedColliderName}.");
            }

            if (rigidBodyCount != 0 || colliderCount != 0) {
                if (rigidBodyCount != 1) {
                    throw new InvalidOperationException(
                        "HelPhysics entities with a BoxCollider3DComponent must carry exactly one RigidBody3DComponent.");
                } else if (colliderCount != 1 || boxColliderCount != 1) {
                    throw new InvalidOperationException(
                        "Each RigidBody3DComponent bound to HelPhysics must carry exactly one BoxCollider3DComponent.");
                } else {
                    ValidateEffectiveBoxSize(entity, boxCollider);
                    _ = CreateBodyDescription(entity, rigidBody, boxCollider, 1);
                }
            }

            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    ValidateEntityAndDescendants(entity.Children[childIndex]);
                }
            }
        }

        /// <summary>
        /// Validates the full box dimensions produced by authored collider size and effective world scale before body reservation begins.
        /// </summary>
        /// <param name="entity">Entity supplying recursively composed world scale.</param>
        /// <param name="boxCollider">Collider supplying authored local full dimensions.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any resulting dimension is non-finite or not strictly positive.</exception>
        static void ValidateEffectiveBoxSize(Entity entity, BoxCollider3DComponent boxCollider) {
            float3 effectiveSize = boxCollider.Size * entity.Scale;
            if (float.IsNaN(effectiveSize.X) ||
                float.IsInfinity(effectiveSize.X) ||
                effectiveSize.X <= 0f ||
                float.IsNaN(effectiveSize.Y) ||
                float.IsInfinity(effectiveSize.Y) ||
                effectiveSize.Y <= 0f ||
                float.IsNaN(effectiveSize.Z) ||
                float.IsInfinity(effectiveSize.Z) ||
                effectiveSize.Z <= 0f) {
                throw new ArgumentOutOfRangeException(
                    nameof(entity),
                    "HelPhysics box dimensions require finite positive authored size and effective world scale on every axis.");
            }
        }

        /// <summary>
        /// Binds one entity when it carries the supported rigid-body and box-collider pair, then visits its children.
        /// </summary>
        /// <param name="entity">Current hierarchy entity.</param>
        void BindEntityAndDescendants(Entity entity) {
            RigidBody3DComponent rigidBody = null;
            BoxCollider3DComponent boxCollider = null;
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is RigidBody3DComponent body) {
                        rigidBody = body;
                    } else if (component is BoxCollider3DComponent box) {
                        boxCollider = box;
                    }
                }
            }

            if (rigidBody != null && boxCollider != null) {
                BindEntity(entity, rigidBody, boxCollider);
            }

            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    BindEntityAndDescendants(entity.Children[childIndex]);
                }
            }
        }

        /// <summary>
        /// Translates one supported entity into an explicit HelPhysics body reservation and stores its public association.
        /// </summary>
        /// <param name="entity">Entity supplying world pose and effective scale.</param>
        /// <param name="rigidBody">Rigid body supplying mode, motion, mass, gravity, and sleep values.</param>
        /// <param name="boxCollider">Box collider supplying dimensions, filtering, and contact material.</param>
        void BindEntity(Entity entity, RigidBody3DComponent rigidBody, BoxCollider3DComponent boxCollider) {
            int bindingId = NextBindingId++;
            HelPhysicsBodyDescription3D description = CreateBodyDescription(
                entity,
                rigidBody,
                boxCollider,
                bindingId);
            HelPhysicsBodyHandle3D handle = World.CreateBody(description);
            HelPhysicsEntityBindingLifecycle3D lifecycle = new HelPhysicsEntityBindingLifecycle3D(this);
            HelPhysicsEntityBinding3D binding = new HelPhysicsEntityBinding3D(
                World,
                entity,
                rigidBody,
                handle,
                bindingId,
                description,
                lifecycle);
            BindingsValue.Add(binding);
            entity.AddComponent(lifecycle);
        }

        /// <summary>
        /// Translates one supported entity and its exact component pair into complete immutable body creation data.
        /// </summary>
        /// <param name="entity">Entity supplying world pose and effective scale.</param>
        /// <param name="rigidBody">Rigid body supplying mode, motion, mass, gravity, and sleep values.</param>
        /// <param name="boxCollider">Box collider supplying dimensions, filtering, and contact material.</param>
        /// <param name="bindingId">Positive binder-local identity retained in body metadata.</param>
        /// <returns>A fully validated HelPhysics body description.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when authored sleep ticks cannot be represented by HelPhysics.</exception>
        static HelPhysicsBodyDescription3D CreateBodyDescription(
            Entity entity,
            RigidBody3DComponent rigidBody,
            BoxCollider3DComponent boxCollider,
            int bindingId) {
            if (rigidBody.SleepTicks > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(
                    nameof(rigidBody),
                    "HelPhysics sleep tick counts cannot exceed 65,535 fixed steps.");
            }

            float3 effectiveSize = boxCollider.Size * entity.Scale;
            PhysicsScalar mass = rigidBody.BodyKind == BodyKind3D.Dynamic
                ? PhysicsScalar.FromFloat((float)rigidBody.Mass)
                : PhysicsScalar.Zero;
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(
                    effectiveSize.X * 0.5f,
                    effectiveSize.Y * 0.5f,
                    effectiveSize.Z * 0.5f)),
                rigidBody.BodyKind,
                ToPhysicsVector(entity.Position),
                ToPhysicsQuaternion(entity.Orientation),
                ToPhysicsVector(rigidBody.LinearVelocity),
                ToPhysicsVector(rigidBody.AngularVelocity),
                mass,
                new HelPhysicsMaterial3D(
                    PhysicsScalar.FromFloat((float)boxCollider.StaticFriction),
                    PhysicsScalar.FromFloat((float)boxCollider.DynamicFriction),
                    PhysicsScalar.FromFloat((float)boxCollider.Restitution)),
                boxCollider.CollisionLayer,
                boxCollider.CollisionMask,
                bindingId,
                PhysicsScalar.FromFloat((float)(rigidBody.UseGravity ? rigidBody.GravityScale : 0d)),
                PhysicsScalar.FromFloat(DefaultLinearDamping),
                PhysicsScalar.FromFloat(DefaultAngularDamping),
                PhysicsScalar.FromFloat((float)rigidBody.SleepThreshold),
                PhysicsScalar.FromFloat((float)rigidBody.SleepThreshold),
                (ushort)rigidBody.SleepTicks,
                rigidBody.BodyKind == BodyKind3D.Dynamic);
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
        /// Converts one engine quaternion into the dedicated HelPhysics numeric domain without normalization.
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
