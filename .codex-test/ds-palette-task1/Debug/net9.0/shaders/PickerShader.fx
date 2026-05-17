struct VS_INPUT
{
    float3 pos    : POSITION;
    float3 normal : NORMAL;   // unused, retained to match the standard 3D vertex layout
    float2 uv     : TEXCOORD0; // unused, retained to match the standard 3D vertex layout
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
};

cbuffer PickerBuffer : register(b0)
{
    matrix worldViewProj;
    float4 pickColor;
};

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(float4(input.pos, 1.0), worldViewProj);
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    return pickColor;
}
