namespace helengine {
    public class EditorEntity : Entity {
        public bool Hidden { get; set; }

        public string Name { get; set; }

        public EditorEntity() {
            Name = "Entity";
        }
    }
}
