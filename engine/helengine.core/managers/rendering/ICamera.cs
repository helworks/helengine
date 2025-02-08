namespace helengine {
    public interface ICamera {
        byte CameraDrawOrder { get; set; }

        float4 Viewport { get; set; }
    }
}
