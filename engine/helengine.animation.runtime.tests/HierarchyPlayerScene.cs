namespace helengine.animation.runtime.tests {
    /// <summary>
    /// A headless core whose content root holds the sample clip, a root entity carrying the player, a child <c>Arm</c>
    /// and a grandchild <c>Hand</c>. Track targets are resolved through the same fixup path scene loading uses.
    /// </summary>
    sealed class HierarchyPlayerScene : IDisposable {
        /// <summary>
        /// Builds the scene and writes the sample clip. Call <see cref="Initialize"/> to start the player.
        /// </summary>
        /// <param name="bindHand">Whether the hand track's reference resolves; false leaves it unresolved.</param>
        public HierarchyPlayerScene(bool bindHand) {
            ContentRoot = Path.Combine(AppContext.BaseDirectory, "hanim-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ContentRoot, "anim"));
            using (FileStream file = File.Create(Path.Combine(ContentRoot, HierarchyAnimationTestClips.ClipPath))) {
                HierarchyAnimationTestClips.WritePackaged(file, HierarchyAnimationTestClips.Sample());
            }

            Core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(ContentRoot)
            });
            Core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            AnimationRuntimeRegistration.Register(Core);

            Root = CreateEntity(1u);
            Root.InitChildren();
            Arm = CreateEntity(10u);
            Arm.InitChildren();
            Hand = CreateEntity(11u);
            Root.AddChild(Arm);
            Arm.AddChild(Hand);

            SceneEntityReference armReference = new SceneEntityReference { EntityId = 10u };
            SceneEntityReference handReference = new SceneEntityReference { EntityId = 11u };
            using RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(armReference, "animation.hierarchy-player");
            if (bindHand) {
                fixups.Track(handReference, "animation.hierarchy-player");
            }
            fixups.Bind(new List<Entity> { Root });

            Player = new HierarchyAnimationPlayerComponent {
                ClipPath = HierarchyAnimationTestClips.ClipPath,
                TrackTargets = new SceneEntityReference[] { armReference, handReference }
            };
            Root.AddComponent(Player);
        }

        /// <summary>
        /// Gets the headless core.
        /// </summary>
        public Core Core { get; }

        /// <summary>
        /// Gets the folder the core loads content from.
        /// </summary>
        public string ContentRoot { get; }

        /// <summary>
        /// Gets the root entity carrying the player.
        /// </summary>
        public Entity Root { get; }

        /// <summary>
        /// Gets the child entity the <c>Arm</c> track drives.
        /// </summary>
        public Entity Arm { get; }

        /// <summary>
        /// Gets the grandchild entity the <c>Arm/Hand</c> track drives.
        /// </summary>
        public Entity Hand { get; }

        /// <summary>
        /// Gets the player under test.
        /// </summary>
        public HierarchyAnimationPlayerComponent Player { get; }

        /// <summary>
        /// Initializes the hierarchy, which loads the clip and resolves the track targets.
        /// </summary>
        public void Initialize() {
            Root.InitializeHierarchy();
        }

        /// <summary>
        /// Disposes the core and deletes the content folder.
        /// </summary>
        public void Dispose() {
            Core.Dispose();
            Directory.Delete(ContentRoot, true);
        }

        /// <summary>
        /// Creates an entity carrying a scene runtime id so track references can resolve to it.
        /// </summary>
        /// <param name="sceneId">Scene entity id.</param>
        /// <returns>The entity.</returns>
        Entity CreateEntity(uint sceneId) {
            Entity entity = new Entity(Core);
            entity.InitComponents();
            entity.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = sceneId });
            return entity;
        }
    }
}
