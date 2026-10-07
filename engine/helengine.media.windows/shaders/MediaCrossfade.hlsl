Texture2D<float4> Outgoing : register(t0);
Texture2D<float4> Incoming : register(t1);
cbuffer FadeData : register(b0) {float4 Fade;}
struct VertexOutput {float4 Position : SV_POSITION;};
float4 CrossfadePS(VertexOutput input) : SV_TARGET {
    int3 location=int3((int2)input.Position.xy,0);
    float4 a=Outgoing.Load(location);float4 b=Incoming.Load(location);
    float4 result=lerp(float4(a.rgb*a.a,a.a),float4(b.rgb*b.a,b.a),Fade.x);
    return result.a>0?float4(result.rgb/result.a,result.a):0;
}
