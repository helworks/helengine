float3 SrgbToLinear(float3 value) {return lerp(value/12.92,pow((value+.055)/1.055,2.4),step(.04045,value));}
float3 LinearToSrgb(float3 value) {value=max(value,0);return lerp(value*12.92,1.055*pow(value,1.0/2.4)-.055,step(.0031308,value));}
float2 SourceUv(float2 displayUv,float rotation) {
    if(rotation==90) {return float2(1-displayUv.y,displayUv.x);}
    if(rotation==180) {return 1-displayUv;}
    if(rotation==270) {return float2(displayUv.y,1-displayUv.x);}
    return displayUv;
}
float4 LinearPremultiplied(float4 color,float inputIsLinear) {return float4((inputIsLinear>0?color.rgb:SrgbToLinear(color.rgb))*color.a,color.a);}
float4 SamplePremultiplied(Texture2D<float4> image,float2 uv,float inputIsLinear) {
    uint width,height;image.GetDimensions(width,height);float2 samplePosition=uv*float2(width,height)-.5;
    int2 low=(int2)floor(samplePosition);float2 fraction=frac(samplePosition);int2 size=int2(width,height)-1;
    float4 a=LinearPremultiplied(image.Load(int3(clamp(low,int2(0,0),size),0)),inputIsLinear);
    float4 b=LinearPremultiplied(image.Load(int3(clamp(low+int2(1,0),int2(0,0),size),0)),inputIsLinear);
    float4 c=LinearPremultiplied(image.Load(int3(clamp(low+int2(0,1),int2(0,0),size),0)),inputIsLinear);
    float4 d=LinearPremultiplied(image.Load(int3(clamp(low+int2(1,1),int2(0,0),size),0)),inputIsLinear);
    return lerp(lerp(a,b,fraction.x),lerp(c,d,fraction.x),fraction.y);
}
