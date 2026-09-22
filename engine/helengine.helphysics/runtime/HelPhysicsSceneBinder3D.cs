namespace helengine {
    /// <summary>
    /// Traverses engine entity hierarchies and reserves one HelPhysics box body for every supported rigid-body entity.
    /// </summary>
    public sealed class HelPhysicsSceneBinder3D : IDisposable {
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

        /// <summary>Stores entity identities for active and recently removed body generations.</summary>
        readonly Dictionary<HelPhysicsBodyHandle3D, Entity> EntitiesByBodyHandleValue;

        /// <summary>Stores translated trigger transitions from the latest world step.</summary>
        readonly List<TriggerEvent3D> TriggerEventsValue;
        readonly IReadOnlyList<TriggerEvent3D> TriggerEventsView;
        readonly List<HelPhysicsBodyHandle3D> RetiredBodyHandlesValue;

        /// <summary>
        /// Marks permanent binder-local identifier exhaustion after the final positive integer has been assigned.
        /// </summary>
        bool BindingIdExhausted;
        /// <summary>Marks terminal disposal so lifecycle callbacks cannot enqueue world mutations.</summary>
        bool IsDisposedValue;

        /// <summary>
        /// Allows one replacement binder to preflight entities that still carry lifecycle observers owned by the previous binder.
        /// </summary>
        bool AllowExistingLifecycleComponents;

        /// <summary>Stores the exact previous binder whose lifecycle observers may be retained during replacement preflight.</summary>
        HelPhysicsSceneBinder3D AllowedPreviousBinder;

        /// <summary>
        /// Stores the next positive engine-binding identifier assigned by this binder.
        /// </summary>
        int NextBindingId;

        /// <summary>
        /// Initializes a binder that exclusively creates scene associations in the supplied world.
        /// </summary>
        /// <param name="world">Explicit HelPhysics world owned by the standalone runtime flow.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="world"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="world"/> already has an owner or contains prior generic-world state.</exception>
        public HelPhysicsSceneBinder3D(HelPhysicsWorld3D world)
            : this(world, false) {
        }

        /// <summary>
        /// Initializes a binder and optionally permits lifecycle observers from a scene binder being replaced.
        /// </summary>
        /// <param name="world">Explicit HelPhysics world owned by this binder.</param>
        /// <param name="allowExistingLifecycleComponents">Whether replacement preflight may observe the prior binder lifecycle component.</param>
        internal HelPhysicsSceneBinder3D(HelPhysicsWorld3D world, bool allowExistingLifecycleComponents) {
            World = world ?? throw new ArgumentNullException(nameof(world));
            AllowExistingLifecycleComponents = allowExistingLifecycleComponents;
            AllowedPreviousBinder = null;
            BindingsValue = new List<HelPhysicsEntityBinding3D>();
            BindingsView = BindingsValue.AsReadOnly();
            EntitiesByBodyHandleValue = new Dictionary<HelPhysicsBodyHandle3D, Entity>();
            TriggerEventsValue = new List<TriggerEvent3D>();
            TriggerEventsView = TriggerEventsValue.AsReadOnly();
            RetiredBodyHandlesValue = new List<HelPhysicsBodyHandle3D>();
            NextBindingId = 1;
            Synchronizer = new HelPhysicsEntitySynchronizer3D(this);
            World.ClaimSceneBinderOwnership(this);
        }

        /// <summary>
        /// Initializes a replacement binder that may retain lifecycle observers owned by one exact previous binder.
        /// </summary>
        /// <param name="world">Explicit HelPhysics world owned by this binder.</param>
        /// <param name="previousBinder">Exact binder being replaced, or null for a fresh scene.</param>
        internal HelPhysicsSceneBinder3D(HelPhysicsWorld3D world, HelPhysicsSceneBinder3D previousBinder) {
            World = world ?? throw new ArgumentNullException(nameof(world));
            AllowExistingLifecycleComponents = previousBinder != null;
            AllowedPreviousBinder = previousBinder;
            BindingsValue = new List<HelPhysicsEntityBinding3D>();
            BindingsView = BindingsValue.AsReadOnly();
            EntitiesByBodyHandleValue = new Dictionary<HelPhysicsBodyHandle3D, Entity>();
            TriggerEventsValue = new List<TriggerEvent3D>();
            TriggerEventsView = TriggerEventsValue.AsReadOnly();
            RetiredBodyHandlesValue = new List<HelPhysicsBodyHandle3D>();
            NextBindingId = 1;
            Synchronizer = new HelPhysicsEntitySynchronizer3D(this);
            World.ClaimSceneBinderOwnership(this);
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

            int requiredBodyCount = ValidateEntityAndDescendants(root);
            ValidateBindingIdCapacity(requiredBodyCount);
            World.ValidateBodyCreationCapacity(requiredBodyCount);
            BindEntityAndDescendants(root);
        }

        /// <summary>
        /// Preflights and binds every root of one scene as a single transaction so a later root cannot leave
        /// earlier roots partially reserved when composition, scale, or fixed capacity validation fails.
        /// </summary>
        /// <param name="roots">Complete scene roots in deterministic authored order.</param>
        /// <exception cref="ArgumentNullException">Thrown when the root list or one root is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when any root contains unsupported or malformed physics composition.</exception>
        public void BindScene(IReadOnlyList<Entity> roots) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            int requiredBodyCount = 0;
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++) {
                Entity root = roots[rootIndex] ?? throw new ArgumentNullException(nameof(roots), "Scene roots cannot contain null entities.");
                int rootBodyCount = ValidateEntityAndDescendants(root);
                if (requiredBodyCount > int.MaxValue - rootBodyCount) {
                    throw new InvalidOperationException("A HelPhysics scene contains too many body entities to preflight safely.");
                }

                requiredBodyCount += rootBodyCount;
            }

            ValidateBindingIdCapacity(requiredBodyCount);
            World.ValidateBodyCreationCapacity(requiredBodyCount);
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++) {
                BindEntityAndDescendants(roots[rootIndex]);
            }
            AllowExistingLifecycleComponents = false;
            AllowedPreviousBinder = null;
        }

        /// <summary>
        /// Synchronizes authored input and advances the owned world by its exact configured fixed step.
        /// </summary>
        public void Step() {
            Synchronizer.Step();
            RefreshTriggerEvents();
            PruneRetiredHandles();
        }
        /// <summary>
        /// Gets trigger transitions emitted by the owned world during the most recent fixed step.
        /// </summary>
        public IReadOnlyList<TriggerEvent3D> TriggerEvents {
            get {
                return TriggerEventsView;
            }
        }
        /// <summary>
        /// Finds the highest valid oriented top-face support under a controller footprint.
        /// </summary>
        internal HelPhysicsCharacterControllerSupport3D FindControllerSupport(
            HelPhysicsEntityBinding3D controllerBinding,
            float3 position,
            double stepHeight) {
            float3 controllerHalf = GetWorldHalfExtents(
                controllerBinding.BoxCollider.Size,
                controllerBinding.Entity.Scale,
                controllerBinding.Entity.Orientation);
            float controllerBottom = position.Y - controllerHalf.Y;
            float bestHeight = float.NegativeInfinity;
            float3 bestVelocity = float3.Zero;
            float3 bestNormal = new float3(0f, 1f, 0f);
            bool found = false;
            for (int index = 0; index < BindingsValue.Count; index++) {
                HelPhysicsEntityBinding3D candidate = BindingsValue[index];
                if (!candidate.IsValid || ReferenceEquals(candidate, controllerBinding) ||
                    candidate.Description.ShapeKind != HelPhysicsShapeKind3D.Box ||
                    candidate.Description.IsTrigger ||
                    (candidate.Description.CollisionLayer & controllerBinding.Description.CollisionMask) == 0 ||
                    (controllerBinding.Description.CollisionLayer & candidate.Description.CollisionMask) == 0) {
                    continue;
                }

                HelPhysicsBodySnapshot3D snapshot = candidate.GetBodySnapshot();
                float3 candidateHalf = new float3(
                    candidate.Description.Shape.HalfExtents.X.ToFloat(),
                    candidate.Description.Shape.HalfExtents.Y.ToFloat(),
                    candidate.Description.Shape.HalfExtents.Z.ToFloat());
                float3 candidatePosition = new float3(
                    snapshot.Position.X.ToFloat(),
                    snapshot.Position.Y.ToFloat(),
                    snapshot.Position.Z.ToFloat());
                float4 candidateOrientation = new float4(
                    snapshot.Orientation.X.ToFloat(),
                    snapshot.Orientation.Y.ToFloat(),
                    snapshot.Orientation.Z.ToFloat(),
                    snapshot.Orientation.W.ToFloat());
                float3 candidateAxisX = float4.RotateVector(new float3(1f, 0f, 0f), candidateOrientation);
                float3 candidateAxisY = float4.RotateVector(new float3(0f, 1f, 0f), candidateOrientation);
                float3 candidateAxisZ = float4.RotateVector(new float3(0f, 0f, 1f), candidateOrientation);
                float candidateExtentX = Math.Abs(candidateAxisX.X) * candidateHalf.X +
                    Math.Abs(candidateAxisY.X) * candidateHalf.Y +
                    Math.Abs(candidateAxisZ.X) * candidateHalf.Z;
                float candidateExtentZ = Math.Abs(candidateAxisX.Z) * candidateHalf.X +
                    Math.Abs(candidateAxisY.Z) * candidateHalf.Y +
                    Math.Abs(candidateAxisZ.Z) * candidateHalf.Z;
                if (Math.Abs(position.X - candidatePosition.X) > controllerHalf.X + candidateExtentX ||
                    Math.Abs(position.Z - candidatePosition.Z) > controllerHalf.Z + candidateExtentZ) {
                    continue;
                }

                float normalY = candidateAxisY.Y;
                if (normalY <= 0.0001f) {
                    continue;
                }

                float3 candidateTop = candidatePosition + (candidateAxisY * candidateHalf.Y);
                float surfaceHeight = float.NegativeInfinity;
                float sampleHeight = GetTopFaceSampleHeight(position.X, position.Z, candidatePosition, candidateTop, candidateAxisX, candidateAxisY, candidateAxisZ, candidateHalf, normalY);
                if (float.IsFinite(sampleHeight)) surfaceHeight = Math.Max(surfaceHeight, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(position.X - controllerHalf.X, position.Z - controllerHalf.Z, candidatePosition, candidateTop, candidateAxisX, candidateAxisY, candidateAxisZ, candidateHalf, normalY);
                if (float.IsFinite(sampleHeight)) surfaceHeight = Math.Max(surfaceHeight, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(position.X - controllerHalf.X, position.Z + controllerHalf.Z, candidatePosition, candidateTop, candidateAxisX, candidateAxisY, candidateAxisZ, candidateHalf, normalY);
                if (float.IsFinite(sampleHeight)) surfaceHeight = Math.Max(surfaceHeight, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(position.X + controllerHalf.X, position.Z - controllerHalf.Z, candidatePosition, candidateTop, candidateAxisX, candidateAxisY, candidateAxisZ, candidateHalf, normalY);
                if (float.IsFinite(sampleHeight)) surfaceHeight = Math.Max(surfaceHeight, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(position.X + controllerHalf.X, position.Z + controllerHalf.Z, candidatePosition, candidateTop, candidateAxisX, candidateAxisY, candidateAxisZ, candidateHalf, normalY);
                if (float.IsFinite(sampleHeight)) surfaceHeight = Math.Max(surfaceHeight, sampleHeight);
                if (!float.IsFinite(surfaceHeight)) {
                    continue;
                }
                float supportDelta = surfaceHeight - controllerBottom;
                if (supportDelta > (float)stepHeight ||
                    supportDelta < -(float)controllerBinding.Controller.GroundSnapDistance ||
                    surfaceHeight <= bestHeight) {
                    continue;
                }

                bestHeight = surfaceHeight;
                bestVelocity = new float3(
                    snapshot.LinearVelocity.X.ToFloat(),
                    snapshot.LinearVelocity.Y.ToFloat(),
                    snapshot.LinearVelocity.Z.ToFloat());
                bestNormal = candidateAxisY;
                found = true;
            }

            return new HelPhysicsCharacterControllerSupport3D(
                found,
                bestHeight,
                bestVelocity,
                bestNormal);
        }

        static float GetTopFaceSampleHeight(
            float sampleX,
            float sampleZ,
            float3 candidatePosition,
            float3 candidateTop,
            float3 candidateAxisX,
            float3 candidateAxisY,
            float3 candidateAxisZ,
            float3 candidateHalf,
            float normalY) {
            float sampleY = candidateTop.Y -
                ((candidateAxisY.X * (sampleX - candidateTop.X)) +
                (candidateAxisY.Z * (sampleZ - candidateTop.Z))) / normalY;
            float3 relative = new float3(
                sampleX - candidatePosition.X,
                sampleY - candidatePosition.Y,
                sampleZ - candidatePosition.Z);
            float localX = (relative.X * candidateAxisX.X) +
                (relative.Y * candidateAxisX.Y) +
                (relative.Z * candidateAxisX.Z);
            float localZ = (relative.X * candidateAxisZ.X) +
                (relative.Y * candidateAxisZ.Y) +
                (relative.Z * candidateAxisZ.Z);
            const float tolerance = 0.01f;
            return Math.Abs(localX) <= candidateHalf.X + tolerance &&
                Math.Abs(localZ) <= candidateHalf.Z + tolerance
                ? sampleY
                : float.NaN;
        }

        static float3 GetWorldHalfExtents(float3 size, float3 scale, float4 orientation) {
            float3 axisX = float4.RotateVector(new float3(1f, 0f, 0f), orientation);
            float3 axisY = float4.RotateVector(new float3(0f, 1f, 0f), orientation);
            float3 axisZ = float4.RotateVector(new float3(0f, 0f, 1f), orientation);
            float3 half = new float3(
                Math.Abs(size.X * scale.X) * 0.5f,
                Math.Abs(size.Y * scale.Y) * 0.5f,
                Math.Abs(size.Z * scale.Z) * 0.5f);
            return new float3(
                Math.Abs(axisX.X) * half.X + Math.Abs(axisY.X) * half.Y + Math.Abs(axisZ.X) * half.Z,
                Math.Abs(axisX.Y) * half.X + Math.Abs(axisY.Y) * half.Y + Math.Abs(axisZ.Y) * half.Z,
                Math.Abs(axisX.Z) * half.X + Math.Abs(axisY.Z) * half.Y + Math.Abs(axisZ.Z) * half.Z);
        }

        internal float3 ClampControllerTarget(
            HelPhysicsEntityBinding3D controllerBinding,
            float3 currentPosition,
            float3 targetPosition) {
            float3 controllerHalf = GetWorldHalfExtents(
                controllerBinding.BoxCollider.Size,
                controllerBinding.Entity.Scale,
                controllerBinding.Entity.Orientation);
            float3 result = targetPosition;
            for (int index = 0; index < BindingsValue.Count; index++) {
                HelPhysicsEntityBinding3D candidate = BindingsValue[index];
                if (!candidate.IsValid || ReferenceEquals(candidate, controllerBinding) ||
                    candidate.Description.ShapeKind != HelPhysicsShapeKind3D.Box ||
                    candidate.Description.IsTrigger ||
                    (candidate.Description.CollisionLayer & controllerBinding.Description.CollisionMask) == 0 ||
                    (controllerBinding.Description.CollisionLayer & candidate.Description.CollisionMask) == 0) {
                    continue;
                }
                HelPhysicsBodySnapshot3D snapshot = candidate.GetBodySnapshot();
                float3 half = new float3(
                    candidate.Description.Shape.HalfExtents.X.ToFloat(),
                    candidate.Description.Shape.HalfExtents.Y.ToFloat(),
                    candidate.Description.Shape.HalfExtents.Z.ToFloat());
                float3 center = new float3(
                    snapshot.Position.X.ToFloat(),
                    snapshot.Position.Y.ToFloat(),
                    snapshot.Position.Z.ToFloat());
                float4 orientation = new float4(
                    snapshot.Orientation.X.ToFloat(),
                    snapshot.Orientation.Y.ToFloat(),
                    snapshot.Orientation.Z.ToFloat(),
                    snapshot.Orientation.W.ToFloat());
                float3 axisX = float4.RotateVector(new float3(1f, 0f, 0f), orientation);
                float3 axisY = float4.RotateVector(new float3(0f, 1f, 0f), orientation);
                float3 axisZ = float4.RotateVector(new float3(0f, 0f, 1f), orientation);
                double axisLength = Math.Sqrt(((double)axisY.X * axisY.X) + ((double)axisY.Y * axisY.Y) + ((double)axisY.Z * axisY.Z));
                double minimumCosine = Math.Cos(controllerBinding.Controller.MaximumSlopeDegrees * Math.PI / 180d);
                float3 candidateTop = center + (axisY * half.Y);
                float targetSurface = float.NegativeInfinity;
                float sampleHeight = GetTopFaceSampleHeight(targetPosition.X, targetPosition.Z, center, candidateTop, axisX, axisY, axisZ, half, axisY.Y);
                if (float.IsFinite(sampleHeight)) targetSurface = Math.Max(targetSurface, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(targetPosition.X - controllerHalf.X, targetPosition.Z - controllerHalf.Z, center, candidateTop, axisX, axisY, axisZ, half, axisY.Y);
                if (float.IsFinite(sampleHeight)) targetSurface = Math.Max(targetSurface, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(targetPosition.X - controllerHalf.X, targetPosition.Z + controllerHalf.Z, center, candidateTop, axisX, axisY, axisZ, half, axisY.Y);
                if (float.IsFinite(sampleHeight)) targetSurface = Math.Max(targetSurface, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(targetPosition.X + controllerHalf.X, targetPosition.Z - controllerHalf.Z, center, candidateTop, axisX, axisY, axisZ, half, axisY.Y);
                if (float.IsFinite(sampleHeight)) targetSurface = Math.Max(targetSurface, sampleHeight);
                sampleHeight = GetTopFaceSampleHeight(targetPosition.X + controllerHalf.X, targetPosition.Z + controllerHalf.Z, center, candidateTop, axisX, axisY, axisZ, half, axisY.Y);
                if (float.IsFinite(sampleHeight)) targetSurface = Math.Max(targetSurface, sampleHeight);
                float supportDelta = targetSurface - (currentPosition.Y - controllerHalf.Y);
                if (float.IsFinite(targetSurface) && axisLength > 0d && axisY.Y / axisLength >= minimumCosine && supportDelta <= (float)controllerBinding.Controller.StepHeight && supportDelta >= -(float)controllerBinding.Controller.GroundSnapDistance) {
                    continue;
                }
                float extentX = Math.Abs(axisX.X) * half.X + Math.Abs(axisY.X) * half.Y + Math.Abs(axisZ.X) * half.Z;
                float extentY = Math.Abs(axisX.Y) * half.X + Math.Abs(axisY.Y) * half.Y + Math.Abs(axisZ.Y) * half.Z;
                float extentZ = Math.Abs(axisX.Z) * half.X + Math.Abs(axisY.Z) * half.Y + Math.Abs(axisZ.Z) * half.Z;
                float controllerBottom = currentPosition.Y - controllerHalf.Y;
                float controllerTop = currentPosition.Y + controllerHalf.Y;
                if (center.Y + extentY <= controllerBottom || center.Y - extentY >= controllerTop ||
                    (Math.Abs(currentPosition.Z - center.Z) > controllerHalf.Z + extentZ &&
                    Math.Abs(result.Z - center.Z) > controllerHalf.Z + extentZ)) {
                    continue;
                }
                float left = center.X - extentX;
                float right = center.X + extentX;
                if (targetPosition.X > currentPosition.X &&
                    currentPosition.X + controllerHalf.X <= left &&
                    result.X + controllerHalf.X > left &&
                    center.Y + extentY - controllerBottom > (float)controllerBinding.Controller.StepHeight) {
                    result = new float3(left - controllerHalf.X - 0.001f, result.Y, result.Z);
                } else if (targetPosition.X < currentPosition.X &&
                    currentPosition.X - controllerHalf.X >= right &&
                    result.X - controllerHalf.X < right &&
                    center.Y + extentY - controllerBottom > (float)controllerBinding.Controller.StepHeight) {
                    result = new float3(right + controllerHalf.X + 0.001f, result.Y, result.Z);
                }
                float nearFace = center.Z - extentZ;
                float farFace = center.Z + extentZ;
                if (targetPosition.Z > currentPosition.Z &&
                    currentPosition.Z + controllerHalf.Z <= nearFace &&
                    result.Z + controllerHalf.Z > nearFace &&
                    center.Y + extentY - controllerBottom > (float)controllerBinding.Controller.StepHeight) {
                    result = new float3(result.X, result.Y, nearFace - controllerHalf.Z - 0.001f);
                } else if (targetPosition.Z < currentPosition.Z &&
                    currentPosition.Z - controllerHalf.Z >= farFace &&
                    result.Z - controllerHalf.Z < farFace &&
                    center.Y + extentY - controllerBottom > (float)controllerBinding.Controller.StepHeight) {
                    result = new float3(result.X, result.Y, farFace + controllerHalf.Z + 0.001f);
                }
            }
            return result;
        }

        void RefreshTriggerEvents() {
            TriggerEventsValue.Clear();
            IReadOnlyList<HelPhysicsTriggerEvent3D> events = World.TriggerEvents;
            int eventCount = World.TriggerEventCount;
            for (int index = 0; index < eventCount; index++) {
                HelPhysicsTriggerEvent3D triggerEvent = events[index];
                if (!EntitiesByBodyHandleValue.TryGetValue(triggerEvent.TriggerBody, out Entity triggerEntity) ||
                    !EntitiesByBodyHandleValue.TryGetValue(triggerEvent.OtherBody, out Entity otherEntity)) {
                    continue;
                }

                TriggerEventKind3D kind;
                if (triggerEvent.Kind == HelPhysicsTriggerEventKind3D.Enter) {
                    kind = TriggerEventKind3D.Enter;
                } else if (triggerEvent.Kind == HelPhysicsTriggerEventKind3D.Stay) {
                    kind = TriggerEventKind3D.Stay;
                } else {
                    kind = TriggerEventKind3D.Exit;
                }

                TriggerEventsValue.Add(new TriggerEvent3D(kind, triggerEntity, otherEntity));
            }
        }

        /// <summary>
        /// Removes retired handle identities after their final fixed-step transitions have been materialized.
        /// </summary>
        void PruneRetiredHandles() {
            for (int index = 0; index < RetiredBodyHandlesValue.Count; index++) {
                EntitiesByBodyHandleValue.Remove(RetiredBodyHandlesValue[index]);
            }

            RetiredBodyHandlesValue.Clear();
        }

        /// <summary>
        /// Releases every binding and queues its exact body generation for removal at the next fixed boundary.
        /// </summary>
        public void ReleaseBindings() {
            while (BindingsValue.Count > 0) {
                Unbind(BindingsValue[BindingsValue.Count - 1].Entity);
            }
        }

        /// <summary>
        /// Releases every binding without stepping the owned world.
        /// </summary>
        public void Dispose() {
            if (IsDisposedValue) {
                return;
            }

            IsDisposedValue = true;
            for (int bindingIndex = BindingsValue.Count - 1; bindingIndex >= 0; bindingIndex--) {
                HelPhysicsEntityBinding3D binding = BindingsValue[bindingIndex];
                binding.Invalidate();
                if (ReferenceEquals(binding.Lifecycle.Parent, binding.Entity)) {
                    binding.Entity.RemoveComponent(binding.Lifecycle);
                    binding.Lifecycle.Dispose();
                }
            }
            BindingsValue.Clear();
            EntitiesByBodyHandleValue.Clear();
            RetiredBodyHandlesValue.Clear();
            TriggerEventsValue.Clear();
            World.DisposeForSceneBinder(this);
        }

        /// <summary>
        /// Resolves the current binding for one exact entity reference.
        /// </summary>
        /// <param name="entity">Entity whose association is required.</param>
        /// <returns>The current valid binding owned by this runtime.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is not currently bound.</exception>
        [NativeBorrowedReturn]
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

            if (IsDisposedValue) {
                return;
            }
            int bindingIndex = FindBindingIndex(entity);
            if (bindingIndex < 0) {
                throw new InvalidOperationException("The entity is not bound to this HelPhysics scene runtime.");
            }

            HelPhysicsEntityBinding3D binding = BindingsValue[bindingIndex];
            World.RemoveBodyForSceneBinder(this, binding.BodyHandle);
            RetiredBodyHandlesValue.Add(binding.BodyHandle);

            InvalidateBinding(bindingIndex, true);
        }

        /// <summary>
        /// Handles removal of a binding-owned lifecycle component during explicit detach or entity disposal.
        /// </summary>
        /// <param name="entity">Entity losing the lifecycle component.</param>
        /// <param name="lifecycle">Exact lifecycle component being detached.</param>
        internal void NotifyBindingLifecycleRemoved(
            Entity entity,
            HelPhysicsEntityBindingLifecycle3D lifecycle) {
            if (IsDisposedValue) {
                return;
            }
            int bindingIndex = FindBindingIndex(entity);
            if (bindingIndex < 0) {
                return;
            }

            HelPhysicsEntityBinding3D binding = BindingsValue[bindingIndex];
            if (!ReferenceEquals(binding.Lifecycle, lifecycle)) {
                throw new InvalidOperationException("A foreign lifecycle component cannot invalidate a HelPhysics entity binding.");
            }

            World.RemoveBodyForSceneBinder(this, binding.BodyHandle);
            RetiredBodyHandlesValue.Add(binding.BodyHandle);

            InvalidateBinding(bindingIndex, false);
        }

        /// <summary>
        /// Removes one accepted association from binder state, invalidates it once, and optionally detaches its lifecycle observer.
        /// </summary>
        /// <param name="bindingIndex">Current deterministic list index of the association.</param>
        /// <param name="detachLifecycle">Whether the lifecycle component is still attached and must be removed explicitly.</param>
        void InvalidateBinding(int bindingIndex, bool detachLifecycle) {
            HelPhysicsEntityBinding3D binding = BindingsValue[bindingIndex];
            BindingsValue.RemoveAt(bindingIndex);
            binding.Invalidate();
            if (detachLifecycle && ReferenceEquals(binding.Lifecycle.Parent, binding.Entity)) {
                binding.Entity.RemoveComponent(binding.Lifecycle);
                binding.Lifecycle.Dispose();
            }
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
        /// <returns>The exact number of supported bodies required by this entity and its descendants.</returns>
        int ValidateEntityAndDescendants(Entity entity) {
            if (FindBindingIndex(entity) >= 0) {
                throw new InvalidOperationException("An entity cannot be bound to the same HelPhysics scene runtime more than once.");
            }

            int rigidBodyCount = 0;
            int colliderCount = 0;
            int boxColliderCount = 0;
            int sphereColliderCount = 0;
            int controllerCount = 0;
            bool hasStaticMeshCollider = false;
            bool hasUnsupportedCollider = false;
            RigidBody3DComponent rigidBody = null;
            BoxCollider3DComponent boxCollider = null;
            SphereCollider3DComponent sphereCollider = null;
            CharacterController3DComponent controller = null;
            int requiredBodyCount = 0;
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is HelPhysicsEntityBindingLifecycle3D lifecycleComponent && (!AllowExistingLifecycleComponents || !lifecycleComponent.IsOwnedBy(AllowedPreviousBinder))) {
                        throw new InvalidOperationException(
                            "An entity cannot be owned by more than one HelPhysics scene binder.");
                    } else if (component is RigidBody3DComponent body) {
                        rigidBodyCount++;
                        rigidBody = body;
                    } else if (component is CharacterController3DComponent characterController) {
                        controllerCount++;
                        controller = characterController;
                    } else if (component is Collider3DComponent collider) {
                        colliderCount++;
                        if (collider is BoxCollider3DComponent box) {
                            boxColliderCount++;
                            boxCollider = box;
                        } else if (collider is SphereCollider3DComponent sphere) {
                            sphereColliderCount++;
                            sphereCollider = sphere;
                        } else if (collider is StaticMeshCollider3DComponent) {
                            hasStaticMeshCollider = true;
                        } else {
                            hasUnsupportedCollider = true;
                        }
                    }
                }
            }

            if (hasStaticMeshCollider) {
                throw new InvalidOperationException(
                    $"HelPhysics scene binding does not support {nameof(StaticMeshCollider3DComponent)}.");
            } else if (hasUnsupportedCollider) {
                throw new InvalidOperationException(
                    "HelPhysics scene binding does not support this Collider3DComponent implementation.");
            }

            if (controllerCount > 1) {
                throw new InvalidOperationException("A controller entity may carry exactly one CharacterController3DComponent.");
            } else if (controllerCount == 1) {
                if (rigidBodyCount != 0 || colliderCount != 1 || boxColliderCount != 1) {
                    throw new InvalidOperationException("A CharacterController3DComponent requires exactly one BoxCollider3DComponent and no RigidBody3DComponent.");
                }

                ValidateEffectiveBoxSize(entity, boxCollider);
                _ = CreateControllerBodyDescription(entity, boxCollider, 1);
                requiredBodyCount = 1;
            } else if (rigidBodyCount != 0 || colliderCount != 0) {
                if (rigidBodyCount != 1) {
                    throw new InvalidOperationException(
                        "HelPhysics entities with a BoxCollider3DComponent must carry exactly one RigidBody3DComponent.");
                } else if (colliderCount != 1 || (boxColliderCount != 1 && sphereColliderCount != 1)) {
                    throw new InvalidOperationException(
                        "Each RigidBody3DComponent bound to HelPhysics must carry exactly one supported collider component (BoxCollider3DComponent or SphereCollider3DComponent).");
                } else {
                    if (boxColliderCount == 1) {
                        ValidateEffectiveBoxSize(entity, boxCollider);
                        _ = CreateBodyDescription(entity, rigidBody, boxCollider, 1);
                    } else {
                        ValidateEffectiveSphereRadius(entity, sphereCollider);
                        _ = CreateBodyDescription(entity, rigidBody, sphereCollider, 1);
                    }
                    requiredBodyCount = 1;
                }
            }
            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    int childBodyCount = ValidateEntityAndDescendants(entity.Children[childIndex]);
                    if (requiredBodyCount > int.MaxValue - childBodyCount) {
                        throw new InvalidOperationException("A HelPhysics hierarchy contains too many body entities to preflight safely.");
                    }

                    requiredBodyCount += childBodyCount;
                }
            }

            return requiredBodyCount;
        }

        /// <summary>
        /// Validates that one complete hierarchy can receive monotonic positive binder-local identities without rollover.
        /// </summary>
        /// <param name="requiredBodyCount">Validated number of supported body entities in the hierarchy.</param>
        /// <exception cref="InvalidOperationException">Thrown when the remaining positive identifier range cannot represent the transaction.</exception>
        void ValidateBindingIdCapacity(int requiredBodyCount) {
            if (requiredBodyCount == 0) {
                return;
            }

            long finalBindingId = (long)NextBindingId + requiredBodyCount - 1L;
            if (BindingIdExhausted || finalBindingId > int.MaxValue) {
                throw new InvalidOperationException("The HelPhysics scene binding identifier range is exhausted.");
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
        /// Validates the positive finite world radius produced by authored sphere radius and effective scale.
        /// </summary>
        /// <param name="entity">Entity supplying recursively composed world scale.</param>
        /// <param name="sphereCollider">Sphere collider supplying authored radius.</param>
        static void ValidateEffectiveSphereRadius(Entity entity, SphereCollider3DComponent sphereCollider) {
            float3 scale = entity.Scale;
            float maximumScale = Math.Max(Math.Abs(scale.X), Math.Max(Math.Abs(scale.Y), Math.Abs(scale.Z)));
            float radius = sphereCollider.Radius * maximumScale;
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) {
                throw new ArgumentOutOfRangeException(
                    nameof(entity),
                    "HelPhysics sphere radius requires a finite positive authored radius and effective scale.");
            }
        }
        /// <summary>
        /// Binds one entity when it carries the supported rigid-body and box-collider pair, then visits its children.
        /// </summary>
        /// <param name="entity">Current hierarchy entity.</param>
        void BindEntityAndDescendants(Entity entity) {
            RigidBody3DComponent rigidBody = null;
            BoxCollider3DComponent boxCollider = null;
            SphereCollider3DComponent sphereCollider = null;
            CharacterController3DComponent controller = null;
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is RigidBody3DComponent body) {
                        rigidBody = body;
                    } else if (component is BoxCollider3DComponent box) {
                        boxCollider = box;
                    } else if (component is SphereCollider3DComponent sphere) {
                        sphereCollider = sphere;
                    } else if (component is CharacterController3DComponent characterController) {
                        controller = characterController;
                    }
                }
            }

            if (rigidBody != null && boxCollider != null) {
                BindEntity(entity, rigidBody, boxCollider);
            }

            if (rigidBody != null && sphereCollider != null) {
                BindEntity(entity, rigidBody, sphereCollider);
            }

            if (rigidBody == null && controller != null && boxCollider != null) {
                BindController(entity, controller, boxCollider);
            }

            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    BindEntityAndDescendants(entity.Children[childIndex]);
                }
            }
        }

        void BindController(Entity entity, CharacterController3DComponent controller, BoxCollider3DComponent boxCollider) {
        /// <summary>
        /// Reserves one internal kinematic body for an authored controller-only entity.
        /// </summary>
        /// <param name="entity">Entity supplying world pose and effective scale.</param>
        /// <param name="controller">Character controller supplying movement and slope settings.</param>
        /// <param name="boxCollider">Box collider supplying dimensions, filtering, and material values.</param>
            int bindingId = NextBindingId;
            if (bindingId == int.MaxValue) {
                BindingIdExhausted = true;
            } else {
                NextBindingId++;
            }

            HelPhysicsBodyDescription3D description = CreateControllerBodyDescription(entity, boxCollider, bindingId);
            HelPhysicsBodyHandle3D handle = World.CreateBodyForSceneBinder(this, description);
            HelPhysicsEntityBindingLifecycle3D lifecycle = new HelPhysicsEntityBindingLifecycle3D(this);
            HelPhysicsEntityBinding3D binding = new HelPhysicsEntityBinding3D(
                World,
                entity,
                controller,
                boxCollider,
                handle,
                bindingId,
                description,
                lifecycle);
            BindingsValue.Add(binding);
            EntitiesByBodyHandleValue[handle] = entity;
            entity.AddComponent(lifecycle);
        }

        /// <summary>
        /// Translates one supported box entity into an explicit HelPhysics body reservation and stores its public association.
        /// </summary>
        /// <param name="entity">Entity supplying world pose and effective scale.</param>
        /// <param name="rigidBody">Rigid body supplying mode, motion, mass, gravity, and sleep values.</param>
        /// <param name="boxCollider">Box collider supplying dimensions, filtering, and contact material.</param>
        void BindEntity(Entity entity, RigidBody3DComponent rigidBody, BoxCollider3DComponent boxCollider) {
            int bindingId = NextBindingId;
            if (bindingId == int.MaxValue) {
                BindingIdExhausted = true;
            } else {
                NextBindingId++;
            }
            HelPhysicsBodyDescription3D description = CreateBodyDescription(
                entity,
                rigidBody,
                boxCollider,
                bindingId);
            HelPhysicsBodyHandle3D handle = World.CreateBodyForSceneBinder(this, description);
            HelPhysicsEntityBindingLifecycle3D lifecycle = new HelPhysicsEntityBindingLifecycle3D(this);
            HelPhysicsEntityBinding3D binding = new HelPhysicsEntityBinding3D(
                World,
                entity,
                rigidBody,
                boxCollider,
                handle,
                bindingId,
                description,
                lifecycle);
            BindingsValue.Add(binding);
            EntitiesByBodyHandleValue[handle] = entity;
            entity.AddComponent(lifecycle);
        }

        /// <summary>
        /// Translates one supported sphere entity into a HelPhysics body reservation and stores its public association.
        /// </summary>
        /// <param name="entity">Entity supplying world pose and effective scale.</param>
        /// <param name="rigidBody">Rigid body supplying mode and motion values.</param>
        /// <param name="sphereCollider">Sphere collider supplying radius, filtering, trigger, and material values.</param>
        void BindEntity(Entity entity, RigidBody3DComponent rigidBody, SphereCollider3DComponent sphereCollider) {
            int bindingId = NextBindingId;
            if (bindingId == int.MaxValue) {
                BindingIdExhausted = true;
            } else {
                NextBindingId++;
            }

            HelPhysicsBodyDescription3D description = CreateBodyDescription(entity, rigidBody, sphereCollider, bindingId);
            HelPhysicsBodyHandle3D handle = World.CreateBodyForSceneBinder(this, description);
            HelPhysicsEntityBindingLifecycle3D lifecycle = new HelPhysicsEntityBindingLifecycle3D(this);
            HelPhysicsEntityBinding3D binding = new HelPhysicsEntityBinding3D(
                World,
                entity,
                rigidBody,
                sphereCollider,
                handle,
                bindingId,
                description,
                lifecycle);
            BindingsValue.Add(binding);
            EntitiesByBodyHandleValue[handle] = entity;
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
        [NativeOwnedReturn]
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
                rigidBody.BodyKind == BodyKind3D.Dynamic,
                boxCollider.IsTrigger);
        }

        /// <summary>
        /// Converts one finite engine vector into the dedicated HelPhysics numeric domain.
        /// </summary>
        /// <param name="value">Engine vector to convert component by component.</param>
        /// <returns>A physics vector carrying the same values.</returns>
        /// <summary>Builds one validated sphere body description from authored values.</summary>
        [NativeOwnedReturn]
        static HelPhysicsBodyDescription3D CreateBodyDescription(Entity entity, RigidBody3DComponent rigidBody, SphereCollider3DComponent sphereCollider, int bindingId) {
            float3 scale = entity.Scale;
            float radius = sphereCollider.Radius * Math.Max(Math.Abs(scale.X), Math.Max(Math.Abs(scale.Y), Math.Abs(scale.Z)));
            PhysicsScalar mass = rigidBody.BodyKind == BodyKind3D.Dynamic ? PhysicsScalar.FromFloat((float)rigidBody.Mass) : PhysicsScalar.Zero;
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(radius)),
                rigidBody.BodyKind,
                ToPhysicsVector(entity.Position),
                ToPhysicsQuaternion(entity.Orientation),
                ToPhysicsVector(rigidBody.LinearVelocity),
                ToPhysicsVector(rigidBody.AngularVelocity),
                mass,
                new HelPhysicsMaterial3D(PhysicsScalar.FromFloat((float)sphereCollider.StaticFriction), PhysicsScalar.FromFloat((float)sphereCollider.DynamicFriction), PhysicsScalar.FromFloat((float)sphereCollider.Restitution)),
                sphereCollider.CollisionLayer,
                sphereCollider.CollisionMask,
                bindingId,
                PhysicsScalar.FromFloat((float)(rigidBody.UseGravity ? rigidBody.GravityScale : 0d)),
                PhysicsScalar.FromFloat(DefaultLinearDamping),
                PhysicsScalar.FromFloat(DefaultAngularDamping),
                PhysicsScalar.FromFloat((float)rigidBody.SleepThreshold),
                PhysicsScalar.FromFloat((float)rigidBody.SleepThreshold),
                (ushort)rigidBody.SleepTicks,
                rigidBody.BodyKind == BodyKind3D.Dynamic,
                sphereCollider.IsTrigger);
        }
        /// <summary>
        /// Builds the internal kinematic box body used by one controller-only entity.
        /// </summary>
        [NativeOwnedReturn]
        static HelPhysicsBodyDescription3D CreateControllerBodyDescription(
            Entity entity,
            BoxCollider3DComponent boxCollider,
            int bindingId) {
            float3 effectiveSize = boxCollider.Size * entity.Scale;
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(
                    effectiveSize.X * 0.5f,
                    effectiveSize.Y * 0.5f,
                    effectiveSize.Z * 0.5f)),
                BodyKind3D.Kinematic,
                ToPhysicsVector(entity.Position),
                ToPhysicsQuaternion(entity.Orientation),
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.Zero,
                new HelPhysicsMaterial3D(
                    PhysicsScalar.FromFloat((float)boxCollider.StaticFriction),
                    PhysicsScalar.FromFloat((float)boxCollider.DynamicFriction),
                    PhysicsScalar.FromFloat((float)boxCollider.Restitution)),
                boxCollider.CollisionLayer,
                boxCollider.CollisionMask,
                bindingId,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(DefaultLinearDamping),
                PhysicsScalar.FromFloat(DefaultAngularDamping),
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false,
                boxCollider.IsTrigger);
        }

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
