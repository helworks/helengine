namespace helengine {
    public interface ICamera {
        Entity Parent { get; }

        byte CameraDrawOrder { get; set; }

        float4 Viewport { get; set; }
    }
}
