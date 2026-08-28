namespace helengine {
    /// <summary>
    /// Creates, updates, and removes registered per-component scene selection visuals for the currently selected entity.
    /// </summary>
    public sealed class ComponentSceneSelectionEditorSyncComponent : UpdateComponent, IEditorHiddenComponent {
        /// <summary>
        /// Renderer used to build selection visual resources.
        /// </summary>
        readonly RenderManager3D Render3D;
        /// <summary>Session-owned generated material cache used by selection visuals.</summary>
        readonly helengine.editor.EngineGeneratedMaterialCache GeneratedMaterialCache;

        /// <summary>
        /// Live visuals keyed by the visualized component instance.
        /// </summary>
        readonly Dictionary<Component, ActiveSelectionVisual> ActiveVisualsByComponent;

        /// <summary>
        /// Entity whose components are currently visualized.
        /// </summary>
        Entity visualizedEntity;

        /// <summary>
        /// Initializes one selection visual synchronizer.
        /// </summary>
        /// <param name="render3D">Renderer used to build selection visual resources.</param>
        public ComponentSceneSelectionEditorSyncComponent(RenderManager3D render3D, helengine.editor.EngineGeneratedMaterialCache generatedMaterialCache) {
            Render3D = render3D ?? throw new ArgumentNullException(nameof(render3D));
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            ActiveVisualsByComponent = new Dictionary<Component, ActiveSelectionVisual>();
        }

        /// <summary>
        /// Synchronizes selection visuals against the current editor selection every frame.
        /// </summary>
        public override void Update() {
            base.Update();
            Entity selectedEntity = helengine.editor.EditorSelectionService.SelectedEntity;
            if (!ReferenceEquals(selectedEntity, visualizedEntity)) {
                DisposeAllVisuals();
                visualizedEntity = selectedEntity;
            }

            if (visualizedEntity == null || visualizedEntity.IsDisposed) {
                DisposeAllVisuals();
                visualizedEntity = null;
                return;
            }

            SynchronizeSelectedEntityVisuals();
            RemoveStaleVisuals();
        }

        /// <summary>
        /// Disposes all owned visuals when the synchronizer leaves the scene.
        /// </summary>
        /// <param name="entity">Entity that hosted this synchronizer.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DisposeAllVisuals();
            visualizedEntity = null;
        }

        /// <summary>
        /// Creates or updates one visual per registered editor for each supported component on the selected entity.
        /// </summary>
        void SynchronizeSelectedEntityVisuals() {
            List<Component> components = visualizedEntity.Components;
            if (components == null) {
                return;
            }

            IReadOnlyList<helengine.editor.IComponentSceneSelectionEditor> editors = helengine.editor.ComponentEditorRegistry.SceneSelectionEditors;
            for (int componentIndex = 0; componentIndex < components.Count; componentIndex++) {
                Component component = components[componentIndex];
                if (component == null) {
                    continue;
                }

                for (int editorIndex = 0; editorIndex < editors.Count; editorIndex++) {
                    helengine.editor.IComponentSceneSelectionEditor editor = editors[editorIndex];
                    if (!editor.Supports(component)) {
                        continue;
                    }

                    if (!ActiveVisualsByComponent.TryGetValue(component, out ActiveSelectionVisual activeVisual)) {
                        activeVisual = new ActiveSelectionVisual(editor, editor.CreateSelectionVisual(Render3D, GeneratedMaterialCache, visualizedEntity, component));
                        ActiveVisualsByComponent[component] = activeVisual;
                    }

                    activeVisual.Editor.UpdateSelectionVisual(activeVisual.VisualEntity, visualizedEntity, component);
                    break;
                }
            }
        }

        /// <summary>
        /// Disposes visuals whose components were removed from the selected entity.
        /// </summary>
        void RemoveStaleVisuals() {
            Component[] componentSnapshot = CreateActiveComponentSnapshot();
            for (int index = 0; index < componentSnapshot.Length; index++) {
                Component component = componentSnapshot[index];
                if (visualizedEntity.Components != null && visualizedEntity.Components.Contains(component)) {
                    continue;
                }

                DisposeVisual(component);
            }
        }

        /// <summary>
        /// Disposes one owned visual and clears its mapping.
        /// </summary>
        /// <param name="component">Component whose visual should be disposed.</param>
        void DisposeVisual(Component component) {
            if (!ActiveVisualsByComponent.TryGetValue(component, out ActiveSelectionVisual activeVisual)) {
                return;
            }

            ActiveVisualsByComponent.Remove(component);
            activeVisual.VisualEntity.Dispose();
        }

        /// <summary>
        /// Disposes every owned visual.
        /// </summary>
        void DisposeAllVisuals() {
            Component[] componentSnapshot = CreateActiveComponentSnapshot();
            for (int index = 0; index < componentSnapshot.Length; index++) {
                DisposeVisual(componentSnapshot[index]);
            }
        }

        /// <summary>
        /// Creates one snapshot of the currently visualized component keys so disposal can iterate safely.
        /// </summary>
        /// <returns>Snapshot of visualized components.</returns>
        Component[] CreateActiveComponentSnapshot() {
            Component[] snapshot = new Component[ActiveVisualsByComponent.Count];
            ActiveVisualsByComponent.Keys.CopyTo(snapshot, 0);
            return snapshot;
        }

        /// <summary>
        /// Associates one live selection visual with the editor that owns it.
        /// </summary>
        sealed class ActiveSelectionVisual {
            public ActiveSelectionVisual(helengine.editor.IComponentSceneSelectionEditor editor, EditorEntity visualEntity) {
                Editor = editor;
                VisualEntity = visualEntity;
            }

            public helengine.editor.IComponentSceneSelectionEditor Editor { get; }

            public EditorEntity VisualEntity { get; }
        }
    }
}
