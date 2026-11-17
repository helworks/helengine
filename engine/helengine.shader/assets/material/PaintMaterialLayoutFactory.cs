namespace helengine {
    /// <summary>Defines the fixed binding contract shared by the cooked generic paper shader and native material registration.</summary>
    public static class PaintMaterialLayoutFactory {
        /// <summary>Creates an owned opaque, depth-writing, two-sided layout without loading shaders or allocating GPU resources.</summary>
        [NativeOwnedReturn]
        public static MaterialLayout Create() { return CreateWithTransparency(false); }
        /// <summary>Creates a two-sided alpha-blended layer layout without writing depth behind transparent pixels.</summary>
        [NativeOwnedReturn]
        public static MaterialLayout CreateTransparentLayer() { return CreateWithTransparency(true); }
        /// <summary>Shares shader bindings while explicitly selecting the compositing contract.</summary>
        [NativeOwnedReturn]
        static MaterialLayout CreateWithTransparency(bool transparent) {
            return new MaterialLayout("helengine.painting.face", "PaintFaceVertex", "PaintFaceFragment", "default",
                new MaterialRenderState { BlendMode = transparent ? MaterialBlendMode.AlphaBlend : MaterialBlendMode.Opaque, CullMode = MaterialCullMode.None, DepthTestEnabled = true, DepthWriteEnabled = !transparent },
                new MaterialLayoutBinding[] {
                    new MaterialLayoutBinding("BaseImage", ShaderResourceType.Texture2D, 0, 100, 0),
                    new MaterialLayoutBinding("Overlay", ShaderResourceType.Texture2D, 0, 101, 0)
                },
                new MaterialLayoutBinding[] {
                    new MaterialLayoutBinding("TransformBuffer", ShaderResourceType.ConstantBuffer, 0, 0, 224),
                    new MaterialLayoutBinding("PaperAppearance", ShaderResourceType.ConstantBuffer, 0, 1, 32)
                },
                new MaterialLayoutBinding[] { new MaterialLayoutBinding("LinearClamp", ShaderResourceType.Sampler, 0, 200, 0) });
        }
    }
}
