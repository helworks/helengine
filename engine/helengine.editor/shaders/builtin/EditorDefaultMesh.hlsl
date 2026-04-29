cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
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
    float3 normal : NORMAL;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.normal = input.normal;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float3 displayNormal = normalize(input.normal) * 0.5f + 0.5f;
    return float4(displayNormal, 1.0f);
}
