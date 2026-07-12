cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
};

Texture2D CanvasTexture : register(t0);
SamplerState CanvasSampler : register(s0);

struct VS_IN
{
    float3 pos : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float2 texCoord : TEXCOORD0;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.texCoord = input.texCoord;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float4 canvas = CanvasTexture.Sample(CanvasSampler, input.texCoord);
    if (canvas.a <= 0.0001f)
    {
        discard;
    }

    return canvas;
}
