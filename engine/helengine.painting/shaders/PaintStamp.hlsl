// 48-byte instance: legacy geometry/color plus version-two inward edge softness.
struct Stamp { float4 Geometry; float4 Color; float4 Options; };
cbuffer Instances : register(b0) { Stamp Stamps[1024]; };
cbuffer Canvas : register(b1) { float2 CanvasSize; float2 Padding; };
// Numeric face mask generated from bounded canvas-clipped triangles; no app geometry or brush algorithm lives here.
cbuffer FaceClip : register(b2) { float4 ClipMeta; float4 ClipPoints[930]; };
struct StampVertex { float4 Position : SV_Position; float2 UV : TEXCOORD0; float4 Color : COLOR0; float Blur : TEXCOORD1; float FillCanvas : TEXCOORD2; };
StampVertex PaintVertex(uint vertexID : SV_VertexID,uint instanceID : SV_InstanceID) {
    const float2 corners[6]={float2(-1,-1),float2(1,-1),float2(-1,1),float2(1,-1),float2(1,1),float2(-1,1)};
    Stamp stamp=Stamps[instanceID];
    float2 uv=corners[vertexID];
    float2 radius=max(stamp.Geometry.zw,float2(0.001,0.001));
    float2 expanded=radius+1;
    float2 pixel=stamp.Geometry.xy+uv*expanded;
    StampVertex result;
    result.Position=float4(pixel.x/CanvasSize.x*2-1,1-pixel.y/CanvasSize.y*2,0,1);
    result.UV=uv*expanded/radius;
    result.Color=stamp.Color;
    result.Blur=stamp.Options.x;
    result.FillCanvas=stamp.Options.y;
    return result;
}
float4 PaintFragment(StampVertex input) : SV_Target0 {
    // Evaluate derivatives before the varying face-mask discard, preserving brush edge coverage on mask boundaries.
    float distance=length(input.UV);
    float edge=max(fwidth(distance),0.001);
    float alpha=input.Color.a*saturate((1-distance)/edge+0.5);
    // Keep blur zero bit-for-bit on the old path. Same curve as PaintBrushCoverage.Evaluate.
    if(input.Blur>0) {
        float t=saturate((distance-(1-input.Blur))/input.Blur);
        alpha*=1-t*t*(3-2*t);
    }
    if(input.FillCanvas>0.5) alpha=input.Color.a;
    float2 sampleUv=input.Position.xy/CanvasSize;
    bool inside=false;
    [loop] for(int index=0;index<(int)ClipMeta.x;index++) {
        float2 a=ClipPoints[index*3].xy,b=ClipPoints[index*3+1].xy,c=ClipPoints[index*3+2].xy;
        float2 ab=b-a,bc=c-b,ca=a-c,ap=sampleUv-a,bp=sampleUv-b,cp=sampleUv-c;
        if(ab.x*(c.y-a.y)-ab.y*(c.x-a.x)==0) continue;
        float x=ab.x*ap.y-ab.y*ap.x,y=bc.x*bp.y-bc.y*bp.x,z=ca.x*cp.y-ca.y*cp.x;
        if((x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0)) { inside=true; break; }
    }
    if(!inside) discard;
    return float4(input.Color.rgb*alpha,alpha);
}
