namespace helengine {
    /// <summary>
    /// Represents an object in the scene graph that can own components and children.
    /// </summary>
    public class Entity : IDisposable {
        bool isEnabled;
        bool isStatic;
        bool isInitialized;
        bool isDisposing;
        bool isDisposed;
        float3 position;
        float3 scale;
        float4 orientation;
        ushort layerMask;
        List<Component> components;
        List<Entity> children;

        /// <summary>
        /// Initializes a new entity with default transforms and registers it with the object manager.
        /// </summary>
        public Entity() {
            isEnabled = true;
            Orientation = float4.Identity;
            Scale = float3.One;
            LayerMask = 0b00000001;

            Core.Instance.ObjectManager.RegisterEntity(this);
        }

        /// <summary>
        /// Gets or sets the local position relative to the parent and resolves world position through parent transforms.
        /// </summary>
        public virtual float3 Position {
            get {
                ThrowIfDisposed();
                float3 pos = position;

                if (Parent != null) {
                    float3 scaledLocal = pos * Parent.Scale;
                    float3 rotatedLocal = float4.RotateVector(scaledLocal, Parent.Orientation);
                    pos = rotatedLocal + Parent.Position;
                }

                return pos;
            }
            set {
                ThrowIfDisposed();
                position = value;
            }
        }

        /// <summary>
        /// Gets or sets the uncomposed local position stored on the entity.
        /// </summary>
        public float3 LocalPosition {
            get {
                ThrowIfDisposed();
                return position;
            }
            set {
                ThrowIfDisposed();
                position = value;
            }
        }

        /// <summary>
        /// Gets or sets the scale of the entity. Multiplies with parent scale when present.
        /// </summary>
        public float3 Scale {
            get {
                ThrowIfDisposed();
                float3 sca = scale;

                if (Parent != null) {
                    sca *= Parent.Scale;
                }

                return sca;
            }
            set {
                ThrowIfDisposed();
                scale = value;
            }
        }

        /// <summary>
        /// Gets or sets the uncomposed local scale stored on the entity.
        /// </summary>
        public float3 LocalScale {
            get {
                ThrowIfDisposed();
                return scale;
            }
            set {
                ThrowIfDisposed();
                scale = value;
            }
        }

        /// <summary>
        /// Gets or sets the orientation quaternion. Multiplies with parent orientation when present.
        /// </summary>
        public float4 Orientation {
            get {
                ThrowIfDisposed();
                float4 ori = orientation;

                if (Parent != null) {
                    float4 parentOrientation = Parent.Orientation;
                    float4.Concatenate(ref ori, ref parentOrientation, out ori);
                }

                return ori;
            }
            set {
                ThrowIfDisposed();
                orientation = value;
            }
        }

        /// <summary>
        /// Gets or sets the uncomposed local orientation stored on the entity.
        /// </summary>
        public float4 LocalOrientation {
            get {
                ThrowIfDisposed();
                return orientation;
            }
            set {
                ThrowIfDisposed();
                orientation = value;
            }
        }

        /// <summary>
        /// Gets the exact local affine transform matrix composed from the stored local scale, local orientation, and local position.
        /// </summary>
        public float4x4 LocalTransformMatrix {
            get {
                ThrowIfDisposed();
                return CreateTransformMatrix(LocalPosition, LocalScale, LocalOrientation);
            }
        }

        /// <summary>
        /// Gets the exact world affine transform matrix by recursively composing local transforms through the full parent chain.
        /// </summary>
        public float4x4 WorldTransformMatrix {
            get {
                ThrowIfDisposed();
                float4x4 localTransform = LocalTransformMatrix;
                if (Parent == null) {
                    return localTransform;
                }

                float4x4 parentWorldTransform = Parent.WorldTransformMatrix;
                float4x4.Multiply(ref localTransform, ref parentWorldTransform, out float4x4 worldTransform);
                return worldTransform;
            }
        }

        /// <summary>
        /// Gets the parent entity when part of a hierarchy.
        /// </summary>
        public Entity Parent { get; private set; }

        /// <summary>
        /// Gets the raw parent entity for internal lifecycle flows that must inspect ownership without triggering disposed-object guards.
        /// </summary>
        internal Entity ParentUnsafe => Parent;

        /// <summary>
        /// Gets or sets the layer mask used for filtering rendering and input.
        /// </summary>
        public ushort LayerMask {
            get {
                ThrowIfDisposed();
                return layerMask;
            }
            set {
                ThrowIfDisposed();
                layerMask = value;
            }
        }




        /// <summary>
        /// Gets the list of components attached to this entity.
        /// </summary>
        public List<Component> Components {
            get {
                ThrowIfDisposed();
                return components;
            }
            internal set { components = value; }
        }

