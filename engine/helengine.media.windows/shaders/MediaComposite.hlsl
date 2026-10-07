#include "MediaTransform.hlsl"
#include "MediaMask.hlsl"
Texture2D<float4> Source : register(t0);
Texture2D<float4> Mask : register(t1);
Texture2D<float4> Composite : register(t2);
cbuffer LayerData : register(b0) {
    float4 Viewport;
    float4 ImageFit;
    float4 ZoomCrop;
    float4 Transform;
    float4 Flags;
    float4 Padding;
    float4 MaskSettings;
    float4 Reserved;
}
struct VertexOutput {float4 Position : SV_POSITION;};
VertexOutput FullscreenVS(uint id : SV_VertexID) {
    VertexOutput result;float2 samplePosition=id==0?float2(-1,-1):id==1?float2(-1,3):float2(3,-1);result.Position=float4(samplePosition,0,1);return result;
}
float4 LayerPS(VertexOutput input) : SV_TARGET {
    float2 pixel=input.Position.xy;
    if(Flags.w>0 && (any(pixel<Viewport.xy) || any(pixel>=Viewport.xy+Viewport.zw))) {return 0;}
    float2 relative=pixel-(Viewport.xy+Viewport.zw*.5+Transform.xy*Viewport.zw);
    float sine=sin(Flags.x);float cosine=cos(Flags.x);
    float2 local=float2(cosine*relative.x+sine*relative.y,-sine*relative.x+cosine*relative.y)/Transform.zw+Viewport.zw*.5;
    if(any(local<0) || any(local>=Viewport.zw)) {return 0;}
    float2 fitted=ZoomCrop.xy+local/Viewport.zw*ZoomCrop.zw;
    float2 uv=(fitted-ImageFit.xy)/ImageFit.zw;
    float4 color=Padding;
    if(all(uv>=0) && all(uv<=1)) {color=SamplePremultiplied(Source,SourceUv(uv,Flags.z),Reserved.x);}
    float mask=MaskSettings.x>0 ? MaskValue(Mask,uv,MaskSettings.y,MaskSettings.z,MaskSettings.w) : 1;
    return color*(Flags.y*mask);
}
float4 FinalPS(VertexOutput input) : SV_TARGET {
    float4 color=Composite.Load(int3((int2)input.Position.xy,0));
    return color.a>0 ? float4(LinearToSrgb(color.rgb/color.a),color.a) : 0;
}

float4 LinearSourcePS(VertexOutput input) : SV_TARGET {
    float4 color=Source.Load(int3((int2)input.Position.xy,0));
    return float4(Reserved.x>0?color.rgb:SrgbToLinear(color.rgb),color.a);
}

float4 FinalLinearPS(VertexOutput input) : SV_TARGET {
    float4 color=Composite.Load(int3((int2)input.Position.xy,0));
    return color.a>0 ? float4(color.rgb/color.a,color.a) : 0;
}
