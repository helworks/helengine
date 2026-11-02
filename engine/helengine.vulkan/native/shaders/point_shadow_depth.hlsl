// Backend-internal equivalent of engine/helengine.editor/shaders/builtin/EditorPointShadowDepth.hlsl.
// Preserve the existing fixed GPU32 PointShadowTransformBuffer ABI: world64 + WVP64 + light/range16.
cbuffer PointShadowTransformBuffer : register(b0) {
    float4x4 world;
    float4x4 worldViewProj;
    float4 lightPositionAndRange;
};
struct VertexInput { float3 position : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
struct VertexOutput { float4 position : SV_POSITION; float3 worldPosition : TEXCOORD0; };
VertexOutput VS(VertexInput input) {
    VertexOutput output;
    output.position = mul(float4(input.position, 1.0f), worldViewProj);
    output.worldPosition = mul(float4(input.position, 1.0f), world).xyz;
    return output;
}
float4 PS(VertexOutput input) : SV_Target {
    float range = max(lightPositionAndRange.w, 0.0001f);
    float depth = saturate(length(input.worldPosition - lightPositionAndRange.xyz) / range);
    return float4(depth, 0.0f, 0.0f, 1.0f);
}
