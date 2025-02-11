namespace helengine {
    public interface IUpdateable {
        byte UpdateOrder { get; set; }

        void Update();
    }
}
