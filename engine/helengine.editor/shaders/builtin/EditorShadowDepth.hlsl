cbuffer ShadowTransformBuffer : register(b0)
{
    float4x4 worldViewProj;
    float4 color;
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
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    return output;
}

void PS(PS_IN input)
{
}
