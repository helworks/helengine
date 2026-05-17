struct VS_INPUT
{
    float3 pos : POSITION;
    float2 uv  : TEXCOORD; // ignored
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
};

cbuffer ColorBuffer : register(b0)
{
    matrix world;
    float4 color;
};

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    // Match the UI coordinate system used by sprite/text: Y grows downward in pixels
    float3 p = input.pos;
    p.y = -p.y;
    output.pos = mul(float4(p, 1.0), world);
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    return color;
}
