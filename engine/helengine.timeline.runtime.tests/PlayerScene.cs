namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// A headless core with the four entities <see cref="SampleCookedTimelines.Rich"/> binds (hero, lamp, door, actor),
    /// a director entity carrying the player and an event listener, slot references resolved through the same fixup
    /// path scene loading uses, and in-memory audio and animation assets.
    /// </summary>
    sealed class PlayerScene : IDisposable {
        /// <summary>
        /// Initializes the scene for one cooked timeline and audio backend.
        /// </summary>
        /// <param name="timeline">Timeline the player plays.</param>
        /// <param name="backend">Audio backend installed on the core.</param>
        public PlayerScene(CookedTimelineAsset timeline, IAudioBackend backend) {
            Core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            Core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Core.SetAudioBackend(backend);
            Root = CreateEntity(0u);
            Root.InitChildren();
            Hero = CreateEntity(10u);
            Hero.LocalPosition = new float3(1, 2, 3);
            Hero.LocalScale = new float3(3, 3, 3);
            Lamp = CreateEntity(11u);
            Receiver = new TestLampReceiver();
            Lamp.AddComponent(Receiver);
            Door = CreateEntity(12u);
            Actor = CreateEntity(13u);
            Animator = new AnimationPlayerComponent();
            Actor.AddComponent(Animator);
            Director = CreateEntity(14u);
            Listener = new RecordingEventListener();
            Director.AddComponent(Listener);
            Root.AddChild(Hero);
            Root.AddChild(Lamp);
            Root.AddChild(Door);
            Root.AddChild(Actor);
            Root.AddChild(Director);

            Sound = new AudioAsset();
            Animation = new AnimationClipAsset { Duration = 5f };
            Assets = new DictionaryAssetSource();
            Assets.AddAudio(SampleCookedTimelines.SoundPath, Sound);
            Assets.AddAnimation(SampleCookedTimelines.AnimationPath, Animation);

            SceneEntityReference[] slots = new SceneEntityReference[timeline.SlotCount];
            using RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            for (int index = 0; index < slots.Length; index++) {
                slots[index] = new SceneEntityReference { EntityId = 10u + (uint)index };
                fixups.Track(slots[index], "timeline.player");
            }
            fixups.Bind(new List<Entity> { Root });

            Player = new TimelinePlayerComponent { Timeline = timeline, Slots = slots, AssetSource = Assets };
            Director.AddComponent(Player);
        }

        /// <summary>
        /// Gets the headless core.
        /// </summary>
        public Core Core { get; }

        /// <summary>
        /// Gets the scene root.
        /// </summary>
        public Entity Root { get; }

        /// <summary>
        /// Gets the entity bound to the hero slot (base position 1,2,3 and scale 3).
        /// </summary>
        public Entity Hero { get; }

        /// <summary>
        /// Gets the entity bound to the lamp slot.
        /// </summary>
        public Entity Lamp { get; }

        /// <summary>
        /// Gets the receiver on the lamp.
        /// </summary>
        public TestLampReceiver Receiver { get; }

        /// <summary>
        /// Gets the entity bound to the door slot.
        /// </summary>
        public Entity Door { get; }

        /// <summary>
        /// Gets the entity bound to the actor slot.
        /// </summary>
        public Entity Actor { get; }

        /// <summary>
        /// Gets the animation player on the actor.
        /// </summary>
        public AnimationPlayerComponent Animator { get; }

        /// <summary>
        /// Gets the entity carrying the player and the event listener.
        /// </summary>
        public Entity Director { get; }

        /// <summary>
        /// Gets the event listener next to the player.
        /// </summary>
        public RecordingEventListener Listener { get; }

        /// <summary>
        /// Gets the player under test.
        /// </summary>
        public TimelinePlayerComponent Player { get; }

        /// <summary>
        /// Gets the sound served for <see cref="SampleCookedTimelines.SoundPath"/>.
        /// </summary>
        public AudioAsset Sound { get; }

        /// <summary>
        /// Gets the animation served for <see cref="SampleCookedTimelines.AnimationPath"/>.
        /// </summary>
        public AnimationClipAsset Animation { get; }

        /// <summary>
        /// Gets the in-memory asset source.
        /// </summary>
        public DictionaryAssetSource Assets { get; }

        /// <summary>
        /// Advances the player by whole ticks of the 10 Hz sample timelines.
        /// </summary>
        /// <param name="ticks">Number of ticks to advance, one update per tick.</param>
        public void AdvanceTicks(int ticks) {
            for (int index = 0; index < ticks; index++) {
                Player.Advance(0.1);
            }
        }

        /// <summary>
        /// Disposes the core.
        /// </summary>
        public void Dispose() {
            Core.Dispose();
        }

        /// <summary>
        /// Creates an entity carrying a scene runtime id so slot references can resolve to it.
        /// </summary>
        /// <param name="sceneId">Scene entity id (0 for none).</param>
        /// <returns>The entity.</returns>
        Entity CreateEntity(uint sceneId) {
            Entity entity = new Entity(Core);
            entity.InitComponents();
            if (sceneId != 0u) {
                entity.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = sceneId });
            }
            return entity;
        }
    }
}
