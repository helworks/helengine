// Host-supplied viewport mapping; the painting library remains independent of UIKit coordinates.
cbuffer Display : register(b0) { float2 ViewSize; float2 Origin; float2 Extent; float2 Padding; };
// Host triangulates a generic polygon; coordinates are normalized against the current surface bounds.
cbuffer Clip : register(b1) { float4 ClipVertices[930]; };
Texture2D<float4> Artwork : register(t0);
SamplerState LinearClamp : register(s0);
struct DisplayVertex { float4 Position : SV_Position; float2 UV : TEXCOORD0; };
DisplayVertex DisplayVertexMain(uint vertexID : SV_VertexID) {
    float2 uv=ClipVertices[vertexID].xy; float2 pixel=Origin+uv*Extent;
    DisplayVertex result;
    result.Position=float4(pixel.x/ViewSize.x*2-1,1-pixel.y/ViewSize.y*2,0,1);
    result.UV=uv; return result;
}
float4 DisplayFragmentMain(DisplayVertex input) : SV_Target0 {
    float4 ink=Artwork.Sample(LinearClamp,input.UV);
    return float4(ink.rgb+(1-ink.a),1);
}