        /// <summary>
        /// Gets the list of child entities owned by this entity.
        /// </summary>
        public List<Entity> Children {
            get {
                ThrowIfDisposed();
                return children;
            }
            internal set { children = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the entity is enabled.
        /// </summary>
        public bool Enabled {
            get {
                ThrowIfDisposed();
                return isEnabled;
            }
            set {
                ThrowIfDisposed();
                bool wasHierarchyEnabled = IsHierarchyEnabled;
                if (isEnabled != value) {
                    isEnabled = value;
                    bool isHierarchyEnabled = IsHierarchyEnabled;
                    if (wasHierarchyEnabled != isHierarchyEnabled) {
                        ParentEnabledChange(isHierarchyEnabled);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this entity is effectively enabled after combining its local state with all parents.
        /// </summary>
        public bool IsHierarchyEnabled {
            get {
                ThrowIfDisposed();
                if (!isEnabled) {
                    return false;
                }

                if (Parent == null) {
                    return true;
                }

                return Parent.IsHierarchyEnabled;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this entity hierarchy has completed component initialization.
        /// </summary>
        public bool IsInitialized {
            get {
                ThrowIfDisposed();
                return isInitialized;
            }
        }

        /// <summary>
        /// Gets whether disposal completed and the entity should reject further public use.
        /// </summary>
        internal bool IsDisposed => isDisposed;

        /// <summary>
        /// Gets or sets a value indicating whether the entity is static.
        /// </summary>
        public bool Static {
            get {
                ThrowIfDisposed();
                return isStatic;
            }
            set {
                ThrowIfDisposed();
                if (isStatic != value) {
                    ParentStaticChange(value);
                }
                isStatic = value;
            }
        }

        /// <summary>
        /// Initializes the children collection for this entity.
        /// </summary>
        public void InitChildren() {
            ThrowIfDisposed();
            children = new List<Entity>();
        }

        /// <summary>
        /// Adds a child entity, enforcing that it does not already have a parent.
        /// </summary>
        /// <param name="entity">Child entity to add.</param>
        public void AddChild(Entity entity) {
            ThrowIfDisposed();
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            entity.ThrowIfDisposed();
            if (children == null) {
                throw new InvalidOperationException("Children collection has not been initialized.");
            }
            if (entity.Parent != null) {
                throw new Exception("Parent is not empty");
            }

            bool wasHierarchyEnabled = entity.IsHierarchyEnabled;
            entity.Parent = this;
            children.Add(entity);
            if (isInitialized) {
                entity.InitializeHierarchy();
            }
            if (wasHierarchyEnabled && entity.IsHierarchyEnabled) {
                entity.RefreshRegistrationsAfterParentChange();
            }
            bool isHierarchyEnabled = entity.IsHierarchyEnabled;
            if (wasHierarchyEnabled != isHierarchyEnabled) {
                entity.ParentEnabledChange(isHierarchyEnabled);
            }
        }

        /// <summary>
        /// Removes one direct child entity from this hierarchy without disposing it and updates its effective hierarchy-enabled state.
        /// </summary>
        /// <param name="entity">Child entity to remove.</param>
        public void RemoveChild(Entity entity) {
            ThrowIfDisposed();
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (children == null) {
                throw new InvalidOperationException("Children collection has not been initialized.");
            } else if (entity.Parent != this) {
                throw new InvalidOperationException("Entity is not parented to this parent.");
            }

            bool wasHierarchyEnabled = entity.IsHierarchyEnabled;
            if (!children.Remove(entity)) {
                throw new InvalidOperationException("Entity could not be removed from the child collection.");
            }

            entity.Parent = null;
            if (!ShouldSuppressRegistrationRefreshForDetachment(entity) && wasHierarchyEnabled && entity.IsHierarchyEnabled) {
                entity.RefreshRegistrationsAfterParentChange();
            }
            bool isHierarchyEnabled = entity.IsHierarchyEnabled;
            if (wasHierarchyEnabled != isHierarchyEnabled) {
                entity.ParentEnabledChange(isHierarchyEnabled);
            }
        }

        /// <summary>
        /// Initializes the component collection for this entity.
        /// </summary>
        public void InitComponents() {
            ThrowIfDisposed();
            components = new List<Component>();
        }

        /// <summary>
        /// Adds a component to the entity and triggers its attach callback.
        /// </summary>
        /// <param name="comp">Component to add.</param>
        public void AddComponent(Component comp) {
            ThrowIfDisposed();
            if (comp == null) {
                throw new ArgumentNullException(nameof(comp));
            }

            comp.ThrowIfDisposed();
            if (components == null) {
                throw new InvalidOperationException("Components collection has not been initialized.");
            }
            if (comp.ParentUnsafe != null) {
                throw new InvalidOperationException("Component is already attached to an entity.");
            }

            components.Add(comp);
            comp.AttachToEntity(this);

            if (ComponentExecutionPolicy.ShouldRunComponentLifecycle(comp, this)) {
                comp.ComponentAdded(this);
                if (isInitialized) {
                    comp.ComponentInitialized(this);
                }
            }
        }

        /// <summary>
        /// Marks this entity hierarchy as fully materialized and notifies all eligible components once.
        /// </summary>
        public void InitializeHierarchy() {
            ThrowIfDisposed();
            if (IsInitialized) {
                return;
            }

            isInitialized = true;
            if (components != null) {
                for (int i = 0; i < components.Count; i++) {
                    Component component = components[i];
                    if (!ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, this)) {
                        continue;
                    }

                    component.ComponentInitialized(this);
                }
            }

            if (children != null) {
                for (int i = 0; i < children.Count; i++) {
                    children[i].InitializeHierarchy();
                }
            }
        }

        /// <summary>
        /// Removes one component from the entity and triggers its detach callback.
        /// </summary>
        /// <param name="comp">Component to remove.</param>
        public void RemoveComponent(Component comp) {
            ThrowIfDisposed();
            if (comp == null) {
                throw new ArgumentNullException(nameof(comp));
            } else if (components == null) {
                throw new InvalidOperationException("Components collection has not been initialized.");
            } else if (comp.ParentUnsafe != this) {
                throw new InvalidOperationException("Component is not attached to this entity.");
            }

            if (!components.Remove(comp)) {
                throw new InvalidOperationException("Component could not be removed from the component collection.");
            }

            bool shouldRunLifecycle = ComponentExecutionPolicy.ShouldRunComponentLifecycle(comp, this);
            if (IsHierarchyEnabled && shouldRunLifecycle) {
                comp.ParentEnabledChange(false);
            }

            if (shouldRunLifecycle) {
                comp.ComponentRemoved(this);
            }

            comp.DetachFromEntity();
        }

        /// <summary>
        /// Notifies components and children that the enabled state changed.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        protected virtual void ParentEnabledChange(bool newEnabled) {
            if (components != null) {
                for (int i = 0; i < components.Count; i++) {
                    Component component = components[i];
                    if (!ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, this)) {
                        continue;
                    }

                    component.ParentEnabledChange(newEnabled);
                }
            }

            if (children != null) {
                for (int i = 0; i < children.Count; i++) {
                    children[i].ParentEnabledChange(children[i].IsHierarchyEnabled);
                }
            }
        }

        /// <summary>
        /// Notifies components and children that the static state changed.
        /// </summary>
        /// <param name="newEnabled">New static state.</param>
        protected virtual void ParentStaticChange(bool newEnabled) {
            if (components != null) {
                for (int i = 0; i < components.Count; i++) {
                    components[i].ParentStaticChange(newEnabled);
                }
            }

            if (children != null) {
                for (int i = 0; i < children.Count; i++) {
                    children[i].ParentStaticChange(newEnabled);
                }
            }
        }

        /// <summary>
        /// Rebuilds render and camera registrations for this subtree after a parent change that preserved enabled state.
        /// </summary>
        void RefreshRegistrationsAfterParentChange() {
            if (!IsHierarchyEnabled || Core.Instance == null || Core.Instance.ObjectManager == null) {
                return;
            }

            RefreshRegistrationsAfterParentChangeRecursive(this);
        }

        /// <summary>
        /// Recursively rebuilds render and camera registrations for one subtree after a reparent operation.
        /// </summary>
        /// <param name="entity">Current entity whose attached components should be refreshed.</param>
        static void RefreshRegistrationsAfterParentChangeRecursive(Entity entity) {
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is IDrawable2D drawable2D) {
                        Core.Instance.ObjectManager.RemoveFromRender2D(drawable2D);
                        Core.Instance.ObjectManager.RegisterForRender2D(drawable2D);
                    } else if (component is IDrawable3D drawable3D) {
                        Core.Instance.ObjectManager.RemoveFromRender3D(drawable3D);
                        Core.Instance.ObjectManager.RegisterForRender3D(drawable3D);
                    } else if (component is ICamera camera) {
                        Core.Instance.ObjectManager.RemoveCamera(camera);
                        Core.Instance.ObjectManager.RegisterCamera(camera);
                    }
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                RefreshRegistrationsAfterParentChangeRecursive(entity.Children[childIndex]);
            }
        }

        /// <summary>
        /// Gets whether detaching the provided child should skip registration rebuilding because one side is actively disposing.
        /// </summary>
        /// <param name="entity">Child entity being detached.</param>
        /// <returns><c>true</c> when disposal is in progress and registration refresh should be suppressed.</returns>
        bool ShouldSuppressRegistrationRefreshForDetachment(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return isDisposing || entity.isDisposing;
        }

        /// <summary>
        /// Builds one affine transform matrix using the engine row-vector convention of scale, then rotation, then translation.
        /// </summary>
        /// <param name="position">Translation component to encode.</param>
        /// <param name="scale">Per-axis scale component to encode.</param>
        /// <param name="orientation">Quaternion rotation component to encode.</param>
        /// <returns>Affine transform matrix that applies scale, rotation, and translation in row-vector order.</returns>
        static float4x4 CreateTransformMatrix(float3 position, float3 scale, float4 orientation) {
            float4x4 rotation;
            float4x4.CreateFromQuaternion(ref orientation, out rotation);
            float4x4 size;
            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);
            float4x4 scaleRotation;
            float4x4.Multiply(ref size, ref rotation, out scaleRotation);
            float4x4 translation;
            float4x4.CreateTranslation(ref position, out translation);
            float4x4.Multiply(ref scaleRotation, ref translation, out float4x4 transform);
            return transform;
        }

        /// <summary>
        /// Recursively tears down this entity subtree, detaches its components, removes it from any parent, and unregisters it from the object manager.
        /// </summary>
        public void Dispose() {
            if (isDisposing) {
                return;
            }

            isDisposing = true;
            List<Component> detachedComponents = null;
            if (components != null) {
                detachedComponents = new List<Component>(components.Count);
                while (components.Count > 0) {
                    int componentIndex = components.Count - 1;
                    ReportDisposalStage("BeforeComponentRemove", componentIndex);
                    Component component = components[components.Count - 1];
                    RemoveComponent(component);
                    detachedComponents.Add(component);
                }

                List<Component> disposedComponents = components;
                ReportDisposalStage("BeforeComponentsListDelete", -1);
                components = null;
                NativeOwnership.Delete(disposedComponents);
            }

            if (children != null) {
                while (children.Count > 0) {
                    ReportDisposalStage("BeforeChildRemove", -1);
                    Entity child = children[children.Count - 1];
                    RemoveChild(child);
                    ReportChildDisposalStage("BeforeChildDispose", child);
                    NativeOwnership.DisposeAndDelete(child);
                    ReportDisposalStage("AfterChildDispose", -1);
                }

                List<Entity> disposedChildren = children;
                ReportDisposalStage("BeforeChildrenListDelete", -1);
                children = null;
                NativeOwnership.Delete(disposedChildren);
            }

            if (detachedComponents != null) {
                for (int i = 0; i < detachedComponents.Count; i++) {
                    Component component = detachedComponents[i];
                    ReportDisposalStage("BeforeComponentDispose", i);
                    component.Dispose();
                    ReportDisposalStage("AfterComponentDispose", i);
                    NativeOwnership.Delete(component);
                    ReportDisposalStage("AfterComponentDelete", i);
                }
            }

            if (Parent != null) {
                ReportDisposalStage("BeforeParentDetach", -1);
                Parent.RemoveChild(this);
            }

            ReportDisposalStage("BeforeObjectManagerRemoveEntity", -1);
            Core.Instance.ObjectManager.RemoveEntity(this);
            ReportDisposalStage("AfterObjectManagerRemoveEntity", -1);
            isDisposed = true;
        }

        /// <summary>
        /// Throws when the entity already completed disposal and should no longer participate in runtime ownership flows.
        /// </summary>
        void ThrowIfDisposed() {
            if (isDisposed) {
                throw new InvalidOperationException("Disposed entities cannot be used.");
            }
        }

        /// <summary>
        /// Reports one disposal stage for this entity when the active core has scene-manager disposal diagnostics.
        /// </summary>
        /// <param name="stage">Short disposal stage label.</param>
        /// <param name="componentIndex">Component index involved in the stage, or -1 for entity-level stages.</param>
        void ReportDisposalStage(string stage, int componentIndex) {
            ReportChildDisposalStage(stage, this, componentIndex);
        }

        /// <summary>
        /// Reports one disposal stage for another entity when the active core has scene-manager disposal diagnostics.
        /// </summary>
        /// <param name="stage">Short disposal stage label.</param>
        /// <param name="entity">Entity associated with the disposal stage.</param>
        void ReportChildDisposalStage(string stage, Entity entity) {
            ReportChildDisposalStage(stage, entity, -1);
        }

        /// <summary>
        /// Reports one disposal stage for another entity when the active core has scene-manager disposal diagnostics.
        /// </summary>
        /// <param name="stage">Short disposal stage label.</param>
        /// <param name="entity">Entity associated with the disposal stage.</param>
        /// <param name="componentIndex">Component index involved in the stage, or -1 for entity-level stages.</param>
        void ReportChildDisposalStage(string stage, Entity entity, int componentIndex) {
            if (Core.Instance == null) {
                return;
            }

            SceneManager sceneManager = Core.Instance.SceneManager;
            if (sceneManager == null) {
                return;
            }

            sceneManager.ReportEntityDisposalStage(stage, entity, componentIndex);
        }
    }
}

