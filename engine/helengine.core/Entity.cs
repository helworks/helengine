namespace helengine {
    /// <summary>
    /// Represents an object in the scene graph that can own components and children.
    /// </summary>
    public class Entity : IDisposable {
        bool isEnabled;
        bool isStatic;
        bool isInitialized;
        bool isDisposing;
        float3 position;
        float3 scale;
        float4 orientation;

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
                float3 pos = position;

                if (Parent != null) {
                    float3 rotatedLocal = float4.RotateVector(pos, Parent.Orientation);
                    pos = rotatedLocal + Parent.Position;
                }

                return pos;
            }
            set { position = value; }
        }

        /// <summary>
        /// Gets or sets the uncomposed local position stored on the entity.
        /// </summary>
        public float3 LocalPosition {
            get { return position; }
            set { position = value; }
        }

        /// <summary>
        /// Gets or sets the scale of the entity. Inherits from parent when present.
        /// </summary>
        public float3 Scale {
            get {
                float3 sca = scale;

                if (Parent != null) {
                    sca += Parent.Scale;
                }

                return sca;
            }
            set { scale = value; }
        }

        /// <summary>
        /// Gets or sets the uncomposed local scale stored on the entity.
        /// </summary>
        public float3 LocalScale {
            get { return scale; }
            set { scale = value; }
        }

        /// <summary>
        /// Gets or sets the orientation quaternion. Multiplies with parent orientation when present.
        /// </summary>
        public float4 Orientation {
            get {
                float4 ori = orientation;

                if (Parent != null) {
                    ori *= Parent.Orientation;
                }

                return ori;
            }
            set { orientation = value; }
        }

        /// <summary>
        /// Gets or sets the uncomposed local orientation stored on the entity.
        /// </summary>
        public float4 LocalOrientation {
            get { return orientation; }
            set { orientation = value; }
        }

        /// <summary>
        /// Gets the parent entity when part of a hierarchy.
        /// </summary>
        public Entity Parent { get; private set; }

        /// <summary>
        /// Gets or sets the layer mask used for filtering rendering and input.
        /// </summary>
        public ushort LayerMask { get; set; }




        /// <summary>
        /// Gets the list of components attached to this entity.
        /// </summary>
        public List<Component> Components { get; internal set; }

        /// <summary>
        /// Gets the list of child entities owned by this entity.
        /// </summary>
        public List<Entity> Children { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entity is enabled.
        /// </summary>
        public bool Enabled {
            get { return isEnabled; }
            set {
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
            get { return isInitialized; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the entity is static.
        /// </summary>
        public bool Static {
            get { return isStatic; }
            set {
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
            Children = new List<Entity>();
        }

        /// <summary>
        /// Adds a child entity, enforcing that it does not already have a parent.
        /// </summary>
        /// <param name="entity">Child entity to add.</param>
        public void AddChild(Entity entity) {
            if (entity.Parent != null) {
                throw new Exception("Parent is not empty");
            }

            bool wasHierarchyEnabled = entity.IsHierarchyEnabled;
            entity.Parent = this;
            Children.Add(entity);
            if (IsInitialized) {
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
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (Children == null) {
                throw new InvalidOperationException("Children collection has not been initialized.");
            } else if (entity.Parent != this) {
                throw new InvalidOperationException("Entity is not parented to this parent.");
            }

            bool wasHierarchyEnabled = entity.IsHierarchyEnabled;
            if (!Children.Remove(entity)) {
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
            Components = new List<Component>();
        }

        /// <summary>
        /// Adds a component to the entity and triggers its attach callback.
        /// </summary>
        /// <param name="comp">Component to add.</param>
        public void AddComponent(Component comp) {
            Components.Add(comp);
            comp.AttachToEntity(this);

            if (ComponentExecutionPolicy.ShouldRunComponentLifecycle(comp, this)) {
                comp.ComponentAdded(this);
                if (IsInitialized) {
                    comp.ComponentInitialized(this);
                }
            }
        }

        /// <summary>
        /// Marks this entity hierarchy as fully materialized and notifies all eligible components once.
        /// </summary>
        public void InitializeHierarchy() {
            if (IsInitialized) {
                return;
            }

            isInitialized = true;
            if (Components != null) {
                for (int i = 0; i < Components.Count; i++) {
                    Component component = Components[i];
                    if (!ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, this)) {
                        continue;
                    }

                    component.ComponentInitialized(this);
                }
            }

            if (Children != null) {
                for (int i = 0; i < Children.Count; i++) {
                    Children[i].InitializeHierarchy();
                }
            }
        }

        /// <summary>
        /// Removes one component from the entity and triggers its detach callback.
        /// </summary>
        /// <param name="comp">Component to remove.</param>
        public void RemoveComponent(Component comp) {
            if (comp == null) {
                throw new ArgumentNullException(nameof(comp));
            } else if (Components == null) {
                throw new InvalidOperationException("Components collection has not been initialized.");
            } else if (comp.Parent != this) {
                throw new InvalidOperationException("Component is not attached to this entity.");
            }

            if (!Components.Remove(comp)) {
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
            if (Components != null) {
                for (int i = 0; i < Components.Count; i++) {
                    Component component = Components[i];
                    if (!ComponentExecutionPolicy.ShouldRunComponentLifecycle(component, this)) {
                        continue;
                    }

                    component.ParentEnabledChange(newEnabled);
                }
            }

            if (Children != null) {
                for (int i = 0; i < Children.Count; i++) {
                    Children[i].ParentEnabledChange(Children[i].IsHierarchyEnabled);
                }
            }
        }

        /// <summary>
        /// Notifies components and children that the static state changed.
        /// </summary>
        /// <param name="newEnabled">New static state.</param>
        protected virtual void ParentStaticChange(bool newEnabled) {
            if (Components != null) {
                for (int i = 0; i < Components.Count; i++) {
                    Components[i].ParentStaticChange(newEnabled);
                }
            }

            if (Children != null) {
                for (int i = 0; i < Children.Count; i++) {
                    Children[i].ParentStaticChange(newEnabled);
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
        /// Recursively tears down this entity subtree, detaches its components, removes it from any parent, and unregisters it from the object manager.
        /// </summary>
        public void Dispose() {
            if (isDisposing) {
                return;
            }

            isDisposing = true;
            if (Components != null) {
                while (Components.Count > 0) {
                    int componentIndex = Components.Count - 1;
                    ReportDisposalStage("BeforeComponentRemove", componentIndex);
                    Component component = Components[Components.Count - 1];
                    RemoveComponent(component);
                    ReportDisposalStage("BeforeComponentDispose", componentIndex);
                    component.Dispose();
                    ReportDisposalStage("AfterComponentDispose", componentIndex);
                    NativeOwnership.Delete(component);
                    ReportDisposalStage("AfterComponentDelete", componentIndex);
                }

                List<Component> components = Components;
                ReportDisposalStage("BeforeComponentsListDelete", -1);
                Components = null;
                NativeOwnership.Delete(components);
            }

            if (Children != null) {
                while (Children.Count > 0) {
                    ReportDisposalStage("BeforeChildRemove", -1);
                    Entity child = Children[Children.Count - 1];
                    RemoveChild(child);
                    ReportChildDisposalStage("BeforeChildDispose", child);
                    NativeOwnership.DisposeAndDelete(child);
                    ReportDisposalStage("AfterChildDispose", -1);
                }

                List<Entity> children = Children;
                ReportDisposalStage("BeforeChildrenListDelete", -1);
                Children = null;
                NativeOwnership.Delete(children);
            }

            if (Parent != null) {
                ReportDisposalStage("BeforeParentDetach", -1);
                Parent.RemoveChild(this);
            }

            ReportDisposalStage("BeforeObjectManagerRemoveEntity", -1);
            Core.Instance.ObjectManager.RemoveEntity(this);
            ReportDisposalStage("AfterObjectManagerRemoveEntity", -1);
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

