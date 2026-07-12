cbuffer PointShadowTransformBuffer : register(b0)
{
    float4x4 world;
    float4x4 worldViewProj;
    float4 lightPositionAndRange;
};

struct VS_IN
{
    float3 pos : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    float4 worldPosition = mul(float4(input.pos, 1.0f), world);
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.worldPos = worldPosition.xyz;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float shadowRange = max(lightPositionAndRange.w, 0.0001f);
    float normalizedDistance = saturate(length(input.worldPos - lightPositionAndRange.xyz) / shadowRange);
    return float4(normalizedDistance, 0.0f, 0.0f, 1.0f);
}
