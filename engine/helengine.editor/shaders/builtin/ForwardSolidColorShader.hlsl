cbuffer HelenginePerDraw : register(b0)
{
    float4x4 HelengineWorldViewProjection;
    float4 HelengineBaseColor;
};

struct VS_IN
{
    float3 pos : POSITION;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), HelengineWorldViewProjection);
    return output;
}

float4 PS(PS_IN input) : SV_TARGET
{
    return HelengineBaseColor;
}
