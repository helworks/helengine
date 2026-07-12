cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
};

cbuffer BorderParams : register(b1)
{
    float4 borderColor;
    float4 borderMetrics;
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
    float thicknessU = borderMetrics.x;
    float thicknessV = borderMetrics.y;
    bool insideHorizontal = input.texCoord.x > thicknessU && input.texCoord.x < (1.0f - thicknessU);
    bool insideVertical = input.texCoord.y > thicknessV && input.texCoord.y < (1.0f - thicknessV);
    if (insideHorizontal && insideVertical)
    {
        discard;
    }

    return borderColor;
}
