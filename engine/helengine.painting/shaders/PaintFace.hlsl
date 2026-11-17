// Uses the engine's existing fixed 224-byte transform binding and top-origin mesh UVs.
cbuffer TransformBuffer : register(b0) {
    float4x4 World;
    float4x4 WorldViewProjection;
    float4x4 WorldInverseTranspose;
    float4 CameraPosition;
    float4 MaterialFeatures;
};
cbuffer PaperAppearance : register(b1) {
    float4 BaseColor;
    // X selects raster-front convention (+1 for preserved indices); Y/Z map original base UVs into the physical paint canvas.
    float4 PrintedSide;
};
Texture2D<float4> BaseImage : register(t0);
Texture2D<float4> Overlay : register(t1);
SamplerState LinearClamp : register(s0);
struct FaceInput { float3 Position : POSITION0; float3 Normal : NORMAL0; float2 UV : TEXCOORD0; };
struct FaceVertex { float4 Position : SV_Position; float2 UV : TEXCOORD0; };
FaceVertex PaintFaceVertex(FaceInput input) {
    FaceVertex result;
    result.Position=mul(float4(input.Position,1),WorldViewProjection);
    result.UV=input.UV;
    return result;
}
float4 PaintFaceFragment(FaceVertex input,bool front : SV_IsFrontFace) : SV_Target0 {
    // Offscreen layers are premultiplied; the normal AlphaBlend pipeline expects straight output.
    if(PrintedSide.w>0.5) {
        float4 layer=Overlay.Sample(LinearClamp,input.UV);
        return float4(layer.a>0 ? layer.rgb/layer.a : float3(0,0,0),layer.a);
    }
    // Renderer explicitly uses counterclockwise front faces. PrintedSide converts the preserved source winding once.
    bool printed=PrintedSide.x==0 || (PrintedSide.x>0 ? front : !front);
    if(!printed) return float4(1,1,1,1);
    float4 base=BaseImage.Sample(LinearClamp,input.UV)*BaseColor;
    float4 ink=Overlay.Sample(LinearClamp,input.UV*PrintedSide.yz);
    // Composite straight base onto opaque paper, then premultiplied ink once. Ink never mutates the base.
    float3 paper=base.rgb*base.a+(1-base.a);
    return float4(ink.rgb+paper*(1-ink.a),1);
}
