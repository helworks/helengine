namespace helengine {
    public class Component {
        public Entity Parent { get; private set; }
        
        public virtual void ComponentAdded(Entity entity) {
            Parent = entity;
        }

        public virtual void ParentEnabledChange(bool newEnabled) {
        }

        public virtual void ParentStaticChange(bool newEnabled) {
        }
    }
}
