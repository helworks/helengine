float MaskCoverage(float4 value,float channel,float inverted) {
    float coverage=channel>0 ? dot(value.rgb,float3(.2126,.7152,.0722)) : value.a;
    return inverted>0 ? 1-coverage : coverage;
}
float MaskValue(Texture2D<float4> image,float2 uv,float channel,float inverted,float rotation) {
    uv=SourceUv(uv,rotation);uint width,height;image.GetDimensions(width,height);
    // Bilinear between the four nearest texels (clamped at the edges): mattes are often smaller than the layer they cut.
    float2 position=uv*float2(width,height)-.5;int2 corner=(int2)floor(position);float2 blend=position-corner;int2 limit=int2(width-1,height-1);
    float topLeft=MaskCoverage(image.Load(int3(clamp(corner,int2(0,0),limit),0)),channel,inverted);
    float topRight=MaskCoverage(image.Load(int3(clamp(corner+int2(1,0),int2(0,0),limit),0)),channel,inverted);
    float bottomLeft=MaskCoverage(image.Load(int3(clamp(corner+int2(0,1),int2(0,0),limit),0)),channel,inverted);
    float bottomRight=MaskCoverage(image.Load(int3(clamp(corner+int2(1,1),int2(0,0),limit),0)),channel,inverted);
    return lerp(lerp(topLeft,topRight,blend.x),lerp(bottomLeft,bottomRight,blend.x),blend.y);
}
