namespace helengine.editor {
    /// <summary>
    /// Advances one viewport navigation cube in the editor update loop.
    /// </summary>
    [RunInEditor]
    public sealed class EditorViewportNavigationCubeUpdateComponent : UpdateComponent {
        /// <summary>Navigation view owned by this update component.</summary>
        readonly EditorViewportNavigationCube NavigationCube;

        /// <summary>Creates the updater for one viewport-owned navigation cube.</summary>
        /// <param name="navigationCube">View updated while the editor is running.</param>
        public EditorViewportNavigationCubeUpdateComponent(EditorViewportNavigationCube navigationCube) {
            NavigationCube = navigationCube ?? throw new ArgumentNullException(nameof(navigationCube));
        }

        /// <summary>
        /// Gets the navigation cube view advanced and disposed by this component.
        /// </summary>
        public EditorViewportNavigationCube View => NavigationCube;

        /// <summary>Initializes the view after the updater joins its viewport hierarchy.</summary>
        /// <param name="entity">Viewport entity receiving the updater.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            if (!ReferenceEquals(entity, NavigationCube.ParentViewportEntity)) {
                throw new InvalidOperationException("Navigation cube updater must be attached to its owning viewport.");
            }
            NavigationCube.Initialize();
        }

        /// <summary>Routes pointer input and navigation animation for the current frame.</summary>
        public override void Update() {
            NavigationCube.Update();
        }

        /// <summary>Releases view resources and pointer blockers when the viewport loses the updater.</summary>
        /// <param name="entity">Viewport entity losing the updater.</param>
        public override void ComponentRemoved(Entity entity) {
            NavigationCube.Dispose();
            base.ComponentRemoved(entity);
        }
    }
}
