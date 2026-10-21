namespace helengine.core.tests {
    /// <summary>Creates authored UI in an initialization callback to exercise nested attachment preparation.</summary>
    public sealed class CookedLayoutChildSpawner : Component {
        /// <summary>Exposes the child geometry for the single-pass assertion.</summary>
        public RoundedRectComponent CreatedRectangle { get; private set; }
        /// <summary>Attaches a newly initialized grandchild while the outer child is still initializing.</summary>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);
            Entity child = new Entity(entity.OwnerCore);
            child.InitChildren();
            child.InitComponents();
            CreatedRectangle = new RoundedRectComponent { Size = new int2(200, 80) };
            child.AddComponent(CreatedRectangle);
            entity.AddChild(child);
        }
    }
}
