// Backend-internal equivalent of engine/helengine.editor/shaders/builtin/EditorShadowDepth.hlsl.
// Preserve the existing fixed GPU32 ShadowTransformBuffer ABI: 64-byte matrix plus 16-byte color.
cbuffer ShadowTransformBuffer : register(b0) {
    float4x4 worldViewProj;
    float4 color;
};
struct VertexInput { float3 position : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
struct VertexOutput { float4 position : SV_POSITION; };
VertexOutput VS(VertexInput input) {
    VertexOutput output;
    output.position = mul(float4(input.position, 1.0f), worldViewProj);
    return output;
}
void PS(VertexOutput input) { }
