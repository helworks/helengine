float MaskValue(Texture2D<float4> image,float2 uv,float channel,float inverted,float rotation) {
    uv=SourceUv(uv,rotation);uint width,height;image.GetDimensions(width,height);
    int2 samplePosition=clamp((int2)(uv*float2(width,height)),int2(0,0),int2(width-1,height-1));float4 value=image.Load(int3(samplePosition,0));
    float coverage=channel>0 ? dot(value.rgb,float3(.2126,.7152,.0722)) : value.a;
    return inverted>0 ? 1-coverage : coverage;
}
