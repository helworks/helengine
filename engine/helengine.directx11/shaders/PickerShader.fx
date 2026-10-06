struct VS_INPUT
{
    float3 pos    : POSITION;
    float3 normal : NORMAL;   // unused, retained to match the standard 3D vertex layout
    float2 uv     : TEXCOORD0;
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

cbuffer PickerBuffer : register(b0)
{
    matrix worldViewProj;
    float4 pickColor;
    float4 alphaTest;
};

Texture2D pickTexture : register(t0);
SamplerState pickSampler : register(s0);

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(float4(input.pos, 1.0), worldViewProj);
    output.uv = input.uv;
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    if (alphaTest.x > 0.5) {
        clip(pickTexture.Sample(pickSampler, input.uv).a - alphaTest.y);
    }
    return pickColor;
}
