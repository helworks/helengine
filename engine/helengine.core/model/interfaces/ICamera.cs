namespace helengine {
    public interface ICamera {
        Entity Parent { get; }

        byte CameraDrawOrder { get; set; }

        float4 Viewport { get; set; }

        RenderBucket2D[] RenderBuckets2D { get; }
        RenderBucket3D[][][] RenderBuckets3D { get; }

        ushort LayerMask { get; set; }
    }
}
